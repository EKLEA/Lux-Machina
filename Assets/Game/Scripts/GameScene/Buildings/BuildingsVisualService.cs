using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

public class BuildingsVisualService
{
    GameStateData _gameStateData;
    BuildingVisualFactory factory;

    public ReadOnlyDictionary<string, BuildingVisual> Buildings => new(buildings);
    Dictionary<string, BuildingVisual> buildings;
    public BuildingsVisualService(GameStateData gameStateData)
    {
        _gameStateData = gameStateData;
        buildings = new();
        factory = new();
    }
    public async Task LoadBuildingsFromSave()
    {
        if (_gameStateData.buildingVisualDatas.Count > 0)
            foreach (var b in _gameStateData.buildingVisualDatas.Values)
                await PlaceBuilding(b);
    }

    public async Task PlaceBuilding(BuildingVisualData buildingVisualData)
    {
        var c =factory.Create(buildingVisualData);
        c.OnBuildingDestroy += RemoveBuilding;
        buildings.Add(buildingVisualData.UnicID, c);
        await Task.Yield();
    }
    void RemoveBuilding(string UnicID)
    {
        buildings.Remove(UnicID);
        _gameStateData.buildingVisualDatas.Remove(UnicID);
    }
}