
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines.ExtrusionShapes;
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]

[UpdateAfter(typeof(TerrainSystem))]
[BurstCompile]

public partial struct DestroyBuildingsSystem : ISystem
{
    EntityQuery _destroyBuildingQuery;
    EntityQuery _checkForDestroyBuildingQuery;
    EntityQuery _destroyManyPointQuery;
    public void OnCreate(ref SystemState state)
    {
        _destroyBuildingQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ForceDestroyTag,BuildingPosData>()
            .WithNone<ManyPointTypeBuildingTag>()
            .Build(ref state);
         _destroyManyPointQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ForceDestroyTag,ManyPointTypeBuildingTag,MapPoint>()
            .Build(ref state);
        _checkForDestroyBuildingQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<CheckForDestroy>()
            .WithNone<ForceDestroyTag>()
            .Build(ref state);
        
    }
    public void OnUpdate(ref SystemState state)
{
    var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
    var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
    
    var entitiesRW = SystemAPI.GetSingletonRW<EntitiesDictionary>();
    var buildingMapRW = SystemAPI.GetSingletonRW<BuildingMap>();
    var turretMapRW = SystemAPI.GetSingletonRW<TurretGrid>();
    Entity mapEntity = SystemAPI.GetSingletonEntity<BuildingMap>();
    
    // --- ПОЛУЧАЕМ НОВЫЕ СИНГЛТОНЫ ДЛЯ ПЕРЕРАСЧЁТА ВЕСОВ И БЛОКОВ ---
    var chunkMap = SystemAPI.GetSingleton<ChunkMap>();
    var worldSettings = SystemAPI.GetSingleton<WorldSettings>(); // или откуда у тебя берется worldSettings

    if (!_checkForDestroyBuildingQuery.IsEmpty)
    {
        state.Dependency = new CheckForDestoryJob { ECB = ecb }.Schedule(state.Dependency);
    }
    
    if (!_destroyBuildingQuery.IsEmpty)
    {
        // Кэшируем буфер блоков для проверки IsBlocked
        var blockLookup = SystemAPI.GetBufferLookup<BlockElement>(true);

        var deleteBJoB = new DestroyBuildingJob
        {
            MapData = buildingMapRW.ValueRW,
            Map = mapEntity,
            turretGrid = turretMapRW.ValueRW,
            EntityDictionary = entitiesRW.ValueRW,
            ECB = ecb,
            ManyPointLookup = SystemAPI.GetComponentLookup<ManyPointTypeBuildingTag>(true),
            CoreBuildingTagLookup = SystemAPI.GetComponentLookup<CoreBuildingTag>(true),
            TurretStatsLookup = SystemAPI.GetComponentLookup<TurretStats>(true),

            // Передача новых полей для каскадного удаления и затекания
            ChunkMap = chunkMap,
            BlockLookup = blockLookup,
            worldSettings = worldSettings
        };
        state.Dependency = deleteBJoB.Schedule(state.Dependency);
    }
    
    if (!_destroyManyPointQuery.IsEmpty)
    {
        // Пересоздаем Lookup для предотвращения конфликтов доступа (Aliasing)
        var manyPointLookup = SystemAPI.GetComponentLookup<ManyPointTypeBuildingTag>(true);
        var blockLookup = SystemAPI.GetBufferLookup<BlockElement>(true);

        var deleteRJoB = new DestroyManyPointJob
        {
            MapData = buildingMapRW.ValueRW,
            Map = mapEntity,
            EntityDictionary = entitiesRW.ValueRW,
            ECB = ecb,
            ManyPointLookup = manyPointLookup,

            // Передача новых полей для каскадного удаления и затекания
            ChunkMap = chunkMap,
            BlockLookup = blockLookup,
            worldSettings = worldSettings
        };
        state.Dependency = deleteRJoB.Schedule(state.Dependency);
    }
}
    [BurstCompile]
    [WithAll(typeof( CheckForDestroy))]
    [WithNone(typeof(ForceDestroyTag))]
    public partial struct CheckForDestoryJob : IJobEntity
    {
        public EntityCommandBuffer ECB;
        public void Execute(Entity entity,in BuildingTag tag)
        {
            ECB.SetComponentEnabled<CheckForDestroy>(entity,false);
            ECB.SetComponentEnabled<ForceDestroyTag>(entity,true);
        }
    }
    
