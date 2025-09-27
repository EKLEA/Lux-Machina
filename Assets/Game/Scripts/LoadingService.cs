using System;
using System.Threading.Tasks;
using UnityEngine;

public interface ILoadingService
{
    Task LoadWithProgressAsync(params Func<Task>[] loadTasks);
    void ShowLoadingScreen();
    void HideLoadingScreen();
}

public class LoadingService : ILoadingService
{
    private readonly LoadingScreen _loadingScreen;
    private bool _isFirstLoad = true;
    
    public LoadingService(LoadingScreen loadingScreen)
    {
        _loadingScreen = loadingScreen;
        
        if (_loadingScreen.transform.parent != null)
        {
            _loadingScreen.transform.SetParent(null);
        }
        
        UnityEngine.Object.DontDestroyOnLoad(_loadingScreen.gameObject);
        
        HideLoadingScreenImmediate(); 
    }
        
    public async Task LoadWithProgressAsync(params Func<Task>[] loadTasks)
    {
        Debug.Log("Показываем экран загрузки");
        
        if (_isFirstLoad)
        {
             ShowLoadingScreenImmediate();
            _isFirstLoad = false;
        }
        else
        {
             ShowLoadingScreen(); 
        }
        
        _loadingScreen.SetProgress(0f);
        
        float totalTasks = loadTasks.Length;
        for (int i = 0; i < loadTasks.Length; i++)
        {
            _loadingScreen.SetProgress(i / totalTasks);
            await loadTasks[i]();
            
            float progress = (i + 1) / totalTasks;
            _loadingScreen.SetProgress(progress);
        }
        
        _loadingScreen.SetProgress(1f);
        
        HideLoadingScreen();
    }
    
    public async void ShowLoadingScreen() => await _loadingScreen.Show(false);
    public async void HideLoadingScreen() => await _loadingScreen.Hide(false);
    
    private async void ShowLoadingScreenImmediate() => await _loadingScreen.Show(true);
    private async void HideLoadingScreenImmediate() => await _loadingScreen.Hide(true);
}