using Unity.Entities;
using UnityEngine;
using Zenject;

public class GameSceneBindings : MonoInstaller
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CameraController cameraController;
    
    public override void InstallBindings()
    {
        BindEcsSystems();
        BindServices();
        BindGameScene();
    }

    private void BindEcsSystems()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        var buildingPositionSystem = world.GetOrCreateSystemManaged<BuildingPositionSystem>();
        var phantomObjectSystem = world.GetOrCreateSystemManaged<PhantomObjectSystem>();
        
        var fixedSimulationGroup = world.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
        fixedSimulationGroup.AddSystemToUpdateList(buildingPositionSystem);
        fixedSimulationGroup.AddSystemToUpdateList(phantomObjectSystem);
        
        Container.Bind<BuildingPositionSystem>().FromInstance(buildingPositionSystem).AsSingle();
        Container.Bind<PhantomObjectSystem>().FromInstance(phantomObjectSystem).AsSingle();
        
        buildingPositionSystem.Enabled = true;
        phantomObjectSystem.Enabled = true;
    }

    private void BindServices()
    {
        SignalBusInstaller.Install(Container);
        Container.Bind<PhantomObjectFactory>().AsSingle(); 
        Container.Bind<BuildingObjectFactorty>().AsSingle();  
    }   

    private void BindGameScene()
    {
        Container.Bind<CameraController>().FromInstance(cameraController).AsSingle();
        
        playerController.enabled = true;
        Container.Bind<PlayerController>().FromInstance(playerController).AsSingle();
        
        Container.Bind<EntityManager>().FromMethod(GetEntityManager).AsSingle();
        Container.Bind<EntityLoader>().AsSingle();
        Container.BindInterfacesAndSelfTo<GameController>().AsSingle().NonLazy(); 
    }
    
    private EntityManager GetEntityManager()
    {
        return World.DefaultGameObjectInjectionWorld.EntityManager;
    }
}