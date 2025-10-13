using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Zenject;

public class BuildingsLogicService:ReadOnlyBuildingsLogicService
{
    [Inject] SignalBus _signalBus;
    GameStateData _gameStateData;
    public ReadOnlyDictionary<string, BuildingLogic> Buildings => new(buildingsLogic);
    public Action<string> OnLogicDeleted;
    Dictionary<string, BuildingLogic> buildingsLogic;
    BuildingsVisualService _buildingsVisualController;
    BuildingLogicFactory factory;

    public BuildingsLogicService(GameStateData gameStateData, BuildingsVisualService buildingsVisualService,BuildingLogicFactory buildingLogicFactory)
    {
        _gameStateData = gameStateData;
        _buildingsVisualController = buildingsVisualService;
        buildingsLogic = new();
        factory = buildingLogicFactory;
    }

    public async Task LoadBuildingsLogicFromSave()
    {
        if (_gameStateData.buildingLogicDatas.Count > 0)
        {
            var tasks = _gameStateData.buildingLogicDatas.Values
                .Select(buildingLogicData => CreateNewLogic(buildingLogicData))
                .ToArray();

            await Task.WhenAll(tasks);
        }
    }

    public async Task CreateNewLogic(BuildingLogicData buildingLogicData)
    {
        var buildingLogic = factory.Create(buildingLogicData,
            _buildingsVisualController.Buildings[buildingLogicData.UnicID].buildingOnScene);

        var visual = _buildingsVisualController.Buildings[buildingLogicData.UnicID];
        visual.OnBuildingDestroy += RemoveBuilding;

        buildingLogic.OnSubscriptionShouldChange += UpdateBuildingSubscription;

        buildingsLogic.Add(buildingLogicData.UnicID, buildingLogic);

        UpdateBuildingSubscription(buildingLogicData.UnicID);

        await Task.Yield();
    }


    private void RemoveBuilding(string unicID)
    {
        var building = buildingsLogic[unicID];
        building.OnSubscriptionShouldChange -= UpdateBuildingSubscription;
        _signalBus.Unsubscribe<TickableEvent>(building.LogicPerTick);
        building.SetConnected(false);
        buildingsLogic.Remove(unicID);
        _gameStateData.buildingLogicDatas.Remove(unicID);
    }

   private void UpdateBuildingSubscription(string unicID)
    {
        var building = buildingsLogic[unicID];
        bool shouldSubscribe = building.ShouldSubscribeToTicks();
        
        if (shouldSubscribe)
            _signalBus.Subscribe<TickableEvent>(building.LogicPerTick);
        else
            _signalBus.TryUnsubscribe<TickableEvent>(building.LogicPerTick);
    }
}
public interface ReadOnlyBuildingsLogicService
{
    public ReadOnlyDictionary<string, BuildingLogic> Buildings { get; }
    public Task CreateNewLogic(BuildingLogicData buildingLogicData);
}