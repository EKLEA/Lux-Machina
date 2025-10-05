using System;
using System.Collections.Generic;
using UnityEngine;

public class Road
{
    public string UnicID => _data.UnicID;
    public Vector2Int startPoint
    {
        get => _data.startPoint;
        set
        {
            if (Vector2Int.Distance(value, _data.endPoint) != 0)
                _data.startPoint = value;
        }
    }
    public Vector2Int   endPoint
    {
        get => _data.endPoint;
        set
        {
            if (Vector2Int.Distance(value, _data.startPoint) != 0)
                _data.endPoint = value;
        }
    }
    RoadData _data;
    public SplineOnScene RoadOnScene{ get; private set; }
    public Road(RoadData roadData)
    {
        _data = roadData;
    }
    public void Initialize(SplineOnScene roadOnScene)
    {
        RoadOnScene = roadOnScene;
    }
    public Vector2Int[] GetRoadOccupedCells()
    {
        return _data.GetRoadOccupedCells();
    }
}