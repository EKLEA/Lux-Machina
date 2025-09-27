using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Zenject;

public class BuildingsLogicService
{
    [Inject] SignalBus _signalBus;
    GameStateData _gameStateData;
    public ReadOnlyDictionary<string, BuildingLogic> Buildings => new(buildingsLogic);
    Dictionary<string, BuildingLogic> buildingsLogic;
    BuildingsVisualService _buildingsVisualController;
    BuildingLogicFactory factory;
    public BuildingsLogicService(GameStateData gameStateData, BuildingsVisualService buildingsVisualService)
    {
        _gameStateData = gameStateData;
        _buildingsVisualController = buildingsVisualService;
        buildingsLogic = new();
        factory = new();
    }
    public async Task LoadBuildingsLogicFromSave()
    {
        if (_gameStateData.buildingLogicDatas.Count > 0)
            foreach (var b in _gameStateData.buildingLogicDatas.Values)
                await CreateNewLogic(b);
    }

    public async Task CreateNewLogic(BuildingLogicData buildingLogicData)
    {
        var c = factory.Create(buildingLogicData, _buildingsVisualController.Buildings[buildingLogicData.UnicID].buildingOnScene);
        _buildingsVisualController.Buildings[buildingLogicData.UnicID].OnBuildingDestroy += RemoveBuilding;
        c.IChangeWorkStatusTo += ChangeSubscibe;
        buildingsLogic.Add(buildingLogicData.UnicID, c);
        await Task.Yield();
    }
    void RemoveBuilding(string UnicID)
    {
        ChangeSubscibe(UnicID, false);
        buildingsLogic.Remove(UnicID);
        _gameStateData.buildingLogicDatas.Remove(UnicID);
    }
    void ChangeSubscibe(string UnicID, bool b)
    {
        if (b) _signalBus.Subscribe<TickableEvent>(buildingsLogic[UnicID].LogicPerTick);
        else _signalBus.Unsubscribe<TickableEvent>(buildingsLogic[UnicID].LogicPerTick);
        buildingsLogic[UnicID].ISubscibed = b;
    }
}