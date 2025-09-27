
using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class BuildingVisual
{
    public Vector2Int leftCornerPos { get => data.leftCornerPos; }
    public string buildingID{ get => data.buildingID; }
    public string UnicID{ get => data.UnicID; }
    public Action<string> OnBuildingDestroy;
    Vector3Int size;
    public BuildingOnScene buildingOnScene{ get; private set; }
    BuildingVisualData data;
    public BuildingVisual(BuildingVisualData buildingControllerData)
    {
        data = buildingControllerData;
    }
    
    public void Initialize(BuildingOnScene building, Vector3Int size)
    {
        this.buildingOnScene = building;
        this.size = size;
    }
    public void DestroyBuilding()
    {
        OnBuildingDestroy?.Invoke(UnicID);
        GameObject.Destroy(buildingOnScene.gameObject);
    } 
    public List<Vector2Int> GetOccupiedCells()
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.z; y++)
            {
                cells.Add(leftCornerPos + new Vector2Int(x, y));
            }
        }
        return cells;
    }
}