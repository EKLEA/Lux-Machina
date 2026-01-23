using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;


public struct BuildingMap : IComponentData, IDisposable
{
    public NativeParallelHashMap<int2, int> CellMapBuildingsIDs; 
    public NativeParallelHashMap<int2, Entity> CellMapEntites;
    public NativeParallelMultiHashMap<Entity, int2> CellEntityMultiMap;

    public void Dispose()
    {
        CellMapBuildingsIDs.Dispose();
        CellMapEntites.Dispose();
        CellEntityMultiMap.Dispose();
    }
}
public struct EntitiesDictionary: IComponentData, IDisposable
{
    public NativeParallelHashMap<int, Entity> Entities;

    public void Dispose()
    {
        Entities.Dispose();
    }
}
public struct ClusterMap : IComponentData, IDisposable
{
    public NativeList<int> clusterIDs;
    public NativeParallelMultiHashMap<int, Entity> producersSlots;
    public NativeParallelMultiHashMap<int, Entity> consumersSlots;
    public NativeParallelMultiHashMap<int, Entity> storagesSlots;
    public NativeParallelMultiHashMap<int, Entity> excessSlots;
    public NativeParallelMultiHashMap<int, Entity> bluePrintsSlots;
    public NativeParallelMultiHashMap<int, Entity> demolitionsSlots;
    public NativeParallelMultiHashMap<int, int2> roadsPoints;
    public NativeParallelHashMap<int2, int> pointToClusterId;
    public void Dispose()
    {
        clusterIDs.Dispose();
        producersSlots.Dispose();
        consumersSlots.Dispose();
        storagesSlots.Dispose();
        excessSlots.Dispose();
        bluePrintsSlots.Dispose();
        demolitionsSlots.Dispose();
        roadsPoints.Dispose();
        pointToClusterId.Dispose();
    }
}
public struct UpdateMapTag : IComponentData, IEnableableComponent { }
public struct UpdateCLustersTag:IComponentData, IEnableableComponent{}
public struct TickInfoData : IComponentData
{
    public int currTickPerSecond;
}
public struct ProductionTable : IComponentData, IDisposable
{
    public NativeParallelMultiHashMap<int, RecipeIngredientStruct> produced;
    public NativeParallelMultiHashMap<int, RecipeIngredientStruct> consumed;

    public void Dispose()
    {
        produced.Dispose();
        consumed.Dispose();
    }
}
public struct MapPoint : IBufferElementData
{
    public int2 pos;
}
public struct PathfindingRequest : IComponentData, IEnableableComponent
{
    public int2 Start;
    public int2 End;
}
public struct DisablePathfindingTag : IComponentData, IEnableableComponent { }
public struct MapUpdateRequest : IBufferElementData
{
    public int2 Pos;
    public int BuildingHash;
    public Entity Entity;
}