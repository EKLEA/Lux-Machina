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

[UpdateAfter(typeof(BuildingSaveSystem))]
public partial class BuildingCreateDestroyVisualSystem : SystemBase
{
    [Inject] BuildingObjectFactory _factorty;
    [Inject] EnemyFactory _enemyFactory;
    
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
        foreach(var (enemyData,entity) in SystemAPI.Query<CreateEnemyEventData>().WithEntityAccess())
        {
            _enemyFactory.CreateEnemy(enemyData.EnemyID,enemyData.pos);
            ecb.DestroyEntity(entity);
        }
        foreach (var (buildingRef,updateRoad,buff,entity) in SystemAPI.Query<BuildingOnSceneReference,EnabledRefRW<UpdateRoad>,DynamicBuffer<MapPoint>>().WithEntityAccess())
        {
            var nativeArray = buff.AsNativeArray();
            var managedArray = new MapPoint[nativeArray.Length];
            nativeArray.CopyTo(managedArray);
            nativeArray.Dispose();
            UpdateRoad(buildingRef.buildingOnScene as RoadOnScene,managedArray);
            updateRoad.ValueRW=false;
            Debug.Log(entity);
        }
        foreach (var (buildingOnSceneReference,entity) in SystemAPI.Query<BuildingOnSceneReference>().WithAll<ForceDestroyTag>().WithEntityAccess())
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
        
        if(buildingOnScene is EnergyBuildingOnScene)  (buildingOnScene as EnergyBuildingOnScene).SetUpNodes();
        ecb.SetComponent(building,new BuildingOnSceneReference{buildingOnScene=buildingOnScene});
        ecb.SetComponentEnabled<CreateVisualTag>(building,false);
    }
    void UpdateRoad(RoadOnScene roadOnScene,MapPoint[] managedArray)
    {
        var _roadPoints =managedArray.Select(f=>new int2(f.pos.x,f.pos.y));
        Dictionary<Vector2Int, bool> neighborsMap=new();
         
        var mapData = SystemAPI.GetSingleton<BuildingMap>();
        var buildingConfig = SystemAPI.GetSingleton<BuildingConfigReference>();
        var dirs = new NativeArray<int2>(4, Allocator.Temp);
        dirs[0] = new int2(1, 0);
        dirs[1] = new int2(-1, 0);
        dirs[2] = new int2(0, -1);
        dirs[3] = new int2(0, 1);
        foreach(var p in _roadPoints)
        {
            foreach(var dir in dirs)
            {
                var pos =p + dir;
                if (!_roadPoints.Contains(pos))
                {
                    if (mapData.CellMapBuildingsIDs.ContainsKey(pos))
                    {
                        neighborsMap.TryAdd(new Vector2Int(pos.x,pos.y),mapData.CellMapBuildingsIDs[pos]==buildingConfig.roadID);
                    }
                }
            }
        }
        roadOnScene.GenerateRoadMesh(_roadPoints.Select(f=>new Vector2Int(f.x,f.y)).ToArray(),neighborsMap);
    }
    void SpawnRoad(BuildingData buildingData,DynamicBuffer<MapPoint> points,Entity building,EntityCommandBuffer ecb)
    {
        var nativeArray = points.AsNativeArray();
        var managedArray = new MapPoint[nativeArray.Length];
        nativeArray.CopyTo(managedArray);
        nativeArray.Dispose();
      
        var  buildingOnScene=_factorty.CreateRoad(buildingData.BuildingIDHash, new Vector2Int[]{new Vector2Int(points[0].pos.x,points[0].pos.y)},null);

        UpdateRoad(buildingOnScene,managedArray);
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