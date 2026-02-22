
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ItemDistributionSystem))]
[BurstCompile]

public partial struct CraftSystem : ISystem
{
    
    float _accumulatedTime;
    uint _frameCount;   
    public void OnCreate(ref SystemState state)
    {
        
        
    }
   public void OnUpdate(ref SystemState state)
    {
        _accumulatedTime += SystemAPI.Time.DeltaTime;
        var tickInfoData = SystemAPI.GetSingleton<TickInfoData>();
        _frameCount++; 

        if (_frameCount % tickInfoData.currTickPerSecond == 0) 
        {   
            
            var recipeCache = SystemAPI.GetSingleton<RecipeConfigRefernce>();
            var productionTable = SystemAPI.GetSingletonRW<ProductionTable>();
            var canCraftLookup=SystemAPI.GetComponentLookup<CanCraft>(false);
            var recipesRef = recipeCache.RecipesConfig; 

            // ВАЖНО: Каждое следующее задание должно принимать handle предыдущего!
            
            // 1. Пинги (Читают RecipeBuildingData)
           

            var handle = new PingConsumerCraftBuildingJob 
            { 
                CanCraftLookup=canCraftLookup,
                RecipesConfig = recipesRef 
            }.ScheduleParallel(state.Dependency);
             handle = new PingProducerCraftBuildingJob 
            { 
                RecipesConfig = recipesRef,
                CanCraftLookup=canCraftLookup
            }.Schedule(handle);

             handle = new PingProcessorCraftBuildingJob 
            { 
                RecipesConfig = recipesRef,
                CanCraftLookup=canCraftLookup
            }.Schedule(handle);

            // 2. Крафт (Пишут в RecipeBuildingData)
            // Теперь эти задания гарантированно ждут завершения Пингов
            handle = new ProducerCraftJob {
                RecipesConfig = recipesRef,
                produced = productionTable.ValueRW.produced.AsParallelWriter(),
                timeStep = _accumulatedTime
            }.ScheduleParallel(handle);

            handle = new ConsumerCraftJob {
                RecipesConfig = recipesRef,
                consumed = productionTable.ValueRW.consumed.AsParallelWriter(),
                timeStep = _accumulatedTime
            }.ScheduleParallel(handle);

            handle = new ProcessorCraftJob {
                RecipesConfig = recipesRef,
                produced = productionTable.ValueRW.produced.AsParallelWriter(),
                consumed = productionTable.ValueRW.consumed.AsParallelWriter(),
                timeStep = _accumulatedTime
            }.ScheduleParallel(handle);

            // 3. ОБЯЗАТЕЛЬНО возвращаем финальный handle в систему
            state.Dependency = handle;
            
            _accumulatedTime = 0; 
        }
    }


    [BurstCompile]
    [WithAll(typeof(BuildingTag),typeof(IsConnectedToEnergy),typeof(IsLogicEnabled),typeof(ProducerTypeBuildingTag))]
    [WithDisabled(typeof(ForceDestroyTag),typeof(IsBlueprint),typeof(IsDemolition))]
    public partial struct PingProducerCraftBuildingJob : IJobEntity
    {
        [ReadOnly] public  BlobAssetReference<BlobLibrary<RecipeStructConfig>> RecipesConfig;
        public ComponentLookup<CanCraft> CanCraftLookup;
        public void Execute(Entity entity,in RecipeBuildingData recipeData,in DynamicBuffer<OutputSlotData> outputs)
        {
            RecipesConfig.Value.TryGetConfig(recipeData.RecipeIDHash,out var res);
            CanCraftLookup.SetComponentEnabled(entity,CanCraft(outputs,res));
        }
        
        bool CanCraft(in DynamicBuffer<OutputSlotData> slots,RecipeStructConfig recipe)
        {
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
        public void Execute(Entity entity,in RecipeBuildingData recipeData,in DynamicBuffer<InputSlotData> inputs)
        {
            RecipesConfig.Value.TryGetConfig(recipeData.RecipeIDHash,out var res);
            CanCraftLookup.SetComponentEnabled(entity,CanCraft(inputs,res));
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
        public void Execute(Entity entity,in RecipeBuildingData recipeData,in DynamicBuffer<InputSlotData> inputs,in DynamicBuffer<OutputSlotData> outputs)
        {
            RecipesConfig.Value.TryGetConfig(recipeData.RecipeIDHash,out var res);
             CanCraftLookup.SetComponentEnabled(entity,CanCraft(inputs,outputs,res));
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
    [WithDisabled(typeof(ForceDestroyTag),typeof(IsBlueprint),typeof(IsDemolition))]
    public partial struct ProducerCraftJob : IJobEntity
    {
        [ReadOnly] public BlobAssetReference<BlobLibrary<RecipeStructConfig>> RecipesConfig;
        public NativeParallelMultiHashMap<int, RecipeIngredientStruct>.ParallelWriter produced; 
        public float timeStep;
        public void Execute(ref RecipeBuildingData recipeData, ref DynamicBuffer<OutputSlotData> slots,EnabledRefRW<CanCraft> canCraft)
        {
            if(recipeData.CurrTime>=recipeData.TimeToCraft)
            {
                if ( RecipesConfig.Value.TryGetConfig(recipeData.RecipeIDHash,out var recipe))
                {
                    for(int i = 0; i < slots.Length; i++)
                    {
                        var data=slots[i];
                        data.Amount+=recipe.OutputItems[i].Amount;
                        produced.Add(recipe.OutputItems[i].ItemId,recipe.OutputItems[i]);
                        slots[i]=data;
                    }
                    recipeData.CurrTime=0;
                    canCraft.ValueRW=CanCraft(slots,recipe);
                }
                
            }
            else recipeData.CurrTime+=timeStep;
            
        }
        bool CanCraft(in DynamicBuffer<OutputSlotData> slots,RecipeStructConfig recipe)
        {
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
        public void Execute(ref RecipeBuildingData recipeData, ref DynamicBuffer<InputSlotData> slots)
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
            }
            else recipeData.CurrTime+=timeStep;
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
        public void Execute(ref RecipeBuildingData recipeData, DynamicBuffer<InputSlotData> inSlots, DynamicBuffer<OutputSlotData> outSlots,EnabledRefRW<CanCraft> canCraft)
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
                    canCraft.ValueRW=CanCraft(inSlots,outSlots,recipe);
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