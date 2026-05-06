using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(CraftSystem))]
[BurstCompile]

public partial struct CraftApplySystem : ISystem
{
    EntityQuery _deleteBuildingsQuery;
    EntityQuery _realizeBuildingsQuery;
    EntityQuery _clearChunkQuery;
    
    EntityQuery _IsPause;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BuildingMap>();
        _deleteBuildingsQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<IsDemolition>()
            .WithDisabled<ChangeDemolitionStateTag,ForceDestroyTag>()
            .Build(ref state);
        _realizeBuildingsQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<IsBlueprint>()
            .WithDisabled<ChangeBluePrintState,ForceDestroyTag,IsDemolition>()
            .Build(ref state);

        _clearChunkQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ChunkData,ResourceElement,BlockElement,NeedsCleanupTag>()
            .Build(ref state);

         _IsPause= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<IsPause,BuildingMap>()
            .Build(ref state);
    }
    public void OnUpdate(ref SystemState state)
    {
        
        if(!_IsPause.IsEmpty) return;
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        
        var mapEntity= SystemAPI.GetSingletonEntity<ClusterMap>();
        var worldSettings=SystemAPI.GetSingletonRW<WorldSettings>();
       if (!_realizeBuildingsQuery.IsEmpty)
        {
            var bJob=new RealizeBluePrintBuildingJob{ECB=ecb,mapEntity=mapEntity};
            state.Dependency=bJob.Schedule( state.Dependency);
        }
        if (!_deleteBuildingsQuery.IsEmpty)
        {
            var dJob=new DestroyBuildingJob{ECB=ecb,mapEntity=mapEntity};
            state.Dependency=dJob.Schedule( state.Dependency);
        }
        if (!_clearChunkQuery.IsEmpty)
        {
            var cJob=new ChunkCleanupJob{ECB=ecb.AsParallelWriter(),Settings=worldSettings.ValueRO};
            state.Dependency=cJob.ScheduleParallel( state.Dependency);
        }
    }
  




    [BurstCompile]
    [WithAll(typeof(NeedsCleanupTag))]
    public partial struct ChunkCleanupJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB;
        [ReadOnly] public WorldSettings Settings;

        public void Execute(Entity chunkEntity, [ChunkIndexInQuery] int chunkIndex, DynamicBuffer<ResourceElement> resources, DynamicBuffer<BlockElement> blocks )
        {

            var aliveResources = new NativeList<ResourceElement>(resources.Length, Allocator.Temp);

            for (int i = 0; i < resources.Length; i++)
            {
                if (resources[i].Amount > 0)
                {
                    aliveResources.Add(resources[i]);
                }
                else
                {
                    int3 pos = resources[i].LocalPos;
                    
                    int blockIdx = pos.x + (pos.z * Settings.Size) + (pos.y * (Settings.Size * Settings.Size)); 
                    
                    if (blockIdx >= 0 && blockIdx < blocks.Length)
                    {
                        var block = blocks[blockIdx];
                        block.BlockID = 0; 
                        blocks[blockIdx] = block;
                    }
                }
            }

            resources.Clear();
            if (aliveResources.Length > 0)
            {
                resources.AddRange(aliveResources.AsArray());
            }

            ECB.AddComponent<UpdateVisualTag>(chunkIndex, chunkEntity);
            ECB.RemoveComponent<NeedsCleanupTag>(chunkIndex, chunkEntity);
            
            aliveResources.Dispose();
        }
    }



    [WithAll(typeof(IsDemolition))]
    [WithDisabled(typeof(ForceDestroyTag),typeof(ChangeDemolitionStateTag))]
    public partial struct DestroyBuildingJob : IJobEntity
    {
        
        public Entity mapEntity;
        public EntityCommandBuffer ECB; 
        public void Execute(Entity entity,in DynamicBuffer<OutputConstructionSlotData> output)
        {
            bool shouldDelete=true;
            foreach(var s in output)
            {
                if (s.Amount != 0)
                {
                    shouldDelete=false;
                    break;  
                }
            }
            if (shouldDelete)
            {
                
                ECB.SetBuffer<OutputConstructionSlotData>(entity);
                ECB.SetComponentEnabled<UpdateClusterSlots>(mapEntity,true);
                ECB.SetComponentEnabled<ForceDestroyTag>(entity,true);
                
            }
        }
    }
    
    [WithAll(typeof(IsBlueprint))]
    [WithDisabled(typeof(ForceDestroyTag),typeof(ChangeBluePrintState),typeof(IsDemolition))]
    public partial struct RealizeBluePrintBuildingJob : IJobEntity
    {
        
        public Entity mapEntity;
        public EntityCommandBuffer ECB; 
        public void Execute(Entity entity,in DynamicBuffer<InputConstructionSlotData> input)
        {
            bool shouldRealize=true;
            
            foreach(var s in input)
            {
                if(s.Amount!=s.Capacity)
                {
                    shouldRealize=false;
                    break;
                }
            }
            if (shouldRealize)
            {
                ECB.SetBuffer<InputConstructionSlotData>(entity);
                ECB.SetComponentEnabled<ChangeBluePrintState>(entity,true);
                ECB.SetComponentEnabled<UpdateClusterSlots>(mapEntity,true);
            }
        }
    }
}