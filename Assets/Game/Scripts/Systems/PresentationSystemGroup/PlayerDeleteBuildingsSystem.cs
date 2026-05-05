using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

[DisableAutoCreation]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(GridUpdateSystem))]
public partial class PlayerDeleteBuildingsSystem : SystemBase
{
    [Inject] BuildingObjectFactory _factorty;
    [Inject] VisualBuildingFactory _visualBuildingFactory;
    [Inject] IReadOnlyBuildingInfo info;

    public Action onBuildingDone;
    public DeleteType DeleteType { get; private set; }

    HashSet<MapPoint> _points;

    bool isSecondPoint;

    Vector3Int _pos;
    Vector3Int _cachedPos;
    Vector3Int _firstPos;

    EntityQuery _removeQuery;
    IPlaceBuildingPlayerData _playerData;

    ManyPointsBuildingInstanced _manyPointBuilding;
    GameObject _destoryArea;
    PhantomObject _preview;

    Entity _playerState;
    bool _isProcessing = false;

    Action CollectPoints;
    Action BackAction;

    int _buildingID;
    Dictionary<int, BuildingBaseConfig> _availableDeleteBuildings;
    int _currentType;

    Vector3 _baseSize;

    public void SetUpDelete(DeleteType deleteType, IPlaceBuildingPlayerData playerData, Entity playerState)
    {
        
        var data =SystemAPI.GetSingleton<PlayerRayCastData>();
        var tPos= new Vector3Int(data.PlaceBlockPos.x,data.PlaceBlockPos.y,data.PlaceBlockPos.z);
        if (_isProcessing ||
            EntityManager.IsComponentEnabled<PlayerPlacingBuilding>(playerState) ||
            EntityManager.IsComponentEnabled<PlayerPlacingManyPointBuilding>(playerState)) return;

        _availableDeleteBuildings = info.BuildingInfos
            .Where(x => x.Value.actionType == ActionType.TwoPointBuilding)
            .ToDictionary(x => x.Key, x => x.Value);

        _currentType = 0;
        _buildingID = _availableDeleteBuildings.Keys.ElementAt(_currentType);

        _playerData = playerData;

        switch (deleteType)
        {
            case DeleteType.DeleteManyPointBuilding:
                CollectPoints = CollectPointsForManyPoint;
                BackAction = BackForManyPoint;
                _manyPointBuilding = _factorty.CreateManyPoint("Road".GetStableHashCode(), new Vector3Int[] {tPos}, null, true);
                _preview = _visualBuildingFactory.PhantomizeObject(_manyPointBuilding.gameObject);
                break;

            case DeleteType.DeleteBuilding:
                BackAction = BackForBuilding;
                break;

            case DeleteType.DeleteManyPoints:
                CollectPoints = CollectManyPoints;
                BackAction = BackForMany;
                _destoryArea = _factorty.CreatePrimitive(tPos, true);
                _preview = _visualBuildingFactory.PhantomizeObject(_destoryArea);
                _baseSize = _destoryArea.transform.localScale;
                break;
        }

        _preview?.CanBuild(false, false);

        DeleteType = deleteType;
        EntityManager.SetComponentEnabled<PlayerDeletePoints>(playerState, true);

        _playerState = playerState;
        _isProcessing = true;

        _points?.Clear();
        _points = new();
    }
    public void Rotate(bool isHold)
    {
        if (!isHold) return;

        _currentType++;
        _currentType %= _availableDeleteBuildings.Count;

        _buildingID = _availableDeleteBuildings.Keys.ElementAt(_currentType);
    }
    protected override void OnCreate()
    {
        _removeQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<PlayerCommand>()
            .WithAll<PlayerDeletePoints>()
            .WithDisabled<PlayerPlacingManyPointBuilding>()
            .WithDisabled<PlayerPlacingBuilding>()
            .Build(this);

        RequireForUpdate(_removeQuery);
    }

    protected override void OnUpdate()
    {
        if (_removeQuery.IsEmpty) return;
          var data =SystemAPI.GetSingleton<PlayerRayCastData>();
        _pos = new Vector3Int(data.PlaceBlockPos.x,data.PlaceBlockPos.y,data.PlaceBlockPos.z);
        Collect();
    }

    public void DeletePoints(bool isHold, bool isForce)
    {
        var ecb = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>().CreateCommandBuffer();

        if (DeleteType != DeleteType.DeleteBuilding)
        {
            if (isSecondPoint)
            {
                var command = ecb.CreateEntity();

                if (DeleteType == DeleteType.DeleteManyPointBuilding)
                    ecb.AddComponent(command, new DeleteManyPointsBuildingFromMap { isForce = isForce, buildingID = _buildingID });
                else
                    ecb.AddComponent(command, new DeleteManyPointsFromMap { isForce = isForce });

                var buff = ecb.AddBuffer<MapPoint>(command);

                foreach (var p in _points)
                    buff.Add(p);

                if (isHold)
                {
                    _firstPos = _pos;
                    _points.Clear();
                }
                else
                {
                    isSecondPoint = false;
                    Back();
                }
            }
            else
            {
                CollectPoints();
                isSecondPoint = true;
            }
        }
        else
        {
            var pos = new int3(_pos.x, _pos.y, _pos.z);
            var mapData = SystemAPI.GetSingleton<BuildingMap>();

            if (mapData.CellMapEntites.ContainsKey(pos))
            {
                var entity = mapData.CellMapEntites[pos];
                var data = EntityManager.GetComponentData<BuildingData>(entity);

                if (info.BuildingInfos[data.BuildingIDHash].buildingType == BuildingsTypes.Special) return;

                if (isForce)
                {
                    Entity command = ecb.CreateEntity();
                    ecb.AddComponent(command, new ChangeBuildingData { targetEntity = entity });
                    ecb.AddComponent(command, new MarkAsForceDestoroyData());
                }
                else
                {
                    ecb.SetComponentEnabled<ChangeDemolitionStateTag>(entity,
                        !EntityManager.IsComponentEnabled<ChangeDemolitionStateTag>(entity));
                }

                if (!isHold) Back();
            }
        }
    }

