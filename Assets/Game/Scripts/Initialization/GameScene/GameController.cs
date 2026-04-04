
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
    [Inject] SceneLoader _sceneLoader;
    Entity Map;
    public bool IsInitialized{get;private set;}
    public bool isPause{get;private set;}
    
    public GameController() 
    {
        World = DefaultWorldInitialization.Initialize("Game Scene World");
    }
    public (WorldTime time,bool isPaused) GetCurrTime()
    {
        var time =World.EntityManager.GetComponentData<WorldTime>(Map);
        return(time,World.EntityManager.IsComponentEnabled<IsPause>(Map));
    }
    public void Initialize()
    {
        IsInitialized=false;
        LoadGame();
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
    public void SetSpeedMul(float speed)
    {
        var ecb= World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
               .CreateCommandBuffer();
        var time =World.EntityManager.GetComponentData<WorldTime>(Map);
        time.SpeedMultiplier=math.max(1,speed);
        ecb.SetComponent(Map,time);
    }
    public void SaveGame()
    {
        var ecb= World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
               .CreateCommandBuffer();
        ecb.SetComponentEnabled<SavingMapTag>(Map,true);
         
    }
    public void SetPause(bool state)
    {
        
        var ecb = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
                    .CreateCommandBuffer();
        
        ecb.SetComponentEnabled<IsPause>(Map, state);
        isPause = state;
    }

    public void TogglePause() => SetPause(!isPause);
    async UniTask GoToMenu()
    {
        
        await _loadingService.ShowBlackScreenForce(false);
        World.Dispose();
        await _sceneLoader.LoadSceneAsync("MainMenu");
        
        await _loadingService.LoadWithProgressAsync();
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
        UIManager.Initialize();
        await CreateMap(save);
        await CreateSystems(World);
        await LoadSavedEntities(save);
        var query = World.EntityManager.CreateEntityQuery(typeof(LoadingMapTag));
    
        await UniTask.WaitUntil(() => query.CalculateEntityCount() == 0);

        cameraController.SetUp(save.camData);
        cameraController.enabled = true;
        
        IsInitialized=true;
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
        AddUnmanaged<EnemyAISystem>(simGroup);
        AddUnmanaged<TurretSystem>(simGroup);
        AddUnmanaged<ProjectileSystem>(simGroup);
        AddUnmanaged<HealthSystem>(simGroup);
        AddUnmanaged<TickGeneratorSystem>(simGroup);
        AddUnmanaged<TickCleanerSystem>(simGroup);

        RegisterManagedSystem<BuildingCreateDestroyVisualSystem>(presGroup);
        RegisterManagedSystem<BuildingChangeVisualSystem>(presGroup);
        RegisterManagedSystem<ProccessDeletePointsSystem>(presGroup);
        RegisterManagedSystem<BuildingLoadSystem>(presGroup);
        var save = RegisterManagedSystem<BuildingSaveSystem>(presGroup);
        RegisterManagedSystem<SunUpdateSystem>(presGroup);

        save.OnGameOver+=()=>UIManager.ShowPauseMenu(PauseMenuType.gameOver);

        UIManager.onReturnToMenu+=() => GoToMenu().Forget(); 
        
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
            CellWeights=new(1000,Allocator.Persistent),
            CellDirections=new(1000,Allocator.Persistent),
            CorePos=gameStateData.CorePos
        });
        World.EntityManager.AddComponentData(Map,new SpawnMobsData
        {
            CountOfCicle=0,
        });
        World.EntityManager.AddComponentData(Map,new TurretGrid
        {
            EnemyGridMap=new(1000,Allocator.Persistent),
            EnemyToTurret=new(1000,Allocator.Persistent),
            TurretGridClaim=new(1000,Allocator.Persistent),
            EnemyInCellsMap=new(1000,Allocator.Persistent),
            CellSize=gameFieldSettings.cellSize
        });
        World.EntityManager.AddBuffer<SpawnPointElement>(Map);
        World.EntityManager.SetComponentEnabled<SpawnMobsData>(Map,false);



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
      
        
       
        await PrepareWorld(Map,gameStateData);
        World.EntityManager.AddComponentData(Map, new ClusterMap(Allocator.Persistent));

        World.EntityManager.AddComponent<UpdateMapTag>(Map);
        World.EntityManager.AddComponent<UpdateClustersTag>(Map);
        World.EntityManager.AddComponent<UpdateClusterSlots>(Map);
        World.EntityManager.AddComponent<UpdateConnectionsTag>(Map);
        World.EntityManager.SetComponentEnabled<UpdateMapTag>(Map,false);
        World.EntityManager.SetComponentEnabled<UpdateClustersTag>(Map,false);
        World.EntityManager.SetComponentEnabled<UpdateClusterSlots>(Map,false);
        World.EntityManager.SetComponentEnabled<UpdateConnectionsTag>(Map,false);
         World.EntityManager.AddComponentData(Map, new WorldTime
        {
            CurrentTick=gameStateData.CurrTick,
            TicksPerDay=1200,
            SpeedMultiplier=1,
            baseTick=0.05f,
            dayLength=0.7f
        });
        World.EntityManager.AddComponent<IsTickFrame>(Map);
        World.EntityManager.AddComponent<IsPause>(Map);
        World.EntityManager.AddComponent<IsGameOver>(Map);
         World.EntityManager.AddComponentData(Map, new ProductionTable
        {
            produced=new(1000,Allocator.Persistent),
            consumed=new(1000,Allocator.Persistent),
        });
        World.EntityManager.AddComponent<LoadingMapTag>(Map);
        World.EntityManager.AddComponent<SavingMapTag>(Map);
        World.EntityManager.SetComponentEnabled<SavingMapTag>(Map,false);
        World.EntityManager.SetComponentEnabled<IsPause>(Map,false);
        World.EntityManager.SetComponentEnabled<IsGameOver>(Map,false);


        
        await UniTask.Yield();
    }
    async UniTask PrepareWorld(Entity Map,GameStateData gameStateData)
    {
        ResourceMap resourceMap=new ResourceMap
        {
            ResouecesMap=new(10000,Allocator.Persistent)
        };
        foreach(var c in gameStateData.ResourcesCellsList)
        {
            resourceMap.ResouecesMap.Add(c.pos,c.val);
        }
         World.EntityManager.AddComponentData(Map, resourceMap);
        await UniTask.Yield();
    }
    async UniTask LoadSavedEntities(GameStateData gameStateData)
    {
        var buildingCommand = World.EntityManager.CreateArchetype(typeof(CreateBuildingEventData),typeof(IsBlueprint),typeof(IsDemolition),typeof(LinkNetworkEnergyTo));

        var roadCommand = World.EntityManager.CreateArchetype(typeof(CreateRoadEventTag),typeof(MapPoint),typeof(IsBlueprint),typeof(IsDemolition));
        CreateBuildingCommand(buildingCommand,gameStateData);

        using var entities = new NativeArray<Entity>(gameStateData.ManyPointsBuildings.Count, Allocator.TempJob);
        World.EntityManager.CreateEntity(roadCommand, entities);
        int index = 0;
        foreach (var pair in gameStateData.ManyPointsBuildings)
        {
            Entity entity = entities[index];
            int id = pair.Key;
            var buff =World.EntityManager.AddBuffer<MapPoint>(entity);
            for(int i =0;i<pair.Value.points.Length;i++)
                buff.Add(new MapPoint{pos=pair.Value.points[i]});
            
            World.EntityManager.SetComponentData(entity,new CreateRoadEventTag{UniqueBuildingID=id});//тут изменить под стены
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
                    buff.Add(new LinkNetworkEnergyTo{LinkFromBuilding=link.from,LinkToBuilding=link.to,});
                }
                World.EntityManager.AddComponentData(entity,new SwitchIsOffCreateData{SwitchIsOff=gameStateData.buildingEnergyNetvorkLinkSaveData[id].isSwitchOff});
            }
            World.EntityManager.SetComponentEnabled<IsBlueprint>(entity,pair.Value.isBlueprint);
            World.EntityManager.SetComponentEnabled<IsDemolition>(entity,pair.Value.IsDemolition);
            index++;
        }
    }
}
