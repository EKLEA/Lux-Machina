using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;


public struct BuildingMap : IComponentData
{
    public NativeParallelHashMap<int2, int> CellMapBuildings; 
    public NativeParallelHashMap<int2, int> CellMapIDs;
    public NativeParallelHashMap<int2, Entity> CellEntity;
}

public struct AddToMapTag : IComponentData {}
public struct RemoveMapPointsTag : IComponentData { }
public struct UpdateMapTag:IComponentData{}