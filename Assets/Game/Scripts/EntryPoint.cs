using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Zenject;

public class EntryPoint : IInitializable
{
    ILoadingService _loadingService;
    SceneLoader _sceneLoader;
    ConfigService _configService;
    
    public ConfigService ConfigService => _configService;
    
    public EntryPoint(ILoadingService loadingService, SceneLoader sceneLoader)
    {
        _loadingService = loadingService;
        _sceneLoader = sceneLoader;
        _configService = new ConfigService();
    }
    
    public async void Initialize()
    {
        await LoadGameAsync();
    }
    
    private async Task LoadGameAsync()
    {
        await _sceneLoader.LoadSceneAsync("MainMenu");
        await _loadingService.LoadWithProgressAsync(
            LoadConfigsAsync,
            LoadInitialAssetsAsync
        );
    }

    private async Task LoadConfigsAsync()
    {
        await _configService.LoadConfigs();
        await Task.Delay(2000); 
    }

    private async Task LoadInitialAssetsAsync()
    {
        await Task.Delay(2000); 
    }
}