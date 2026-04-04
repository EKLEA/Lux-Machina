using System;
using System.Collections.Generic;
using ModestTree;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BuildingConfigManagerSystem))]
[DisableAutoCreation]
public partial struct ItemDistributionSystem : ISystem
{
    EntityQuery MapUpdate;
    
    EntityQuery _deleteBuildingsQuery;
    EntityQuery _realizeBuildingsQuery;
    
    EntityQuery _IsPause;
    public void OnCreate(ref SystemState state)
    {
        MapUpdate=new EntityQueryBuilder(Allocator.Temp).WithAll<ClusterMap,UpdateClusterSlots>().Build(ref state);
        state.RequireForUpdate<BuildingMap>();
        _deleteBuildingsQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<IsDemolition>()
            .WithDisabled<ChangeDemolitionStateTag,ForceDestroyTag>()
            .Build(ref state);
        _realizeBuildingsQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<IsBlueprint>()
            .WithDisabled<ChangeBluePrintState,ForceDestroyTag,IsDemolition>()
            .Build(ref state);
         _IsPause= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<IsPause,BuildingMap>()
            .Build(ref state);
    }
    public void OnUpdate(ref SystemState state)
    {
        
        if(!_IsPause.IsEmpty) return;
        var clusterMap= SystemAPI.GetSingletonRW<ClusterMap>();
        var mapEntity= SystemAPI.GetSingletonEntity<ClusterMap>();
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        ComponentLookup<CraftingPriorityData> CraftPriority=SystemAPI.GetComponentLookup<CraftingPriorityData>(false);
        BufferLookup<InputSlotData> InputCraftSlotDataLookup=SystemAPI.GetBufferLookup<InputSlotData>(false);
        ComponentLookup<IsInputCraftEnabled> IsInputCraftEnabled=SystemAPI.GetComponentLookup<IsInputCraftEnabled>(false);
        
        BufferLookup<OutputSlotData> OutputCraftSlotsDataLookup=SystemAPI.GetBufferLookup<OutputSlotData>(false);;
        ComponentLookup<IsOutputCraftEnabled> IsOutputCraftEnabled=SystemAPI.GetComponentLookup<IsOutputCraftEnabled>(false);


        ComponentLookup<ConstructionPriorityData> ConstructionPriority=SystemAPI.GetComponentLookup<ConstructionPriorityData>(false);
        BufferLookup<InputConstructionSlotData> InputConstructionSlotDataLookup=SystemAPI.GetBufferLookup<InputConstructionSlotData>(false);;
        ComponentLookup<IsInputConstructionEnabled> IsInputConstructionEnabled=SystemAPI.GetComponentLookup<IsInputConstructionEnabled>(false);
    
        BufferLookup<OutputConstructionSlotData> OutputConstructionSlotsDataLookup=SystemAPI.GetBufferLookup<OutputConstructionSlotData>(false);;
        ComponentLookup<IsOutputConstuctionEnabled> IsOutputConstructionEnabled=SystemAPI.GetComponentLookup<IsOutputConstuctionEnabled>(false);

        BufferLookup<ExcessSlotData> ExcesSlotsDataLookup=SystemAPI.GetBufferLookup<ExcessSlotData>(false);;
        BufferLookup<StorageSlotData> StorageSlotsDataLookup=SystemAPI.GetBufferLookup<StorageSlotData>(false);;

        ComponentLookup<IsBlueprint> IsBlueprintLookup=SystemAPI.GetComponentLookup<IsBlueprint>(false);
        ComponentLookup<IsDemolition> IsDemolitionLookup=SystemAPI.GetComponentLookup<IsDemolition>(false);

        if (!MapUpdate.IsEmpty)
        {
            CraftPriority.Update(ref state);
            InputCraftSlotDataLookup.Update(ref state);
            IsInputCraftEnabled.Update(ref state);
            OutputCraftSlotsDataLookup.Update(ref state);
            IsOutputCraftEnabled.Update(ref state);
            ConstructionPriority.Update(ref state);
            InputConstructionSlotDataLookup.Update(ref state);
            IsInputConstructionEnabled.Update(ref state);
            OutputConstructionSlotsDataLookup.Update(ref state);
            IsOutputConstructionEnabled.Update(ref state);
            ExcesSlotsDataLookup.Update(ref state);
            StorageSlotsDataLookup.Update(ref state);
            IsBlueprintLookup.Update(ref state);
            IsDemolitionLookup.Update(ref state);
            
            var clusterMapData = clusterMap.ValueRW;
            var clearJob = new ClearClusterMapJob
            {
                ClusterMap = clusterMapData
            };
            state.Dependency = clearJob.Schedule(state.Dependency);
            var collectJob = new CollectClusterSlots
            {
                AllProducersWriter = clusterMap.ValueRW.AllProducersList.AsParallelWriter(),
                SlotToClustersWriter = clusterMap.ValueRW.SlotToClusters.AsParallelWriter(),
                ProducersWriter = clusterMap.ValueRW.ClusterToProducers.AsParallelWriter(),
                ConsumersWriter = clusterMap.ValueRW.ClusterToConsumers.AsParallelWriter(),
                InputSlotsWriter = clusterMap.ValueRW.EntityInputSlots.AsParallelWriter(),
                OutputSlotsWriter = clusterMap.ValueRW.EntityOutputSlots.AsParallelWriter(),
                CraftPriority=CraftPriority,
                InputCraftSlotDataLookup=InputCraftSlotDataLookup,
                IsInputCraftEnabled=IsInputCraftEnabled,
                OutputCraftSlotsDataLookup=OutputCraftSlotsDataLookup,
                IsOutputCraftEnabled=IsOutputCraftEnabled,
                ConstructionPriority=ConstructionPriority,
                InputConstructionSlotDataLookup=InputConstructionSlotDataLookup,
                IsInputConstructionEnabled=IsInputConstructionEnabled,
                OutputConstructionSlotsDataLookup=OutputConstructionSlotsDataLookup,
                IsOutputConstructionEnabled=IsOutputConstructionEnabled,
                ExcesSlotsDataLookup=ExcesSlotsDataLookup,
                StorageSlotsDataLookup=StorageSlotsDataLookup,
                IsBlueprintLookup=IsBlueprintLookup,
                IsDemolitionLookup=IsDemolitionLookup

            };
            state.Dependency = collectJob.ScheduleParallel(state.Dependency);
            var linkJob = new LinkSlotsCrossClusterJob
            {
                AllProducers = clusterMap.ValueRO.AllProducersList.AsDeferredJobArray(),
                ClusterToConsumers = clusterMap.ValueRO.ClusterToConsumers,
                SlotToClusters = clusterMap.ValueRO.SlotToClusters, 

                SlotGraph = clusterMap.ValueRW.SlotGraph.AsParallelWriter(),
                ReverseSlotGraph = clusterMap.ValueRW.ReverseSlotGraph.AsParallelWriter()
            };

            state.Dependency= linkJob.Schedule(clusterMap.ValueRO.AllProducersList, 1, state.Dependency);
        }
        var query = SystemAPI.QueryBuilder().WithAll<IsTickFrame>().Build();
        if (query.IsEmpty) return;
        else
        {
            if (!SystemAPI.HasSingleton<ClusterMap>())
                return;

             CraftPriority.Update(ref state);
            InputCraftSlotDataLookup.Update(ref state);
            IsInputCraftEnabled.Update(ref state);
            OutputCraftSlotsDataLookup.Update(ref state);
            IsOutputCraftEnabled.Update(ref state);
            ConstructionPriority.Update(ref state);
            InputConstructionSlotDataLookup.Update(ref state);
            IsInputConstructionEnabled.Update(ref state);
            OutputConstructionSlotsDataLookup.Update(ref state);
            IsOutputConstructionEnabled.Update(ref state);
            ExcesSlotsDataLookup.Update(ref state);
            StorageSlotsDataLookup.Update(ref state);
            IsBlueprintLookup.Update(ref state);


            var transactions = new NativeStream(1024, Allocator.TempJob);
            var distJob = new LogisticsDistributionJob
            {
                
                 AllProducers = clusterMap.ValueRO.AllProducersList.AsDeferredJobArray(),
                SlotGraph = clusterMap.ValueRO.SlotGraph,
                Transactions = transactions.AsWriter(),
                 MaxStreamPockets = 1024,
                InputLookup = InputCraftSlotDataLookup,
                OutputLookup = OutputCraftSlotsDataLookup,
                InputConstructionLookup = InputConstructionSlotDataLookup,
                OutputConstructionLookup = OutputConstructionSlotsDataLookup,
                StorageLookup = StorageSlotsDataLookup,
                ExcessLookup = ExcesSlotsDataLookup
            };
            state.Dependency = distJob.Schedule(clusterMap.ValueRO.AllProducersList, 64,  state.Dependency);

            var applyJob = new ApplyLogisticsJob
            {
                Transactions = transactions.AsReader(),
                
                InputLookup = InputCraftSlotDataLookup,
                OutputLookup = OutputCraftSlotsDataLookup,
                InputConstructionLookup = InputConstructionSlotDataLookup,
                OutputConstructionLookup = OutputConstructionSlotsDataLookup,
                StorageLookup = StorageSlotsDataLookup,
                ExcessLookup = ExcesSlotsDataLookup
            };
            state.Dependency=applyJob.Schedule( state.Dependency);
            
            
            state.Dependency= new ExcessCleanupJob{ECB=ecb.AsParallelWriter(),mapEntity=mapEntity}.ScheduleParallel(state.Dependency);
            
            transactions.Dispose(state.Dependency);
        }
    }

