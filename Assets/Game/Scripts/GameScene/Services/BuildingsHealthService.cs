using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Zenject;

public class BuildingsHealthService
{
    [Inject] private SignalBus _signalBus;
    private GameStateData _gameStateData;
    
    public ReadOnlyDictionary<string, BuildingHealth> Buildings => new(buildingsHealth);
    private Dictionary<string, BuildingHealth> buildingsHealth;
    private BuildingsVisualService _buildingsVisualController;
    private BuildingHealthFactory factory;

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
        var buildingHealth = factory.Create(buildingHealthData);
        var visual = _buildingsVisualController.Buildings[buildingHealthData.UnicID];
        
        visual.OnBuildingDestroy += RemoveBuilding;
        buildingHealth.OnDead += visual.DestroyBuilding;
        buildingHealth.OnHealthStateChanged += OnBuildingHealthStateChanged;
        
        buildingsHealth.Add(buildingHealthData.UnicID, buildingHealth);
        await Task.Yield();
    }

    private void RemoveBuilding(string unicID)
    {
        if (buildingsHealth.TryGetValue(unicID, out var building))
        {
            building.OnHealthStateChanged -= OnBuildingHealthStateChanged;
            _signalBus.Unsubscribe<TickableEvent>(building.RestoreHealth);
        }
        
        buildingsHealth.Remove(unicID);
        _gameStateData.buildingHealthData.Remove(unicID);
    }

    private void OnBuildingHealthStateChanged(string unicID, bool isFullHealth)
    {
        if (buildingsHealth.TryGetValue(unicID, out var building))
        {
            if (isFullHealth)
                _signalBus.Unsubscribe<TickableEvent>(building.RestoreHealth);
            else
                _signalBus.Subscribe<TickableEvent>(building.RestoreHealth);
        }
    }
}