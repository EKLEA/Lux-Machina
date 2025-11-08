using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

public interface IReadOnlyGameFieldSettings
{
    float cellSize { get; }
    int tickPerSecond { get; }
    DistributionPriority defaultDistributionPriority { get; set; }
}

public class GameFieldSettings : IReadOnlyGameFieldSettings
{
    public float cellSize { get; private set; }

    public int tickPerSecond { get; private set; }

    public DistributionPriority defaultDistributionPriority { get; set; }

    public GameFieldSettings(
        float cellSize,
        int tickPerSecond,
        DistributionPriority distributionPriority
    )
    {
        this.tickPerSecond = tickPerSecond;
        this.cellSize = cellSize;
        this.defaultDistributionPriority = defaultDistributionPriority;
    }
}
