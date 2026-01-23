using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Scenes;
using UnityEngine;
using Zenject;

public class BuildingObjectFactory
{
    readonly IReadOnlyBuildingInfo _buildingInfo;
    readonly IInstantiator _instantiator;
    readonly IReadOnlyGameFieldSettings _gameFieldSettings;

    [Inject]
    public BuildingObjectFactory(
        IReadOnlyBuildingInfo buildingInfo,
        IInstantiator instantiator,
        IReadOnlyGameFieldSettings gameFieldSettings
    )
    {
        _buildingInfo = buildingInfo;
        _instantiator = instantiator;
        _gameFieldSettings = gameFieldSettings;
    }
    public void DestoryObject(BuildingOnScene BuildingOnScene)
    {
        BuildingOnScene.Dispose();
        Object.DestroyImmediate(BuildingOnScene);
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
        ApplyExactScale(buildingOnScene.transform);
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
    public void MoveBuilding(GameObject buildingOnScene,int buidlingID, Vector2Int pos,int rotation)
    {
        var buildingInfo = _buildingInfo.BuildingInfos[buidlingID];
        var size =
            rotation % 2 != 0
                ? new Vector3Int(buildingInfo.size.z, buildingInfo.size.y, buildingInfo.size.x)
                : buildingInfo.size;
        buildingOnScene.transform.position=CalculateWorldPosition(CenterGridPosition(pos, size));
        buildingOnScene.transform.rotation=   GetRotationFromData(rotation);
    }
    void ApplyExactScale(Transform buildingTransform)
    {
        buildingTransform.localScale *= _gameFieldSettings.cellSize;
    }

    Vector3 CalculateWorldPosition(Vector3 pos)
    {
        return pos * _gameFieldSettings.cellSize;
    }

    Vector3 CenterGridPosition(Vector2Int pos, Vector3 size)
    {
        return new Vector3(pos.x + size.x * 0.5f, 0, pos.y + size.z * 0.5f);
    }

    Quaternion GetRotationFromData(int rotationValue)
    {
        float angle = Mathf.Clamp(rotationValue, 0, 3) * 90f;
        return Quaternion.Euler(0f, angle, 0f);
    }
}
