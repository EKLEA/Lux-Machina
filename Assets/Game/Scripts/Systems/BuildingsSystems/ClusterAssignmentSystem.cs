using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RoadSystem))]
public partial class ClusterAssignmentSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (
            var (gameObjectRef, clusterId, entity) in SystemAPI
                .Query<GameObjectReference, ClusterId>()
                .WithEntityAccess()
                .WithChangeFilter<ClusterId>()
        )
        {
            if (gameObjectRef.gameObject == null) continue;
            
            if (SystemAPI.HasComponent<BuildingPosData>(entity))
            {
                if (clusterId.Value != -1)
                {
                    AssignBuildingCluster(gameObjectRef.gameObject, clusterId);
                }
            }
            else if (SystemAPI.HasComponent<RoadTag>(entity))
            {
                if (clusterId.Value != -1)
                {
                    AssignRoadCluster(gameObjectRef.gameObject, clusterId);
                }
            }
        }
    }

    void AssignBuildingCluster(BuildingOnScene buildingOnScene, ClusterId clusterId)
    {
        if (buildingOnScene.clusterID != clusterId.Value)
        {
            buildingOnScene.clusterID = clusterId.Value;
            
           
        }
    }

    void AssignRoadCluster(BuildingOnScene buildingOnScene, ClusterId clusterId)
    {
        if (buildingOnScene.clusterID != clusterId.Value)
        {
            buildingOnScene.clusterID = clusterId.Value;
            
           
        }
    }
}