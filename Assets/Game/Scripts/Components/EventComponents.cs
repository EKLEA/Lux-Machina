using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct CreateBuildingEventData : IComponentData
{
    public int UniqueBuildingID;
    public int buildingID;
    public int2 buildingPosition;
    public int rotation;
}
public struct CreateEnemyEventData : IComponentData
{
    public int EnemyID;
    public float3 pos;
}
public struct SwitchIsOffCreateData : IComponentData
{
    public bool SwitchIsOff;
}
public struct LinkNetworkEnergyTo : IBufferElementData
{
    public int2 LinkToBuilding; //x=node , y entity
    public int2 LinkFromBuilding;
}
public struct UnLinkNetworkEnergyTo : IBufferElementData
{
    public int2 UnLinkToBuilding; //x=node , y entity
    public int2 UnLinkFromBuilding;
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

