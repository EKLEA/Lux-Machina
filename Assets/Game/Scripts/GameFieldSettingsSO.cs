
using UnityEngine;

[CreateAssetMenu(menuName = "GameFieldSettings")]
public class GameFieldSettingsSO : ScriptableObject
{
    [Range(0, 5)][SerializeField] public float cellSize;
}