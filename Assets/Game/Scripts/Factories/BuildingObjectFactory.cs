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
        Object.DestroyImmediate(BuildingOnScene.gameObject);
    }
    public GameObject CreatePrimitive(Vector2Int pos,bool IsRemove=false)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.localScale=(Vector3.one+Vector3.up*2)*_gameFieldSettings.cellSize;
        cube.transform.position = CalculateWorldPosition(CenterGridPosition(pos, Vector3.one));
        if (IsRemove)
        {
            int mask = _gameFieldSettings.removeLayer.value;
            int layerIndex = 0;
            while (mask > 1) { mask >>= 1; layerIndex++; }
            
            //cube.layer=layerIndex;
        }
            
            
        return cube;
    }
    public BuildingOnScene CreateBuilding(int buidlingID, Vector2Int pos, int rotation,bool IsPlaceDestroy=false)
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
        if(IsPlaceDestroy)
        {
            int mask = _gameFieldSettings.removeLayer.value;
            int layerIndex = 0;
            while (mask > 1) { mask >>= 1; layerIndex++; }
            
            buildingOnScene.gameObject.layer = layerIndex;
        }
        return buildingOnScene;
    }

    public RoadOnScene CreateRoad(int buildingID, Vector2Int[] points,Dictionary<Vector2Int, bool> neighborsMap,bool IsPlaceDestroy=false)
    {

        if (!_buildingInfo.BuildingInfos.TryGetValue(buildingID, out var info))
        {
            return null;
        }

        var roadObject = GameObject.Instantiate(_buildingInfo.GetBuildingPrefab(buildingID));
        var roadOnScene = roadObject.GetComponent<RoadOnScene>();

        if (roadOnScene != null)
        {
            roadOnScene.Init(_gameFieldSettings.cellSize);
            roadOnScene.GenerateRoadMesh(points,neighborsMap);
        }
        if(IsPlaceDestroy)
        {
            int mask = _gameFieldSettings.removeLayer.value;
            int layerIndex = 0;
            while (mask > 1) { mask >>= 1; layerIndex++; }
            
            roadOnScene.gameObject.layer = layerIndex;
        }
        return roadOnScene;
    }
    public void MoveBuilding(GameObject buildingOnScene, Vector2Int pos,int rotation=-1,int buidlingID=-1)
    {
        Vector3Int size=Vector3Int.FloorToInt( new Vector3(buildingOnScene.transform.localScale.x/_gameFieldSettings.cellSize,0,buildingOnScene.transform.localScale.z/_gameFieldSettings.cellSize));
        if (buidlingID != -1)
        {
             var buildingInfo = _buildingInfo.BuildingInfos[buidlingID];
            size =
                rotation % 2 != 0
                    ? new Vector3Int(buildingInfo.size.z, buildingInfo.size.y, buildingInfo.size.x)
                    : buildingInfo.size;
            
            buildingOnScene.transform.rotation=   GetRotationFromData(rotation);
        }
        buildingOnScene.transform.position=CalculateWorldPosition(CenterGridPosition(pos, size));
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
