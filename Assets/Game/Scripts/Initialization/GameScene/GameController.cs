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

        await CreateMap();
        await CreateSystems(World);
        await LoadSavedEntities(save);

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
        AddUnmanaged<MarkBuildingOnMapSystem>(simGroup);
        AddUnmanaged<DestroyBuildingsSystem>(simGroup);
        AddUnmanaged<DeleteMapPointsSystem>(simGroup);
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

        var BuildSystem=RegisterManagedSystem<PlayerPlaceBuildingSystem>(presGroup);
        var RoadSystem= RegisterManagedSystem<PlayerPlaceRoadSystem>(presGroup);

         var player = _container.Resolve<PlayerController>();
        player.Initialize(BuildSystem, RoadSystem);
        simGroup.SortSystems();
        presGroup.SortSystems();
        await UniTask.Yield();
    }
    async UniTask CreateMap()
    {
        Map=World.EntityManager.CreateEntity();
        World.EntityManager.AddComponentData(Map, new BuildingMap
        {
            CellMapBuildingsIDs=new(1000,Allocator.Persistent),
            CellMapEntites=new(1000,Allocator.Persistent),
            CellEntityMultiMap=new(1000,Allocator.Persistent),
        });

        World.EntityManager.AddComponentData(Map, new EntitiesDictionary
        {
            Entities=new(250,Allocator.Persistent)
        });
        World.EntityManager.AddComponentData(Map, new ClusterMap
        {
            clusterIDs=new(50,Allocator.Persistent),
            producersSlots=new(2000,Allocator.Persistent),
            consumersSlots=new(2000,Allocator.Persistent),
            storagesSlots=new(2000,Allocator.Persistent),
             excessSlots=new(2000,Allocator.Persistent),
             bluePrintsSlots=new(2000,Allocator.Persistent),
            demolitionsSlots=new(2000,Allocator.Persistent),
             roadsPoints=new(2000,Allocator.Persistent),
             pointToClusterId=new(2000,Allocator.Persistent),
        });

        World.EntityManager.AddComponent<UpdateMapTag>(Map);
        World.EntityManager.AddComponent<UpdateCLustersTag>(Map);
        World.EntityManager.SetComponentEnabled<UpdateMapTag>(Map,false);
        World.EntityManager.SetComponentEnabled<UpdateCLustersTag>(Map,false);
         World.EntityManager.AddComponentData(Map, new TickInfoData
        {
            currTickPerSecond=gameFieldSettings.tickPerSecond,
        });
         World.EntityManager.AddComponentData(Map, new ProductionTable
        {
            produced=new(1000,Allocator.Persistent),
            consumed=new(1000,Allocator.Persistent),
        });
        await UniTask.Yield();
    }
    async UniTask LoadSavedEntities(GameStateData gameStateData)
    {
        var buildingCommand = World.EntityManager.CreateArchetype(typeof(CreateFromSave),typeof(CreateBuildingEventData),typeof(IsBlueprint));

        var roadCommand = World.EntityManager.CreateArchetype(typeof(CreateFromSave),typeof(CreateRoadEventTag),typeof(MapPoint),typeof(IsBlueprint));
        CreateBuildingCommand(buildingCommand,gameStateData.ProcessorsBuildings);
        CreateBuildingCommand(buildingCommand,gameStateData.ConsumerBuildings);
        CreateBuildingCommand(buildingCommand,gameStateData.ProducerBuildings);
        CreateBuildingCommand(buildingCommand,gameStateData.baseBuildings);

        using var entities = new NativeArray<Entity>(gameStateData.RoadsBuildings.Count, Allocator.TempJob);
        World.EntityManager.CreateEntity(roadCommand, entities);
        int index = 0;
        foreach (var pair in gameStateData.RoadsBuildings)
        {
            Entity entity = entities[index];
            int id = pair.Key;
            World.EntityManager.SetComponentData(entity, new CreateFromSave { UniqueIDHash = id });
            var buff =World.EntityManager.AddBuffer<MapPoint>(entity);
            for(int i =0;i<pair.Value.points.Length;i++)
                buff.Add(new MapPoint{pos=pair.Value.points[i]});
            
            
            World.EntityManager.SetComponentEnabled<IsBlueprint>(entity,pair.Value.isBlueprint);
            index++;
        }
        await UniTask.Yield();
    }
    void CreateBuildingCommand<T>(EntityArchetype commandArchetype,Dictionary<int,T> data) where T : BaseBuildingSaveData
    {
        using var entities = new NativeArray<Entity>(data.Count, Allocator.TempJob);
        World.EntityManager.CreateEntity(commandArchetype, entities);
        int index = 0;
        foreach (var pair in data)
        {
            Entity entity = entities[index];
            int id = pair.Key;

            World.EntityManager.SetComponentData(entity, new CreateFromSave { UniqueIDHash = id });
            World.EntityManager.SetComponentData(entity, new CreateBuildingEventData
            {
                buildingID=pair.Value.buildingID,
                buildingPosition=pair.Value.buildingPosition,
                rotation=pair.Value.rotation,
                isConnected=pair.Value.isConnected,
            });
            World.EntityManager.SetComponentEnabled<IsBlueprint>(entity,pair.Value.isBlueprint);
            index++;
        }
    }
}
