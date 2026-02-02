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
        foreach (var (buff,reference) in SystemAPI.Query<DynamicBuffer<InputConstructionSlotData>,BuildingOnSceneReference>().WithAll<IsBlueprint>().WithDisabled<ChangeBluePrintState>())
        {
            if(buff.Length<1) continue;
            int maxItems=0,currItem=0;
            foreach(var b in buff)
            {
                maxItems+=b.Capacity;
                currItem+=b.Amount;
            }
            _visualBuildingFactory.SetProgress(reference.buildingOnScene.gameObject,(float)currItem/maxItems);
        }
         foreach (var (buff,reference) in SystemAPI.Query<DynamicBuffer<OutputConstructionSlotData>,BuildingOnSceneReference>().WithAll<IsDemolition>().WithDisabled<ChangeDemolitionStateTag>())
        {
            
            if(buff.Length<1) continue;
            int maxItems=0,currItem=0;
            foreach(var b in buff)
            {
                maxItems+=b.Capacity;
                currItem+=b.Amount;
            }
            _visualBuildingFactory.SetProgress(reference.buildingOnScene.gameObject,(float)currItem/maxItems);
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
                            var pos =buildingPosData.LeftCornerPos+new int2(x,y);
                            if(MapData.IsBluePrintOrDemolitionPoints.ContainsKey(pos))
                                 MapData.IsBluePrintOrDemolitionPoints[pos]=true;
                            else
                                 MapData.IsBluePrintOrDemolitionPoints.Add(pos,true);
                        }
                    }
                }
                else
                {
                    var points= EntityManager.GetBuffer<MapPoint>(building);
                    foreach(var p in points)
                    {
                        if(MapData.IsBluePrintOrDemolitionPoints.ContainsKey(p.pos))
                            MapData.IsBluePrintOrDemolitionPoints[p.pos]=true;
                        else
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
            if(!EntityManager.IsComponentEnabled<IsBlueprint>(building))
                _visualBuildingFactory.DemolitionObject(gameobject.buildingOnScene.gameObject,false);
            else
                _visualBuildingFactory.PhantomizeObject(gameobject.buildingOnScene.gameObject);  
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
            _visualBuildingFactory.DemolitionObject(gameobject.buildingOnScene.gameObject,true);       
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
                            
                            var pos =buildingPosData.LeftCornerPos+new int2(x,y);
                            if(MapData.IsBluePrintOrDemolitionPoints.ContainsKey(pos))
                                
                                MapData.IsBluePrintOrDemolitionPoints[pos]=false;
                            else
                            {
                                MapData.IsBluePrintOrDemolitionPoints.Add(pos,false);
                            }
                        }
                    }
                }
                else
                {
                    var points= EntityManager.GetBuffer<MapPoint>(building);
                    foreach(var p in points)
                    {
                        if(MapData.IsBluePrintOrDemolitionPoints.ContainsKey(p.pos))
                            MapData.IsBluePrintOrDemolitionPoints[p.pos]=false;
                        else
                        {
                            MapData.IsBluePrintOrDemolitionPoints.Add(p.pos,false);
                        }
                    }
                }
            }
        }
        ecb.SetComponentEnabled<ChangeDemolitionStateTag>(building,false);
    }
}