    void Collect()
    {
        if (DeleteType != DeleteType.DeleteBuilding)
            CollectPoints();
    }

    void CollectManyPoints()
    {
        _preview.CanBuild(false, _playerData.isForce);

        if (!isSecondPoint)
        {
            _firstPos = _pos;
            _factorty.MoveBuilding(_destoryArea, _pos);
        }
        else
        {
            _points.Clear();

            int xSize = math.abs(_pos.x - _firstPos.x) + 1;
            int ySize = math.abs(_pos.y - _firstPos.y) + 1;
            int zSize = math.abs(_pos.z - _firstPos.z) + 1;

            Vector3Int corner = new Vector3Int(
                math.min(_firstPos.x, _pos.x),
                math.min(_firstPos.y, _pos.y),
                math.min(_firstPos.z, _pos.z)
            );

            for (int x = 0; x < xSize; x++)
            {
                for (int y = 0; y < ySize; y++)
                {
                    for (int z = 0; z < zSize; z++)
                    {
                        var p = corner + new Vector3Int(x, y, z);

                        _points.Add(new MapPoint
                        {
                            pos = new int3(p.x, p.y, p.z)
                        });
                    }
                }
            }

            _destoryArea.transform.localScale = new Vector3(
                xSize * _baseSize.x,
                ySize * _baseSize.y,
                zSize * _baseSize.z
            );

            _factorty.MoveBuilding(_destoryArea, corner);
        }
    }

    void BackForMany()
    {
        if (isSecondPoint)
        {
            isSecondPoint = false;
            _points.Clear();

            _destoryArea.transform.localScale = _baseSize;
            _factorty.MoveBuilding(_destoryArea, _pos);
        }
        else
        {
            _points.Clear();

            if (_destoryArea != null)
                GameObject.DestroyImmediate(_destoryArea.gameObject);

            _destoryArea = null;
            _preview = null;

            _pos = new Vector3Int(-1, -1, -1);

            EntityManager.SetComponentEnabled<PlayerDeletePoints>(_playerState, false);

            onBuildingDone?.Invoke();

            _points = null;
            _playerData = null;
            _isProcessing = false;
            onBuildingDone = null;
        }
    }

    void BackForBuilding()
    {
        EntityManager.SetComponentEnabled<PlayerDeletePoints>(_playerState, false);

        onBuildingDone?.Invoke();

        _playerData = null;
        _isProcessing = false;
        onBuildingDone = null;
    }

    void CollectPointsForManyPoint()
    {
        _preview.CanBuild(false, _playerData.isForce);

        var ecb = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>().CreateCommandBuffer();

        if (!isSecondPoint)
        {
            _firstPos = _pos;
            _manyPointBuilding.Generate(new Vector3Int[] { _firstPos }, null);
        }
        else
        {
            var playerCommand = SystemAPI.GetSingletonEntity<PlayerCommand>();

            if (_pos != _cachedPos)
            {
                ecb.SetComponent(playerCommand, new PathfindingRequest
                {
                    BuildingID = _buildingID,
                    Start = new int3(_firstPos.x, _firstPos.y, _firstPos.z),
                    End = new int3(_pos.x, _pos.y, _pos.z),
                    SamePerfer = true,
                    straigh=_playerData.rotation%2==0
                });

                ecb.SetComponentEnabled<PathfindingRequest>(playerCommand, true);
                _cachedPos = _pos;
            }

            var buff = EntityManager.GetBuffer<MapPoint>(playerCommand, true);

            _points.Clear();

            foreach (var p in buff)
                _points.Add(p);

            _manyPointBuilding.Generate(
                _points.Select(f => new Vector3Int(f.pos.x, f.pos.y, f.pos.z)).ToArray(),
                null);
        }
    }

    void BackForManyPoint()
    {
        if (isSecondPoint)
        {
            var playerCommand = SystemAPI.GetSingletonEntity<PlayerCommand>();

            isSecondPoint = false;
            _points.Clear();

            var pBuff = EntityManager.GetBuffer<MapPoint>(playerCommand, false);
            pBuff.Clear();
        }
        else
        {
            _points.Clear();

            if (_manyPointBuilding != null)
                GameObject.DestroyImmediate(_manyPointBuilding.gameObject);

            _manyPointBuilding = null;
            _preview = null;

            _pos = new Vector3Int(-1, -1, -1);

            EntityManager.SetComponentEnabled<PlayerDeletePoints>(_playerState, false);

            onBuildingDone?.Invoke();

            _points = null;
            _playerData = null;
            _isProcessing = false;
            onBuildingDone = null;
        }
    }

    public void Back()
    {
        BackAction?.Invoke();
    }
}

public enum DeleteType
{
    DeleteBuilding = 1,
    DeleteManyPointBuilding = 2,
    DeleteManyPoints = 3
}