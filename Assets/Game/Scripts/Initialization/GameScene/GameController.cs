using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

public class GameController : IInitializable
{
    [Inject] private IReadOnlyGameFieldSettings gameFieldSettings;
    [Inject] private IReadOnlyBuildingInfo _buildingInfo;
    [Inject] private SaveService saveService;
    [Inject] private ILoadingService _loadingService;
    [Inject] private CameraController cameraController;
    [Inject] private EntityLoader EntityLoader;
    [Inject] private BuildingPositionSystem _buildingPositionSystem;

    public void Initialize()
    {
        Debug.Log("GameController Initialized");
        Time.fixedDeltaTime = 1 / gameFieldSettings.tickPerSecond;

        _buildingPositionSystem.Enabled = true;
        
        LoadGame();
    }

    public void SpeedUpTick()
    {
        Time.fixedDeltaTime /= 2;
    }
    
    public void SlowDownTick()
    {
        Time.fixedDeltaTime *= 2;
    }
    
    public PhantomObject GeneratePhantom(string id)
    {
        return null;
    }
    
    public void PlaceBuilding(PosData posData, BuildingPosData buildingPosData)
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
        Debug.Log("заргестрировал");
        EntityLoader.CreateSavedBuildingEntity(UnicID, save);
        
        // Помечаем здание как измененное для системы позиций
        // (нужно будет реализовать в EntityLoader)
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
        Debug.Log("Game loaded successfully");

        cameraController.SetUp(save.camData);
        cameraController.enabled = true;
    }

   public bool CanBuildHere(Vector2Int position)
    {
        _buildingPositionSystem.MarkForRebuild();
        return _buildingPositionSystem.CanBuildHere(new int2(position.x, position.y));
    }

    public bool CanBuildHereMany(Vector2Int[] positions)
    {
        _buildingPositionSystem.MarkForRebuild();
        
        foreach (var pos in positions)
        {
            if (!_buildingPositionSystem.CanBuildHere(new int2(pos.x, pos.y)))
                return false;
        }
        return true;
    }

    public int GetBuildingInThere(Vector2Int position)
    {
        _buildingPositionSystem.MarkForRebuild();
        return _buildingPositionSystem.GetBuildingInThere(new int2(position.x, position.y));
    }

    public int[] GetBuildingInThereMany(Vector2Int[] positions)
    {
        _buildingPositionSystem.MarkForRebuild();
        
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