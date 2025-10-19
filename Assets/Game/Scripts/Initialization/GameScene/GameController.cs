using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

public class GameController : IInitializable
{
    [Inject] IReadOnlyGameFieldSettings gameFieldSettings;
    [Inject] IReadOnlyBuildingInfo _buildingInfo;
    [Inject] SaveService saveService;
    [Inject] ILoadingService _loadingService;
    [Inject] CameraController cameraController;
    [Inject] EntityLoader EntityLoader;
    [Inject] BuildingPositionSystem _buildingPositionSystem;
    [Inject] FixedStepSimulationSystemGroup fixedStepSimulationSystemGroup;

    public void Initialize()
    {
        fixedStepSimulationSystemGroup.Timestep = 1 / gameFieldSettings.tickPerSecond;

        _buildingPositionSystem.Enabled = true;
        
        LoadGame();
    }

    public void SpeedUpTick()
    {
        fixedStepSimulationSystemGroup.Timestep  /= 2;
    }
    
    public void SlowDownTick()
    {
        fixedStepSimulationSystemGroup.Timestep  *= 2;
    }
   
    async void LoadGame()
    {
        await _loadingService.LoadWithProgressAsync(
            saveService.LoadGameState,
            LoadGameField
        );
    }
    
    async Task LoadGameField()
    {
        var save = saveService.GameState;
        await EntityLoader.LoadSavedEntitiesAsync(save);
        
        cameraController.SetUp(save.camData);
        cameraController.enabled = true;
        _buildingPositionSystem.ForceImmediateRebuild();
    }

   public void PlaceBuilding(PosData posData, BuildingPosData buildingPosData, bool IsPhantom)
    {
        var UnicID = posData.UnicIDHash;
        var save = saveService.GameState;
        save.posDatas.Add(UnicID, posData);
        save.buildingPosDatas.Add(UnicID, buildingPosData);
        
        var info = _buildingInfo.BuildingInfos[posData.BuildingIDHash];
        if (info.typeOfLogic != TypeOfLogic.None)
        {
            BuildingLogicData logicData = new BuildingLogicData()
            {
                Priority = (int)DistributionPriority.Middle,
                RequiredRecipesGroup = (int)info.requiredRecipesGroup,
                RecipeIDHash = 0,
            };
            save.buildingLogicDatas.Add(UnicID, logicData);

            switch (info.typeOfLogic)
            {
                case TypeOfLogic.Consuming:
                    save.consumerBuildingDatas.Add(UnicID, new ConsumerBuildingData());
                    break;
                case TypeOfLogic.Production:
                    save.producerBuildingDatas.Add(UnicID, new ProducerBuildingData());
                    break;
                case TypeOfLogic.Procession:
                    save.consumerBuildingDatas.Add(UnicID, new ConsumerBuildingData());
                    save.producerBuildingDatas.Add(UnicID, new ProducerBuildingData());
                    break;
            }
        }
        if (IsPhantom) save.phantomBuildings.Add(UnicID);
        EntityLoader.CreateSavedBuildingEntity(UnicID, save);
        
        _buildingPositionSystem.ForceImmediateRebuild();
    }

    public bool CanBuildHere(Vector2Int position)
    {
        // Убрали вызов MarkForRebuild - система сама обновляется
        return _buildingPositionSystem.CanBuildHere(new int2(position.x, position.y));
    }

    public bool CanBuildHereMany(Vector2Int[] positions)
    {
        foreach (var pos in positions)
        {
            if (!_buildingPositionSystem.CanBuildHere(new int2(pos.x, pos.y)))
                return false;
        }
        return true;
    }

    public int GetBuildingInThere(Vector2Int position)
    {
        return _buildingPositionSystem.GetBuildingInThere(new int2(position.x, position.y));
    }

    public int[] GetBuildingInThereMany(Vector2Int[] positions)
    {
        List<int> result = new List<int>();
        foreach (var pos in positions)
        {
            int buildingId = _buildingPositionSystem.GetBuildingInThere(new int2(pos.x, pos.y));
            if (buildingId != -1)
                result.Add(buildingId);
        }
        return result.ToArray();
    }
}