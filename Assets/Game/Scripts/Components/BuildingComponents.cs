using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct BuildingData : IComponentData
{
    public int BuildingIDHash;
    public int BuildingUniqueID;
}
[Serializable]
public struct BuildingPosData : IComponentData
{
    public int2 LeftCornerPos;
    public int2 size;
    public int Rotation;
}
public class BuildingOnSceneReference : IComponentData
{   
    public BuildingOnScene buildingOnScene;
}
