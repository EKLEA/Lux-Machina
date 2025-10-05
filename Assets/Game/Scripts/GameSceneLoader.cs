using System;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

public class GameSceneLoader : IInitializable
{
    ILoadingService _loadingService;
    SaveService _saveService;
    public BuildingsVisualService buildingsVisualService { get; private set; }
    public BuildingsLogicService buildingsLogicService { get; private set; }
    public BuildingsHealthService buildingsHealghService { get; private set; }
    public VirtualLogisticsCenter virtualLogisticsCenter{ get; private set; }
    public GameSceneLoader(ILoadingService loadingService, SaveService saveService)
    {
        _saveService = saveService;
        _loadingService = loadingService;
    }

    public async void Initialize()
    {
        await LoadGameAsync();
    }

    async Task LoadGameAsync()
    {
        await _loadingService.LoadWithProgressAsync(
            _saveService.LoadGameState,
            LoadGameField
        );
        
    }

    async Task LoadGameField()
    {
        var save = _saveService.GameState;
        if (!save.buildingVisualDatas.ContainsKey("Core") ||
            !save.buildingLogicDatas.ContainsKey("Core") ||
            !save.buildingHealthData.ContainsKey("Core"))
        {
            save.buildingVisualDatas.Add("Core", new BuildingVisualData
            {
                UnicID = "Core",
                leftCornerPos = new Vector2Int(-1, -1),
                rotation = 0,
                buildingID = "Core"
            });
            save.buildingLogicDatas.Add("Core", new BuildingLogicData
            {
                UnicID = "Core",
                buildingID = "Core",
                //innerStorageSlots=рецепт конфиг от ядра
                priority = Priority.Hight,
            });
            save.buildingHealthData.Add("Core", new BuildingHealthData
            {
                UnicID = "Core",
                buildingID = "Core"
            });
        }
        buildingsVisualService = new(save);
        await buildingsVisualService.LoadBuildingsFromSave();
        buildingsLogicService = new(save, buildingsVisualService);
        await buildingsLogicService.LoadBuildingsLogicFromSave();
        buildingsHealghService = new(save, buildingsVisualService);
        await buildingsHealghService.LoadBuildingsHealthFromSave();
        //дороги и лэпы
       // virtualLogisticsCenter = new VirtualLogisticsCenter(buildingsLogicService);
    }
    //setupUI
}