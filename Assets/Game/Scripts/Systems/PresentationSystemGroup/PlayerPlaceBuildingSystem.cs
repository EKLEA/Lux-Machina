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
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Zenject;
[DisableAutoCreation]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(GridUpdateSystem))]
public partial class PlayerPlaceBuildingSystem : SystemBase
{
    [Inject] BuildingObjectFactory _factorty;
    [Inject] VisualBuildingFactory _visualBuildingFactory;
    [Inject] IReadOnlyBuildingInfo _buildingInfo;
    [Inject] GameFieldSettings _gameFieldSettings;
    [Inject] GameController _gameController;
    int _buildingID;
    int _rotation;
    Vector2Int _pos;
    EntityQuery _buildReadyQuery;
    IPlaceBuildingPlayerData _buildingPlayerData;
    PhantomObject _preview;
    BuildingOnScene _buildingOnScene;
    Entity _playerState;
    int2 _connectionFrom;//для лэп
    public int2 connectTo;
    public int2 NextConnectFrom;
    public  Action onBuildingDone;
    public EnergyNode energyNode;
    // NativeList<MapPoint> removePoints;
    public bool canBuild{get;private set;}
    int uniqueId;
    public void SetUpBuilding(int buildingID,IPlaceBuildingPlayerData buildingPlayerData,Entity playerState, int2? connectionFrom = null)
    {
        if(EntityManager.IsComponentEnabled<PlayerPlacingBuilding>(playerState)||EntityManager.IsComponentEnabled<PlayerPlacingRoad>(playerState)||EntityManager.IsComponentEnabled<PlayerDeletePoints>(playerState)) return;
        _buildingID=buildingID;
        Guid newGuid = Guid.NewGuid();
        uniqueId  = newGuid.GetHashCode(); 
        _connectionFrom=connectionFrom ?? new int2(-1, -1);
        _buildingPlayerData=buildingPlayerData;
        _rotation=buildingPlayerData.rotation;
        _buildingOnScene= _factorty.CreateBuilding(_buildingID,_buildingPlayerData.pos,_buildingPlayerData.rotation,true);
        if(_connectionFrom.y!=-1&&_buildingOnScene is EnergyBuildingOnScene energyBuildingOnScene)
        {
            energyBuildingOnScene.SetUpNodes();
            energyNode=energyBuildingOnScene.nodes[_rotation%_buildingInfo.BuildingEnegryConfigs[_buildingID].maxConnections];
        }
        _preview=_visualBuildingFactory.PhantomizeObject(_buildingOnScene.gameObject);
        EntityManager.SetComponentEnabled<PlayerPlacingBuilding>(playerState,true);
        _playerState=playerState;
    }

    protected override void OnCreate()
    {
        _buildReadyQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<PlayerCommand>()
        .WithAll<PlayerPlacingBuilding>()
        .WithDisabled<PlayerPlacingRoad>()
        .WithDisabled<PlayerDeletePoints>()
        .WithDisabled<PathfindingRequest>()
        
        .Build(this);
        RequireForUpdate(_buildReadyQuery);
    }
    protected override void OnUpdate()
    {
        
        var entitiesDic= SystemAPI.GetSingleton<EntitiesDictionary>();
        var map= SystemAPI.GetSingleton<BuildingMap>();
        var configs= SystemAPI.GetSingleton<BuildingConfigReference>();
        
        if(_buildReadyQuery.IsEmpty) return;
        
        if(_buildingPlayerData==null) return;
        
        _rotation=_buildingPlayerData.rotation;
        _pos=_buildingPlayerData.pos;
        Vector3Int size=_buildingInfo.BuildingInfos[_buildingID].size;
        size = _rotation % 2 != 0
                ? new Vector3Int(size.z, size.y, size.x)
                : size;
        var enData = SystemAPI.GetSingleton<EnergyMap>();
        canBuild=true;
        bool canConnect=false;
        for(int x=0; x < size.x; x++)
        {
            for(int z=0; z < size.z; z++)
            {
                
                var pos=new int2(_pos.x+x, _pos.y+z);
                if(map.CellMapBuildingsIDs.ContainsKey(new int2(_pos.x+x, _pos.y+z)))
                {
                    canBuild=false;
                    break;
                }
                else
                {
                    if(enData.CellToEnergyBuildingMap.ContainsKey(pos))
                    {
                        canConnect=true;
                        break;
                    }
                }
            }
        }
        bool connectRes=true;
        if (_connectionFrom.y != -1)
        {
            
            if(!entitiesDic.Entities.ContainsKey(_connectionFrom.y)) return;
            var en=entitiesDic.Entities[_connectionFrom.y];
            var buildingOnSceneFrom=EntityManager.GetComponentData<BuildingOnSceneReference>(en).buildingOnScene as EnergyBuildingOnScene;
           
            energyNode=(_buildingOnScene as EnergyBuildingOnScene).nodes[_rotation%_buildingInfo.BuildingEnegryConfigs[_buildingID].maxConnections];
            connectTo=new int2(_rotation%_buildingInfo.BuildingEnegryConfigs[_buildingID].maxConnections,uniqueId);;
            connectRes=math.distance(buildingOnSceneFrom.nodes[_connectionFrom.x].Connect.transform.position,energyNode.Connect.transform.position)<configs.range;
        }
        _buildingOnScene.SetOutLine(canConnect?_gameFieldSettings.selectBuildingColor:_gameFieldSettings.makeAsDemolitionBuidlingColor);
        canBuild=canBuild&&connectRes;
        _preview.CanBuild(canBuild,_buildingPlayerData.isForce);
        _factorty.MoveBuilding(_preview.gameObject,_pos,_connectionFrom.y != -1?0:_rotation,_buildingID);

    } 
    public void PlaceBuilding(bool isHold,bool IsBlueprint)
    {
        var ecb = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>().CreateCommandBuffer();
        if (canBuild)
        {
            var command=ecb.CreateEntity();
            
            
            if (_connectionFrom.y!=-1&&_buildingInfo.BuildingEnegryConfigs.ContainsKey(_buildingID))
            {;
                var node=new int2(_rotation%_buildingInfo.BuildingEnegryConfigs[_buildingID].maxConnections,uniqueId);
                connectTo=node;
                node.x=( node.x+_buildingInfo.BuildingEnegryConfigs[_buildingID].maxConnections/2)%_buildingInfo.BuildingEnegryConfigs[_buildingID].maxConnections;
                NextConnectFrom=node;
                _connectionFrom=node;
                
            }
            ecb.AddComponent(command,new CreateBuildingEventData{UniqueBuildingID=uniqueId,buildingID=_buildingID,rotation=_connectionFrom.y != -1?0:_rotation,buildingPosition=new int2(_pos.x,_pos.y)});
            ecb.AddComponent<IsBlueprint>(command);
            ecb.SetComponentEnabled<IsBlueprint>(command,IsBlueprint);
            
            Guid newGuid = Guid.NewGuid();
            uniqueId  = newGuid.GetHashCode(); 
            if(!isHold) Back();
        }
    }
    public void Back()
    {
        _buildingPlayerData=null;;
        if(_preview!=null)GameObject.DestroyImmediate(_preview.gameObject);
        _preview=null;
        _buildingID=-1;
        _rotation=-1;
        _connectionFrom=default;
        _pos=new Vector2Int(-1,-1);
        
        EntityManager.SetComponentEnabled<PlayerPlacingBuilding>(_playerState,false);
        onBuildingDone?.Invoke();
        
        onBuildingDone=null;
    }
}