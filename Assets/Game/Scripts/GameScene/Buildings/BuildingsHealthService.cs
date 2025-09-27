using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Zenject;

public class BuildingsHealthService
{
    [Inject] SignalBus _signalBus;
    GameStateData _gameStateData;
    public ReadOnlyDictionary<string, BuildingHealth> Buildings => new(buildingsHealth);
    Dictionary<string, BuildingHealth> buildingsHealth;
    BuildingsVisualService _buildingsVisualController;
    BuildingHealthFactory factory;
    public BuildingsHealthService(GameStateData gameStateData, BuildingsVisualService buildingsVisualService)
    {
        _gameStateData = gameStateData;
        _buildingsVisualController = buildingsVisualService;
        buildingsHealth = new();
        factory = new();
    }

    public async Task LoadBuildingsHealthFromSave()
    {
        if (_gameStateData.buildingHealthData.Count > 0)
            foreach (var b in _gameStateData.buildingHealthData.Values)
                await CreateNewBuildingHealth(b);
    }

    public async Task CreateNewBuildingHealth(BuildingHealthData buildingHealthData)
    {
        var c = factory.Create(buildingHealthData);
        var visual = _buildingsVisualController.Buildings[buildingHealthData.UnicID];
        visual.OnBuildingDestroy += RemoveBuilding;
        c.OnDead += visual.DestroyBuilding;
        c.IHealAllHP += ChangeSubscibe;
        buildingsHealth.Add(buildingHealthData.UnicID, c);
        await Task.Yield();
    }
    void RemoveBuilding(string UnicID)
    {
        ChangeSubscibe(UnicID, false);
        buildingsHealth.Remove(UnicID);
        _gameStateData.buildingHealthData.Remove(UnicID);
    }
    void ChangeSubscibe(string UnicID, bool b)
    {
        if (!b) _signalBus.Subscribe<TickableEvent>(buildingsHealth[UnicID].RestoreHealth);
        else _signalBus.Unsubscribe<TickableEvent>(buildingsHealth[UnicID].RestoreHealth);
        buildingsHealth[UnicID].ISubscibed = !b;
    }
}