
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
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
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        // 1. Получаем синглтоны и данные
        var tickData = SystemAPI.GetSingleton<WorldTime>();
        var recipeCache = SystemAPI.GetSingleton<RecipeConfigRefernce>();
        var productionTable = SystemAPI.GetSingletonRW<ProductionTable>();
        var recipesRef = recipeCache.RecipesConfig;
        
        var chunkMap = SystemAPI.GetSingleton<ChunkMap>().ChunkMapData; 

        var canCraftLookup = SystemAPI.GetComponentLookup<CanCraft>(false);
        var resourceBufferLookup = SystemAPI.GetBufferLookup<ResourceElement>(false); 

        
        var handle = new PingConsumerCraftBuildingJob 
        { 
            CanCraftLookup = canCraftLookup,
            RecipesConfig = recipesRef 
        }.Schedule(state.Dependency);

        handle = new PingProducerCraftBuildingJob 
        { 
            RecipesConfig = recipesRef,
            CanCraftLookup = canCraftLookup,
            ChunkMapData = chunkMap,
            ResourceElementLookup = resourceBufferLookup
        }.Schedule(handle); 
        handle = new PingProcessorCraftBuildingJob 
        { 
            RecipesConfig = recipesRef,
            CanCraftLookup = canCraftLookup
        }.Schedule(handle);


        handle = new ProducerCraftJob {
            RecipesConfig = recipesRef,
            produced = productionTable.ValueRW.produced.AsParallelWriter(),
            timeStep = tickData.acceleretedTick,
            ChunkMapData = chunkMap,
            ResourceElementLookup = resourceBufferLookup,
            ECB=ecb.AsParallelWriter()

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
    [WithAll(typeof(BuildingTag), typeof(IsConnectedToEnergy), typeof(IsLogicEnabled), typeof(ProducerTypeBuildingTag))]
    [WithDisabled(typeof(ForceDestroyTag), typeof(IsBlueprint), typeof(IsDemolition))]
    public partial struct PingProducerCraftBuildingJob : IJobEntity
    {
        [ReadOnly] public BlobAssetReference<BlobLibrary<RecipeStructConfig>> RecipesConfig;
        [ReadOnly] public NativeParallelHashMap<int2, Entity> ChunkMapData; 
        public ComponentLookup<CanCraft> CanCraftLookup;
        [ReadOnly] public BufferLookup<ResourceElement> ResourceElementLookup;

        public void Execute(Entity entity, in RecipeBuildingData recipeData, in DynamicBuffer<OutputSlotData> outputs, in DynamicBuffer<ResourcesInChunkLink> resourcesLink, ref BuildingStateData buildingStateData)
        {
            if (!RecipesConfig.Value.TryGetConfig(recipeData.RecipeIDHash, out var recipe)) return;
            if (resourcesLink.Length == 0) return; 

            int requiredResourceID = recipe.OutputItems[0].ItemId;
            
            bool b = CanCraft(outputs, recipe, resourcesLink, requiredResourceID);
            
            CanCraftLookup.SetComponentEnabled(entity, b);
            buildingStateData.State = (int)(b ? WorkStateEnum.Work : WorkStateEnum.Await);
        }
        
         bool CanCraft(in DynamicBuffer<OutputSlotData> slots, RecipeStructConfig recipe, in DynamicBuffer<ResourcesInChunkLink> links, int resourceID)
        {
            if (slots.Length < recipe.OutputItems.Length) 
            {
                return false; 
            }

            for (int i = 0; i < recipe.OutputItems.Length; i++)
            {
                if (i >= slots.Length) return false;

                if (slots[i].Capacity - slots[i].Amount < recipe.OutputItems[i].Amount) 
                    return false;
            }

            if (links.Length == 0) return false;

            for (int i = 0; i < links.Length; i++)
            {
                var link = links[i];
                
                if (link.chunkPos.x == int.MinValue) continue;

                if (!ChunkMapData.TryGetValue(link.chunkPos, out Entity chunkEntity)) continue;
                if (!ResourceElementLookup.HasBuffer(chunkEntity)) continue;

                var chunkResources = ResourceElementLookup[chunkEntity];
                for (int j = 0; j < chunkResources.Length; j++)
                {
                    var res = chunkResources[j];
                    if (res.Amount > 0 && res.ID == resourceID && link.ResourcesCells.Contains(res.LocalPos))
                        return true;
                }
            }
            return false;
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
        [ReadOnly] public NativeParallelHashMap<int2, Entity> ChunkMapData;
        [NativeDisableParallelForRestriction] public BufferLookup<ResourceElement> ResourceElementLookup;
        public EntityCommandBuffer.ParallelWriter ECB; 
        public float timeStep;

        public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex,ref RecipeBuildingData recipeData, ref DynamicBuffer<OutputSlotData> slots, EnabledRefRW<CanCraft> canCraft, ref DynamicBuffer<ResourcesInChunkLink> resourcesLink, ref BuildingStateData buildingStateData)
        {
            if (recipeData.CurrTime < recipeData.TimeToCraft)
            {
                recipeData.CurrTime += timeStep;
                return;
            }

            if (resourcesLink.Length == 0 || !RecipesConfig.Value.TryGetConfig(recipeData.RecipeIDHash, out var recipe)) return;

            int requiredResourceID = recipe.OutputItems[0].ItemId;
            
            var link = resourcesLink[0]; 
            int3 targetLocalPos = link.ResourcesCells[link.indexCell];
            
            bool successMining = false;

            if (ChunkMapData.TryGetValue(link.chunkPos, out Entity chunkEntity))
            {
                var chunkResources = ResourceElementLookup[chunkEntity];
                for (int i = 0; i < chunkResources.Length; i++)
                {
                    var res = chunkResources[i];
                    if (res.LocalPos.Equals(targetLocalPos) && res.ID == requiredResourceID && res.Amount > 0)
                    {
                        res.Amount -= 1;
                        
                        if (res.Amount <= 0)
                        {
                            ECB.SetComponentEnabled<NeedsCleanupTag>(chunkIndex, chunkEntity,true);
                        }

                        chunkResources[i] = res;
                        successMining = true;
                        break;
                    }
                }
            }

            if (successMining)
            {
                link.indexCell = (link.indexCell + 1) % link.ResourcesCells.Length;
                resourcesLink[0] = link;

                for (int i = 0; i < slots.Length; i++)
                {
                    var data = slots[i];
                    data.Amount += recipe.OutputItems[i].Amount;
                    slots[i] = data;
                    produced.Add(recipe.OutputItems[i].ItemId, recipe.OutputItems[i]);
                }
                recipeData.CurrTime = 0;
            }
            else
            {
                link.indexCell = (link.indexCell + 1) % link.ResourcesCells.Length;
                resourcesLink[0] = link;
            }

            bool b = CanCraft(slots, recipe, resourcesLink, requiredResourceID);
            canCraft.ValueRW = b;
            buildingStateData.State = (int)(b ? WorkStateEnum.Work : WorkStateEnum.Await);
        }


        bool CanCraft(in DynamicBuffer<OutputSlotData> slots, RecipeStructConfig recipe, in DynamicBuffer<ResourcesInChunkLink> links, int resourceID)
        {
            if (slots.Length < recipe.OutputItems.Length) 
            {
                return false; 
            }

            for (int i = 0; i < recipe.OutputItems.Length; i++)
            {
                if (i >= slots.Length) return false;

                if (slots[i].Capacity - slots[i].Amount < recipe.OutputItems[i].Amount) 
                    return false;
            }

            if (links.Length == 0) return false;

            for (int i = 0; i < links.Length; i++)
            {
                var link = links[i];
                
                if (link.chunkPos.x == int.MinValue) continue;

                if (!ChunkMapData.TryGetValue(link.chunkPos, out Entity chunkEntity)) continue;
                if (!ResourceElementLookup.HasBuffer(chunkEntity)) continue;

                var chunkResources = ResourceElementLookup[chunkEntity];
                for (int j = 0; j < chunkResources.Length; j++)
                {
                    var res = chunkResources[j];
                    if (res.Amount > 0 && res.ID == resourceID && link.ResourcesCells.Contains(res.LocalPos))
                        return true;
                }
            }
            return false;
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