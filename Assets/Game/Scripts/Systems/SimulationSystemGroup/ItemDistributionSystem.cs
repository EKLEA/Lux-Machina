using System;
using ModestTree;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BuildingConfigManagerSystem))]
[DisableAutoCreation]
public partial struct ItemDistributionSystem : ISystem
{
    EntityQuery _deleteBuildingsQuery;
    EntityQuery _realizeBuildingsQuery;
    
    float _accumulatedTime;
    uint _frameCount;   
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
    //передалть
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

    //МОЕ
    // public void OnUpdate(ref SystemState state)
    // {
    //     _accumulatedTime += SystemAPI.Time.DeltaTime;
    //     var tickInfoData = SystemAPI.GetSingleton<TickInfoData>();
        
    //     _frameCount++; 
    //     if (_frameCount % tickInfoData.currTickPerSecond == 0) 
    //     {  
    //         if (!SystemAPI.HasSingleton<ClusterMap>()) return;
            

    //         var clusterMap = SystemAPI.GetSingleton<ClusterMap>();
            
    //         var clusterIDs = clusterMap.clusterIDs;
    //         clusterIDs.Sort(); 
    //         JobHandle inputDeps = state.Dependency;
    //         var combinedHandles = new NativeList<JobHandle>(clusterIDs.Length, Allocator.Temp);
    //         foreach( var clusterID in clusterIDs)
    //         {
    //             var job = new ItemDistributionJob
    //             {
    //                 producersSlots =GetEntityArrayByID(clusterID,clusterMap.producersSlots),
    //                 consumersSlots =GetEntityArrayByID(clusterID,clusterMap.consumersSlots),
    //                 storagesSlots =GetEntityArrayByID(clusterID,clusterMap.storagesSlots),
    //                 excessSlots =GetEntityArrayByID(clusterID,clusterMap.excessSlots),
    //                 bluePrintsSlots =GetEntityArrayByID(clusterID,clusterMap.bluePrintsSlots),
    //                 demolitionsSlots =GetEntityArrayByID(clusterID,clusterMap.demolitionsSlots)
    //             };
                
    //             JobHandle jobHandle = job.Schedule(inputDeps);
    //             combinedHandles.Add(jobHandle);
    //         }
            
    //         state.Dependency = JobHandle.CombineDependencies(combinedHandles.AsArray());
    //         if (!_realizeBuildingsQuery.IsEmptyIgnoreFilter)
    //         {
    //             var bJob=new RealizeBluePrintBuildingJob();
    //             state.Dependency=bJob.ScheduleParallel( state.Dependency);
    //         }
    //         if (!_deleteBuildingsQuery.IsEmptyIgnoreFilter)
    //         {
    //             var dJob=new DestroyBuildingJob();
    //             state.Dependency=dJob.ScheduleParallel( state.Dependency);
    //         }
    //         if (!_realizeBuildingsQuery.IsEmptyIgnoreFilter)
    //         {
    //             var pJob=new DestroyBuildingJob();
    //             state.Dependency=pJob.ScheduleParallel( state.Dependency);
    //         }
    //         clusterIDs.Dispose(state.Dependency);
    //         combinedHandles.Dispose();
        
    //         _accumulatedTime = 0; 
    //     }
    // }
    
    public void OnUpdate(ref SystemState state)
    {
        _accumulatedTime += SystemAPI.Time.DeltaTime;
        var tickInfoData = SystemAPI.GetSingleton<TickInfoData>();
        
        _frameCount++; 
        if (_frameCount % tickInfoData.currTickPerSecond == 0) 
        {  
            if (!SystemAPI.HasSingleton<ClusterMap>())
                return;

            var clusterMap = SystemAPI.GetSingleton<ClusterMap>();

            // if (clusterMap.clusterIDs.IsCreated && clusterMap.clusterIDs.Length > 1)
            // {
            //     state.Dependency = clusterMap.clusterIDs.SortJob().Schedule(state.Dependency);
            // }

            var job = new ItemDistributionParallelJob
            {
                ClusterIDs = clusterMap.clusterIDs.AsDeferredJobArray(),
                
                ProducersMap = clusterMap.producersSlots,
                ConsumersMap = clusterMap.consumersSlots,
                StoragesMap = clusterMap.storagesSlots,
                ExcessMap = clusterMap.excessSlots,
                BluePrintsMap = clusterMap.bluePrintsSlots,
                DemolitionsMap = clusterMap.demolitionsSlots,

                InputConstruction = SystemAPI.GetBufferLookup<InputConstructionSlotData>(false),
                InputCraft = SystemAPI.GetBufferLookup<InputSlotData>(false),
                Storage = SystemAPI.GetBufferLookup<StorageSlotData>(false),
                OutputConstruction = SystemAPI.GetBufferLookup<OutputConstructionSlotData>(false),
                OutputCraft = SystemAPI.GetBufferLookup<OutputSlotData>(false),
                Excess = SystemAPI.GetBufferLookup<ExcessSlotData>(false)
            };

            state.Dependency = job.Schedule(clusterMap.clusterIDs, 1, state.Dependency);







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
            _accumulatedTime = 0; 
        }
    }
    
