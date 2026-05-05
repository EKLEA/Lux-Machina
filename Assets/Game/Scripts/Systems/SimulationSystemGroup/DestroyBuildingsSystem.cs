
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

[UpdateAfter(typeof(TerrainSystem))]
[BurstCompile]

public partial struct DestroyBuildingsSystem : ISystem
{
    EntityQuery _destroyBuildingQuery;
    EntityQuery _checkForDestroyBuildingQuery;
    EntityQuery _destroyManyPointQuery;
    public void OnCreate(ref SystemState state)
    {
        _destroyBuildingQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ForceDestroyTag,BuildingPosData>()
            .WithNone<ManyPointTypeBuildingTag>()
            .Build(ref state);
         _destroyManyPointQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ForceDestroyTag,ManyPointTypeBuildingTag,MapPoint>()
            .Build(ref state);
        _checkForDestroyBuildingQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<CheckForDestroy>()
            .WithNone<ForceDestroyTag>()
            .Build(ref state);
        
    }
    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        
        var entitiesRW = SystemAPI.GetSingletonRW<EntitiesDictionary>();
        var buildingMapRW = SystemAPI.GetSingletonRW<BuildingMap>();
        var turretMapRW = SystemAPI.GetSingletonRW<TurretGrid>();
        Entity mapEntity = SystemAPI.GetSingletonEntity<BuildingMap>();
        if (!_checkForDestroyBuildingQuery.IsEmpty)
        {
            state.Dependency=new CheckForDestoryJob{ECB=ecb}.Schedule(state.Dependency);
        }
        if (!_destroyBuildingQuery.IsEmpty)
        {
            var deleteBJoB=new DestroyBuildingJob
            {
                MapData=buildingMapRW.ValueRW,
                Map=mapEntity,
                turretGrid=turretMapRW.ValueRW,
                EntityDictionary=entitiesRW.ValueRW,
                ECB=ecb,
                ManyPointLookup=SystemAPI.GetComponentLookup<ManyPointTypeBuildingTag>(true),
                CoreBuildingTagLookup=SystemAPI.GetComponentLookup<CoreBuildingTag>(true),
                TurretStatsLookup=SystemAPI.GetComponentLookup<TurretStats>(true)
            };
            state.Dependency=deleteBJoB.Schedule(state.Dependency);
        }
        if (!_destroyManyPointQuery.IsEmpty)
        {
            var deleteRJoB=new DestroyManyPointJob
            {
                MapData=buildingMapRW.ValueRW,
                Map=mapEntity,
                EntityDictionary=entitiesRW.ValueRW,
                ECB=ecb,
                ManyPointLookup=SystemAPI.GetComponentLookup<ManyPointTypeBuildingTag>(true)

            };
            state.Dependency=deleteRJoB.Schedule(state.Dependency);
        }
    }
    [BurstCompile]
    [WithAll(typeof( CheckForDestroy))]
    [WithNone(typeof(ForceDestroyTag))]
    public partial struct CheckForDestoryJob : IJobEntity
    {
        public EntityCommandBuffer ECB;
        public void Execute(Entity entity,in BuildingTag tag)
        {
            ECB.SetComponentEnabled<CheckForDestroy>(entity,false);
            ECB.SetComponentEnabled<ForceDestroyTag>(entity,true);
        }
    }
    [BurstCompile]
    [WithAll(typeof(ForceDestroyTag))]
    [WithNone(typeof(ManyPointTypeBuildingTag),typeof(CheckForDestroy))]
    public partial struct DestroyBuildingJob : IJobEntity
    {
        public BuildingMap MapData; 
        public TurretGrid turretGrid;
        public EntitiesDictionary EntityDictionary; 
        public Entity Map;
        public EntityCommandBuffer ECB;
        [ReadOnly] public ComponentLookup<ManyPointTypeBuildingTag> ManyPointLookup;
        [ReadOnly] public ComponentLookup<CoreBuildingTag> CoreBuildingTagLookup;
        [ReadOnly] public ComponentLookup<TurretStats> TurretStatsLookup;
        public void Execute(Entity entity, in BuildingData buildingData,in BuildingPosData posData)
        {
            if (MapData.CellEntityMultiMap.ContainsKey(entity))
            {
                for(int x = 0; x < posData.size.x;x++)
                {
                    for(int y = 0; y< posData.size.y;y++)
                    {
                        for(int z = 0; z< posData.size.z;z++)
                        {
                            var pos=posData.LeftCornerPos+new int3(x,y,z);
                            MapData.CellMapEntites.Remove(pos);
                            MapData.CellMapBuildingsIDs.Remove(pos);
                            MapData.IsBluePrintOrDemolitionPoints.Remove(pos);
                        
                        }
                    
                    }
                }
                if (TurretStatsLookup.HasComponent (entity))
                {
                    turretGrid.EnemyGridMap.Remove(buildingData.BuildingUniqueID);

                    var enemyData = turretGrid.EnemyToTurret.GetKeyValueArrays(Allocator.Temp);
                    for (int i = 0; i < enemyData.Values.Length; i++)
                    {
                        if (enemyData.Values[i] == buildingData.BuildingUniqueID)
                        {
                            turretGrid.EnemyToTurret.Remove(enemyData.Keys[i], buildingData.BuildingUniqueID);
                        }
                    }
                    enemyData.Dispose();

                    var cellData = turretGrid.TurretGridClaim.GetKeyValueArrays(Allocator.Temp);
                    for (int i = 0; i < cellData.Values.Length; i++)
                    {
                        if (cellData.Values[i] == buildingData.BuildingUniqueID)
                        {
                            turretGrid.TurretGridClaim.Remove(cellData.Keys[i], buildingData.BuildingUniqueID);
                        }
                    }
                    cellData.Dispose();
                }
                MapData.CellEntityMultiMap.Remove(entity);
                EntityDictionary.Entities.Remove(buildingData.BuildingUniqueID);
                if (CoreBuildingTagLookup.HasComponent(entity))
                {
                    ECB.SetComponentEnabled<IsPause>(Map,true);
                    ECB.SetComponentEnabled<IsGameOver>(Map,true);
                    ECB.SetComponentEnabled<SavingMapTag>(Map,true);
                }
                ECB.DestroyEntity(entity);
                ECB.SetComponentEnabled<UpdateClusterSlots>(Map,true);
                ECB.SetComponentEnabled<UpdateMapTag>(Map,true);

                NativeHashSet<Entity> roadsToUpdate=new(100,Allocator.Temp);
                 for(int x = posData.LeftCornerPos.x; x < posData.LeftCornerPos.x + posData.size.x; x++)
                {
                    CheckPoint(new int3(x,posData.LeftCornerPos.y,posData.LeftCornerPos.z-1),ref roadsToUpdate);
                    CheckPoint(new int3(x,posData.LeftCornerPos.y,posData.LeftCornerPos.z+posData.size.y),ref roadsToUpdate);
                }
                for(int z= posData.LeftCornerPos.z; z < posData.LeftCornerPos.z + posData.size.z; z++)
                {
                    CheckPoint(new int3(posData.LeftCornerPos.x-1,posData.LeftCornerPos.y,z),ref roadsToUpdate);
                    CheckPoint(new int3(posData.LeftCornerPos.x+posData.size.x,posData.LeftCornerPos.y,z),ref roadsToUpdate);
                }
                foreach(var road in roadsToUpdate)
                {
                    ECB.SetComponentEnabled<UpdateManyPoint>(road,true);
                }
                roadsToUpdate.Dispose();
            }
        }
        void CheckPoint(int3 pos, ref  NativeHashSet<Entity> roads)
        {
            if (MapData.CellMapEntites.ContainsKey(pos))
            {
                if(ManyPointLookup.HasComponent(MapData.CellMapEntites[pos])) roads.Add(MapData.CellMapEntites[pos]);
            }
        }
    }
    [BurstCompile]
    [WithAll(typeof(ForceDestroyTag),typeof(ManyPointTypeBuildingTag))]
    public partial struct DestroyManyPointJob : IJobEntity
    {
        public BuildingMap MapData; 
        public EntitiesDictionary EntityDictionary; 
        public Entity Map;
        public EntityCommandBuffer ECB;
        Entity roadEn;
        [ReadOnly] public ComponentLookup<ManyPointTypeBuildingTag> ManyPointLookup;
        public void Execute(Entity entity, in BuildingData buildingData,in DynamicBuffer<MapPoint> points)
        {
            if (MapData.CellEntityMultiMap.ContainsKey(entity))
            {
                roadEn=entity;


                var dirs = new NativeArray<int3>(4, Allocator.Temp);
                dirs[0] = new int3(1,0, 0);
                dirs[1] = new int3(-1,0, 0);
                dirs[2] = new int3(0, 0,-1);
                dirs[3] = new int3(0, 0,1);
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
                    ECB.SetComponentEnabled<UpdateManyPoint>(road,true);
                }
                roadsToUpdate.Dispose();
                dirs.Dispose();
                
                ECB.DestroyEntity(entity);
                ECB.SetComponentEnabled<UpdateClusterSlots>(Map,true);
                ECB.SetComponentEnabled<UpdateMapTag>(Map,true);
                ECB.SetComponentEnabled<UpdateClustersTag>(Map,true);   
            }
        }
        void CheckPoint(int3 pos, ref  NativeHashSet<Entity> roads)
        {
            if (MapData.CellMapEntites.ContainsKey(pos))
            {
                if(ManyPointLookup.HasComponent(MapData.CellMapEntites[pos])&&MapData.CellMapEntites[pos]!=roadEn) roads.Add(MapData.CellMapEntites[pos]);
            }
        }
    }

}