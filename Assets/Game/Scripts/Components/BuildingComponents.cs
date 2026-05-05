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
    public int3 LeftCornerPos;
    public int3 size;
    public int Rotation;
    public float3 center;
}
public class BuildingOnSceneReference : IComponentData
{   
    public BuildingOnScene buildingOnScene;
}
