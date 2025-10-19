
using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
[Serializable]
public struct PosData : IComponentData
{
    public int BuildingIDHash;
    public int UnicIDHash;
}
[Serializable]
public struct BuildingPosData : IComponentData
{
    public int2 LeftCornerPos;
    public int Rotation;
    public int2 Size;
}
[Serializable]
public struct RoadPosData : IComponentData
{
    public int2 FirstPoint;
    public int2 EndPoint;
}
public struct BuildingTag : IComponentData { }
public struct RoadTag : IComponentData { }
public struct RoadModifiedTag : IComponentData { }
public struct BuildingModifiedTag : IComponentData { }
public struct PhantomTag: IComponentData { }

// Ссылка на GameObject (если нужна)
public class GameObjectReference : IComponentData
{
    public GameObject BuildingOnScene;
}