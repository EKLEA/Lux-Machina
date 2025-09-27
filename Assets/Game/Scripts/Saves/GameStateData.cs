using System;
using System.Collections.Generic;


[Serializable]
public class GameStateData
{
    public Dictionary<string,BuildingVisualData> buildingVisualDatas;
    public Dictionary<string,BuildingLogicData> buildingLogicDatas;
    public Dictionary<string,BuildingHealthData> buildingHealthData;
}

