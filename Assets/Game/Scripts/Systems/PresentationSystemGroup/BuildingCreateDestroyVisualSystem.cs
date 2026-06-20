using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

[DisableAutoCreation]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(PlayerVisualSystem))]
public partial class BuildingCreateDestroyVisualSystem : SystemBase
{
    [Inject] BuildingObjectFactory _factorty;
    [Inject] EnemyFactory _enemyFactory;
    [Inject] ConnectEnergyFactory _energyFactory;
    
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // 1. Force Destroy Logic
        foreach(var (energyData, reference, entity) in SystemAPI.Query<EnergyBuildingData, BuildingOnSceneReference>()
            .WithAll<ForceDestroyTag>()
            .WithEntityAccess())
        {
            var en = reference.buildingOnScene as EnergyBuildingOnScene;
            foreach(var c in energyData.connections)
            {
                _energyFactory.Disconnect(en.nodes[c.Item1]);
            }
        }

        // 2. Spawn Single Buildings
        foreach (var (buildingData, posData, entity) in SystemAPI.Query<BuildingData, BuildingPosData>().WithAll<CreateVisualTag>().WithEntityAccess())
        {
            SpawnBuilding(buildingData, posData, entity, ecb);
        }

        // 3. Spawn Multi-point Buildings (Roads/Walls in 3D)
        foreach (var (buildingData, points, entity) in SystemAPI.Query<BuildingData, DynamicBuffer<MapPoint>>().WithAll<CreateVisualTag>().WithEntityAccess())
        {
            SpawnManyPoint(buildingData, points, entity, ecb);
        }

        // 4. Enemy Spawning
        foreach(var (enemyData, entity) in SystemAPI.Query<CreateEnemyEventData>().WithEntityAccess())
        {
            _enemyFactory.CreateEnemy(enemyData.EnemyID, enemyData.pos); // enemyData.pos должен быть float3
            ecb.DestroyEntity(entity);
        }

        // 5. Update Existing Multi-point Visuals
        foreach (var (data, buildingRef, updateManyPoint, buff, entity) in SystemAPI.Query<BuildingData, BuildingOnSceneReference, EnabledRefRW<UpdateManyPoint>, DynamicBuffer<MapPoint>>().WithEntityAccess())
        {
            // В OnUpdate() вместо managedArray:
            UpdateManyPoint(buildingRef.buildingOnScene as ManyPointsBuildingInstanced, buff, data.BuildingIDHash);

            updateManyPoint.ValueRW = false;
        }

