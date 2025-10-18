using System;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

[Serializable]
public class GameStateData
{
    public Dictionary<int, PosData> posDatas;
    public Dictionary<int, RoadPosData> roadPosDatas;
    public Dictionary<int, BuildingPosData> buildingPosDatas;
    public Dictionary<int, HealthData> healthDatas;
    public Dictionary<int, InputStorageSlotData> inputSlotDatas;
    public Dictionary<int, OutputStorageSlotData> outPutSlotDatas;
    public Dictionary<int, BuildingLogicData> buildingLogicDatas;
    public Dictionary<int, ConsumerBuildingData> consumerBuildingDatas;
    public Dictionary<int, ProducerBuildingData> producerBuildingDatas;
    public PlayerCamData camData;
}