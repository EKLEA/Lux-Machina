using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ConfigService
    : IReadOnlyBuildingInfo,
        IReadOnlyItemsInfo,
        IReadOnlyRecipeInfo,
        IReadOnlyTypeBuildingButtonInfo
{
    public Dictionary<int, ItemConfig> ItemsInfos { get; private set; }
    public Dictionary<int, RecipeConfig> RecipeInfos { get; private set; }
    public Dictionary<int, string> TypeBuildingButtonConfig { get; private set; }
     public Dictionary<int, BuildingBaseConfig> BuildingInfos{ get; private set; }
    public Dictionary<int, BuildingStorageConfig> BuildingStorageInfos{ get; private set; }
    public Dictionary<int, BuildingProcessionConfig> BuildingProcessionInfos { get; private set; }
    public Dictionary<int, BuildingItemRequestsConfig> BuildingItemRequestsInfos { get; private set; }
    public Dictionary<int, BuildingEnegryConfig> BuildingEnegryConfigs  { get; private set; }

    public Dictionary<int, string> ItemClassButtonConfig  { get; private set; }


    Dictionary<int, Sprite> _spriteCache = new Dictionary<int, Sprite>();
    Dictionary<int, GameObject> _prefabCache = new Dictionary<int, GameObject>();

    public ConfigService()
    {
        ItemsInfos = new Dictionary<int, ItemConfig>();
        BuildingInfos = new Dictionary<int, BuildingBaseConfig>();
        BuildingStorageInfos = new Dictionary<int, BuildingStorageConfig>();
        BuildingProcessionInfos = new Dictionary<int, BuildingProcessionConfig>();
        BuildingItemRequestsInfos = new Dictionary<int, BuildingItemRequestsConfig>();
        BuildingEnegryConfigs = new Dictionary<int, BuildingEnegryConfig>();
        RecipeInfos = new Dictionary<int, RecipeConfig>();
        TypeBuildingButtonConfig=new Dictionary<int, string>();
        ItemClassButtonConfig=new();
    }

    public async UniTask LoadConfigs()
    {
        LoadItems();
        LoadBuildingsBase();
        LoadBuildingsStorages();
        LoadBuildingsProcession();
        LoadBuildingsItemRequests();
        LoadBuildingsEnergy();
        LoadRecipes();
        LoadTypeBuildingButtons();
        await UniTask.Yield();
    }
    

    void LoadTypeBuildingButtons()
    {
        foreach (var t in Enum.GetValues(typeof(BuildingsTypes)))
        {
            TypeBuildingButtonConfig[(int)t] = t.ToString();
        }
    }
    void LoadItems()
    {
        var wrapper = LoadJson<ItemConfigList>("Configs/JsonData/items");
        if (wrapper?.items != null)
        {
            foreach (var item in wrapper.items)
            {
                ItemsInfos[item.id] = item;
            }
            Debug.Log($"Loaded {wrapper.items.Count} items");
        }
        else
        {
            Debug.LogError("Failed to load items - wrapper or items list is null");
        }
        foreach (var t in Enum.GetValues(typeof(ItemClass)))
        {
            ItemClassButtonConfig[(int)t] = t.ToString();
        }
    }

    void LoadBuildingsBase()
    {
        var wrapper = LoadJson<BuildingBaseConfigList>("Configs/JsonData/buildings");
        if (wrapper?.buildingsBaseConfigs != null)
        {
            foreach (var building in wrapper.buildingsBaseConfigs)
            {
                BuildingInfos[building.id.GetStableHashCode()] = building;
            }
            Debug.Log($"Loaded {wrapper.buildingsBaseConfigs.Count} buildings");
        }
        else
        {
            Debug.LogError("Failed to load buildings - wrapper or buildings list is null");
        }
    }
    void LoadBuildingsStorages()
    {
        var wrapper = LoadJson<BuildingStorageConfigList>("Configs/JsonData/buildingsStorages");
        if (wrapper?.storageConfigs != null)
        {
            foreach (var building in wrapper.storageConfigs)
            {
                BuildingStorageInfos[building.BuildingID.GetStableHashCode()] = building;
            }
            Debug.Log($"Loaded {wrapper.storageConfigs.Count} buildings storages");
        }
        else
        {
            Debug.LogError("Failed to load buildings storages - wrapper or buildings storages list is null");
        }
    }
    void LoadBuildingsProcession()
    {
        var wrapper = LoadJson<BuildingProcessionConfigList>("Configs/JsonData/buildingProcessions");
        if (wrapper?.processionConfigs != null)
        {
            foreach (var building in wrapper.processionConfigs)
            {
                BuildingProcessionInfos[building.BuildingID.GetStableHashCode()] = building;
            }
            Debug.Log($"Loaded {wrapper.processionConfigs.Count} buildings processions");
        }
        else
        {
            Debug.LogError("Failed to load buildings processions- wrapper or buildings processions list is null");
        }
    }
    void LoadBuildingsItemRequests()
    {
        var wrapper = LoadJson<BuildingItemRequestsConfigList>("Configs/JsonData/buildingItemRequests");
        if (wrapper?.buildingItemRequestsConfigs != null)
        {
            foreach (var building in wrapper.buildingItemRequestsConfigs)
            {
                BuildingItemRequestsInfos[building.BuildingID.GetStableHashCode()] = building;
            }
            Debug.Log($"Loaded {wrapper.buildingItemRequestsConfigs.Count} buildings");
        }
        else
        {
            Debug.LogError("Failed to load buildings item requests - wrapper or buildings requests list is null");
        }
    }

    void LoadBuildingsEnergy()
    {
        var wrapper = LoadJson<BuildingEnegryConfigList>("Configs/JsonData/buildingsEnergy");
        if (wrapper?.buildingEnegryConfigs != null)
        {
            foreach (var building in wrapper.buildingEnegryConfigs)
            {
                BuildingEnegryConfigs[building.BuildingID.GetStableHashCode()] = building;
            }
            Debug.Log($"Loaded {wrapper.buildingEnegryConfigs.Count} buildings energy");
        }
        else
        {
            Debug.LogError("Failed to load buildings energy - wrapper or buildings requests list is null");
        }
    }
    void LoadRecipes()
    {
        var wrapper = LoadJson<RecipeConfigList>("Configs/JsonData/recipes");
        if (wrapper?.recipes != null)
        {
            foreach (var recipe in wrapper.recipes)
            {
                RecipeInfos[recipe.id] = recipe;
            }
            Debug.Log($"Loaded {wrapper.recipes.Count} recipes");
        }
        else
        {
            Debug.LogError("Failed to load recipes - wrapper or recipes list is null");
        }
    }

    T LoadJson<T>(string path)
        where T : class,IWrapper
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(path);
        if (jsonFile != null)
        {
            Debug.Log($"Found JSON file: {path}, length: {jsonFile.text.Length} chars");
            try
            {
                var result = JsonUtility.FromJson<T>(jsonFile.text);
                if (result == null)
                {
                    Debug.LogError($"Failed to parse JSON from {path}");
                }
                return result;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error parsing JSON from {path}: {ex.Message}");
                return null;
            }
        }
        else
        {
            Debug.LogError($"JSON file not found: {path}");
            return null;
        }
    }

    public Sprite GetItemSprite(int itemId)
    {
        if (!ItemsInfos.TryGetValue(itemId, out var item) || string.IsNullOrEmpty(item.iconPath))
            return null;

        return GetOrLoadSprite($"Images/Items/{item.iconPath}");
    }
    public Sprite GetItemClassBTSprite(int path)
    {
        if (!ItemClassButtonConfig.TryGetValue(path, out var info) || string.IsNullOrEmpty(info))
            return null;

        return GetOrLoadSprite($"Images/ItemClasses/{info}");
    }

    public Sprite GetBuildingTypeBTSprite(int path)
    {
        if (!TypeBuildingButtonConfig.TryGetValue(path, out var info) || string.IsNullOrEmpty(info))
            return null;

        return GetOrLoadSprite($"Images/BuildingTypesBT/{info}");
    }

    public Sprite GetBuildingSprite(int buildingId)
    {
        if (
            !BuildingInfos.TryGetValue(buildingId, out var building)
            || string.IsNullOrEmpty(building.iconPath)
        )
            return null;

        return GetOrLoadSprite($"Images/Buildings/{building.iconPath}");
    }
    public Sprite GetEnumSprite<T>(int id) where T : struct, Enum
    {
        if (Enum.IsDefined(typeof(T), id))
        {
            string enumTypeName = typeof(T).Name; 
            
            string valueName = Enum.GetName(typeof(T), id);

            return GetOrLoadSprite($"Images/{enumTypeName}/{valueName}");
        }
        return null;
    }
    public Sprite GetRecipeSprite(int recipeId)
    {
        if (
            !RecipeInfos.TryGetValue(recipeId, out var recipe)
            || string.IsNullOrEmpty(recipe.recipeSpritePath)
        )
            return null;

        return GetOrLoadSprite($"Images/Recipes/{recipe.recipeSpritePath}");
    }

    public GameObject GetBuildingPrefab(int buildingId)
    {
        if (
            !BuildingInfos.TryGetValue(buildingId, out var building)
            || string.IsNullOrEmpty(building.prefabPath)
        )
        {
            Debug.LogError(
                $"Building config not found or prefabPath is empty for ID: {buildingId}"
            );
            return null;
        }

        var prefab = GetOrLoadPrefab($"Prefabs/{building.prefabPath}");
        if (prefab == null)
        {
            Debug.LogError(
                $"Prefab not found at path: Prefabs/{building.prefabPath} for building ID: {buildingId}"
            );

            // Покажем все доступные префабы для отладки
            var allPrefabs = Resources.LoadAll<GameObject>("Prefabs");
            Debug.Log($"Available prefabs: {allPrefabs.Length}");
            foreach (var p in allPrefabs)
            {
                Debug.Log($"Prefab: {p.name}");
            }
        }

        return prefab;
    }

    Sprite GetOrLoadSprite(string path)
    {
        int pathHash = path.GetStableHashCode();
        if (_spriteCache.TryGetValue(pathHash, out var cachedSprite))
            return cachedSprite;

        var sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
        {
            _spriteCache[pathHash] = sprite;
        }
        else
        {
            Debug.LogWarning($"Sprite not found at path: {path}");
        }

        return sprite;
    }

    GameObject GetOrLoadPrefab(string path)
    {
        int pathHash = path.GetStableHashCode();
        if (_prefabCache.TryGetValue(pathHash, out var cachedPrefab))
            return cachedPrefab;

        var prefab = Resources.Load<GameObject>(path);
        if (prefab != null)
        {
            _prefabCache[pathHash] = prefab;
        }
        else
        {
            Debug.LogWarning($"Prefab not found at path: {path}");
        }

        return prefab;
    }

    public void ClearCache()
    {
        _spriteCache.Clear();
        _prefabCache.Clear();
    }

    public async UniTask PreloadSprites(IEnumerable<string> spritePaths)
    {
        foreach (var path in spritePaths)
        {
            int pathHash = path.GetStableHashCode();
            if (!_spriteCache.ContainsKey(pathHash))
            {
                await UniTask.RunOnThreadPool(() =>
                {
                    var sprite = Resources.Load<Sprite>(path);
                    if (sprite != null)
                    {
                        lock (_spriteCache)
                        {
                            _spriteCache[pathHash] = sprite;
                        }
                    }
                });
            }
        }
    }
}

