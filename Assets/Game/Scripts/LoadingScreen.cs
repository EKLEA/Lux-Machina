using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using Unity.Mathematics;
using System;
using Zenject;
using Unity.VisualScripting;

public class LoadingScreen : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Slider _progressBar;
    [SerializeField] private TextMeshProUGUI _progressText;
    [SerializeField] private GameObject _loadingPanel;
    [Inject] LoadingSettings loadingSettings;
    
    public async Task Show(bool force)
    {
       if(!force)await Fader();
        _canvasGroup.alpha = 1;
        _canvasGroup.blocksRaycasts = true;
        
    }
    
    public async Task Hide(bool force)
    {
        if(!force)await Fader();
        _canvasGroup.alpha = 0;
        _canvasGroup.blocksRaycasts = false;
    }
    async Task Fader()
    {
        float duration = loadingSettings.TimeOfFade;
        float targetAlpha = _canvasGroup.alpha == 1 ? 0 : 1;
        float startAlpha = _canvasGroup.alpha-targetAlpha;
        
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            
            await Task.Yield(); 
        }
        _canvasGroup.alpha = targetAlpha;
    }
    public void SetProgress(float progress)
    {
        _progressBar.value = progress;
        _progressText.text = $"{(progress * 100):F0}%";
    }
}