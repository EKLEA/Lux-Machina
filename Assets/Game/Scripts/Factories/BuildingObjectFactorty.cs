using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Zenject;
using Unity.Scenes; 
public class BuildingObjectFactorty
{
    readonly IReadOnlyBuildingInfo _buildingInfo;
    readonly IInstantiator _instantiator;
    readonly IReadOnlyGameFieldSettings _gameFieldSettings;
    [Inject]
    public BuildingObjectFactorty(
        IReadOnlyBuildingInfo buildingInfo,
        IInstantiator instantiator,
        IReadOnlyGameFieldSettings gameFieldSettings)
    {
        _buildingInfo = buildingInfo;
        _instantiator = instantiator;
        _gameFieldSettings = gameFieldSettings;
    }
    public GameObject CreateBuilding(PosData posData, BuildingPosData buildingPosData)
    {
        var buildingInfo = _buildingInfo.BuildingInfos[posData.BuildingIDHash];
        var buildingPrefab = buildingInfo.prefab;
        var size = buildingPosData.Rotation % 2 != 0 ?  new Vector3Int(buildingInfo.size.z, buildingInfo.size.y, buildingInfo.size.x):buildingInfo.size;
        
        var buildingOnScene = _instantiator.InstantiatePrefabForComponent<BuildingOnScene>(
            buildingPrefab,
            CalculateWorldPosition(CenterGridPosition(
                new Vector2Int(buildingPosData.LeftCornerPos.x, buildingPosData.LeftCornerPos.y),
               size)),
            GetRotationFromData(buildingPosData.Rotation),
            null
        );
        ApplyExactScale(buildingOnScene.transform, buildingInfo.size);
        return buildingOnScene.gameObject;
    }
    public GameObject CreateRoad(PosData posData, RoadPosData roadPosData)
    {
        var buildingInfo = _buildingInfo.BuildingInfos[posData.BuildingIDHash];
        var buildingPrefab = buildingInfo.prefab;
        var size = buildingInfo.size;

        var roadOnScene = _instantiator.InstantiatePrefabForComponent<RoadOnScene>(
            buildingPrefab,
            CalculateWorldPosition(new Vector3(roadPosData.FirstPoint.x,roadPosData.FirstPoint.y)),
            quaternion.identity,
            null
        );
        roadOnScene.DrawRoad(SplineState.Passive,
        (new Vector3(roadPosData.FirstPoint.x * _gameFieldSettings.cellSize, size.y / 2*_gameFieldSettings.cellSize, roadPosData.FirstPoint.y * _gameFieldSettings.cellSize),
        Quaternion.identity),
        (new Vector3(roadPosData.EndPoint.x * _gameFieldSettings.cellSize, size.y / 2*_gameFieldSettings.cellSize, roadPosData.EndPoint.y * _gameFieldSettings.cellSize),
        Quaternion.identity));
        ApplyExactScale(roadOnScene.transform, size);

        return roadOnScene.gameObject;
    }
    /*public void Modify(Road road)
    {
        var r = road.RoadOnScene;
        r.Reset();
        r.SetFirstPointSpline(new Vector3(road.startPoint.x * _gameFieldSettings.cellSize, 0, road.startPoint.y * _gameFieldSettings.cellSize),quaternion.identity);
        r.SetSecondPointSpline(new Vector3(road.endPoint.x * _gameFieldSettings.cellSize, 0, road.endPoint.y * _gameFieldSettings.cellSize),quaternion.identity);
        r.DrawSpline(SplineState.Passive);
    }*/
    void ApplyExactScale(Transform buildingTransform, Vector3Int buildingSize)
    {
        buildingTransform.localScale = _gameFieldSettings.cellSize * (Vector3)buildingSize;
    }
    
    Vector3 CalculateWorldPosition(Vector3 pos)
    {
        return pos * _gameFieldSettings.cellSize;
    }
    
    Vector3 CenterGridPosition(Vector2Int pos,Vector3Int size)
    {
        return new Vector3(
            pos.x + size.x * 0.5f,
            size.y * 0.5f,
            pos.y + size.z * 0.5f
        );
    }
    
    Quaternion GetRotationFromData(int rotationValue)
    {
        float angle = Mathf.Clamp(rotationValue, 0, 3) * 90f;
        return Quaternion.Euler(0f, angle, 0f);
    }
}