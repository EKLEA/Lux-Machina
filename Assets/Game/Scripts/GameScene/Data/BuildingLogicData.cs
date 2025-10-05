using System;
using System.Collections.Generic;

[Serializable]
public class BuildingLogicData
{
    public string UnicID;
    public string buildingID;
    public Dictionary<int, SlotData> innerStorageSlots;
    public Priority priority;
}
public class BuildingProductionLogicData : BuildingLogicData
{
    public string recipeID;
    public Dictionary<int, SlotData> outerStrorageSlots;
}
public enum Priority : int
{
    Low = 0,
    MiddleLow = 1,
    Middle = 2,
    HightMiddle = 3,
    Hight=4,
}