   [BurstCompile]
    public partial struct ExcessCleanupJob : IJobEntity
    {
        public Entity mapEntity;
        public EntityCommandBuffer.ParallelWriter ECB; 
        public void Execute(ref DynamicBuffer<ExcessSlotData> excessBuffer,[EntityIndexInQuery] int sortKey)
        {
            bool shouldUpdate=false;
            for (int i = excessBuffer.Length - 1; i >= 0; i--)
            {
                if (excessBuffer[i].Amount <= 0)
                {
                    excessBuffer.RemoveAt(i);
                    shouldUpdate=true;
                }
            }
            if (shouldUpdate)
            {
                
                ECB.SetComponentEnabled<UpdateClusterSlots>(sortKey,mapEntity,true);
            }
        }
    }
    
    [BurstCompile]
    partial struct ClearClusterMapJob : IJobEntity
    {
        public ClusterMap ClusterMap;
        public void Execute(EnabledRefRW<UpdateClusterSlots> enabledRefRW)
        {
            ClusterMap.ClusterToProducers.Clear();
            ClusterMap.ClusterToConsumers.Clear();
            ClusterMap.EntityInputSlots.Clear();
            ClusterMap.EntityOutputSlots.Clear();
            ClusterMap.SlotGraph.Clear();
            ClusterMap.ReverseSlotGraph.Clear();
            
            ClusterMap.AllProducersList.Clear();
            ClusterMap.SlotToClusters.Clear();
            enabledRefRW.ValueRW=false;
        }
    }
   
