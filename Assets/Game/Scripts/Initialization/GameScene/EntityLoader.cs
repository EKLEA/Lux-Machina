using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

public class EntityLoader
{
    [Inject] EntityManager _entityManager;

    public async Task LoadSavedEntitiesAsync(GameStateData gameState)
    {
        await CreateSavedBuildingsAsync(gameState);
        await CreateSavedRoadsAsync(gameState);
    }


    async Task CreateSavedBuildingsAsync(GameStateData gameState)
    {
        var entityIds = gameState.buildingPosDatas.Keys.ToArray();
        const int buildingsPerFrame = 5;

        for (int i = 0; i < entityIds.Length; i += buildingsPerFrame)
        {
            var chunk = entityIds.Skip(i).Take(buildingsPerFrame);
            foreach (var id in chunk)
                CreateSavedBuildingEntity(id, gameState);

            await Task.Yield();

        }
    }
    async Task CreateSavedRoadsAsync(GameStateData gameState)
    {
        if (gameState.roadPoints.Count != 0)
            CreateRoadsQuery(gameState.roadPoints, false);
        if (gameState.phantomPoints.Count != 0)
            CreateRoadsQuery(gameState.phantomPoints, true);
        await Task.Yield();
    }
    void CreateRoadsQuery(HashSet<int2> roadPoints, bool isBluePrint)
    {
        Debug.Log("Запрос нажал");
        Entity entity = _entityManager.CreateEntity();
        _entityManager.AddComponentData(entity, new ProcessBuildingEventComponent());
        _entityManager.AddComponentData(entity, new AddToMapTag());
        var buffer = _entityManager.AddBuffer<MapPointData>(entity);
        foreach (var point in roadPoints)
            buffer.Add(new MapPointData { Value = point });
        _entityManager.AddComponentData(entity, new CreateRoadSegmentsTag());
        if (isBluePrint)
        {
            _entityManager.AddComponentData(entity, new BluePrint());
            Debug.Log("Добавлено в ентити");
        }
    }
    void CreateSavedBuildingEntity(int entityId, GameStateData gameState)
    {
        if (gameState.buildingDatas.ContainsKey(entityId))
        {
            Entity entity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(entity, new ProcessBuildingEventComponent());
            _entityManager.AddComponentData(entity, new AddToMapTag());
            _entityManager.AddComponentData(entity, gameState.buildingDatas[entityId]);
            _entityManager.AddComponentData(entity, gameState.buildingPosDatas[entityId]);
            var inf = gameState.buildingPosDatas[entityId];
            var size = inf.Rotation % 2 != 0 ? new int2(inf.Size.y, inf.Size.x) : inf.Size;
            var buffer = _entityManager.AddBuffer<MapPointData>(entity);
            for (int x = 0; x < size.x; x++)
                for (int y = 0; y < size.y; y++)
                    buffer.Add(new MapPointData { Value = inf.LeftCornerPos + new int2(x, y) });
            _entityManager.AddComponentData(entity, new CreateVisualTag());
            if (gameState.phantomBuildings.Contains(entityId)) _entityManager.AddComponentData(entity, new BluePrint());
        }
    }
    public void CreateBuilding(BuildingData buildingData, BuildingPosData buildingPosData, bool isBluePrint, GameStateData gameState)
    {
        if (!gameState.buildingDatas.ContainsKey(buildingData.UniqueIDHash))
        {
            gameState.buildingDatas.Add(buildingData.UniqueIDHash, buildingData);
            gameState.buildingPosDatas.Add(buildingData.UniqueIDHash, buildingPosData);
           if(isBluePrint)gameState.phantomBuildings.Add(buildingData.UniqueIDHash);
            CreateSavedBuildingEntity(buildingData.UniqueIDHash, gameState);
        }
    }
    public void CreateRoad(HashSet<Vector2Int> roadPoints, bool isBluePrint, GameStateData gameState)
    {
         Debug.Log("Подготовка нажал");
        var newPoints = roadPoints.Select(f => new int2(f.x, f.y)).ToHashSet();
        HashSet<int2> selectedRoads;
        if (isBluePrint)
            selectedRoads = gameState.phantomPoints;
        else selectedRoads = gameState.roadPoints;
        foreach (var p in newPoints)
            selectedRoads.Add(p);
        CreateRoadsQuery(newPoints, isBluePrint);
    }
    
}