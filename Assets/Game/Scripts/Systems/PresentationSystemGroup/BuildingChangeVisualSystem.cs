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
[UpdateAfter(typeof(BuildingCreateDestroyVisualSystem))]
public partial class BuildingChangeVisualSystem : SystemBase
{
    [Inject] VisualBuildingFactory _visualBuildingFactory;
    [Inject] IReadOnlyBuildingInfo _buildingInfo;
    [Inject] EntityManager _entityManager;
    
    protected override void OnUpdate()
    {
        
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        Entity mapEntity = SystemAPI.GetSingletonEntity<BuildingMap>();
        foreach (var (reference,entity) in SystemAPI.Query<BuildingOnSceneReference>().WithAll<ChangeBluePrintState>().WithEntityAccess())
        {
            ChangeBluePrintState(reference,entity,ecb);
            ecb.SetComponentEnabled<UpdateClusterSlots>(mapEntity,true);
        }
        foreach (var (reference,entity) in SystemAPI.Query<BuildingOnSceneReference>().WithAll<ChangeDemolitionStateTag>().WithEntityAccess())
        {
            ChangeDemolitionState(reference,entity,ecb);
            ecb.SetComponentEnabled<UpdateClusterSlots>(mapEntity,true);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    void ChangeBluePrintState(BuildingOnSceneReference gameobject, Entity building,EntityCommandBuffer ecb)
    {
        var MapData= SystemAPI.GetSingleton<BuildingMap>();
        if (EntityManager.IsComponentEnabled<IsBlueprint>(building))
        {
            _visualBuildingFactory.UnPhantomizeObject(gameobject.buildingOnScene.gameObject);       
            ecb.SetComponentEnabled<IsBlueprint>(building,false);
            if (MapData.CellEntityMultiMap.ContainsKey(building))
            {
                if (EntityManager.HasComponent<BuildingPosData>(building))
                {
                    var buildingPosData= EntityManager.GetComponentData<BuildingPosData>(building);
                    for(int x = 0; x < buildingPosData.size.x;x++)
                    {
                        for(int y = 0; y< buildingPosData.size.y;y++)
                        {
                            MapData.IsBluePrintOrDemolitionPoints.Remove(buildingPosData.LeftCornerPos+new int2(x,y));
                        }
                    }
                }
                else
                {
                    var points= EntityManager.GetBuffer<MapPoint>(building);
                    foreach(var p in points)
                    {
                        MapData.IsBluePrintOrDemolitionPoints.Remove(p.pos);
                    }
                }
            }

        }
        else
        {
            _visualBuildingFactory.PhantomizeObject(gameobject.buildingOnScene.gameObject);       
            ecb.SetComponentEnabled<IsBlueprint>(building,true);
            if (MapData.CellEntityMultiMap.ContainsKey(building))
            {
                if (EntityManager.HasComponent<BuildingPosData>(building))
                {
                    var buildingPosData= EntityManager.GetComponentData<BuildingPosData>(building);
                    for(int x = 0; x < buildingPosData.size.x;x++)
                    {
                        for(int y = 0; y< buildingPosData.size.y;y++)
                        {
                            MapData.IsBluePrintOrDemolitionPoints.Add(buildingPosData.LeftCornerPos+new int2(x,y),true);
                        }
                    }
                }
                else
                {
                    var points= EntityManager.GetBuffer<MapPoint>(building);
                    foreach(var p in points)
                    {
                        MapData.IsBluePrintOrDemolitionPoints.Add(p.pos,true);
                    }
                }
            }
        }
        ecb.SetComponentEnabled<ChangeBluePrintState>(building,false);
    }
    void ChangeDemolitionState(BuildingOnSceneReference gameobject, Entity building,EntityCommandBuffer ecb)
    {
        
        var MapData= SystemAPI.GetSingleton<BuildingMap>();
        if(_entityManager.HasComponent<CanCraft>(building)) ecb.SetComponentEnabled<CanCraft>(building,false);
        if (EntityManager.IsComponentEnabled<IsDemolition>(building))
        {
            _visualBuildingFactory.UnDemolitionObject(gameobject.buildingOnScene.gameObject);       
            ecb.SetComponentEnabled<IsDemolition>(building,false);
            if (MapData.CellEntityMultiMap.ContainsKey(building))
            {
                if (EntityManager.HasComponent<BuildingPosData>(building))
                {
                    var buildingPosData= EntityManager.GetComponentData<BuildingPosData>(building);
                    for(int x = 0; x < buildingPosData.size.x;x++)
                    {
                        for(int y = 0; y< buildingPosData.size.y;y++)
                        {
                            MapData.IsBluePrintOrDemolitionPoints.Remove(buildingPosData.LeftCornerPos+new int2(x,y));
                        }
                    }
                }
                else
                {
                    var points= EntityManager.GetBuffer<MapPoint>(building);
                    foreach(var p in points)
                    {
                        MapData.IsBluePrintOrDemolitionPoints.Remove(p.pos);
                    }
                }
            }

        }
        else
        {
            _visualBuildingFactory.DemolitionObject(gameobject.buildingOnScene.gameObject);       
            ecb.SetComponentEnabled<IsDemolition>(building,true);
            if (MapData.CellEntityMultiMap.ContainsKey(building))
            {
                if (EntityManager.HasComponent<BuildingPosData>(building))
                {
                    var buildingPosData= EntityManager.GetComponentData<BuildingPosData>(building);
                    for(int x = 0; x < buildingPosData.size.x;x++)
                    {
                        for(int y = 0; y< buildingPosData.size.y;y++)
                        {
                            MapData.IsBluePrintOrDemolitionPoints.Add(buildingPosData.LeftCornerPos+new int2(x,y),false);
                        }
                    }
                }
                else
                {
                    var points= EntityManager.GetBuffer<MapPoint>(building);
                    foreach(var p in points)
                    {
                        MapData.IsBluePrintOrDemolitionPoints.Add(p.pos,false);
                    }
                }
            }
        }
        ecb.SetComponentEnabled<ChangeDemolitionStateTag>(building,false);
    }
}