    [BurstCompile]
    NativeArray<Entity> GetEntityArrayByID(int id, NativeParallelMultiHashMap<int, Entity> map)
    {
        int count = map.CountValuesForKey(id);
        NativeArray<Entity> results = new NativeArray<Entity>(count, Allocator.TempJob);

        int index = 0;
        if (map.TryGetFirstValue(id, out Entity entity, out var it))
        {
            do
            {
                results[index++] = entity;
            } while (map.TryGetNextValue(out entity, ref it));
        }
        return results;
    }

    [BurstCompile]
    [WithDisabled(typeof(LoadInfo))]
    public partial struct CollectClusterSlots : IJobEntity
    { 
        public NativeList<SlotReference>.ParallelWriter AllProducersWriter;
        public NativeParallelHashMap<SlotReference, FixedList32Bytes<int>>.ParallelWriter SlotToClustersWriter;       
         public NativeParallelMultiHashMap<int, SlotReference>.ParallelWriter ProducersWriter;
        public NativeParallelMultiHashMap<int, SlotReference>.ParallelWriter ConsumersWriter;
        public NativeParallelMultiHashMap<Entity, SlotReference>.ParallelWriter InputSlotsWriter;
        public NativeParallelMultiHashMap<Entity, SlotReference>.ParallelWriter OutputSlotsWriter;

