using System;
using System.Collections.Generic;
using Unity.Mathematics;

[Serializable]
public class GameStateData
{
    public PlayerCamData camData;
    public Dictionary<int,BaseBuildingSaveData> Buildings;
    public Dictionary<int,RoadSaveData> RoadsBuildings;
    public Dictionary<int,ConstructionSlotsSaveData> constructionSlotsSaveData;
    public Dictionary<int,ExcessSlotsSaveData> excessSlotsSaveData;
    public Dictionary<int,RecipeAndCraftBuildingSaveData> recipeBuildingSaveData;
    public Dictionary<int,StorageSlotsSaveData> storageSlotsSaveData;
}