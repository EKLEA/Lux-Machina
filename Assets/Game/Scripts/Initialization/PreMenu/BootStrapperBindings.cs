using System.Linq;
using UnityEngine;
using Zenject;

public class BootStrapperBindings : MonoInstaller
{
    [SerializeField] LoadingScreen _loadingScreenPrefab;
    
    public override void InstallBindings()
    {
        BindServices();
        BindEntryPoint();
    }

    private void BindServices()
    {
        Container.Bind<LoadingSettings>()
                .FromMethod(f => Resources.LoadAll<LoadingSettingsSO>("Game/")
                            .Select(ls => new LoadingSettings(ls.LoadingImages,ls.TimeOfFade,ls.Smoothness))
                            .First())
                .AsSingle().NonLazy();
         Container.Bind<GameFieldSettings>()
                .FromMethod(f => Resources.LoadAll<GameFieldSettingsSO>("Game/")
                             .Select(ls => new GameFieldSettings(ls.cellSize,ls.tickPerSecond,DistributionPriority.Middle))
                             .First())
              .AsSingle().NonLazy();
        Debug.Log(Container.Resolve<GameFieldSettings>());
        Container.Bind<IReadOnlyLoadingSettings>().To<LoadingSettings>().FromResolve();
        Container.Bind<IReadOnlyGameFieldSettings>().To<GameFieldSettings>().FromResolve();
        var loadingScreen = Container.InstantiatePrefabForComponent<LoadingScreen>(_loadingScreenPrefab);
        Container.Bind<ILoadingService>().To<LoadingService>().AsSingle()
            .WithArguments(loadingScreen);

        Container.Bind<SceneLoader>().AsSingle();
        Container.BindInterfacesAndSelfTo<SaveService>().AsSingle().NonLazy();
        Container.Bind<ConfigService>().AsSingle();
    }
    
    private void BindEntryPoint()
    {
        Container.BindInterfacesAndSelfTo<BootStrapper>().AsSingle().NonLazy();
        
        Container.Bind<IReadOnlyBuildingInfo>()
               .FromMethod(ctx => ctx.Container.Resolve<ConfigService>())
               .AsSingle();
               
        Container.Bind<IReadOnlyItemsInfo>()
               .FromMethod(ctx => ctx.Container.Resolve<ConfigService>())
               .AsSingle();

        Container.Bind<IReadOnlyUIInfo>()
               .FromMethod(ctx => ctx.Container.Resolve<ConfigService>())
               .AsSingle();
        Container.Bind<IReadOnlyMaterialInfo>()
               .FromMethod(ctx => ctx.Container.Resolve<ConfigService>())
               .AsSingle();
    }
}