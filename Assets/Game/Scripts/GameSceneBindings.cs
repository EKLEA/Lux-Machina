using UnityEngine;
using Zenject;

public class GameSceneBindings : MonoInstaller
{
    public override void InstallBindings()
    {
        BindServices();
        BindGameScene();
    }

    private void BindServices()
    {
        SignalBusInstaller.Install(Container);
        Container.BindInterfacesAndSelfTo<TickMachine>().AsSingle();
        Container.DeclareSignal<TickableEvent>();
        Container.Bind<BuildingVisualFactory>().AsSingle(); 
        Container.Bind<BuildingLogicFactory>().AsSingle(); 
        Container.Bind<BuildingHealthFactory>().AsSingle(); 
    }   

    private void BindGameScene()
    {
        var gameSceneLoader = Container.Instantiate<GameSceneLoader>();
        gameSceneLoader.LoadGameAsync();
       
        Container.Bind<ReadOnlyBuildingsVisualService>().FromInstance(gameSceneLoader.buildingsVisualService).AsSingle();
        Container.Bind<ReadOnlyBuildingsLogicService>().FromInstance(gameSceneLoader.buildingsLogicService).AsSingle();
        Container.Bind<ReadOnlyVirtualLogistucsCCenterService>().FromInstance(gameSceneLoader.virtualLogisticsCenterService).AsSingle();
        Container.Bind<BuildingsHealthService>().FromInstance(gameSceneLoader.buildingsHealghService).AsSingle();
    }   
}