        // 6. Cleanup Visuals
        foreach (var (buildingOnSceneReference, entity) in SystemAPI.Query<BuildingOnSceneReference>().WithAll<ForceDestroyTag>().WithEntityAccess())
        {
            DeleteVisual(buildingOnSceneReference, entity, ecb);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    void SpawnBuilding(BuildingData buildingData, BuildingPosData posData, Entity building, EntityCommandBuffer ecb)
    {
        // Используем Vector3Int для позиции (x, y, z)
        var buildingOnScene = _factorty.CreateBuilding(buildingData.BuildingIDHash,
                                                    new Vector3Int(posData.LeftCornerPos.x, posData.LeftCornerPos.y, posData.LeftCornerPos.z),
                                                    EntityManager.HasComponent<TurretStats>(building) ? 0 : posData.Rotation);
        
        buildingOnScene.id = buildingData.BuildingUniqueID;

        if(EntityManager.HasComponent<TurretStats>(building))
        {
            var data = EntityManager.GetComponentData<TurretTranform>(building);
            float baseAngleRad = math.radians(posData.Rotation); // В 3D лучше передавать чистый угол или Quaternion
            data.baseRotation = baseAngleRad;
            data.rotation.y = baseAngleRad; 
            
            (buildingOnScene as TurretOnScene).TurretHead.transform.localRotation = Quaternion.Euler(0, posData.Rotation, 0);
            ecb.SetComponent(building, data);
        }

        if(buildingOnScene is EnergyBuildingOnScene energy) energy.SetUpNodes();
        
        ecb.SetComponent(building, new BuildingOnSceneReference { buildingOnScene = buildingOnScene });
        ecb.SetComponentEnabled<CreateVisualTag>(building, false);
    }

  void UpdateManyPoint(ManyPointsBuildingInstanced roadOnScene, DynamicBuffer<MapPoint> points, int buildingID)
    {
        // Вместо .ToArray() и .Select() работаем напрямую с DynamicBuffer через индексы
        int pointCount = points.Length;
        
        // Выделяем память на стеке кадра через Allocator.Temp
        var roadPoints = new NativeArray<int3>(pointCount, Allocator.Temp);
        for (int i = 0; i < pointCount; i++)
        {
            roadPoints[i] = new int3(points[i].pos.x, points[i].pos.y, points[i].pos.z);
        }

        // Вместо постоянного создания 'new Dictionary' используем структуру побольше или кешируем её во внешнее поле класса
        // Для примера используем нативный аналог:
        var neighborsMapManaged = new Dictionary<Vector3Int, bool>(); 
        var mapData = SystemAPI.GetSingleton<BuildingMap>();
        
        // Направления в 3D
        var dirs = new NativeArray<int3>(6, Allocator.Temp);
        dirs[0] = new int3(1, 0, 0); dirs[1] = new int3(-1, 0, 0);
        dirs[2] = new int3(0, 1, 0); dirs[3] = new int3(0, -1, 0);
        dirs[4] = new int3(0, 0, 1); dirs[5] = new int3(0, 0, -1);

        for (int i = 0; i < pointCount; i++)
        {
            int3 p = roadPoints[i];
            for (int d = 0; d < 6; d++)
            {
                int3 pos = p + dirs[d];
                
                // Замена .Any() на быстрый цикл
                bool isPointInRoad = false;
                for (int j = 0; j < pointCount; j++)
                {
                    if (roadPoints[j].Equals(pos)) { isPointInRoad = true; break; }
                }

                if (!isPointInRoad)
                {
                    if (mapData.CellMapBuildingsIDs.ContainsKey(pos))
                    {
                        neighborsMapManaged.TryAdd(new Vector3Int(pos.x, pos.y, pos.z), mapData.CellMapBuildingsIDs[pos] == buildingID);
                    }
                }
            }
        }
        
        // Конвертация для генератора меша без LINQ
        Vector3Int[] targetPoints = new Vector3Int[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            targetPoints[i] = new Vector3Int(roadPoints[i].x, roadPoints[i].y, roadPoints[i].z);
        }

        roadOnScene.Generate(targetPoints, neighborsMapManaged);
        
        // Обязательно освобождаем нативную временную память
        dirs.Dispose();
        roadPoints.Dispose();
    }


    void SpawnManyPoint(BuildingData buildingData, DynamicBuffer<MapPoint> points, Entity building, EntityCommandBuffer ecb)
    {
      
        // Инициализируем через Vector3Int
        var buildingOnScene = _factorty.CreateManyPoint(buildingData.BuildingIDHash, 
            new Vector3Int[] { new Vector3Int(points[0].pos.x, points[0].pos.y, points[0].pos.z) }, null);

        UpdateManyPoint(buildingOnScene, points, buildingData.BuildingIDHash);
        buildingOnScene.id = buildingData.BuildingUniqueID; 
        
        ecb.SetComponent(building, new BuildingOnSceneReference { buildingOnScene = buildingOnScene });
        ecb.SetComponentEnabled<CreateVisualTag>(building, false);
    }

    void DeleteVisual(BuildingOnSceneReference reference, Entity building, EntityCommandBuffer ecb)
    {
            if (reference.buildingOnScene != null)
            _factorty.DestoryObject(reference.buildingOnScene);
            
        ecb.SetComponent(building, new BuildingOnSceneReference { buildingOnScene = null });
    }
}