public interface IReadOnlyItemsInfo
{
    Dictionary<int, ItemConfig> ItemsInfos { get; }
    public Dictionary<int, string> ItemClassButtonConfig { get; }
    Sprite GetItemSprite(int itemId);
    Sprite GetItemClassBTSprite(int itemClass);
}

public interface IReadOnlyBuildingInfo
{
    Dictionary<int, BuildingBaseConfig> BuildingInfos { get; }
    Dictionary<int, BuildingStorageConfig> BuildingStorageInfos { get; }
    Dictionary<int, BuildingProcessionConfig> BuildingProcessionInfos { get; }
    Dictionary<int, BuildingItemRequestsConfig> BuildingItemRequestsInfos { get; }
    Dictionary<int, BuildingEnegryConfig> BuildingEnegryConfigs { get; }
    GameObject GetBuildingPrefab(int buildingId);
    Sprite GetBuildingSprite(int buildingId);
    Sprite GetEnumSprite<T>(int id)where T : struct, Enum;
}

public interface IReadOnlyRecipeInfo
{
    Dictionary<int, RecipeConfig> RecipeInfos { get; }
    Sprite GetRecipeSprite(int recipeId);
}
public interface IReadOnlyTypeBuildingButtonInfo
{

    public Dictionary<int, string> TypeBuildingButtonConfig { get; }
    Sprite GetBuildingTypeBTSprite(int type);
}


