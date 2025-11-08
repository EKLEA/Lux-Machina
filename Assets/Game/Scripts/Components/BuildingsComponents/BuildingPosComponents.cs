#nullable disable
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct BuildingData : IComponentData
{
    public int BuildingIDHash;
    public int UniqueIDHash;
}
public struct RoadTag : IComponentData{}

[Serializable]
public struct BuildingPosData : IComponentData
{
    public int2 LeftCornerPos;
    public int Rotation;
    public int2 Size;
}
public struct MapPointData : IBufferElementData
{
    public int2 Value;
}

public struct CreateRoadSegmentsTag : IComponentData { }
public struct CheckForBreaksTag : IComponentData {}


