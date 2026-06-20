using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using NUnit.Framework;
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
public partial class PlayerPlaceManyPointSystem : SystemBase
{
    [Inject] BuildingObjectFactory _factorty;
    [Inject] VisualBuildingFactory _visualBuildingFactory;
    int _buildingID;
    bool isSecondPoint;
    Vector3Int _pos;
    Vector3Int _cachedPos;
    int _cachedRot;
    Vector3Int _firstPos;
    EntityQuery _buildReadyQuery;
    IPlaceBuildingPlayerData _placeManyPointPlayerData;
    ManyPointsBuildingInstanced _manyPointBuilding;
    PhantomObject _preview;
    List<int3> _manyPointBuildingPoints;
    
    Entity _playerState;
    bool _isProcessing = false;
    bool _isStraight;
    public  Action onBuildingDone;
    public bool canBuild{get;private set;}
    public bool updateDraw;
    public void SetUpBuilding(int buildingID,IPlaceBuildingPlayerData placeManyPointPlayerData, Entity playerState)
    {
        if(_isProcessing || _manyPointBuilding != null || EntityManager.IsComponentEnabled<PlayerPlacingManyPointBuilding>(playerState)||EntityManager.IsComponentEnabled<PlayerDeletePoints>(playerState)) return;
        _isProcessing = true; 
        _manyPointBuildingPoints?.Clear();
        _manyPointBuildingPoints=new();
        _buildingID=buildingID;
        _placeManyPointPlayerData=placeManyPointPlayerData;
        var data =SystemAPI.GetSingleton<PlayerRayCastData>();
        _manyPointBuilding = _factorty.CreateManyPoint(_buildingID,new Vector3Int[]{new Vector3Int(data.PlaceBlockPos.x,data.PlaceBlockPos.y,data.PlaceBlockPos.z)},null,true);
        _preview=_visualBuildingFactory.PhantomizeObject(_manyPointBuilding.gameObject);
        EntityManager.SetComponentEnabled<PlayerPlacingManyPointBuilding>(playerState,true);
        
        _playerState=playerState;
    }

    protected override void OnCreate()
    {
        _buildReadyQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<PlayerCommand>()
        .WithAll<PlayerPlacingManyPointBuilding>()
        .WithPresent<PathfindingRequest>()
        .WithDisabled<PlayerPlacingBuilding>() 
        .WithDisabled<PlayerDeletePoints>() 
        
        .Build(this);
        RequireForUpdate(_buildReadyQuery);
    }
    protected override void OnUpdate()
    {
        
        var data =SystemAPI.GetSingleton<PlayerRayCastData>();
        var mapData = SystemAPI.GetSingleton<BuildingMap>();
        var buildingConfig = SystemAPI.GetSingleton<BuildingConfigReference>();
        if(_buildReadyQuery.IsEmpty) return;
        else
        {
            var ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            var ecb = ecbSystem.CreateCommandBuffer(); 
            if (_manyPointBuilding == null) return;
            _manyPointBuildingPoints.Clear();
            var playerCommand = SystemAPI.GetSingletonEntity<PlayerCommand>();
            _pos=new Vector3Int(data.PlaceBlockPos.x,data.PlaceBlockPos.y,data.PlaceBlockPos.z);
            if (!isSecondPoint)
            {
                _firstPos=_pos;
                _manyPointBuilding.Generate(new Vector3Int[]{_firstPos},null);
                var pos=new int3(_pos.x,_pos.y,_pos.z);
                
                _preview.CanBuild(!(mapData.CellMapBuildingsIDs.ContainsKey(pos)&&mapData.CellMapBuildingsIDs[pos]!=_buildingID),_placeManyPointPlayerData.isForce);
            }
            else
            {
                
                if(_pos!=_cachedPos||_cachedRot!=_placeManyPointPlayerData.rotation||updateDraw)
                {
                    Debug.Log("dssddssd");
                    _cachedRot=_placeManyPointPlayerData.rotation;
                    ecb.SetComponent(playerCommand,new PathfindingRequest{BuildingID=_buildingID,Start=new int3(_firstPos.x,_firstPos.y,_firstPos.z),End=new int3(_pos.x,_pos.y,_pos.z),SamePerfer= _placeManyPointPlayerData.rotation%2==0,straigh=_isStraight});
                    ecb.SetComponentEnabled<PathfindingRequest>(playerCommand,true);
                    _cachedPos=_pos;
                }
                var buff=EntityManager.GetBuffer<MapPoint>(playerCommand,true);
                
                foreach(var p in buff)
                {
                    _manyPointBuildingPoints.Add(p.pos);
                }
              
                _manyPointBuilding.Generate(_manyPointBuildingPoints.Select(f=>new Vector3Int(f.x,f.y,f.z)).ToArray(),null);
                
                _preview.CanBuild(true,_placeManyPointPlayerData.isForce);
            }
        }
        

    } 
    public void Rotate(bool isHold)
    {
        if(isHold)
        {
            _isStraight=!_isStraight;
            updateDraw=true;
        }
    }
    public void PlaceManyPoint(bool IsHold,bool IsBlueprint)
    {
        var ecb = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>().CreateCommandBuffer();
        
        var mapData = SystemAPI.GetSingleton<BuildingMap>();
        var buildingConfig = SystemAPI.GetSingleton<BuildingConfigReference>();
        if (isSecondPoint)
        {
            var command = ecb.CreateEntity();
            ecb.AddComponent(command,new ProcessManyPointPointsEventTag{buildingID=_buildingID});
            ecb.AddComponent<IsBlueprint>(command);
            ecb.SetComponentEnabled<IsBlueprint>(command,IsBlueprint);//IsBlueprint
            var buff = ecb.AddBuffer<MapPoint>(command);
            foreach (var p in _manyPointBuildingPoints)
            {
                
                buff.Add(new MapPoint{pos=p});
            }

            if (IsHold)
            {
                _firstPos = _pos;
                _manyPointBuildingPoints.Clear();
            }
            else
            {
                isSecondPoint = false;
                Back();
            }
        }
        else
        {
            var pos=new int3(_pos.x,_pos.y,_pos.z);
            if(mapData.CellMapBuildingsIDs.ContainsKey(pos)&&mapData.CellMapBuildingsIDs[pos]!=_buildingID) return;
            _firstPos = _pos;
            isSecondPoint = true;
        }
    }

    public void Back()
    {
        if(isSecondPoint)
        {
            
            var playerCommand = SystemAPI.GetSingletonEntity<PlayerCommand>();
            isSecondPoint=false;
            _manyPointBuildingPoints.Clear();
            var pBuff=EntityManager.GetBuffer<MapPoint>(playerCommand,false);
            pBuff.Clear();
        }
        else
        {
            _manyPointBuildingPoints.Clear();
            _placeManyPointPlayerData=null;
            if(_manyPointBuilding!=null)GameObject.DestroyImmediate(_manyPointBuilding.gameObject);
            _manyPointBuilding=null;
            _preview=null;
            _buildingID=-1;
            _pos=new Vector3Int(-1,-1,-1);
            EntityManager.SetComponentEnabled<PlayerPlacingManyPointBuilding>(_playerState,false);
            
             Debug.Log("вызов");
            onBuildingDone?.Invoke();
            _isProcessing=false;
            onBuildingDone=null;
        }

    }
}