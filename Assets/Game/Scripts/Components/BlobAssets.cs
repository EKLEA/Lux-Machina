using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
public interface IConfigBase
{
    public int id{get;set;}
}
public struct BlobLibrary<T> where T : unmanaged,IConfigBase
{
    public BlobArray<T> Configs;

    public bool TryGetConfig(int id, out T result)
    {
        int low = 0;
        int high = Configs.Length - 1;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            int midId = Configs[mid].id;

            if (midId == id)
            {
                result = Configs[mid];
                return true;
            }
            if (midId < id) low = mid + 1;
            else high = mid - 1;
        }

        result = default;
        return false;
    }
}
#region Configs

public struct BuildingBaseStructConfig:IConfigBase
{
    
    public int id {get;set;}
    public BuildingsTypes buildingType;
    public ActionType actionType;
    public int3 size;
    public TypeOfLogic typeOfLogic;
}

public struct BuildingStorageStructConfig:IConfigBase
{
    
    public int id {get;set;}
    public int maxSlots;
    public FixedList64Bytes<int> requiredItemTypesGroups;
}
public struct BuildingProcessionStructConfig:IConfigBase
{
    
    public int id {get;set;}
    public FixedList64Bytes<int> requiredRecipesGroups;
    public TypeOfProcession typeOfProcession;
}
public struct BuildingItemRequestsStructConfig:IConfigBase
{
    
    public int id {get;set;}
    
    public FixedList64Bytes<RecipeIngredientStruct> itemsRequests;
}
public struct RecipeStructConfig:IConfigBase
{
    public FixedList64Bytes<RecipeIngredientStruct> InputItems;
    public FixedList64Bytes<RecipeIngredientStruct> OutputItems;
    public FixedList64Bytes<int> RecipesGroups;
    public float CraftTime;
    public int id {get;set;}
}
public struct RecipeIngredientStruct
{
    public int ItemId;
    public int Amount;
}
#endregion
#region  Reference

public struct BuildingConfigReference : IComponentData,IDisposable
{
    public BlobAssetReference<BlobLibrary<BuildingBaseStructConfig>> BuildingsBaseConfigs;
    public BlobAssetReference<BlobLibrary<BuildingStorageStructConfig>> BuildingStorageStructConfigs;
    public BlobAssetReference<BlobLibrary<BuildingProcessionStructConfig>> BuildingProcessionStructConfigs;
    public BlobAssetReference<BlobLibrary<BuildingItemRequestsStructConfig>> BuildingItemRequestsStructConfigs;
    public int roadID;

    public void Dispose()
    {
        BuildingsBaseConfigs.Dispose();
        BuildingStorageStructConfigs.Dispose();
        BuildingProcessionStructConfigs.Dispose();
        BuildingItemRequestsStructConfigs.Dispose();
    }
}
public struct RecipeConfigRefernce : IComponentData
{
    public BlobAssetReference<BlobLibrary<RecipeStructConfig>> RecipesConfig;
}

#endregion



