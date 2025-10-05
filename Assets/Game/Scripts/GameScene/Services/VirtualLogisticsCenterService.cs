using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor.Rendering.LookDev;
using UnityEngine;

public class VirtualLogisticsCenterService
{
    GameStateData _gameStateData;
    Dictionary<string, VirtualLogisticsCenter> logisticsCenters;
    Dictionary<Vector2Int, Road> roadMap;
    RoadFactory roadFactory;
    public VirtualLogisticsCenterService(GameStateData data)
    {
        _gameStateData = data;
        logisticsCenters = new();
        roadFactory = new();
        roadMap = new();
    }
    public async Task LoadCenters()
    {
        if (_gameStateData.virtualLogisticsCentersData.Count > 0)
            foreach (var vc in _gameStateData.virtualLogisticsCentersData.Values)
                await CreateCenter(vc);
    }
    async Task CreateCenter(VirtualLogisticsCenterData data)
    {
        var vc = new VirtualLogisticsCenter(data, roadFactory);
        await vc.LoadRoads();
        foreach (var r in vc.Roads)
        {
            roadMap.AddRange(vc.Roads.Values
            .SelectMany(road => road.GetRoadOccupedCells().Select(cell => new { cell, road }))
            .ToDictionary(x => x.cell, x => x.road));
        }
        await Task.Yield();
    }
    public async Task CreateRoad(RoadData data)
    {
        List<VirtualLogisticsCenter> closestVC = new();
        foreach (var cell in data.GetRoadOccupedCells())
        {
            if (roadMap.ContainsKey(cell))
                closestVC.Add(logisticsCenters.First(f => f.Value.Roads.ContainsKey(roadMap[cell].UnicID)).Value);
        }
        VirtualLogisticsCenter currCenter;
        switch (closestVC.Count)
        {
            case > 0:
                var first = closestVC[0];
                currCenter = first;
                closestVC.Remove(first);
                foreach (var vs in closestVC)
                {
                    await first.MergeData(vs.GetDataForMerge());
                    logisticsCenters.Remove(vs.UnicID);
                    _gameStateData.virtualLogisticsCentersData.Remove(vs.UnicID);
                }
                currCenter = first;
                break;
            default:
                var newdata = new VirtualLogisticsCenterData
                {
                    UnicID = Guid.NewGuid().ToString()
                };
                _gameStateData.virtualLogisticsCentersData.Add(newdata.UnicID, newdata);
                currCenter = new VirtualLogisticsCenter(newdata, roadFactory);
                logisticsCenters.Add(currCenter.UnicID, currCenter);
                break;
        }
        await currCenter.CreateRoad(data);
    }
    public void RemoveRoadCells(Vector2Int startPoint, Vector2Int endPoint)
    {
        var cells = GetRoadOccupedCells(startPoint, endPoint);
        List<string> roadsF = new();
        foreach (var v in cells)
            foreach (var cen in logisticsCenters.Values)
            {
                var temp = cen.ContainInOtherRoadsThisCell(v);
                foreach (var t in temp)
                {
                    cen.RemoveCell(t, v);
                    roadsF.Add(t);
                }
            }
    }
     Vector2Int[] GetRoadOccupedCells(Vector2Int startPoint, Vector2Int endPoint)
    {
        var points = new List<Vector2Int>();

        int dx = Math.Abs(endPoint.x - startPoint.x);
        int dy = Math.Abs(endPoint.y - startPoint.y);
        int steps = Math.Max(dx, dy);

        for (int i = 1; i < steps; i++)
        {
            float t = (float)i / steps;
            int x = (int)Math.Round(startPoint.x + (endPoint.x - startPoint.x) * t);
            int y = (int)Math.Round(startPoint.y + (endPoint.y - startPoint.y) * t);
            points.Add(new Vector2Int(x, y));
        }

        return points.ToArray();
    }
}