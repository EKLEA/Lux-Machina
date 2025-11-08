using Unity.Collections;
using Unity.Entities;
using Zenject;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class RecipeCacheFillSystem : SystemBase
{
    [Inject]
    IReadOnlyRecipeInfo recipeConfig;

    protected override void OnCreate()
    {
        RequireForUpdate<RecipeCache>();
    }

    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingletonEntity<RecipeCache>(out var cacheEntity))
            return;

        var recipeCache = EntityManager.GetComponentData<RecipeCache>(cacheEntity);

        foreach (var recipePair in recipeConfig.RecipeInfos)
        {
            var recipeConfig = recipePair.Value;
            var burstRecipe = new BurstRecipe { CraftTime = recipeConfig.craftTime };

            foreach (var input in recipeConfig.inputItems)
            {
                if (burstRecipe.InputItems.Length < burstRecipe.InputItems.Capacity)
                {
                    burstRecipe.InputItems.Add(
                        new RecipeIngredientStruct { ItemId = input.itemId, Amount = input.amount }
                    );
                }
            }

            foreach (var output in recipeConfig.outputItems)
            {
                if (burstRecipe.OutputItems.Length < burstRecipe.OutputItems.Capacity)
                {
                    burstRecipe.OutputItems.Add(
                        new RecipeIngredientStruct
                        {
                            ItemId = output.itemId,
                            Amount = output.amount,
                        }
                    );
                }
            }

            recipeCache.Recipes.TryAdd(recipePair.Key, burstRecipe);
        }

        EntityManager.SetComponentData(cacheEntity, recipeCache);

        Enabled = false;
    }
}
