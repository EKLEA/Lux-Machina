using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ItemConfig
{
    public int id;
    public string title;
    public string description;
    public string iconPath;
    public int maxInStack;
    public ItemClass ItemClass;
    public ItemType ItemType;
}

[Serializable]
public class BuildingConfig
{
    public int id;
    public string title;
    public string description;
    public string iconPath;
    public string prefabPath;
    public BuildingsTypes buildingType;
    public ActionType actionType;
    public Vector3Int size;
    public TypeOfLogic typeOfLogic;
    public HashSet<int> requiredRecipesGroup;
    public float maxHealth;
    public float timeToStartRestore;
    public float restoreHealthPerSecond;
}

[Serializable]
public class RecipeConfig
{
    public int id;
    public string title;
    public HashSet<int> groupIds;
    public Dictionary<int,RecipeIngredient> inputItems = new Dictionary<int,RecipeIngredient>();
    public Dictionary<int,RecipeIngredient> outputItems = new Dictionary<int,RecipeIngredient>();
    public float craftTime;
    public string recipeSpritePath;
}

[Serializable]
public class RecipeIngredient
{
    public int itemId;
    public int amount;
}
[Serializable]
public class StorageConfig
{
    public int BuildingID;
    public int MaxSlots;
    public HashSet<int> ItemsTypes; 
}

public enum ItemClass: int
{
    Rawitems=0,
    Components,
    Assembly,
    Weapon
}
public enum ItemType: int
{
    None=0,
    Bullets=20,
}
public enum BuildingsTypes : int
{
    Prop=0,
    Special = 1,
    Production = 2,
    Enegry = 3,
    Logistic = 4,
    Defence = 5,
}

public enum ActionType : int
{
    Building = 0,
    TwoPointBuilding = 1,
}

public enum TypeOfLogic : int
{
    None=0,
    WorkWithItems = 1,
    CollectInfo = 2,
    Unlock=3,
}

public enum RequiredRecipesGroup : int
{
    None = 0,
    Smeleting = 1,
    Processing = 2,
}

[Serializable]
public class ItemConfigList
{
    public List<ItemConfig> items;
}

[Serializable]
public class BuildingConfigList
{
    public List<BuildingConfig> buildings;
}

[Serializable]
public class RecipeConfigList
{
    public List<RecipeConfig> recipes;
}
[Serializable]
public class StorageConfigList
{
    public List<StorageConfig> storages;
}

