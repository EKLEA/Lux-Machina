using Unity.Entities;
using Unity.Collections;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RoadSystem))]
public partial class ClusterAssignmentSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (building, clusterId, entity) in SystemAPI.Query<GameObjectReference, ClusterId>()
                     .WithAll<BuildingPosData>()
                     .WithEntityAccess()
                     .WithChangeFilter<ClusterId>())
        {
            if (clusterId.Value != -1) 
            {
                AssignBuildingCluster(building.gameObject, clusterId);
            }
        }

        foreach (var (road, clusterId, entity) in SystemAPI.Query<GameObjectReference, ClusterId>()
                     .WithAll<RoadTag>()
                     .WithEntityAccess()
                     .WithChangeFilter<ClusterId>()) 
        {
            if (clusterId.Value != -1) 
            {
                AssignRoadCluster(road.gameObject, clusterId);
            }
        }
    }

    void AssignBuildingCluster(BuildingOnScene building, ClusterId clusterId)
    {
        if (building.clusterID != clusterId.Value)
        {
            building.clusterID = clusterId.Value;
            Debug.Log($"Building {building.id} assigned to cluster {clusterId.Value}");
            
        }
    }

    void AssignRoadCluster(BuildingOnScene road, ClusterId clusterId)
    {
        if (road.clusterID != clusterId.Value)
        {
            road.clusterID = clusterId.Value;
            Debug.Log($"Road {road.id} assigned to cluster {clusterId.Value}");
            
        }
    }
}