using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class LoadingScreen : MonoBehaviour
{
    [SerializeField]
    private CanvasGroup _canvasGroup;

    [SerializeField]
    private CanvasGroup _blackScreen;

    [SerializeField]
    private Slider _progressBar;

    [SerializeField]
    private TextMeshProUGUI _progressText;

    [SerializeField]
    private GameObject _loadingPanel;

    [Inject]
    private LoadingSettings _loadingSettings;

    public async Task ShowBlackScreen(bool force)
    {
        if (!force)
            await FadeCanvas(_blackScreen, 0, 1);

        _blackScreen.alpha = 1;
        _blackScreen.blocksRaycasts = true;
        await Task.Yield();
    }

    public async Task ShowLoadingScreen()
    {
        await FadeCanvas(_canvasGroup, 0, 1);
        _canvasGroup.alpha = 1;
        _canvasGroup.blocksRaycasts = true;
        _blackScreen.alpha = 0;
        _blackScreen.blocksRaycasts = false;
        _loadingPanel.SetActive(true);
        await Task.Yield();
    }

    public async Task Hide(bool force)
    {
        if (!force)
            await FadeCanvas(_canvasGroup, 1f, 0f);
        _blackScreen.alpha = 0;
        _blackScreen.blocksRaycasts = true;
        _canvasGroup.alpha = 0;
        _canvasGroup.blocksRaycasts = false;
        _loadingPanel.SetActive(false);
        await Task.Yield();
    }

    private async Task FadeCanvas(CanvasGroup group, float fromAlpha, float toAlpha)
    {
        float duration = _loadingSettings.TimeOfFade;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            group.alpha = Mathf.Lerp(fromAlpha, toAlpha, progress);

            await Task.Yield();
        }
        group.alpha = toAlpha;
        await Task.Yield();
    }

    public void SetProgress(float progress)
    {
        _progressBar.value = progress;
        _progressText.text = $"{(progress * 100):F0}%";
    }
}
