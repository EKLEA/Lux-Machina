using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Zenject;

public class GameSceneBindings : MonoInstaller
{
    [SerializeField]
    PlayerController playerController;
     [SerializeField]
    GridVisualizer gridVisualizer;

    [SerializeField]
    CameraController cameraController;
    [SerializeField]
    UIManager UIManager;

    public override void InstallBindings()
    {
        BindServices();
        //BindEcsSystems();
        BindGameScene();
    }


    // void BindEcsSystems()
    // {
    //     var world = World.DefaultGameObjectInjectionWorld;
    //     if (world == null)
    //         return;

    //     var buildingVisualSystem = world.GetOrCreateSystemManaged<BuildingVisualSystem>();
    //     var pathfindingSystem = world.GetOrCreateSystemManaged<PathfindingSystem>();
    //     var buildingMapQuerySystem = world.GetOrCreateSystemManaged<PublicBuildingMapSystem>();
    //     var buildingLogicAssignSystem = world.GetOrCreateSystemManaged<BuildingLogicAssignSystem>();
    //     var recipeCacheFillSystem = world.GetOrCreateSystemManaged<RecipeCacheFillSystem>();

    //     var fixedSimulationGroup = world.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
    //     var simulationGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();

    //     Container.Inject(buildingVisualSystem);
    //     Container.Inject(pathfindingSystem);
    //     Container.Inject(buildingMapQuerySystem);
    //     Container.Inject(buildingLogicAssignSystem);
    //     Container.Inject(recipeCacheFillSystem);

    //     Container.Bind<PublicBuildingMapSystem>().FromInstance(buildingMapQuerySystem).AsSingle();
    //     Container.Bind<BuildingVisualSystem>().FromInstance(buildingVisualSystem).AsSingle();
    //     Container.Bind<PathfindingSystem>().FromInstance(pathfindingSystem).AsSingle();
    //     Container.Bind<RecipeCacheFillSystem>().FromInstance(recipeCacheFillSystem).AsSingle();
    //     Container.Bind<BuildingLogicAssignSystem>().FromInstance(buildingLogicAssignSystem).AsSingle();
    //     Container.Bind<FixedStepSimulationSystemGroup>().FromInstance(fixedSimulationGroup).AsSingle();
    //     Container.Bind<SimulationSystemGroup>().FromInstance(simulationGroup).AsSingle();

    //     var ecsSystemsManager = new ECSSystemsManager();
    //     Container.Bind<ECSSystemsManager>().FromInstance(ecsSystemsManager).AsSingle();
    // }

    void BindServices()
    {
        SignalBusInstaller.Install(Container);
        Container.Bind<VisualBuildingFactory>().AsSingle().NonLazy();
        Container.Bind<BuildingObjectFactory>().AsSingle().NonLazy();
        Container.Bind<ConnectEnergyFactory>().AsSingle().NonLazy();
    }

    void BindGameScene()
    {
        Container.Bind<CameraController>().FromInstance(cameraController).AsSingle();

        playerController.enabled = true;
        
        Container.Bind<ConfigToBlob>().AsSingle().NonLazy();

        Container.Bind<UIManager>().FromInstance(UIManager).AsSingle();
        
        Container.BindInterfacesAndSelfTo<GameController>().AsSingle().NonLazy();
        Container.Bind<World>().FromMethod(ctx => ctx.Container.Resolve<GameController>().World).AsSingle();
        Container.Bind<EntityManager>().FromMethod(ctx => ctx.Container.Resolve<GameController>().World.EntityManager).AsSingle();
        
        Container.Bind<PlayerPlaceBuildingSystem>().FromMethod(ctx => 
            ctx.Container.Resolve<GameController>().World.GetOrCreateSystemManaged<PlayerPlaceBuildingSystem>()
        ).AsSingle();
        
        Container.Bind<PlayerPlaceRoadSystem>().FromMethod(ctx => 
            ctx.Container.Resolve<GameController>().World.GetOrCreateSystemManaged<PlayerPlaceRoadSystem>()
        ).AsSingle();
        Container.Bind<PlayerDeleteBuildingsSystem>().FromMethod(ctx => 
            ctx.Container.Resolve<GameController>().World.GetOrCreateSystemManaged<PlayerDeleteBuildingsSystem>()
        ).AsSingle();
         Container.Bind<GridUpdateSystem>().FromMethod(ctx => 
            ctx.Container.Resolve<GameController>().World.GetOrCreateSystemManaged<GridUpdateSystem>()
        ).AsSingle();
         Container.Bind<PlayerConnectionEnergySystem>().FromMethod(ctx => 
            ctx.Container.Resolve<GameController>().World.GetOrCreateSystemManaged<PlayerConnectionEnergySystem>()
        ).AsSingle();
        Container.Bind<PlayerController>().FromInstance(playerController).AsSingle();
        Container.Bind<GridVisualizer>().FromInstance(gridVisualizer).AsSingle();
        

    }
    
}
