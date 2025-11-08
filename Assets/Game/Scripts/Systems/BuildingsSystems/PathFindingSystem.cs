using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(MapSystem))]
public partial class PathfindingSystem : SystemBase
{
    Entity _mapEntity;
    bool _isInitialized = false;
    int _roadHash;

    struct PendingJob
    {
        public JobHandle handle;
        public Action<NativeList<int2>> callback;
        public NativeList<int2> result;
        public int2 start;
        public int2 end;
    }

    List<PendingJob> _pendingJobs = new List<PendingJob>();
    Dictionary<(int2 start, int2 end), NativeList<int2>> _pathCache =
        new Dictionary<(int2, int2), NativeList<int2>>();

    protected override void OnCreate()
    {
        _isInitialized = false;

        _roadHash = "Road".GetStableHashCode();
    }

    protected override void OnUpdate()
    {
        if (!_isInitialized)
            TryInitialize();

        if (!_isInitialized)
            return;

        CompleteAllPendingJobs();

        if (!SystemAPI.HasSingleton<BuildingMap>())
            return;
        var buildingMap = SystemAPI.GetSingleton<BuildingMap>();

        for (int i = _pendingJobs.Count - 1; i >= 0; i--)
        {
            var pj = _pendingJobs[i];
            if (pj.handle.IsCompleted)
            {
                pj.handle.Complete();

                var pathCopy = new NativeList<int2>(Allocator.Persistent);
                for (int j = 0; j < pj.result.Length; j++)
                    pathCopy.Add(pj.result[j]);

                var cacheKey = (pj.start, pj.end);
                if (!_pathCache.ContainsKey(cacheKey))
                    _pathCache[cacheKey] = pathCopy;
                else
                    pathCopy.Dispose();

                var callbackCopy = new NativeList<int2>(Allocator.Persistent);
                var cachedPath = _pathCache[cacheKey];
                for (int k = 0; k < cachedPath.Length; k++)
                    callbackCopy.Add(cachedPath[k]);

                pj.callback?.Invoke(callbackCopy);

                if (pj.result.IsCreated)
                    pj.result.Dispose();

                _pendingJobs.RemoveAtSwapBack(i);
            }
        }
    }

    void CompleteAllPendingJobs()
    {
        for (int i = _pendingJobs.Count - 1; i >= 0; i--)
        {
            var pj = _pendingJobs[i];
            if (!pj.handle.IsCompleted)
            {
                pj.handle.Complete();
            }
        }
    }

    public JobHandle GetPendingJobsHandle()
    {
        JobHandle combined = default;
        bool first = true;
        for (int i = 0; i < _pendingJobs.Count; i++)
        {
            var h = _pendingJobs[i].handle;
            if (first)
            {
                combined = h;
                first = false;
            }
            else
            {
                combined = JobHandle.CombineDependencies(combined, h);
            }
        }
        return combined;
    }

    void TryInitialize()
    {
        if (_isInitialized)
            return;

        var query = EntityManager.CreateEntityQuery(ComponentType.ReadWrite<BuildingMap>());
        if (!query.IsEmpty && query.GetSingleton<BuildingMap>().CellMapBuildings.IsCreated)
        {
            _mapEntity = query.GetSingletonEntity();
            _isInitialized = true;
        }
        query.Dispose();
    }

    public void FindBuildingPathAsync(int2 start, int2 end, Action<NativeList<int2>> onComplete)
    {
        if (!_isInitialized)
        {
            onComplete?.Invoke(new NativeList<int2>(Allocator.Temp));
            return;
        }

        CompleteAllPendingJobs();
        if (!SystemAPI.HasSingleton<BuildingMap>())
        {
            onComplete?.Invoke(new NativeList<int2>(Allocator.Temp));
            return;
        }

        var buildingMap = EntityManager.GetComponentData<BuildingMap>(_mapEntity);

        bool startBlocked =
            buildingMap.CellMapBuildings.TryGetValue(start, out int startID)
            && startID != _roadHash;
        bool endBlocked =
            buildingMap.CellMapBuildings.TryGetValue(end, out int endID) && endID != _roadHash;

        if (startBlocked)
        {
            int2? freeStart = GetFreeNeighbor(start, buildingMap.CellMapBuildings);
            start = freeStart ?? start;
        }

        if (endBlocked)
        {
            int2? freeEnd = GetFreeNeighbor(end, buildingMap.CellMapBuildings);
            end = freeEnd ?? end;
        }

        InternalFindPathAStar(start, end, onComplete);
    }

    int2? GetFreeNeighbor(int2 cell, NativeParallelHashMap<int2, int> obstacles)
    {
        int2[] directions = new int2[]
        {
            new int2(1, 0),
            new int2(-1, 0),
            new int2(0, 1),
            new int2(0, -1),
        };

        foreach (var dir in directions)
        {
            int2 neighbor = cell + dir;
            if (!obstacles.ContainsKey(neighbor) || obstacles[neighbor] == _roadHash)
                return neighbor;
        }

        return null;
    }

    protected override void OnStartRunning()
    {
        TryInitialize();
    }

