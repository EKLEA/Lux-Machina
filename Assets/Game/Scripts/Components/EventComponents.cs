using Unity.Entities;
using Unity.Mathematics;

public struct CreateBuildingEventData : IComponentData
{
    public int UniqueBuildingID;
    public int buildingID;
    public int2 buildingPosition;
    public int rotation;
    public bool isConnected;
}
public struct ProcessRoadPointsEventTag: IComponentData
{
    
}
public struct DeleteRoadPointsFromMap: IComponentData{public bool isForce;}
public struct DeleteManyPointsFromMap: IComponentData{public bool isForce;}
public struct CreateRoadEventTag: IComponentData
{
    public int UniqueBuildingID;
}
public struct ConnectEntities: IComponentData
{
    
}
public struct EntityToConnect : IBufferElementData
{
    public Entity entity;
}