    [BurstCompile]
    [WithAll(typeof(ForceDestroyTag))]
    [WithNone(typeof(ManyPointTypeBuildingTag), typeof(CheckForDestroy))]
    public partial struct DestroyBuildingJob : IJobEntity
    {
        public BuildingMap MapData; 
        public TurretGrid turretGrid;
        public EntitiesDictionary EntityDictionary; 
        public Entity Map;
        public EntityCommandBuffer ECB;
        [ReadOnly] public ComponentLookup<ManyPointTypeBuildingTag> ManyPointLookup;
        [ReadOnly] public ComponentLookup<CoreBuildingTag> CoreBuildingTagLookup;
        [ReadOnly] public ComponentLookup<TurretStats> TurretStatsLookup;

        [ReadOnly] public ChunkMap ChunkMap;
        [ReadOnly] public BufferLookup<BlockElement> BlockLookup;
        public WorldSettings worldSettings;

        private const int MaxSearchDist = 20;

        struct DirectionDataWeights
        {
            public int3 Offset;
            public float StepCost;
            public DirectionDataWeights(int3 offset) { Offset = offset; StepCost = math.length(new float3(offset)); }
        }

        struct DirectionDataFlow
        {
            public int3 Offset;
            public float3 Normalized;
            public DirectionDataFlow(int3 offset) { Offset = offset; Normalized = math.normalize((float3)offset); }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static DirectionDataWeights GetWeightDirection(int index)
        {
            switch (index)
            {
                case 0: return new DirectionDataWeights(new int3(1, 0, 0));
                case 1: return new DirectionDataWeights(new int3(-1, 0, 0));
                case 2: return new DirectionDataWeights(new int3(0, 1, 0));
                case 3: return new DirectionDataWeights(new int3(0, -1, 0));
                case 4: return new DirectionDataWeights(new int3(0, 0, 1));
                case 5: return new DirectionDataWeights(new int3(0, 0, -1));
                case 6: return new DirectionDataWeights(new int3(1, 1, 0));
                case 7: return new DirectionDataWeights(new int3(-1, 1, 0));
                case 8: return new DirectionDataWeights(new int3(1, 0, 1));
                case 9: return new DirectionDataWeights(new int3(-1, 0, 1));
                case 10: return new DirectionDataWeights(new int3(0, 1, 1));
                case 11: return new DirectionDataWeights(new int3(0, -1, 1));
                case 12: return new DirectionDataWeights(new int3(1, -1, 0));
                case 13: return new DirectionDataWeights(new int3(-1, -1, 0));
                case 14: return new DirectionDataWeights(new int3(1, 0, -1));
                case 15: return new DirectionDataWeights(new int3(-1, 0, -1));
                case 16: return new DirectionDataWeights(new int3(0, 1, -1));
                case 17: return new DirectionDataWeights(new int3(0, -1, -1));
                default: return new DirectionDataWeights(new int3(0, 0, 0));
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static DirectionDataFlow GetFlowDirection(int index)
        {
            switch (index)
            {
                case 0: return new DirectionDataFlow(new int3(1, 0, 0));
                case 1: return new DirectionDataFlow(new int3(-1, 0, 0));
                case 2: return new DirectionDataFlow(new int3(0, 1, 0));
                case 3: return new DirectionDataFlow(new int3(0, -1, 0));
                case 4: return new DirectionDataFlow(new int3(0, 0, 1));
                case 5: return new DirectionDataFlow(new int3(0, 0, -1));
                case 6: return new DirectionDataFlow(new int3(1, 1, 0));
                case 7: return new DirectionDataFlow(new int3(-1, 1, 0));
                case 8: return new DirectionDataFlow(new int3(1, -1, 0));
                case 9: return new DirectionDataFlow(new int3(-1, -1, 0));
                case 10: return new DirectionDataFlow(new int3(1, 0, 1));
                case 11: return new DirectionDataFlow(new int3(0, 1, 1));
                case 12: return new DirectionDataFlow(new int3(1, 0, -1));
                case 13: return new DirectionDataFlow(new int3(0, -1, 1));
                case 14: return new DirectionDataFlow(new int3(-1, 0, 1));
                case 15: return new DirectionDataFlow(new int3(0, 1, -1));
                case 16: return new DirectionDataFlow(new int3(-1, 0, -1));
                case 17: return new DirectionDataFlow(new int3(0, -1, -1));
                case 18: return new DirectionDataFlow(new int3(1, 1, 1));
                case 19: return new DirectionDataFlow(new int3(-1, 1, 1));
                case 20: return new DirectionDataFlow(new int3(1, -1, 1));
                case 21: return new DirectionDataFlow(new int3(-1, -1, 1));
                case 22: return new DirectionDataFlow(new int3(1, 1, -1));
                case 23: return new DirectionDataFlow(new int3(-1, 1, -1));
                case 24: return new DirectionDataFlow(new int3(1, -1, -1));
                case 25: return new DirectionDataFlow(new int3(-1, -1, -1));
                default: return new DirectionDataFlow(new int3(0, 0, 0));
            }
        }

        bool IsBlocked(int3 worldPos, ref int2 lastChunkPos, ref DynamicBuffer<BlockElement> lastBuffer, ref bool hasLastBuffer)
        {
            if (worldPos.y != math.clamp(worldPos.y, 0, worldSettings.Height - 1)) return true;
            int2 chunkPos = new int2(
                worldPos.x >= 0 ? worldPos.x / worldSettings.Size : (worldPos.x - worldSettings.Size + 1) / worldSettings.Size,
                worldPos.z >= 0 ? worldPos.z / worldSettings.Size : (worldPos.z - worldSettings.Size + 1) / worldSettings.Size
            );
            if (!hasLastBuffer || !chunkPos.Equals(lastChunkPos))
            {
                if (!ChunkMap.ChunkMapData.TryGetValue(chunkPos, out var chunkEntity) || !BlockLookup.HasBuffer(chunkEntity))
                {
                    hasLastBuffer = false; return true;
                }
                lastBuffer = BlockLookup[chunkEntity]; lastChunkPos = chunkPos; hasLastBuffer = true;
            }
            int3 local = new int3(worldPos.x - chunkPos.x * worldSettings.Size, worldPos.y, worldPos.z - chunkPos.y * worldSettings.Size);
            if (local.x != math.clamp(local.x, 0, worldSettings.Size - 1) || local.z != math.clamp(local.z, 0, worldSettings.Size - 1)) return true;
            int index = local.x + worldSettings.Size * (local.y + worldSettings.Height * local.z);
            if (index != math.clamp(index, 0, lastBuffer.Length - 1)) return true;
            return lastBuffer[index].BlockID != 0;
        }

        public void Execute(Entity entity, in BuildingData buildingData, in BuildingPosData posData)
        {
            if (MapData.CellEntityMultiMap.ContainsKey(entity))
            {
               var deleteQueue = new NativeQueue<int3>(Allocator.Temp);
                var cellsToRemove = new NativeList<int3>(128, Allocator.Temp);

                var fillQueue = new NativeQueue<int3>(Allocator.Temp);
                var affectedCells = new NativeList<int3>(256, Allocator.Temp);

                var deleteVisited = new NativeHashSet<int3>(1024, Allocator.Temp);
                var fillVisited = new NativeHashSet<int3>(1024, Allocator.Temp);
                var affectedSet = new NativeHashSet<int3>(1024, Allocator.Temp);

                int2 lastChunkPos = new int2(int.MinValue, int.MinValue);
                DynamicBuffer<BlockElement> lastBuffer = default;
                bool hasLastBuffer = false;

                // Шаг 1: Инициализируем удаление со стартовых позиций уничтожаемого здания
                for (int x = 0; x != posData.size.x; x++)
                {
                    for (int y = 0; y != posData.size.y; y++)
                    {
                        for (int z = 0; z != posData.size.z; z++)
                        {
                            var pos = posData.LeftCornerPos + new int3(x, y, z);
                            if (MapData.CellWeights.ContainsKey(pos))
                            {
                                deleteQueue.Enqueue(pos);
                            }
                            MapData.CellMapEntites.Remove(pos);
                            MapData.CellMapBuildingsIDs.Remove(pos);
                            MapData.IsBluePrintOrDemolitionPoints.Remove(pos);
                        }
                    }
                }

                // Шаг 2: Каскадный поиск зависимых весов + выявление чужих «границ»
              while (deleteQueue.TryDequeue(out int3 curr))
                {
                    if (!deleteVisited.Add(curr))
                        continue;

                    if (!MapData.CellWeights.TryGetValue(curr, out float currWeight))
                        continue;

                    cellsToRemove.Add(curr);

                    for (int i = 0; i < 18; i++)
                    {
                        DirectionDataWeights dir = GetWeightDirection(i);
                         if (dir.Offset.y != 0) continue; 
                        int3 neighbor = curr + dir.Offset;

                        if (IsBlocked(neighbor, ref lastChunkPos, ref lastBuffer, ref hasLastBuffer))
                            continue;

                        if (!MapData.CellWeights.TryGetValue(neighbor, out float neighborWeight))
                            continue;

                        float expectedWeight = currWeight + dir.StepCost;
                        if (math.abs(neighborWeight - expectedWeight) < 0.001f)
                        {
                            deleteQueue.Enqueue(neighbor);
                        }
                        else
                        {
                            // Защита: не добавляем в очередь затекания то, что уже там или обработано
                            if (fillVisited.Add(neighbor))
                            {
                                fillQueue.Enqueue(neighbor);
                            }
                        }
                    }
                }

                // Шаг 3: Физически стираем старые веса
               for (int i = 0; i != cellsToRemove.Length; i++)
                {
                    int3 targetCell = cellsToRemove[i];
                    MapData.CellWeights.Remove(targetCell);
                    MapData.CellDirections.Remove(targetCell);
                }

                // Шаг 4: Волна «затекания» соседних весов в образовавшуюся пустоту
                while (fillQueue.TryDequeue(out int3 curr))
                {
                    if (!MapData.CellWeights.TryGetValue(curr, out float currWeight))
                        continue;

                    if (affectedSet.Add(curr))
                        affectedCells.Add(curr);

                    for (int i = 0; i < 18; i++)
                    {
                        DirectionDataWeights dir = GetWeightDirection(i);
                         if (dir.Offset.y != 0) continue; 
                        int3 neighbor = curr + dir.Offset;

                        if (IsBlocked(neighbor, ref lastChunkPos, ref lastBuffer, ref hasLastBuffer))
                            continue;

                        if (MapData.CellMapEntites.TryGetValue(neighbor, out _))
                            continue;

                        float newWeight = currWeight + dir.StepCost;
                        if (newWeight > MaxSearchDist + 30f)
                            continue;

                        if (!MapData.CellWeights.TryGetValue(neighbor, out float old))
                        {
                            if (fillVisited.Add(neighbor))
                            {
                                MapData.CellWeights.Add(neighbor, newWeight);
                                fillQueue.Enqueue(neighbor);
                            }
                        }
                        else if (newWeight < old - 0.001f) // Защита от микро-колебаний
                        {
                            MapData.CellWeights[neighbor] = newWeight;
                            if (fillVisited.Add(neighbor))
                            {
                                fillQueue.Enqueue(neighbor);
                            }
                        }
                        else 
                        {
                            // КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ:
                            // Если новый вес НЕ меньше старого, значит, эту клетку удерживает ДРУГОЕ здание.
                            // Сама клетка не изменится, но её стрелка (Flow Field) обязана пересчитаться, 
                            // так как с одной из её сторон только что исчезли/изменились блоки!
                            if (affectedSet.Add(neighbor))
                            {
                                affectedCells.Add(neighbor);
                            }
                        }
                    }
                }

                // Шаг 5: Пересчет векторов направлений (Flow Field) для всей обновленной зоны соседей
                for (int i = 0; i != affectedCells.Length; i++)
                {
                    int3 curr = affectedCells[i];
                    float currWeight = MapData.CellWeights[curr];
                    float3 flowDir = float3.zero;
                    bool foundLowerWeight = false;

                    for (int d = 0; d != 26; d++)
                    {
                        DirectionDataFlow dirData = GetFlowDirection(d);
                        
                        // ЖЕСТКАЯ ФИЛЬТРАЦИЯ 2D: Игнорируем соседей сверху и снизу
                        if (dirData.Offset.y != 0)
                            continue;

                        int3 n = curr + dirData.Offset;
                        if (IsBlocked(n, ref lastChunkPos, ref lastBuffer, ref hasLastBuffer))
                            continue;

                        if (MapData.CellWeights.TryGetValue(n, out float w))
                        {
                            if (w < currWeight)
                            {
                                float diff = currWeight - w;
                                
                                // Пересчитываем чистый 2D-вектор направления, чтобы длина была честной
                                float2 flatDir2D = math.normalize(new float2(dirData.Offset.x, dirData.Offset.z));
                                float3 flatDir3D = new float3(flatDir2D.x, 0f, flatDir2D.y);
                                
                                flowDir += flatDir3D * diff;
                                foundLowerWeight = true;
                            }
                        }
                    }

                    // Нормализуем итоговую строго горизонтальную стрелку
                    if (foundLowerWeight && math.lengthsq(flowDir) > 0.001f)
                    {
                        MapData.CellDirections[curr] = math.normalize(flowDir);
                    }
                    else
                    {
                        MapData.CellDirections[curr] = float3.zero;
                    }
                }

                // Чистим временные контейнеры
                deleteQueue.Dispose();
                cellsToRemove.Dispose();
                fillQueue.Dispose();
                affectedCells.Dispose();

                // --- Ваша стандартная логика деструкции ---
                if (TurretStatsLookup.HasComponent(entity))
                {
                    turretGrid.EnemyGridMap.Remove(buildingData.BuildingUniqueID);
                    var enemyData = turretGrid.EnemyToTurret.GetKeyValueArrays(Allocator.Temp);
                    for (int i = 0; i != enemyData.Values.Length; i++)
                    {
                        if (enemyData.Values[i] == buildingData.BuildingUniqueID)
                            turretGrid.EnemyToTurret.Remove(enemyData.Keys[i], buildingData.BuildingUniqueID);
                    }
                    enemyData.Dispose();

                    var cellData = turretGrid.TurretGridClaim.GetKeyValueArrays(Allocator.Temp);
                    for (int i = 0; i != cellData.Values.Length; i++)
                    {
                        if (cellData.Values[i] == buildingData.BuildingUniqueID)
                            turretGrid.TurretGridClaim.Remove(cellData.Keys[i], buildingData.BuildingUniqueID);
                    }
                    cellData.Dispose();
                }

                MapData.CellEntityMultiMap.Remove(entity);
                EntityDictionary.Entities.Remove(buildingData.BuildingUniqueID);
                if (CoreBuildingTagLookup.HasComponent(entity))
                {
                    ECB.SetComponentEnabled<IsPause>(Map, true);
                    ECB.SetComponentEnabled<IsGameOver>(Map, true);
                    ECB.SetComponentEnabled<SavingMapTag>(Map, true);
                }
                ECB.DestroyEntity(entity);
                ECB.SetComponentEnabled<UpdateClusterSlots>(Map, true);

                NativeHashSet<Entity> roadsToUpdate = new(100, Allocator.Temp);
                for (int x = posData.LeftCornerPos.x; x != posData.LeftCornerPos.x + posData.size.x; x++)
                {
                    CheckPoint(new int3(x, posData.LeftCornerPos.y, posData.LeftCornerPos.z - 1), ref roadsToUpdate);
                    CheckPoint(new int3(x, posData.LeftCornerPos.y, posData.LeftCornerPos.z + posData.size.y), ref roadsToUpdate);
                }
                for (int z = posData.LeftCornerPos.z; z != posData.LeftCornerPos.z + posData.size.z; z++)
                {
                    CheckPoint(new int3(posData.LeftCornerPos.x - 1, posData.LeftCornerPos.y, z), ref roadsToUpdate);
                    CheckPoint(new int3(posData.LeftCornerPos.x + posData.size.x, posData.LeftCornerPos.y, z), ref roadsToUpdate);
                }
                foreach (var road in roadsToUpdate)
                {
                    ECB.SetComponentEnabled<UpdateManyPoint>(road, true);
                }
                roadsToUpdate.Dispose();
            }
            
        }

        void CheckPoint(int3 pos, ref NativeHashSet<Entity> roads)
        {
            if (MapData.CellMapEntites.ContainsKey(pos))
            {
                if (ManyPointLookup.HasComponent(MapData.CellMapEntites[pos])) roads.Add(MapData.CellMapEntites[pos]);
            }
        }
    }

    [BurstCompile]
    [WithAll(typeof(ForceDestroyTag), typeof(ManyPointTypeBuildingTag))]
    public partial struct DestroyManyPointJob : IJobEntity
    {
        public BuildingMap MapData; 
        public EntitiesDictionary EntityDictionary; 
        public Entity Map;
        public EntityCommandBuffer ECB;
        Entity roadEn;

        [ReadOnly] public ComponentLookup<ManyPointTypeBuildingTag> ManyPointLookup;

        [ReadOnly] public ChunkMap ChunkMap;
        [ReadOnly] public BufferLookup<BlockElement> BlockLookup;
        public WorldSettings worldSettings;

        private const int MaxSearchDist = 20;

        struct DirectionDataWeights
        {
            public int3 Offset;
            public float StepCost;
            public DirectionDataWeights(int3 offset)
            {
                Offset = offset;
                StepCost = math.length(new float3(offset));
            }
        }

        struct DirectionDataFlow
        {
            public int3 Offset;
            public float3 Normalized;
            public DirectionDataFlow(int3 offset)
            {
                Offset = offset;
                Normalized = math.normalize((float3)offset);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static DirectionDataWeights GetWeightDirection(int index)
        {
            switch (index)
            {
                case 0: return new(new int3(1, 0, 0));
                case 1: return new(new int3(-1, 0, 0));
                case 2: return new(new int3(0, 1, 0));
                case 3: return new(new int3(0, -1, 0));
                case 4: return new(new int3(0, 0, 1));
                case 5: return new(new int3(0, 0, -1));
                case 6: return new(new int3(1, 1, 0));
                case 7: return new(new int3(-1, 1, 0));
                case 8: return new(new int3(1, 0, 1));
                case 9: return new(new int3(-1, 0, 1));
                case 10: return new(new int3(0, 1, 1));
                case 11: return new(new int3(0, -1, 1));
                case 12: return new(new int3(1, -1, 0));
                case 13: return new(new int3(-1, -1, 0));
                case 14: return new(new int3(1, 0, -1));
                case 15: return new(new int3(-1, 0, -1));
                case 16: return new(new int3(0, 1, -1));
                case 17: return new(new int3(0, -1, -1));
                default: return new(new int3(0, 0, 0));
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static DirectionDataFlow GetFlowDirection(int index)
        {
            switch (index)
            {
                case 0: return new(new int3(1, 0, 0));
                case 1: return new(new int3(-1, 0, 0));
                case 2: return new(new int3(0, 1, 0));
                case 3: return new(new int3(0, -1, 0));
                case 4: return new(new int3(0, 0, 1));
                case 5: return new(new int3(0, 0, -1));
                case 6: return new(new int3(1, 1, 0));
                case 7: return new(new int3(-1, 1, 0));
                case 8: return new(new int3(1, -1, 0));
                case 9: return new(new int3(-1, -1, 0));
                case 10: return new(new int3(1, 0, 1));
                case 11: return new(new int3(0, 1, 1));
                case 12: return new(new int3(1, 0, -1));
                case 13: return new(new int3(0, -1, 1));
                case 14: return new(new int3(-1, 0, 1));
                case 15: return new(new int3(0, 1, -1));
                case 16: return new(new int3(-1, 0, -1));
                case 17: return new(new int3(0, -1, -1));
                case 18: return new(new int3(1, 1, 1));
                case 19: return new(new int3(-1, 1, 1));
                case 20: return new(new int3(1, -1, 1));
                case 21: return new(new int3(-1, -1, 1));
                case 22: return new(new int3(1, 1, -1));
                case 23: return new(new int3(-1, 1, -1));
                case 24: return new(new int3(1, -1, -1));
                case 25: return new(new int3(-1, -1, -1));
                default: return new(new int3(0, 0, 0));
            }
        }

        bool IsBlocked(int3 worldPos, ref int2 lastChunkPos, ref DynamicBuffer<BlockElement> lastBuffer, ref bool hasLastBuffer)
        {
            if (worldPos.y != math.clamp(worldPos.y, 0, worldSettings.Height - 1)) return true;

            int2 chunkPos = new int2(
                worldPos.x >= 0 ? worldPos.x / worldSettings.Size : (worldPos.x - worldSettings.Size + 1) / worldSettings.Size,
                worldPos.z >= 0 ? worldPos.z / worldSettings.Size : (worldPos.z - worldSettings.Size + 1) / worldSettings.Size
            );

            if (!hasLastBuffer || !chunkPos.Equals(lastChunkPos))
            {
                if (!ChunkMap.ChunkMapData.TryGetValue(chunkPos, out var chunkEntity) || !BlockLookup.HasBuffer(chunkEntity))
                {
                    hasLastBuffer = false;
                    return true;
                }

                lastBuffer = BlockLookup[chunkEntity];
                lastChunkPos = chunkPos;
                hasLastBuffer = true;
            }

            int3 local = new int3(
                worldPos.x - chunkPos.x * worldSettings.Size,
                worldPos.y,
                worldPos.z - chunkPos.y * worldSettings.Size
            );

            if (local.x != math.clamp(local.x, 0, worldSettings.Size - 1) ||
                local.z != math.clamp(local.z, 0, worldSettings.Size - 1))
                return true;

            int index = local.x + worldSettings.Size * (local.y + worldSettings.Height * local.z);

            if (index != math.clamp(index, 0, lastBuffer.Length - 1))
                return true;

            return lastBuffer[index].BlockID != 0;
        }

        public void Execute(Entity entity, in BuildingData buildingData, in DynamicBuffer<MapPoint> points)
        {
            if (!MapData.CellEntityMultiMap.ContainsKey(entity))
                return;

            roadEn = entity;

            var deleteQueue = new NativeQueue<int3>(Allocator.Temp);
            var cellsToRemove = new NativeList<int3>(128, Allocator.Temp);
            var fillQueue = new NativeQueue<int3>(Allocator.Temp);
            var affectedCells = new NativeList<int3>(256, Allocator.Temp);
            
            // Хэшсеты для защиты от бесконечных циклов
            var deleteVisited = new NativeHashSet<int3>(1024, Allocator.Temp);
            var fillVisited = new NativeHashSet<int3>(1024, Allocator.Temp);

            int2 lastChunkPos = new(int.MinValue, int.MinValue);
            DynamicBuffer<BlockElement> lastBuffer = default;
            bool hasLastBuffer = false;

            var dirs = new NativeArray<int3>(4, Allocator.Temp);
            dirs[0] = new(1, 0, 0);
            dirs[1] = new(-1, 0, 0);
            dirs[2] = new(0, 0, -1);
            dirs[3] = new(0, 0, 1);

            var roadsToUpdate = new NativeHashSet<Entity>(100, Allocator.Temp);

            // Шаг 1: Инициализация очередей
            for (int i = 0; i != points.Length; i++)
            {
                int3 p = points[i].pos;
                if (MapData.CellWeights.ContainsKey(p))
                {
                    deleteQueue.Enqueue(p);
                }
                for (int d = 0; d != dirs.Length; d++)
                {
                    CheckPoint(p + dirs[d], ref roadsToUpdate);
                }
            }

            // Шаг 2: Каскадный поиск удаляемых весов (ИСПРАВЛЕНО ЗАВИСАНИЕ)
            while (deleteQueue.TryDequeue(out int3 curr))
            {
                if (!deleteVisited.Add(curr)) 
                    continue;

                if (!MapData.CellWeights.TryGetValue(curr, out float currWeight))
                    continue;

                cellsToRemove.Add(curr);

                for (int i = 0; i != 18; i++)
                {
                    var dir = GetWeightDirection(i);
                     if (dir.Offset.y != 0) continue; 
                    int3 n = curr + dir.Offset;

                    if (IsBlocked(n, ref lastChunkPos, ref lastBuffer, ref hasLastBuffer))
                        continue;

                    if (MapData.CellWeights.TryGetValue(n, out float w))
                    {
                        float expected = currWeight + dir.StepCost;
                        
                        // ИСПРАВЛЕНО: проверяем равенство веса (с погрешностью), а не неравенство
                        if (math.abs(w - expected) < 0.001f)
                        {
                            if (expected > MaxSearchDist) 
                                continue;
                            
                            deleteQueue.Enqueue(n);
                        }
                        else
                        {
                            if (fillVisited.Add(n))
                            {
                                fillQueue.Enqueue(n);
                            }
                        }
                    }
                }
            }

            // Чистим точки дорог с карты
            for (int i = 0; i != points.Length; i++)
            {
                var p = points[i];
                MapData.CellMapEntites.Remove(p.pos);
                MapData.CellMapBuildingsIDs.Remove(p.pos);
                MapData.IsBluePrintOrDemolitionPoints.Remove(p.pos);
            }

            // Шаг 3: Физически стираем старые веса
            for (int i = 0; i != cellsToRemove.Length; i++)
            {
                var c = cellsToRemove[i];
                MapData.CellWeights.Remove(c);
                MapData.CellDirections.Remove(c);
            }

            // Шаг 4: Волна «затекания» соседних весов в пустоту
            while (fillQueue.TryDequeue(out int3 curr))
            {
                if (!MapData.CellWeights.TryGetValue(curr, out float currWeight))
                    continue;

                affectedCells.Add(curr);

                for (int i = 0; i != 18; i++)
                {
                    var dir = GetWeightDirection(i);
                     if (dir.Offset.y != 0) continue; 
                    int3 n = curr + dir.Offset;

                    if (IsBlocked(n, ref lastChunkPos, ref lastBuffer, ref hasLastBuffer))
                        continue;

                    float newW = currWeight + dir.StepCost;
                    if (newW > MaxSearchDist) 
                        continue;

                    if (!MapData.CellWeights.TryGetValue(n, out float old))
                    {
                        if (fillVisited.Add(n))
                        {
                            MapData.CellWeights.Add(n, newW);
                            fillQueue.Enqueue(n);
                        }
                    }
                    else if (newW < old - 0.001f) // Защита от микро-колебаний float
                    {
                        MapData.CellWeights[n] = newW;
                        if (fillVisited.Add(n))
                        {
                            fillQueue.Enqueue(n);
                        }
                    }
                    else 
                    {
                        if (fillVisited.Add(n))
                        {
                            affectedCells.Add(n);
                        }
                    }
                }
            }

            // Шаг 5: Пересчет Flow Field
           for (int i = 0; i != affectedCells.Length; i++)
            {
                int3 curr = affectedCells[i];
                float cw = MapData.CellWeights[curr];
                float3 flow = float3.zero;
                bool found = false;
                for (int d = 0; d != 26; d++)
                {
                    var fd = GetFlowDirection(d);
                    
                    // ЖЕСТКАЯ ФИЛЬТРАЦИЯ 2D: Игнорируем соседей сверху/снизу
                    if (fd.Offset.y != 0)
                        continue;

                    int3 n = curr + fd.Offset;
                    if (IsBlocked(n, ref lastChunkPos, ref lastBuffer, ref hasLastBuffer))
                        continue;

                    if (MapData.CellWeights.TryGetValue(n, out float w) && w < cw)
                    {
                        float diff = cw - w;
                        
                        // Расчет честного горизонтального 2D-вектора
                        float2 flatDir2D = math.normalize(new float2(fd.Offset.x, fd.Offset.z));
                        float3 flatDir3D = new float3(flatDir2D.x, 0f, flatDir2D.y);
                        
                        flow += flatDir3D * diff;
                        found = true;
                    }
                }

                if (found && math.lengthsq(flow) > 0.001f)
                {
                    MapData.CellDirections[curr] = math.normalize(flow);
                }
                else
                {
                    MapData.CellDirections[curr] = float3.zero;
                }
            }
            // Освобождение ресурсов
            deleteQueue.Dispose();
            cellsToRemove.Dispose();
            fillQueue.Dispose();
            affectedCells.Dispose();
            dirs.Dispose();
            deleteVisited.Dispose();
            fillVisited.Dispose();

            MapData.CellEntityMultiMap.Remove(entity);
            EntityDictionary.Entities.Remove(buildingData.BuildingUniqueID);

            foreach (var r in roadsToUpdate)
                ECB.SetComponentEnabled<UpdateManyPoint>(r, true);

            roadsToUpdate.Dispose();

            ECB.DestroyEntity(entity);
            ECB.SetComponentEnabled<UpdateClusterSlots>(Map, true);
            ECB.SetComponentEnabled<UpdateMapTag>(Map, true);
            ECB.SetComponentEnabled<UpdateClustersTag>(Map, true);
        }

        void CheckPoint(int3 pos, ref NativeHashSet<Entity> roads)
        {
            if (MapData.CellMapEntites.ContainsKey(pos))
            {
                var e = MapData.CellMapEntites[pos];
                if (ManyPointLookup.HasComponent(e) && e != roadEn)
                    roads.Add(e);
            }
        }
    }

}