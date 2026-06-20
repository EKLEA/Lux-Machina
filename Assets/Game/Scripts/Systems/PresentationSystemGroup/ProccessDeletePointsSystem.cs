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
        foreach (var (buff,deleteData,entity) in SystemAPI.Query<DynamicBuffer<MapPoint>,DeleteManyPointsBuildingFromMap>().WithEntityAccess())
        {
            ProcessDeleteManyPointPoints(entity,mapData,buff,deleteData,ecb);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    
    void ProcessDeleteManyPointPoints(Entity command, BuildingMap mapData, DynamicBuffer<MapPoint> points, DeleteManyPointsBuildingFromMap deleteData,EntityCommandBuffer ecb)
    {
        NativeParallelMultiHashMap<Entity, MapPoint> entitiesToPoints = new(points.Length, Allocator.Temp);
        foreach (var p in points)
        {
            if (mapData.CellMapEntites.TryGetValue(p.pos, out Entity roadEntity))
            {
                if(mapData.CellMapBuildingsIDs[p.pos]!=deleteData.buildingID) continue;
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
            
            var demolitionSet = new NativeParallelHashSet<int3>(16, Allocator.Temp);
            var demolitionList = new NativeList<MapPoint>(16, Allocator.Temp);
            foreach (var p in entitiesToPoints.GetValuesForKey(road))
            {
                demolitionSet.Add(p.pos);
                demolitionList.Add(p);
            }

            Entity createDemolitonManyPointCommand = Entity.Null;
            if (!deleteData.isForce && demolitionList.Length > 0)
            {
                createDemolitonManyPointCommand = ecb.CreateEntity();
                 uint hash = math.hash(demolitionList[0].pos);
                 hash = math.hash(new int3((int)hash, demolitionList[demolitionList.Length - 1].pos.x, demolitionList[demolitionList.Length - 1].pos.y));
                ecb.AddComponent(createDemolitonManyPointCommand,new CreateManyPointEventTag{UniqueBuildingID=(int)hash ,buildingID=deleteData.buildingID});
                ecb.AddComponent<IsDemolition>(createDemolitonManyPointCommand);
                var buff = ecb.AddBuffer<MapPoint>(createDemolitonManyPointCommand);
                foreach (var p in demolitionList) buff.Add(p);
            }

            NativeList<MapPoint> restPoints = new(roadPoints.Length, Allocator.Temp);
            foreach (var p in roadPoints)
            {
                if (!demolitionSet.Contains(p.pos)) restPoints.Add(p);
            }

            Entity createRestManyPointsCommand = Entity.Null;
            if (restPoints.Length > 0)
            {
                createRestManyPointsCommand = ecb.CreateEntity();
                ecb.AddComponent(createRestManyPointsCommand,new ProcessManyPointPointsEventTag{buildingID=deleteData.buildingID});
                var buff = ecb.AddBuffer<MapPoint>(createRestManyPointsCommand);
                foreach (var p in restPoints) buff.Add(p);
            }

            if (EntityManager.HasBuffer<ManyPointPointHealthData>(road)) 
            {
                var oldData=EntityManager.GetBuffer<ManyPointPointHealthData>(road);
                // Подготавливаем буферы для новых сущностей
                DynamicBuffer<ManyPointPointHealthData> restExtraBuff = default;
                if (createRestManyPointsCommand != Entity.Null) 
                    restExtraBuff = ecb.AddBuffer<ManyPointPointHealthData>(createRestManyPointsCommand);

                DynamicBuffer<ManyPointPointHealthData> demoExtraBuff = default;
                if (createDemolitonManyPointCommand != Entity.Null) 
                    demoExtraBuff = ecb.AddBuffer<ManyPointPointHealthData>(createDemolitonManyPointCommand);

                foreach (var data in oldData)
                {
                    if (demolitionSet.Contains(data.pos))
                    {
                        if (demoExtraBuff.IsCreated) demoExtraBuff.Add(data);
                    }
                    else
                    {
                        if (restExtraBuff.IsCreated) restExtraBuff.Add(data);
                    }
                }
            }
            if (EntityManager.HasComponent<IsBlueprint>(road) && EntityManager.IsComponentEnabled<IsBlueprint>(road))
            {
                var items = EntityManager.GetBuffer<InputConstructionSlotData>(road);
                float procent = (float)demolitionList.Length / roadPoints.Length;

                DynamicBuffer<TransitionSlotData> buffItemRest = default;
                if (createRestManyPointsCommand != Entity.Null)
                {
                    ecb.AddComponent<IsBlueprint>(createRestManyPointsCommand);
                    buffItemRest = ecb.AddBuffer<TransitionSlotData>(createRestManyPointsCommand);
                }

                DynamicBuffer<TransitionSlotData> buffItemDemo = default;
                if (createDemolitonManyPointCommand != Entity.Null)
                {
                    ecb.AddComponent<IsBlueprint>(createDemolitonManyPointCommand);
                    buffItemDemo = ecb.AddBuffer<TransitionSlotData>(createDemolitonManyPointCommand);
                }

                for (int i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    int amountDemo = createDemolitonManyPointCommand != Entity.Null?(int)(item.Amount * procent):0;
                    int amountRest = item.Amount - amountDemo;
                    

                    if (createDemolitonManyPointCommand != Entity.Null)
                        buffItemDemo.Add(new TransitionSlotData { itemID = item.ItemId, amount = amountDemo });
                    
                    if (createRestManyPointsCommand != Entity.Null)
                        buffItemRest.Add(new TransitionSlotData { itemID = item.ItemId, amount = amountRest });
                }
            }

            // Финализация
            ecb.SetComponentEnabled<ForceDestroyTag>(road, true);

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