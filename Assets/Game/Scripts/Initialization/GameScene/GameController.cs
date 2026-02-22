using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.LowLevel;
using Zenject;

public class GameController : IInitializable
{
    public World World {get;private set;}
    [Inject] IReadOnlyGameFieldSettings gameFieldSettings;
    [Inject] SaveService saveService;

    [Inject] ILoadingService _loadingService;

    [Inject] CameraController cameraController;


    [Inject] UIManager UIManager;
    [Inject] DiContainer _container;
    [Inject] ConfigToBlob configToBlob;
    Entity Map;
    public float Timestep{get;private set;}
    
    public GameController() 
    {
        World = DefaultWorldInitialization.Initialize("Game Scene World");
    }
    public void Initialize()
    {
        Timestep=1 / gameFieldSettings.tickPerSecond;
        //fixedStepSimulationSystemGroup.Timestep = Timestep;


        LoadGame();
    }

    public void SpeedUpTick()
    {
        Timestep/=2;
        //.Timestep =Timestep;
    }

    public void SlowDownTick()
    {
        Timestep*=2;
       // fixedStepSimulationSystemGroup.Timestep = Timestep;
    }
    public Vector2Int GetMapPos(Vector3 pos)
    {
        return new Vector2Int(
        Mathf.FloorToInt(pos.x / gameFieldSettings.cellSize),
        Mathf.FloorToInt(pos.z / gameFieldSettings.cellSize)
    );
    }
    async void LoadGame()
    {
        await _loadingService.LoadWithProgressAsync(saveService.LoadGameState, LoadGameField);
         
    }
    public bool GetEntity(int id,out Entity entity)
    {
       var buildingEntities= World.EntityManager.GetComponentData<EntitiesDictionary>(Map);
        if (buildingEntities.Entities.ContainsKey(id))
        {
            entity= buildingEntities.Entities[id];;
            return true;
        }
        entity=default;
        return false;
    }
    async UniTask LoadGameField()
    {
        await configToBlob.LoadConfigs(World.EntityManager);
        var save = saveService.GameState;

        await CreateMap(save);
        await CreateSystems(World);
        await LoadSavedEntities(save);
        var query = World.EntityManager.CreateEntityQuery(typeof(LoadingMapTag));
    
        await UniTask.WaitUntil(() => query.CalculateEntityCount() == 0);

        cameraController.SetUp(save.camData);
        cameraController.enabled = true;
        UIManager.Initialize();

        await UniTask.Yield();
    }

