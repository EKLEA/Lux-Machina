using System;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

public class RoadFactory
{
    [Inject] IReadOnlyBuildingInfo _buildingInfo;
    [Inject] IInstantiator instantiator;
    [Inject] IReadOnlyGameFieldSettings _gameFieldSettings;
    public Road Create(RoadData data)
    {
        var buildingInfo = _buildingInfo.BuildingInfos["Road"];
        var buildingPrefab = buildingInfo.prefab;
        var size = buildingInfo.size;

        var roadOnScene = instantiator.InstantiatePrefabForComponent<SplineOnScene>(
            buildingPrefab,
            CalculateWorldPosition(data.startPoint),
            quaternion.identity,
            null
        );
        roadOnScene.Initialize();
        roadOnScene.SetFirstPointSpline(new Vector3(data.startPoint.x * _gameFieldSettings.cellSize, 0, data.startPoint.y * _gameFieldSettings.cellSize),quaternion.identity);
        roadOnScene.SetSecondPointSpline(new Vector3(data.endPoint.x * _gameFieldSettings.cellSize, 0, data.endPoint.y * _gameFieldSettings.cellSize),quaternion.identity);
        roadOnScene.DrawSpline(SplineState.Passive);
        ApplyExactScale(roadOnScene.transform, size);

        var Road = new Road(data);
        Road.Initialize(roadOnScene);
        return Road;
    }
    public void Modify(Road road)
    {
        var r = road.RoadOnScene;
        r.Reset();
        r.SetFirstPointSpline(new Vector3(road.startPoint.x * _gameFieldSettings.cellSize, 0, road.startPoint.y * _gameFieldSettings.cellSize),quaternion.identity);
        r.SetSecondPointSpline(new Vector3(road.endPoint.x * _gameFieldSettings.cellSize, 0, road.endPoint.y * _gameFieldSettings.cellSize),quaternion.identity);
        r.DrawSpline(SplineState.Passive);
    }
    private void ApplyExactScale(Transform buildingTransform, Vector3Int buildingSize)
    {

        buildingTransform.localScale = _gameFieldSettings.cellSize * (Vector3)buildingSize;
    }
    
    private Vector3 CalculateWorldPosition(Vector2Int pos)
    {
       
        return new Vector3(
            pos.x * _gameFieldSettings.cellSize,
            0f,
            pos.y * _gameFieldSettings.cellSize
        );
    }
}
