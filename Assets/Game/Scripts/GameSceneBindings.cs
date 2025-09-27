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
        Container.BindInterfacesAndSelfTo<BuildingsVisualService>().FromInstance(gameSceneLoader.buildingsVisualService).AsSingle();
        Container.BindInterfacesAndSelfTo<BuildingsLogicService>().FromInstance(gameSceneLoader.buildingsLogicService).AsSingle();

    }
}