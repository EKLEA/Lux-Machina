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
    public Dictionary<int, HealthData> healthDatas;
    public Dictionary<int, List<SlotData>> SlotDatas;
    public Dictionary<int, HasOutputSlots> outputSlots;
    public Dictionary<int, HasOutputSlots> inputSlots;
    public Dictionary<int, BuildingWorkWithItemsLogicData> buildingWorkWithItemsLogicDatas;
    public Dictionary<int, ProcessBuildingData> processBuildingDatas;
    public HashSet<int> phantomBuildings;
    public PlayerCamData camData;
}
