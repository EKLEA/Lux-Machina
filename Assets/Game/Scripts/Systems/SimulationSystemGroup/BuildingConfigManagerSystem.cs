
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]

[UpdateAfter(typeof(ClusterAssignSystem))]
[BurstCompile]

public partial struct BuildingConfigManagerSystem : ISystem
{
    EntityQuery _changeRecipeQuery;
    EntityQuery _changeDemolitionQuery;
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
            
        _changeDemolitionQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BuildingData,ChangeDemolitionStateTag>()
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
        var MapPointLookup= SystemAPI.GetBufferLookup<MapPoint>(false);

        
        var IsDemolitionLookup= SystemAPI.GetComponentLookup<IsDemolition>(false);
        var IsBlueprintLookup= SystemAPI.GetComponentLookup<IsBlueprint>(false);
        var RoadTypeLookup= SystemAPI.GetComponentLookup<RoadTypeBuildingTag>(false);
        var HealthLookup= SystemAPI.GetComponentLookup<HealthData>(false);

        var buildingCache = SystemAPI.GetSingleton<BuildingConfigReference>();
        var itemRequestRef = buildingCache.BuildingItemRequestsStructConfigs; 
        var mapEntity= SystemAPI.GetSingletonEntity<ClusterMap>();
        
        var InputConstLookup= SystemAPI.GetBufferLookup<InputConstructionSlotData>(false);
        var OutputConstLookup= SystemAPI.GetBufferLookup<OutputConstructionSlotData>(false);
        if(!_changeRecipeQuery.IsEmpty)
            state.Dependency= new AssignRecipeJob{RecipesConfig=_recipeConfig.RecipesConfig,
                                                     BuildingProcessionStructConfig=_buildingConfigs.BuildingProcessionStructConfigs,
                                                    RecipeBuildingDataLookup=RecipeBuildingDataLookup,
                                                    BuildingDataLookup=BuildingDataLookup,
                                                    CountOfPackBuildingDataLookup=CountOfPackBuildingDataLookup,
                                                    InputSlotDataLookup=InputBufferLookup,
                                                    OutputSlotDataLookup=OutputBufferLookup,
                                                    ExcessSlotDataLookup=ExceessBufferLookup,
                                                    mapEntity=mapEntity,
                                                    ECB=ecbParallel}.Schedule(state.Dependency);
        
        if(!_changeDemolitionQuery.IsEmpty)
            state.Dependency= new ChangeDemolitionJob{ ECB=ecbParallel,
                                                        IsDemolitionLookup=IsDemolitionLookup,
                                                        IsBlueprintLookup=IsBlueprintLookup,
                                                        BuildingDataLookup=BuildingDataLookup,
                                                        RoadTypeLookup=RoadTypeLookup,
                                                        HealthLookup=HealthLookup,
                                                        ItemRequestsConfig=itemRequestRef,
                                                        OutputConstLookup=OutputConstLookup,
                                                        InputConstLookup=InputConstLookup,
                                                        MapPointLookup=MapPointLookup}.Schedule(state.Dependency); 


        if(!_markAsForceDestoryQuery.IsEmpty)
            state.Dependency= new MarkAsForceDestoryJob{ECB=ecbParallel}.Schedule(state.Dependency);

        if(!_addStorageSlotQuery.IsEmpty)
            state.Dependency= new AddStorageSlotJob{ECB=ecbParallel,StorageSlotDataLookup= StorageSlotDataLookup,mapEntity=mapEntity,
                                                    ExcessSlotDataLookup=ExceessBufferLookup}.Schedule(state.Dependency);

        if(!_removeStorageSlotQuery.IsEmpty)
            state.Dependency= new RemoveStorageSlotJob{ECB=ecbParallel,StorageSlotDataLookup= StorageSlotDataLookup,mapEntity=mapEntity,
                                                    ExcessSlotDataLookup=ExceessBufferLookup}.Schedule(state.Dependency);
        
        if(!_changeConstructionPriotiyQuery.IsEmpty)
            state.Dependency= new ChangeConstructionPriorityJob{ECB=ecbParallel,ConstructionPriorityDataLookup= ConstructionPriorityDataLookup, mapEntity=mapEntity}.Schedule(state.Dependency);

        if(!_changeCraftPriotiyQuery.IsEmpty)
            state.Dependency= new ChangeCraftPriorityJob{ECB=ecbParallel,CraftingPriorityDataLookup= CraftingPriorityDataLookup, mapEntity=mapEntity}.Schedule(state.Dependency);

