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
public partial class BuildingCreateDestroyVisualSystem : SystemBase
{
    [Inject] BuildingObjectFactory _factorty;
    
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (buildingData,posData,entity) in SystemAPI.Query<BuildingData,BuildingPosData>().WithAll<CreateVisualTag>().WithEntityAccess())
        {
            SpawnBuilding(buildingData,posData,entity,ecb);
        }
        foreach (var (buildingData,points,entity) in SystemAPI.Query<BuildingData,DynamicBuffer<MapPoint>>().WithAll<CreateVisualTag>().WithEntityAccess())
        {
            SpawnRoad(buildingData,points,entity,ecb);
        }
        foreach (var (buildingOnSceneReference,entity) in SystemAPI.Query<BuildingOnSceneReference>().WithAll<DestroyVisualTag>().WithEntityAccess())
        {
            DeleteVisual(buildingOnSceneReference,entity,ecb);
        }


        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    void SpawnBuilding(BuildingData buildingData,BuildingPosData posData,Entity building,EntityCommandBuffer ecb)
    {
        var buildingOnScene=_factorty.CreateBuilding(buildingData.BuildingIDHash,
                                                    new Vector2Int(posData.LeftCornerPos.x,posData.LeftCornerPos.y),
                                                    posData.Rotation);
        buildingOnScene.id=buildingData.BuildingUniqueID;                                      
        ecb.SetComponent(building,new BuildingOnSceneReference{buildingOnScene=buildingOnScene});
        ecb.SetComponentEnabled<CreateVisualTag>(building,false);
    }

    void SpawnRoad(BuildingData buildingData,DynamicBuffer<MapPoint> points,Entity building,EntityCommandBuffer ecb)
    {
        var nativeArray = points.AsNativeArray();
        var managedArray = new MapPoint[nativeArray.Length];
        nativeArray.CopyTo(managedArray);
        var  buildingOnScene=_factorty.CreateRoad(buildingData.BuildingIDHash, managedArray.Select(f=>new Vector2Int(f.pos.x,f.pos.y)).ToArray());
        buildingOnScene.id=buildingData.BuildingUniqueID; 
        ecb.SetComponent(building,new BuildingOnSceneReference{buildingOnScene=buildingOnScene});
        ecb.SetComponentEnabled<CreateVisualTag>(building,false);
    }
    void DeleteVisual(BuildingOnSceneReference reference,Entity building,EntityCommandBuffer ecb)
    {
        _factorty.DestoryObject(reference.buildingOnScene);
        ecb.SetComponent(building,new BuildingOnSceneReference{buildingOnScene=null});
    }
}