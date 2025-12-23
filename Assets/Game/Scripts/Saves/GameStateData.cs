using System;
using System.Collections.Generic;
using Unity.Mathematics;

[Serializable]
public class GameStateData
{
    public Dictionary<int, BuildingData> buildingDatas;
    public HashSet<int2> roadPoints;
    public HashSet<int2> phantomPoints;
    public Dictionary<int, BuildingPosData> buildingPosDatas;
    public Dictionary<int, BuildingPriorityData> buildingsPriorityDatas;
    public Dictionary<int, HealthData> healthDatas;
    public Dictionary<int, HashSet<(int ind,SlotData slotData)> > slotDatas;
    public Dictionary<int, OutputSlots> outputSlots;
    public Dictionary<int, OutputSlots> inputSlots;
    public Dictionary<int, ExcessItemSlots> excessItemSlots;
    public Dictionary<int, ProcessBuildingData> processBuildingDatas;
    public Dictionary<int, ChangeRecipeData> changeRecipeDatas;
    public Dictionary<int, ChangePriorityData> changePrioritDatas;
    public Dictionary<int, List<ChangeSlotCapacityData>> changeSlotCapacitDatas;
    public Dictionary<int, ChangeBuildingCountOfPackData> changeBuildingCountOfPackDatas;
    public HashSet<int> canResoucesBeAddedTag;
    public HashSet<int> canResoucesBeRemovedTag;
    public HashSet<int>  canAnimateTag;
    public HashSet<int> phantomBuildings;
    public PlayerCamData camData;
}