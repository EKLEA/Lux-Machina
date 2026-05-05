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

       foreach (var (turretData, reference) in SystemAPI.Query<RefRW<TurretTranform>, BuildingOnSceneReference>())
        { 
            if (!(reference.buildingOnScene is TurretOnScene view)) continue;
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
            turretData.ValueRW.projectTyleSpawn=view.TurretSpawn[0].position;
        }
                foreach (var (reference,entity) in SystemAPI.Query<BuildingOnSceneReference>().WithAll<ChangeBluePrintState>().WithDisabled<ForceDestroyTag>().WithEntityAccess())
        {
            ChangeBluePrintState(reference,entity,ecb);
            ecb.SetComponentEnabled<UpdateClusterSlots>(mapEntity,true);
            ecb.SetComponentEnabled<UpdateClustersTag>(mapEntity,true);
            ecb.SetComponentEnabled<UpdateConnectionsTag>(mapEntity,true);
            if(EntityManager.HasComponent<NeedsClusterAssign>(entity)) 
                ecb.SetComponentEnabled<NeedsClusterAssign>(entity,true);
            if(EntityManager.HasComponent<UpdateConnectStatus>(entity)) 
                ecb.SetComponentEnabled<UpdateConnectStatus>(entity,true);
        }
        foreach (var (reference,entity) in SystemAPI.Query<BuildingOnSceneReference>().WithAll<ChangeDemolitionStateTag>().WithDisabled<ForceDestroyTag>().WithEntityAccess())
        {
            ChangeDemolitionState(reference,entity,ecb);
            ecb.SetComponentEnabled<UpdateClusterSlots>(mapEntity,true);
            ecb.SetComponentEnabled<UpdateClustersTag>(mapEntity,true);
            ecb.SetComponentEnabled<UpdateConnectionsTag>(mapEntity,true);
            if(EntityManager.HasComponent<NeedsClusterAssign>(entity)) 
                ecb.SetComponentEnabled<NeedsClusterAssign>(entity,true);
            if(EntityManager.HasComponent<UpdateConnectStatus>(entity)) 
                ecb.SetComponentEnabled<UpdateConnectStatus>(entity,true);
        }
        foreach (var (buff,reference) in SystemAPI.Query<DynamicBuffer<InputConstructionSlotData>,BuildingOnSceneReference>().WithAll<IsBlueprint>().WithDisabled<ChangeBluePrintState,ForceDestroyTag>())
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
        foreach (var (buff,reference) in SystemAPI.Query<DynamicBuffer<OutputConstructionSlotData>,BuildingOnSceneReference>().WithAll<IsDemolition>().WithDisabled<ChangeDemolitionStateTag,ForceDestroyTag>())
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
       
        foreach(var (energyData, reference, connect, entity) in SystemAPI.Query<EnergyBuildingData, BuildingOnSceneReference, EnabledRefRW<UpdateConnectStatus>>().WithDisabled<MarkOnMap,ForceDestroyTag>().WithEntityAccess())
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
            if (EntityManager.HasComponent<BuildingPosData>(building))
            {
                var buildingPosData= EntityManager.GetComponentData<BuildingPosData>(building);
                for(int x = 0; x < buildingPosData.size.x;x++)
                {
                    for(int y = 0; y< buildingPosData.size.y;y++)
                    {
                        for(int z= 0; z< buildingPosData.size.z;z++)
                        {
                            MapData.IsBluePrintOrDemolitionPoints.Remove(buildingPosData.LeftCornerPos+new int3(x,y,z));
                        }
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
            
            ecb.SetComponent(building,new BuildingStateData{State=(int)(WorkStateEnum.Work)});
        }
        else
        {
            _visualBuildingFactory.PhantomizeObject(gameobject.buildingOnScene.gameObject);       
            ecb.SetComponentEnabled<IsBlueprint>(building,true);
            if (EntityManager.HasComponent<BuildingPosData>(building))
            {
                var buildingPosData= EntityManager.GetComponentData<BuildingPosData>(building);
                for(int x = 0; x < buildingPosData.size.x;x++)
                {
                    for(int y = 0; y< buildingPosData.size.y;y++)
                    {
                        for(int z = 0; y< buildingPosData.size.z;z++)
                        {
                            var pos =buildingPosData.LeftCornerPos+new int3(x,y,z);
                            if(MapData.IsBluePrintOrDemolitionPoints.ContainsKey(pos))
                                    MapData.IsBluePrintOrDemolitionPoints[pos]=true;
                            else
                                    MapData.IsBluePrintOrDemolitionPoints.Add(pos,true);
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
                        MapData.IsBluePrintOrDemolitionPoints[p.pos]=true;
                    else
                            MapData.IsBluePrintOrDemolitionPoints.Add(p.pos,true);
                }
            }
            
            ecb.SetComponent(building,new BuildingStateData{State=(int)(WorkStateEnum.Phantom)});
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
            if (EntityManager.HasComponent<BuildingPosData>(building))
            {
                var buildingPosData= EntityManager.GetComponentData<BuildingPosData>(building);
                for(int x = 0; x < buildingPosData.size.x;x++)
                {
                    for(int y = 0; y< buildingPosData.size.y;y++)
                    {
                        for(int z= 0; z< buildingPosData.size.z;z++)
                        {
                            MapData.IsBluePrintOrDemolitionPoints.Remove(buildingPosData.LeftCornerPos+new int3(x,y,z));
                        }
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
            
            ecb.SetComponent(building,new BuildingStateData{State=(int)(WorkStateEnum.Work)});
        }
        else
        {
            _visualBuildingFactory.DemolitionObject(gameobject.buildingOnScene.gameObject,true);       
            ecb.SetComponentEnabled<IsDemolition>(building,true);
            if (EntityManager.HasComponent<BuildingPosData>(building))
            {
                var buildingPosData= EntityManager.GetComponentData<BuildingPosData>(building);
                for(int x = 0; x < buildingPosData.size.x;x++)
                {
                    for(int y = 0; y< buildingPosData.size.y;y++)
                    {
                        
                        for(int z= 0; z< buildingPosData.size.z;z++)
                        {
                            
                            var pos =buildingPosData.LeftCornerPos+new int3(x,y,z);
                            if(MapData.IsBluePrintOrDemolitionPoints.ContainsKey(pos))
                                
                                MapData.IsBluePrintOrDemolitionPoints[pos]=false;
                            else
                            {
                                MapData.IsBluePrintOrDemolitionPoints.Add(pos,false);
                            }
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
            
            ecb.SetComponent(building,new BuildingStateData{State=(int)(WorkStateEnum.Demolition)});
        }
        ecb.SetComponentEnabled<ChangeDemolitionStateTag>(building,false);
    }
}