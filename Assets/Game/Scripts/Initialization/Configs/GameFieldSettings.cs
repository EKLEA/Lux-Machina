using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

public interface IReadOnlyGameFieldSettings
{
    float cellSize { get; }
    int tickPerSecond { get; }
    DistributionPriority defaultDistributionPriority { get; set; }
    Color selectBuildingColor{get;}
    Color makeAsDemolitionBuidlingColor{get;}
    Color forceDestoryBuidlingColor{get;}
    LayerMask removeLayer{get;}
}

public class GameFieldSettings : IReadOnlyGameFieldSettings
{
    public float cellSize { get; private set; }

    public int tickPerSecond { get; private set; }

    public DistributionPriority defaultDistributionPriority { get; set; }
    public Color selectBuildingColor { get; private set; }
    public Color makeAsDemolitionBuidlingColor{ get; private set; }
    public Color forceDestoryBuidlingColor { get; private set; }

    public LayerMask removeLayer { get; private set; }

    public GameFieldSettings(
        float cellSize,
        int tickPerSecond,
        DistributionPriority distributionPriority,
        Color selectBuildingColor,
        Color makeAsDemolitionBuidlingColor,
        Color forceDestoryBuidlingColor,
        LayerMask removeLayer
    )
    {
        this.tickPerSecond = tickPerSecond;
        this.cellSize = cellSize;
        this.defaultDistributionPriority = distributionPriority;
        this.selectBuildingColor=selectBuildingColor;
        this.makeAsDemolitionBuidlingColor=makeAsDemolitionBuidlingColor;
        this.forceDestoryBuidlingColor=forceDestoryBuidlingColor;
        this.removeLayer=removeLayer;
    }
}

