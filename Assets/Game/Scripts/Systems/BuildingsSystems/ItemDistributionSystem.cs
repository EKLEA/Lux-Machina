using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[BurstCompile]
public partial struct ItemDistributionSystem : ISystem
{
    EntityQuery _mapUpdateQuery;
    EntityQuery _consumersQuery;
    EntityQuery _producersQuery;
    EntityQuery _unnecessaryItemsQuery;
    EntityQuery _recipeProcessingQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _mapUpdateQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BuildingMap, UpdateMapTag>()
            .Build(ref state);

        _consumersQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ClusterId, HasInputSlots, CanResoucesBeAddedTag, SlotData, BuildingWorkWithItemsLogicData>()
            .Build(ref state);

        _producersQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ClusterId, HasOutputSlots, CanResoucesBeRemovedTag, SlotData>()
            .Build(ref state);

        _unnecessaryItemsQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ClusterId, DistribureRemovedItems, SlotData>()
            .Build(ref state);

        _recipeProcessingQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ProcessBuildingData, ConnectedToEnegry, SlotData>()
            .WithAny<HasInputSlots, HasOutputSlots>()
            .Build(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<RecipeCache>(out var recipeCache))
            return;

        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        
        var processRecipesJob = new ProcessRecipesJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            RecipeCache = recipeCache.Recipes,
            Ecb = ecb.AsParallelWriter()
        };
        state.Dependency = processRecipesJob.Schedule(_recipeProcessingQuery, state.Dependency);

        
        if (!_mapUpdateQuery.IsEmptyIgnoreFilter)
        {
            state.Dependency.Complete();

            
            var clusters = new NativeHashSet<int>(100, Allocator.Temp);
            foreach (var cluster in SystemAPI.Query<RefRO<ClusterId>>().WithAll<BuildingWorkWithItemsLogicData>())
            {
                clusters.Add(cluster.ValueRO.Value);
            }

            
            foreach (int clusterId in clusters)
            {
                ProcessClusterSync(ref state, clusterId, ecb);
            }

            clusters.Dispose();
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    [BurstCompile]
    void ProcessClusterSync(ref SystemState state, int clusterId, EntityCommandBuffer ecb)
    {
        var consumers = GetConsumersInClusterSync(ref state, clusterId);
        if (consumers.Length == 0)
            return;

        var producers = GetProducersInClusterSync(ref state, clusterId);
        var unnecessaryItems = GetUnnecessaryItemsInClusterSync(ref state, clusterId);

        DistributeResourcesSync(ref state, consumers, producers, unnecessaryItems, ecb);

        consumers.Dispose();
        producers.Dispose();
        unnecessaryItems.Dispose();
    }

    [BurstCompile]
    NativeList<Entity> GetConsumersInClusterSync(ref SystemState state, int clusterId)
    {
        var consumers = new NativeList<Entity>(Allocator.Temp);

        foreach (var (cluster, entity) in SystemAPI.Query<RefRO<ClusterId>>()
            .WithAll<HasInputSlots, BuildingWorkWithItemsLogicData, SlotData>()
            .WithAll<CanResoucesBeAddedTag>()
            .WithEntityAccess())
        {
            if (cluster.ValueRO.Value == clusterId)
                consumers.Add(entity);
        }

        return SortConsumersByPrioritySync(ref state, consumers);
    }

    [BurstCompile]
    NativeList<Entity> GetProducersInClusterSync(ref SystemState state, int clusterId)
    {
        var producers = new NativeList<Entity>(Allocator.Temp);

        foreach (var (cluster, entity) in SystemAPI.Query<RefRO<ClusterId>>()
            .WithAll<HasOutputSlots, CanResoucesBeRemovedTag, SlotData>()
            .WithEntityAccess())
        {
            if (cluster.ValueRO.Value == clusterId)
                producers.Add(entity);
        }

        return producers;
    }
    
    [BurstCompile]
    NativeList<Entity> GetUnnecessaryItemsInClusterSync(ref SystemState state, int clusterId)
    {
        var entities = new NativeList<Entity>(Allocator.Temp);

        foreach (var (cluster, entity) in SystemAPI.Query<RefRO<ClusterId>>()
            .WithAll<DistribureRemovedItems, SlotData>()
            .WithEntityAccess())
        {
            if (cluster.ValueRO.Value == clusterId)
                entities.Add(entity);
        }

        return entities;
    }

    [BurstCompile]
    NativeList<Entity> SortConsumersByPrioritySync(ref SystemState state, NativeList<Entity> consumers)
    {
        if (consumers.Length <= 1)
            return consumers;

        var priorityBuckets = new NativeArray<NativeList<Entity>>(5, Allocator.Temp);
        for (int i = 0; i < priorityBuckets.Length; i++)
            priorityBuckets[i] = new NativeList<Entity>(Allocator.Temp);

        foreach (var consumer in consumers)
        {
            var priority = state.EntityManager.GetComponentData<BuildingWorkWithItemsLogicData>(consumer).Priority;
            int bucketIndex = math.clamp(priority, 0, priorityBuckets.Length - 1);
            priorityBuckets[bucketIndex].Add(consumer);
        }

        var sortedConsumers = new NativeList<Entity>(consumers.Length, Allocator.Temp);
        for (int i = priorityBuckets.Length - 1; i >= 0; i--)
        {
            sortedConsumers.AddRange(priorityBuckets[i].AsArray());
            priorityBuckets[i].Dispose();
        }

        priorityBuckets.Dispose();
        consumers.Dispose();
        return sortedConsumers;
    }

    [BurstCompile]
    void DistributeResourcesSync(
        ref SystemState state,
        NativeList<Entity> consumers,
        NativeList<Entity> producers,
        NativeList<Entity> unnecessaryItems,
        EntityCommandBuffer ecb
    )
    {
        
        foreach (var rem in unnecessaryItems)
        {
            var outputBuffer = state.EntityManager.GetBuffer<SlotData>(rem);
            var dr = state.EntityManager.GetComponentData<DistribureRemovedItems>(rem);

            for (int slotIndex = dr.StartIND; slotIndex < dr.EndIND; slotIndex++)
            {
                var outputSlot = outputBuffer[slotIndex];
                if (outputSlot.Count <= 0) continue;

                int remainingItems = outputSlot.Count;
                foreach (var consumer in consumers)
                {
                    if (remainingItems <= 0) break;
                    remainingItems = TransferItemsToConsumerSync(ref state, consumer, outputSlot.ItemId, remainingItems);
                }
                outputSlot.Count = remainingItems;
                outputBuffer[slotIndex] = outputSlot;
            }
        }

        
        foreach (var producer in producers)
        {
            var outputBuffer = state.EntityManager.GetBuffer<SlotData>(producer);
            var od = state.EntityManager.GetComponentData<HasOutputSlots>(producer);

            for (int slotIndex = od.StartIND; slotIndex < od.EndIND; slotIndex++)
            {
                var outputSlot = outputBuffer[slotIndex];
                if (outputSlot.Count <= 0) continue;

                int remainingItems = outputSlot.Count;
                foreach (var consumer in consumers)
                {
                    if (remainingItems <= 0) break;
                    remainingItems = TransferItemsToConsumerSync(ref state, consumer, outputSlot.ItemId, remainingItems);
                }
                outputSlot.Count = remainingItems;
                outputBuffer[slotIndex] = outputSlot;
            }
        }
    }

    [BurstCompile]
    int TransferItemsToConsumerSync(ref SystemState state, Entity consumer, int itemId, int availableItems)
    {
        var inputBuffer = state.EntityManager.GetBuffer<SlotData>(consumer);
        var id = state.EntityManager.GetComponentData<HasInputSlots>(consumer);

        for (int i = id.StartIND; i < id.EndIND; i++)
        {
            if (availableItems <= 0) break;

            var inputSlot = inputBuffer[i];
            if (inputSlot.ItemId == 0 || inputSlot.ItemId == itemId)
            {
                if (inputSlot.ItemId == 0)
                {
                    inputSlot.ItemId = itemId;
                }

                int freeSpace = inputSlot.Capacity - inputSlot.Count;
                if (freeSpace > 0)
                {
                    int itemsToTransfer = math.min(availableItems, freeSpace);
                    inputSlot.Count += itemsToTransfer;
                    availableItems -= itemsToTransfer;
                    inputBuffer[i] = inputSlot;
                }
            }
        }
        return availableItems;
    }

    [BurstCompile]
    partial struct ProcessRecipesJob : IJobEntity
    {
        public float DeltaTime;
        [ReadOnly] public NativeHashMap<int, BurstRecipe> RecipeCache;
        public EntityCommandBuffer.ParallelWriter Ecb;

       void Execute(Entity entity, [EntityIndexInQuery] int entityIndex, ref ProcessBuildingData processData, 
           in DynamicBuffer<SlotData> slotBuff, in HasInputSlots hasInput, in HasOutputSlots hasOutput)
        {
            if (processData.RecipeIDHash != 0 && RecipeCache.TryGetValue(processData.RecipeIDHash, out var recipe))
            {
                if (CanCraftRecipe(entity, slotBuff, recipe, hasInput, hasOutput, entityIndex))
                {
                    processData.CurrTime += DeltaTime;

                    if (processData.CurrTime >= processData.TimeToProduceNext)
                    {
                        CraftRecipe(entity, slotBuff, recipe, hasInput, hasOutput, Ecb, entityIndex);
                        processData.CurrTime = 0;
                    }
                }
            }
        }

        bool CanCraftRecipe(Entity entity, in DynamicBuffer<SlotData> slotBuff, BurstRecipe recipe, 
                        in HasInputSlots hasInput, in HasOutputSlots hasOutput, int entityIndex)
        {
            
            if (slotBuff.Length > hasInput.EndIND)
            {
                for (int i = hasInput.StartIND; i < hasInput.EndIND; i++)
                    if (slotBuff[i].Count < recipe.InputItems[i - hasInput.StartIND].Amount)
                        return false;
            }

            
            if (slotBuff.Length > hasOutput.EndIND)
            {
                for (int i = hasOutput.StartIND; i < hasOutput.EndIND; i++)
                    if (slotBuff[i].Capacity - slotBuff[i].Count < recipe.OutputItems[i - hasOutput.StartIND].Amount)
                        return false;
            }

            Ecb.AddComponent<CanAnimateTag>(entityIndex, entity);
            return true;
        }

        void CraftRecipe(Entity entity, in DynamicBuffer<SlotData> slotBuff, BurstRecipe recipe, 
                        in HasInputSlots hasInput, in HasOutputSlots hasOutput, 
                        EntityCommandBuffer.ParallelWriter ecb, int entityIndex)
        {
            
            
            if (slotBuff.Length > hasInput.EndIND)
            {
                for (int i = hasInput.StartIND; i < hasInput.EndIND; i++)
                {
                    var slotIndex = i;
                    var amount = recipe.InputItems[i - hasInput.StartIND].Amount;
                    ecb.AppendToBuffer(entityIndex, entity, new SlotModification 
                    { 
                        SlotIndex = slotIndex,
                        ItemId = -1, 
                        Amount = amount
                    });
                }
            }

            
            if (slotBuff.Length > hasOutput.EndIND)
            {
                for (int i = hasOutput.StartIND; i < hasOutput.EndIND; i++)
                {
                    var slotIndex = i;
                    var amount = recipe.OutputItems[i - hasOutput.StartIND].Amount;
                    ecb.AppendToBuffer(entityIndex, entity, new SlotModification 
                    { 
                        SlotIndex = slotIndex,
                        ItemId = recipe.OutputItems[i - hasOutput.StartIND].ItemId,
                        Amount = amount
                    });
                }
            }
        }
    }
}

public struct SlotModification : IBufferElementData
{
    public int SlotIndex;
    public int ItemId; 
    public int Amount;
}