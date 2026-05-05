using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BuildingCreateSystem))]
[BurstCompile]
public partial struct PathFindingSystem : ISystem
{
    EntityQuery _pathFindingEntityQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BuildingMap>();
        state.RequireForUpdate<ChunkMap>();
        state.RequireForUpdate<WorldSettings>();

        _pathFindingEntityQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<PathfindingRequest>()
            .Build(ref state);
    }

    public void OnUpdate(ref SystemState state)
    {
        if (_pathFindingEntityQuery.IsEmpty) return;

        var map = SystemAPI.GetSingleton<BuildingMap>();
        var chunkMap = SystemAPI.GetSingleton<ChunkMap>();
        var settings = SystemAPI.GetSingleton<WorldSettings>();

        var job = new PathfindingParallelJob
        {
            BuildingMap = map.CellMapBuildingsIDs,
            ChunkMap = chunkMap,
            BlockLookup = SystemAPI.GetBufferLookup<BlockElement>(true),
            Settings = settings
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

[BurstCompile]
[WithAll(typeof(PathfindingRequest))]
public partial struct PathfindingParallelJob : IJobEntity
{
    [ReadOnly] public NativeParallelHashMap<int3, int> BuildingMap;
    [ReadOnly] public ChunkMap ChunkMap;
    [ReadOnly] public BufferLookup<BlockElement> BlockLookup;
    [ReadOnly] public WorldSettings Settings;

    private struct ChunkCache { public int2 Coords; public DynamicBuffer<BlockElement> Buffer; public bool IsValid; }

    public void Execute(Entity entity, RefRW<PathfindingRequest> requestRef, EnabledRefRW<PathfindingRequest> requestEnabled, DynamicBuffer<MapPoint> pathBuffer)
    {
        var directionMap = new NativeParallelHashMap<int3, int3>(1024, Allocator.Temp);
        var request = requestRef.ValueRO;
        pathBuffer.Clear();

        int3 startPos = new int3(
            (int)math.floor(request.Start.x), 
            (int)math.floor(request.Start.y + 0.1f), 
            (int)math.floor(request.Start.z)
        );
        int3 endPos = request.End;

        ChunkCache cache = new ChunkCache { IsValid = false };
        ChunkCache groundCache = new ChunkCache { IsValid = false };

        var openSet = new NativeList<Node>(Allocator.Temp);
        var closedSet = new NativeParallelHashSet<int3>(1024, Allocator.Temp);
        var gScoreMap = new NativeParallelHashMap<int3, float>(1024, Allocator.Temp);
        var cameFrom = new NativeParallelHashMap<int3, int3>(1024, Allocator.Temp);

        // --- ПЕРЕМЕННЫЕ ДЛЯ ЛУЧШЕЙ ДОСТИЖИМОЙ ТОЧКИ ---
        int3 bestReachedPos = startPos;
        float minH = math.distance(math.float3(startPos), math.float3(endPos));
        // ----------------------------------------------

        openSet.Add(new Node { Position = startPos, GScore = 0, HScore = minH });
        gScoreMap.TryAdd(startPos, 0f);

        bool found = false;
        int iterations = 0;

        while (openSet.Length > 0 && iterations < 2000)
        {
            iterations++;
            
            int bestIdx = 0;
            float minF = openSet[0].FScore;
            for (int i = 1; i < openSet.Length; i++) {
                if (openSet[i].FScore < minF) {
                    minF = openSet[i].FScore;
                    bestIdx = i;
                }
            }

            var current = openSet[bestIdx];
            openSet.RemoveAtSwapBack(bestIdx);

            // Обновляем "лучшую" точку, если эта нода ближе к цели, чем предыдущие
            if (current.HScore < minH)
            {
                minH = current.HScore;
                bestReachedPos = current.Position;
            }

            if (current.Position.Equals(endPos)) { 
                found = true; 
                bestReachedPos = current.Position; // Точно дошли
                break; 
            }

            if (!closedSet.Add(current.Position)) continue;

                for (int i = 0; i < directions.Length; i++)
            {
                int3 neighbor = current.Position + directions[i];
                if (closedSet.Contains(neighbor)) continue;

                if (IsPhysicallyBlocked(neighbor, ref cache)) continue;

                int3 under = neighbor + new int3(0, -1, 0);
                bool hasGround = IsSolidBlock(under, ref groundCache) || IsSolidBlock(under + new int3(0, -1, 0), ref groundCache);
                if (!hasGround) continue;

                  float cost = math.distance(math.float3(current.Position), math.float3(neighbor));
                
                // Штраф за поворот (увеличиваем до 10, чтобы диагональ была очень дорогой)
                if (request.straigh) 
                {
                    if (directionMap.TryGetValue(current.Position, out int3 prevDir))
                    {
                        int3 currentDir = neighbor - current.Position;
                        if (!currentDir.Equals(prevDir)) cost += 10.0f; 
                    }
                }

                // Твоя логика SamePerfer (со штрафом/бонусом)
                bool isSameBuilding = BuildingMap.ContainsKey(neighbor) || BuildingMap.ContainsKey(under);
                if (isSameBuilding) cost += request.SamePerfer ? -0.9f : 50.0f;

                float newG = current.GScore + cost;

                if (!gScoreMap.TryGetValue(neighbor, out float oldG) || newG < oldG)
                {
                    // --- 2. ИЗМЕНЕННАЯ ЭВРИСТИКА (H) ---
                    float h;
                    if (request.straigh)
                    {
                        // Используем Манхэттенское расстояние: путь по сетке всегда будет иметь одинаковый H
                        int3 diff = math.abs(neighbor - endPos);
                        h = (diff.x + diff.y + diff.z) * 1.01f; // небольшой множитель для стабильности
                    }
                    else
                    {
                        h = math.distance(math.float3(neighbor), math.float3(endPos));
                    }

                    gScoreMap[neighbor] = newG;
                    cameFrom[neighbor] = current.Position;
                    directionMap[neighbor] = neighbor - current.Position;

                    openSet.Add(new Node { 
                        Position = neighbor, 
                        GScore = newG, 
                        HScore = h 
                    });
                }
            }
        }

        // --- ВОССТАНОВЛЕНИЕ ПУТИ ---
        // Если нашли цель — строим до цели. Если нет — строим до bestReachedPos.
        int3 curr = bestReachedPos; 
        
        // Предотвращаем бесконечный цикл, если путь не был найден даже на один шаг
        if (!curr.Equals(startPos) || found) 
        {
            while (!curr.Equals(startPos)) {
                pathBuffer.Add(new MapPoint { pos = curr });
                if (!cameFrom.TryGetValue(curr, out curr)) break;
            }
            pathBuffer.Add(new MapPoint { pos = startPos });
        }
        
        requestEnabled.ValueRW = false;
    }

    private bool IsSolidBlock(int3 p, ref ChunkCache cache)
    {
        // 1. Границы мира
        if (p.y < 0) return true; // Считаем, что под миром твердь
        if (p.y >= Settings.Height) return false;

        // 2. Координаты чанка (используем math.floor для отрицательных координат)
        int cx = (int)math.floor((float)p.x / Settings.Size);
        int cz = (int)math.floor((float)p.z / Settings.Size);
        int2 cp = new int2(cx, cz);

        if (!cache.IsValid || !cache.Coords.Equals(cp))
        {
            if (ChunkMap.ChunkMapData.TryGetValue(cp, out var e) && BlockLookup.HasBuffer(e))
            {
                cache.Buffer = BlockLookup[e];
                cache.Coords = cp;
                cache.IsValid = true;
            }
            else return false; 
        }

        // 3. Локальные координаты внутри чанка (безопасно для отрицательных чисел)
        int lx = p.x % Settings.Size; if (lx < 0) lx += Settings.Size;
        int lz = p.z % Settings.Size; if (lz < 0) lz += Settings.Size;

        // 4. ФОРМУЛА ИНДЕКСА (взята из твоего генератора)
        // ГЕНЕРАТОР: x + World.Size * (y + World.Height * z)
        int index = lx + Settings.Size * (p.y + Settings.Height * lz);
        
        if (index < 0 || index >= cache.Buffer.Length) return false;
        
        return cache.Buffer[index].BlockID != 0; 
    }

    private bool IsPhysicallyBlocked(int3 p, ref ChunkCache cache)
    {
        // Проверка, что сама клетка, куда мы хотим наступить — это воздух
        return IsSolidBlock(p, ref cache);
    }
    // Убрал сильные перепады высот из направлений, чтобы он не прыгал через блоки
    static readonly int3[] directions = {
        new int3(1,0,0), new int3(-1,0,0), new int3(0,0,1), new int3(0,0,-1), // Прямые
        new int3(1,1,0), new int3(-1,1,0), new int3(0,1,1), new int3(0,1,-1), // Ступеньки вверх
        new int3(1,-1,0), new int3(-1,-1,0), new int3(0,-1,1), new int3(0,-1,-1) // Ступеньки вниз
    };

    struct Node { public int3 Position; public float GScore; public float HScore; public float FScore => GScore + HScore; }
}

}