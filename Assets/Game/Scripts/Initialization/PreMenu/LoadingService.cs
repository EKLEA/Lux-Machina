using System;
using System.Threading.Tasks;
using UnityEngine;

public interface ILoadingService
{
    Task LoadWithProgressAsync(params Func<Task>[] loadTasks);
    Task ShowLoadingScreen();
    Task HideLoadingScreen();
    Task ShowBlackScreenForce(bool b);
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
    }

    public async Task LoadWithProgressAsync(params Func<Task>[] loadTasks)
    {

        if (_isFirstLoad)
        {
            await ShowBlackScreenForce(true);
            _isFirstLoad = false;
        }

        await ShowLoadingScreen();
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

        await HideLoadingScreen();
    }

    public async Task ShowBlackScreenForce(bool b) => await _loadingScreen.ShowBlackScreen(b);
    public async Task ShowLoadingScreen() => await _loadingScreen.ShowLoadingScreen();
    public async Task HideLoadingScreen() => await _loadingScreen.Hide(false);
}