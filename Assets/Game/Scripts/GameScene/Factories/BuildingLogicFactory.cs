using UnityEngine;
using Zenject;
public class BuildingLogicFactory
{
    readonly IReadOnlyBuildingInfo _buildingInfo;
     [Inject]
    public BuildingLogicFactory(IReadOnlyBuildingInfo buildingInfo)
    {
        _buildingInfo = buildingInfo;
    }
    public BuildingLogic Create(BuildingLogicData data, BuildingOnScene buildingOnScene)
    {
        BuildingLogic buildingLogic = null;
        switch (data)
        {
            case BuildingProductionLogicData:
                buildingLogic = new BuildingProductionLogic(data, buildingOnScene);
                break;

            case BuildingLogicData:
                switch(_buildingInfo.BuildingInfos[data.UnicID])
                {
                    case TurretInfo:
                    buildingLogic = new TurretBuildingLogic(data, buildingOnScene);
                        break;
                    default:
                        buildingLogic = new BuildingLogic(data, buildingOnScene);
                        break;
                }
                

                //другие типы логик те же питалки ресурсами
                break;
        }
        return buildingLogic;
    }
    
}