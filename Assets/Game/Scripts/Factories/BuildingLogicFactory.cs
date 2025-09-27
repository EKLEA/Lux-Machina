using UnityEngine;
using Zenject;
public class BuildingLogicFactory
{
    [Inject]  IReadOnlyBuildingInfo _buildingInfo;
    public BuildingLogic Create(BuildingLogicData data, BuildingOnScene buildingOnScene)
    {
        BuildingLogic buildingLogic = null;
        switch (data)
        {
            case BuildingProductionLogicData:
                buildingLogic = new BuildingProductionLogic(data, buildingOnScene);
                break;

            case BuildingLogicData:
                if (_buildingInfo.BuildingInfos[data.UnicID] is TurretInfo)
                {
                    buildingLogic = new BuildingTurretLogic(data, buildingOnScene);
                    //смотрим типы и сетапаем

                }
                //другие типы логик те же питалки ресурсами
                break;
        }
        return buildingLogic;
    }
    
}