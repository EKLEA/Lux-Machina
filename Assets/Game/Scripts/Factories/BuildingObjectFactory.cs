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
    public GameObject CreatePrimitive(Vector3Int pos,bool IsRemove=false)
    {
        GameObject cube  = _instantiator.InstantiatePrefab(_buildingInfo.primitive);
        cube.transform.localScale=(Vector3.one+Vector3.up*2)*_gameFieldSettings.cellSize;
        cube.transform.position = CalculateWorldPosition(CenterGridPosition(pos, Vector3.one));
        if (IsRemove)
        {
            int layerIndex = Mathf.RoundToInt(Mathf.Log(_gameFieldSettings.removeLayer.value, 2));
            cube.layer = layerIndex;
        } 
                    
        return cube;
    }
    public BuildingOnScene CreateBuilding(int buidlingID, Vector3Int pos, int rotation,bool IsPlaceDestroy=false)
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

    public ManyPointsBuildingInstanced CreateManyPoint(int buildingID, Vector3Int[] points,Dictionary<Vector3Int, bool> neighborsMap,bool IsPlaceDestroy=false)
    {

        if (!_buildingInfo.BuildingInfos.TryGetValue(buildingID, out var info))
        {
            return null;
        }

        var manyPointBuildingObject = GameObject.Instantiate(_buildingInfo.GetBuildingPrefab(buildingID));
        var manyPointBuildingOnScene = manyPointBuildingObject.GetComponent<ManyPointsBuildingInstanced>();

        if (manyPointBuildingOnScene != null)
        {
            manyPointBuildingOnScene.Init(_gameFieldSettings.cellSize);
            manyPointBuildingOnScene.Generate(points,neighborsMap);
        }
        if(IsPlaceDestroy)
        {
            int mask = _gameFieldSettings.removeLayer.value;
            int layerIndex = 0;
            while (mask > 1) { mask >>= 1; layerIndex++; }
            
            manyPointBuildingOnScene.gameObject.layer = layerIndex;
        }
        return manyPointBuildingOnScene;
    }
    public void MoveBuilding(GameObject buildingOnScene, Vector3Int pos,int rotation=-1,int buidlingID=-1)
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

    Vector3 CenterGridPosition(Vector3Int pos, Vector3 size)
    {
        return new Vector3(pos.x + size.x * 0.5f, pos.y, pos.z + size.z * 0.5f);
    }

    Quaternion GetRotationFromData(int rotationValue)
    {
        float angle = Mathf.Clamp(rotationValue, 0, 3) * 90f;
        return Quaternion.Euler(0f, angle, 0f);
    }
}
