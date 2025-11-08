using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ItemConfig
{
    public string id;
    public string title;
    public string description;
    public string iconPath;
    public int maxInStack;
}

[Serializable]
public class BuildingConfig
{
    public string id;
    public string title;
    public string description;
    public string iconPath;
    public string prefabPath;
    public BuildingsTypes buildingType;
    public ActionType actionType;
    public Vector3Int size;
    public TypeOfLogic typeOfLogic;
    public RequiredRecipesGroup requiredRecipesGroup;
    public float maxHealth;
    public float timeToStartRestore;
    public float restoreHealthPerSecond;
}

[Serializable]
public class RecipeConfig
{
    public string id;
    public int groupId;
    public List<RecipeIngredient> inputItems = new List<RecipeIngredient>();
    public List<RecipeIngredient> outputItems = new List<RecipeIngredient>();
    public float craftTime;
    public string recipeSpritePath;
    public string buildingId;
}

[Serializable]
public class RecipeIngredient
{
    public int itemId;
    public int amount;
}

public enum BuildingsTypes : int
{
    Production = 0,
    Enegry = 1,
    Logistic = 2,
    Defence = 3,
}

public enum ActionType : int
{
    Building = 0,
    TwoPointBuilding = 1,
}

public enum TypeOfLogic : int
{
    None = 0,
    Production = 1,
    Consuming = 2,
    Procession = 3,
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