    //МОЕ
    // [BurstCompile]
    // public struct ItemDistributionJob : IJob 
    // {
        
    //     [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> producersSlots;
    //     [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> consumersSlots;
    //     [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> storagesSlots;
    //     [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> excessSlots;
    //     [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> bluePrintsSlots;
    //     [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> demolitionsSlots;
        
    //     [DeallocateOnJobCompletion] NativeParallelMultiHashMap<int, (InputConstructionSlotData slotData, int slotInd, Entity entity)> constructItemsRequests;
    //     [DeallocateOnJobCompletion] NativeParallelMultiHashMap<int, (InputSlotData slotData, int slotInd, Entity entity)> craftItemsRequests;
    //     [DeallocateOnJobCompletion] NativeParallelMultiHashMap<int, (StorageSlotData slotData, int slotInd, Entity entity)> storageItemsRequests;

    //     [DeallocateOnJobCompletion] NativeParallelMultiHashMap<int, (OutputConstructionSlotData slotData, int slotInd, Entity entity)> demolitionsItemSlots;
    //     [DeallocateOnJobCompletion] NativeParallelMultiHashMap<int, (ExcessSlotData slotData, int slotInd, Entity entity)> excessItemSlots;
    //     [DeallocateOnJobCompletion] NativeParallelMultiHashMap<int, (OutputSlotData slotData, int slotInd, Entity entity)> craftOutputItemsSlots;
    //     [DeallocateOnJobCompletion] NativeParallelMultiHashMap<int, (StorageSlotData slotData, int slotInd, Entity entity)> storageItemsSlots;

    //     [DeallocateOnJobCompletion] NativeHashSet<int> ids;

        
    //     public BufferLookup<InputSlotData> InputCraftSlotDataLookup;
    //     public BufferLookup<OutputSlotData> OutputCraftSlotsDataLookup;
    //     public BufferLookup<InputConstructionSlotData> InputConstructionSlotDataLookup;
    //     public BufferLookup<OutputConstructionSlotData> OutputConstructionSlotsDataLookup;
    //     public BufferLookup<ExcessSlotData> ExcesSlotsDataLookup;
    //     public BufferLookup<StorageSlotData> StorageSlotsDataLookup;

    //     public void Execute()
    //     {
    //         constructItemsRequests=new(bluePrintsSlots.Length*4,Allocator.TempJob);
    //         craftItemsRequests=new(consumersSlots.Length*4,Allocator.TempJob);
    //         storageItemsRequests=new(storagesSlots.Length*10,Allocator.TempJob);

    //         demolitionsItemSlots=new(consumersSlots.Length*4,Allocator.TempJob);
    //         excessItemSlots=new(consumersSlots.Length*4,Allocator.TempJob);
    //         craftOutputItemsSlots=new(consumersSlots.Length*4,Allocator.TempJob);
    //         storageItemsSlots=new(consumersSlots.Length*10,Allocator.TempJob);
    //         ids=new(100,Allocator.TempJob);

            
    //         FillMaps(ref bluePrintsSlots,InputConstructionSlotDataLookup, ref constructItemsRequests);
    //         FillMaps(ref consumersSlots,InputCraftSlotDataLookup, ref craftItemsRequests);

