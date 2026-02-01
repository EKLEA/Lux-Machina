using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Zenject;
[DisableAutoCreation]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(BuildingChangeVisualSystem))]
public partial class BuildingGameObjectClusterAssignSystem : SystemBase
{
    protected override void OnUpdate()
    {

         foreach (var (cluster, buildingRef) in SystemAPI.Query<RefRO<ClusterLink>, BuildingOnSceneReference>())
        {

            if (cluster.ValueRO.ClusterIds.Length > 0)
            {
                if(buildingRef.buildingOnScene!=null)
                {
                    if(buildingRef.buildingOnScene.clusterID==null||cluster.ValueRO.ClusterIds.Length !=buildingRef.buildingOnScene.clusterID.Length) buildingRef.buildingOnScene.clusterID=new int[cluster.ValueRO.ClusterIds.Length];
                    for(int i = 0; i < cluster.ValueRO.ClusterIds.Length; i++)
                    {
                        buildingRef.buildingOnScene.clusterID[i] = cluster.ValueRO.ClusterIds[i];
                    }
                }
            }
        }
    }
    
}