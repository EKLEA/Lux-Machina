using UnityEngine;
using Zenject;
public class BuildingHealthFactory
{
    [Inject] IReadOnlyBuildingInfo _buildingInfo;
    public BuildingHealth Create(BuildingHealthData data)
    {
        BuildingHealth buildingHealth = new BuildingHealth(data);
        buildingHealth.Initialize(_buildingInfo.BuildingInfos[data.UnicID].MaxHealth,_buildingInfo.BuildingInfos[data.UnicID].RestoreHealthPerSecond,_buildingInfo.BuildingInfos[data.UnicID].TimeToStartRestore);
        return buildingHealth;
    }
    
       
    
}