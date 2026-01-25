using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
[UpdateAfter(typeof(BuildingChangeVisualSystem))]
public partial class PlayerPlaceRoadSystem : SystemBase
{
    [Inject] BuildingObjectFactory _factorty;
    [Inject] VisualBuildingFactory _visualBuildingFactory;
    [Inject] EntityManager entityManager;
    int _buildingID;
    bool isSecondPoint;
    Vector2Int _pos;
    Vector2Int _cachedPos;
    Vector2Int _firstPos;
    EntityQuery _buildReadyQuery;
    IPlaceRoadPlayerData _placeRoadPlayerData;
    RoadOnScene _road;
    PhantomObject _preview;
    List<MapPoint> _roadPoints;
    
    Entity _playerState;
    bool _isProcessing = false;
    public  Action onBuildingDone;
    public bool canBuild{get;private set;}
    public void SetUpBuilding(int buildingID,IPlaceRoadPlayerData placeRoadPlayerData, Entity playerState)
    {
        if(_isProcessing || _road != null || EntityManager.IsComponentEnabled<PlayerPlacingRoad>(playerState)) return;
        _isProcessing = true; 
        _roadPoints?.Clear();
        _roadPoints=new();
        _buildingID=buildingID;
        _placeRoadPlayerData=placeRoadPlayerData;
        _road = _factorty.CreateRoad(_buildingID,new Vector2Int[]{_placeRoadPlayerData.pos});
        _preview=_visualBuildingFactory.PhantomizeObject(_road.gameObject);
        EntityManager.SetComponentEnabled<PlayerPlacingRoad>(playerState,true);
        
        _playerState=playerState;
    }

    protected override void OnCreate()
    {
        _buildReadyQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<PlayerCommand>()
        .WithPresent<PlayerPlacingRoad>()
        .WithPresent<PathfindingRequest>()
        .WithDisabled<PlayerPlacingBuilding>() 
        
        .Build(this);
        RequireForUpdate(_buildReadyQuery);
    }
    protected override void OnUpdate()
    {
        
        if(_buildReadyQuery.IsEmptyIgnoreFilter) return;
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
                _road.GenerateRoadMesh(new Vector2Int[]{_firstPos});
            }
            else
            {
                if(_pos!=_cachedPos)
                {
                    ecb.SetComponent(playerCommand,new PathfindingRequest{Start=new int2(_firstPos.x,_firstPos.y),End=new int2(_pos.x,_pos.y)});
                    ecb.SetComponentEnabled<PathfindingRequest>(playerCommand,true);
                    _cachedPos=_pos;
                }
                var buff=EntityManager.GetBuffer<MapPoint>(playerCommand,true);
                foreach(var p in buff)
                {
                    _roadPoints.Add(p);
                }
                _road.GenerateRoadMesh(_roadPoints.Select(f=>new Vector2Int(f.pos.x,f.pos.y)).ToArray());
            }
        }
        

    } 
    public void PlaceRoad(bool IsHold,bool IsBlueprint)
    {
        var ecb = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().CreateCommandBuffer();

        if (isSecondPoint)
        {
            var command = ecb.CreateEntity();
            ecb.AddComponent<CreateRoadEventTag>(command);
            ecb.AddComponent<IsBlueprint>(command);
            ecb.SetComponentEnabled<IsBlueprint>(command,IsBlueprint);
            
            var buff = ecb.AddBuffer<MapPoint>(command);
            foreach (var p in _roadPoints)
            {
                buff.Add(p);
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