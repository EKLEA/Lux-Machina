using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

[Serializable]
public class RoadData
{
    public string UnicID;
    public Vector2Int startPoint;
    public Vector2Int endPoint;
    public Vector2Int[] GetRoadOccupedCells()
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