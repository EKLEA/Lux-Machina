using System;
using System.Collections.Generic;
using Unity.Mathematics;

[Serializable]
public class GameStateData
{
    public PlayerCamData camData;
    public Dictionary<int,BaseBuildingSaveData> baseBuildings;
    public Dictionary<int,ProcessorBuildingSaveData> ProcessorsBuildings;
    public Dictionary<int,ConsumerBuildingSaveData> ConsumerBuildings;
    public Dictionary<int,ProducerBuildingSaveData> ProducerBuildings;
    public Dictionary<int,RoadSaveData> RoadsBuildings;
}