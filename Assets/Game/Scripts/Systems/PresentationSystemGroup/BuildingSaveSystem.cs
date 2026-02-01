using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Zenject;
[DisableAutoCreation]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof( ProccessDeletePointsSystem) )]
public partial class BuildingSaveSystem : SystemBase
{
    EntityQuery SaveLoadInfo;
    [Inject] SaveService saveService;
    [Inject] IReadOnlyBuildingInfo buildingInfo;
    [Inject] GameFieldSettings gameFieldSettings;

    protected override void OnCreate()
    {
        SaveLoadInfo= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BuildingMap,LoadingMapTag>()
            .Build(World.EntityManager);
    }
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (buildingData,loadState,building) in SystemAPI.Query<BuildingData,EnabledRefRW<LoadInfo>>().WithEntityAccess())
        {
            LoadInfo(buildingData,building,loadState,ecb);
        }
        if (!SaveLoadInfo.IsEmpty)
        {
            Entity mapEntity=SystemAPI.GetSingletonEntity<BuildingMap>();
            ecb.SetComponentEnabled<LoadingMapTag>(mapEntity,false);
        }
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    public void LoadInfo(BuildingData buildingData, Entity building,EnabledRefRW<LoadInfo> loadState,EntityCommandBuffer ecb)
    {
        if (saveService.GameState.constructionSlotsSaveData.TryGetValue(buildingData.BuildingUniqueID,out var constructionSlotsSaveData))
        {
            if (constructionSlotsSaveData.InputConstructionItems != null)
            {
                var inputConstBuff=ecb.SetBuffer<InputConstructionSlotData>(building);
                ecb.SetComponentEnabled<IsInputConstructionEnabled>(building,constructionSlotsSaveData.isInputEnabled);
                foreach(var iC in constructionSlotsSaveData.InputConstructionItems)
                {
                    inputConstBuff.Add(iC);
                }
            }
            if (constructionSlotsSaveData.OutputConstructionItems != null)
            {
                var outputConstBuff=ecb.SetBuffer<OutputConstructionSlotData>(building);
                ecb.SetComponentEnabled<IsOutputConstuctionEnabled>(building,constructionSlotsSaveData.isOutputEnabled);
                foreach(var oC in constructionSlotsSaveData.OutputConstructionItems)
                {
                    outputConstBuff.Add(oC);
                }
            }
            ecb.SetComponent(building,new ConstructionPriorityData{ConstructionPriority=(int)constructionSlotsSaveData.priority});
            
            ecb.SetComponentEnabled<IsConstuctionSlotsAssigned>(building, true);
        }
        else
        {
            if(buildingInfo.BuildingItemRequestsInfos.TryGetValue(buildingData.BuildingIDHash,out var itemRequests))
            {
                var k =1;
                if (EntityManager.HasComponent<RoadTypeBuildingTag>(building))
                {
                    k=EntityManager.GetBuffer<MapPoint>(building).Length;
                }
                if (EntityManager.HasBuffer<TransitionSlotData>(building))
                {
                    if (!EntityManager.IsComponentEnabled<IsDemolition>(building) && 
                    EntityManager.IsComponentEnabled<ChangeDemolitionStateTag>(building))
                    {
                        var outputConstBuff=ecb.SetBuffer<OutputConstructionSlotData>(building);
                        foreach(var iR in itemRequests.itemsRequest)
                        {
                            outputConstBuff.Add(new OutputConstructionSlotData{ItemId=iR.itemId,Amount=0,Capacity=iR.amount*k});
                        }
                        var tSlots=EntityManager.GetBuffer<TransitionSlotData>(building);
                        for(int i = 0; i < outputConstBuff.Length; i++)
                        {
                            var outSlot=outputConstBuff[i];
                            if(outSlot.Capacity==outSlot.Amount) continue;
                            for(int j = 0; j < tSlots.Length; j++)
                            {
                                var tSL=tSlots[j];
                                if (tSL.itemID == outSlot.ItemId)
                                {
                                    int fill=outSlot.Capacity-outSlot.Amount;
                                    if (tSL.amount > fill)
                                    {
                                        outSlot.Amount=outSlot.Capacity;
                                        tSL.amount-=fill;
                                    }
                                    else
                                    {
                                        outSlot.Amount+=tSL.amount;
                                        tSL.amount=0;
                                    }
                                }
                                tSlots[j]=tSL;
                            }
                            outputConstBuff[i]=outSlot;
                        }
                        NativeList<TransitionSlotData> transitionSlotDatas=new(100,Allocator.Temp);
                        foreach(var tSL in tSlots)
                        {
                            if(tSL.amount>0) transitionSlotDatas.Add(tSL);
                        }
                        if (transitionSlotDatas.Length > 0)
                        {
                            var ex=ecb.SetBuffer<ExcessSlotData>(building);
                            foreach(var tSL in transitionSlotDatas)
                                ex.Add(new ExcessSlotData{ItemId=tSL.itemID,Amount=tSL.amount,Capacity=100});
                        }
                        ecb.RemoveComponent<TransitionSlotData>(building);
                    }
                    else
                    {
                        
                        var inputConstBuff=ecb.SetBuffer<InputConstructionSlotData>(building);
                        foreach(var iR in itemRequests.itemsRequest)
                        {
                            inputConstBuff.Add(new InputConstructionSlotData{ItemId=iR.itemId,Amount=0,Capacity=iR.amount*k});
                        }
                        var tSlots=EntityManager.GetBuffer<TransitionSlotData>(building);
                        for(int i = 0; i < inputConstBuff.Length; i++)
                        {
                            var inpSlot=inputConstBuff[i];
                            if(inpSlot.Capacity==inpSlot.Amount) continue;
                            for(int j = 0; j < tSlots.Length; j++)
                            {
                                var tSL=tSlots[j];
                                if (tSL.itemID == inpSlot.ItemId)
                                {
                                    int fill=inpSlot.Capacity-inpSlot.Amount;
                                    if (tSL.amount > fill)
                                    {
                                        inpSlot.Amount=inpSlot.Capacity;
                                        tSL.amount-=fill;
                                    }
                                    else
                                    {
                                        inpSlot.Amount+=tSL.amount;
                                        tSL.amount=0;
                                    }
                                }
                                tSlots[j]=tSL;
                            }
                            inputConstBuff[i]=inpSlot;
                        }
                        NativeList<TransitionSlotData> transitionSlotDatas=new(100,Allocator.Temp);
                        foreach(var tSL in tSlots)
                        {
                            if(tSL.amount>0) transitionSlotDatas.Add(tSL);
                        }
                        if (transitionSlotDatas.Length > 0)
                        {
                            var ex=ecb.SetBuffer<ExcessSlotData>(building);
                            foreach(var tSL in transitionSlotDatas)
                            {
                                ex.Add(new ExcessSlotData{ItemId=tSL.itemID,Amount=tSL.amount,Capacity=100});
                                Debug.Log("dssddsds");
                            }
                        }
                        ecb.RemoveComponent<TransitionSlotData>(building);
                    }
                }
                else
                {
                    if (!EntityManager.IsComponentEnabled<IsBlueprint>(building) && 
                        EntityManager.IsComponentEnabled<ChangeBluePrintState>(building))
                    {
                        var inputConstBuff=ecb.SetBuffer<InputConstructionSlotData>(building);
                        foreach(var iR in itemRequests.itemsRequest)
                        {
                            inputConstBuff.Add(new InputConstructionSlotData{ItemId=iR.itemId,Amount=0,Capacity=iR.amount*k});
                        }
                    }
                    else
                    {
                        var outputConstBuff=ecb.SetBuffer<OutputConstructionSlotData>(building);
                        foreach(var iR in itemRequests.itemsRequest)
                        {
                            outputConstBuff.Add(new OutputConstructionSlotData{ItemId=iR.itemId,Amount=iR.amount*k,Capacity=iR.amount*k});
                        }
                    }
                }
            }
            ecb.SetComponent(building,new ConstructionPriorityData{ConstructionPriority=(int)gameFieldSettings.defaultDistributionPriority});
            ecb.SetComponentEnabled<IsConstuctionSlotsAssigned>(building, true);
            
        }

        if(saveService.GameState.excessSlotsSaveData.TryGetValue(buildingData.BuildingUniqueID,out var excessData))
        {
            var exBuff=ecb.SetBuffer<ExcessSlotData>(building);
            foreach(var ex in excessData.ExcessItems)
                exBuff.Add(ex);
        }

        if (EntityManager.HasComponent<IsRecipeAssigned>(building))
        {
            if(saveService.GameState.recipeBuildingSaveData.TryGetValue(buildingData.BuildingUniqueID,out var recipeData))
            {
                if (recipeData.InputCrafttems != null)
                {
                    var inputCraftBuff=ecb.SetBuffer<InputSlotData>(building);
                    ecb.SetComponentEnabled<IsInputCraftEnabled>(building,recipeData.isInputEnabled);
                    foreach(var iC in recipeData.InputCrafttems)
                    {
                        inputCraftBuff.Add(iC);
                    }
                }
                if (recipeData.OutputCrafttems != null)
                {
                    var outputCraftBuff=ecb.SetBuffer<OutputSlotData>(building);
                    ecb.SetComponentEnabled<IsOutputCraftEnabled>(building,recipeData.isOutputEnabled);
                    foreach(var oC in recipeData.OutputCrafttems)
                    {
                        outputCraftBuff.Add(oC);
                    }
                }
                
                ecb.SetComponent(building,new CraftingPriorityData{CraftingPriority=(int)recipeData.priority});
                ecb.SetComponent(building,
                    new CountOfPackInBuildingData { CountOfPack = recipeData.ContOfPack });
                ecb.SetComponent(building,new RecipeBuildingData{RecipeIDHash=recipeData.RecipeID,CurrTime=recipeData.CurrTime,TimeToCraft=recipeData.TimeToCraft});
                ecb.SetComponentEnabled<IsRecipeAssigned>(building,true);
            }
            else
            {
                ecb.SetComponent(building,
                    new CountOfPackInBuildingData { CountOfPack = 2 });
                ecb.SetComponent(building,new CraftingPriorityData{CraftingPriority=(int)gameFieldSettings.defaultDistributionPriority});
                ecb.SetComponent(building,new RecipeBuildingData{RecipeIDHash=-1,CurrTime=0,TimeToCraft=0});
            }
        }
        if (EntityManager.HasComponent<StorageTypeBuildingTag>(building))
        {
            if(saveService.GameState.storageSlotsSaveData.TryGetValue(buildingData.BuildingUniqueID,out var storageData))
            {
                if (storageData.slots != null)
                {
                    var storageBuff=ecb.SetBuffer<StorageSlotData>(building);
                    foreach(var iC in storageData.slots)
                    {
                        storageBuff.Add(iC);
                    }
                }   
                ecb.SetComponent(building,new CraftingPriorityData{CraftingPriority=(int)storageData.priority});
            }
            else
            {
                ecb.SetComponent(building,new CraftingPriorityData{CraftingPriority=(int)gameFieldSettings.defaultDistributionPriority});
            }
        }
        
       loadState.ValueRW=false;
    }
}