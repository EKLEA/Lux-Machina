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

         foreach (var (cluster, buildingRef) in SystemAPI.Query<RefRO<ClusterId>, BuildingOnSceneReference>())
        {
            buildingRef.buildingOnScene.clusterID = cluster.ValueRO.Value;
        }
    }
    
}