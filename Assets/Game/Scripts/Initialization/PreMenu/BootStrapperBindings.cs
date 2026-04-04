using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class BootStrapperBindings : MonoInstaller
{
    [SerializeField]
    LoadingScreen _loadingScreenPrefab;
    [SerializeField] Button button;

    public override void InstallBindings()
    {
         ApplyGlobalSettings();
        BindServices();
        BindСonfigsPoint();
    }
    private void ApplyGlobalSettings()
    {
        // Загружаем громкость (по умолчанию 1.0)
        float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        AudioListener.volume = savedVolume;

        // Загружаем полноэкранный режим (по умолчанию true/1)
        bool isFull = PlayerPrefs.GetInt("IsFullscreen", 1) == 1;
        Screen.fullScreen = isFull;
        
        Debug.Log($"[Settings] Applied: Volume {savedVolume}, Fullscreen {isFull}");
}
    void BindServices()
    {
        Container .Bind<LoadingSettings>().FromMethod(f =>
                     Resources
                    .LoadAll<LoadingSettingsSO>("Game/")
                    .Select(ls => new LoadingSettings(
                        ls.LoadingImages,
                        ls.TimeOfFade,
                        ls.Smoothness
                    ))
                    .First()
            )
            .AsSingle()
            .NonLazy();
        Container
            .Bind<GameFieldSettings>()
            .FromMethod(f =>
                Resources
                    .LoadAll<GameFieldSettingsSO>("Game/")
                    .Select(ls => new GameFieldSettings(
                        ls.cellSize,
                        ls.tickPerSecond,
                        ls.range,
                        ls.defaultDistributionPriority,
                        ls.selectBuildingColor,
                        ls.makeAsDemolitionBuidlingColor,
                        ls.forceDestoryBuidlingColor,
                        ls.BluePrintPhantomConfig,
                        ls.DemolitionAndFalsePhantomConfig,
                        ls.ForceDestroyPhantomConfig,
                        ls.removeLayer,
                        ls.ConnectionColor,
                        ls.ConnectionPulseColor,
                        ls.DisconnectColor,
                        ls.DisconnectPulseColor,
                        ls.EnergyLine
                    ))
                    .First()
            )
            .AsSingle()
            .NonLazy();
        Container.Bind<Button>().FromInstance(button).AsSingle();
        Container.Bind<IReadOnlyLoadingSettings>().To<LoadingSettings>().FromResolve();
        Container.Bind<IReadOnlyGameFieldSettings>().To<GameFieldSettings>().FromResolve();
        Container.Bind<IReadOnlyPhantomConfig>().To<GameFieldSettings>().FromResolve();
        Container.Bind<IReadOnlyOutLineConfig>().To<GameFieldSettings>().FromResolve();
        Container.Bind<IReadOnlyEnergyLineConfig>().To<GameFieldSettings>().FromResolve();
        var loadingScreen = Container.InstantiatePrefabForComponent<LoadingScreen>(
            _loadingScreenPrefab
        );
        Container
            .Bind<ILoadingService>()
            .To<LoadingService>()
            .AsSingle()
            .WithArguments(loadingScreen);

        Container.Bind<SceneLoader>().AsSingle();
        Container.BindInterfacesAndSelfTo<SaveService>().AsSingle().NonLazy();
        Container.Bind<ConfigService>().AsSingle();
    }

    void BindСonfigsPoint()
    {
        Container.BindInterfacesAndSelfTo<BootStrapper>().AsSingle().NonLazy();

        Container
            .Bind<IReadOnlyBuildingInfo>()
            .FromMethod(ctx => ctx.Container.Resolve<ConfigService>())
            .AsSingle();

        Container
            .Bind<IReadOnlyItemsInfo>()
            .FromMethod(ctx => ctx.Container.Resolve<ConfigService>())
            .AsSingle();

        Container
            .Bind<IReadOnlyRecipeInfo>()
            .FromMethod(ctx => ctx.Container.Resolve<ConfigService>())
            .AsSingle();
        Container
            .Bind<IReadOnlyTypeBuildingButtonInfo>()
            .FromMethod(ctx => ctx.Container.Resolve<ConfigService>())
            .AsSingle();
        Container
            .Bind<IReadOnlyEnemyBaseConfig>()
            .FromMethod(ctx => ctx.Container.Resolve<ConfigService>())
            .AsSingle();
    }
}
