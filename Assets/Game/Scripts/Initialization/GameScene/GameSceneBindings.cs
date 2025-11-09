using Unity.Entities;
using UnityEngine;
using Zenject;

public class GameSceneBindings : MonoInstaller
{
    [SerializeField]
    PlayerController playerController;

    [SerializeField]
    CameraController cameraController;
    [SerializeField]
    UIManager UIManager;

    public override void InstallBindings()
    {
        BindServices();
        BindEcsSystems();
        BindGameScene();
    }

    void BindEcsSystems()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
            return;

        var buildingVisualSystem = world.GetOrCreateSystemManaged<BuildingVisualSystem>();
        var pathfindingSystem = world.GetOrCreateSystemManaged<PathfindingSystem>();
        var buildingMapQuerySystem = world.GetOrCreateSystemManaged<PublicBuildingMapSystem>();
        var buildingLogicAssignSystem = world.GetOrCreateSystemManaged<BuildingLogicAssignSystem>();
        var recipeCacheFillSystem = world.GetOrCreateSystemManaged<RecipeCacheFillSystem>();

        var fixedSimulationGroup = world.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
        var simulationGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();

        Container.Inject(buildingVisualSystem);
        Container.Inject(pathfindingSystem);
        Container.Inject(buildingMapQuerySystem);
        Container.Inject(buildingLogicAssignSystem);
        Container.Inject(recipeCacheFillSystem);

        Container.Bind<PublicBuildingMapSystem>().FromInstance(buildingMapQuerySystem).AsSingle();
        Container.Bind<BuildingVisualSystem>().FromInstance(buildingVisualSystem).AsSingle();
        Container.Bind<PathfindingSystem>().FromInstance(pathfindingSystem).AsSingle();
        Container.Bind<RecipeCacheFillSystem>().FromInstance(recipeCacheFillSystem).AsSingle();
        Container.Bind<BuildingLogicAssignSystem>().FromInstance(buildingLogicAssignSystem).AsSingle();
        Container.Bind<FixedStepSimulationSystemGroup>().FromInstance(fixedSimulationGroup).AsSingle();
        Container.Bind<SimulationSystemGroup>().FromInstance(simulationGroup).AsSingle();

        var ecsSystemsManager = new ECSSystemsManager();
        Container.Bind<ECSSystemsManager>().FromInstance(ecsSystemsManager).AsSingle();
    }

    void BindServices()
    {
        SignalBusInstaller.Install(Container);
        Container.Bind<PhantomObjectFactory>().AsSingle().NonLazy();
        Container.Bind<BuildingObjectFactorty>().AsSingle().NonLazy();
    }

    void BindGameScene()
    {
        Container.Bind<CameraController>().FromInstance(cameraController).AsSingle();

        playerController.enabled = true;
        Container
            .BindInterfacesAndSelfTo<PlayerController>()
            .FromInstance(playerController)
            .AsSingle();

        Container.Bind<EntityManager>().FromMethod(GetEntityManager).AsSingle();
        Container.Bind<EntityLoader>().AsSingle();

        Container.Bind<UIManager>().FromInstance(UIManager).AsSingle();
        
        Container.BindInterfacesAndSelfTo<GameController>().AsSingle().NonLazy();
    }

    EntityManager GetEntityManager()
    {
        return World.DefaultGameObjectInjectionWorld.EntityManager;
    }
}
