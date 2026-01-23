using Unity.Entities;
using Unity.Mathematics;

public struct CreateBuildingEventData : IComponentData
{
    public int buildingID;
    public int2 buildingPosition;
    public int rotation;
    public bool isConnected;
}
public struct ProcessRoadPointsEventTag: IComponentData
{
    
}
public struct DeletePointFromMapTag: IComponentData{}
public struct CreateRoadEventTag: IComponentData
{
    
}
public struct ConnectEntities: IComponentData
{
    
}
public struct CreateFromSave : IComponentData
{
    public int UniqueIDHash;
}
public struct EntityToConnect : IBufferElementData
{
    public Entity entity;
}
