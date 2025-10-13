using System.Collections.Generic;
using UnityEngine;

public interface IReadOnlyGameFieldSettings
{
    float cellSize{ get; }
}

public class GameFieldSettings : IReadOnlyGameFieldSettings
{
    public  float cellSize{ get; private set; }
    public GameFieldSettings(float cellSize)
    {
        this.cellSize = cellSize;
    }
}