    //         foreach(var st in storagesSlots)
    //         {
    //             StorageSlotsDataLookup.TryGetBuffer(st, out DynamicBuffer<StorageSlotData> buff);
    //             for(int i =0;i<buff.Length;i++)
    //             {
    //                 var sl =buff[i];
    //                 if(sl.IsInputEnabled&&sl.Amount<sl.Capacity)
    //                     storageItemsRequests.Add(sl.ItemId,(sl,i,st));
    //             }
    //         }
            
    //         FillMaps(ref demolitionsSlots,OutputConstructionSlotsDataLookup, ref demolitionsItemSlots);
    //         FillMaps(ref excessSlots,ExcesSlotsDataLookup, ref excessItemSlots);
    //         FillMaps(ref producersSlots,OutputCraftSlotsDataLookup, ref craftOutputItemsSlots);
            
    //         foreach(var st in storagesSlots)
    //         {
    //             StorageSlotsDataLookup.TryGetBuffer(st, out DynamicBuffer<StorageSlotData> buff);
    //             for(int i =0;i<buff.Length;i++)
    //             {
    //                 var sl =buff[i];
    //                 if (sl.IsOutputEnabled)
    //                 {
    //                     if(sl.Amount<sl.Capacity)
    //                         storageItemsSlots.Add(sl.ItemId,(sl,i,st));
    //                 }
    //             }
    //         }
            
    //         foreach (var id in ids)
    //         {
    //             var inputConst=GetItemsArrayByID(id,constructItemsRequests);
    //             var inputCraft=GetItemsArrayByID(id,craftItemsRequests);
    //             var inputStorage=GetItemsArrayByID(id,storageItemsRequests);

    //             var outputConst=GetItemsArrayByID(id,demolitionsItemSlots);
    //             var excess=GetItemsArrayByID(id,excessItemSlots);
    //             var outputCraft=GetItemsArrayByID(id,craftOutputItemsSlots);
    //             var outputStorage=GetItemsArrayByID(id,storageItemsSlots);
    //             if(IsRequestDone(inputConst)&&IsRequestDone(inputCraft)&&IsRequestDone(inputStorage)) continue;
    //             DestributeRequestsByProducer(ref outputConst, ref inputConst,ref inputCraft,ref inputStorage);
    //             DestributeRequestsByProducer(ref excess, ref inputConst,ref inputCraft,ref inputStorage);
    //             DestributeRequestsByProducer(ref outputCraft, ref inputConst,ref inputCraft,ref inputStorage);
    //             DestributeRequestsByProducer(ref outputStorage,ref inputConst,ref inputCraft,ref inputStorage);
    //             inputConst.Dispose();
    //             inputCraft.Dispose();
    //             inputStorage.Dispose();
    //             outputConst.Dispose();
    //             excess.Dispose();
    //             outputCraft.Dispose();
    //             outputStorage.Dispose();
    //         }
    //         UpdateBuffer(InputConstructionSlotDataLookup,constructItemsRequests);
    //         UpdateBuffer(OutputConstructionSlotsDataLookup,demolitionsItemSlots);

    //         UpdateBuffer(InputCraftSlotDataLookup,craftItemsRequests);
    //         UpdateBuffer(OutputCraftSlotsDataLookup,craftOutputItemsSlots);

    //         UpdateBuffer(ExcesSlotsDataLookup,excessItemSlots);

    //         UpdateBuffer(StorageSlotsDataLookup,storageItemsRequests);
    //         UpdateBuffer(StorageSlotsDataLookup,storageItemsSlots);
    //     }
    //     [BurstCompile]
    //     NativeArray<(T slotData, int slotInd, Entity entity)> GetItemsArrayByID<T>(int id, NativeParallelMultiHashMap<int,(T slotData, int slotInd, Entity entity)> map) where T:unmanaged ,ISlot
    //     {
    //         int count = map.CountValuesForKey(id);
    //         var results = new NativeArray<(T slotData, int slotInd, Entity entity)>(count, Allocator.TempJob);

    //         int index = 0;
    //         if (map.TryGetFirstValue(id, out var tupleData, out var it))
    //         {
    //             do
    //             {
    //                 results[index++] = tupleData;
    //             } 
    //             while (map.TryGetNextValue(out tupleData, ref it));
    //         }
            
