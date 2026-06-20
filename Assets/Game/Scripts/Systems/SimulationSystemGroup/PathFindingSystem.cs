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
        
        private struct Node 
        { 
            public int3 Position; 
            public float GScore; 
            public float HScore; 
            public int3 Direction; 
            public float FScore => GScore + HScore; 
        }

        
        private bool IsSolidBlock(int3 p, ref ChunkCache cache, int requestBuildingID)
        {
            if (p.y < 0) return true;
            if (p.y >= Settings.Height) return false;

            
            if (BuildingMap.TryGetValue(p, out int existingBuildingID)) 
            {
                
                if (existingBuildingID != requestBuildingID) return true;
                return false; 
            }

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
            int lx = p.x % Settings.Size; if (lx < 0) lx += Settings.Size;
            int lz = p.z % Settings.Size; if (lz < 0) lz += Settings.Size;
            int index = lx + Settings.Size * (p.y + Settings.Height * lz);
            
            if (index < 0 || index >= cache.Buffer.Length) return false;
            
            return cache.Buffer[index].BlockID != 0; 
        }

        
        private bool IsWalkableSpace(int3 p, ref ChunkCache cache, int requestBuildingID)
        {
            if (IsSolidBlock(p, ref cache, requestBuildingID)) return false;
            if (IsSolidBlock(p + new int3(0, 1, 0), ref cache, requestBuildingID)) return false;
            if (!IsSolidBlock(p + new int3(0, -1, 0), ref cache, requestBuildingID)) return false;

            return true;
        }

        public void Execute(Entity entity, RefRW<PathfindingRequest> requestRef, EnabledRefRW<PathfindingRequest> requestEnabled, DynamicBuffer<MapPoint> pathBuffer)
        {
            var request = requestRef.ValueRO;
            pathBuffer.Clear();

            int3 startPos = new int3(
                (int)math.floor(request.Start.x), 
                (int)math.floor(request.Start.y + 0.1f), 
                (int)math.floor(request.Start.z)
            );
            int3 endPos = request.End;

            ChunkCache cache = new ChunkCache { IsValid = false };

            
            if (IsSolidBlock(endPos, ref cache, request.BuildingID))
            {
                int3 alternateEnd = endPos;
                float closestDist = float.MaxValue;
                bool foundValidNeighbor = false;

                var checkDirs = new NativeArray<int3>(4, Allocator.Temp);
                checkDirs[0] = new int3(1, 0, 0);
                checkDirs[1] = new int3(-1, 0, 0);
                checkDirs[2] = new int3(0, 0, 1);
                checkDirs[3] = new int3(0, 0, -1);

                for (int i = 0; i < checkDirs.Length; i++)
                {
                    int3 neighbor = endPos + checkDirs[i];
                    
                    if (IsWalkableSpace(neighbor, ref cache, request.BuildingID))
                    {
                        float d = math.distance(math.float3(startPos), math.float3(neighbor));
                        if (d < closestDist)
                        {
                            closestDist = d;
                            alternateEnd = neighbor;
                            foundValidNeighbor = true;
                        }
                    }
                }
                checkDirs.Dispose();
                if (foundValidNeighbor)
                {
                    endPos = alternateEnd;
                }
                else
                {
                    requestEnabled.ValueRW = false;
                    return;
                }
            }

            if (math.all(startPos == endPos))
            {
                requestEnabled.ValueRW = false;
                return;
            }

            var openSet = new NativeList<Node>(Allocator.Temp);
            var closedSet = new NativeParallelHashSet<int3>(1024, Allocator.Temp);
            var gScoreMap = new NativeParallelHashMap<int3, float>(1024, Allocator.Temp);
            var cameFrom = new NativeParallelHashMap<int3, int3>(1024, Allocator.Temp);

            float startH = math.distance(math.float3(startPos), math.float3(endPos)) * 0.5f;
            openSet.Add(new Node { Position = startPos, GScore = 0, HScore = startH, Direction = new int3(0,0,0) });
            gScoreMap.TryAdd(startPos, 0f);

            bool found = false;
            int iterations = 0;

            var dirs = new NativeArray<int3>(12, Allocator.Temp);
            dirs[0] = new int3(1,0,0);  dirs[1] = new int3(-1,0,0); dirs[2] = new int3(0,0,1);  dirs[3] = new int3(0,0,-1);
            dirs[4] = new int3(1,1,0);  dirs[5] = new int3(-1,1,0); dirs[6] = new int3(0,1,1);  dirs[7] = new int3(0,1,-1);
            dirs[8] = new int3(1,-1,0); dirs[9] = new int3(-1,-1,0);dirs[10] = new int3(0,-1,1); dirs[11] = new int3(0,-1,-1);

            while (openSet.Length > 0 && iterations < 2000)
            {
                iterations++;
                
                int bestIdx = 0;
                float minF = openSet[bestIdx].FScore;
                for (int i = 1; i < openSet.Length; i++)
                {
                    if (openSet[i].FScore < minF)
                    {
                        minF = openSet[i].FScore;
                        bestIdx = i;
                    }
                }

                Node current = openSet[bestIdx];

                if (math.all(current.Position == endPos))
                {
                    found = true;
                    break;
                }

                openSet.RemoveAtSwapBack(bestIdx);
                closedSet.Add(current.Position);

                for (int i = 0; i < dirs.Length; i++)
                {
                    if (request.straigh && i > 3) continue;

                    int3 neighborPos = current.Position + dirs[i];

                    if (closedSet.Contains(neighborPos)) continue;
                    
                    
                    if (!IsWalkableSpace(neighborPos, ref cache, request.BuildingID)) continue;

                    if (dirs[i].y != 0)
                    {
                        int3 overhead = current.Position + new int3(0, 2, 0);
                        if (IsSolidBlock(overhead, ref cache, request.BuildingID)) continue;
                    }

                    float cellCostMultiplier = 1.0f;

                    if (BuildingMap.TryGetValue(neighborPos, out int existingBuildingID))
                    {
                        if (existingBuildingID == request.BuildingID)
                        {
                            
                            
                            cellCostMultiplier = request.SamePerfer ? 0.2f : 3.0f; 
                        }
                        else
                        {
                            
                            cellCostMultiplier = 5.0f; 
                        }
                    }
                    else
                    {
                        
                        
                        
                        cellCostMultiplier = request.SamePerfer ? 1.5f : 0.8f;
                    }

                    float baseStepCost = (dirs[i].y != 0) ? 1.41f : 1.0f;
                    float stepCost = baseStepCost * cellCostMultiplier;
                                        
                    bool isChangingDirection = math.any(current.Direction != 0) && !math.all(current.Direction == dirs[i]);

                    if (request.straigh) 
                    {
                        if (isChangingDirection)
                        {
                            stepCost += 15.0f; 
                        }
                    }
                    else
                    {
                        
                        
                        if (!isChangingDirection)
                        {
                            
                            float snakePattern = math.sin(neighborPos.x * 1.2f) * math.cos(neighborPos.z * 1.2f);
                            stepCost += 0.3f + math.abs(snakePattern) * 0.8f; 
                        }
                        else
                        {
                            
                            stepCost += 0.1f; 
                        }
                    }

                    float tentativeG = current.GScore + stepCost;

                    if (!gScoreMap.TryGetValue(neighborPos, out float oldG) || tentativeG < oldG)
                    {
                        cameFrom[neighborPos] = current.Position;
                        gScoreMap[neighborPos] = tentativeG;

                        float h = math.distance(math.float3(neighborPos), math.float3(endPos)) * 0.5f;
                        
                        Node neighborNode = new Node { Position = neighborPos, GScore = tentativeG, HScore = h, Direction = dirs[i] };

                        bool inOpenSet = false;
                        for (int j = 0; j < openSet.Length; j++)
                        {
                            if (math.all(openSet[j].Position == neighborPos))
                            {
                                openSet[j] = neighborNode;
                                inOpenSet = true;
                                break;
                            }
                        }

                        if (!inOpenSet) openSet.Add(neighborNode);
                    }
                }
            }

            if (found)
            {
                int3 curr = endPos;
                var tempPath = new NativeList<MapPoint>(Allocator.Temp);
                
                while (!math.all(curr == startPos))
                {
                    tempPath.Add(new MapPoint { pos = math.int3(curr) });
                    curr = cameFrom[curr];
                }
                tempPath.Add(new MapPoint { pos = math.int3(startPos) });

                for (int i = tempPath.Length - 1; i >= 0; i--)
                {
                    pathBuffer.Add(tempPath[i]);
                }
            }
            dirs.Dispose(); 
            requestEnabled.ValueRW = false;
        }
    }

}