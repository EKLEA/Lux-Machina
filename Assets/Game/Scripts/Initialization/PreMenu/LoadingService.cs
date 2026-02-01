using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public interface ILoadingService
{
    UniTask LoadWithProgressAsync(params Func<UniTask>[] loadTasks);
    UniTask ShowLoadingScreen();
    UniTask HideLoadingScreen();
    UniTask ShowBlackScreenForce(bool b);
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

    public async UniTask LoadWithProgressAsync(params Func<UniTask>[] loadTasks)
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
        
        _loadingScreen.SetProgress(0f);
    }

    public async UniTask ShowBlackScreenForce(bool b) => await _loadingScreen.ShowBlackScreen(b);

    public async UniTask ShowLoadingScreen() => await _loadingScreen.ShowLoadingScreen();

    public async UniTask HideLoadingScreen() => await _loadingScreen.Hide(false);
}
