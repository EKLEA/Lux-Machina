using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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
    Vector3Int _pos;
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
    RefAction UpdateFunc;
    // NativeList<MapPoint> removePoints;
    bool canBuild;
    int uniqueId;
    EntitiesDictionary entitiesDictionary;
    BuildingMap buildingMap;
    EnergyMap energyMap;
    ChunkMap chunkMap;
    WorldSettings worldSettings;
    BufferLookup<ResourceElement> _resourceLookup;
    
    public void SetUpBuilding(int buildingID,IPlaceBuildingPlayerData buildingPlayerData,Entity playerState, int2? connectionFrom = null)
    {
        if(EntityManager.IsComponentEnabled<PlayerPlacingBuilding>(playerState)||EntityManager.IsComponentEnabled<PlayerPlacingManyPointBuilding>(playerState)||EntityManager.IsComponentEnabled<PlayerDeletePoints>(playerState)) return;
        if(!_buildingInfo.BuildingInfos.ContainsKey(buildingID)) return;
        _buildingID=buildingID;
        Guid newGuid = Guid.NewGuid();
        uniqueId  = newGuid.GetHashCode(); 
        _connectionFrom=connectionFrom ?? new int2(-1, -1);
        _buildingPlayerData=buildingPlayerData;
        _rotation=buildingPlayerData.rotation;
        
          var data =SystemAPI.GetSingleton<PlayerRayCastData>();
        _buildingOnScene= _factorty.CreateBuilding(_buildingID,new Vector3Int(data.PlaceBlockPos.x,data.PlaceBlockPos.y,data.PlaceBlockPos.z),_buildingPlayerData.rotation,true);
        _preview=_visualBuildingFactory.PhantomizeObject(_buildingOnScene.gameObject);
        EntityManager.SetComponentEnabled<PlayerPlacingBuilding>(playerState,true);
        _playerState=playerState;

        
        Vector3Int size=_buildingInfo.BuildingInfos[_buildingID].size;
        size = _rotation % 2 != 0
                ? new Vector3Int(size.z, size.y, size.x)
                : size;
        UpdateFunc+=(ref bool b)=>UpdateBuild(ref b,size,energyMap,buildingMap);

        if(_connectionFrom.y!=-1&&_buildingOnScene is EnergyBuildingOnScene energyBuildingOnScene)
        {
            energyBuildingOnScene.SetUpNodes();
            energyNode=energyBuildingOnScene.nodes[_rotation%_buildingInfo.BuildingEnegryConfigs[_buildingID].maxConnections];
            UpdateFunc+=(ref bool b)=>UpdateEnergy(ref b,entitiesDictionary);
        }
        if(_buildingInfo.BuildingInfos[_buildingID].buildingType==BuildingsTypes.Procession&&_buildingInfo.BuildingProcessionInfos[_buildingID].typeOfProcession==TypeOfProcession.Generate)
        {
            UpdateFunc+=(ref bool b)=>UpdateResourceBuilding(ref b,_pos.y-1,size,chunkMap,worldSettings,_resourceLookup);
        }        
    }

    protected override void OnCreate()
    {
        _buildReadyQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<PlayerCommand>()
        .WithAll<PlayerPlacingBuilding>()
        .WithDisabled<PlayerPlacingManyPointBuilding>()
        .WithDisabled<PlayerDeletePoints>()
        .WithDisabled<PathfindingRequest>()
        .Build(this);
        
         _resourceLookup = GetBufferLookup<ResourceElement>(true);

        RequireForUpdate(_buildReadyQuery);
    }
    protected override void OnUpdate()
    {
        
        entitiesDictionary= SystemAPI.GetSingleton<EntitiesDictionary>();
        buildingMap= SystemAPI.GetSingleton<BuildingMap>();
        energyMap= SystemAPI.GetSingleton<EnergyMap>();
        chunkMap= SystemAPI.GetSingleton<ChunkMap>();
        worldSettings= SystemAPI.GetSingleton<WorldSettings>();
        
        var data =SystemAPI.GetSingleton<PlayerRayCastData>();
        
        if(_buildReadyQuery.IsEmpty) return;
        
        if(_buildingPlayerData==null) return;
         _resourceLookup.Update(this);
        _rotation=_buildingPlayerData.rotation;
        _pos=new Vector3Int(data.PlaceBlockPos.x,data.PlaceBlockPos.y,data.PlaceBlockPos.z);;
        
        canBuild=true;
        UpdateFunc?.Invoke(ref canBuild);
        _preview.CanBuild(canBuild,_buildingPlayerData.isForce);
        _factorty.MoveBuilding(_preview.gameObject,_pos,_connectionFrom.y != -1?0:_rotation,_buildingID);

    } 
    void UpdateBuild(ref bool prevBool, Vector3Int size, EnergyMap enData, BuildingMap map)
    {
        bool canConnect = false;
        bool isOverlapping = false; 

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.z; z++)
            {
                var pos = new int3(_pos.x + x, _pos.y,_pos.z + z);
                
                if (map.CellMapBuildingsIDs.ContainsKey(pos))
                {
                    isOverlapping = true;
                    break; 
                }
                
                if (!canConnect && enData.CellToEnergyBuildingMap.ContainsKey(pos))
                {
                    canConnect = true;
                }
            }
            if (isOverlapping) break; 
        }

        prevBool = !isOverlapping; 
        _buildingOnScene.SetOutLine(canConnect?_gameFieldSettings.selectBuildingColor:_gameFieldSettings.makeAsDemolitionBuidlingColor);
    }
    void UpdateEnergy(ref bool prevBool,EntitiesDictionary entitiesDic)
    {
        bool result=false;
        if(entitiesDic.Entities.ContainsKey(_connectionFrom.y))
        {
             var en=entitiesDic.Entities[_connectionFrom.y];
            var buildingOnSceneFrom=EntityManager.GetComponentData<BuildingOnSceneReference>(en).buildingOnScene as EnergyBuildingOnScene;
            
            energyNode=(_buildingOnScene as EnergyBuildingOnScene).nodes[_rotation%_buildingInfo.BuildingEnegryConfigs[_buildingID].maxConnections];
            connectTo=new int2(_rotation%_buildingInfo.BuildingEnegryConfigs[_buildingID].maxConnections,uniqueId);;
            result=math.distance(buildingOnSceneFrom.nodes[_connectionFrom.x].Connect.transform.position,energyNode.Connect.transform.position)<_gameFieldSettings.range;
        }
        
        prevBool=prevBool&&result;
    }
    void UpdateResourceBuilding(ref bool prevBool, int y, Vector3Int size, ChunkMap chunkMap, WorldSettings worldSettings, BufferLookup<ResourceElement> resourceLookup)
    {
        if (!prevBool) return;

        bool hasResources = false;
        

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.z; z++)
            {
                int3 worldPos = new int3(_pos.x + x, y, _pos.z + z);

                int2 chunkPos = new int2(
                    Mathf.FloorToInt((float)worldPos.x / worldSettings.Size),
                    Mathf.FloorToInt((float)worldPos.z / worldSettings.Size)
                );

                int3 localPos = new int3(
                    worldPos.x - (chunkPos.x * worldSettings.Size),
                    worldPos.y,
                    worldPos.z - (chunkPos.y * worldSettings.Size)
                );

                if (chunkMap.ChunkMapData.TryGetValue(chunkPos, out Entity chunkEntity))
                {
                    if (resourceLookup.HasBuffer(chunkEntity))
                    {
                        var resources = resourceLookup[chunkEntity];
                        
                        for (int i = 0; i < resources.Length; i++)
                        {
                            if (resources[i].LocalPos.Equals(localPos) && resources[i].Amount > 0)
                            {
                                hasResources = true;
                                break;
                            }
                        }
                    }
                }

                if (hasResources) break;
            }
            if (hasResources) break;
        }

        prevBool = hasResources;
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
            ecb.AddComponent(command,new CreateBuildingEventData{UniqueBuildingID=uniqueId,buildingID=_buildingID,rotation=_connectionFrom.y != -1?0:_rotation,buildingPosition=new int3(_pos.x,_pos.y,_pos.z)});
            ecb.AddComponent<IsBlueprint>(command);
            ecb.SetComponentEnabled<IsBlueprint>(command,IsBlueprint);//
            
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
        _pos=new Vector3Int(-1,-1,-1);
        
        EntityManager.SetComponentEnabled<PlayerPlacingBuilding>(_playerState,false);
        onBuildingDone?.Invoke();
        
        onBuildingDone=null;
        UpdateFunc=null;
    }
    public delegate void RefAction(ref bool value);
}