    //         return results;
    //     }
    //     void DestributeRequestsByProducer<T>(ref NativeArray<(T slotData, int slotInd, Entity entity)> producer,
    //     ref NativeArray<( InputConstructionSlotData slotData, int slotInd, Entity entity)> inputConst,
    //     ref NativeArray<( InputSlotData slotData, int slotInd, Entity entity)> inputCraft,
    //     ref NativeArray<( StorageSlotData slotData, int slotInd, Entity entity)> inputStorage)where T : unmanaged,ISlot,IBufferElementData
    //     {
    //         if(IsRequestDone(inputConst)&&IsRequestDone(inputCraft)&&IsRequestDone(inputStorage)) return;
    //         int ind=0;
    //         while (!IsProdecerDone(producer))
    //         {
    //             if(ind>=producer.Length) break;
    //             var slotP=producer[ind];
    //             if (slotP.slotData.Amount == 0)
    //             {
    //                 ind++;
    //                 continue;
    //             }
    //             if(!IsRequestDone(inputConst))
    //             {
    //                 DestributeRequestsByRequest(ref slotP, ref inputConst);
    //             }
    //             if(!IsRequestDone(inputCraft)&&(slotP.slotData.Amount>0))
    //             {
    //                 DestributeRequestsByRequest(ref slotP, ref inputCraft);
    //             }
    //             if(!IsRequestDone(inputStorage)&&(slotP.slotData.Amount>0))
    //             {
    //                 DestributeRequestsByRequest(ref slotP, ref inputStorage);
    //             }
    //             producer[ind]=slotP;
    //             if (slotP.slotData.Amount == 0) ind++;
    //         }
    //     }
    //     void DestributeRequestsByRequest<T,V>(ref (T slotData, int slotInd, Entity entity) producerSlot,ref NativeArray<(V slotData, int slotInd, Entity entity)> requests)where T : unmanaged, ISlot where V : unmanaged, ISlot
    //     {
    //         for (int i = 0; i < requests.Length; i++)
    //         {
                
    //             var req = requests[i]; 
                
    //             if(producerSlot.entity==req.entity) continue;
    //             if (producerSlot.slotData.Amount > 0 && req.slotData.Amount < req.slotData.Capacity)
    //             {
    //                 producerSlot.slotData.Amount--;
    //                 req.slotData.Amount++; 
                    
    //                 requests[i] = req; 
    //             }
    //             else break;
    //         }
    //     }
    //     [BurstCompile]
    //     bool IsRequestDone<T>( NativeArray<(T slotData, int slotInd, Entity entity)> map) where T : unmanaged,ISlot
    //     {
    //         foreach (var el in map)
    //         {
    //             if(el.slotData.Capacity!=el.slotData.Amount) return false;
    //         }
    //         return true;
    //     }
    //     bool IsProdecerDone<T>( NativeArray<(T slotData, int slotInd, Entity entity)> map) where T : unmanaged,ISlot
    //     {
    //         foreach (var el in map)
    //         {
    //             if(el.slotData.Amount!=0) return false;
    //         }
    //         return true;
    //     }
    //     void FillMaps<T>(ref NativeArray<Entity> entities, BufferLookup<T> lookup,ref NativeParallelMultiHashMap<int, (T slotData, int slotInd, Entity entity)> map) where T :  unmanaged,ISlot,IBufferElementData
    //     {
    //         foreach(var en in entities)
    //         {
    //             lookup.TryGetBuffer(en, out DynamicBuffer<T> buff);
    //             for(int i =0;i<buff.Length;i++)
    //             {
    //                 var sl =buff[i];
    //                 if(sl.Amount<sl.Capacity)
    //                 {
    //                     map.Add(sl.ItemId,(sl,i,en));
    //                     ids.Add(sl.ItemId);
    //                 }
    //             }
    //         }
    //     }
    //     void UpdateBuffer<T>(BufferLookup<T> lookup, NativeParallelMultiHashMap<int, (T slotData, int slotInd, Entity entity)>  map) where T : unmanaged,ISlot, IBufferElementData
    //     {
            
    //         var entites=map.GetValueArray(Allocator.Temp);
    //         for(int i = 0; i < entites.Length; i++)
    //         {
    //             lookup.TryGetBuffer(entites[i].entity,out var buff);
    //             buff[entites[i].slotInd]=entites[i].slotData;
    //         }
    //         entites.Dispose();
    //     }
    // }

    
   
