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
        foreach (
            var (buildingLogicData, entity) in SystemAPI
                .Query<BuildingData>()
                .WithAll<AssignLogicTag>()
                .WithNone<ConsumerBuildingTag, ProducerBuildingTag, ProcessorBuildingTag>()
                .WithEntityAccess()
        )
        {
            AssignLogic(entity, buildingLogicData, ecb);
        }
        ;

        foreach (
            var (buildingLogicData, recipe, entity) in SystemAPI
                .Query<BuildingLogicData, ChangeRecipeData>()
                .WithAny<ConsumerBuildingTag, ProducerBuildingTag, ProcessorBuildingTag>()
                .WithEntityAccess()
        )
        {
            ChangeRecipe(entity, buildingLogicData, recipe, ecb);
        }
        ;
        foreach (
            var (changeSlotData, entity) in SystemAPI
                .Query<ChangeInnerSlotCapacityData>()
                .WithAll<BuildingLogicData>()
                .WithAny<ConsumerBuildingTag, ProcessorBuildingTag>()
                .WithEntityAccess()
        )
        {
            ChangeInnerSlotCapacity(entity, changeSlotData);
        }
        ;
        foreach (
            var (changeSlotData, entity) in SystemAPI
                .Query<ChangeOutputSlotCapacityData>()
                .WithAll<BuildingLogicData>()
                .WithAny<ProducerBuildingTag, ProcessorBuildingTag>()
                .WithEntityAccess()
        )
        {
            ChangeOutputSlotCapacity(entity, changeSlotData);
        }
        ;
        foreach (
            var (buildingLogicData, data, entity) in SystemAPI
                .Query<BuildingLogicData, ChangePriorityData>()
                .WithAll<BuildingLogicData>()
                .WithAny<ConsumerBuildingTag, ProducerBuildingTag, ProcessorBuildingTag>()
                .WithEntityAccess()
        )
        {
            ChangePriority(entity, buildingLogicData, data, ecb);
        }
        ;

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    void AssignLogic(Entity entity, BuildingData buildingData, EntityCommandBuffer ecb)
    {
        var info = buildingInfo.BuildingInfos[buildingData.BuildingIDHash];
        if (!(info.typeOfLogic == TypeOfLogic.None))
        {
            ecb.AddComponent(
                entity,
                new BuildingLogicData
                {
                    Priority = (int)DistributionPriority.Middle,
                    RecipeIDHash = 0,
                    RequiredRecipesGroup = (int)info.requiredRecipesGroup,
                }
            );

            ecb.AddComponent(
                entity,
                new AnimationData
                {
                    AnimationProgress = 0,
                    AnimationState = (int)BuildingAnimationState.Disconnected,
                }
            );
            switch (info.typeOfLogic)
            {
                case TypeOfLogic.Procession:
                    ecb.AddComponent<ProcessorBuildingTag>(entity);
                    break;
                case TypeOfLogic.Production:
                    ecb.AddComponent<ProducerBuildingTag>(entity);
                    break;
                case TypeOfLogic.Consuming:
                    ecb.AddComponent<ConsumerBuildingTag>(entity);
                    break;
            }
            ecb.RemoveComponent<AssignLogicTag>(entity);
        }
    }

    void ChangeRecipe(
        Entity entity,
        BuildingLogicData buildingData,
        ChangeRecipeData recipeData,
        EntityCommandBuffer ecb
    )
    {
        if (
            recipeInfo.RecipeInfos.ContainsKey(recipeData.newRecipeID)
            && recipeInfo.RecipeInfos[recipeData.newRecipeID].groupId
                == buildingData.RequiredRecipesGroup
        )
        {
            var recipe = recipeInfo.RecipeInfos[recipeData.newRecipeID];
            BuildingLogicData newData = new BuildingLogicData
            {
                Priority = buildingData.Priority,
                RequiredRecipesGroup = buildingData.RequiredRecipesGroup,
                RecipeIDHash = recipeData.newRecipeID,
            };
            ecb.SetComponent(entity, newData);
            if (
                EntityManager.HasComponent<ConsumerBuildingTag>(entity)
                || EntityManager.HasComponent<ProcessorBuildingTag>(entity)
            )
            {
                if (EntityManager.HasBuffer<InnerStorageSlotData>(entity))
                {
                    var rBuff = EntityManager.GetBuffer<InnerStorageSlotData>(entity);
                    NativeHashMap<int, InnerStorageSlotData> datas = new NativeHashMap<
                        int,
                        InnerStorageSlotData
                    >(rBuff.Length, Allocator.TempJob);
                    foreach (var r in rBuff)
                        datas.Add(r.ItemId, r);
                    ecb.RemoveComponent<InnerStorageSlotData>(entity);

                    var inBuffer = ecb.AddBuffer<InnerStorageSlotData>(entity);
                    foreach (var inn in recipe.inputItems)
                    {
                        var id = inn.itemId;
                        var capacity = 100;
                        var count = 0;
                        if (datas.ContainsKey(id))
                        {
                            capacity = datas[id].Capacity;
                            count = datas[id].Count;
                            datas.Remove(id);
                        }
                        inBuffer.Add(
                            new InnerStorageSlotData
                            {
                                ItemId = id,
                                Count = count,
                                Capacity = capacity,
                            }
                        );
                    }
                    if (datas.Count > 0)
                    {
                        var en = ecb.CreateEntity();
                        ecb.AddComponent<DistribureRemovedItems>(en);
                        var dbf = ecb.AddBuffer<OutputStorageSlotData>(en);
                        foreach (var d in datas)
                            dbf.Add(
                                new OutputStorageSlotData
                                {
                                    ItemId = d.Value.ItemId,
                                    Count = d.Value.Count,
                                    Capacity = d.Value.Capacity,
                                }
                            );
                    }
                    datas.Dispose();
                }
                else
                {
                    foreach (var inn in recipe.inputItems)
                    {
                        var inBuffer = ecb.AddBuffer<InnerStorageSlotData>(entity);
                        inBuffer.Add(
                            new InnerStorageSlotData
                            {
                                ItemId = inn.itemId,
                                Count = 0,
                                Capacity = 100,
                            }
                        );
                    }
                }
            }
            if (
                EntityManager.HasComponent<ProducerBuildingTag>(entity)
                || EntityManager.HasComponent<ProcessorBuildingTag>(entity)
            )
            {
                if (EntityManager.HasBuffer<OutputStorageSlotData>(entity))
                {
                    var rBuff = EntityManager.GetBuffer<OutputStorageSlotData>(entity);

                    NativeHashMap<int, OutputStorageSlotData> datas = new NativeHashMap<
                        int,
                        OutputStorageSlotData
                    >(rBuff.Length, Allocator.TempJob);
                    foreach (var r in rBuff)
                        datas.Add(r.ItemId, r);
                    ecb.RemoveComponent<OutputStorageSlotData>(entity);

                    var inBuffer = ecb.AddBuffer<OutputStorageSlotData>(entity);
                    foreach (var inn in recipe.outputItems)
                    {
                        var id = inn.itemId;
                        var capacity = 100;
                        var count = 0;
                        if (datas.ContainsKey(id))
                        {
                            capacity = datas[id].Capacity;
                            count = datas[id].Count;
                            datas.Remove(id);
                        }
                        inBuffer.Add(
                            new OutputStorageSlotData
                            {
                                ItemId = id,
                                Count = count,
                                Capacity = capacity,
                            }
                        );
                    }
                    if (datas.Count > 0)
                    {
                        var en = ecb.CreateEntity();
                        ecb.AddComponent<DistribureRemovedItems>(en);
                        var dbf = ecb.AddBuffer<OutputStorageSlotData>(en);
                        foreach (var d in datas)
                            dbf.Add(d.Value);
                    }
                    datas.Dispose();
                    ecb.SetComponent(
                        entity,
                        new ProducerBuildingData
                        {
                            TimeToProduceNext = recipe.craftTime,
                            CurrTime = 0,
                        }
                    );
                }
                else
                {
                    foreach (var inn in recipe.inputItems)
                    {
                        var inBuffer = ecb.AddBuffer<OutputStorageSlotData>(entity);
                        inBuffer.Add(
                            new OutputStorageSlotData
                            {
                                ItemId = inn.itemId,
                                Count = 0,
                                Capacity = 100,
                            }
                        );
                    }
                    ecb.AddComponent(
                        entity,
                        new ProducerBuildingData
                        {
                            TimeToProduceNext = recipe.craftTime,
                            CurrTime = 0,
                        }
                    );
                }
            }
            ecb.RemoveComponent<ChangeRecipeData>(entity);
        }
    }

    void ChangeInnerSlotCapacity(
        Entity entity,
        ChangeInnerSlotCapacityData changeInnerSlotCapacityData
    )
    {
        var bf = EntityManager.GetBuffer<InnerStorageSlotData>(entity);
        if (bf.Length > changeInnerSlotCapacityData.SlotID)
        {
            var slot = bf[changeInnerSlotCapacityData.SlotID];
            slot.Capacity = changeInnerSlotCapacityData.newCapacity;
            bf[changeInnerSlotCapacityData.SlotID] = slot;
        }
    }

    void ChangeOutputSlotCapacity(
        Entity entity,
        ChangeOutputSlotCapacityData changeOutputSlotCapacityData
    )
    {
        var bf = EntityManager.GetBuffer<OutputStorageSlotData>(entity);
        if (bf.Length > changeOutputSlotCapacityData.SlotID)
        {
            var slot = bf[changeOutputSlotCapacityData.SlotID];
            slot.Capacity = changeOutputSlotCapacityData.newCapacity;
            bf[changeOutputSlotCapacityData.SlotID] = slot;
        }
    }

    void ChangePriority(
        Entity entity,
        BuildingLogicData buildingData,
        ChangePriorityData data,
        EntityCommandBuffer ecb
    )
    {
        if (Enum.IsDefined(typeof(DistributionPriority), data.newPriorityID))
        {
            BuildingLogicData newData = new BuildingLogicData
            {
                Priority = data.newPriorityID,
                RequiredRecipesGroup = buildingData.RequiredRecipesGroup,
                RecipeIDHash = buildingData.RecipeIDHash,
            };
            ecb.SetComponent(entity, newData);
        }
    }
}
