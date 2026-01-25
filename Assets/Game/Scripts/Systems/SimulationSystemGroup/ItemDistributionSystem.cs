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
    
    float _accumulatedTime;
    uint _frameCount;   
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
    }
    public void OnUpdate(ref SystemState state)
    {
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
        if (!MapUpdate.IsEmptyIgnoreFilter)
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
                UniqueIDs= clusterMap.ValueRW.UniqueClusterIDs,
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
            var deferredArray = clusterMap.ValueRO.UniqueClusterIDs.AsDeferredJobArray();
            var linkJob = new LinkSlotsInClustertGraphJob
            {
                UniqueClusterIDs = deferredArray,
                ClusterToProducers = clusterMap.ValueRO.ClusterToProducers,
                ClusterToConsumers = clusterMap.ValueRO.ClusterToConsumers,
                SlotGraph = clusterMap.ValueRW.SlotGraph.AsParallelWriter(),
                ReverseSlotGraph = clusterMap.ValueRW.ReverseSlotGraph.AsParallelWriter()
            };

            state.Dependency= linkJob.Schedule(clusterMap.ValueRO.UniqueClusterIDs, 1, state.Dependency);

        }
        _accumulatedTime += SystemAPI.Time.DeltaTime;
        var tickInfoData = SystemAPI.GetSingleton<TickInfoData>();
        
        _frameCount++; 
        if (_frameCount % tickInfoData.currTickPerSecond == 0) 
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
            IsDemolitionLookup.Update(ref state);

            var distJob = new LogisticsDistributionJob
            {
                UniqueClusterIDs = clusterMap.ValueRO.UniqueClusterIDs.AsDeferredJobArray(),
                ClusterToProducers = clusterMap.ValueRO.ClusterToProducers,
                SlotGraph = clusterMap.ValueRO.SlotGraph,

                InputLookup = InputCraftSlotDataLookup,
                OutputLookup = OutputCraftSlotsDataLookup,
                InputConstructionLookup = InputConstructionSlotDataLookup,
                OutputConstructionLookup = OutputConstructionSlotsDataLookup,
                StorageLookup = StorageSlotsDataLookup,
                ExcessLookup = ExcesSlotsDataLookup
            };
            state.Dependency = distJob.Schedule(clusterMap.ValueRO.UniqueClusterIDs, 1, state.Dependency);
             if (!_realizeBuildingsQuery.IsEmptyIgnoreFilter)
            {
                var bJob=new RealizeBluePrintBuildingJob();
                state.Dependency=bJob.ScheduleParallel( state.Dependency);
            }
            if (!_deleteBuildingsQuery.IsEmptyIgnoreFilter)
            {
                var dJob=new DestroyBuildingJob();
                state.Dependency=dJob.ScheduleParallel( state.Dependency);
            }
            
            state.Dependency= new ExcessCleanupJob{ECB=ecb.AsParallelWriter(),mapEntity=mapEntity}.ScheduleParallel(state.Dependency);
            _accumulatedTime = 0; 
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
            enabledRefRW.ValueRW=false;
        }
    }
    [BurstCompile]
    [WithAll(typeof(IsDemolition))]
    [WithDisabled(typeof(ForceDestroyTag),typeof(ChangeDemolitionStateTag))]
    public partial struct DestroyBuildingJob : IJobEntity
    {
        public void Execute(in DynamicBuffer<OutputConstructionSlotData> output,EnabledRefRW<ForceDestroyTag> state)
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
            if(shouldDelete)state.ValueRW=true;
        }
    }
    
    //передалть
    [BurstCompile]
    [WithAll(typeof(IsBlueprint))]
    [WithDisabled(typeof(ForceDestroyTag),typeof(ChangeBluePrintState),typeof(IsDemolition))]
    public partial struct RealizeBluePrintBuildingJob : IJobEntity
    {
        public void Execute(in DynamicBuffer<InputConstructionSlotData> input,
                                        EnabledRefRW<ChangeBluePrintState> state,
                                        EnabledRefRW<IsLogicEnabled> logicState)
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
                 state.ValueRW=true;
                 logicState.ValueRW=true;
            }
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
    public partial struct CollectClusterSlots : IJobEntity
    {
        public NativeParallelMultiHashMap<int, SlotReference>.ParallelWriter ProducersWriter;
        public NativeParallelMultiHashMap<int, SlotReference>.ParallelWriter ConsumersWriter;
        public NativeParallelMultiHashMap<Entity, SlotReference>.ParallelWriter InputSlotsWriter;
        public NativeParallelMultiHashMap<Entity, SlotReference>.ParallelWriter OutputSlotsWriter;

        [ReadOnly] public NativeList<int> UniqueIDs;

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
        public void Execute(Entity entity,in ClusterId clusterId)
        {
            if (clusterId.Value != -1)
            {
                if (ExcesSlotsDataLookup[entity].Length > 0)
                {
                    var buff=ExcesSlotsDataLookup[entity];
                    for(int i=0;i<buff.Length;i++)
                    {
                        if(buff[i].Amount<1) continue;
                        var slot=new SlotReference
                        {
                            Owner=entity,
                            ItemID=buff[i].ItemId,
                            Type=SlotType.Excess,
                            Index=i,
                            Priority=0,
                        };
                        ProducersWriter.Add(clusterId.Value,slot);
                        OutputSlotsWriter.Add(entity,slot);
                    }
                }
                if (IsBlueprintLookup.IsComponentEnabled(entity) || IsDemolitionLookup.IsComponentEnabled(entity))
                {
                    byte Priority=(byte)ConstructionPriority[entity].ConstructionPriority;
                    if(IsInputConstructionEnabled.IsComponentEnabled(entity))
                    {
                        var buff=InputConstructionSlotDataLookup[entity];
                        for(int i=0;i<buff.Length;i++)
                        {
                            var slot=new SlotReference
                            {
                                Owner=entity,
                                ItemID=buff[i].ItemId,
                                Type=SlotType.InputConstruction,
                                Index=i,
                                Priority=buff[i].Amount<buff[i].Capacity?Priority:(byte)(Priority+100),
                            };
                           InputSlotsWriter.Add(entity,slot);
                           ConsumersWriter.Add(clusterId.Value,slot);
                        }
                    }
                    if(IsOutputConstructionEnabled.IsComponentEnabled(entity))
                    {
                        var buff=OutputConstructionSlotsDataLookup[entity];
                        for(int i=0;i<buff.Length;i++)
                        {
                            var slot=new SlotReference
                            {
                                Owner=entity,
                                ItemID=buff[i].ItemId,
                                Type=SlotType.OutputConstruction,
                                Index=i,
                                Priority=Priority,
                            };
                            ProducersWriter.Add(clusterId.Value,slot);
                            OutputSlotsWriter.Add(entity,slot);
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
                            ConsumersWriter.Add(clusterId.Value,slot);;
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
                            ProducersWriter.Add(clusterId.Value,slot);
                            OutputSlotsWriter.Add(entity,slot);
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
                                ConsumersWriter.Add(clusterId.Value,slot);;
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
                                ProducersWriter.Add(clusterId.Value,slot);
                                OutputSlotsWriter.Add(entity,slot);
                            }
                        }
                    }
                }
            }
        }

    }


    [BurstCompile]
    public struct LinkSlotsInClustertGraphJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<int> UniqueClusterIDs;
        
        [ReadOnly] public NativeParallelMultiHashMap<int, SlotReference> ClusterToProducers;
        [ReadOnly] public NativeParallelMultiHashMap<int, SlotReference> ClusterToConsumers;

        public NativeParallelMultiHashMap<SlotReference, SlotReference>.ParallelWriter SlotGraph;
        public NativeParallelMultiHashMap<SlotReference, SlotReference>.ParallelWriter ReverseSlotGraph;

        public void Execute(int index)
        {
            int clusterId = UniqueClusterIDs[index];

            var consumersInCluster = ClusterToConsumers.GetValuesForKey(clusterId);
            var tempConsumers = new NativeList<SlotReference>(32, Allocator.Temp);
            
            while (consumersInCluster.MoveNext())
            {
                tempConsumers.Add(consumersInCluster.Current);
            }

            if (tempConsumers.Length == 0) return;

            var producers = ClusterToProducers.GetValuesForKey(clusterId);

            while (producers.MoveNext())
            {
                SlotReference producer = producers.Current;

                for (int i = 0; i < tempConsumers.Length; i++)
                {
                    SlotReference consumer = tempConsumers[i];

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
        [ReadOnly] public NativeArray<int> UniqueClusterIDs;
        [ReadOnly] public NativeParallelMultiHashMap<int, SlotReference> ClusterToProducers;
        [ReadOnly] public NativeParallelMultiHashMap<SlotReference, SlotReference> SlotGraph;
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
            int clusterId = UniqueClusterIDs[index];

            var producerEnumerator = ClusterToProducers.GetValuesForKey(clusterId);
            var sortedProducers = new NativeList<SlotReference>(64, Allocator.Temp);
            while (producerEnumerator.MoveNext())
            {
                sortedProducers.Add(producerEnumerator.Current);
            }

            if (sortedProducers.Length == 0) return;

            sortedProducers.Sort(new ProducerAmountComparer { 
                InputLookup = InputLookup, OutputLookup = OutputLookup, 
                InputConstructionLookup = InputConstructionLookup, OutputConstructionLookup = OutputConstructionLookup,
                StorageLookup = StorageLookup, ExcessLookup = ExcessLookup 
            });

            // 2. Начинаем распределение
            var sortedConsumers = new NativeList<SlotReference>(32, Allocator.Temp);

            for (int pIdx = 0; pIdx < sortedProducers.Length; pIdx++)
            {
                SlotReference pRef = sortedProducers[pIdx];
                int availableItems = GetAmount(pRef);
                if (availableItems <= 0) continue;

                var consumerEnumerator = SlotGraph.GetValuesForKey(pRef);
                sortedConsumers.Clear();
                while (consumerEnumerator.MoveNext())
                {
                    sortedConsumers.Add(consumerEnumerator.Current);
                }

                sortedConsumers.Sort();

                for (int cIdx = 0; cIdx < sortedConsumers.Length; cIdx++)
                {
                    if (availableItems <= 0) break;

                    SlotReference cRef = sortedConsumers[cIdx];
                    int currentAmount = GetAmount(cRef);
                    int capacity = GetCapacity(cRef);
                    int space = capacity - currentAmount;

                    if (space > 0)
                    {
                        int transfer = math.min(availableItems, space);
                        
                        AddAmount(ref pRef, -transfer);
                        AddAmount(ref cRef, transfer);
                        
                        availableItems -= transfer;
                    }
                }
            }
        }
        public static int StaticGetAmount(SlotReference s, 
        in BufferLookup<InputSlotData> inL, in BufferLookup<OutputSlotData> outL,
        in BufferLookup<InputConstructionSlotData> inConL, in BufferLookup<OutputConstructionSlotData> outConL,
        in BufferLookup<StorageSlotData> stL, in BufferLookup<ExcessSlotData> exL)
        {
            return s.Type switch
            {
                SlotType.Input => inL[s.Owner][s.Index].Amount,
                SlotType.Output => outL[s.Owner][s.Index].Amount,
                SlotType.InputConstruction => inConL[s.Owner][s.Index].Amount,
                SlotType.OutputConstruction => outConL[s.Owner][s.Index].Amount,
                SlotType.StorageInput or SlotType.StorageOutput => stL[s.Owner][s.Index].Amount,
                SlotType.Excess => exL[s.Owner][s.Index].Amount,
                _ => 0
            };
        }
        private int GetAmount(SlotReference s) => StaticGetAmount(s, InputLookup, OutputLookup, 
        InputConstructionLookup, OutputConstructionLookup, StorageLookup, ExcessLookup);
        private int GetCapacity(SlotReference s)
        {
            return s.Type switch
            {
                SlotType.Input => InputLookup[s.Owner][s.Index].Capacity,
                SlotType.Output => OutputLookup[s.Owner][s.Index].Capacity,
                SlotType.InputConstruction => InputConstructionLookup[s.Owner][s.Index].Capacity,
                SlotType.OutputConstruction => OutputConstructionLookup[s.Owner][s.Index].Capacity,
                SlotType.StorageInput or SlotType.StorageOutput => StorageLookup[s.Owner][s.Index].Capacity,
                SlotType.Excess => ExcessLookup[s.Owner][s.Index].Capacity,
                _ => 0
            };
        }
        private void AddAmount(ref SlotReference s, int change)
        {
            int finalAmount = 0;
            int capacity = 0;

            switch (s.Type)
            {
                case SlotType.Input:
                    var b1 = InputLookup[s.Owner]; var d1 = b1[s.Index]; d1.Amount += change; 
                    finalAmount = d1.Amount; capacity = d1.Capacity; b1[s.Index] = d1; break;
                case SlotType.Output:
                    var b2 = OutputLookup[s.Owner]; var d2 = b2[s.Index]; d2.Amount += change; 
                    finalAmount = d2.Amount; capacity = d2.Capacity; b2[s.Index] = d2; break;
                case SlotType.InputConstruction:
                    var b3 = InputConstructionLookup[s.Owner]; var d3 = b3[s.Index]; d3.Amount += change; 
                    finalAmount = d3.Amount; capacity = d3.Capacity; b3[s.Index] = d3; break;
                case SlotType.OutputConstruction:
                    var b4 = OutputConstructionLookup[s.Owner]; var d4 = b4[s.Index]; d4.Amount += change; 
                    finalAmount = d4.Amount; capacity = d4.Capacity; b4[s.Index] = d4; break;
                case SlotType.StorageInput:
                    var b5 = StorageLookup[s.Owner]; var d5 = b5[s.Index]; d5.Amount += change; 
                    finalAmount = d5.Amount; capacity = d5.Capacity; b5[s.Index] = d5; break;
                case SlotType.StorageOutput:
                    var b6 = StorageLookup[s.Owner]; var d6 = b6[s.Index]; d6.Amount += change; 
                    finalAmount = d6.Amount; capacity = d6.Capacity; b6[s.Index] = d6; break;
                case SlotType.Excess:
                    var b7 = ExcessLookup[s.Owner]; var d7 = b7[s.Index]; d7.Amount += change; 
                    finalAmount = d7.Amount; capacity = d7.Capacity; b7[s.Index] = d7; break;
            } 
            if (change > 0 && finalAmount >= capacity)
            {
                s.Priority = (byte)math.min(255, s.Priority + 100);
            }
        }
        public struct ProducerAmountComparer : IComparer<SlotReference>
        {
            [ReadOnly] public BufferLookup<InputSlotData> InputLookup;
            [ReadOnly] public BufferLookup<OutputSlotData> OutputLookup;
            [ReadOnly] public BufferLookup<InputConstructionSlotData> InputConstructionLookup;
            [ReadOnly] public BufferLookup<OutputConstructionSlotData> OutputConstructionLookup;
            [ReadOnly] public BufferLookup<StorageSlotData> StorageLookup;
            [ReadOnly] public BufferLookup<ExcessSlotData> ExcessLookup;

            public int Compare(SlotReference x, SlotReference y)
            {
                int amountX = LogisticsDistributionJob.StaticGetAmount(x, InputLookup, OutputLookup, 
                    InputConstructionLookup, OutputConstructionLookup, StorageLookup, ExcessLookup);
                
                int amountY = LogisticsDistributionJob.StaticGetAmount(y, InputLookup, OutputLookup, 
                    InputConstructionLookup, OutputConstructionLookup, StorageLookup, ExcessLookup);

                return amountX.CompareTo(amountY);
            }
        }
    }
}
