using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
[DisableAutoCreation]

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BuildingCreateSystem))]
 [BurstCompile]
public partial struct PathFindingSystem : ISystem
{
    BuildingConfigReference _buildingConfigs;
    EntityQuery _pathFindingEntityQuery;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BuildingMap>();
        state.RequireForUpdate<BuildingConfigReference>();
        if (SystemAPI.TryGetSingleton<BuildingConfigReference>(out var lib))
        {
            _buildingConfigs = lib;
        }
        _pathFindingEntityQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<PathfindingRequest>()
            .Build(ref state);
    }
    public void OnUpdate(ref SystemState state)
    {
        var map = SystemAPI.GetSingleton<BuildingMap>();
        if(!_pathFindingEntityQuery.IsEmpty)
        {
            state.Dependency = new PathfindingParallelJob
            {
                BuildingMap = map.CellMapBuildingsIDs,
                //PathLookup=SystemAPI.GetComponentLookup<PathfindingRequest>(false)
            }.ScheduleParallel(state.Dependency);
        }
        
    }

[BurstCompile]
[WithAll(typeof(PathfindingRequest))]
public partial struct PathfindingParallelJob : IJobEntity
{
    [ReadOnly] public NativeParallelHashMap<int2, int> BuildingMap;

    public void Execute(
        Entity entity, 
        RefRW<PathfindingRequest> requestRef, 
        EnabledRefRW<PathfindingRequest> requestEnabled, 
        DynamicBuffer<MapPoint> pathBuffer)
    {
        var request = requestRef.ValueRO; 
        pathBuffer.Clear();

        if (request.Start.Equals(request.End))
        {
            pathBuffer.Add(new MapPoint { pos = request.Start });
            requestEnabled.ValueRW = false;
            return;
        }

        var openSet = new NativeList<Node>(Allocator.Temp);
        var closedSet = new NativeParallelHashSet<int2>(256, Allocator.Temp);
        var cameFrom = new NativeParallelHashMap<int2, int2>(256, Allocator.Temp);
        var gScoreMap = new NativeParallelHashMap<int2, float>(256, Allocator.Temp);

        openSet.Add(new Node 
        { 
            Position = request.Start, 
            Direction = new int2(0, 0),
            GScore = 0, 
            HScore = GetDistance(request.Start, request.End)
        });
        gScoreMap.TryAdd(request.Start, 0f);

        Node bestNode = openSet[0];
        bool exactPathFound = false;
        int iterations = 0;

        while (openSet.Length > 0 && iterations < 5000)
        {
            iterations++;
            
            int bestIndex = 0;
            for (int i = 1; i < openSet.Length; i++)
            {
                if (openSet[i].FScore < openSet[bestIndex].FScore)
                    bestIndex = i;
            }

            var current = openSet[bestIndex];
            openSet.RemoveAtSwapBack(bestIndex);

            if (current.HScore < bestNode.HScore) bestNode = current;
            if (current.Position.Equals(request.End))
            {
                exactPathFound = true;
                break;
            }

            if (!closedSet.Add(current.Position)) continue;

            for (int i = 0; i < 4; i++)
            {
                int2 offset = i switch {
                    0 => new int2(0, 1), 1 => new int2(0, -1),
                    2 => new int2(1, 0), _ => new int2(-1, 0)
                };

                int2 neighborPos = current.Position + offset;
                if (closedSet.Contains(neighborPos)) continue;

                bool hasBuilding = BuildingMap.TryGetValue(neighborPos, out int bID);
                bool sameBuilding = hasBuilding && bID == request.BuildingID;

                // Непроходимо, если там чужое здание
                if (hasBuilding && !sameBuilding) continue;

                // ПРИОРИТЕТ: по дороге идти в 10 раз выгоднее, чем по пустой клетке
                float stepCost = sameBuilding ? 0.1f : 1.0f;
                float turnPenalty = (current.Direction.x == offset.x && current.Direction.y == offset.y) ? 0f : 0.01f;
                
                float tentativeG = current.GScore + stepCost + turnPenalty;

                if (!gScoreMap.TryGetValue(neighborPos, out float oldG) || tentativeG < oldG)
                {
                    cameFrom[neighborPos] = current.Position;
                    gScoreMap[neighborPos] = tentativeG;
                    
                    openSet.Add(new Node { 
                        Position = neighborPos, 
                        Direction = offset,
                        GScore = tentativeG, 
                        HScore = GetDistance(neighborPos, request.End)
                    });
                }
            }
        }

        int2 backtrackPos = exactPathFound ? request.End : bestNode.Position;
        while (cameFrom.TryGetValue(backtrackPos, out int2 prev))
        {
            pathBuffer.Add(new MapPoint { pos = backtrackPos });
            backtrackPos = prev;
        }
        pathBuffer.Add(new MapPoint { pos = request.Start });

        requestEnabled.ValueRW = false; 

        // Обязательно Dispose в конце Execute, если используешь Allocator.Temp (не в Job)
        // Но здесь Allocator.Temp живет до конца кадра, так что ок.
    }

    private static float GetDistance(int2 a, int2 b)
    {
        return (math.abs(a.x - b.x) + math.abs(a.y - b.y)) * 0.99f;
    }

    struct Node {
        public int2 Position; 
        public int2 Direction; 
        public float GScore;   
        public float HScore;  
        public float FScore => GScore + HScore;
    }
}


}