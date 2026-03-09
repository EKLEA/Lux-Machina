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
    }
    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        
        var mapEntity= SystemAPI.GetSingletonEntity<ClusterMap>();
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