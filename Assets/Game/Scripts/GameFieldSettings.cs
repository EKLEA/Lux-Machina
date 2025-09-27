using System.Collections.Generic;
using UnityEngine;

public interface IReadOnlyGameFieldSettings
{
    float cellSize{ get; }
}

public class GameFieldSettings : IReadOnlyGameFieldSettings
{
    public  float cellSize{ get; private set; }
    public GameFieldSettings(GameFieldSettingsSO info)
    {
        cellSize = info.cellSize;
    }
}
[CreateAssetMenu(menuName = "GameFieldSettings")]
public class GameFieldSettingsSO : ScriptableObject
{
    [Range(0, 5)][SerializeField] public float cellSize;
}