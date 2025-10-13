using UnityEngine;
using Zenject;
public class BuildingHealthFactory
{
    readonly IReadOnlyBuildingInfo _buildingInfo;
    [Inject]
    public BuildingHealthFactory(IReadOnlyBuildingInfo buildingInfo)
    {
        _buildingInfo = buildingInfo;
    }
    public BuildingHealth Create(BuildingHealthData data)
    {
        BuildingHealth buildingHealth = new BuildingHealth(data);
        buildingHealth.Initialize(_buildingInfo.BuildingInfos[data.UnicID].MaxHealth,_buildingInfo.BuildingInfos[data.UnicID].RestoreHealthPerSecond,_buildingInfo.BuildingInfos[data.UnicID].TimeToStartRestore);
        return buildingHealth;
    }
    
       
    
}