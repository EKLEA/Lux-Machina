
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ItemDistributionSystem))]
[BurstCompile]

public partial struct CraftSystem : ISystem
{
    
    EntityQuery _IsPause;
    public void OnCreate(ref SystemState state)
    {
         _IsPause= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<IsPause,BuildingMap>()
            .Build(ref state);
        
    }
    public void OnUpdate(ref SystemState state)
    {
        
        if(!_IsPause.IsEmpty) return;
        var query = SystemAPI.QueryBuilder().WithAll<IsTickFrame>().Build();
        if (query.IsEmpty) return;

        var tickData=SystemAPI.GetSingleton<WorldTime>();
        var recipeCache = SystemAPI.GetSingleton<RecipeConfigRefernce>();
        var productionTable = SystemAPI.GetSingletonRW<ProductionTable>();
        var canCraftLookup=SystemAPI.GetComponentLookup<CanCraft>(false);
        var recipesRef = recipeCache.RecipesConfig; 
        var ResourceMap = SystemAPI.GetSingletonRW<ResourceMap>();

        var handle = new PingConsumerCraftBuildingJob 
        { 
            CanCraftLookup=canCraftLookup,
            RecipesConfig = recipesRef 
        }.ScheduleParallel(state.Dependency);
            handle = new PingProducerCraftBuildingJob 
        { 
            RecipesConfig = recipesRef,
            CanCraftLookup=canCraftLookup,
            ResouecesMap=ResourceMap.ValueRO.ResouecesMap
        }.Schedule(handle);

            handle = new PingProcessorCraftBuildingJob 
        { 
            RecipesConfig = recipesRef,
            CanCraftLookup=canCraftLookup
        }.Schedule(handle);

        handle = new ProducerCraftJob {
            RecipesConfig = recipesRef,
            produced = productionTable.ValueRW.produced.AsParallelWriter(),
            timeStep = tickData.acceleretedTick,
            ResouecesMap=ResourceMap.ValueRW.ResouecesMap
        }.ScheduleParallel(handle);

        handle = new ConsumerCraftJob {
            RecipesConfig = recipesRef,
            consumed = productionTable.ValueRW.consumed.AsParallelWriter(),
            timeStep = tickData.acceleretedTick
        }.ScheduleParallel(handle);

        handle = new ProcessorCraftJob {
            RecipesConfig = recipesRef,
            produced = productionTable.ValueRW.produced.AsParallelWriter(),
            consumed = productionTable.ValueRW.consumed.AsParallelWriter(),
            timeStep = tickData.acceleretedTick
        }.ScheduleParallel(handle);

        state.Dependency = handle;
    }


    [BurstCompile]
    [WithAll(typeof(BuildingTag),typeof(IsConnectedToEnergy),typeof(IsLogicEnabled),typeof(ProducerTypeBuildingTag))]
    [WithDisabled(typeof(ForceDestroyTag),typeof(IsBlueprint),typeof(IsDemolition))]
    public partial struct PingProducerCraftBuildingJob : IJobEntity
    {
        [ReadOnly] public  BlobAssetReference<BlobLibrary<RecipeStructConfig>> RecipesConfig;
        [ReadOnly] public  NativeParallelHashMap<int2,int2> ResouecesMap;
        public ComponentLookup<CanCraft> CanCraftLookup;
        public void Execute(Entity entity,in RecipeBuildingData recipeData,in DynamicBuffer<OutputSlotData> outputs, in ResourcesLink resourcesLink,ref BuildingStateData buildingStateData)
        {
            RecipesConfig.Value.TryGetConfig(recipeData.RecipeIDHash,out var res);
            
            bool b= CanCraft(outputs,res,resourcesLink);
            CanCraftLookup.SetComponentEnabled(entity,b);
            buildingStateData.State=(int)(b?WorkStateEnum.Work:WorkStateEnum.Await);
            
            
        }
        
