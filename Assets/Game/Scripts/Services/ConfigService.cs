using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class ConfigService
    : IReadOnlyBuildingInfo,
        IReadOnlyItemsInfo,
        IReadOnlyMaterialInfo,
        IReadOnlyRecipeInfo
{
    public Dictionary<int, ItemConfig> ItemsInfos { get; private set; }
    public Dictionary<int, BuildingConfig> BuildingInfos { get; private set; }
    public Dictionary<string, Material> MaterialInfos { get; private set; }
    public Dictionary<int, RecipeConfig> RecipeInfos { get; private set; }

    Dictionary<int, Sprite> _spriteCache = new Dictionary<int, Sprite>();
    Dictionary<int, GameObject> _prefabCache = new Dictionary<int, GameObject>();

    public ConfigService()
    {
        ItemsInfos = new Dictionary<int, ItemConfig>();
        BuildingInfos = new Dictionary<int, BuildingConfig>();
        MaterialInfos = new Dictionary<string, Material>();
        RecipeInfos = new Dictionary<int, RecipeConfig>();
    }

    public async Task LoadConfigs()
    {
        LoadItems();
        LoadBuildings();
        LoadMaterials();
        LoadRecipes();

        Debug.Log(
            $"Configs loaded: {ItemsInfos.Count} items, {BuildingInfos.Count} buildings, {MaterialInfos.Count} materials, {RecipeInfos.Count} recipes"
        );
        await Task.Yield();
    }

    void LoadMaterials()
    {
        Material[] allMaterials = Resources.LoadAll<Material>("Materials");
        foreach (var material in allMaterials)
        {
            MaterialInfos[material.name] = material;
        }
        Debug.Log($"Loaded {allMaterials.Length} materials from Resources/Materials");
    }

    void LoadItems()
    {
        var wrapper = LoadJson<ItemConfigList>("Configs/JsonData/items");
        if (wrapper?.items != null)
        {
            foreach (var item in wrapper.items)
            {
                ItemsInfos[item.id.GetStableHashCode()] = item;
            }
            Debug.Log($"Loaded {wrapper.items.Count} items");
        }
        else
        {
            Debug.LogError("Failed to load items - wrapper or items list is null");
        }
    }

    void LoadBuildings()
    {
        var wrapper = LoadJson<BuildingConfigList>("Configs/JsonData/buildings");
        if (wrapper?.buildings != null)
        {
            foreach (var building in wrapper.buildings)
            {
                BuildingInfos[building.id.GetStableHashCode()] = building;
            }
            Debug.Log($"Loaded {wrapper.buildings.Count} buildings");
        }
        else
        {
            Debug.LogError("Failed to load buildings - wrapper or buildings list is null");
        }
    }

    void LoadRecipes()
    {
        var wrapper = LoadJson<RecipeConfigList>("Configs/JsonData/recipes");
        if (wrapper?.recipes != null)
        {
            foreach (var recipe in wrapper.recipes)
            {
                RecipeInfos[recipe.id.GetStableHashCode()] = recipe;
            }
            Debug.Log($"Loaded {wrapper.recipes.Count} recipes");
        }
        else
        {
            Debug.LogError("Failed to load recipes - wrapper or recipes list is null");
        }
    }

    T LoadJson<T>(string path)
        where T : class
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

        return GetOrLoadSprite($"Items/{item.iconPath}");
    }

    public Sprite GetBuildingSprite(int buildingId)
    {
        if (
            !BuildingInfos.TryGetValue(buildingId, out var building)
            || string.IsNullOrEmpty(building.iconPath)
        )
            return null;

        return GetOrLoadSprite($"Buildings/{building.iconPath}");
    }

    public Sprite GetRecipeSprite(int recipeId)
    {
        if (
            !RecipeInfos.TryGetValue(recipeId, out var recipe)
            || string.IsNullOrEmpty(recipe.recipeSpritePath)
        )
            return null;

        return GetOrLoadSprite($"Recipes/{recipe.recipeSpritePath}");
    }

    public Material GetMaterial(string materialName)
    {
        if (MaterialInfos.TryGetValue(materialName, out var material))
            return material;

        Debug.LogWarning($"Material not found: {materialName}");
        return null;
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

    public async Task PreloadSprites(IEnumerable<string> spritePaths)
    {
        foreach (var path in spritePaths)
        {
            int pathHash = path.GetStableHashCode();
            if (!_spriteCache.ContainsKey(pathHash))
            {
                await Task.Run(() =>
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

    public List<RecipeConfig> GetRecipesByGroup(int groupId)
    {
        return RecipeInfos.Values.Where(r => r.groupId == groupId).ToList();
    }
}

public interface IReadOnlyItemsInfo
{
    Dictionary<int, ItemConfig> ItemsInfos { get; }
    Sprite GetItemSprite(int itemId);
}

public interface IReadOnlyBuildingInfo
{
    Dictionary<int, BuildingConfig> BuildingInfos { get; }
    GameObject GetBuildingPrefab(int buildingId);
    Sprite GetBuildingSprite(int buildingId);
}

public interface IReadOnlyMaterialInfo
{
    Dictionary<string, Material> MaterialInfos { get; }
}

public interface IReadOnlyRecipeInfo
{
    Dictionary<int, RecipeConfig> RecipeInfos { get; }
    List<RecipeConfig> GetRecipesByGroup(int groupId);
    Sprite GetRecipeSprite(int recipeId);
}
