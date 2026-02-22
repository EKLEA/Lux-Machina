using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;


[Serializable]
public class RoadSaveData
{
    public int2[] points;
    public bool isBlueprint;
    public bool IsDemolition;
}

[Serializable]
public class BaseBuildingSaveData
{
    public int buildingID;
    public int2 buildingPosition;
    public int rotation;
    public bool isBlueprint;
    public bool IsDemolition;
}
[Serializable] 
public class BuildingEnergyNetvorkLinkSaveData
{
    public List<(int2,int2)> entitesLink;//x-node y -entity
}
[Serializable]
public class ConstructionSlotsSaveData
{
    public bool isInputEnabled;
    public bool isOutputEnabled;
    public DistributionPriority priority;
    public List<InputConstructionSlotData> InputConstructionItems;
    public List<OutputConstructionSlotData> OutputConstructionItems;
}
[Serializable]
public class ExcessSlotsSaveData
{
    public List<ExcessSlotData> ExcessItems;
}
[Serializable]
public class RecipeAndCraftBuildingSaveData
{
    public int RecipeID;
    public float CurrTime;
    public float TimeToCraft;
    public bool isInputEnabled;
    public bool isOutputEnabled;
    public int ContOfPack;
    public DistributionPriority priority;
    public List<InputSlotData> InputCrafttems;
    public List<OutputSlotData> OutputCrafttems;
}
[Serializable]
public class StorageSlotsSaveData
{
    public List<StorageSlotData> slots;    
    public DistributionPriority priority;
}