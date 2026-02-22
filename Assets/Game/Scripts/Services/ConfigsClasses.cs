using System;
using System.Collections.Generic;
using UnityEngine;
#region  Building

[Serializable]
public class BuildingBaseConfig
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
    public float maxHealth;
    public float timeToStartRestore;
    public float restoreHealthPerSecond;
}
[Serializable]
public class BuildingProcessionConfig
{
    public string BuildingID;
    public TypeOfProcession typeOfProcession;
    public List<RequiredRecipesGroup> requiredRecipesGroup;
}
[Serializable]
public class BuildingStorageConfig
{
    public string BuildingID;
    public int MaxSlots;
    public List<ItemClass> ItemsTypes; 
}

[Serializable]
public class BuildingItemRequestsConfig
{
    public string BuildingID;
    public List<RecipeIngredient> itemsRequest =  new List<RecipeIngredient>();
}
[Serializable]
public class BuildingEnegryConfig
{
    public string BuildingID;
    public float radius;
    public int maxConnections;
}

[Serializable]
public class BuildingBaseConfigList:IWrapper
{
    public List<BuildingBaseConfig> buildingsBaseConfigs;
}

[Serializable]
public class BuildingStorageConfigList:IWrapper
{
    public List<BuildingStorageConfig> storageConfigs;
}
[Serializable]
public class BuildingProcessionConfigList:IWrapper
{
    public List<BuildingProcessionConfig> processionConfigs;
}
[Serializable]
public class BuildingItemRequestsConfigList:IWrapper
{
    public List<BuildingItemRequestsConfig> buildingItemRequestsConfigs;
}
[Serializable]
public class BuildingEnegryConfigList:IWrapper
{
    public List<BuildingEnegryConfig> buildingEnegryConfigs;
}
#endregion
#region  Items

[Serializable]
public class ItemConfig
{
    public int id;
    public string title;
    public string description;
    public string iconPath;
    public ItemClass ItemClass;
    public ItemType ItemType;
}


[Serializable]
public class RecipeConfig
{
    public int id;
    public string title;
    public List<RequiredRecipesGroup> RecipesGroupIds;
    public ItemClass ItemClass;
    public List<RecipeIngredient> inputItems = new List<RecipeIngredient>();
    public List<RecipeIngredient> outputItems = new List<RecipeIngredient>();
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
public class ItemConfigList:IWrapper
{
    public List<ItemConfig> items;
}

[Serializable]
public class RecipeConfigList:IWrapper
{
    public List<RecipeConfig> recipes;
}


#endregion
public interface IWrapper{}
public enum ItemClass
{
    Components=1,
    Assembly=2,
    Weapon=3
}
public enum ItemType
{
    None=0,
    RawMaterial=1,
    Ignot=2,
    AlloyIgnot=3,
    Cog=4,
    Plate=5,
    BuildMatrial=6,
    SteamPart=7,
    LivePart=8,
    Bullets=9,
    Artillery=10,
}
public enum BuildingsTypes 
{
    Prop=0,
    Special = 1,
    Procession = 2,
    Enegry = 3,
    Logistic = 4,
    Defence = 5,
    DeleteBuilding=99,
    ConnectBuilding=100
}
public enum TypeOfProcession 
{
    Consumer = 0,
    Processing = 1,
    Generate = 2,
}

public enum ActionType 
{
    Building = 0,
    TwoPointBuilding = 1,
}

public enum TypeOfLogic 
{
    None=0,
    WorkWithItems = 1,
    CollectInfo = 2,
    Unlock=3,
}

public enum RequiredRecipesGroup 
{
    Generating = 1,
    Smeleting = 2,
    BlastSmelting=3,
    Processing = 4,
    ManufactoryProcessing=5,
}
