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
    
    [Inject] EntityManager _entityManager;
    [Inject] ConnectEnergyFactory _energyFactory;
    
    protected override void OnUpdate()
    {
        
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        Entity mapEntity = SystemAPI.GetSingletonEntity<BuildingMap>();
        EntitiesDictionary entitiesDictionary = SystemAPI.GetSingleton<EntitiesDictionary>();

        foreach (var (turretData, reference) in SystemAPI.Query<RefRO<TurretTranform>, BuildingOnSceneReference>())
        { 
            if(!(reference.buildingOnScene is TurretOnScene view)) return;
            if (view == null || view.TurretHead == null || view.TurretBarrel == null) continue;
            float deltaTime = SystemAPI.Time.DeltaTime;
            float lerpSpeed = 10f; 

            view.TurretHead.localRotation = math.slerp(
                view.TurretHead.localRotation, 
                quaternion.Euler(0, turretData.ValueRO.rotation.y, 0),
                deltaTime * lerpSpeed
            );

            view.TurretBarrel.localRotation = math.slerp(
                view.TurretBarrel.localRotation, 
                quaternion.Euler(turretData.ValueRO.rotation.x, 0, 0), 
                deltaTime * lerpSpeed
            );
        }
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
        foreach(var (data,energyData, reference, connect, entity) in SystemAPI.Query<BuildingData,EnergyBuildingData, BuildingOnSceneReference, EnabledRefRW<UpdateConnectStatus>>().WithDisabled<MarkOnMap>().WithEntityAccess())
        {
            var en = reference.buildingOnScene as EnergyBuildingOnScene;
            foreach(var c in energyData.connections)
            {
                if(c.Item2.y != -1 && entitiesDictionary.Entities.TryGetValue(c.Item2.y, out Entity targetEntity))
                {
                    bool selfHasPower = EntityManager.IsComponentEnabled<IsConnectedToEnergy>(entity) 
                                        && !EntityManager.IsComponentEnabled<SwitchIsOff>(entity);
                    
                    bool targetHasPower = EntityManager.IsComponentEnabled<IsConnectedToEnergy>(targetEntity) 
                                        && !EntityManager.IsComponentEnabled<SwitchIsOff>(targetEntity);

                    bool finalLineStatus = selfHasPower && targetHasPower;

                    var nodefrom = en.nodes[c.Item1];
                    var tobuilding = EntityManager.GetComponentData<BuildingOnSceneReference>(targetEntity).buildingOnScene as EnergyBuildingOnScene;
                    var nodeto = tobuilding.nodes[c.Item2.x];

                    _energyFactory.UpdateConnect(nodefrom, nodeto, finalLineStatus);
                }
            }
            connect.ValueRW = false;
            Debug.Log(data.BuildingUniqueID+"               "+EntityManager.IsComponentEnabled<IsConnectedToEnergy>(entity));     
            _energyFactory.UpdateLuxBall(en.luxBall,EntityManager.IsComponentEnabled<IsConnectedToEnergy>(entity));
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