        bool CanCraft(in DynamicBuffer<OutputSlotData> slots,RecipeStructConfig recipe,ResourcesLink resourcesLink)
        {
            bool hasResources=false;
            for(int i = 0; i < resourcesLink.ResourcesCells.Length; i++)
            {
                int2 pos= resourcesLink.ResourcesCells[i];
                if (ResouecesMap.ContainsKey(pos) && ResouecesMap[pos].y > 0)
                {
                    hasResources= true; 
                    break;
                }
            }
            if(!hasResources) return false;
            for(int i=0;i<slots.Length;i++)
            {
                if (slots[i].Capacity - slots[i].Amount < recipe.OutputItems[i].Amount) return false;
            }
            return true;
        }
    }
    [BurstCompile]
    
    [WithAll(typeof(BuildingTag),typeof(IsConnectedToEnergy),typeof(IsLogicEnabled),typeof(ConsumerTypeBuildingTag))]
    [WithDisabled(typeof(ForceDestroyTag),typeof(IsBlueprint),typeof(IsDemolition))]
    public partial struct PingConsumerCraftBuildingJob : IJobEntity
    {
        [ReadOnly] public BlobAssetReference<BlobLibrary<RecipeStructConfig>> RecipesConfig;
        
        public ComponentLookup<CanCraft> CanCraftLookup;
        public void Execute(Entity entity,in RecipeBuildingData recipeData,in DynamicBuffer<InputSlotData> inputs,ref BuildingStateData buildingStateData)
        {
            
            RecipesConfig.Value.TryGetConfig(recipeData.RecipeIDHash,out var res);
            bool b=CanCraft(inputs,res);
            CanCraftLookup.SetComponentEnabled(entity,b);
            buildingStateData.State=(int)(b?WorkStateEnum.Work:WorkStateEnum.Await);
        }
        
        bool CanCraft(in DynamicBuffer<InputSlotData> slots,RecipeStructConfig recipe)
        {
            for(int i=0;i<slots.Length;i++)
            {
                if (slots[i].Amount <recipe.InputItems[i].Amount) return false;
            }
            return true;
        }
    }
    [WithAll(typeof(BuildingTag),typeof(IsConnectedToEnergy),typeof(IsLogicEnabled),typeof(ProcessorTypeBuildingTag))]
    [WithDisabled(typeof(ForceDestroyTag),typeof(IsBlueprint),typeof(IsDemolition))]
    public partial struct PingProcessorCraftBuildingJob : IJobEntity
    {
        [ReadOnly] public BlobAssetReference<BlobLibrary<RecipeStructConfig>> RecipesConfig;
        
        public ComponentLookup<CanCraft> CanCraftLookup;
        public void Execute(Entity entity,in RecipeBuildingData recipeData,in DynamicBuffer<InputSlotData> inputs,in DynamicBuffer<OutputSlotData> outputs,ref BuildingStateData buildingStateData)
        {
            RecipesConfig.Value.TryGetConfig(recipeData.RecipeIDHash,out var res);
            bool b=CanCraft(inputs,outputs,res);
            CanCraftLookup.SetComponentEnabled(entity,b);
            buildingStateData.State=(int)(b?WorkStateEnum.Work:WorkStateEnum.Await);
        }
        
     bool CanCraft(in DynamicBuffer<InputSlotData> inSlots,in DynamicBuffer<OutputSlotData> outSlots,RecipeStructConfig recipe)
        {
            bool input=true;

            for(int i=0;i<inSlots.Length;i++)
            {
                if (inSlots[i].Amount < recipe.InputItems[i].Amount)
                {
                    input=false;
                    break;
                }
            }
            bool output=true;
            for(int i=0;i<outSlots.Length;i++)
            {
                if (outSlots[i].Capacity - outSlots[i].Amount < recipe.OutputItems[i].Amount)
                {
                    output=false;
                    break;
                }
            }
            return output&&input;
        }
    }

