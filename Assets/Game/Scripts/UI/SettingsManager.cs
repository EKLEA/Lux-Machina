using UnityEngine;
using UnityEngine.UI; // Нужно для работы со слайдером

public class SettingsManager : MonoBehaviour
{
    [Header("UI Elements")]
    public Slider volumeSlider; 
    public Toggle toggle;

    private const string VolumeKey = "MasterVolume";
    private const string FullscreenKey = "IsFullscreen";

    void Start()
    {
        if (volumeSlider != null)
        {
            volumeSlider.value = AudioListener.volume ;
        }
        if (toggle != null)
        {
            toggle.isOn = PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
        }
    }

    public void SetVolume(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat(VolumeKey, volume);
    }

    public void ToggleFullscreen()
    {
        bool newFullscreenState = !Screen.fullScreen;
        Screen.fullScreen = newFullscreenState;

        // Сохраняем (bool сохраняем как int: 1 или 0)
        PlayerPrefs.SetInt(FullscreenKey, newFullscreenState ? 1 : 0);
        PlayerPrefs.Save();
    }

}