    [BurstCompile]
    public struct ItemDistributionParallelJob : IJobParallelForDefer 
    {
        [ReadOnly] public NativeArray<int> ClusterIDs;

        // Глобальные карты из ClusterMap
        [ReadOnly] public NativeParallelMultiHashMap<int, Entity> ProducersMap;
        [ReadOnly] public NativeParallelMultiHashMap<int, Entity> ConsumersMap;
        [ReadOnly] public NativeParallelMultiHashMap<int, Entity> StoragesMap;
        [ReadOnly] public NativeParallelMultiHashMap<int, Entity> ExcessMap;
        [ReadOnly] public NativeParallelMultiHashMap<int, Entity> BluePrintsMap;
        [ReadOnly] public NativeParallelMultiHashMap<int, Entity> DemolitionsMap;

        // Буферы данных (разрешаем параллельную запись, так как кластеры изолированы)
        [NativeDisableParallelForRestriction] public BufferLookup<InputConstructionSlotData> InputConstruction;
        [NativeDisableParallelForRestriction] public BufferLookup<InputSlotData> InputCraft;
        [NativeDisableParallelForRestriction] public BufferLookup<StorageSlotData> Storage;
        [NativeDisableParallelForRestriction] public BufferLookup<OutputConstructionSlotData> OutputConstruction;
        [NativeDisableParallelForRestriction] public BufferLookup<OutputSlotData> OutputCraft;
        [NativeDisableParallelForRestriction] public BufferLookup<ExcessSlotData> Excess;

        public void Execute(int index)
        {
            int clusterID = ClusterIDs[index];

            // Локальные коллекции для конкретного кластера
            var constructRequests = new NativeParallelMultiHashMap<int, SlotRef<InputConstructionSlotData>>(256, Allocator.Temp);
            var craftRequests     = new NativeParallelMultiHashMap<int, SlotRef<InputSlotData>>(256, Allocator.Temp);
            var storageRequests   = new NativeParallelMultiHashMap<int, SlotRef<StorageSlotData>>(256, Allocator.Temp);

            var demolitionOutputs = new NativeParallelMultiHashMap<int, SlotRef<OutputConstructionSlotData>>(256, Allocator.Temp);
            var excessOutputs     = new NativeParallelMultiHashMap<int, SlotRef<ExcessSlotData>>(256, Allocator.Temp);
            var craftOutputs      = new NativeParallelMultiHashMap<int, SlotRef<OutputSlotData>>(256, Allocator.Temp);
            var storageOutputs    = new NativeParallelMultiHashMap<int, SlotRef<StorageSlotData>>(256, Allocator.Temp);

            var itemIds = new NativeHashSet<int>(64, Allocator.Temp);

            // Наполнение локальных данных фильтрацией по clusterID
            FillFromMap(clusterID, BluePrintsMap, InputConstruction, constructRequests, itemIds, true);
            FillFromMap(clusterID, ConsumersMap, InputCraft, craftRequests, itemIds, true);
            FillStorage(clusterID, StoragesMap, Storage, storageRequests, itemIds, true);

            FillFromMap(clusterID, DemolitionsMap, OutputConstruction, demolitionOutputs, itemIds, false);
            FillFromMap(clusterID, ExcessMap, Excess, excessOutputs, itemIds, false);
            FillFromMap(clusterID, ProducersMap, OutputCraft, craftOutputs, itemIds, false);
            FillStorage(clusterID, StoragesMap, Storage, storageOutputs, itemIds, false);

            // Распределение
            foreach (var itemId in itemIds)
            {
                Distribute(itemId, demolitionOutputs, constructRequests, craftRequests, storageRequests);
                Distribute(itemId, excessOutputs, constructRequests, craftRequests, storageRequests);
                Distribute(itemId, craftOutputs, constructRequests, craftRequests, storageRequests);
                Distribute(itemId, storageOutputs, constructRequests, craftRequests, storageRequests);
            }

            // Применение изменений
            Apply(InputConstruction, constructRequests);
            Apply(InputCraft, craftRequests);
            Apply(Storage, storageRequests);
            Apply(OutputConstruction, demolitionOutputs);
            Apply(OutputCraft, craftOutputs);
            Apply(Excess, excessOutputs);
            Apply(Storage, storageOutputs);
        }

        // ---------------- DISTRIBUTION ----------------