        if(!_changeConstructionBuildingAccessQuery.IsEmpty)
            state.Dependency= new ChangeConstructionBuildingAccessDataJob{ECB=ecbParallel,ConstructionPriorityDataLookup= ConstructionPriorityDataLookup, mapEntity=mapEntity}.Schedule(state.Dependency);

        if(!_changeProcessorBuildingAccessData.IsEmpty)
            state.Dependency= new ChangeProcessorBuildingAccessDataJob{ECB=ecbParallel,CraftingPriorityDataLookup= CraftingPriorityDataLookup, mapEntity=mapEntity}.Schedule(state.Dependency);
        
        if(!_changeStorageSlotAccessData.IsEmpty)
            state.Dependency= new ChangeStorageSlotAccessDataJob{ECB=ecbParallel,StorageSlotDataLookup= StorageSlotDataLookup, mapEntity=mapEntity}.Schedule(state.Dependency);

        if(!_changeStorageSlotCapacityData.IsEmpty)
            state.Dependency= new ChangeStorageSlotCapacityDataJob{ECB=ecbParallel,StorageSlotDataLookup= StorageSlotDataLookup,ExcessSlotDataLookup=ExceessBufferLookup,mapEntity=mapEntity,}.Schedule(state.Dependency);
      
        if(!_changeCountOfPackData.IsEmpty)
            state.Dependency= new ChangeCountOfPackDataJob{
                ECB=ecbParallel,
                RecipesConfig=_recipeConfig.RecipesConfig,
                CountOfPackBuildingDataLookup=CountOfPackBuildingDataLookup,
                RecipeBuildingDataLookup=RecipeBuildingDataLookup,
                InputSlotDataLookup=InputBufferLookup,
                OutputSlotDataLookup=OutputBufferLookup,
                mapEntity=mapEntity,
                ExcessSlotDataLookup=ExceessBufferLookup}.Schedule(state.Dependency);
        // state.Dependency=new CleanExcess{mapEntity=mapEntity,ECB=ecbParallel}.ScheduleParallel(state.Dependency);
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
        public Entity mapEntity;
        
