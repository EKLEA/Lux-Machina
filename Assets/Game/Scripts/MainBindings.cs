using UnityEngine;
using Zenject;

public class MainBindings : MonoInstaller
{
    [SerializeField] LoadingScreen _loadingScreenPrefab;
    [SerializeField] LoadingSettingsSO _loadingSettingsSO;
    
    public override void InstallBindings()
    {
        BindServices();
        BindEntryPoint();
    }

    private void BindServices()
    {
        Container.Bind<LoadingSettings>()
                .FromMethod(f => new LoadingSettings(_loadingSettingsSO))
                .AsSingle();


        var loadingScreen = Container.InstantiatePrefabForComponent<LoadingScreen>(_loadingScreenPrefab);

        Container.Bind<ILoadingService>().To<LoadingService>().AsSingle()
            .WithArguments(loadingScreen);

        Container.Bind<SceneLoader>().AsSingle();
        Container.BindInterfacesAndSelfTo<SaveService>().AsSingle();
    }
    
    private void BindEntryPoint()
    {
        EntryPoint entryPoint = new EntryPoint(Container.Resolve<ILoadingService>(),Container.Resolve<SceneLoader>());
        entryPoint.Initialize();
        Container.Bind<IReadOnlyBuildingInfo>().FromInstance(entryPoint.ConfigService).AsSingle();
        Container.Bind<IReadOnlyItemsInfo>().FromInstance(entryPoint.ConfigService).AsSingle();
        Container.Bind<IReadOnlyUIInfo>().FromInstance(entryPoint.ConfigService).AsSingle();
    }
}