
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines.ExtrusionShapes;
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]

[UpdateAfter(typeof(TickGeneratorSystem))]
[BurstCompile]

public partial struct DestroyBuildingsSystem : ISystem
{
    EntityQuery _destroyBuildingQuery;
    EntityQuery _destroyRoadQuery;
    public void OnCreate(ref SystemState state)
    {
        _destroyBuildingQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ForceDestroyTag,BuildingPosData>()
            .WithNone<RoadTypeBuildingTag>()
            .Build(ref state);
         _destroyRoadQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ForceDestroyTag,RoadTypeBuildingTag,MapPoint>()
            .Build(ref state);
        
    }
    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        
        var entitiesRW = SystemAPI.GetSingletonRW<EntitiesDictionary>();
        var buildingMapRW = SystemAPI.GetSingletonRW<BuildingMap>();
        Entity mapEntity = SystemAPI.GetSingletonEntity<BuildingMap>();
        if (!_destroyBuildingQuery.IsEmpty)
        {
            var deleteBJoB=new DestroyBuildingJob
            {
                MapData=buildingMapRW.ValueRW,
                Map=mapEntity,
                EntityDictionary=entitiesRW.ValueRW,
                ECB=ecb,
                RoadLookup=SystemAPI.GetComponentLookup<RoadTypeBuildingTag>(true)
            };
            state.Dependency=deleteBJoB.Schedule(state.Dependency);
        }
        if (!_destroyRoadQuery.IsEmpty)
        {
            var deleteRJoB=new DestroyRoadJob
            {
                MapData=buildingMapRW.ValueRW,
                Map=mapEntity,
                EntityDictionary=entitiesRW.ValueRW,
                ECB=ecb,
                RoadLookup=SystemAPI.GetComponentLookup<RoadTypeBuildingTag>(true)

            };
            state.Dependency=deleteRJoB.Schedule(state.Dependency);
        }
    }
    [BurstCompile]
    [WithAll(typeof(ForceDestroyTag))]
    [WithNone(typeof(RoadTypeBuildingTag))]
    public partial struct DestroyBuildingJob : IJobEntity
    {
        public BuildingMap MapData; 
        public EntitiesDictionary EntityDictionary; 
        public Entity Map;
        public EntityCommandBuffer ECB;
        [ReadOnly] public ComponentLookup<RoadTypeBuildingTag> RoadLookup;
                public void Execute(Entity entity, in BuildingData buildingData,in BuildingPosData posData)
        {
            if (MapData.CellEntityMultiMap.ContainsKey(entity))
            {
                for(int x = 0; x < posData.size.x;x++)
                {
                    for(int y = 0; y< posData.size.y;y++)
                    {
                        var pos=posData.LeftCornerPos+new int2(x,y);
                        MapData.CellMapEntites.Remove(pos);
                        MapData.CellMapBuildingsIDs.Remove(pos);
                        MapData.IsBluePrintOrDemolitionPoints.Remove(pos);
                    
                    }
                }
                
                MapData.CellEntityMultiMap.Remove(entity);
                EntityDictionary.Entities.Remove(buildingData.BuildingUniqueID);
                ECB.DestroyEntity(entity);
                ECB.SetComponentEnabled<UpdateClusterSlots>(Map,true);
                ECB.SetComponentEnabled<UpdateMapTag>(Map,true);

                NativeHashSet<Entity> roadsToUpdate=new(100,Allocator.Temp);
                 for(int x = posData.LeftCornerPos.x; x < posData.LeftCornerPos.x + posData.size.x; x++)
                {
                    CheckPoint(new int2(x,posData.LeftCornerPos.y-1),ref roadsToUpdate);
                    CheckPoint(new int2(x,posData.LeftCornerPos.y+posData.size.y),ref roadsToUpdate);
                }
                for(int y= posData.LeftCornerPos.y; y < posData.LeftCornerPos.y + posData.size.y; y++)
                {
                    CheckPoint(new int2(posData.LeftCornerPos.x-1,y),ref roadsToUpdate);
                    CheckPoint(new int2(posData.LeftCornerPos.x+posData.size.x,y),ref roadsToUpdate);
                }
                foreach(var road in roadsToUpdate)
                {
                    ECB.SetComponentEnabled<UpdateRoad>(road,true);
                }
                roadsToUpdate.Dispose();
            }
        }
        void CheckPoint(int2 pos, ref  NativeHashSet<Entity> roads)
        {
            if (MapData.CellMapEntites.ContainsKey(pos))
            {
                if(RoadLookup.HasComponent(MapData.CellMapEntites[pos])) roads.Add(MapData.CellMapEntites[pos]);
            }
        }
    }
    [BurstCompile]
    [WithAll(typeof(ForceDestroyTag),typeof(RoadTypeBuildingTag))]
    public partial struct DestroyRoadJob : IJobEntity
    {
        public BuildingMap MapData; 
        public EntitiesDictionary EntityDictionary; 
        public Entity Map;
        public EntityCommandBuffer ECB;
        Entity roadEn;
        [ReadOnly] public ComponentLookup<RoadTypeBuildingTag> RoadLookup;
        public void Execute(Entity entity, in BuildingData buildingData,in DynamicBuffer<MapPoint> points)
        {
            if (MapData.CellEntityMultiMap.ContainsKey(entity))
            {
                roadEn=entity;


                var dirs = new NativeArray<int2>(4, Allocator.Temp);
                dirs[0] = new int2(1, 0);
                dirs[1] = new int2(-1, 0);
                dirs[2] = new int2(0, -1);
                dirs[3] = new int2(0, 1);
                NativeHashSet<Entity> roadsToUpdate=new(100,Allocator.Temp);
                for (int i = 0; i < points.Length; i++)
                {
                    for (int d = 0; d < dirs.Length; d++)
                    {
                        CheckPoint(points[i].pos + dirs[d], ref roadsToUpdate);
                    }
                }
                
                foreach( var p in points)
                {
                    MapData.CellMapEntites.Remove(p.pos);
                    MapData.CellMapBuildingsIDs.Remove(p.pos);
                    MapData.IsBluePrintOrDemolitionPoints.Remove(p.pos);
                }
                MapData.CellEntityMultiMap.Remove(entity);
                
                EntityDictionary.Entities.Remove(buildingData.BuildingUniqueID);
                foreach(var road in roadsToUpdate)
                {
                    ECB.SetComponentEnabled<UpdateRoad>(road,true);
                }
                roadsToUpdate.Dispose();
                dirs.Dispose();
                
                ECB.DestroyEntity(entity);
                ECB.SetComponentEnabled<UpdateClusterSlots>(Map,true);
                ECB.SetComponentEnabled<UpdateMapTag>(Map,true);
                ECB.SetComponentEnabled<UpdateClustersTag>(Map,true);   
            }
        }
        void CheckPoint(int2 pos, ref  NativeHashSet<Entity> roads)
        {
            if (MapData.CellMapEntites.ContainsKey(pos))
            {
                if(RoadLookup.HasComponent(MapData.CellMapEntites[pos])&&MapData.CellMapEntites[pos]!=roadEn) roads.Add(MapData.CellMapEntites[pos]);
            }
        }
    }

}