        public EntityCommandBuffer.ParallelWriter ECB; 

        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in SetRecipeData recipeData)
        {
            if (RecipeBuildingDataLookup.HasComponent(changeBuildingData.targetEntity))
            {
                var excessSlots=ECB.SetBuffer<ExcessSlotData>(sortKey, changeBuildingData.targetEntity);
                var ex =ExcessSlotDataLookup[changeBuildingData.targetEntity];
                var newRecipeData = new RecipeBuildingData { RecipeIDHash = recipeData.RecipeID,TimeToCraft = 0,CurrTime=0 };
                if (recipeData.RecipeID!=-1&&RecipesConfig.Value.TryGetConfig(recipeData.RecipeID, out var res))
                {
                    if(BuildingProcessionStructConfig.Value.TryGetConfig(BuildingDataLookup[changeBuildingData.targetEntity].BuildingIDHash,out var building))
                    {
                        bool CanCraftRecipe=false;
                        for (int i = 0; i < building.requiredRecipesGroups.Length; i++)
                        {   
                            int buildingNeededGroup = building.requiredRecipesGroups[i];
                            
                            for (int j = 0; j < res.RecipesGroups.Length; j++)
                            {
                                if (res.RecipesGroups[j] == buildingNeededGroup)
                                {
                                    CanCraftRecipe = true;
                                    break; 
                                }
                            }
                            
                            if (CanCraftRecipe) break;
                        }
                        if (CanCraftRecipe)
                        {
                            int CountOfPack=CountOfPackBuildingDataLookup[changeBuildingData.targetEntity].CountOfPack;
                            
                            if (res.InputItems.Length > 0)
                            {
                                var inputBuff=ECB.SetBuffer<InputSlotData>(sortKey,changeBuildingData.targetEntity);
                                for(int i=0;i< res.InputItems.Length; i++)
                                {
                                    
                                    int max =res.InputItems[i].Amount*CountOfPack;
                                    var data = new InputSlotData{ItemId=res.InputItems[i].ItemId,Amount=0,Capacity=max};
                                    for(int j = 0; j < ex.Length; j++)
                                    {
                                        var exS=ex[j];
                                        if(exS.Amount==0) continue;
                                            
                                        if (exS.ItemId == res.InputItems[i].ItemId)
                                        {
                                            int fillSpace = max-data.Amount;
                                            if (exS.Amount > fillSpace)
                                            {
                                                data.Amount=data.Capacity;
                                                exS.Amount-=fillSpace;
                                            }
                                            else
                                            {
                                                data.Amount=exS.Amount;
                                                exS.Amount=0;
                                            }
                                        }
                                        ex[j]=exS;
                                    }
                                    inputBuff.Add(data);
                                }
                            }
                            if (res.OutputItems.Length > 0)
                            {
                                var outputBuff=ECB.SetBuffer<OutputSlotData>(sortKey, changeBuildingData.targetEntity);
                                for(int i = 0; i < res.OutputItems.Length; i++)
                                {
                                    int max =res.OutputItems[i].Amount*CountOfPack;
                                    var data=new OutputSlotData{ItemId=res.OutputItems[i].ItemId,Amount=0,Capacity=max};
                                    for(int j = 0; j < ex.Length; j++)
                                    {
                                        var exS=ex[j];
                                        if(exS.Amount==0) continue;
                                        if (exS.ItemId == res.OutputItems[i].ItemId)
                                        {
                                            int fillSpace =max-data.Amount;
                                            if (exS.Amount > fillSpace)
                                            {
                                                data.Amount=data.Capacity;
                                                exS.Amount-=fillSpace;
                                            }
                                            else
                                            {
                                                data.Amount=exS.Amount;
                                                exS.Amount=0;
                                            }
                                        }
                                        ex[j]=exS;
                                    }
                                    
                                    outputBuff.Add(data);
                                }
                            }
                            newRecipeData.TimeToCraft=res.CraftTime;
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
                                var exS=ex[j];
                                if(exS.Amount==exS.Capacity) continue;
                                if (exS.ItemId == inputData.ItemId)
                                {
                                    int fillSpace=exS.Capacity-exS.Amount;
                                    if (inputData.Amount > fillSpace)
                                    {
                                        inputData.Amount-=fillSpace;
                                        exS.Amount=exS.Capacity;
                                    }
                                    else
                                    {
                                        exS.Amount+=inputData.Amount;
                                        inputData.Amount=0;
                                    }
                                }
                                ex[j]=exS;
                            }
                            if (inputData.Amount > 0)
                            {
                                
                                do
                                {
                                    int add=inputData.Amount>100?100:inputData.Amount;
                                    excessSlots.Add(new ExcessSlotData{ItemId=inputData.ItemId,Amount=add,Capacity=100});
                                    inputData.Amount-=add;
                                }
                                while(inputData.Amount>0);
                            }
                        }
                        input.Clear();
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
                                var exS=ex[j];
                                if(exS.Amount==exS.Capacity) continue;
                                if (exS.ItemId == outputData.ItemId)
                                {
                                    int fillSpace=exS.Capacity-exS.Amount;
                                    if (outputData.Amount > fillSpace)
                                    {
                                        outputData.Amount-=fillSpace;
                                        exS.Amount=exS.Capacity;
                                    }
                                    else
                                    {
                                        exS.Amount+=outputData.Amount;
                                        outputData.Amount=0;
                                    }
                                }
                                ex[j]=exS;
                            }
                            if (outputData.Amount > 0)
                            {
                                 do
                                {
                                    int add=outputData.Amount>100?100:outputData.Amount;
                                    excessSlots.Add(new ExcessSlotData{ItemId=outputData.ItemId,Amount=add,Capacity=100});
                                    outputData.Amount-=add;
                                }
                                while(outputData.Amount>0);
                            }
                        }
                        output.Clear();
                    }
                }
                foreach(var exS in ex)
                {
                    if(exS.Amount>0)
                        excessSlots.Add(new ExcessSlotData{ItemId=exS.ItemId,Amount=exS.Amount,Capacity=100});
                }
                ECB.SetComponentEnabled<UpdateClusterSlots>(sortKey,mapEntity,true);
                ECB.SetComponent<RecipeBuildingData>(sortKey, changeBuildingData.targetEntity, newRecipeData);
                ECB.SetComponentEnabled<IsRecipeAssigned>(sortKey, changeBuildingData.targetEntity, recipeData.RecipeID!=-1);
            }
            
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    [BurstCompile]
    public partial struct ChangeDemolitionJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 
        [ReadOnly] public ComponentLookup<IsDemolition> IsDemolitionLookup;
        [ReadOnly] public ComponentLookup<IsBlueprint> IsBlueprintLookup;
        [ReadOnly] public ComponentLookup<BuildingData> BuildingDataLookup;
        [ReadOnly] public ComponentLookup<RoadTypeBuildingTag> RoadTypeLookup;
        [ReadOnly] public ComponentLookup<HealthData> HealthLookup;
        [ReadOnly] public BlobAssetReference<BlobLibrary<BuildingItemRequestsStructConfig>> ItemRequestsConfig;
        [ReadOnly] public BufferLookup<OutputConstructionSlotData> OutputConstLookup;
        [ReadOnly] public BufferLookup<InputConstructionSlotData> InputConstLookup;
        [ReadOnly] public BufferLookup<MapPoint> MapPointLookup;
        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey,EnabledRefRO<ChangeDemolitionStateTag> demolutuinState)
        {
            if (IsDemolitionLookup.IsComponentEnabled(entity))
            {

                var outputBuff = OutputConstLookup[entity];
                ItemRequestsConfig.Value.TryGetConfig(BuildingDataLookup[entity].BuildingIDHash,out var itemRequest);

                var ecbInputBuff = ECB.SetBuffer<InputConstructionSlotData>(sortKey,entity);
                var ecbOutputBuff = ECB.SetBuffer<OutputConstructionSlotData>(sortKey,entity);

                for (int i = 0;i<itemRequest.itemsRequests.Length;i++)
                {
                    ecbInputBuff.Add( new InputConstructionSlotData
                    {
                        ItemId = itemRequest.itemsRequests[i].ItemId,
                        Capacity = itemRequest.itemsRequests[i].Amount,
                        Amount = outputBuff[i].Amount
                    });
                    ecbOutputBuff.Add( new OutputConstructionSlotData
                    {
                        ItemId = itemRequest.itemsRequests[i].ItemId,
                        Capacity = itemRequest.itemsRequests[i].Amount,
                        Amount = 0
                    });
                }
            }
            else
            {
                var inputBuff = InputConstLookup[entity];
                ItemRequestsConfig.Value.TryGetConfig(BuildingDataLookup[entity].BuildingIDHash,out var itemRequest);

                var ecbInputBuff = ECB.SetBuffer<InputConstructionSlotData>(sortKey,entity);
                var ecbOutputBuff = ECB.SetBuffer<OutputConstructionSlotData>(sortKey,entity);
                if (IsBlueprintLookup.IsComponentEnabled(entity))
                {
                    for (int i = 0; i <itemRequest.itemsRequests.Length; i++)
                    {
                        ecbOutputBuff.Add( new OutputConstructionSlotData
                        {
                            ItemId = itemRequest.itemsRequests[i].ItemId,
                            Capacity = itemRequest.itemsRequests[i].Amount,
                            Amount = inputBuff[i].Amount
                        });

                        ecbInputBuff.Add( new InputConstructionSlotData
                        {
                            ItemId = itemRequest.itemsRequests[i].ItemId,
                            Capacity = itemRequest.itemsRequests[i].Amount,
                            Amount = 0,
                        });
                    }
                    
                }
                else
                {
                    float ak=1;
                    int ck=1;
                    if (HealthLookup.HasComponent(entity))
                    {
                        var healthData = HealthLookup[entity];
                        if (healthData.CurrHealth != healthData.MaxHealth)
                            ak=healthData.CurrHealth / healthData.MaxHealth;
                    }
                    if (RoadTypeLookup.HasComponent(entity))
                    {
                        int l =MapPointLookup[entity].Length;
                        ak=ak*l;
                        ck=ck*l;
                    }
                    for (int i = 0; i <itemRequest.itemsRequests.Length; i++)
                    {
                        float amount=itemRequest.itemsRequests[i].Amount*ak;
                        ecbOutputBuff.Add( new OutputConstructionSlotData
                        {
                            ItemId = itemRequest.itemsRequests[i].Amount,
                            Capacity = itemRequest.itemsRequests[i].Amount*ck,
                            Amount = (int)amount
                        });
                    }
                }
            }
        }
    }
    [BurstCompile]
    public partial struct MarkAsForceDestoryJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 

        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in MarkAsForceDestoroyData markAsForceDestoroyData)
        {
            ECB.SetComponentEnabled<ForceDestroyTag>(sortKey,changeBuildingData.targetEntity,true);
            ECB.SetComponentEnabled<DestroyVisualTag>(sortKey,changeBuildingData.targetEntity,true);
            
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    [BurstCompile]
    public partial struct AddStorageSlotJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 
        public BufferLookup<StorageSlotData> StorageSlotDataLookup;
        public BufferLookup<ExcessSlotData> ExcessSlotDataLookup;
         public Entity mapEntity;
        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in AddStorageSlotData addStorageSlotData)
        {
            if(StorageSlotDataLookup.TryGetBuffer(changeBuildingData.targetEntity,out var buff))
            {
                if (buff.Length < buff.Capacity)
                {
                    
                    var excessECB=ECB.SetBuffer<ExcessSlotData>(sortKey, changeBuildingData.targetEntity);
                    var exLookup =ExcessSlotDataLookup[changeBuildingData.targetEntity];
                    int amount=0;
                    if(exLookup.Length>0)
                    {
                        int i=0;
                        do
                        {
                            var exS=exLookup[i];
                            if(amount==addStorageSlotData.Capacity) break;
                            int fillSpace=addStorageSlotData.Capacity-amount;
                            if(exS.Amount!=0&&exS.ItemId==addStorageSlotData.ItemID)
                            {
                                if (exS.Amount > fillSpace)
                                {
                                    amount=addStorageSlotData.Capacity;
                                    exS.Amount-=fillSpace;
                                }
                                else
                                {
                                    amount+=exS.Amount;
                                    exS.Amount=0;
                                }
                            }
                            exLookup[i]=exS;
                            i++;
                        }while(i<exLookup.Length);
                        foreach(var exS in exLookup)
                        {
                            if(exS.Amount>0)
                                excessECB.Add(new ExcessSlotData{ItemId=exS.ItemId,Amount=exS.Amount,Capacity=100});
                        } 
                    }
                    ECB.AppendToBuffer(sortKey, changeBuildingData.targetEntity, new StorageSlotData { 
                            ItemId = addStorageSlotData.ItemID, 
                            Amount = amount,
                            Capacity=addStorageSlotData.Capacity,
                            IsInputEnabled=true,
                            IsOutputEnabled=true,
                    });
                }
                
                ECB.SetComponentEnabled<UpdateClusterSlots>(sortKey,mapEntity,true);
            }
            
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    [BurstCompile]
    public partial struct RemoveStorageSlotJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 
        public BufferLookup<StorageSlotData> StorageSlotDataLookup;
        public BufferLookup<ExcessSlotData> ExcessSlotDataLookup;
         public Entity mapEntity;

        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in RemoveStorageSlotData removeStorageSlotData)
        {
            if (StorageSlotDataLookup.HasBuffer(changeBuildingData.targetEntity))
            {
                var prevBuff = StorageSlotDataLookup[changeBuildingData.targetEntity];
                
                var newBuffer = ECB.SetBuffer<StorageSlotData>(sortKey, changeBuildingData.targetEntity);
                if (removeStorageSlotData.slotIND >= 0 && removeStorageSlotData.slotIND < prevBuff.Length)
                {
                    
                    var slotData=prevBuff[removeStorageSlotData.slotIND];
                    var excessECB=ECB.SetBuffer<ExcessSlotData>(sortKey, changeBuildingData.targetEntity);
                    var exLookup =ExcessSlotDataLookup[changeBuildingData.targetEntity];
                    for (int i = 0; i < prevBuff.Length; i++)
                    {
                        if (i != removeStorageSlotData.slotIND)
                            newBuffer.Add(prevBuff[i]);
                    }
                    int remain=slotData.Amount;
                    if (exLookup.Length > 0)
                    {
                        for (int j = 0; j < exLookup.Length; j++)
                        {
                            if(remain==0) break;
                            var exS=exLookup[j];
                            if (exS.Amount == exS.Capacity||exS.ItemId!=slotData.ItemId)
                            {
                                continue;
                            }
                            else
                            {
                                int fillSpace =  exS.Capacity-exS.Amount;
                                if (remain > fillSpace)
                                {
                                    exS.Amount=exS.Capacity;
                                    remain-=fillSpace;
                                }
                                else
                                {
                                    exS.Amount+=remain;
                                    remain=0;
                                }
                            }
                            exLookup[j]=exS;
                        }

                    }

                    if (remain > 0)
                    {
                        do
                        {
                            int add=remain>=100?100:remain;
                            excessECB.Add(new ExcessSlotData{ItemId=slotData.ItemId,Amount=add,Capacity=100});
                            remain-=add;
                        }while(remain>0);
                    }
                    foreach(var exS in exLookup)
                    {
                        if(exS.Amount>0)
                            excessECB.Add(new ExcessSlotData{ItemId=exS.ItemId,Amount=exS.Amount,Capacity=100});
                    }  
                }
                ECB.SetComponentEnabled<UpdateClusterSlots>(sortKey,mapEntity,true);
            }
            ECB.DestroyEntity(sortKey, entity);
        }
    }


    [BurstCompile]
    public partial struct ChangeConstructionPriorityJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 
         public Entity mapEntity;
        
         [ReadOnly] public ComponentLookup<ConstructionPriorityData> ConstructionPriorityDataLookup;
        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in ChangeConstructionPriotiyData changeConstructionPriotiyData)
        {
            if (ConstructionPriorityDataLookup.IsComponentEnabled(changeBuildingData.targetEntity))
            {
                ECB.SetComponent(sortKey,changeBuildingData.targetEntity,new ConstructionPriorityData{ConstructionPriority=changeConstructionPriotiyData.newPriority});
                
                ECB.SetComponentEnabled<UpdateClusterSlots>(sortKey,mapEntity,true);
            }
            
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    [BurstCompile]
    public partial struct ChangeCraftPriorityJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 
         public Entity mapEntity;
        
         [ReadOnly] public ComponentLookup<CraftingPriorityData> CraftingPriorityDataLookup;
        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in ChangeCraftPriotiyData craftPriotiyData)
        {
            if (CraftingPriorityDataLookup.HasComponent(changeBuildingData.targetEntity))
            {
                ECB.SetComponent(sortKey,changeBuildingData.targetEntity,new CraftingPriorityData{CraftingPriority=craftPriotiyData.newPriority});
            
                ECB.SetComponentEnabled<UpdateClusterSlots>(sortKey,mapEntity,true);
            }
            
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    [BurstCompile]
    public partial struct ChangeConstructionBuildingAccessDataJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 
         public Entity mapEntity;
        [ReadOnly] public ComponentLookup<ConstructionPriorityData> ConstructionPriorityDataLookup;
        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in ChangeConstructionBuildingAccessData changeConstructionPriotiyData)
        {
            if (ConstructionPriorityDataLookup.IsComponentEnabled(changeBuildingData.targetEntity))
            {
                if(changeConstructionPriotiyData.IsInput)
                    ECB.SetComponentEnabled<IsInputConstructionEnabled>(sortKey,changeBuildingData.targetEntity,changeConstructionPriotiyData.IsEnabled);
                else
                    ECB.SetComponentEnabled<IsOutputConstuctionEnabled>(sortKey,changeBuildingData.targetEntity,changeConstructionPriotiyData.IsEnabled);
                
                ECB.SetComponentEnabled<UpdateClusterSlots>(sortKey,mapEntity,true);
            }
            
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    [BurstCompile]
    public partial struct ChangeProcessorBuildingAccessDataJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 
         public Entity mapEntity;
        
        [ReadOnly] public ComponentLookup<CraftingPriorityData> CraftingPriorityDataLookup;
        public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, in ChangeBuildingData changeBuildingData, in ChangeProcessorBuildingAccessData changeProcessorBuildingAccessData)
        {
            if (CraftingPriorityDataLookup.HasComponent(changeBuildingData.targetEntity))
            {
                if(changeProcessorBuildingAccessData.IsInput)
                    ECB.SetComponentEnabled<IsInputCraftEnabled>(sortKey,changeBuildingData.targetEntity,changeProcessorBuildingAccessData.IsEnabled);
                else
                    ECB.SetComponentEnabled<IsOutputCraftEnabled>(sortKey,changeBuildingData.targetEntity,changeProcessorBuildingAccessData.IsEnabled);
                 ECB.SetComponentEnabled<UpdateClusterSlots>(sortKey,mapEntity,true);
            }
            
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    [BurstCompile]
    public partial struct ChangeStorageSlotAccessDataJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB; 
        public BufferLookup<StorageSlotData> StorageSlotDataLookup;
        
        public Entity mapEntity;
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
                    ECB.SetComponentEnabled<UpdateClusterSlots>(sortKey,mapEntity,true);
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
         public Entity mapEntity;
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
                    int remain=data.Amount-data.Capacity;
                    data.Amount=remain>0?data.Capacity:data.Amount;
                    if (remain<0)
                    {
                        if (exLookup.Length > 0)
                        {
                            for(int i = 0; i < exLookup.Length; i++)
                            {
                                var exSlot=exLookup[i];
                                
                                if(data.Capacity==data.Amount) break;
                                if(exSlot.Amount==0) continue;
                                if (exSlot.ItemId == data.ItemId)
                                {
                                    int fillAmount=data.Capacity-data.Amount;
                                    if (exSlot.Amount > fillAmount)
                                    {
                                        data.Amount=data.Capacity;
                                        exSlot.Amount-=fillAmount;
                                    }
                                    else
                                    {
                                        data.Amount+=exSlot.Amount;
                                        exSlot.Amount=0;
                                    }
                                }
                                exLookup[i]=exSlot;
                            }
                        }
                    }
                    else
                    {
                        if(exLookup.Length>0)
                        {
                            for (int j = 0; j < exLookup.Length; j++)
                            {
                                if (remain == 0) break;

                                var exS = exLookup[j];
                                if (exS.Amount == exS.Capacity || exS.ItemId != data.ItemId)
                                {
                                    continue;
                                }

                                int fillSpace = exS.Capacity - exS.Amount;
                                if (remain > fillSpace)
                                {
                                    exS.Amount = exS.Capacity;
                                    remain -= fillSpace;
                                }
                                else
                                {
                                    exS.Amount += remain;
                                    remain = 0;
                                }
                                exLookup[j] = exS;
                            }
                        }
                        if(remain>0)
                        {
                            do
                            {
                                int add=remain>=100?100:remain;
                                excessECB.Add(new ExcessSlotData{ItemId=data.ItemId,Amount=add,Capacity=100});
                                remain-=add;
                            }while(remain>0);
                        }
                    }
                   
                    foreach(var exS in exLookup)
                    {
                        if(exS.Amount>0)
                            excessECB.Add(new ExcessSlotData{ItemId=exS.ItemId,Amount=exS.Amount,Capacity=100});
                    }
                    
                    buff[storageSlotCapacityData.SlotIND]=data;
                    
                    ECB.SetComponentEnabled<UpdateClusterSlots>(sortKey,mapEntity,true);
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
        public BufferLookup<InputSlotData> InputSlotDataLookup;
        public BufferLookup<OutputSlotData> OutputSlotDataLookup;
        public BufferLookup<ExcessSlotData> ExcessSlotDataLookup;
        
         public Entity mapEntity;
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
                            var inputLookup =InputSlotDataLookup[changeBuildingData.targetEntity];
                            for(int j = 0; j < res.InputItems.Length; j++)
                            {
                                
                                var inputData=inputLookup[j];
                                
                                inputData.Capacity=res.InputItems[j].Amount*changeCountOfPackData.newCapacity;
                                if(exLookup.Length>0)
                                {
                                    int i=0;
                                    do
                                    {
                                        var exS=exLookup[i];
                                        
                                        int fillSpace=inputData.Capacity-inputData.Amount;
                                        if(exS.Amount!=0&&exS.ItemId==inputData.ItemId)
                                        {
                                            if (exS.Amount > fillSpace)
                                            {
                                                inputData.Amount=inputData.Capacity;
                                                exS.Amount-=fillSpace;
                                            }
                                            else
                                            {
                                                inputData.Amount+=exS.Amount;
                                                exS.Amount=0;
                                            }
                                        }
                                        exLookup[i]=exS;
                                        i++;
                                    }while(i<exLookup.Length);
                                }
                                inputLookup[j]=inputData;
                            }
                        }
                        if (OutputSlotDataLookup.HasBuffer(changeBuildingData.targetEntity))
                        {
                            var outputLookup =OutputSlotDataLookup[changeBuildingData.targetEntity];
                            for(int j = 0; j < res.OutputItems.Length; j++)
                            {
                                
                                var outputData=outputLookup[j];
                                
                                outputData.Capacity=res.OutputItems[j].Amount*changeCountOfPackData.newCapacity;
                                
                                if (exLookup.Length > 0)
                                {
                                    int i=0;
                                    do
                                    {
                                        var exS=exLookup[i];
                                        
                                        int fillSpace=outputData.Capacity-outputData.Amount;
                                        if(exS.Amount!=0&&exS.ItemId==outputData.ItemId)
                                        {
                                            if (exS.Amount > fillSpace)
                                            {
                                                outputData.Amount=outputData.Capacity;
                                                exS.Amount-=fillSpace;
                                            }
                                            else
                                            {
                                                outputData.Amount+=exS.Amount;
                                                exS.Amount=0;
                                            }
                                        }
                                        exLookup[i]=exS;
                                        i++;
                                    }while(i<exLookup.Length);
                                }
                                
                                outputLookup[j]=outputData;
                            }
                            
                        }
                        foreach(var exS in exLookup)
                        {
                            if(exS.Amount>0)
                                excessECB.Add(new ExcessSlotData{ItemId=exS.ItemId,Amount=exS.Amount,Capacity=100});
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
                                
                                inputData.Capacity=changeCountOfPackData.newCapacity* res.InputItems[i].Amount;
                                int remain=inputData.Amount-inputData.Capacity;
                                inputData.Amount=inputData.Amount>inputData.Capacity?inputData.Capacity:inputData.Amount;
                                
                                input[i]=inputData;
                                
                                if(remain<=0) continue;
                                else
                                {
                                    if(exLookup.Length>0)
                                    {
                                        for (int j = 0; j < exLookup.Length; j++)
                                        {
                                            if (remain == 0) break;

                                            var exS = exLookup[j];
                                            if (exS.Amount == exS.Capacity || exS.ItemId != inputData.ItemId)
                                            {
                                                continue;
                                            }

                                            int fillSpace = exS.Capacity - exS.Amount;
                                            if (remain > fillSpace)
                                            {
                                                exS.Amount = exS.Capacity;
                                                remain -= fillSpace;
                                            }
                                            else
                                            {
                                                exS.Amount += remain;
                                                remain = 0;
                                            }
                                            exLookup[j] = exS;
                                        }
                                    }
                                    if(remain>0)
                                    {
                                        do
                                        {
                                            int add=remain>=100?100:remain;
                                            excessECB.Add(new ExcessSlotData{ItemId=inputData.ItemId,Amount=add,Capacity=100});
                                            remain-=add;
                                        }while(remain>0);
                                    }
                                }
                            }
                        }
                        if (OutputSlotDataLookup.HasBuffer(changeBuildingData.targetEntity))
                        {
                            var output =OutputSlotDataLookup[changeBuildingData.targetEntity];
                            for(int i = 0; i < output.Length; i++)
                            {
                                var outputData =output[i];
                                
                                outputData.Capacity=changeCountOfPackData.newCapacity* res.OutputItems[i].Amount;
                                int remain=outputData.Amount-outputData.Capacity;
                                outputData.Amount=outputData.Amount>outputData.Capacity?outputData.Capacity:outputData.Amount;
                                
                                output[i]=outputData;
                                
                                if(remain<=0) continue;
                                else
                                {
                                    
                                    if (exLookup.Length > 0)
                                    {
                                        for (int j = 0; j < exLookup.Length; j++)
                                        {
                                            if(remain==0) break;
                                            var exS=exLookup[j];
                                            if (exS.Amount == exS.Capacity||exS.ItemId!=outputData.ItemId)
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                int fillSpace =  exS.Capacity-exS.Amount;
                                                if (remain > fillSpace)
                                                {
                                                    exS.Amount=exS.Capacity;
                                                    remain-=fillSpace;
                                                }
                                                else
                                                {
                                                    exS.Amount+=remain;
                                                    remain=0;
                                                }
                                            }
                                            exLookup[j]=exS;
                                        }

                                    }

                                    if (remain > 0)
                                    {
                                        do
                                        {
                                            int add=remain>=100?100:remain;
                                            excessECB.Add(new ExcessSlotData{ItemId=outputData.ItemId,Amount=add,Capacity=100});
                                            remain-=add;
                                        }while(remain>0);
                                    }
                                }
                               
                            }
                        }
                      
                        foreach(var exS in exLookup)
                        {
                            if(exS.Amount>0)
                                excessECB.Add(new ExcessSlotData{ItemId=exS.ItemId,Amount=exS.Amount,Capacity=100});
                        }  
                    }
                }
                ECB.SetComponent(sortKey,changeBuildingData.targetEntity,new CountOfPackInBuildingData{CountOfPack=changeCountOfPackData.newCapacity});
                ECB.SetComponentEnabled<UpdateClusterSlots>(sortKey,mapEntity,true);
            }
            ECB.DestroyEntity(sortKey, entity);
        }
    }
    
}