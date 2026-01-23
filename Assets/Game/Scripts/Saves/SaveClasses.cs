using System;
using Unity.Mathematics;

[Serializable]
public class ProcessorBuildingSaveData : BaseBuildingSaveData
{
}
[Serializable]
public class ConsumerBuildingSaveData : BaseBuildingSaveData
{
}
[Serializable]
public class ProducerBuildingSaveData : BaseBuildingSaveData
{
}
[Serializable]
public class RoadSaveData
{
    public int2[] points;
    public bool isBlueprint;
}
public class BaseBuildingSaveData
{
    public int buildingID;
    public int2 buildingPosition;
    public int rotation;
    public bool isConnected;
    public bool isBlueprint;
}