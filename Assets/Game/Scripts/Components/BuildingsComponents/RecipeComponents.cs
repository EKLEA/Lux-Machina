using Unity.Collections;
using Unity.Entities;

public struct BurstRecipe
{
    public FixedList32Bytes<RecipeIngredientStruct> InputItems;
    public FixedList32Bytes<RecipeIngredientStruct> OutputItems;
    public float CraftTime;
}

public struct RecipeIngredientStruct
{
    public int ItemId;
    public int Amount;
}

public struct RecipeCache : IComponentData
{
    public NativeHashMap<int, BurstRecipe> Recipes;
}
