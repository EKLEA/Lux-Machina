using System;
using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

public interface IReadOnlyGameFieldSettings
{
    float cellSize { get; }
    int tickPerSecond { get; }
    DistributionPriority defaultDistributionPriority { get; set; }
   
    LayerMask removeLayer{get;}
}
public interface IReadOnlyOutLineConfig
{
    Color selectBuildingColor{get;}
    Color makeAsDemolitionBuidlingColor{get;}
    Color forceDestoryBuidlingColor{get;}
}
public interface IReadOnlyPhantomConfig
{
    
     PhantomConfig BluePrintPhantomConfig{get;}
     PhantomConfig DemolitionAndFalsePhantomConfig{get;}
     PhantomConfig ForceDestroyPhantomConfig{get;}
}
public class GameFieldSettings : IReadOnlyGameFieldSettings,IReadOnlyOutLineConfig,IReadOnlyPhantomConfig
{
    public float cellSize { get; private set; }

    public int tickPerSecond { get; private set; }

    public DistributionPriority defaultDistributionPriority { get; set; }
    public Color selectBuildingColor { get; private set; }
    public Color makeAsDemolitionBuidlingColor{ get; private set; }
    public Color forceDestoryBuidlingColor { get; private set; }

    public PhantomConfig BluePrintPhantomConfig{ get; private set; }
    public PhantomConfig DemolitionAndFalsePhantomConfig{ get; private set; }
    public PhantomConfig ForceDestroyPhantomConfig{ get; private set; }
    public LayerMask removeLayer { get; private set; }

    public GameFieldSettings(
        float cellSize,
        int tickPerSecond,
        DistributionPriority distributionPriority,
        Color selectBuildingColor,
        Color makeAsDemolitionBuidlingColor,
        Color forceDestoryBuidlingColor,
        PhantomConfig bluePrint,
        PhantomConfig demolition,
        PhantomConfig forcedestroy,
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
        this.BluePrintPhantomConfig=bluePrint;
        this.DemolitionAndFalsePhantomConfig=demolition;
        this.ForceDestroyPhantomConfig=forcedestroy;
    }
}
[Serializable]
public class PhantomConfig
{
    public Color MainColor;
    public Color LineColor;
}
