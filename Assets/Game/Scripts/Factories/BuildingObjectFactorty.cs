using Unity.Entities;
using Unity.Mathematics;
using Unity.Scenes;
using UnityEngine;
using Zenject;

public class BuildingObjectFactorty
{
    readonly IReadOnlyBuildingInfo _buildingInfo;
    readonly IInstantiator _instantiator;
    readonly IReadOnlyGameFieldSettings _gameFieldSettings;

    [Inject]
    public BuildingObjectFactorty(
        IReadOnlyBuildingInfo buildingInfo,
        IInstantiator instantiator,
        IReadOnlyGameFieldSettings gameFieldSettings
    )
    {
        _buildingInfo = buildingInfo;
        _instantiator = instantiator;
        _gameFieldSettings = gameFieldSettings;
    }

    public BuildingOnScene CreateBuilding(int buidlingID, Vector2Int pos, int rotation)
    {
        var buildingInfo = _buildingInfo.BuildingInfos[buidlingID];
        var buildingPrefab = _buildingInfo.GetBuildingPrefab(buidlingID);
        var size =
            rotation % 2 != 0
                ? new Vector3Int(buildingInfo.size.z, buildingInfo.size.y, buildingInfo.size.x)
                : buildingInfo.size;

        var buildingOnScene = _instantiator.InstantiatePrefabForComponent<BuildingOnScene>(
            buildingPrefab,
            CalculateWorldPosition(CenterGridPosition(pos, size)),
            GetRotationFromData(rotation),
            null
        );
        ApplyExactScale(buildingOnScene.transform, buildingInfo.size);
        return buildingOnScene;
    }

    public RoadOnScene CreateRoad(int buildingID, Vector2Int[] points)
    {
        Debug.Log($"CreateRoad вызван с ID: {buildingID}, точек: {points.Length}");

        if (!_buildingInfo.BuildingInfos.TryGetValue(buildingID, out var info))
        {
            Debug.LogError($"Не найден префаб для buildingID: {buildingID}");
            return null;
        }

        var roadObject = GameObject.Instantiate(_buildingInfo.GetBuildingPrefab(buildingID));
        var roadOnScene = roadObject.GetComponent<RoadOnScene>();

        if (roadOnScene != null)
        {
            roadOnScene.Init(_gameFieldSettings.cellSize);
            roadOnScene.GenerateRoadMesh(points);
            Debug.Log($"Дорога создана: {roadObject.name}");
        }
        else
        {
            Debug.LogError($"Компонент RoadOnScene не найден на префабе: {roadObject.name}");
        }

        return roadOnScene;
    }

    void ApplyExactScale(Transform buildingTransform, Vector3Int buildingSize)
    {
        buildingTransform.localScale = _gameFieldSettings.cellSize * (Vector3)buildingSize;
    }

    Vector3 CalculateWorldPosition(Vector3 pos)
    {
        return pos * _gameFieldSettings.cellSize;
    }

    Vector3 CenterGridPosition(Vector2Int pos, Vector3 size)
    {
        return new Vector3(pos.x + size.x * 0.5f, size.y * 0.5f, pos.y + size.z * 0.5f);
    }

    Quaternion GetRotationFromData(int rotationValue)
    {
        float angle = Mathf.Clamp(rotationValue, 0, 3) * 90f;
        return Quaternion.Euler(0f, angle, 0f);
    }
}
