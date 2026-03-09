using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.VisualScripting;
public interface IConfigBase
{
    public int id{get;set;}
}
public struct BlobLibrary<T> where T : unmanaged,IConfigBase
{
    public BlobArray<T> Configs;
    public int GetIdByPos(int position)
    {
        if (position >= 0 && position < Configs.Length)
        {
            return Configs[position].id;
        }
        
        return -1; 
    }
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
    public float RestoreHpPerTick;        
    public float TimeToRestore;
    public float MaxHealth;
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
public struct BuildingEnergyStructConfig:IConfigBase
{
    
    public int id {get;set;}
    
    public float radius;
    public int maxConnections;
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

public struct ItemsStructConfig : IConfigBase
{
    public int id{get;set;}
    public int ItemClass;
    public int ItemType;
}

public struct EnemyBaseStructConfig : IConfigBase
{
    public int id{get;set;}
    public int costInPoints;
}
#endregion
#region  Reference
public struct EnemyBaseConfigRefence: IComponentData, IDisposable
{
     public BlobAssetReference<BlobLibrary<EnemyBaseStructConfig>> EnemyBaseConfigs;
    
    public void Dispose()
    {
        EnemyBaseConfigs.Dispose();
    }
}
public struct ItemsConfigReference : IComponentData, IDisposable
{
    public BlobAssetReference<BlobLibrary<ItemsStructConfig>> ItemsConfigs;
    public void Dispose()
    {
        ItemsConfigs.Dispose();
    }
}
public struct BuildingConfigReference : IComponentData,IDisposable
{
    public BlobAssetReference<BlobLibrary<BuildingBaseStructConfig>> BuildingsBaseConfigs;
    public BlobAssetReference<BlobLibrary<BuildingStorageStructConfig>> BuildingStorageStructConfigs;
    public BlobAssetReference<BlobLibrary<BuildingProcessionStructConfig>> BuildingProcessionStructConfigs;
    public BlobAssetReference<BlobLibrary<BuildingItemRequestsStructConfig>> BuildingItemRequestsStructConfigs;
    public BlobAssetReference<BlobLibrary<BuildingEnergyStructConfig>> BuildingEnergyStructConfig;
    public int roadID;
    public int CoreID;
    public int range;

    public void Dispose()
    {
        BuildingsBaseConfigs.Dispose();
        BuildingStorageStructConfigs.Dispose();
        BuildingProcessionStructConfigs.Dispose();
        BuildingItemRequestsStructConfigs.Dispose();
        BuildingEnergyStructConfig.Dispose();
    }
}
public struct RecipeConfigRefernce : IComponentData,IDisposable
{
    public BlobAssetReference<BlobLibrary<RecipeStructConfig>> RecipesConfig;
    public void Dispose()
    {
        RecipesConfig.Dispose();
    }
}

#endregion