    async UniTask CreateSystems(World world)
    {
        var simGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
        var presGroup = world.GetOrCreateSystemManaged<PresentationSystemGroup>();


        T RegisterManagedSystem<T>(ComponentSystemGroup group) where T : SystemBase
        {
            var system = world.GetOrCreateSystemManaged<T>();
            _container.Inject(system); 
           // _container.BindInterfacesAndSelfTo<T>().FromInstance(system).AsSingle();
            group.AddSystemToUpdateList(system); 
            return system;
        }
         void AddUnmanaged<T>(ComponentSystemGroup group) where T : unmanaged, ISystem
        {
            var handle = world.GetOrCreateSystem<T>();
            group.AddSystemToUpdateList(handle);
        }

        world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
        world.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        AddUnmanaged<DestroyBuildingsSystem>(simGroup);
        AddUnmanaged<MarkBuildingOnMapSystem>(simGroup);
        AddUnmanaged<ProcessRoadPointsSystem>(simGroup);
        AddUnmanaged<BuildingCreateSystem>(simGroup);
        AddUnmanaged<EnergySystem>(simGroup);
        AddUnmanaged<ClusterAssignSystem>(simGroup);
        AddUnmanaged<BuildingConfigManagerSystem>(simGroup);
        AddUnmanaged<ItemDistributionSystem>(simGroup);
        AddUnmanaged<CraftSystem>(simGroup);
        AddUnmanaged<CraftApplySystem>(simGroup);
        AddUnmanaged<PathFindingSystem>(simGroup);

        RegisterManagedSystem<BuildingCreateDestroyVisualSystem>(presGroup);
        RegisterManagedSystem<BuildingChangeVisualSystem>(presGroup);
        RegisterManagedSystem<BuildingGameObjectClusterAssignSystem>(presGroup);
        RegisterManagedSystem<ProccessDeletePointsSystem>(presGroup);
        RegisterManagedSystem<BuildingSaveSystem>(presGroup);

        var BuildSystem=RegisterManagedSystem<PlayerPlaceBuildingSystem>(presGroup);
        var RoadSystem= RegisterManagedSystem<PlayerPlaceRoadSystem>(presGroup);
        var deleteSystem= RegisterManagedSystem<PlayerDeleteBuildingsSystem>(presGroup);
        var gridSystem= RegisterManagedSystem<GridUpdateSystem>(presGroup);
        var connSystem= RegisterManagedSystem<PlayerConnectionEnergySystem>(presGroup);

         var player = _container.Resolve<PlayerController>();
        player.Initialize(BuildSystem, RoadSystem,deleteSystem,connSystem,gridSystem);
        simGroup.SortSystems();
        presGroup.SortSystems();
        await UniTask.Yield();
    }
    async UniTask CreateMap(GameStateData gameStateData)
    {
        Map=World.EntityManager.CreateEntity();
        World.EntityManager.AddComponentData(Map, new BuildingMap
        {
            CellMapBuildingsIDs=new(1000,Allocator.Persistent),
            CellMapEntites=new(1000,Allocator.Persistent),
            CellEntityMultiMap=new(1000,Allocator.Persistent),
            IsBluePrintOrDemolitionPoints=new(1000,Allocator.Persistent),
        });
        World.EntityManager.AddComponentData(Map, new EnergyMap
        {
            CellToEnergyBuildingMap=new(1000,Allocator.Persistent),
            CellToEnergyEntityBuildingMap=new(1000,Allocator.Persistent),
            EnergyEntityToCellBuildingMap=new(1000,Allocator.Persistent),
            EnergyLinks=new(5000,Allocator.Persistent),
            CoreID=gameStateData.CoreID
        });
        World.EntityManager.AddComponentData(Map, new EntitiesDictionary
        {
            Entities=new(250,Allocator.Persistent)
        });
        World.EntityManager.AddComponentData(Map, new ClusterMap(Allocator.Persistent));

        World.EntityManager.AddComponent<UpdateMapTag>(Map);
        World.EntityManager.AddComponent<UpdateClustersTag>(Map);
        World.EntityManager.AddComponent<UpdateClusterSlots>(Map);
        World.EntityManager.AddComponent<UpdateConnectionsTag>(Map);
        World.EntityManager.SetComponentEnabled<UpdateMapTag>(Map,false);
        World.EntityManager.SetComponentEnabled<UpdateClustersTag>(Map,false);
        World.EntityManager.SetComponentEnabled<UpdateClusterSlots>(Map,false);
        World.EntityManager.SetComponentEnabled<UpdateConnectionsTag>(Map,false);
         World.EntityManager.AddComponentData(Map, new TickInfoData
        {
            currTickPerSecond=gameFieldSettings.tickPerSecond,
        });
         World.EntityManager.AddComponentData(Map, new ProductionTable
        {
            produced=new(1000,Allocator.Persistent),
            consumed=new(1000,Allocator.Persistent),
        });
        World.EntityManager.AddComponent<LoadingMapTag>(Map);
        World.EntityManager.AddComponent<SavingMapTag>(Map);
        World.EntityManager.SetComponentEnabled<SavingMapTag>(Map,false);
        await UniTask.Yield();
    }
    async UniTask LoadSavedEntities(GameStateData gameStateData)
    {
        var buildingCommand = World.EntityManager.CreateArchetype(typeof(CreateBuildingEventData),typeof(IsBlueprint),typeof(IsDemolition),typeof(LinkNetworkEnergyTo));

        var roadCommand = World.EntityManager.CreateArchetype(typeof(CreateRoadEventTag),typeof(MapPoint),typeof(IsBlueprint),typeof(IsDemolition));
        CreateBuildingCommand(buildingCommand,gameStateData);

        using var entities = new NativeArray<Entity>(gameStateData.RoadsBuildings.Count, Allocator.TempJob);
        World.EntityManager.CreateEntity(roadCommand, entities);
        int index = 0;
        foreach (var pair in gameStateData.RoadsBuildings)
        {
            Entity entity = entities[index];
            int id = pair.Key;
            var buff =World.EntityManager.AddBuffer<MapPoint>(entity);
            for(int i =0;i<pair.Value.points.Length;i++)
                buff.Add(new MapPoint{pos=pair.Value.points[i]});
            
            World.EntityManager.SetComponentData(entity,new CreateRoadEventTag{UniqueBuildingID=id});
            World.EntityManager.SetComponentEnabled<IsBlueprint>(entity,pair.Value.isBlueprint);
            World.EntityManager.SetComponentEnabled<IsDemolition>(entity,pair.Value.IsDemolition);
            index++;
        }
        await UniTask.Yield();
    }
    void CreateBuildingCommand(EntityArchetype commandArchetype,GameStateData gameStateData)
    {
        using var entities = new NativeArray<Entity>(gameStateData.Buildings.Count, Allocator.TempJob);
        World.EntityManager.CreateEntity(commandArchetype, entities);
        int index = 0;
        foreach (var pair in gameStateData.Buildings)
        {
            Entity entity = entities[index];
            int id = pair.Key;
            World.EntityManager.SetComponentData(entity, new CreateBuildingEventData
            {
                UniqueBuildingID=id,
                buildingID=pair.Value.buildingID,
                buildingPosition=pair.Value.buildingPosition,
                rotation=pair.Value.rotation,
            });
            
            if (gameStateData.buildingEnergyNetvorkLinkSaveData.ContainsKey(id))
            {
                var buff=World.EntityManager.AddBuffer<LinkNetworkEnergyTo>(entity);
                var links = gameStateData.buildingEnergyNetvorkLinkSaveData[id].entitesLink;
                foreach(var link in links)
                {
                    buff.Add(new LinkNetworkEnergyTo{LinkFromBuilding=link.Item1,LinkToBuilding=link.Item2,});
                }
            }
            World.EntityManager.SetComponentEnabled<IsBlueprint>(entity,pair.Value.isBlueprint);
            World.EntityManager.SetComponentEnabled<IsDemolition>(entity,pair.Value.IsDemolition);
            index++;
        }
    }
}