    [BurstCompile]
    [WithAll(typeof(IsConnectedToEnergy),typeof(IsRecipeAssigned),typeof(IsLogicEnabled),typeof(ProducerTypeBuildingTag))]
    [WithDisabled(typeof(ForceDestroyTag),typeof(IsBlueprint),typeof(IsDemolition),typeof(MarkOnMap))]
    public partial struct ProducerCraftJob : IJobEntity
    {
        [ReadOnly] public BlobAssetReference<BlobLibrary<RecipeStructConfig>> RecipesConfig;
        public NativeParallelMultiHashMap<int, RecipeIngredientStruct>.ParallelWriter produced; 
        [NativeDisableParallelForRestriction] public NativeParallelHashMap<int2,int2> ResouecesMap;
        public float timeStep;
        public void Execute(ref RecipeBuildingData recipeData, ref DynamicBuffer<OutputSlotData> slots,EnabledRefRW<CanCraft> canCraft,ref ResourcesLink resourcesLink,ref BuildingStateData buildingStateData)
        {
            if(recipeData.CurrTime>=recipeData.TimeToCraft)
            {
                if ( RecipesConfig.Value.TryGetConfig(recipeData.RecipeIDHash,out var recipe))
                {
                    var resCell=ResouecesMap[resourcesLink.ResourcesCells[resourcesLink.indexCell]];
                    resCell.y-=1;
                    
                    ResouecesMap[resourcesLink.ResourcesCells[resourcesLink.indexCell]]=resCell;
                    resourcesLink.indexCell=( resourcesLink.indexCell+1)%resourcesLink.ResourcesCells.Length;;
                    for(int i = 0; i < slots.Length; i++)
                    {
                        var data=slots[i];
                        data.Amount+=recipe.OutputItems[i].Amount;
                        produced.Add(recipe.OutputItems[i].ItemId,recipe.OutputItems[i]);
                        slots[i]=data;
                    }
                    
                    recipeData.CurrTime=0;
                    bool b=CanCraft(slots,recipe,resourcesLink);
                    canCraft.ValueRW=b;
                    
                    buildingStateData.State=(int)(b?WorkStateEnum.Work:WorkStateEnum.Await);
                }
                
            }
            else recipeData.CurrTime+=timeStep;
            
        }
        bool CanCraft(in DynamicBuffer<OutputSlotData> slots,RecipeStructConfig recipe,ResourcesLink resourcesLink)
        {
            bool hasResources=false;
            for(int i = 0; i < resourcesLink.ResourcesCells.Length; i++)
            {
                int2 pos= resourcesLink.ResourcesCells[i];
                if (ResouecesMap.ContainsKey(pos) && ResouecesMap[pos].y > 0)
                {
                    hasResources= true; 
                    break;
                }
            }
            if(!hasResources) return false;
            for(int i=0;i<slots.Length;i++)
            {
                if (slots[i].Capacity - slots[i].Amount < recipe.OutputItems[i].Amount) return false;
            }
            return true;
        }
    }
    
