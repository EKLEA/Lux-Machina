using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.VisualScripting;
using UnityEngine;
using Zenject;

public class EntityLoader
{
    [Inject] EntityManager _entityManager;
    [Inject] BuildingObjectFactorty _buildingObjectFactory;

    public async Task LoadSavedEntitiesAsync(GameStateData gameState)
    {
        await CreateSavedBuildingsAsync(gameState);
    }
    async Task CreateSavedBuildingsAsync(GameStateData gameState)
    {
        var entityIds = gameState.buildingPosDatas.Keys.ToArray();
        
        const int buildingsPerFrame = 5;
        
        for (int i = 0; i < entityIds.Length; i += buildingsPerFrame)
        {
            var chunkEntityIds = entityIds.Skip(i).Take(buildingsPerFrame).ToArray();
            
            foreach (var entityId in chunkEntityIds)
            {
                CreateSavedBuildingEntity(entityId, gameState);
            }
            
            await Task.Yield();
            
            Debug.Log($"Загружено {Mathf.Min(i + buildingsPerFrame, entityIds.Length)} / {entityIds.Length} построек");
        }
    }

    public void CreateSavedBuildingEntity(int entityId, GameStateData gameState)
    {
        Entity entity = _entityManager.CreateEntity();
        AddSavedVisualComponent(entity, entityId, gameState);
        AddSavedHealthComponent(entity, entityId, gameState);
        AddSavedLogicComponent(entity, entityId, gameState);
    }

    void AddSavedVisualComponent(Entity entity, int entityId, GameStateData gameState)
    {
        if (gameState.posDatas.TryGetValue(entityId, out var posData))
        {
            _entityManager.AddComponentData(entity, posData);
            GameObject gmOnScene;

            if (gameState.buildingPosDatas.TryGetValue(entityId, out var bPosData))
            {
                _entityManager.AddComponentData(entity, bPosData);
                gmOnScene = _buildingObjectFactory.CreateBuilding(posData, bPosData);
                _entityManager.AddComponent<BuildingTag>(entity);
            }
            else
            {
                var rPosData = gameState.roadPosDatas[entityId];
                _entityManager.AddComponentData(entity, rPosData);
                gmOnScene = _buildingObjectFactory.CreateRoad(posData, rPosData);
                _entityManager.AddComponent<RoadTag>(entity);
            }
            if (gameState.phantomBuildings.Contains(entityId))
                _entityManager.AddComponent<MakePhantomTag>(entity);
                
            _entityManager.AddComponentData(entity, new GameObjectReference
            {
                BuildingOnScene = gmOnScene
            });
        }
    }

    void AddSavedHealthComponent(Entity entity, int entityId, GameStateData gameState)
    {
        if (gameState.healthDatas.TryGetValue(entityId, out var healthData))
            _entityManager.AddComponentData(entity, healthData);
    }

    void AddSavedLogicComponent(Entity entity, int entityId, GameStateData gameState)
    {
        if (gameState.buildingLogicDatas.TryGetValue(entityId, out var logicData))
        {
            _entityManager.AddComponentData(entity, logicData);
            
            bool hasConsumer = gameState.consumerBuildingDatas.TryGetValue(entityId, out var clogicData);
            bool hasProducer = gameState.producerBuildingDatas.TryGetValue(entityId, out var plogicData);

            if (hasConsumer)
            {
                _entityManager.AddComponentData(entity, clogicData);
                var buffer = _entityManager.AddBuffer<StorageSlotData>(entity);
                foreach (var slot in gameState.inputSlotDatas[entityId].Slots)
                {
                    buffer.Add(slot);
                }
                _entityManager.AddComponent<ConsumerBuildingTag>(entity);
            }

            if (hasProducer)
            {
                _entityManager.AddComponentData(entity, plogicData);
                var buffer = _entityManager.AddBuffer<StorageSlotData>(entity);
                foreach (var slot in gameState.outPutSlotDatas[entityId].Slots)
                {
                    buffer.Add(slot);
                }
                _entityManager.AddComponent<ProducerBuildingTag>(entity);
            }

            if (hasConsumer && hasProducer)
                _entityManager.AddComponent<ProcessorBuildingTag>(entity);
        }
    }
}