        [ReadOnly] public ComponentLookup<CraftingPriorityData> CraftPriority;
        [ReadOnly] public BufferLookup<InputSlotData> InputCraftSlotDataLookup;
        [ReadOnly] public ComponentLookup<IsInputCraftEnabled> IsInputCraftEnabled;
        
        [ReadOnly] public BufferLookup<OutputSlotData> OutputCraftSlotsDataLookup;
        [ReadOnly] public ComponentLookup<IsOutputCraftEnabled> IsOutputCraftEnabled;


        [ReadOnly] public ComponentLookup<ConstructionPriorityData> ConstructionPriority;
        [ReadOnly] public BufferLookup<InputConstructionSlotData> InputConstructionSlotDataLookup;
        [ReadOnly] public ComponentLookup<IsInputConstructionEnabled> IsInputConstructionEnabled;
        
        [ReadOnly] public BufferLookup<OutputConstructionSlotData> OutputConstructionSlotsDataLookup;
        [ReadOnly] public ComponentLookup<IsOutputConstuctionEnabled> IsOutputConstructionEnabled;

        [ReadOnly] public BufferLookup<ExcessSlotData> ExcesSlotsDataLookup;
        [ReadOnly] public BufferLookup<StorageSlotData> StorageSlotsDataLookup;

        [ReadOnly] public ComponentLookup<IsBlueprint> IsBlueprintLookup;
        [ReadOnly] public ComponentLookup<IsDemolition> IsDemolitionLookup;
        public void Execute(Entity entity, in ClusterLink clusterLink)
        {
            var ids = clusterLink.ClusterIds;
            if (ids.Length == 0) return;
             if (ExcesSlotsDataLookup.HasBuffer(entity))
            {
                var buff = ExcesSlotsDataLookup[entity];
                for (int i = 0; i < buff.Length; i++)
                {
                    if (buff[i].Amount < 1) continue;
                    var slot=new SlotReference
                    {
                        Owner=entity,
                        ItemID=buff[i].ItemId,
                        Type=SlotType.Excess,
                        Index=i,
                        Priority=0,
                    };
                    AllProducersWriter.AddNoResize(slot); 
                    SlotToClustersWriter.TryAdd(slot, ids);
                    OutputSlotsWriter.Add(entity, slot);
                    for (int j = 0; j < ids.Length; j++) 
                         ProducersWriter.Add(ids[j], slot);
                }
            }
            if (IsBlueprintLookup.HasComponent(entity)&&IsBlueprintLookup.IsComponentEnabled(entity) || IsDemolitionLookup.HasComponent(entity)&&IsDemolitionLookup.IsComponentEnabled(entity))
            {
                byte priority = (byte)ConstructionPriority[entity].ConstructionPriority;
                if (IsInputConstructionEnabled.IsComponentEnabled(entity))
                {
                    var buff = InputConstructionSlotDataLookup[entity];
                    for (int i = 0; i < buff.Length; i++)
                    {
                        var slot=new SlotReference
                        {
                            Owner=entity,
                            ItemID=buff[i].ItemId,
                            Type=SlotType.InputConstruction,
                            Index=i,
                            Priority=buff[i].Amount<buff[i].Capacity?priority:(byte)(priority+100),
                        };
                        InputSlotsWriter.Add(entity, slot);
                        for (int j = 0; j < ids.Length; j++) 
                            ConsumersWriter.Add(ids[j], slot);
                    }
                }
                if (IsOutputConstructionEnabled.IsComponentEnabled(entity))
                {
                    var buff = OutputConstructionSlotsDataLookup[entity];
                    for (int i = 0; i < buff.Length; i++)
                    {
                        var slot=new SlotReference
                        {
                            Owner=entity,
                            ItemID=buff[i].ItemId,
                            Type=SlotType.OutputConstruction,
                            Index=i,
                            Priority=priority,
                        }; 
                        AllProducersWriter.AddNoResize(slot); 
                        SlotToClustersWriter.TryAdd(slot, ids);
                        OutputSlotsWriter.Add(entity, slot);
                        for (int j = 0; j < ids.Length; j++) 
                            ProducersWriter.Add(ids[j], slot);
                    }
                }
            }
               
            if (CraftPriority.HasComponent(entity))
            {
                byte Priority=(byte)(10+CraftPriority[entity].CraftingPriority);
                if (IsInputCraftEnabled.HasComponent(entity) && IsInputCraftEnabled.IsComponentEnabled(entity))
                {
                    var buff=InputCraftSlotDataLookup[entity];
                    for(int i=0;i<buff.Length;i++)
                    {
                        var slot=new SlotReference
                        {
                            Owner=entity,
                            ItemID=buff[i].ItemId,
                            Type=SlotType.Input,
                            Index=i,
                            Priority=buff[i].Amount<buff[i].Capacity?Priority:(byte)(Priority+100),
                        };
                        InputSlotsWriter.Add(entity,slot);
                        for (int j = 0; j < ids.Length; j++) 
                            ConsumersWriter.Add(ids[j], slot);
                    }
                }
                if (IsOutputCraftEnabled.HasComponent(entity) && IsOutputCraftEnabled.IsComponentEnabled(entity))
                {
                    var buff=OutputCraftSlotsDataLookup[entity];
                    for(int i=0;i<buff.Length;i++)
                    {
                        var slot=new SlotReference
                        {
                            Owner=entity,
                            ItemID=buff[i].ItemId,
                            Type=SlotType.Output,
                            Index=i,
                            Priority=Priority,
                        };
                        AllProducersWriter.AddNoResize(slot); 
                        SlotToClustersWriter.TryAdd(slot, ids);
                        OutputSlotsWriter.Add(entity,slot);
                        for (int j = 0; j < ids.Length; j++) 
                            ProducersWriter.Add(ids[j], slot);
                    }
                }
                if (StorageSlotsDataLookup.HasBuffer(entity))
                {
                    Priority=(byte)(Priority+10);
                    var buff=StorageSlotsDataLookup[entity];
                    for(int i=0;i<buff.Length;i++)
                    {
                        var buffSlot=buff[i];
                        if(buffSlot.IsInputEnabled)
                        {
                            var slot=new SlotReference
                            {
                                Owner=entity,
                                ItemID=buffSlot.ItemId,
                                Type=SlotType.StorageInput,
                                Index=i,
                                Priority=buffSlot.Amount<buffSlot.Capacity?Priority:(byte)(Priority+100),
                            };
                            InputSlotsWriter.Add(entity,slot);
                            for (int j = 0; j < ids.Length; j++) 
                                ConsumersWriter.Add(ids[j], slot);
                        }
                        if (buffSlot.IsOutputEnabled)
                        {
                            var slot=new SlotReference
                            {
                                Owner=entity,
                                ItemID=buffSlot.ItemId,
                                Type=SlotType.StorageOutput,
                                Index=i,
                                Priority=Priority,
                            };
                            AllProducersWriter.AddNoResize(slot); 
                            SlotToClustersWriter.TryAdd(slot, ids);
                            OutputSlotsWriter.Add(entity,slot);
                            for (int j = 0; j < ids.Length; j++) 
                                ProducersWriter.Add(ids[j], slot);
                        }
                    }
                }
            }
        }

    }


