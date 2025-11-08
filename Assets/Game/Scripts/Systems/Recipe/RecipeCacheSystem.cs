using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct RecipeCacheSystem : ISystem
{
    private bool _isInitialized;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _isInitialized = false;
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!_isInitialized)
        {
            InitializeRecipeCache(ref state);
            _isInitialized = true;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }

    private void InitializeRecipeCache(ref SystemState state)
    {
        var entity = state.EntityManager.CreateEntity();
        var recipeCache = new RecipeCache
        {
            Recipes = new NativeHashMap<int, BurstRecipe>(100, Allocator.Persistent),
        };

        state.EntityManager.AddComponentData(entity, recipeCache);
    }
}
