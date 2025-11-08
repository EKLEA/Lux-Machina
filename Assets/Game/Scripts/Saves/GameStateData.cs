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
    public Dictionary<int, List<InnerStorageSlotData>> innerSlotDatas;
    public Dictionary<int, List<OutputStorageSlotData>> outputSlotDatas;
    public Dictionary<int, BuildingLogicData> buildingLogicDatas;
    public Dictionary<int, ProducerBuildingData> producerBuildingDatas;
    public HashSet<int> phantomBuildings;
    public PlayerCamData camData;
}