   [BurstCompile]
    public struct LinkSlotsCrossClusterJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<SlotReference> AllProducers;
        
        [ReadOnly] public NativeParallelMultiHashMap<int, SlotReference> ClusterToConsumers;
        [ReadOnly] public NativeParallelHashMap<SlotReference, FixedList32Bytes<int>> SlotToClusters;

        public NativeParallelMultiHashMap<SlotReference, SlotReference>.ParallelWriter SlotGraph;
        public NativeParallelMultiHashMap<SlotReference, SlotReference>.ParallelWriter ReverseSlotGraph;

        public void Execute(int index)
        {
            SlotReference producer = AllProducers[index];
            
            if (!SlotToClusters.TryGetValue(producer, out var clusterList)) return;
            if(clusterList[0]==-1) return;
            for (int i = 0; i < clusterList.Length; i++)
            {
                int clusterId = clusterList[i];
                
                var consumersInCluster = ClusterToConsumers.GetValuesForKey(clusterId);
                while (consumersInCluster.MoveNext())
                {
                    SlotReference consumer = consumersInCluster.Current;

                    if (producer.ItemID == consumer.ItemID)
                    {
                        SlotGraph.Add(producer, consumer);
                        ReverseSlotGraph.Add(consumer, producer);
                    }
                }
            }
        }
    }

     [BurstCompile]
    public struct LogisticsDistributionJob : IJobParallelForDefer 
    {
        [ReadOnly] public NativeArray<SlotReference> AllProducers;
        [ReadOnly] public NativeParallelMultiHashMap<SlotReference, SlotReference> SlotGraph;
        public int MaxStreamPockets;

        public NativeStream.Writer Transactions;
         [NativeDisableParallelForRestriction] 
        public BufferLookup<InputSlotData> InputLookup;
         [NativeDisableParallelForRestriction] 
        public BufferLookup<OutputSlotData> OutputLookup;
         [NativeDisableParallelForRestriction] 
        public BufferLookup<InputConstructionSlotData> InputConstructionLookup;
         [NativeDisableParallelForRestriction] 
        public BufferLookup<OutputConstructionSlotData> OutputConstructionLookup;
         [NativeDisableParallelForRestriction] 
        public BufferLookup<StorageSlotData> StorageLookup;
         [NativeDisableParallelForRestriction] 
        public BufferLookup<ExcessSlotData> ExcessLookup;


        public void Execute(int index)
        {
            int pocketIdx = index % MaxStreamPockets;
            Transactions.BeginForEachIndex(pocketIdx);

            SlotReference pRef = AllProducers[index];
            int available = GetAmountReadOnly(pRef); 
            if (available > 0) 
            {
                var consumers = new NativeList<SlotReference>(16, Allocator.Temp);
                if (SlotGraph.TryGetFirstValue(pRef, out var cRef, out var it))
                {
                    do { consumers.Add(cRef); } 
                    while (SlotGraph.TryGetNextValue(out cRef, ref it));
                }

                consumers.Sort();

                for (int j = 0; j < consumers.Length; j++)
                {
                    if (available <= 0) break;
                    
                    var target = consumers[j];
                    int space = GetCapacityReadOnly(target) - GetAmountReadOnly(target);

                    if (space > 0)
                    {
                        int transfer = math.min(available, space);
                        
                        Transactions.Write(new LogisticsTransaction {
                            Source = pRef,
                            Target = target,
                            Amount = transfer
                        });

                        available -= transfer;
                    }
                }
            }

            Transactions.EndForEachIndex();
        }
        private int GetAmountReadOnly(SlotReference slot)
        {
            switch (slot.Type)
            {
                case SlotType.Input:
                    return InputLookup[slot.Owner][slot.Index].Amount;
                case SlotType.Output:
                    return OutputLookup[slot.Owner][slot.Index].Amount;
                case SlotType.InputConstruction:
                    return InputConstructionLookup[slot.Owner][slot.Index].Amount;
                case SlotType.OutputConstruction:
                    return OutputConstructionLookup[slot.Owner][slot.Index].Amount;
                case SlotType.StorageInput: 
                case SlotType.StorageOutput:
                    return StorageLookup[slot.Owner][slot.Index].Amount;
                case SlotType.Excess:
                    return ExcessLookup[slot.Owner][slot.Index].Amount;
                default:
                    return 0;
            }
        }

        private int GetCapacityReadOnly(SlotReference slot)
        {
            switch (slot.Type)
            {
                case SlotType.Input:
                    return InputLookup[slot.Owner][slot.Index].Capacity;
                case SlotType.InputConstruction:
                    return InputConstructionLookup[slot.Owner][slot.Index].Capacity;
                case SlotType.StorageInput:
                    return StorageLookup[slot.Owner][slot.Index].Capacity;
                case SlotType.Output:
                    return OutputLookup[slot.Owner][slot.Index].Capacity;
                case SlotType.OutputConstruction:
                    return OutputConstructionLookup[slot.Owner][slot.Index].Capacity;
                case SlotType.StorageOutput:
                    return StorageLookup[slot.Owner][slot.Index].Capacity;
                case SlotType.Excess:
                    return ExcessLookup[slot.Owner][slot.Index].Capacity;
                default:
                    return 0;
            }
        }
        
    }
}

