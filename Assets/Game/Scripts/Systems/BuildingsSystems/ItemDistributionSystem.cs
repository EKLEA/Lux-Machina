using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[BurstCompile]
public partial struct ItemDistributionSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<RecipeCache>(out var recipeCache))
            return;

        ProduceResources(ref state, recipeCache.Recipes);

        ProcessRecipes(ref state, recipeCache.Recipes);

        DistributeResourcesByClusters(ref state);
    }

    [BurstCompile]
    private void ProduceResources(
        ref SystemState state,
        NativeHashMap<int, BurstRecipe> recipeCache
    )
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        foreach (
            var (producerData, logicData, outputBuffer, entity) in SystemAPI
                .Query<
                    RefRW<ProducerBuildingData>,
                    RefRO<BuildingLogicData>,
                    DynamicBuffer<OutputStorageSlotData>
                >()
                .WithAll<ProducerBuildingTag>()
                .WithEntityAccess()
        )
        {
            producerData.ValueRW.CurrTime += SystemAPI.Time.DeltaTime;

            if (producerData.ValueRW.CurrTime >= producerData.ValueRW.TimeToProduceNext)
            {
                producerData.ValueRW.CurrTime = 0;

                if (
                    logicData.ValueRO.RecipeIDHash != 0
                    && recipeCache.TryGetValue(logicData.ValueRO.RecipeIDHash, out var recipe)
                )
                {
                    ProduceFromRecipe(outputBuffer, recipe);
                }

                ecb.SetComponent(entity, producerData.ValueRO);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    [BurstCompile]
    private void ProcessRecipes(ref SystemState state, NativeHashMap<int, BurstRecipe> recipeCache)
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        foreach (
            var (producerData, logicData, inputBuffer, outputBuffer, entity) in SystemAPI
                .Query<
                    RefRW<ProducerBuildingData>,
                    RefRO<BuildingLogicData>,
                    DynamicBuffer<InnerStorageSlotData>,
                    DynamicBuffer<OutputStorageSlotData>
                >()
                .WithAll<ProcessorBuildingTag>()
                .WithEntityAccess()
        )
        {
            producerData.ValueRW.CurrTime += SystemAPI.Time.DeltaTime;

            if (producerData.ValueRW.CurrTime >= producerData.ValueRW.TimeToProduceNext)
            {
                producerData.ValueRW.CurrTime = 0;

                if (
                    logicData.ValueRO.RecipeIDHash != 0
                    && recipeCache.TryGetValue(logicData.ValueRO.RecipeIDHash, out var recipe)
                )
                {
                    if (CanCraftRecipe(inputBuffer, recipe))
                    {
                        CraftRecipe(inputBuffer, outputBuffer, recipe);
                    }
                }

                ecb.SetComponent(entity, producerData.ValueRO);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    [BurstCompile]
    private void DistributeResourcesByClusters(ref SystemState state)
    {
        var clusters = GetUniqueClusters(ref state);

        foreach (int clusterId in clusters)
        {
            ProcessCluster(ref state, clusterId);
        }

        clusters.Dispose();
    }

    [BurstCompile]
    private NativeHashSet<int> GetUniqueClusters(ref SystemState state)
    {
        var clusters = new NativeHashSet<int>(100, Allocator.Temp);

        foreach (
            var cluster in SystemAPI
                .Query<RefRO<ClusterId>>()
                .WithAll<InnerStorageSlotData>()
                .WithAny<ConsumerBuildingTag, ProcessorBuildingTag>()
        )
        {
            clusters.Add(cluster.ValueRO.Value);
        }

        return clusters;
    }

    [BurstCompile]
    private void ProcessCluster(ref SystemState state, int clusterId)
    {
        var consumers = GetConsumersInCluster(ref state, clusterId);
        if (consumers.Length == 0)
            return;

        var producers = GetProducersInCluster(ref state, clusterId);
        if (producers.Length == 0)
            return;

        DistributeResources(ref state, producers, consumers);

        consumers.Dispose();
        producers.Dispose();
    }

    [BurstCompile]
    private NativeList<Entity> GetConsumersInCluster(ref SystemState state, int clusterId)
    {
        var consumers = new NativeList<Entity>(Allocator.Temp);

        foreach (
            var (cluster, buffer, entity) in SystemAPI
                .Query<RefRO<ClusterId>, DynamicBuffer<InnerStorageSlotData>>()
                .WithAny<ConsumerBuildingTag, ProcessorBuildingTag>()
                .WithEntityAccess()
        )
        {
            if (cluster.ValueRO.Value == clusterId)
                consumers.Add(entity);
        }

        return SortConsumersByPriority(ref state, consumers);
    }

    [BurstCompile]
    private NativeList<Entity> GetProducersInCluster(ref SystemState state, int clusterId)
    {
        var producers = new NativeList<Entity>(Allocator.Temp);

        foreach (
            var (cluster, buffer, entity) in SystemAPI
                .Query<RefRO<ClusterId>, DynamicBuffer<OutputStorageSlotData>>()
                .WithAny<ProducerBuildingTag, ProcessorBuildingTag>()
                .WithEntityAccess()
        )
        {
            if (cluster.ValueRO.Value == clusterId)
                producers.Add(entity);
        }

        return producers;
    }

    [BurstCompile]
    private NativeList<Entity> SortConsumersByPriority(
        ref SystemState state,
        NativeList<Entity> consumers
    )
    {
        if (consumers.Length <= 1)
            return consumers;

        var priorityBuckets = new NativeArray<NativeList<Entity>>(5, Allocator.Temp);

        for (int i = 0; i < priorityBuckets.Length; i++)
        {
            priorityBuckets[i] = new NativeList<Entity>(Allocator.Temp);
        }

        foreach (var consumer in consumers)
        {
            var priority = SystemAPI.GetComponent<BuildingLogicData>(consumer).Priority;
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
    private void DistributeResources(
        ref SystemState state,
        NativeList<Entity> producers,
        NativeList<Entity> consumers
    )
    {
        foreach (var producer in producers)
        {
            var outputBuffer = SystemAPI.GetBuffer<OutputStorageSlotData>(producer);

            for (int slotIndex = 0; slotIndex < outputBuffer.Length; slotIndex++)
            {
                var outputSlot = outputBuffer[slotIndex];
                if (outputSlot.Count <= 0)
                    continue;

                int remainingItems = outputSlot.Count;

                foreach (var consumer in consumers)
                {
                    if (remainingItems <= 0)
                        break;

                    var inputBuffer = SystemAPI.GetBuffer<InnerStorageSlotData>(consumer);
                    remainingItems = TransferItemsToConsumer(
                        inputBuffer,
                        outputSlot.ItemId,
                        remainingItems
                    );
                }

                outputSlot.Count = remainingItems;
                outputBuffer[slotIndex] = outputSlot;
            }
        }
    }

    [BurstCompile]
    private int TransferItemsToConsumer(
        DynamicBuffer<InnerStorageSlotData> inputBuffer,
        int itemId,
        int availableItems
    )
    {
        for (int i = 0; i < inputBuffer.Length; i++)
        {
            if (availableItems <= 0)
                break;

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
    private void ProduceFromRecipe(
        DynamicBuffer<OutputStorageSlotData> outputBuffer,
        BurstRecipe recipe
    )
    {
        for (int i = 0; i < recipe.OutputItems.Length; i++)
        {
            var outputItem = recipe.OutputItems[i];
            AddItemsToOutput(outputBuffer, outputItem.ItemId, outputItem.Amount);
        }
    }

    [BurstCompile]
    private bool CanCraftRecipe(DynamicBuffer<InnerStorageSlotData> inputBuffer, BurstRecipe recipe)
    {
        for (int i = 0; i < recipe.InputItems.Length; i++)
        {
            var inputItem = recipe.InputItems[i];
            int totalAvailable = 0;

            foreach (var slot in inputBuffer)
            {
                if (slot.ItemId == inputItem.ItemId)
                    totalAvailable += slot.Count;
            }

            if (totalAvailable < inputItem.Amount)
                return false;
        }
        return true;
    }

    [BurstCompile]
    private void CraftRecipe(
        DynamicBuffer<InnerStorageSlotData> inputBuffer,
        DynamicBuffer<OutputStorageSlotData> outputBuffer,
        BurstRecipe recipe
    )
    {
        for (int i = 0; i < recipe.InputItems.Length; i++)
        {
            var inputItem = recipe.InputItems[i];
            RemoveItemsFromInput(inputBuffer, inputItem.ItemId, inputItem.Amount);
        }

        for (int i = 0; i < recipe.OutputItems.Length; i++)
        {
            var outputItem = recipe.OutputItems[i];
            AddItemsToOutput(outputBuffer, outputItem.ItemId, outputItem.Amount);
        }
    }

    [BurstCompile]
    private void RemoveItemsFromInput(
        DynamicBuffer<InnerStorageSlotData> inputBuffer,
        int itemId,
        int amount
    )
    {
        for (int i = 0; i < inputBuffer.Length && amount > 0; i++)
        {
            var slot = inputBuffer[i];
            if (slot.ItemId == itemId)
            {
                int itemsToRemove = math.min(amount, slot.Count);
                slot.Count -= itemsToRemove;
                amount -= itemsToRemove;

                if (slot.Count == 0)
                    slot.ItemId = 0;

                inputBuffer[i] = slot;
            }
        }
    }

    [BurstCompile]
    private void AddItemsToOutput(
        DynamicBuffer<OutputStorageSlotData> outputBuffer,
        int itemId,
        int amount
    )
    {
        for (int i = 0; i < outputBuffer.Length; i++)
        {
            var slot = outputBuffer[i];
            if (slot.ItemId == itemId)
            {
                int freeSpace = slot.Capacity - slot.Count;
                int itemsToAdd = math.min(amount, freeSpace);
                slot.Count += itemsToAdd;
                outputBuffer[i] = slot;
                amount -= itemsToAdd;
                if (amount <= 0)
                    break;
            }
        }

        if (amount > 0)
        {
            for (int i = 0; i < outputBuffer.Length && amount > 0; i++)
            {
                var slot = outputBuffer[i];
                if (slot.ItemId == 0)
                {
                    slot.ItemId = itemId;
                    int itemsToAdd = math.min(amount, slot.Capacity);
                    slot.Count = itemsToAdd;
                    outputBuffer[i] = slot;
                    amount -= itemsToAdd;
                }
            }
        }
    }
}
