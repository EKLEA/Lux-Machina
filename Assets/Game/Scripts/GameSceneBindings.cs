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
    }   

    private void BindGameScene()
    {
        GameSceneLoader gameSceneLoader =
         new GameSceneLoader(
            Container.Resolve<ILoadingService>(),
            Container.Resolve<SaveService>());
        gameSceneLoader.Initialize();
        Container.Bind<ReadOnlyBuildingsVisualService>().FromInstance(gameSceneLoader.buildingsVisualService).AsSingle();
        Container.BindInterfacesAndSelfTo<ReadOnlyBuildingsLogicService>().FromInstance(gameSceneLoader.buildingsLogicService).AsSingle();

    }
}