using UnityEngine;

[CreateAssetMenu(menuName = "LoadingSettings")]
public class LoadingSettingsSO : ScriptableObject
{
    public Sprite[] LoadingImages;

    [Range(0, 5)]
    [SerializeField]
    public float TimeOfFade;

    [Min(1)]
    [SerializeField]
    public float Smoothness;
}