[BurstCompile]
public struct ApplyLogisticsJob : IJob
{
    public NativeStream.Reader Transactions;

    public BufferLookup<InputSlotData> InputLookup;
    public BufferLookup<OutputSlotData> OutputLookup;
    public BufferLookup<InputConstructionSlotData> InputConstructionLookup;
    public BufferLookup<OutputConstructionSlotData> OutputConstructionLookup;
    public BufferLookup<StorageSlotData> StorageLookup;
    public BufferLookup<ExcessSlotData> ExcessLookup;

    public void Execute()
    {
        for (int i = 0; i < Transactions.ForEachCount; i++)
        {
            Transactions.BeginForEachIndex(i);
            while (Transactions.RemainingItemCount > 0)
            {
                var tx = Transactions.Read<LogisticsTransaction>();
                
                int actualAvailable = GetAmount(tx.Source);
                int actualSpace = GetCapacity(tx.Target) - GetAmount(tx.Target);

                int finalTransfer = math.min(tx.Amount, math.min(actualAvailable, actualSpace));

                if (finalTransfer > 0)
                {
                    AddAmount(tx.Source, -finalTransfer);
                    AddAmount(tx.Target, finalTransfer);
                }
            }
            Transactions.EndForEachIndex();
        }
    }

    private int GetAmount(SlotReference slot)
    {
        switch (slot.Type)
        {
            case SlotType.Input: return InputLookup[slot.Owner][slot.Index].Amount;
            case SlotType.Output: return OutputLookup[slot.Owner][slot.Index].Amount;
            case SlotType.InputConstruction: return InputConstructionLookup[slot.Owner][slot.Index].Amount;
            case SlotType.OutputConstruction: return OutputConstructionLookup[slot.Owner][slot.Index].Amount;
            case SlotType.StorageInput: 
            case SlotType.StorageOutput: return StorageLookup[slot.Owner][slot.Index].Amount;
            case SlotType.Excess: return ExcessLookup[slot.Owner][slot.Index].Amount;
            default: return 0;
        }
    }

