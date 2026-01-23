using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]

[UpdateAfter(typeof(ClusterAssignSystem))]
[BurstCompile]

public partial struct BuildingConfigManagerSystem : ISystem
{
    EntityQuery _changeRecipeQuery;
    EntityQuery _markAsDemolitionQuery;
    EntityQuery _markAsForceDestoryQuery;
    EntityQuery _addStorageSlotQuery;
    EntityQuery _removeStorageSlotQuery;
    EntityQuery _changeConstructionPriotiyQuery;
    EntityQuery _changeCraftPriotiyQuery;

    EntityQuery _changeConstructionBuildingAccessQuery;
    EntityQuery _changeProcessorBuildingAccessData;
    EntityQuery _changeStorageSlotAccessData;
    EntityQuery _changeStorageSlotCapacityData;
    EntityQuery _changeCountOfPackData;
    
    BuildingConfigReference _buildingConfigs;
    RecipeConfigRefernce _recipeConfig;
    public void OnCreate(ref SystemState state)
    {
        _changeRecipeQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ChangeBuildingData,SetRecipeData>()
            .Build(ref state);
            
        _markAsDemolitionQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ChangeBuildingData,MarkAsDemolitionData>()
            .Build(ref state);

        _markAsForceDestoryQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ChangeBuildingData,MarkAsForceDestoroyData>()
            .Build(ref state);

        _addStorageSlotQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ChangeBuildingData,AddStorageSlotData>()
            .Build(ref state);

        _removeStorageSlotQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ChangeBuildingData,RemoveStorageSlotData>()
            .Build(ref state);

         _changeConstructionPriotiyQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ChangeBuildingData,ChangeConstructionPriotiyData>()
            .Build(ref state);  

         _changeCraftPriotiyQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ChangeBuildingData,ChangeCraftPriotiyData>()
            .Build(ref state);  

        _changeConstructionBuildingAccessQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ChangeBuildingData,ChangeConstructionBuildingAccessData>()
            .Build(ref state);  

        _changeProcessorBuildingAccessData = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ChangeBuildingData,ChangeProcessorBuildingAccessData>()
            .Build(ref state); 

        _changeStorageSlotAccessData  = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ChangeBuildingData,ChangeStorageSlotAccessData>()
            .Build(ref state); 

        _changeStorageSlotCapacityData = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ChangeBuildingData,ChangeStorageSlotCapacityData>()
            .Build(ref state); 

        _changeCountOfPackData=  new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ChangeBuildingData,ChangeCountOfPackData>()
            .Build(ref state); 

        if (SystemAPI.TryGetSingleton<BuildingConfigReference>(out var blib))
        {
            _buildingConfigs = blib;
        }
        if (SystemAPI.TryGetSingleton<RecipeConfigRefernce>(out var rlib))
        {
            _recipeConfig = rlib;
        }
        
    }
    public void OnUpdate(ref SystemState state)
    {

        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
         var ecbParallel = ecb.AsParallelWriter();
        var StorageSlotDataLookup= SystemAPI.GetBufferLookup<StorageSlotData>(false);
        var ConstructionPriorityDataLookup= SystemAPI.GetComponentLookup<ConstructionPriorityData>(false);
        var CraftingPriorityDataLookup= SystemAPI.GetComponentLookup<CraftingPriorityData>(false);
        var ExceessBufferLookup= SystemAPI.GetBufferLookup<ExcessSlotData>(false);
        var InputBufferLookup= SystemAPI.GetBufferLookup<InputSlotData>(false);
        var OutputBufferLookup= SystemAPI.GetBufferLookup<OutputSlotData>(false);
        var BuildingDataLookup= SystemAPI.GetComponentLookup<BuildingData>(true);
        var CountOfPackBuildingDataLookup= SystemAPI.GetComponentLookup<CountOfPackInBuildingData>(false);
        var RecipeBuildingDataLookup= SystemAPI.GetComponentLookup<RecipeBuildingData>(false);
        if(!_changeRecipeQuery.IsEmptyIgnoreFilter)
            state.Dependency= new AssignRecipeJob{RecipesConfig=_recipeConfig.RecipesConfig,
                                                     BuildingProcessionStructConfig=_buildingConfigs.BuildingProcessionStructConfigs,
                                                    RecipeBuildingDataLookup=RecipeBuildingDataLookup,
                                                    BuildingDataLookup=BuildingDataLookup,
                                                    CountOfPackBuildingDataLookup=CountOfPackBuildingDataLookup,
                                                    InputSlotDataLookup=InputBufferLookup,
                                                    OutputSlotDataLookup=OutputBufferLookup,
                                                    ExcessSlotDataLookup=ExceessBufferLookup,

                                                    ECB=ecbParallel}.Schedule(state.Dependency);
        
        if(!_markAsDemolitionQuery.IsEmptyIgnoreFilter)
            state.Dependency= new MarkAsDemolitionJob{ ECB=ecbParallel}.Schedule(state.Dependency); 

        if(!_markAsForceDestoryQuery.IsEmptyIgnoreFilter)
            state.Dependency= new MarkAsForceDestoryJob{ECB=ecbParallel}.Schedule(state.Dependency);

        if(!_addStorageSlotQuery.IsEmptyIgnoreFilter)
            state.Dependency= new AddStorageSlotJob{ECB=ecbParallel,StorageSlotDataLookup= StorageSlotDataLookup}.Schedule(state.Dependency);

        if(!_removeStorageSlotQuery.IsEmptyIgnoreFilter)
            state.Dependency= new RemoveStorageSlotJob{ECB=ecbParallel,StorageSlotDataLookup= StorageSlotDataLookup}.Schedule(state.Dependency);
        
        if(!_changeConstructionPriotiyQuery.IsEmptyIgnoreFilter)
            state.Dependency= new ChangeConstructionPriorityJob{ECB=ecbParallel,ConstructionPriorityDataLookup= ConstructionPriorityDataLookup}.Schedule(state.Dependency);

        if(!_changeCraftPriotiyQuery.IsEmptyIgnoreFilter)
            state.Dependency= new ChangeCraftPriorityJob{ECB=ecbParallel,CraftingPriorityDataLookup= CraftingPriorityDataLookup}.Schedule(state.Dependency);

        if(!_changeConstructionBuildingAccessQuery.IsEmptyIgnoreFilter)
            state.Dependency= new ChangeConstructionBuildingAccessDataJob{ECB=ecbParallel,ConstructionPriorityDataLookup= ConstructionPriorityDataLookup}.Schedule(state.Dependency);

        if(!_changeProcessorBuildingAccessData.IsEmptyIgnoreFilter)
            state.Dependency= new ChangeProcessorBuildingAccessDataJob{ECB=ecbParallel,CraftingPriorityDataLookup= CraftingPriorityDataLookup}.Schedule(state.Dependency);
        
        if(!_changeStorageSlotAccessData.IsEmptyIgnoreFilter)
            state.Dependency= new ChangeStorageSlotAccessDataJob{ECB=ecbParallel,StorageSlotDataLookup= StorageSlotDataLookup}.Schedule(state.Dependency);

        if(!_changeStorageSlotCapacityData.IsEmptyIgnoreFilter)
            state.Dependency= new ChangeStorageSlotCapacityDataJob{ECB=ecbParallel,StorageSlotDataLookup= StorageSlotDataLookup,ExcessSlotDataLookup=ExceessBufferLookup}.Schedule(state.Dependency);
      
        if(!_changeCountOfPackData.IsEmptyIgnoreFilter)
            state.Dependency= new ChangeCountOfPackDataJob{
                ECB=ecbParallel,
                RecipesConfig=_recipeConfig.RecipesConfig,
                CountOfPackBuildingDataLookup=CountOfPackBuildingDataLookup,
                RecipeBuildingDataLookup=RecipeBuildingDataLookup,
                InputSlotDataLookup=InputBufferLookup,
                OutputSlotDataLookup=OutputBufferLookup,
                ExcessSlotDataLookup=ExceessBufferLookup}.Schedule(state.Dependency);
    }
    [BurstCompile]
    public partial struct AssignRecipeJob : IJobEntity
    {
        [ReadOnly] public BlobAssetReference<BlobLibrary<RecipeStructConfig>> RecipesConfig;
        [ReadOnly] public BlobAssetReference<BlobLibrary<BuildingProcessionStructConfig>> BuildingProcessionStructConfig;
        public ComponentLookup<RecipeBuildingData> RecipeBuildingDataLookup;
        [ReadOnly] public ComponentLookup<BuildingData> BuildingDataLookup;
        [ReadOnly] public ComponentLookup<CountOfPackInBuildingData> CountOfPackBuildingDataLookup;
        public BufferLookup<InputSlotData> InputSlotDataLookup;
        public BufferLookup<OutputSlotData> OutputSlotDataLookup;
        public BufferLookup<ExcessSlotData> ExcessSlotDataLookup;
        
