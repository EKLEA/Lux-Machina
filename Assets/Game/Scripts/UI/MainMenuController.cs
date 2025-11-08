using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

public class MainMenuController : MonoBehaviour
{
    [SerializeField]
    CanvasGroup[] Screens;

    [Inject]
    LoadingSettings loadingSettings;
    CanvasGroup currScreen;

    [Inject]
    SceneLoader _sceneLoader;

    [Inject]
    SaveService saveService;

    [Inject]
    ILoadingService loadingService;
    private bool isTransitioning = false;

    void Awake()
    {
        currScreen = Screens[0];
        currScreen.alpha = 1f;
        currScreen.gameObject.SetActive(true);
        foreach (var screen in Screens)
        {
            if (screen != currScreen)
            {
                screen.alpha = 0f;
                screen.gameObject.SetActive(false);
            }
        }
    }

    public async void LoadGameAsync(int index)
    {
        await loadingService.ShowBlackScreenForce(false);
        saveService.saveIndex = index;
        await _sceneLoader.LoadSceneAsync("GameScene");
    }

    public async void ChangeScreenTo(CanvasGroup nextScreen)
    {
        if (isTransitioning)
            return;

        isTransitioning = true;
        await ChangeScreenFade(currScreen, nextScreen);
        currScreen = nextScreen;
        isTransitioning = false;
    }

    async Task ChangeScreenFade(CanvasGroup from, CanvasGroup to)
    {
        await Fader(from);
        from.gameObject.SetActive(false);
        to.gameObject.SetActive(true);
        await Fader(to);
    }

    async Task Fader(CanvasGroup screen)
    {
        float duration = loadingSettings.TimeOfFade;
        float targetAlpha = screen.alpha == 1 ? 0 : 1;
        float startAlpha = screen.alpha - targetAlpha;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            screen.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);

            await Task.Yield();
        }
        screen.alpha = targetAlpha;
    }
}
