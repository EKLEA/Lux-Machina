using System;
using Unity.Mathematics;
using Unity.Collections;


[Serializable]
public struct ManyPointsBuildingSaveData
{
    
    public int buildingID;
    public FixedList512Bytes<int2> points; 
    public bool isBlueprint;
    public bool IsDemolition;
}

[Serializable]
public struct BaseBuildingSaveData
{
    public int buildingID;
    public int2 buildingPosition;
    public int rotation;
    public bool isBlueprint;
    public bool IsDemolition;
}

[Serializable] 
public struct BuildingEnergyNetvorkLinkSaveData
{
    public FixedList128Bytes<EntityLink> entitesLink; 
    public bool isSwitchOff;
}

[Serializable]
public struct EntityLink
{
    public int2 from;
    public int2 to;
}

[Serializable]
public struct ConstructionSlotsSaveData
{
    public bool isInputEnabled;
    public bool isOutputEnabled;
    public DistributionPriority priority;
    public FixedList512Bytes<InputConstructionSlotData> InputConstructionItems;
    public FixedList512Bytes<OutputConstructionSlotData> OutputConstructionItems;
}

[Serializable]
public struct ExcessSlotsSaveData
{
    public FixedList512Bytes<ExcessSlotData> ExcessItems;
}

[Serializable]
public struct RecipeAndCraftBuildingSaveData
{
    public int RecipeID;
    public float CurrTime;
    public float TimeToCraft;
    public bool isInputEnabled;
    public bool isOutputEnabled;
    public int ContOfPack;
    public DistributionPriority priority;
    public FixedList512Bytes<InputSlotData> InputCrafttems;
    public FixedList512Bytes<OutputSlotData> OutputCrafttems;
}

[Serializable]
public struct StorageSlotsSaveData
{
    public FixedList512Bytes<StorageSlotData> slots;    
    public DistributionPriority priority;
}
