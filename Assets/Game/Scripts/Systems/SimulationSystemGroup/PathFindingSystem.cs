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
                RoadID = _buildingConfigs.roadID,
                //PathLookup=SystemAPI.GetComponentLookup<PathfindingRequest>(false)
            }.ScheduleParallel(state.Dependency);
        }
        
    }

   [BurstCompile]
[WithAll(typeof(PathfindingRequest))]
public partial struct PathfindingParallelJob : IJobEntity
{
    [ReadOnly] public NativeParallelHashMap<int2, int> BuildingMap;
    public int RoadID;

    public void Execute(
        Entity entity, 
        RefRW<PathfindingRequest> requestRef, // Используем RefRW вместо комбинации in и EnabledRefRW
        EnabledRefRW<PathfindingRequest> requestEnabled, 
        DynamicBuffer<MapPoint> pathBuffer)
    {
        // Получаем доступ к данным через .ValueRO (Read Only)
        var request = requestRef.ValueRO; 
        
        pathBuffer.Clear();
        pathBuffer.Add(new MapPoint { pos = request.Start });
        if (request.Start.Equals(request.End))
        {
            pathBuffer.Add(new MapPoint { pos = request.Start });
            requestEnabled.ValueRW = false;
            return;
        }

        // Используем Allocator.TempJob для параллельных вычислений
        var openSet = new NativeList<Node>(Allocator.TempJob);
        var closedSet = new NativeParallelHashSet<int2>(64, Allocator.TempJob);
        var cameFrom = new NativeParallelHashMap<int2, int2>(64, Allocator.TempJob);
        var gScoreMap = new NativeParallelHashMap<int2, float>(64, Allocator.TempJob);

        int2 delta = request.End - request.Start;
        int2 initialDir = math.abs(delta.x) >= math.abs(delta.y) 
            ? new int2(delta.x > 0 ? 1 : -1, 0) 
            : new int2(0, delta.y > 0 ? 1 : -1);

        openSet.Add(new Node 
        { 
            Position = request.Start, 
            Direction = initialDir,
            GScore = 0, 
            HScore = math.abs(delta.x) + math.abs(delta.y)
        });
        gScoreMap.TryAdd(request.Start, 0f);

        Node bestNode = openSet[0];
        bool exactPathFound = false;
        int iterations = 0;

        while (openSet.Length > 0 && iterations < 1000)
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

                if (BuildingMap.TryGetValue(neighborPos, out int bID) && bID != RoadID)
                    continue;

                float turnPenalty = current.Direction.Equals(offset) ? 0f : 0.6f;
                float tentativeG = current.GScore + 1.0f + turnPenalty;

                if (!gScoreMap.TryGetValue(neighborPos, out float oldG) || tentativeG < oldG)
                {
                    cameFrom[neighborPos] = current.Position;
                    gScoreMap[neighborPos] = tentativeG;
                    openSet.Add(new Node { 
                        Position = neighborPos, 
                        Direction = offset,
                        GScore = tentativeG, 
                        HScore = math.abs(neighborPos.x - request.End.x) + math.abs(neighborPos.y - request.End.y)
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

        // Выключаем компонент
        requestEnabled.ValueRW = false; 

        // Очистка памяти
        openSet.Dispose();
        closedSet.Dispose();
        cameFrom.Dispose();
        gScoreMap.Dispose();
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