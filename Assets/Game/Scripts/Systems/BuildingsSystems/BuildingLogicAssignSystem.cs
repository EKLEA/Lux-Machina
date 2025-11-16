using System;
using Unity.Collections;
using Unity.Entities;
using Zenject;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RoadSystem))]
public partial class BuildingLogicAssignSystem : SystemBase
{
    [Inject]
    IReadOnlyBuildingInfo buildingInfo;

    [Inject]
    IReadOnlyRecipeInfo recipeInfo;

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        
        foreach (var (buildingLogicData, entity) in SystemAPI
            .Query<BuildingData>()
            .WithAll<AssignLogicTag>()
            .WithNone<HasInputSlots, HasOutputSlots>()
            .WithEntityAccess())
        {
            AssignLogic(entity, buildingLogicData, ecb);
        }

        foreach (var (buildingData, recipe, entity) in SystemAPI
            .Query<BuildingWorkWithItemsLogicData, ChangeRecipeData>()
            .WithAll<ProcessBuildingData>()
            .WithAny<HasInputSlots, HasOutputSlots>()
            .WithEntityAccess())
        {
            ChangeRecipe(entity, buildingData, recipe, ecb);
        }

        foreach (var (changeSlotData, entity) in SystemAPI
            .Query<ChangeSlotCapacityData>()
            .WithAll<BuildingWorkWithItemsLogicData>()
            .WithAny<HasInputSlots, HasOutputSlots>()
            .WithEntityAccess())
        {
            ChangeSlotCapacity(entity, changeSlotData);
        }

