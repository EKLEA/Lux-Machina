using Unity.Entities;
using UnityEngine;
using Zenject;

public class GameSceneBindings : MonoInstaller
{
    [SerializeField] PlayerController playerController;
    [SerializeField] CameraController cameraController;
    
    public override void InstallBindings()
    {

        BindServices();
        BindEcsSystems();
        BindGameScene();
    }

    void BindEcsSystems()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        var buildingPositionSystem = world.GetOrCreateSystemManaged<BuildingPositionSystem>();
        var phantomObjectSystem = world.GetOrCreateSystemManaged<PhantomObjectSystem>();
        
    
        Container.Inject(buildingPositionSystem);
        Container.Inject(phantomObjectSystem);
        
        var fixedSimulationGroup = world.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
        fixedSimulationGroup.AddSystemToUpdateList(buildingPositionSystem);
        fixedSimulationGroup.AddSystemToUpdateList(phantomObjectSystem);
        Container.Bind<FixedStepSimulationSystemGroup>().FromInstance(fixedSimulationGroup).AsSingle();
        Container.Bind<BuildingPositionSystem>().FromInstance(buildingPositionSystem).AsSingle();
        Container.Bind<PhantomObjectSystem>().FromInstance(phantomObjectSystem).AsSingle();
        
        buildingPositionSystem.Enabled = true;
        phantomObjectSystem.Enabled = true;
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
        Container.Bind<PlayerController>().FromInstance(playerController).AsSingle();
        
        Container.Bind<EntityManager>().FromMethod(GetEntityManager).AsSingle();
        Container.Bind<EntityLoader>().AsSingle();
        Container.BindInterfacesAndSelfTo<GameController>().AsSingle().NonLazy(); 
    }
    
    EntityManager GetEntityManager()
    {
        return World.DefaultGameObjectInjectionWorld.EntityManager;
    }
}