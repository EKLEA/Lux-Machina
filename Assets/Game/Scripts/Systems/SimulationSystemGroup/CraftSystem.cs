
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ItemDistributionSystem))]
[BurstCompile]

public partial struct CraftSystem : ISystem
{
    
    EntityQuery _pingProducerCraftBuildings;
    EntityQuery _pingConsumerCraftBuildings;
    EntityQuery _producerQuery;
    EntityQuery _consumerQuery;
    EntityQuery _processorQuery;
    float _accumulatedTime;
    uint _frameCount;   
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<IsRecipeAssigned>();
        state.RequireForUpdate<ProductionTable>();
        _producerQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<RecipeBuildingData,IsLogicEnabled,IsConnectedToEnegy,IsRecipeAssigned>()
            .WithAll<ProducerTypeBuildingTag>()
            .WithDisabled<IsBlueprint,IsDemolition>()
            .Build(ref state);
         _consumerQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<RecipeBuildingData,IsLogicEnabled,IsConnectedToEnegy,IsRecipeAssigned>()
            .WithAll<ConsumerTypeBuildingTag>()
            .WithDisabled<IsBlueprint,IsDemolition>()
            .Build(ref state);
         _processorQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<RecipeBuildingData,IsLogicEnabled,IsConnectedToEnegy,IsRecipeAssigned>()
            .WithAll<ProducerTypeBuildingTag>()
            .WithDisabled<IsBlueprint,IsDemolition>()
            .Build(ref state);
        
        
         _pingProducerCraftBuildings= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BuildingTag,IsConnectedToEnegy>()
            .WithAny<ProducerTypeBuildingTag,ProcessorTypeBuildingTag>()
            .WithDisabled<IsBlueprint,IsDemolition>()
            .Build(ref state);
        _pingProducerCraftBuildings= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BuildingTag,IsConnectedToEnegy,ConsumerTypeBuildingTag>()
            .WithDisabled<IsBlueprint,IsDemolition>()
            .Build(ref state);
        
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
            var recipesRef = recipeCache.RecipesConfig; 

            // ВАЖНО: Каждое следующее задание должно принимать handle предыдущего!
            
            // 1. Пинги (Читают RecipeBuildingData)
            var handle = new PingProducerCraftBuildingJob 
            { 
                RecipesConfig = recipesRef 
            }.ScheduleParallel(state.Dependency);

            handle = new PingConsumerCraftBuildingJob 
            { 
                RecipesConfig = recipesRef 
            }.ScheduleParallel(handle);

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
    [WithAll(typeof(BuildingTag),typeof(IsConnectedToEnegy),typeof(IsLogicEnabled))]
    [WithAny(typeof(ProducerTypeBuildingTag),typeof(ProcessorTypeBuildingTag))]
    [WithDisabled(typeof(ForceDestroyTag),typeof(IsBlueprint),typeof(IsDemolition),typeof(CanCraft))]
    public partial struct PingProducerCraftBuildingJob : IJobEntity
    {
        [ReadOnly] public  BlobAssetReference<BlobLibrary<RecipeStructConfig>> RecipesConfig;
                public void Execute(in RecipeBuildingData recipeData,in DynamicBuffer<OutputSlotData> outputs, EnabledRefRW<CanCraft> canCraftEnabled)
        {
            RecipesConfig.Value.TryGetConfig(recipeData.RecipeIDHash,out var res);
            canCraftEnabled.ValueRW=CanCraft(outputs,res);
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
    [WithAll(typeof(BuildingTag),typeof(IsConnectedToEnegy),typeof(ConsumerTypeBuildingTag),typeof(IsLogicEnabled))]
    [WithDisabled(typeof(ForceDestroyTag),typeof(IsBlueprint),typeof(IsDemolition),typeof(CanCraft))]
    public partial struct PingConsumerCraftBuildingJob : IJobEntity
    {
        [ReadOnly] public BlobAssetReference<BlobLibrary<RecipeStructConfig>> RecipesConfig;
        public void Execute(in RecipeBuildingData recipeData,in DynamicBuffer<InputSlotData> inputs, EnabledRefRW<CanCraft> canCraftEnabled)
        {
            RecipesConfig.Value.TryGetConfig(recipeData.RecipeIDHash,out var res);
            canCraftEnabled.ValueRW=CanCraft(inputs,res);
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
    [WithAll(typeof(IsConnectedToEnegy),typeof(IsRecipeAssigned),typeof(IsLogicEnabled),typeof(ProducerTypeBuildingTag))]
    [WithNone(typeof(ProcessorTypeBuildingTag))]
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
    [WithAll(typeof(IsConnectedToEnegy),typeof(IsRecipeAssigned),typeof(IsLogicEnabled),typeof(ConsumerTypeBuildingTag))]
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
    [WithAll(typeof(IsConnectedToEnegy),typeof(IsRecipeAssigned),typeof(IsLogicEnabled),typeof(ProcessorTypeBuildingTag))]
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
            bool output=false;
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