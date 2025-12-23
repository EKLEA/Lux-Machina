using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.VisualScripting;
using UnityEngine;
using Zenject;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RoadSystem))]
public partial class BuildingLogicAssignSystem : SystemBase
{
    [Inject] IReadOnlyBuildingInfo buildingInfo;
    [Inject] IReadOnlySave saveFile;
    [Inject] IReadOnlyRecipeInfo recipeInfo;
    [Inject] IReadOnlyStorageConfig storageConfig;
    [Inject] IReadOnlyItemsInfo itemsInfo;
    protected override void OnCreate()
    {
        RequireForUpdate<SaveService>();
    }
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        
        foreach (var (buildingLogicData, entity) in SystemAPI
            .Query<BuildingData>()
            .WithAll<AssignLogicTag>()
            .WithNone<BuildingTag,PropTag>()
            .WithNone< ProcessorBuildingTag,DefenceBuildingTag,StorageBuildingTag>()
            .WithEntityAccess())
        {
            AssignLogic(entity, buildingLogicData, ecb);
        };
        foreach (var (changeBuff,slotBuff, entity) in SystemAPI
            .Query<DynamicBuffer<ChangeSlotCapacityData>,DynamicBuffer<SlotData>>()
            .WithAny<InputSlots,OutputSlots>()
            .WithEntityAccess())
        {
            if(changeBuff.Length>0&&slotBuff.Length>0 && slotBuff.Length>=changeBuff.Length)
                ChangeSlotCapacity(entity,changeBuff,slotBuff,ecb);
        }

        foreach (var (data, entity) in SystemAPI
            .Query<ChangePriorityData>()
            .WithAll<BuildingPriorityData>()
            .WithEntityAccess())
        {
            ChangePriority(entity, data, ecb);
        }
        foreach (var (data, entity) in SystemAPI
            .Query<ChangeBuildingCountOfPackData>()
            .WithAll<CountOfPackInBuildingData>()
            .WithEntityAccess())
        {
            ChangeBuildingCountOfPack(entity, data, ecb);
        }
        foreach (var (countOfPackData, recipeGroupData,newRecipe, entity) in SystemAPI
            .Query<CountOfPackInBuildingData,BuildingRequiredRecipesGroupData, ChangeRecipeData>()
            .WithEntityAccess())
        {
            ChangeRecipe(entity, countOfPackData, recipeGroupData,newRecipe, ecb);
        }

