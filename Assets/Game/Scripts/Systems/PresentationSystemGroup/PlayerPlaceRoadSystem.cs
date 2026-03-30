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
public partial class PlayerPlaceRoadSystem : SystemBase
{
    [Inject] BuildingObjectFactory _factorty;
    [Inject] VisualBuildingFactory _visualBuildingFactory;
    int _buildingID;
    bool isSecondPoint;
    Vector2Int _pos;
    Vector2Int _cachedPos;
    int _cachedRot;
    Vector2Int _firstPos;
    EntityQuery _buildReadyQuery;
    IPlaceBuildingPlayerData _placeRoadPlayerData;
    RoadOnScene _road;
    PhantomObject _preview;
    List<int2> _roadPoints;
    
    Entity _playerState;
    bool _isProcessing = false;
    public  Action onBuildingDone;
    public bool canBuild{get;private set;}
    public void SetUpBuilding(int buildingID,IPlaceBuildingPlayerData placeRoadPlayerData, Entity playerState)
    {
        if(_isProcessing || _road != null || EntityManager.IsComponentEnabled<PlayerPlacingRoad>(playerState)||EntityManager.IsComponentEnabled<PlayerDeletePoints>(playerState)) return;
        _isProcessing = true; 
        _roadPoints?.Clear();
        _roadPoints=new();
        _buildingID=buildingID;
        _placeRoadPlayerData=placeRoadPlayerData;
        _road = _factorty.CreateRoad(_buildingID,new Vector2Int[]{_placeRoadPlayerData.pos},null,true);
        _preview=_visualBuildingFactory.PhantomizeObject(_road.gameObject);
        EntityManager.SetComponentEnabled<PlayerPlacingRoad>(playerState,true);
        
        _playerState=playerState;
    }

    protected override void OnCreate()
    {
        _buildReadyQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<PlayerCommand>()
        .WithAll<PlayerPlacingRoad>()
        .WithPresent<PathfindingRequest>()
        .WithDisabled<PlayerPlacingBuilding>() 
        .WithDisabled<PlayerDeletePoints>() 
        
        .Build(this);
        RequireForUpdate(_buildReadyQuery);
    }
    protected override void OnUpdate()
    {
        
        var mapData = SystemAPI.GetSingleton<BuildingMap>();
        var buildingConfig = SystemAPI.GetSingleton<BuildingConfigReference>();
        if(_buildReadyQuery.IsEmpty) return;
        else
        {
             var ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            var ecb = ecbSystem.CreateCommandBuffer(); 
            if (_road == null) return;
            _roadPoints.Clear();
            var playerCommand = SystemAPI.GetSingletonEntity<PlayerCommand>();
            _pos=_placeRoadPlayerData.pos;
            if (!isSecondPoint)
            {
                _firstPos=_pos;
                _road.GenerateRoadMesh(new Vector2Int[]{_firstPos},null);
                var pos=new int2(_pos.x,_pos.y);
                
                _preview.CanBuild(!(mapData.CellMapBuildingsIDs.ContainsKey(pos)&&mapData.CellMapBuildingsIDs[pos]!=buildingConfig.roadID),_placeRoadPlayerData.isForce);
            }
            else
            {
                
                if(_pos!=_cachedPos||_cachedRot!=_placeRoadPlayerData.rotation)
                {
                    _cachedRot=_placeRoadPlayerData.rotation;
                    ecb.SetComponent(playerCommand,new PathfindingRequest{Start=new int2(_firstPos.x,_firstPos.y),End=new int2(_pos.x,_pos.y),RoadPerfer= _cachedRot%2==0});
                    ecb.SetComponentEnabled<PathfindingRequest>(playerCommand,true);
                    _cachedPos=_pos;
                }
                var buff=EntityManager.GetBuffer<MapPoint>(playerCommand,true);
                foreach(var p in buff)
                {
                    _roadPoints.Add(p.pos);
                }
              
                _road.GenerateRoadMesh(_roadPoints.Select(f=>new Vector2Int(f.x,f.y)).ToArray(),null);
                
                _preview.CanBuild(true,_placeRoadPlayerData.isForce);
            }
        }
        

    } 
    public void PlaceRoad(bool IsHold,bool IsBlueprint)
    {
        var ecb = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>().CreateCommandBuffer();
        
        var mapData = SystemAPI.GetSingleton<BuildingMap>();
        var buildingConfig = SystemAPI.GetSingleton<BuildingConfigReference>();
        if (isSecondPoint)
        {
            var command = ecb.CreateEntity();
            ecb.AddComponent<ProcessRoadPointsEventTag>(command);
            ecb.AddComponent<IsBlueprint>(command);
            ecb.SetComponentEnabled<IsBlueprint>(command,IsBlueprint);
            Debug.Log(IsBlueprint);
            var buff = ecb.AddBuffer<MapPoint>(command);
            foreach (var p in _roadPoints)
            {
                buff.Add(new MapPoint{pos=p});
            }

            if (IsHold)
            {
                _firstPos = _pos;
                _roadPoints.Clear();
            }
            else
            {
                isSecondPoint = false;
                Back();
            }
        }
        else
        {
            var pos=new int2(_pos.x,_pos.y);
            if(mapData.CellMapBuildingsIDs.ContainsKey(pos)&&mapData.CellMapBuildingsIDs[pos]!=buildingConfig.roadID) return;
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
            _roadPoints.Clear();
            var pBuff=EntityManager.GetBuffer<MapPoint>(playerCommand,false);
            pBuff.Clear();
        }
        else
        {
            _roadPoints.Clear();
            _placeRoadPlayerData=null;
            if(_road!=null)GameObject.DestroyImmediate(_road.gameObject);
            _road=null;
            _preview=null;
            _buildingID=-1;
            _pos=new Vector2Int(-1,-1);
            EntityManager.SetComponentEnabled<PlayerPlacingRoad>(_playerState,false);
            
             Debug.Log("вызов");
            onBuildingDone?.Invoke();
            _isProcessing=false;
            onBuildingDone=null;
        }

    }
}