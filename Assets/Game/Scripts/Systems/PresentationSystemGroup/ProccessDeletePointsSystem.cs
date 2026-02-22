using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Zenject;
[DisableAutoCreation]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(PlayerDeleteBuildingsSystem))]
public partial class ProccessDeletePointsSystem : SystemBase
{
    protected override void OnUpdate()
    {
         var ecb = new EntityCommandBuffer(Allocator.Temp);
        BuildingMap mapData= SystemAPI.GetSingleton<BuildingMap>();
        foreach (var (buff,deleteData,entity) in SystemAPI.Query<DynamicBuffer<MapPoint>,DeleteRoadPointsFromMap>().WithEntityAccess())
        {
            ProcessDeleteRoadPoints(entity,mapData,buff,deleteData.isForce,ecb);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    void ProcessDeleteRoadPoints(Entity command, BuildingMap mapData, DynamicBuffer<MapPoint> points, bool isForce, EntityCommandBuffer ecb)
    {
        NativeParallelMultiHashMap<Entity, MapPoint> entitiesToPoints = new(points.Length, Allocator.Temp);
        foreach (var p in points)
        {
            if (mapData.CellMapEntites.TryGetValue(p.pos, out Entity roadEntity))
            {
                if (isForce || !mapData.IsBluePrintOrDemolitionPoints.TryGetValue(p.pos, out var isProtected) || isProtected)
                    entitiesToPoints.Add(roadEntity, p);
            }
        }

        var allEntities = entitiesToPoints.GetKeyArray(Allocator.Temp);
        var uniqueEntities = new NativeParallelHashSet<Entity>(allEntities.Length, Allocator.Temp);
        for (int i = 0; i < allEntities.Length; i++) uniqueEntities.Add(allEntities[i]);

        foreach (Entity road in uniqueEntities)
        {
            if (!EntityManager.HasBuffer<MapPoint>(road)) continue;

            var roadPoints = EntityManager.GetBuffer<MapPoint>(road);
            
            var demolitionSet = new NativeParallelHashSet<int2>(16, Allocator.Temp);
            var demolitionList = new NativeList<MapPoint>(16, Allocator.Temp);
            foreach (var p in entitiesToPoints.GetValuesForKey(road))
            {
                demolitionSet.Add(p.pos);
                demolitionList.Add(p);
            }

            Entity createDemolitonRoadCommand = Entity.Null;
            if (!isForce && demolitionList.Length > 0)
            {
                createDemolitonRoadCommand = ecb.CreateEntity();
                 uint hash = math.hash(demolitionList[0].pos);
                 hash = math.hash(new int3((int)hash, demolitionList[demolitionList.Length - 1].pos.x, demolitionList[demolitionList.Length - 1].pos.y));
                ecb.AddComponent(createDemolitonRoadCommand,new CreateRoadEventTag{UniqueBuildingID=(int)hash });
                ecb.AddComponent<IsDemolition>(createDemolitonRoadCommand);
                var buff = ecb.AddBuffer<MapPoint>(createDemolitonRoadCommand);
                foreach (var p in demolitionList) buff.Add(p);
            }

            NativeList<MapPoint> restPoints = new(roadPoints.Length, Allocator.Temp);
            foreach (var p in roadPoints)
            {
                if (!demolitionSet.Contains(p.pos)) restPoints.Add(p);
            }

            Entity createRestRoadsCommand = Entity.Null;
            if (restPoints.Length > 0)
            {
                createRestRoadsCommand = ecb.CreateEntity();
                ecb.AddComponent<ProcessRoadPointsEventTag>(createRestRoadsCommand);
                var buff = ecb.AddBuffer<MapPoint>(createRestRoadsCommand);
                foreach (var p in restPoints) buff.Add(p);
            }

            if (EntityManager.HasComponent<IsBlueprint>(road) && EntityManager.IsComponentEnabled<IsBlueprint>(road))
            {
                var items = EntityManager.GetBuffer<InputConstructionSlotData>(road);
                float procent = (float)demolitionList.Length / roadPoints.Length;

                DynamicBuffer<TransitionSlotData> buffItemRest = default;
                if (createRestRoadsCommand != Entity.Null)
                {
                    ecb.AddComponent<IsBlueprint>(createRestRoadsCommand);
                    buffItemRest = ecb.AddBuffer<TransitionSlotData>(createRestRoadsCommand);
                }

                DynamicBuffer<TransitionSlotData> buffItemDemo = default;
                if (createDemolitonRoadCommand != Entity.Null)
                {
                    ecb.AddComponent<IsBlueprint>(createDemolitonRoadCommand);
                    buffItemDemo = ecb.AddBuffer<TransitionSlotData>(createDemolitonRoadCommand);
                }

                for (int i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    int amountDemo = createDemolitonRoadCommand != Entity.Null?(int)(item.Amount * procent):0;
                    int amountRest = item.Amount - amountDemo;
                    

                    if (createDemolitonRoadCommand != Entity.Null)
                        buffItemDemo.Add(new TransitionSlotData { itemID = item.ItemId, amount = amountDemo });
                    
                    if (createRestRoadsCommand != Entity.Null)
                        buffItemRest.Add(new TransitionSlotData { itemID = item.ItemId, amount = amountRest });
                }
            }

            // Финализация
            ecb.SetComponentEnabled<ForceDestroyTag>(road, true);
            ecb.SetComponentEnabled<DestroyVisualTag>(road, true);

            demolitionSet.Dispose();
            demolitionList.Dispose();
            restPoints.Dispose();
        }

        allEntities.Dispose();
        uniqueEntities.Dispose();
        entitiesToPoints.Dispose();
        ecb.DestroyEntity(command);
    }

}