        void Distribute<T>(int itemId, 
            NativeParallelMultiHashMap<int, SlotRef<T>> producers,
            NativeParallelMultiHashMap<int, SlotRef<InputConstructionSlotData>> constructRequests,
            NativeParallelMultiHashMap<int, SlotRef<InputSlotData>> craftRequests,
            NativeParallelMultiHashMap<int, SlotRef<StorageSlotData>> storageRequests)
            where T : unmanaged, ISlot, IBufferElementData
        {
            if (!producers.TryGetFirstValue(itemId, out var producer, out var pit)) return;
            do
            {
                if (producer.Amount == 0) continue;
                Transfer(itemId, ref producer, constructRequests);
                Transfer(itemId, ref producer, craftRequests);
                Transfer(itemId, ref producer, storageRequests);
            } while (producers.TryGetNextValue(out producer, ref pit));
        }

        void Transfer<TP, TR>(int itemId, ref SlotRef<TP> producer, NativeParallelMultiHashMap<int, SlotRef<TR>> requests)
            where TP : unmanaged, ISlot
            where TR : unmanaged, ISlot
        {
            if (!requests.TryGetFirstValue(itemId, out var req, out var it)) return;
            do
            {
                if (producer.Amount == 0) break;
                if (req.Amount >= req.Capacity) continue;

                producer.Amount--;
                req.Amount++;
                requests.Remove(it);
                requests.Add(itemId, req);
            } while (requests.TryGetNextValue(out req, ref it));
        }

        // ---------------- FILL METHODS ----------------

        void FillFromMap<T>(int clusterID, NativeParallelMultiHashMap<int, Entity> sourceMap, BufferLookup<T> lookup, 
            NativeParallelMultiHashMap<int, SlotRef<T>> targetMap, NativeHashSet<int> itemIds, bool isInput) 
            where T : unmanaged, ISlot, IBufferElementData
        {
            if (!sourceMap.TryGetFirstValue(clusterID, out var entity, out var it)) return;
            do {
                var buffer = lookup[entity];
                for (int i = 0; i < buffer.Length; i++) {
                    var s = buffer[i];
                    if (isInput && s.Amount >= s.Capacity) continue;
                    if (!isInput && s.Amount <= 0) continue;
                    targetMap.Add(s.ItemId, new SlotRef<T>(entity, i, s));
                    itemIds.Add(s.ItemId);
                }
            } while (sourceMap.TryGetNextValue(out entity, ref it));
        }

        void FillStorage(int clusterID, NativeParallelMultiHashMap<int, Entity> sourceMap, BufferLookup<StorageSlotData> lookup,
            NativeParallelMultiHashMap<int, SlotRef<StorageSlotData>> targetMap, NativeHashSet<int> itemIds, bool isInput)
        {
            if (!sourceMap.TryGetFirstValue(clusterID, out var entity, out var it)) return;
            do {
                var buffer = lookup[entity];
                for (int i = 0; i < buffer.Length; i++) {
                    var s = buffer[i];
                    if (isInput) {
                        if (!s.IsInputEnabled || s.Amount >= s.Capacity) continue;
                    } else {
                        if (!s.IsOutputEnabled || s.Amount <= 0) continue;
                    }
                    targetMap.Add(s.ItemId, new SlotRef<StorageSlotData>(entity, i, s));
                    itemIds.Add(s.ItemId);
                }
            } while (sourceMap.TryGetNextValue(out entity, ref it));
        }

        void Apply<T>(BufferLookup<T> lookup, NativeParallelMultiHashMap<int, SlotRef<T>> map)
            where T : unmanaged, IBufferElementData, ISlot
        {
            using var values = map.GetValueArray(Allocator.Temp);
            for (int i = 0; i < values.Length; i++) 
            {
                var v = values[i];
                var buffer = lookup[v.Entity];
                buffer[v.Index] = v.Data;
            }
        }

        // ---------------- HELPER STRUCT ----------------
        struct SlotRef<T> where T : unmanaged, ISlot
        {
            public Entity Entity;
            public int Index;
            public T Data;
            public int Amount { get => Data.Amount; set { var d = Data; d.Amount = value; Data = d; } }
            public int Capacity => Data.Capacity;
            public SlotRef(Entity entity, int index, T data) { Entity = entity; Index = index; Data = data; }
        }
    }



}