        public EntityCommandBuffer.ParallelWriter ECB; 

        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in SetRecipeData recipeData)
        {
            if (RecipeBuildingDataLookup.HasComponent(changeBuildingData.targetEntity))
            {
                
                var excessSlots=ECB.SetBuffer<ExcessSlotData>(sortKey, changeBuildingData.targetEntity);
                var ex =ExcessSlotDataLookup[changeBuildingData.targetEntity];
                if (recipeData.RecipeID!=-1&&RecipesConfig.Value.TryGetConfig(recipeData.RecipeID, out var res))
                {
                    if(BuildingProcessionStructConfig.Value.TryGetConfig(BuildingDataLookup[changeBuildingData.targetEntity].BuildingIDHash,out var building))
                    {
                        bool CanCraftRecipe=false;
                        for(int i = 0; i < building.requiredRecipesGroups.Length; i++)
                        {
                            if(res.RecipesGroups.Contains(building.requiredRecipesGroups[i]))
                            {
                                CanCraftRecipe=true;
                                break;
                            }
                        }
                        if (CanCraftRecipe)
                        {
                            int CountOfPack=CountOfPackBuildingDataLookup[changeBuildingData.targetEntity].CountOfPack;
                            
                            if (res.InputItems.Length > 0)
                            {
                                var input=ECB.SetBuffer<InputSlotData>(sortKey, changeBuildingData.targetEntity);
                                for(int j = 0; j < res.InputItems.Length; j++)
                                {
                                    int max =res.InputItems[j].Amount*CountOfPack;
                                    input.Add(new InputSlotData{ItemId=res.InputItems[j].ItemId,Amount=0,Capacity=max});
                                }

                                for(int i = 0; i < ex.Length; i++)
                                {
                                    var exS=ex[i];
                                    if(exS.Amount==0) continue;
                                    for(int j = 0; j < res.InputItems.Length; j++)
                                    {
                                        if(exS.Amount==0) break;
                                        
                                        if (exS.ItemId == res.InputItems[j].ItemId)
                                        {
                                            var inputData=input[j];
                                            int max =res.InputItems[j].Amount*CountOfPack;
                                            if (exS.Amount > max)
                                            {
                                                inputData.Amount=max;
                                                exS.Amount-=max;
                                            }
                                            else
                                            {
                                                inputData.Amount=exS.Amount;
                                                exS.Amount=0;
                                            }
                                            input[j]=inputData;
                                        }
                                        
                                    }
                                    ex[i]=exS;
                                }
                            }
                            if (res.OutputItems.Length > 0)
                            {
                                var output=ECB.SetBuffer<OutputSlotData>(sortKey, changeBuildingData.targetEntity);
                                for(int j = 0; j < res.OutputItems.Length; j++)
                                {
                                     int max =res.OutputItems[j].Amount*CountOfPack;
                                    output.Add(new OutputSlotData{ItemId=res.OutputItems[j].ItemId,Amount=0,Capacity=max});
                                }
                                for(int i = 0; i < ex.Length; i++)
                                {
                                    var exS=ex[i];
                                    if(exS.Amount==0) continue;
                                    for(int j = 0; j < res.OutputItems.Length; j++)
                                    {
                                        if(exS.Amount==0) break;
                                        if (exS.ItemId == res.OutputItems[j].ItemId)
                                        {
                                            var outputData=output[j];
                                            int max =res.OutputItems[j].Amount*CountOfPack;
                                            if (exS.Amount > max)
                                            {
                                                outputData.Amount=max;
                                                exS.Amount-=max;
                                            }
                                            else
                                            {
                                                outputData.Amount=exS.Amount;
                                                exS.Amount=0;
                                            }
                                            
                                            output[j]=outputData;
                                        }
                                    }
                                    ex[i]=exS;
                                }
                            }
                            
                            var data = new RecipeBuildingData { RecipeIDHash = recipeData.RecipeID };
                            data.TimeToCraft = res.CraftTime;
                            data.CurrTime = 0;
                            ECB.SetComponent(sortKey, changeBuildingData.targetEntity, data);
                            ECB.SetComponentEnabled<IsRecipeAssigned>(sortKey, changeBuildingData.targetEntity, true);
                        }
                       
                    }
                   
                }
                else
                {
                    if (InputSlotDataLookup.HasBuffer(changeBuildingData.targetEntity))
                    {
                        var input =InputSlotDataLookup[changeBuildingData.targetEntity];
                        for(int i = 0; i < input.Length; i++)
                        {
                            var inputData =input[i];
                            if(inputData.Amount<1) continue;
                            for(int j = 0; j < ex.Length; j++)
                            {
                                if(inputData.Amount<1) break;
                                var exS=ex[j];
                                if(exS.Amount==exS.Capacity) continue;
                                if (exS.ItemId == inputData.ItemId)
                                {
                                    int fillSpace=exS.Capacity-exS.Amount;
                                    if (inputData.Amount > fillSpace)
                                    {
                                        inputData.Amount-=fillSpace;
                                        exS.Amount=exS.Capacity;
                                        do
                                        {
                                            int add=inputData.Amount>100?100:inputData.Amount;
                                            excessSlots.Add(new ExcessSlotData{ItemId=inputData.ItemId,Amount=add,Capacity=100});
                                            inputData.Amount-=add;
                                        }
                                        while(inputData.Amount>0);
                                    }
                                    else
                                    {
                                        exS.Amount+=inputData.Amount;
                                        inputData.Amount=0;
                                    }
                                }
                                ex[j]=exS;
                            }
                        }
                        ECB.SetBuffer<InputSlotData>(sortKey,changeBuildingData.targetEntity);
                    }
                    if (OutputSlotDataLookup.HasBuffer(changeBuildingData.targetEntity))
                    {
                        var output =OutputSlotDataLookup[changeBuildingData.targetEntity];
                        for(int i = 0; i < output.Length; i++)
                        {
                            var outputData =output[i];
                            if(outputData.Amount<1) continue;
                            for(int j = 0; j < ex.Length; j++)
                            {
                                if(outputData.Amount<1) break;
                                var exS=ex[j];
                                 if(exS.Amount==exS.Capacity) continue;
                                if (exS.ItemId == outputData.ItemId)
                                {
                                    int fillSpace=exS.Capacity-exS.Amount;
                                    if (outputData.Amount > fillSpace)
                                    {
                                        outputData.Amount-=fillSpace;
                                        exS.Amount=exS.Capacity;
                                        do
                                        {
                                            int add=outputData.Amount>100?100:outputData.Amount;
                                            excessSlots.Add(new ExcessSlotData{ItemId=outputData.ItemId,Amount=add,Capacity=100});
                                            outputData.Amount-=add;
                                        }
                                        while(outputData.Amount>0);
                                    }
                                    else
                                    {
                                        exS.Amount+=outputData.Amount;
                                        outputData.Amount=0;
                                    }
                                }
                                ex[j]=exS;
                            }
                        }
                        ECB.SetBuffer<OutputSlotData>(sortKey,changeBuildingData.targetEntity);
                    }
                    
                    var data = new RecipeBuildingData { RecipeIDHash = recipeData.RecipeID};
                    ex.Clear();
                    data.TimeToCraft = 0;
                    data.CurrTime = 0;
                    ECB.SetComponentEnabled<IsRecipeAssigned>(sortKey, changeBuildingData.targetEntity, false);
                }
                foreach(var exS in ex)
                {
                    if(exS.Amount>0)
                        excessSlots.Add(new ExcessSlotData{ItemId=exS.ItemId,Amount=exS.Amount,Capacity=100});
                }

            }
            
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    [BurstCompile]
    public partial struct MarkAsDemolitionJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 

        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in MarkAsDemolitionData markAsDemolition)
        {
            ECB.SetComponentEnabled<ChangeDemolitionStateTag>(sortKey,changeBuildingData.targetEntity,markAsDemolition.IsDemolition);
            
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    [BurstCompile]
    public partial struct MarkAsForceDestoryJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 

        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in MarkAsForceDestoroyData markAsForceDestoroyData)
        {
            ECB.SetComponentEnabled<ChangeDemolitionStateTag>(sortKey,changeBuildingData.targetEntity,true);
            
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    [BurstCompile]
    public partial struct AddStorageSlotJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 
        public BufferLookup<StorageSlotData> StorageSlotDataLookup;
        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in AddStorageSlotData addStorageSlotData)
        {
            if(StorageSlotDataLookup.TryGetBuffer(changeBuildingData.targetEntity,out var buff))
            {
                if (buff.Length < buff.Capacity)
                {
                    ECB.AppendToBuffer(sortKey, changeBuildingData.targetEntity, new StorageSlotData { 
                            ItemId = addStorageSlotData.ItemID, 
                            Amount = 0,
                            Capacity=addStorageSlotData.Capacity,
                            IsInputEnabled=true,
                            IsOutputEnabled=true,
                    });
                }
            }
            
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    [BurstCompile]
    public partial struct RemoveStorageSlotJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 
        public BufferLookup<StorageSlotData> StorageSlotDataLookup;

        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in RemoveStorageSlotData removeStorageSlotData)
        {
            if (StorageSlotDataLookup.HasBuffer(changeBuildingData.targetEntity))
            {
                var prevBuff = StorageSlotDataLookup[changeBuildingData.targetEntity];
                
                var newBuffer = ECB.SetBuffer<StorageSlotData>(sortKey, changeBuildingData.targetEntity);
                
                if (removeStorageSlotData.slotIND >= 0 && removeStorageSlotData.slotIND < prevBuff.Length)
                {
                    for (int i = 0; i < prevBuff.Length; i++)
                    {
                        if (i != removeStorageSlotData.slotIND)
                            newBuffer.Add(prevBuff[i]);
                    }
                }
            }
            ECB.DestroyEntity(sortKey, entity);
        }
    }


    [BurstCompile]
    public partial struct ChangeConstructionPriorityJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 
        
         [ReadOnly] public ComponentLookup<ConstructionPriorityData> ConstructionPriorityDataLookup;
        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in ChangeConstructionPriotiyData changeConstructionPriotiyData)
        {
            if (ConstructionPriorityDataLookup.IsComponentEnabled(changeBuildingData.targetEntity))
            {
                ECB.SetComponent(sortKey,changeBuildingData.targetEntity,new ConstructionPriorityData{ConstructionPriority=changeConstructionPriotiyData.newPriority});
            }
            
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    [BurstCompile]
    public partial struct ChangeCraftPriorityJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 
        
         [ReadOnly] public ComponentLookup<CraftingPriorityData> CraftingPriorityDataLookup;
        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in ChangeCraftPriotiyData craftPriotiyData)
        {
            if (CraftingPriorityDataLookup.HasComponent(changeBuildingData.targetEntity))
            {
                ECB.SetComponent(sortKey,changeBuildingData.targetEntity,new CraftingPriorityData{CraftingPriority=craftPriotiyData.newPriority});
            }
            
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    [BurstCompile]
    public partial struct ChangeConstructionBuildingAccessDataJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 
        [ReadOnly] public ComponentLookup<ConstructionPriorityData> ConstructionPriorityDataLookup;
        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in ChangeConstructionBuildingAccessData changeConstructionPriotiyData)
        {
            if (ConstructionPriorityDataLookup.IsComponentEnabled(changeBuildingData.targetEntity))
            {
                if(changeConstructionPriotiyData.IsInput)
                    ECB.SetComponentEnabled<IsInputConstructionEnabled>(sortKey,changeBuildingData.targetEntity,changeConstructionPriotiyData.IsEnabled);
                else
                    ECB.SetComponentEnabled<IsOutputConstuctionEnabled>(sortKey,changeBuildingData.targetEntity,changeConstructionPriotiyData.IsEnabled);
            }
            
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    [BurstCompile]
    public partial struct ChangeProcessorBuildingAccessDataJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 
        
        [ReadOnly] public ComponentLookup<CraftingPriorityData> CraftingPriorityDataLookup;
        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in ChangeProcessorBuildingAccessData changeProcessorBuildingAccessData)
        {
            if (CraftingPriorityDataLookup.HasComponent(changeBuildingData.targetEntity))
            {
                if(changeProcessorBuildingAccessData.IsInput)
                    ECB.SetComponentEnabled<IsInputCraftEnabled>(sortKey,changeBuildingData.targetEntity,changeProcessorBuildingAccessData.IsEnabled);
                else
                    ECB.SetComponentEnabled<IsOutputCraftEnabled>(sortKey,changeBuildingData.targetEntity,changeProcessorBuildingAccessData.IsEnabled);
            }
            
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    [BurstCompile]
    public partial struct ChangeStorageSlotAccessDataJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 
        public BufferLookup<StorageSlotData> StorageSlotDataLookup;
        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in ChangeStorageSlotAccessData storageSlotAccessData)
        {
            if(StorageSlotDataLookup.TryGetBuffer(changeBuildingData.targetEntity,out var buff))
            {
                if (storageSlotAccessData.SlotIND >= 0 && storageSlotAccessData.SlotIND < buff.Length)
                {
                    var data = buff[storageSlotAccessData.SlotIND];
                    if(storageSlotAccessData.IsInput)
                        data.IsInputEnabled=storageSlotAccessData.IsEnabled;
                    else
                        data.IsOutputEnabled=storageSlotAccessData.IsEnabled;
                    buff[storageSlotAccessData.SlotIND]=data;
                }
            }
            
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    [BurstCompile]
    public partial struct ChangeStorageSlotCapacityDataJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 
        public BufferLookup<StorageSlotData> StorageSlotDataLookup;
        public BufferLookup<ExcessSlotData> ExcessSlotDataLookup;
        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in ChangeStorageSlotCapacityData storageSlotCapacityData)
        {
            if(StorageSlotDataLookup.TryGetBuffer(changeBuildingData.targetEntity,out var buff))
            {
                if (storageSlotCapacityData.SlotIND >= 0 && storageSlotCapacityData.SlotIND < buff.Length)
                {
                    
                    var exLookup=ExcessSlotDataLookup[changeBuildingData.targetEntity];
                    var excessECB=ECB.SetBuffer<ExcessSlotData>(sortKey,changeBuildingData.targetEntity);
                    var data = buff[storageSlotCapacityData.SlotIND];
                    
                    data.Capacity=storageSlotCapacityData.newCapacity;
                    if (data.Capacity > storageSlotCapacityData.newCapacity)
                    {
                        data.Capacity=storageSlotCapacityData.newCapacity;
                        for(int i = 0; i < exLookup.Length; i++)
                        {
                            var exS=exLookup[i];
                            if(data.Amount==data.Capacity) break;
                            if(exS.Amount==0) continue;
                            if (exS.ItemId == data.ItemId)
                            {
                                var fillSpace=data.Capacity-data.Amount;
                                if (exS.Amount >= fillSpace)
                                {
                                    data.Amount=data.Capacity;
                                    exS.Amount-=fillSpace;
                                }
                                else
                                {
                                    data.Amount+=exS.Amount;
                                    exS.Amount=0;
                                }
                            }
                            exLookup[i]=exS;
                        }
                    }
                    else
                    {
                        int amount=data.Capacity-storageSlotCapacityData.newCapacity;
                        for(int i = 0; i < exLookup.Length; i++)
                        {
                            var exSlot=exLookup[i];
                            
                            if(amount==0) break;
                            if(exSlot.Amount==exSlot.Capacity) continue;
                            if (exSlot.ItemId == data.ItemId)
                            {
                                if(exSlot.Amount==exSlot.Capacity) continue;
                                var fillSpace=exSlot.Capacity-exSlot.Amount;
                                if (amount >= fillSpace)
                                {
                                    exSlot.Amount=exSlot.Capacity;
                                    amount-=fillSpace;
                                }
                                else
                                {
                                    exSlot.Amount+=amount;
                                    amount=0;
                                }
                            }
                            exLookup[i]=exSlot;
                        }
                    }
                    foreach(var exS in exLookup)
                    {
                        if(exS.Amount>0)
                            excessECB.Add(new ExcessSlotData{ItemId=exS.ItemId,Amount=exS.Amount,Capacity=100});
                    }
                    buff[storageSlotCapacityData.SlotIND]=data;
                    
                }
            }
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    [BurstCompile]
    public partial struct ChangeCountOfPackDataJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 
        [ReadOnly] public BlobAssetReference<BlobLibrary<RecipeStructConfig>> RecipesConfig;
        [ReadOnly] public ComponentLookup<CountOfPackInBuildingData> CountOfPackBuildingDataLookup;
        [ReadOnly] public ComponentLookup<RecipeBuildingData> RecipeBuildingDataLookup;
        [ReadOnly] public BufferLookup<InputSlotData> InputSlotDataLookup;
        [ReadOnly] public BufferLookup<OutputSlotData> OutputSlotDataLookup;
        [ReadOnly] public BufferLookup<ExcessSlotData> ExcessSlotDataLookup;
        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in ChangeCountOfPackData changeCountOfPackData)
        {
            
            int CountOfPack=CountOfPackBuildingDataLookup[changeBuildingData.targetEntity].CountOfPack;
            if (RecipeBuildingDataLookup.HasComponent(changeBuildingData.targetEntity))
            {
                var recipeData=RecipeBuildingDataLookup[changeBuildingData.targetEntity];
                if (recipeData.RecipeIDHash!=-1&&RecipesConfig.Value.TryGetConfig(recipeData.RecipeIDHash   , out var res))
                {
                    var excessECB=ECB.SetBuffer<ExcessSlotData>(sortKey, changeBuildingData.targetEntity);
                    var exLookup =ExcessSlotDataLookup[changeBuildingData.targetEntity];
                    if (CountOfPack < changeCountOfPackData.newCapacity)
                    {
                        if (InputSlotDataLookup.HasBuffer(changeBuildingData.targetEntity))
                        {
                            var inputECB=ECB.SetBuffer<InputSlotData>(sortKey, changeBuildingData.targetEntity);
                            var inputLookup =InputSlotDataLookup[changeBuildingData.targetEntity];
                            for(int i = 0; i < exLookup.Length; i++)
                            {
                                var exS=exLookup[i];
                                if(exS.Amount==0) continue;
                                for(int j = 0; j < res.InputItems.Length; j++)
                                {
                                    if(exS.Amount==0) break;
                                    if (exS.ItemId == res.InputItems[j].ItemId)
                                    {
                                        var inputData=inputLookup[j];
                                        if(inputData.Amount==inputData.Capacity) continue;
                                        int max =res.InputItems[j].Amount*CountOfPack;
                                        if (exS.Amount > max)
                                        {
                                            inputData.Amount=max;
                                            exS.Amount-=max;
                                        }
                                        else
                                        {
                                            inputData.Amount=exS.Amount;
                                            exS.Amount=0;
                                        }
                                        inputLookup[j]=inputData;
                                    }
                                    
                                }
                                exLookup[i]=exS;
                            }
                            foreach(var inL in inputLookup)
                                inputECB.Add(inL);
                        }
                        if (OutputSlotDataLookup.HasBuffer(changeBuildingData.targetEntity))
                        {
                            var outputECB=ECB.SetBuffer<OutputSlotData>(sortKey, changeBuildingData.targetEntity);
                            var outputLookup =OutputSlotDataLookup[changeBuildingData.targetEntity];
                            for(int i = 0; i < exLookup.Length; i++)
                            {
                                var exS=exLookup[i];
                                if(exS.Amount==0) continue;
                                for(int j = 0; j < res.InputItems.Length; j++)
                                {
                                    if(exS.Amount==0) break;
                                    if (exS.ItemId == res.InputItems[j].ItemId)
                                    {   
                                        var outputData=outputLookup[j];
                                        if(outputData.Amount==outputData.Capacity) continue;
                                        int max =res.InputItems[j].Amount*CountOfPack;
                                        if (exS.Amount > max)
                                        {
                                            outputData.Amount=max;
                                            exS.Amount-=max;
                                        }
                                        else
                                        {
                                            outputData.Amount=exS.Amount;
                                            exS.Amount=0;
                                        }
                                        outputLookup[j]=outputData;
                                    }
                                    
                                }
                                exLookup[i]=exS;
                            }
                            foreach(var inL in outputLookup)
                                outputECB.Add(inL);
                        }
                        
                    }
                    else
                    {
                        if (InputSlotDataLookup.HasBuffer(changeBuildingData.targetEntity))
                        {
                            var input =InputSlotDataLookup[changeBuildingData.targetEntity];
                            for(int i = 0; i < input.Length; i++)
                            {
                                var inputData =input[i];
                                int max=CountOfPack* res.InputItems[i].Amount;
                                if(inputData.Amount<max) continue;
                                for(int j = 0; j < exLookup.Length; j++)
                                {
                                    if(inputData.Amount<max) break;
                                    var exS=exLookup[j];
                                    if(exS.Amount==exS.Capacity) continue;
                                    if (exS.ItemId == inputData.ItemId)
                                    {
                                        int remain=inputData.Amount-max;
                                        if(remain==0) break;
                                        inputData.Amount=max;
                                        int fillSpace=exS.Capacity-exS.Amount;
                                        if (remain> fillSpace)
                                        {
                                            remain-=fillSpace;
                                            exS.Amount=exS.Capacity;
                                            do
                                            {
                                                int add=remain>100?100:remain;
                                                excessECB.Add(new ExcessSlotData{ItemId=inputData.ItemId,Amount=add,Capacity=100});
                                                remain-=add;
                                            }
                                            while(remain>0);
                                        }
                                        else
                                        {
                                            exS.Amount+=remain;
                                        }
                                    }
                                    exLookup[j]=exS;
                                }
                            }
                            var inputECB=ECB.SetBuffer<InputSlotData>(sortKey,changeBuildingData.targetEntity);
                            foreach( var inp in input)
                            {
                                inputECB.Add(inp);
                            }
                        }
                        if (OutputSlotDataLookup.HasBuffer(changeBuildingData.targetEntity))
                        {
                            var output =OutputSlotDataLookup[changeBuildingData.targetEntity];
                            for(int i = 0; i < output.Length; i++)
                            {
                                var outputData =output[i];
                                int max=CountOfPack* res.OutputItems[i].Amount;
                                if(outputData.Amount<max) continue;
                                for(int j = 0; j < exLookup.Length; j++)
                                {
                                    if(outputData.Amount<max) break;
                                    var exS=exLookup[j];
                                    if(exS.Amount==exS.Capacity) continue;
                                    if (exS.ItemId == outputData.ItemId)
                                    {
                                        int remain=outputData.Amount-max;
                                        if(remain==0) break;
                                        outputData.Amount=max;
                                        int fillSpace=exS.Capacity-exS.Amount;
                                        if (remain> fillSpace)
                                        {
                                            remain-=fillSpace;
                                            exS.Amount=exS.Capacity;
                                            do
                                            {
                                                int add=remain>100?100:remain;
                                                excessECB.Add(new ExcessSlotData{ItemId=outputData.ItemId,Amount=add,Capacity=100});
                                                remain-=add;
                                            }
                                            while(remain>0);
                                        }
                                        else
                                        {
                                            exS.Amount+=remain;
                                        }
                                    }
                                    exLookup[j]=exS;
                                }
                            }
                            var outPutECB=ECB.SetBuffer<OutputSlotData>(sortKey,changeBuildingData.targetEntity);
                            foreach( var outp in output)
                            {
                                outPutECB.Add(outp);
                            }
                        }
                    }
                    foreach(var exS in exLookup)
                    {
                        if(exS.Amount>0)
                            excessECB.Add(new ExcessSlotData{ItemId=exS.ItemId,Amount=exS.Amount,Capacity=100});
                    }
                }
                ECB.SetComponent(sortKey,changeBuildingData.targetEntity,new CountOfPackInBuildingData{CountOfPack=changeCountOfPackData.newCapacity});
            }
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    
}