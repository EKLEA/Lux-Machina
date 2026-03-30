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
    FlowFieldVisualizer FlowFieldVisualizer;
    [SerializeField]
    CameraController cameraController;
    [SerializeField]
    UIManager UIManager;
    [SerializeField] EnemyFactory enemyFactory;
    [SerializeField] SunController sunController;
    public override void InstallBindings()
    {
        BindServices();
        //BindEcsSystems();
        BindGameScene();
    }



    void BindServices()
    {
        SignalBusInstaller.Install(Container);
        Container.Bind<VisualBuildingFactory>().AsSingle().NonLazy();
        Container.Bind<BuildingObjectFactory>().AsSingle().NonLazy();
        Container.Bind<ConnectEnergyFactory>().AsSingle().NonLazy();
        Container.Bind<EnemyFactory>().FromInstance(enemyFactory).AsSingle();
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
        Container.Bind<SunController>().FromInstance(sunController).AsSingle();
        Container.Bind<GridVisualizer>().FromInstance(gridVisualizer).AsSingle();
        //Container.Bind<FlowFieldVisualizer>().FromInstance(FlowFieldVisualizer).AsSingle();
        

    }
    
}
