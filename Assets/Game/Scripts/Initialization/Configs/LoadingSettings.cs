
using UnityEngine;

public interface IReadOnlyLoadingSettings
{
    Sprite[] LoadingImages { get; }
    float TimeOfFade { get; }
    float Smoothness { get; }
}

public class LoadingSettings : IReadOnlyLoadingSettings
{
    public Sprite[] LoadingImages { get; private set; }

    public float TimeOfFade { get; private set; }

    public float Smoothness { get; private set; }
    public LoadingSettings(Sprite[] loadingImages,float timeOfFade,float smoothness )
    {
        LoadingImages = loadingImages;
        TimeOfFade = timeOfFade;
        Smoothness = smoothness;
    }
}