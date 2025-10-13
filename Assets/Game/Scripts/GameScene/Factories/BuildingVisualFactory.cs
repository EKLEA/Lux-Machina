using UnityEngine;
using Zenject;

public class BuildingVisualFactory
{
    readonly IReadOnlyBuildingInfo _buildingInfo;
    readonly IInstantiator _instantiator;
    readonly IReadOnlyGameFieldSettings _gameFieldSettings;

    [Inject]
    public BuildingVisualFactory(
        IReadOnlyBuildingInfo buildingInfo,
        IInstantiator instantiator,
        IReadOnlyGameFieldSettings gameFieldSettings)
    {
        _buildingInfo = buildingInfo;
        _instantiator = instantiator;
        _gameFieldSettings = gameFieldSettings;
    }

    public BuildingVisual Create(BuildingVisualData data)
    {
        var buildingInfo = _buildingInfo.BuildingInfos[data.buildingID];
        var buildingPrefab = buildingInfo.prefab;   
        var size = buildingInfo.size;
        
        var buildingOnScene = _instantiator.InstantiatePrefabForComponent<BuildingOnScene>(
            buildingPrefab, 
            CalculateWorldPosition(data), 
            GetRotationFromData(data.rotation), 
            null
        );
        buildingOnScene.UnicID = data.UnicID;
        ApplyExactScale(buildingOnScene.transform, size);
        
        var buildingVisual = new BuildingVisual(data);
        buildingVisual.Initialize(buildingOnScene, size);
        return buildingVisual;
    }
    
    private void ApplyExactScale(Transform buildingTransform, Vector3Int buildingSize)
    {

        buildingTransform.localScale = _gameFieldSettings.cellSize* (Vector3)buildingSize;
    }
    
    private Vector3 CalculateWorldPosition(BuildingVisualData data)
    {
        Vector2 centerGridPos = CenterGridPosition(data);
        return new Vector3(
            centerGridPos.x * _gameFieldSettings.cellSize,
            _buildingInfo.BuildingInfos[data.buildingID].size.y* _gameFieldSettings.cellSize/2,
            centerGridPos.y * _gameFieldSettings.cellSize
        );
    }
    
    private Vector2 CenterGridPosition(BuildingVisualData data)
    {
        var size = _buildingInfo.BuildingInfos[data.buildingID].size;
        return new Vector2(
            data.leftCornerPos.x + size.x * 0.5f,
            data.leftCornerPos.y + size.y * 0.5f
        );
    }
    
    private Quaternion GetRotationFromData(int rotationValue)
    {
        float angle = Mathf.Clamp(rotationValue, 0, 3) * 90f;
        return Quaternion.Euler(0f, angle, 0f);
    }
}