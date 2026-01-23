using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(MarkBuildingOnMapSystem))]
[BurstCompile]

public partial struct DestroyBuildingsSystem : ISystem
{
    EntityQuery _forceDemolitionBuildingQuery;
    EntityArchetype _destroyMapPointCommand;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BuildingMap>();
        state.RequireForUpdate<ClusterMap>();
        _destroyMapPointCommand=state.EntityManager.CreateArchetype(
            typeof(DeletePointFromMapTag),
            typeof(MapPoint));

        _forceDemolitionBuildingQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<IsDemolition,ForceDestroyTag,OutputConstructionSlotData>()
            .Build(ref state);
    }
    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        
        var buildingMap = SystemAPI.GetSingleton<BuildingMap>();
        
        if (!_forceDemolitionBuildingQuery.IsEmptyIgnoreFilter)
        {
            var deleteJob = new DestroyBuildingJob
            {
                MapData = buildingMap,
                DestroyMapPointCommandArchetype = _destroyMapPointCommand,
                ECB = ecb.AsParallelWriter() 
            };
            
            state.Dependency = deleteJob.ScheduleParallel(state.Dependency);
        }
    }
    [BurstCompile]
    [WithAll(typeof(IsDemolition), typeof(ForceDestroyTag))]
    public partial struct DestroyBuildingJob : IJobEntity
    {
        [ReadOnly] public BuildingMap MapData; 
        public EntityCommandBuffer.ParallelWriter ECB;
        public EntityArchetype DestroyMapPointCommandArchetype;

        public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex)
        {
            if (MapData.CellEntityMultiMap.TryGetFirstValue(entity, out int2 pos, out var it))
            {
                var comm = ECB.CreateEntity(chunkIndex, DestroyMapPointCommandArchetype);
                
                var buff = ECB.AddBuffer<MapPoint>(chunkIndex, comm);
                do 
                {
                    buff.Add(new MapPoint { pos = pos });
                } 
                while (MapData.CellEntityMultiMap.TryGetNextValue(out pos, ref it));
            }
        }
    }
}