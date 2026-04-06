using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using UniRx;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
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
    public  Action onBuildingDone;
    public DeleteType DeleteType{get;private set;}
    HashSet<MapPoint> _points;
    
    bool isSecondPoint;
    Vector2Int _pos;
    Vector2Int _cachedPos;
    Vector2Int _firstPos;
    EntityQuery _removeQuery;
    IPlayerData _playerData;
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
    public void SetUpDelete(DeleteType deleteType,IPlayerData playerData,Entity playerState)
    {
        if(_isProcessing||EntityManager.IsComponentEnabled<PlayerPlacingBuilding>(playerState)||EntityManager.IsComponentEnabled<PlayerPlacingManyPointBuilding>(playerState)) return;
         _availableDeleteBuildings = info.BuildingInfos
        .Where(x => x.Value.actionType==ActionType.TwoPointBuilding)
        .ToDictionary(x => x.Key, x => x.Value);

    _currentType = 0;
    _buildingID = _availableDeleteBuildings.Keys.ElementAt(_currentType);
        _playerData=playerData;
        switch (deleteType)
        {
            case DeleteType.DeleteManyPointBuilding:
                CollectPoints=CollectPointsForManyPoint;
                BackAction=BackForManyPoint;
                _manyPointBuilding = _factorty.CreateManyPoint("Road".GetStableHashCode(),new Vector2Int[]{_playerData.pos},null,true);
                _preview=_visualBuildingFactory.PhantomizeObject(_manyPointBuilding.gameObject);
            break;
            case DeleteType.DeleteBuilding:
                BackAction=BackForBuilding;
                break;
            case DeleteType.DeleteManyPoints:
                CollectPoints=CollectManyPoints;
                BackAction=BackForMany;
                _destoryArea= _factorty.CreatePrimitive(_playerData.pos,true);
                _preview=_visualBuildingFactory.PhantomizeObject(_destoryArea);
                _baseSize=_destoryArea.transform.localScale;
            break;
        }
        
        _preview?.CanBuild(false,false);
        DeleteType=deleteType;
        EntityManager.SetComponentEnabled<PlayerDeletePoints>(playerState,true);
        _playerState=playerState;
        _isProcessing = true; 
        _points?.Clear();
        _points=new();
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
        if(_removeQuery.IsEmpty) return;
        else
        {
            _pos=_playerData.pos;
            
            Collect();
        }
    }
    public void Rotate(bool isHold)
    {
        if (!isHold) return;

        _currentType++;
        _currentType %= _availableDeleteBuildings.Count;

        _buildingID = _availableDeleteBuildings.Keys.ElementAt(_currentType);
    }
    public void DeletePoints(bool isHold,bool IsForce)
    {
        
        var ecb = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>().CreateCommandBuffer();
        if(DeleteType!=DeleteType.DeleteBuilding)
        {
            if (isSecondPoint)
            {
                
                var command=ecb.CreateEntity();
                Debug.Log("удалил "+IsForce);
                if(DeleteType==DeleteType.DeleteManyPointBuilding)
                    ecb.AddComponent(command,new DeleteManyPointsBuildingFromMap{isForce=IsForce,buildingID=_buildingID});
                else if(DeleteType==DeleteType.DeleteManyPoints)
                    ecb.AddComponent(command,new DeleteManyPointsFromMap{isForce=IsForce});
                var buff=ecb.AddBuffer<MapPoint>(command);
                foreach(var p in _points)
                {
                    buff.Add(p);
                }
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
                
                 isSecondPoint=true;
            }
        }
        else
        {
            var pos = new int2(_pos.x,_pos.y);
            var mapData = SystemAPI.GetSingleton<BuildingMap>();
            if (mapData.CellMapEntites.ContainsKey(pos))
            {
                var entity=mapData.CellMapEntites[pos];
                var data=EntityManager.GetComponentData<BuildingData>(entity);
                if(info.BuildingInfos[data.BuildingIDHash].buildingType==BuildingsTypes.Special) return;
                if (IsForce)
                {
                    Entity Command=ecb.CreateEntity();
                    ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=entity});
                    ecb.AddComponent(Command,new MarkAsForceDestoroyData());
                }
                else
                {
                    ecb.SetComponentEnabled<ChangeDemolitionStateTag>(entity,!EntityManager.IsComponentEnabled<ChangeDemolitionStateTag>(entity));
                }
                if (!isHold)
                {
                    Back();
                }
            }
        }
    }
    void Collect()
    {
        if(DeleteType!=DeleteType.DeleteBuilding)
        {
            CollectPoints();
        }
    }
    void CollectManyPoints()
    {
        
        _preview.CanBuild(false,_playerData.isForce);
         if (!isSecondPoint)
        {
            _firstPos=_pos;
            _factorty.MoveBuilding(_destoryArea,_pos);
        }
        else
        {
            _points.Clear();

            int xSize = math.abs(_pos.x - _firstPos.x) + 1;
            int ySize = math.abs(_pos.y - _firstPos.y) + 1;

            Vector2Int leftBottomCorner = new Vector2Int(
                math.min(_firstPos.x, _pos.x),
                math.min(_firstPos.y, _pos.y)
            );

            for(int i = 0; i < xSize; i++)
            {
                for(int j = 0; j < ySize; j++)
                {
                    var pos = leftBottomCorner + new Vector2Int(i, j);
                    _points.Add(new MapPoint { pos = new int2(pos.x, pos.y) });
                }
            }
            _destoryArea.transform.localScale = new Vector3(
                xSize * _baseSize.x, 
                _destoryArea.transform.localScale.y, 
                ySize * _baseSize.z
            );

            _factorty.MoveBuilding(_destoryArea, leftBottomCorner);
        }
    }
    void BackForMany()
    {
        if(isSecondPoint)
        {
            isSecondPoint=false;
            _points.Clear();
            _destoryArea.transform.localScale=_baseSize;
            _factorty.MoveBuilding(_destoryArea,_pos);
        }
        else
        {
            _points.Clear();
            if(_destoryArea!=null)GameObject.DestroyImmediate(_destoryArea.gameObject);
            _destoryArea=null;
            _preview=null;
            _pos=new Vector2Int(-1,-1);
            EntityManager.SetComponentEnabled<PlayerDeletePoints>(_playerState,false);
            onBuildingDone?.Invoke();
            _points=null;
            _playerData=null;
            _isProcessing=false;
            onBuildingDone=null;
            }
    }
    void BackForBuilding()
    {
       
        EntityManager.SetComponentEnabled<PlayerDeletePoints>(_playerState,false);
        onBuildingDone?.Invoke();
        _playerData=null;
       _isProcessing=false;
        onBuildingDone=null;
    }
    void CollectPointsForManyPoint()
    {
        
        _preview.CanBuild(false,_playerData.isForce);
        var ecb = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>().CreateCommandBuffer();
        if (!isSecondPoint)
        {
            _firstPos=_pos;
            _manyPointBuilding.Generate(new Vector2Int[]{_firstPos},null);
        }
        else
        {
             var playerCommand = SystemAPI.GetSingletonEntity<PlayerCommand>();
            if(_pos!=_cachedPos)
            {
                ecb.SetComponent(playerCommand,new PathfindingRequest{BuildingID=_buildingID, Start=new int2(_firstPos.x,_firstPos.y),End=new int2(_pos.x,_pos.y),SamePerfer=true});
                ecb.SetComponentEnabled<PathfindingRequest>(playerCommand,true);
                _cachedPos=_pos;
            }
            var buff=EntityManager.GetBuffer<MapPoint>(playerCommand,true);
            _points.Clear();
            foreach(var p in buff)
            {
                _points.Add(p);
            }
            _manyPointBuilding.Generate(_points.Select(f=>new Vector2Int(f.pos.x,f.pos.y)).ToArray(),null);
        }
    }
    void BackForManyPoint()
    {
        if(isSecondPoint)
        {
            
            var playerCommand = SystemAPI.GetSingletonEntity<PlayerCommand>();
            isSecondPoint=false;
            _points.Clear();
            var pBuff=EntityManager.GetBuffer<MapPoint>(playerCommand,false);
            pBuff.Clear();
        }
        else
        {
            _points.Clear();
            if(_manyPointBuilding!=null)GameObject.DestroyImmediate(_manyPointBuilding.gameObject);
            _manyPointBuilding=null;
            _preview=null;
            _pos=new Vector2Int(-1,-1);
            EntityManager.SetComponentEnabled<PlayerDeletePoints>(_playerState,false);
            onBuildingDone?.Invoke();
            _points=null;
            _playerData=null;
        _isProcessing=false;
            onBuildingDone=null;
        }
    }
    public void Back()
    {
        BackAction?.Invoke();
        
    }
    
}
public enum DeleteType
{
    DeleteBuilding=1,
    DeleteManyPointBuilding=2,
    DeleteManyPoints=3
}