using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

public class GameController : IInitializable
{
    [Inject]
    IReadOnlyGameFieldSettings gameFieldSettings;

    [Inject]
    IReadOnlyBuildingInfo _buildingInfo;

    [Inject]
    SaveService saveService;

    [Inject]
    ILoadingService _loadingService;

    [Inject]
    CameraController cameraController;

    [Inject]
    EntityLoader EntityLoader;

    [Inject]
    PublicBuildingMapSystem _buildingMapSystem;

    [Inject]
    FixedStepSimulationSystemGroup fixedStepSimulationSystemGroup;

    [Inject]
    PathfindingSystem _pathfindingSystem;

    [Inject]
    ECSSystemsManager ecssSystemsManager;

    public void Initialize()
    {
        fixedStepSimulationSystemGroup.Timestep = 1 / gameFieldSettings.tickPerSecond;

        _buildingMapSystem.Enabled = true;

        LoadGame();
    }

    public void SpeedUpTick()
    {
        fixedStepSimulationSystemGroup.Timestep /= 2;
    }

    public void SlowDownTick()
    {
        fixedStepSimulationSystemGroup.Timestep *= 2;
    }

    async void LoadGame()
    {
        await _loadingService.LoadWithProgressAsync(saveService.LoadGameState, LoadGameField);
    }

    async Task LoadGameField()
    {
        var save = saveService.GameState;
        await EntityLoader.LoadSavedEntitiesAsync(save);

        cameraController.SetUp(save.camData);
        cameraController.enabled = true;
        ecssSystemsManager.EnableGameplaySystems();
    }

    public void PlaceBuilding(
        BuildingData buildingData,
        BuildingPosData buildingPosData,
        bool isBluePrint
    )
    {
        EntityLoader.CreateBuilding(
            buildingData,
            buildingPosData,
            isBluePrint,
            saveService.GameState
        );
    }

    public void PlaceRoad(HashSet<Vector2Int> roadPoints, bool isBluePrint)
    {
        Debug.Log("Контроллер нажал");
        EntityLoader.CreateRoad(roadPoints, isBluePrint, saveService.GameState);
    }

    public List<Vector2Int> FilterExistingRoadPoints(List<Vector2Int> positions)
    {
        var result = new List<Vector2Int>();

        foreach (var position in positions)
        {
            if (!_buildingMapSystem.IsPositionOccupiedByRoad(new int2(position.x, position.y)))
            {
                result.Add(position);
            }
        }

        return result;
    }

    public void RemoveRoadPoints(int entityId, List<Vector2Int> pointsToRemove) { }

    public void RequestPath(
        Vector2Int start,
        Vector2Int end,
        System.Action<List<Vector2Int>> onPathFound
    )
    {
        _pathfindingSystem.FindBuildingPathAsync(
            new int2(start.x, start.y),
            new int2(end.x, end.y),
            (nativePath) =>
            {
                var points = new List<Vector2Int>();

                if (nativePath.IsCreated && nativePath.Length > 0)
                {
                    for (int i = 0; i < nativePath.Length; i++)
                    {
                        var point = nativePath[i];
                        points.Add(new Vector2Int(point.x, point.y));
                    }

                    nativePath.Dispose();
                }
                else
                {
                    points.Add(start);
                    points.Add(end);
                }

                onPathFound?.Invoke(points);
            }
        );
    }

    public bool CanBuildHereMany(List<Vector2Int> positions, bool isRoad)
    {
        var nativeCells = new NativeArray<int2>(positions.Count, Allocator.Temp);
        for (int i = 0; i < positions.Count; i++)
            nativeCells[i] = new int2(positions[i].x, positions[i].y);

        bool canBuild = _buildingMapSystem.CanBuildAt(nativeCells, isRoad);
        nativeCells.Dispose();
        return canBuild;
    }

    public int GetBuildingInThere(Vector2Int position)
    {
        return _buildingMapSystem.GetBuildingAt(new int2(position.x, position.y));
    }

    public int[] GetBuildingInThereMany(Vector2Int[] positions)
    {
        List<int> result = new List<int>();
        foreach (var pos in positions)
        {
            int buildingId = _buildingMapSystem.GetBuildingAt(new int2(pos.x, pos.y));
            if (buildingId != -1)
                result.Add(buildingId);
        }
        return result.ToArray();
    }
}
