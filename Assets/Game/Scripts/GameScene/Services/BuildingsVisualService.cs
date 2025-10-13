using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

public class BuildingsVisualService:ReadOnlyBuildingsVisualService
{
    GameStateData _gameStateData;
    BuildingVisualFactory factory;

    public ReadOnlyDictionary<string, BuildingVisual> Buildings => new(buildings);
    Dictionary<string, BuildingVisual> buildings;
    public BuildingsVisualService(GameStateData gameStateData,BuildingVisualFactory buildingFactory)
    {
        _gameStateData = gameStateData;
        buildings = new();
        factory = buildingFactory;
    }
    public async Task LoadBuildingsFromSave()
    {
        if (_gameStateData.buildingVisualDatas.Count > 0)
        {
            var tasks = _gameStateData.buildingVisualDatas.Values
                .Select(buildingVisualDatas => PlaceBuilding(buildingVisualDatas))
                .ToArray();

            await Task.WhenAll(tasks);
        }
    }

    public async Task PlaceBuilding(BuildingVisualData buildingVisualData)
    {
        var c = factory.Create(buildingVisualData);
        c.OnBuildingDestroy += RemoveBuilding;
        buildings.Add(buildingVisualData.UnicID, c);
        await Task.Yield();
    }
    public string[] GetUnicIDsAroundPoint(Vector2Int pos)
    {
        var firstBuildingSample = Buildings.Where(f => Vector2Int.Distance(f.Value.leftCornerPos, pos) <= 10);
        List<string> res = new();
        for (int x = -1; x < 2; x++)
            for (int y = -1; y < 2; y++)
                if (x == 0 && y == 0)
                    continue;
                else
                    res.Add(firstBuildingSample.First(f => f.Value.GetOccupiedCells().Contains(pos + new Vector2Int(x, y))).Key);
        return res.ToArray();
    }
    void RemoveBuilding(string UnicID)
    {
        buildings.Remove(UnicID);
        _gameStateData.buildingVisualDatas.Remove(UnicID);
    }

    public string[] GetUnicIDsAroundPoints(Vector2Int[] poss)
    {
        List<string> res = new();
        foreach (var pos in poss)
            res.AddRange(GetUnicIDsAroundPoint(pos).ToList());
        return res.ToArray();
    }
}
public interface ReadOnlyBuildingsVisualService
{
    public ReadOnlyDictionary<string, BuildingVisual> Buildings{ get; }
    Task PlaceBuilding(BuildingVisualData buildingVisualData);
    string[] GetUnicIDsAroundPoint(Vector2Int pos);
    string[] GetUnicIDsAroundPoints(Vector2Int[] poss);
}