        foreach (var (BuildingData,CreateStorageSlotData,slotBuff, entity) in SystemAPI
            .Query<BuildingData,CreateStorageSlot,DynamicBuffer<SlotData>>()
            .WithAll<BuildingTag>()
            .WithAny<StorageBuildingTag,DefenceBuildingTag>()
            .WithEntityAccess())
        {
            CreateStorageSlot(entity,BuildingData,CreateStorageSlotData,slotBuff,ecb);
        }
        foreach (var (DeleteStorageSlotData,slotBuff, entity) in SystemAPI
            .Query<DeleteStorageSlot,DynamicBuffer<SlotData>>()
            .WithAll<BuildingTag>()
            .WithAny<StorageBuildingTag,DefenceBuildingTag>()
            .WithEntityAccess())
        {
            DeleteStorageSlot(entity,DeleteStorageSlotData,slotBuff,ecb);
        }
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    void AssignLogic(Entity entity, BuildingData buildingData, EntityCommandBuffer ecb)
    {
        var info = buildingInfo.BuildingInfos[buildingData.BuildingIDHash];
        if (info.buildingType == BuildingsTypes.Prop)
        {
            ecb.AddComponent<PropTag>(entity);
        }
        else
        {
            if (info.typeOfLogic != TypeOfLogic.None)
            {
                ecb.AddComponent<BuildingStateData>(entity);
                switch (info.typeOfLogic)
                {
                    case TypeOfLogic.WorkWithItems:
                        var slotBuff=ecb.AddBuffer<SlotData>(entity);
                        if (saveFile.GameState.slotDatas.ContainsKey(buildingData.UniqueIDHash))
                        {
                            slotBuff.ResizeUninitialized(saveFile.GameState.slotDatas[buildingData.UniqueIDHash].Count);
                            foreach(var sl in saveFile.GameState.slotDatas[buildingData.UniqueIDHash])
                                slotBuff[sl.ind]=sl.slotData;
                        }
                        ecb.AddComponent(entity, new BuildingPriorityData(){Priority=saveFile.GameState.buildingsPriorityDatas.ContainsKey(buildingData.UniqueIDHash)?
                        saveFile.GameState.buildingsPriorityDatas[buildingData.UniqueIDHash].Priority:(int)DistributionPriority.Middle
                        });

                        switch (info.buildingType)
                        {
                            case BuildingsTypes.Production:
                                ecb.AddComponent<ProcessorBuildingTag>(entity);
                                ecb.AddComponent(entity,new CountOfPackInBuildingData{CountOfPack=1});
                                var fixedListRIDs = new FixedList32Bytes<int>();
                                foreach(var rId in info.requiredRecipesGroup)
                                    fixedListRIDs.Add(rId);
                                ecb.AddComponent(entity,new BuildingRequiredRecipesGroupData{RequiredRecipesGroups=fixedListRIDs});
                                
                                if(saveFile.GameState.processBuildingDatas.ContainsKey(buildingData.UniqueIDHash))
                                    ecb.AddComponent(entity,saveFile.GameState.processBuildingDatas[buildingData.UniqueIDHash]);
                                else  
                                    ecb.AddComponent<AssingRecipeTag>(entity);
                                
                                if(saveFile.GameState.changeRecipeDatas.ContainsKey(buildingData.UniqueIDHash))
                                    ecb.AddComponent(entity,saveFile.GameState.changeRecipeDatas[buildingData.UniqueIDHash]);

                                if(saveFile.GameState.changeBuildingCountOfPackDatas.ContainsKey(buildingData.UniqueIDHash))
                                    ecb.AddComponent(entity,saveFile.GameState.changeBuildingCountOfPackDatas[buildingData.UniqueIDHash]);
                            break;

                            case BuildingsTypes.Defence:
                                ecb.AddComponent<DefenceBuildingTag>(entity);
                                //компонент амуниции
                            break;
                            case BuildingsTypes.Logistic:
                                ecb.AddComponent<StorageBuildingTag>(entity);
                            break;

                        }


                    if(saveFile.GameState.changeSlotCapacitDatas.ContainsKey(buildingData.UniqueIDHash))
                    {
                        var changebf = ecb.AddBuffer<ChangeSlotCapacityData>(entity);
                        foreach(var csc in saveFile.GameState.changeSlotCapacitDatas[buildingData.UniqueIDHash])
                            changebf.Add(csc);
                    }
                    if(saveFile.GameState.changePrioritDatas.ContainsKey(buildingData.UniqueIDHash))
                        ecb.AddComponent(entity,saveFile.GameState.changePrioritDatas[buildingData.UniqueIDHash]);

                    if(saveFile.GameState.inputSlots.ContainsKey(buildingData.UniqueIDHash))
                    {
                        ecb.AddComponent(entity,saveFile.GameState.inputSlots[buildingData.UniqueIDHash]);
                        if(saveFile.GameState.canResoucesBeAddedTag.Contains(buildingData.UniqueIDHash))
                             ecb.AddComponent<CanResoucesBeAddedTag>(entity);
                    }
                    if(saveFile.GameState.outputSlots.ContainsKey(buildingData.UniqueIDHash))
                    {
                        ecb.AddComponent(entity,saveFile.GameState.outputSlots[buildingData.UniqueIDHash]);
                        if(saveFile.GameState.canResoucesBeRemovedTag.Contains(buildingData.UniqueIDHash))
                            ecb.AddComponent<CanResoucesBeRemovedTag>(entity);
                    }
                    if(saveFile.GameState.excessItemSlots.ContainsKey(buildingData.UniqueIDHash))
                        ecb.AddComponent(entity,saveFile.GameState.excessItemSlots[buildingData.UniqueIDHash]);
                    break;
                        
                        
                }
                ecb.AddComponent<BuildingTag>(entity);
            }
        }
        ecb.RemoveComponent<AssignLogicTag>(entity);
    }
    void DeleteStorageSlot(Entity entity, DeleteStorageSlot deleteStorageSlotData, DynamicBuffer<SlotData> slotBuff, EntityCommandBuffer ecb)
    {
        if (deleteStorageSlotData.SlotIND < slotBuff.Length)
        {
            var slot = slotBuff[deleteStorageSlotData.SlotIND];
            var InputData=EntityManager.GetComponentData<InputSlots>(entity);
            var OutputData=EntityManager.GetComponentData<OutputSlots>(entity);
            slotBuff.RemoveAt(deleteStorageSlotData.SlotIND);
            InputData.EndIND--;
            OutputData.EndIND--;
            ecb.SetComponent<InputSlots>(entity,InputData);
            ecb.SetComponent<OutputSlots>(entity,OutputData);
            if(slot.Amount>0)
            {
                if (EntityManager.HasComponent<ExcessItemSlots>(entity))
                {
                    var excessData=EntityManager.GetComponentData<ExcessItemSlots>(entity);
                    excessData.EndIND++;
                    ecb.SetComponent<ExcessItemSlots>(entity,excessData);
                }
                else
                {
                    EntityManager.AddComponentData(entity,new ExcessItemSlots{StartIND=slotBuff.Length,EndIND=slotBuff.Length} );
                }
                slotBuff.Add(slot);
            }
        }
        ecb.RemoveComponent<DeleteStorageSlot>(entity);
    }
    void CreateStorageSlot(Entity building,BuildingData buildingData,CreateStorageSlot createSlotData,DynamicBuffer<SlotData>slotBuff, EntityCommandBuffer ecb)
    {
        var config=storageConfig.StorageConfig[buildingData.BuildingIDHash];
        if (slotBuff.Length < config.MaxSlots)
        {
            if (!config.ItemsTypes.Contains((int)ItemType.None))
            {
                if (config.ItemsTypes.Contains((int)itemsInfo.ItemsInfos[createSlotData.ItemId].ItemType))
                {
                    var InputData=EntityManager.GetComponentData<InputSlots>(building);
                    var OutputData=EntityManager.GetComponentData<OutputSlots>(building);
                    int amount=0;
                    if (EntityManager.HasComponent<ExcessItemSlots>(building))
                    {
                        NativeList<int> indexToRemove=new NativeList<int>(Allocator.Temp);
                        var excessData=EntityManager.GetComponentData<ExcessItemSlots>(building);
                        for(int i=excessData.StartIND;i<=excessData.EndIND;i++)
                        {
                            if (slotBuff[i].ItemId == createSlotData.ItemId&&slotBuff[i].Amount>0)
                            {
                                if (amount + slotBuff[i].Amount <= createSlotData.Capacity)
                                {
                                    amount +=slotBuff[i].Amount;
                                    var t=slotBuff[i];
                                    t.Amount=0;
                                    slotBuff[i]=t;
                                    indexToRemove.Add(i);
                                }
                            }
                        }
                        indexToRemove.Sort();
                        for(int i = indexToRemove.Length - 1; i >= 0; i--)
                        {
                            slotBuff.RemoveAt(indexToRemove[i]);
                            excessData.EndIND--;
                        }
                        indexToRemove.Dispose();
                    }
                    slotBuff.Add(new SlotData{Amount=amount,Capacity=createSlotData.Capacity,ItemId=createSlotData.ItemId});
                    InputData.EndIND++;
                    OutputData.EndIND++;
                    ecb.SetComponent<InputSlots>(building,InputData);
                    ecb.SetComponent<OutputSlots>(building,OutputData);
                }
            }
        }
        ecb.RemoveComponent<CreateStorageSlot>(building);
    }
    void ChangeRecipe(Entity building, CountOfPackInBuildingData countOfPackDataData, 
    BuildingRequiredRecipesGroupData recipeGroupData, ChangeRecipeData newRecipe, EntityCommandBuffer ecb)
    {
        if (newRecipe.newRecipeID ==-1)
        {
            if(!EntityManager.HasComponent<ProcessBuildingData>(building)) return;
            else
            {
                 var buff = EntityManager.GetBuffer<SlotData>(building);
                
                
                if (EntityManager.HasComponent<ExcessItemSlots>(building))
                    ecb.RemoveComponent<ExcessItemSlots>(building);
                
                
                if (EntityManager.HasComponent<OutputSlots>(building))
                    ecb.RemoveComponent<OutputSlots>(building);
                
                if (EntityManager.HasComponent<InputSlots>(building))
                    ecb.RemoveComponent<InputSlots>(building);
                
                
                ecb.AddComponent(building, new ExcessItemSlots
                {
                    StartIND = 0,
                    EndIND = buff.Length - 1
                });
            
            
                ecb.AddComponent<AssingRecipeTag>(building);
                
                
                if (EntityManager.HasComponent<ProcessBuildingData>(building))
                {
                    ecb.RemoveComponent<ProcessBuildingData>(building);
                }
                
                return;
            }
        }
        else
        {
            
            var recipe=recipeInfo.RecipeInfos[newRecipe.newRecipeID];
            if (CanBuildingUseRecipe(recipeGroupData.RequiredRecipesGroups, recipe.groupIds))
            {
                var processData= new ProcessBuildingData()
                {
                    RecipeIDHash=newRecipe.newRecipeID,
                    TimeToProduceNext=recipe.craftTime,
                    CurrTime=0
                };
                var excessItems = new NativeHashMap<int,SlotData>(100,Allocator.Temp);
                NativeList<int> indexToRemove=new NativeList<int>(Allocator.Temp);
                if (EntityManager.HasBuffer<SlotData>(building))
                {
                    var allSlots = EntityManager.GetBuffer<SlotData>(building);
                    
                    for (int i = 0; i < allSlots.Length; i++)
                    {
                        if(allSlots[i].Amount > 0)
                            excessItems.Add(i,allSlots[i]);
                    }
                    
                    ecb.RemoveComponent<SlotData>(building);  
                }  
                var slotBuff =ecb.AddBuffer<SlotData>(building);
                int startInd=0;
                if (recipe.inputItems.Count > 0)
                {
                    ecb.AddComponent(building,new InputSlots{StartIND=startInd,EndIND=startInd+recipe.inputItems.Count-1});
                    startInd=startInd+recipe.outputItems.Count;
                    foreach (var inputItem in recipe.inputItems.Values)
                    {
                        int amount = 0;
                        int capacity = countOfPackDataData.CountOfPack * inputItem.amount;

                        var keys = excessItems.GetKeyArray(Allocator.Temp);

                        foreach (int key in keys)
                        {
                            if (amount >= capacity)
                                break;
                            
                            if (!indexToRemove.Contains(key))
                            {
                                SlotData slotData = excessItems[key];
                                
                                if (slotData.ItemId == inputItem.itemId)
                                {
                                    if (amount + slotData.Amount <= capacity)
                                    {
                                        amount += slotData.Amount;
                                        slotData.Amount = 0;
                                        excessItems[key] = slotData;
                                        indexToRemove.Add(key);
                                    }
                                    else
                                    {
                                        int takeAmount = capacity - amount;
                                        slotData.Amount -= takeAmount;
                                        amount = capacity;
                                        excessItems[key] = slotData;
                                    }
                                }
                            }
                        }
                        keys.Dispose();
                        slotBuff.Add(new SlotData{Amount=amount,Capacity=capacity,ItemId=inputItem.itemId});
                    }
                }
                if (recipe.outputItems.Count > 0)
                {
                    ecb.AddComponent(building,new OutputSlots{StartIND=startInd,EndIND=startInd+recipe.outputItems.Count-1});
                    startInd=startInd+recipe.outputItems.Count;
                    foreach( var outputItem in recipe.outputItems.Values)
                    {
                        int capacity = countOfPackDataData.CountOfPack * outputItem.amount;
                        slotBuff.Add(new SlotData{Amount=0,Capacity=capacity,ItemId=outputItem.itemId});
                    }
                }
                indexToRemove.Sort();
                for(int i = indexToRemove.Length - 1; i >= 0; i--)
                {
                    excessItems.Remove(indexToRemove[i]);
                }
                indexToRemove.Dispose();
                if (excessItems.Count > 0)
                {
                    ecb.AddComponent(building,new ExcessItemSlots{StartIND=startInd,EndIND=startInd+excessItems.Count-1});
                    foreach( var ex in excessItems)
                         slotBuff.Add(ex.Value);
                }
                excessItems.Dispose();
                ecb.SetComponent<ProcessBuildingData>(building,processData);
                if (EntityManager.HasComponent<AssingRecipeTag>(building))
                {
                    ecb.RemoveComponent<AssingRecipeTag>(building);
                }
            }
        }
        
        ecb.RemoveComponent<ChangeRecipeData>(building);
    }
    bool CanBuildingUseRecipe(FixedList32Bytes<int> buildingGroups, HashSet<int> recipeGroups)
    {
        foreach (int buildingGroup in buildingGroups)
        {
            if (recipeGroups.Contains(buildingGroup))
                return true;
        }
        return false;
    }
    void ChangeBuildingCountOfPack(Entity entity, ChangeBuildingCountOfPackData countOfPackDataData, EntityCommandBuffer ecb)
    {
        if (countOfPackDataData.newCountOfPack>=1)
        {
            var data = new CountOfPackInBuildingData(){CountOfPack=countOfPackDataData.newCountOfPack};
            ecb.SetComponent<CountOfPackInBuildingData>(entity, data);
            if (EntityManager.HasBuffer<SlotData>(entity)&&EntityManager.HasComponent<ProcessBuildingData>(entity))
            {
                var slotBuff=EntityManager.GetBuffer<SlotData>(entity);
                var recipe=recipeInfo.RecipeInfos[EntityManager.GetComponentData<ProcessBuildingData>(entity).RecipeIDHash];
                if(EntityManager.HasComponent<InputSlots>(entity)||EntityManager.HasComponent<OutputSlots>(entity))
                {
                    DynamicBuffer<ChangeSlotCapacityData> changeBuff;
                    if(EntityManager.HasBuffer<ChangeSlotCapacityData>(entity))
                        changeBuff=EntityManager.GetBuffer<ChangeSlotCapacityData>(entity);
                    else
                        changeBuff=ecb.AddBuffer<ChangeSlotCapacityData>(entity);

                    if (EntityManager.HasComponent<InputSlots>(entity))
                    {
                        var inputData=EntityManager.GetComponentData<InputSlots>(entity);
                        for (int i = inputData.StartIND; i <= inputData.EndIND; i++)
                        {
                            if (i >= slotBuff.Length) break;
                            
                            var itemId = slotBuff[i].ItemId;
                            if (recipe.inputItems.TryGetValue(itemId, out var inputItem))
                            {
                                changeBuff.Add(new ChangeSlotCapacityData {
                                    SlotIND = i,
                                    newCapacity = inputItem.amount * countOfPackDataData.newCountOfPack
                                });
                            }
                            else
                            {
                                Debug.Log("Ошибка инпута   "+itemId);
                            }
                        }
                    }

                    if (EntityManager.HasComponent<OutputSlots>(entity))
                    {
                        var outputData=EntityManager.GetComponentData<OutputSlots>(entity);
                        for (int i = outputData.StartIND; i <= outputData.EndIND; i++)
                        {
                            if (i >= slotBuff.Length) break;
                            
                            var itemId = slotBuff[i].ItemId;
                            if (recipe.outputItems.TryGetValue(itemId, out var outputItem))
                            {
                                changeBuff.Add(new ChangeSlotCapacityData {
                                    SlotIND = i,
                                    newCapacity = outputItem.amount * countOfPackDataData.newCountOfPack
                                });
                            }
                            else
                            {
                                Debug.Log("Ошибка оутпута  "+itemId);
                            }
                        }
                    }
                }
            }
        }
        ecb.RemoveComponent<ChangeBuildingCountOfPackData>(entity);
    }
    void ChangePriority(Entity entity, ChangePriorityData priorityData, EntityCommandBuffer ecb)
    {
        if (Enum.IsDefined(typeof(DistributionPriority), priorityData.newPriorityID))
        {
            var data = new BuildingPriorityData(){Priority=priorityData.newPriorityID};
            ecb.SetComponent<BuildingPriorityData>(entity, data);
        }
        ecb.RemoveComponent<ChangePriorityData>(entity);
    }
    void ChangeSlotCapacity(Entity building,DynamicBuffer<ChangeSlotCapacityData> changeBuff,DynamicBuffer<SlotData>slotBuff, EntityCommandBuffer ecb)
    {
        foreach(var changeData in changeBuff)
        {
            var slot=slotBuff[changeData.SlotIND];
            
            if (changeData.newCapacity > slot.Capacity)
            {
                if (EntityManager.HasComponent<ExcessItemSlots>(building))
                {
                     NativeList<int> indexToRemove=new NativeList<int>(Allocator.Temp);
                    var excessData=EntityManager.GetComponentData<ExcessItemSlots>(building);
                    for(int i=excessData.StartIND;i<=excessData.EndIND;i++)
                    {
                        if (slotBuff[i].ItemId == slot.ItemId&&slotBuff[i].Amount>0)
                        {
                            if (slot.Amount + slotBuff[i].Amount <= changeData.newCapacity)
                            {
                                slot.Amount +=slotBuff[i].Amount;
                                var t=slotBuff[i];
                                t.Amount=0;
                                slotBuff[i]=t;
                                indexToRemove.Add(i);
                            }
                        }
                    }
                    indexToRemove.Sort();
                    for(int i = indexToRemove.Length - 1; i >= 0; i--)
                    {
                        slotBuff.RemoveAt(indexToRemove[i]);
                        excessData.EndIND--;
                    }
                    indexToRemove.Dispose();
                }
                slot.Capacity=changeData.newCapacity;
                slotBuff[changeData.SlotIND]=slot;
            }
            else
            {
                if (slot.Amount > changeData.newCapacity)
                {
                    int remain=slot.Amount- changeData.newCapacity;
                    slot.Capacity=changeData.newCapacity;
                    slot.Amount= slot.Capacity;
                    if (EntityManager.HasComponent<ExcessItemSlots>(building))
                    {
                        var excessData=EntityManager.GetComponentData<ExcessItemSlots>(building);
                        for(int i=excessData.StartIND;i<=excessData.EndIND;i++)
                        {
                            if (slotBuff[i].ItemId == slot.ItemId&&slotBuff[i].Amount<slotBuff[i].Capacity)
                            {
                                int addValue=slotBuff[i].Amount+remain<=slotBuff[i].Capacity?remain:slotBuff[i].Capacity-slotBuff[i].Amount;
                                var t=slotBuff[i];
                                t.Amount+=addValue;
                                slotBuff[i]=t;
                                remain-=addValue;
                                if(remain==0)
                                    break;
                            }  

                        }
                        if(remain>0)
                        {
                            slotBuff.Add(new SlotData(){ItemId=slot.ItemId,Capacity=100, Amount=remain});
                            excessData.EndIND++;
                        }
                    }
                    else
                    {
                        slotBuff.Add(new SlotData(){ItemId=slot.ItemId,Capacity=100, Amount=remain});
                        ecb.AddComponent(building,new ExcessItemSlots(){StartIND=slotBuff.Length - 1,EndIND=slotBuff.Length - 1});
                    }
                }
            }
        }
        ecb.RemoveComponent<ChangeSlotCapacityData>(building);
    }
}
