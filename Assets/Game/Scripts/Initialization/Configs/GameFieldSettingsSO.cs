using UnityEngine;

[CreateAssetMenu(menuName = "GameFieldSettings")]
public class GameFieldSettingsSO : ScriptableObject
{
    [Range(0, 5)]
    [SerializeField]
    public float cellSize;

    [Range(10, 30)]
    [SerializeField]
    public int tickPerSecond;
}
