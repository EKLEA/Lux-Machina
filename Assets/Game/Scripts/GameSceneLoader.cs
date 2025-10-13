using System;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

public class GameSceneLoader
{
    private readonly IInstantiator _instantiator; 
    private readonly ILoadingService _loadingService;
    private readonly SaveService _saveService;
    private readonly DiContainer _container;
    
    public BuildingsVisualService buildingsVisualService { get; private set; }
    public BuildingsLogicService buildingsLogicService { get; private set; }
    public BuildingsHealthService buildingsHealghService { get; private set; }
    public VirtualLogisticsCenterService virtualLogisticsCenterService { get; private set; }
    [Inject]
    public GameSceneLoader(ILoadingService loadingService, SaveService saveService, DiContainer container)
    {
        _saveService = saveService;
        _loadingService = loadingService;
        _container = container;
    }

    public async void LoadGameAsync()
    {
        await _loadingService.LoadWithProgressAsync(
            _saveService.LoadGameState,
            LoadGameField
        );
    }

    async Task LoadGameField()
    {
        var save = _saveService.GameState;
        
        buildingsVisualService = _container.Instantiate<BuildingsVisualService>(new object[] { save });
        await buildingsVisualService.LoadBuildingsFromSave();
        
        buildingsLogicService = _container.Instantiate<BuildingsLogicService>(new object[] { save, buildingsVisualService });
        await buildingsLogicService.LoadBuildingsLogicFromSave();
        
        buildingsHealghService = _container.Instantiate<BuildingsHealthService>(new object[] { save, buildingsVisualService });
        await buildingsHealghService.LoadBuildingsHealthFromSave();
        
        virtualLogisticsCenterService = _container.Instantiate<VirtualLogisticsCenterService>(new object[] { save, buildingsVisualService, buildingsLogicService });
        await virtualLogisticsCenterService.LoadCenters();
    }
}