     private int GetCapacity(SlotReference slot)
    {
        switch (slot.Type)
        {
            case SlotType.Input:
                return InputLookup[slot.Owner][slot.Index].Capacity;
            case SlotType.InputConstruction:
                return InputConstructionLookup[slot.Owner][slot.Index].Capacity;
            case SlotType.StorageInput:
                return StorageLookup[slot.Owner][slot.Index].Capacity;
            case SlotType.Output:
                return OutputLookup[slot.Owner][slot.Index].Capacity;
            case SlotType.OutputConstruction:
                return OutputConstructionLookup[slot.Owner][slot.Index].Capacity;
            case SlotType.StorageOutput:
                return StorageLookup[slot.Owner][slot.Index].Capacity;
            case SlotType.Excess:
                return ExcessLookup[slot.Owner][slot.Index].Capacity;
            default:
                return 0;
        }
    }

    private void AddAmount(SlotReference slot, int change)
    {
        switch (slot.Type)
        {
            case SlotType.Input:
                var input = InputLookup[slot.Owner];
                var inputVal = input[slot.Index];
                inputVal.Amount += change;
                input[slot.Index] = inputVal;
                break;
            case SlotType.Output:
                var output = OutputLookup[slot.Owner];
                var outputVal = output[slot.Index];
                outputVal.Amount += change;
                output[slot.Index] = outputVal;
                break;
            case SlotType.StorageInput:
            case SlotType.StorageOutput:
                var storage = StorageLookup[slot.Owner];
                var storageVal = storage[slot.Index];
                storageVal.Amount += change;
                storage[slot.Index] = storageVal;
                break;
            case SlotType.InputConstruction:
                var inputConst = InputConstructionLookup[slot.Owner];
                var inputConstVal = inputConst[slot.Index];
                inputConstVal.Amount += change;
                inputConst[slot.Index] = inputConstVal;
                break;
            case SlotType.OutputConstruction:
                var outputConst = OutputConstructionLookup[slot.Owner];
                var outputConstVal = outputConst[slot.Index];
                outputConstVal.Amount += change;
                outputConst[slot.Index] = outputConstVal;
                break;
            case SlotType.Excess:
                var excess = ExcessLookup[slot.Owner];
                var excessVal = excess[slot.Index];
                excessVal.Amount += change;
                excess[slot.Index] = excessVal;
                break;
        }
    }
}

public struct LogisticsTransaction
{
    public SlotReference Source;
    public SlotReference Target;
    public int Amount;
}
