using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Zenject;

public class BootStrapper : IInitializable
{
     readonly ILoadingService _loadingService;
     readonly SceneLoader _sceneLoader;
     readonly ConfigService _configService;
    
    public ConfigService ConfigService => _configService;

    [Inject]
    public BootStrapper(ILoadingService loadingService, SceneLoader sceneLoader, ConfigService configService)
    {
        _loadingService = loadingService;
        _sceneLoader = sceneLoader;
        _configService = configService;
    }
    
    public void Initialize()
    {
        LoadGameAsync();
    }
    
    public async void LoadGameAsync()
    {
        await _sceneLoader.LoadSceneAsync("MainMenu");
        await _loadingService.LoadWithProgressAsync(
            LoadConfigsAsync,
            LoadInitialAssetsAsync
        );
    }

    async Task LoadConfigsAsync()
    {
        await _configService.LoadConfigs();
        await Task.Delay(2000); 
    }

    async Task LoadInitialAssetsAsync()
    {
        await Task.Delay(2000); 
    }
}