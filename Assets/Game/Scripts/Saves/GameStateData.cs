using System;
using System.Collections.Generic;
using Unity.Mathematics;

[Serializable]
public class GameStateData
{
    public bool IsGameOver;
    public long CurrTick;    
    public int TicksPerDay;       
    public float dayLength;
    public int CoreID;
    public PlayerCamData camData;
    public int2 CorePos;
    public Dictionary<int,BaseBuildingSaveData> Buildings;
    public Dictionary<int,ManyPointsBuildingSaveData> ManyPointsBuildings;
    public Dictionary<int,ConstructionSlotsSaveData> constructionSlotsSaveData;
    public Dictionary<int,ExcessSlotsSaveData> excessSlotsSaveData;
    public Dictionary<int,RecipeAndCraftBuildingSaveData> recipeBuildingSaveData;
    public Dictionary<int,StorageSlotsSaveData> storageSlotsSaveData;
    public Dictionary<int,BuildingEnergyNetvorkLinkSaveData> buildingEnergyNetvorkLinkSaveData;
    public List<ResourceCellSave> ResourcesCellsList; 
    public EnemyAIConfig EnemyAiConfig;
    public SpawnMobsData SpawnMobsData;
}
[Serializable]
public struct ResourceCellSave { 
    public int2 pos; 
    public int2 val; 
}