    void InternalFindPathAStar(int2 start, int2 end, Action<NativeList<int2>> onComplete)
    {
        if (!SystemAPI.HasSingleton<BuildingMap>())
        {
            onComplete?.Invoke(new NativeList<int2>(Allocator.Temp));
            return;
        }

        var buildingMap = SystemAPI.GetSingleton<BuildingMap>();
        var result = new NativeList<int2>(Allocator.TempJob);

        var job = new AStarJob
        {
            start = start,
            goal = end,
            obstacles = buildingMap.CellMapBuildings,
            result = result,
            roadHash = _roadHash,
        };

        JobHandle handle = job.Schedule();

        _pendingJobs.Add(
            new PendingJob
            {
                handle = handle,
                callback = onComplete,
                result = result,
                start = start,
                end = end,
            }
        );
    }

    [BurstCompile]
    struct AStarJob : IJob
    {
        [ReadOnly]
        public NativeParallelHashMap<int2, int> obstacles;

        [ReadOnly]
        public int roadHash;
        public int2 start;
        public int2 goal;
        public NativeList<int2> result;

        public void Execute()
        {
            NativeArray<int2> directions = new NativeArray<int2>(4, Allocator.Temp)
            {
                [0] = new int2(1, 0),
                [1] = new int2(-1, 0),
                [2] = new int2(0, 1),
                [3] = new int2(0, -1),
            };

            var openSet = new NativeList<int2>(Allocator.Temp);
            var cameFrom = new NativeParallelHashMap<int2, int2>(512, Allocator.Temp);
            var gScore = new NativeParallelHashMap<int2, float>(512, Allocator.Temp);
            var fScore = new NativeParallelHashMap<int2, float>(512, Allocator.Temp);

            gScore[start] = 0f;
            fScore[start] = Heuristic(start, goal);
            openSet.Add(start);

            while (openSet.Length > 0)
            {
                int2 current = openSet[0];
                float minScore = fScore[current];
                int minIndex = 0;

                for (int i = 1; i < openSet.Length; i++)
                {
                    int2 node = openSet[i];
                    float score = fScore.ContainsKey(node) ? fScore[node] : float.MaxValue;
                    if (score < minScore)
                    {
                        minScore = score;
                        current = node;
                        minIndex = i;
                    }
                }

                openSet.RemoveAtSwapBack(minIndex);

                if (current.Equals(goal) || IsGoalNeighbor(current))
                {
                    BuildResultPath(cameFrom, current);
                    break;
                }

                for (int i = 0; i < directions.Length; i++)
                {
                    int2 neighbor = current + directions[i];
                    bool blocked =
                        obstacles.ContainsKey(neighbor) && obstacles[neighbor] != roadHash;
                    if (blocked)
                        continue;

                    float stepCost = 1f;
                    if (cameFrom.TryGetValue(current, out int2 prev))
                    {
                        int2 prevDir = current - prev;
                        if (!prevDir.Equals(directions[i]))
                            stepCost = 2f;
                    }

                    float tentativeG = gScore.ContainsKey(current)
                        ? gScore[current] + stepCost
                        : float.MaxValue;

                    if (
                        !gScore.TryGetValue(neighbor, out float neighborG)
                        || tentativeG < neighborG
                    )
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + Heuristic(neighbor, goal);

                        bool inOpenSet = false;
                        for (int j = 0; j < openSet.Length; j++)
                            if (openSet[j].Equals(neighbor))
                            {
                                inOpenSet = true;
                                break;
                            }

                        if (!inOpenSet)
                            openSet.Add(neighbor);
                    }
                }
            }

            if (result.Length == 0)
                result.Add(start);

            directions.Dispose();
            openSet.Dispose();
            cameFrom.Dispose();
            gScore.Dispose();
            fScore.Dispose();
        }

        float Heuristic(int2 a, int2 b) => math.abs(a.x - b.x) + math.abs(a.y - b.y);

        bool IsGoalNeighbor(int2 current)
        {
            if (!obstacles.ContainsKey(goal) || obstacles[goal] == roadHash)
                return current.Equals(goal);

            int2 right = goal + new int2(1, 0);
            int2 left = goal + new int2(-1, 0);
            int2 up = goal + new int2(0, 1);
            int2 down = goal + new int2(0, -1);

            return current.Equals(right)
                || current.Equals(left)
                || current.Equals(up)
                || current.Equals(down);
        }

        void BuildResultPath(NativeParallelHashMap<int2, int2> cameFrom, int2 current)
        {
            var tempPath = new NativeList<int2>(Allocator.Temp);
            tempPath.Add(current);

            int2 prev;
            while (cameFrom.TryGetValue(current, out prev))
            {
                current = prev;
                tempPath.Add(current);
            }

            for (int i = tempPath.Length - 1; i >= 0; i--)
                result.Add(tempPath[i]);

            tempPath.Dispose();
        }
    }

    protected override void OnDestroy()
    {
        var combined = default(JobHandle);
        foreach (var pj in _pendingJobs)
            combined = combined.Equals(default)
                ? pj.handle
                : JobHandle.CombineDependencies(combined, pj.handle);
        if (!combined.Equals(default))
            combined.Complete();

        foreach (var pj in _pendingJobs)
            if (pj.result.IsCreated)
                pj.result.Dispose();
        _pendingJobs.Clear();

        foreach (var kv in _pathCache)
            if (kv.Value.IsCreated)
                kv.Value.Dispose();
        _pathCache.Clear();
    }
}
