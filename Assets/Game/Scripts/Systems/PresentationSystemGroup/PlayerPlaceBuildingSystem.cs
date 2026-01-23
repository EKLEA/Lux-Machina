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
[UpdateAfter(typeof(BuildingChangeVisualSystem))]
public partial class PlayerPlaceBuildingSystem : SystemBase
{
    [Inject] BuildingObjectFactory _factorty;
    [Inject] VisualBuildingFactory _visualBuildingFactory;
    [Inject] IReadOnlyBuildingInfo _buildingInfo;
    int _buildingID;
    int _rotation;
    bool _isConnected;
    Vector2Int _pos;
    EntityQuery _buildReadyQuery;
    IPlaceBuildingPlayerData _buildingPlayerData;
    PhantomObject _preview;
    Entity _playerState;
    public  Action onBuildingDone;
    // NativeList<MapPoint> removePoints;
    public bool canBuild{get;private set;}
    public void SetUpBuilding(int buildingID,bool isConnected,IPlaceBuildingPlayerData buildingPlayerData,Entity playerState)
    {
        if(EntityManager.IsComponentEnabled<PlayerPlacingBuilding>(playerState)||EntityManager.IsComponentEnabled<PlayerPlacingRoad>(playerState)) return;
        _buildingID=buildingID;
        _isConnected=isConnected;
        _buildingPlayerData=buildingPlayerData;
        var gm = _factorty.CreateBuilding(_buildingID,_buildingPlayerData.pos,_buildingPlayerData.rotation);
        _preview=_visualBuildingFactory.PhantomizeObject(gm.gameObject);
        EntityManager.SetComponentEnabled<PlayerPlacingBuilding>(playerState,true);
        _playerState=playerState;
    }

    protected override void OnCreate()
    {
        _buildReadyQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<PlayerCommand>()
        .WithPresent<PlayerPlacingBuilding>()
        .WithDisabled<PlayerPlacingRoad>()
        .WithDisabled<PathfindingRequest>()
        
        .Build(this);
        RequireForUpdate(_buildReadyQuery);
    }
    protected override void OnUpdate()
    {
        if(_buildReadyQuery.IsEmptyIgnoreFilter) return;
        if(_buildingPlayerData==null) return;
        _rotation=_buildingPlayerData.rotation;
        _pos=_buildingPlayerData.pos;
        Vector3Int size=_buildingInfo.BuildingInfos[_buildingID].size;
        size = _rotation % 2 != 0
                ? new Vector3Int(size.z, size.y, size.x)
                : size;
        var map = SystemAPI.GetSingleton<BuildingMap>();
        canBuild=true;
        for(int x=0; x < size.x; x++)
        {
            for(int z=0; z < size.z; z++)
            {
                if(map.CellMapBuildingsIDs.ContainsKey(new int2(_pos.x+x, _pos.y+z)))
                {
                    canBuild=false;
                    break;
                }
            }
        }
        _preview.CanBuild(canBuild);
        _factorty.MoveBuilding(_preview.gameObject,_buildingID,_pos,_rotation);

    } 
    public void PlaceBuilding(bool isHold,bool IsBlueprint)
    {
        var ecb = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().CreateCommandBuffer();
        if (canBuild)
        {
            var command=ecb.CreateEntity();
            ecb.AddComponent(command,new CreateBuildingEventData{buildingID=_buildingID,rotation=_rotation,isConnected=_isConnected,buildingPosition=new int2(_pos.x,_pos.y)});
            
            ecb.AddComponent<IsBlueprint>(command);
            ecb.SetComponentEnabled<IsBlueprint>(command,IsBlueprint);
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
        _isConnected=false;
        _pos=new Vector2Int(-1,-1);
        
        EntityManager.SetComponentEnabled<PlayerPlacingBuilding>(_playerState,false);
        Debug.Log("вызов");
        onBuildingDone?.Invoke();
    }
}