        foreach (var (data, entity) in SystemAPI
            .Query<ChangePriorityData>()
            .WithAll<BuildingWorkWithItemsLogicData>()
            .WithAny<HasInputSlots, HasOutputSlots>()
            .WithEntityAccess())
        {
            ChangePriority(entity, data, ecb);
        }
        foreach (var (data, entity) in SystemAPI
            .Query<ChangeBuildingCountOfPackData>()
            .WithAll<BuildingWorkWithItemsLogicData>()
            .WithAny<HasInputSlots, HasOutputSlots>()
            .WithEntityAccess())
        {
            ChangeBuildingCountOfPack(entity, data, ecb);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    void AssignLogic(Entity entity, BuildingData buildingData, EntityCommandBuffer ecb)
    {
        var info = buildingInfo.BuildingInfos[buildingData.BuildingIDHash];
        if (info.typeOfLogic != TypeOfLogic.None)
        {
            switch (info.typeOfLogic)
            {
                case TypeOfLogic.WorkWithItems:
                    ecb.AddComponent(entity, new BuildingWorkWithItemsLogicData
                    {
                        Priority = (int)DistributionPriority.Middle,
                        RequiredRecipesGroup = (int)info.requiredRecipesGroup,
                        CountOfPack = 5
                    });
                    break;
            }
            ecb.AddComponent(entity, new AnimationData
            {
                AnimationProgress = 0,
                AnimationState = (int)BuildingAnimationState.Disconnected,
            });
            ecb.RemoveComponent<AssignLogicTag>(entity);
        }
    }

    void ChangeRecipe(
        Entity entity,
        BuildingWorkWithItemsLogicData buildingData,
        ChangeRecipeData recipeData,
        EntityCommandBuffer ecb)
    {
        if (recipeInfo.RecipeInfos.TryGetValue(recipeData.newRecipeID, out RecipeConfig recipe) 
            && recipe.groupId == buildingData.RequiredRecipesGroup)
        {
            using (var removedItems = new NativeHashMap<int, SlotData>(1000, Allocator.Temp))
            {
                if (EntityManager.HasBuffer<SlotData>(entity))
                {
                    var buff = EntityManager.GetBuffer<SlotData>(entity);
                    CollectItemsFromSlots(entity, buff, removedItems);
                }

                ProcessBuildingData newData = new()
                {
                    RecipeIDHash = recipeData.newRecipeID,
                    TimeToProduceNext = recipe.craftTime,
                    CurrTime = 0
                };

                if (EntityManager.HasBuffer<SlotData>(entity))
                    ecb.RemoveComponent<SlotData>(entity);
                if (EntityManager.HasComponent<HasInputSlots>(entity))
                    ecb.RemoveComponent<HasInputSlots>(entity);
                if (EntityManager.HasComponent<HasOutputSlots>(entity))
                    ecb.RemoveComponent<HasOutputSlots>(entity);
                if (EntityManager.HasComponent<DistribureRemovedItems>(entity))
                    ecb.RemoveComponent<DistribureRemovedItems>(entity);

                var newBuff = ecb.AddBuffer<SlotData>(entity);
                int currentIndex = 0;

                if (recipe.inputItems.Count > 0)
                    currentIndex = CreateInputSlots(recipe, buildingData, removedItems, newBuff, currentIndex, ecb, entity);

                if (recipe.outputItems.Count > 0)
                    currentIndex = CreateOutputSlots(recipe, buildingData, removedItems, newBuff, currentIndex, ecb, entity);

                if (!removedItems.IsEmpty)
                    CreateRemovedItemsSlots(removedItems, newBuff, currentIndex, ecb, entity);
                
                ecb.SetComponent(entity, newData);
            }
            ecb.RemoveComponent<ChangeRecipeData>(entity);
        }
    }

    private void CollectItemsFromSlots(Entity entity, DynamicBuffer<SlotData> buff, NativeHashMap<int, SlotData> removedItems)
    {
        if (EntityManager.HasComponent<HasInputSlots>(entity))
        {
            var hIn = EntityManager.GetComponentData<HasInputSlots>(entity);
            CollectFromRange(buff, hIn.StartIND, hIn.EndIND, removedItems);
        }

        if (EntityManager.HasComponent<HasOutputSlots>(entity))
        {
            var hOut = EntityManager.GetComponentData<HasOutputSlots>(entity);
            CollectFromRange(buff, hOut.StartIND, hOut.EndIND, removedItems);
        }

        if (EntityManager.HasComponent<DistribureRemovedItems>(entity))
        {
            var dist = EntityManager.GetComponentData<DistribureRemovedItems>(entity);
            CollectFromRange(buff, dist.StartIND, dist.EndIND, removedItems);
        }
    }

    private void CollectFromRange(DynamicBuffer<SlotData> buff, int start, int end, NativeHashMap<int, SlotData> removedItems)
    {
        for (int i = start; i < end; i++)
        {
            if (buff[i].Count > 0)
            {
                int itemId = buff[i].ItemId;
                if (removedItems.TryGetValue(itemId, out SlotData data))
                {
                    data.Count += buff[i].Count;
                    removedItems[itemId] = data;
                }
                else
                {
                    removedItems.Add(itemId, buff[i]);
                }
            }
        }
    }

    private int CreateInputSlots(
        RecipeConfig recipe,
        BuildingWorkWithItemsLogicData buildingData,
        NativeHashMap<int, SlotData> removedItems,
        DynamicBuffer<SlotData> newBuff,
        int startIndex,
        EntityCommandBuffer ecb,
        Entity entity)
    {
        var inputSlots = new HasInputSlots
        {
            StartIND = startIndex,
            EndIND = startIndex + recipe.inputItems.Count
        };

        for (int i = 0; i < recipe.inputItems.Count; i++)
        {
            var inputItem = recipe.inputItems[i];
            int count = 0;
            
            if (removedItems.TryGetValue(inputItem.itemId, out SlotData existingItem))
            {
                count = existingItem.Count;
                removedItems.Remove(inputItem.itemId);
            }

            newBuff.Add(new SlotData
            {
                ItemId = inputItem.itemId,
                Capacity = inputItem.amount * buildingData.CountOfPack,
                Count = count
            });
        }

        ecb.AddComponent(entity, inputSlots);
        return inputSlots.EndIND;
    }

    private int CreateOutputSlots(
        RecipeConfig recipe,
        BuildingWorkWithItemsLogicData buildingData,
        NativeHashMap<int, SlotData> removedItems,
        DynamicBuffer<SlotData> newBuff,
        int startIndex,
        EntityCommandBuffer ecb,
        Entity entity)
    {
        var outputSlots = new HasOutputSlots
        {
            StartIND = startIndex,
            EndIND = startIndex + recipe.outputItems.Count
        };

        for (int i = 0; i < recipe.outputItems.Count; i++)
        {
            var outputItem = recipe.outputItems[i];
            int count = 0;
            
            if (removedItems.TryGetValue(outputItem.itemId, out SlotData existingItem))
            {
                count = existingItem.Count;
                removedItems.Remove(outputItem.itemId);
            }

            newBuff.Add(new SlotData
            {
                ItemId = outputItem.itemId,
                Capacity = outputItem.amount * buildingData.CountOfPack,
                Count = count
            });
        }

        ecb.AddComponent(entity, outputSlots);
        return outputSlots.EndIND;
    }

    private void CreateRemovedItemsSlots(
        NativeHashMap<int, SlotData> removedItems,
        DynamicBuffer<SlotData> newBuff,
        int startIndex,
        EntityCommandBuffer ecb,
        Entity entity)
    {
        int count = 0;
        foreach (var item in removedItems)
        {
            newBuff.Add(new SlotData
            {
                ItemId = item.Value.ItemId,
                Capacity = 999,
                Count = item.Value.Count
            });
            count++;
        }

        ecb.AddComponent(entity, new DistribureRemovedItems
        {
            StartIND = startIndex,
            EndIND = startIndex + count
        });
    }

    void ChangeSlotCapacity(Entity entity, ChangeSlotCapacityData changeSlotCapacityData)
    {
        if (EntityManager.HasBuffer<SlotData>(entity))
        {
            var bf = EntityManager.GetBuffer<SlotData>(entity);
            if (changeSlotCapacityData.SlotIND >= 0 && changeSlotCapacityData.SlotIND < bf.Length)
            {
                var slot = bf[changeSlotCapacityData.SlotIND];
                slot.Capacity = changeSlotCapacityData.newCapacity;
                bf[changeSlotCapacityData.SlotIND] = slot;
            }
        }
    }

    void ChangePriority(Entity entity, ChangePriorityData priorityData, EntityCommandBuffer ecb)
    {
        if (Enum.IsDefined(typeof(DistributionPriority), priorityData.newPriorityID))
        {
            var data = EntityManager.GetComponentData<BuildingWorkWithItemsLogicData>(entity);
            data.Priority = priorityData.newPriorityID;
            ecb.SetComponent(entity, data);
        }
    }
    void ChangeBuildingCountOfPack(Entity entity, ChangeBuildingCountOfPackData countOfPackDataData, EntityCommandBuffer ecb)
    {
        if (countOfPackDataData.newCountOfPack>1)
        {
            var data = EntityManager.GetComponentData<BuildingWorkWithItemsLogicData>(entity);
            data.CountOfPack = countOfPackDataData.newCountOfPack;
            ecb.SetComponent(entity, data);
        }
    }
}