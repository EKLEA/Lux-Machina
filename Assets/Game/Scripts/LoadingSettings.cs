using System.Collections.Generic;
using UnityEngine;

public interface IReadOnlyLoadingSettings
{
    Sprite[] loadingImages { get; }
    float TimeOfFade { get; }
    float smoothness { get; }
}

public class LoadingSettings : IReadOnlyLoadingSettings
{
    public Sprite[] loadingImages { get; private set; }

    public float TimeOfFade { get; private set; }

    public float smoothness { get; private set; }
    public LoadingSettings(LoadingSettingsSO info)
    {
        loadingImages = info.loadingImages;
        TimeOfFade = info.timeOfFade;
        smoothness = info.smoothness;
    }
}
[CreateAssetMenu(menuName = "LoadingSettings")]
public class LoadingSettingsSO : ScriptableObject
{
    public Sprite[] loadingImages;
    [Range(0, 5)][SerializeField] public float timeOfFade;
    [Min(1)][SerializeField] public float smoothness;
}