    [BurstCompile]
    [WithAll(typeof(IsConnectedToEnergy),typeof(IsRecipeAssigned),typeof(IsLogicEnabled),typeof(ConsumerTypeBuildingTag))]
    [WithDisabled(typeof(ForceDestroyTag),typeof(IsBlueprint),typeof(IsDemolition))]
    public partial struct ConsumerCraftJob : IJobEntity
    {
        [ReadOnly] public BlobAssetReference<BlobLibrary<RecipeStructConfig>> RecipesConfig;
        public NativeParallelMultiHashMap<int, RecipeIngredientStruct>.ParallelWriter consumed; 
        public float timeStep;
        public void Execute(ref RecipeBuildingData recipeData,EnabledRefRW<CanCraft> canCraft, ref DynamicBuffer<InputSlotData> slots,ref BuildingStateData buildingStateData)
        {
            if(recipeData.CurrTime>=recipeData.TimeToCraft)
            {
                if ( RecipesConfig.Value.TryGetConfig(recipeData.RecipeIDHash,out var recipe))
                {
                     for(int i = 0; i < slots.Length; i++)
                    {
                        var data=slots[i];
                        data.Amount-=recipe.InputItems[i].Amount;
                        consumed.Add(recipe.InputItems[i].ItemId,recipe.InputItems[i]);
                        slots[i]=data;
                    }
                    recipeData.CurrTime=0;
                    
                }
                bool b=CanCraft(slots,recipe);
                canCraft.ValueRW=b;
                
                    buildingStateData.State=(int)(b?WorkStateEnum.Work:WorkStateEnum.Await);

            }
            else recipeData.CurrTime+=timeStep;
        }
         bool CanCraft(in DynamicBuffer<InputSlotData> slots,RecipeStructConfig recipe)
        {
            for(int i=0;i<slots.Length;i++)
            {
                if (slots[i].Amount <recipe.InputItems[i].Amount) return false;
            }
            return true;
        }
    }

      [BurstCompile]
    [WithAll(typeof(IsConnectedToEnergy),typeof(IsRecipeAssigned),typeof(IsLogicEnabled),typeof(ProcessorTypeBuildingTag))]
    [WithDisabled(typeof(ForceDestroyTag),typeof(IsBlueprint),typeof(IsDemolition))]
    [WithNone(typeof(ProducerTypeBuildingTag))]
    public partial struct ProcessorCraftJob: IJobEntity
    {
        [ReadOnly] public BlobAssetReference<BlobLibrary<RecipeStructConfig>> RecipesConfig;
        public NativeParallelMultiHashMap<int, RecipeIngredientStruct>.ParallelWriter consumed; 
        public NativeParallelMultiHashMap<int, RecipeIngredientStruct>.ParallelWriter produced; 
        public float timeStep;
        public void Execute(ref RecipeBuildingData recipeData, DynamicBuffer<InputSlotData> inSlots, DynamicBuffer<OutputSlotData> outSlots,EnabledRefRW<CanCraft> canCraft,ref BuildingStateData buildingStateData)
        {
            if(recipeData.CurrTime>=recipeData.TimeToCraft)
            {
               
                if ( RecipesConfig.Value.TryGetConfig(recipeData.RecipeIDHash,out var recipe))
                {
                     for(int i = 0; i < inSlots.Length; i++)
                    {
                        var data=inSlots[i];
                        data.Amount-=recipe.InputItems[i].Amount;
                        consumed.Add(recipe.InputItems[i].ItemId,recipe.InputItems[i]);
                        inSlots[i]=data;
                    }

                    for(int i = 0; i < outSlots.Length; i++)
                    {
                        var data=outSlots[i];
                        data.Amount+=recipe.OutputItems[i].Amount;
                        produced.Add(recipe.OutputItems[i].ItemId,recipe.OutputItems[i]);
                        outSlots[i]=data;
                    }
                    recipeData.CurrTime=0;
                    bool b=CanCraft(inSlots,outSlots,recipe);
                    canCraft.ValueRW=b;
                    
                    buildingStateData.State=(int)(b?WorkStateEnum.Work:WorkStateEnum.Await);
                }
            }
            else recipeData.CurrTime+=timeStep;
            
        }
        bool CanCraft(in DynamicBuffer<InputSlotData> inSlots,in DynamicBuffer<OutputSlotData> outSlots,RecipeStructConfig recipe)
        {
            bool input=true;

            for(int i=0;i<inSlots.Length;i++)
            {
                if (inSlots[i].Amount < recipe.InputItems[i].Amount)
                {
                    input=false;
                    break;
                }
            }
            bool output=true;
            for(int i=0;i<outSlots.Length;i++)
            {
                if (outSlots[i].Capacity - outSlots[i].Amount < recipe.OutputItems[i].Amount)
                {
                    output=false;
                    break;
                }
            }
            return output&&input;
        }
    }

}   