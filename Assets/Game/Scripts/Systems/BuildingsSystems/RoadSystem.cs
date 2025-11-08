using System.Linq;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(MapSystem))] 
public partial struct RoadSystem : ISystem
{
    EntityQuery _createRoadsQuery;
    EntityQuery _createBluePrintRoadsQuery;
    EntityQuery _removePointsQuery;
    EntityQuery _mapUpdateQuery;
    NativeParallelHashMap<int2, Entity> roadMap;
    int roadHash;
    
    public void OnCreate(ref SystemState state)
    {
        roadHash = "Road".GetStableHashCode();
        roadMap = new NativeParallelHashMap<int2, Entity>(1000, Allocator.Persistent);
        _createRoadsQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ProcessBuildingEventComponent, CreateRoadSegmentsTag>()
            .WithAllRW<MapPointData>()
            .WithNone<BluePrint>()
            .Build(ref state);
            
        _createBluePrintRoadsQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ProcessBuildingEventComponent, CreateRoadSegmentsTag, BluePrint>()
            .WithAllRW<MapPointData>()
            .Build(ref state);
        _removePointsQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ProcessMapEventComponent, BuildingData, RemoveMapPointsTag>()
            .WithAllRW<MapPointData>()
            .Build(ref state);
        _mapUpdateQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BuildingMap, UpdateMapTag>()
            .Build(ref state);
    }

    [BurstCompile]
    void ResizeRoadDictionary(ref SystemState state, int newCapacity)
    {
        if (roadMap.Capacity >= newCapacity)
            return;

        var newRoadDictionary = new NativeParallelHashMap<int2, Entity>(newCapacity, Allocator.Persistent);

        foreach (var pair in roadMap)
        {
            newRoadDictionary.TryAdd(pair.Key, pair.Value);
        }

        
        var oldMap = roadMap;
        roadMap = newRoadDictionary;
        oldMap.Dispose();
    }


    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (roadMap.Count() > roadMap.Capacity * 0.8f)
        {
            ResizeRoadDictionary(ref state, roadMap.Capacity * 2);
        }

        if (!SystemAPI.HasSingleton<BuildingMap>()) return;

        var buildingMapRW = SystemAPI.GetSingletonRW<BuildingMap>();
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        try
        {
            state.Dependency.Complete();
            
            if (!_removePointsQuery.IsEmpty)
            {
                RemoveRoads(ref state, ecb, buildingMapRW.ValueRO);
            }
            
           if (!_createRoadsQuery.IsEmpty || !_createBluePrintRoadsQuery.IsEmpty)
            {
                
                var entityCount = (!_createRoadsQuery.IsEmpty ? _createRoadsQuery : _createBluePrintRoadsQuery).CalculateEntityCount();
                var estimatedPoints = entityCount * 100;
                using var uniquePoints = new NativeList<int2>(estimatedPoints, Allocator.TempJob);

                
                bool isBlueprint = !_createBluePrintRoadsQuery.IsEmpty;
                var queryToUse = isBlueprint ? _createBluePrintRoadsQuery : _createRoadsQuery;

                var collectJob = new GetNewPoints
                {
                    RoadMap = roadMap,
                    ecb = ecb.AsParallelWriter(),
                    UniquePointsWriter = uniquePoints.AsParallelWriter(),
                    IsBlueprint = isBlueprint
                };

                var collectHandle = collectJob.Schedule(queryToUse, state.Dependency);
                collectHandle.Complete();

                if (uniquePoints.Length > 0)
                {
                    CreateNewRoadsFromPoints(ref state, uniquePoints, isBlueprint);
                }
            }
            
            if (!_mapUpdateQuery.IsEmpty)
            {
                UpdateRoadClustering(ref state, ecb, buildingMapRW.ValueRO);
            }
            
            ecb.Playback(state.EntityManager);
        }
        finally
        {
            ecb.Dispose();
        }
    }
   
    [BurstCompile]
    void UpdateRoadClustering(ref SystemState state, EntityCommandBuffer ecb, BuildingMap buildingMap)
    {
        var phantomFlags = new NativeParallelHashMap<Entity, byte>(roadMap.Count(), Allocator.TempJob);
        foreach (var kv in roadMap)
        {
            var roadEntity = kv.Value;
            byte isPhantom = state.EntityManager.HasComponent<PhantomTag>(roadEntity) ? (byte)1 : (byte)0;
            phantomFlags.TryAdd(roadEntity, isPhantom);
        }

        var allRoadPoints = new NativeList<int2>(roadMap.Count(), Allocator.TempJob);
        foreach (var point in roadMap)
        {
            allRoadPoints.Add(point.Key);
        }

        var clusterStarts = new NativeList<int2>(Allocator.TempJob);
        var allClusterPoints = new NativeList<int2>(Allocator.TempJob);
        
        var clusterJob = new UniversalClusterJob
        {
            InputPoints = allRoadPoints,
            ClusterStarts = clusterStarts,
            AllClusterPoints = allClusterPoints
        };
        clusterJob.Schedule().Complete();

        var assignClusterJob = new AssignClusterIdsToRoadsJob
        {
            ClusterStarts = clusterStarts,
            AllClusterPoints = allClusterPoints,
            RoadMap = roadMap,
            Ecb = ecb.AsParallelWriter()
        };
        assignClusterJob.Schedule().Complete();

        var assignToBuildingsJob = new AssignClusterIdsToBuildingsJob
        {
            ClusterStarts = clusterStarts,
            AllClusterPoints = allClusterPoints,
            BuildingMap = buildingMap.CellEntity,
            RoadMap = roadMap,
            PhantomFlags = phantomFlags,
            Ecb = ecb.AsParallelWriter()
        };
        assignToBuildingsJob.Schedule().Complete();

        
        var mapEntity = SystemAPI.GetSingletonEntity<BuildingMap>();
        if (state.EntityManager.HasComponent<UpdateMapTag>(mapEntity))
        {
            ecb.RemoveComponent<UpdateMapTag>(mapEntity);
        }

        phantomFlags.Dispose();
        allRoadPoints.Dispose();
        clusterStarts.Dispose();
        allClusterPoints.Dispose();
    }
    [BurstCompile]
    void RemoveRoads(ref SystemState state, EntityCommandBuffer ecb, BuildingMap buildingMap)
    {
        var roadsToModify = new NativeParallelMultiHashMap<Entity, int2>(100, Allocator.TempJob);

        var collectRemoveJob = new RemovePointsJob
        {
            roadMap = roadMap,
            roadsToModify = roadsToModify,
            Ecb = ecb
        };
        collectRemoveJob.Schedule(_removePointsQuery, state.Dependency).Complete();

        if (roadsToModify.IsEmpty)
        {
            roadsToModify.Dispose();
            return;
        }
        
        var roadsToDelete = new NativeList<Entity>(Allocator.TempJob);
        var roadsToUpdate = new NativeList<Entity>(Allocator.TempJob);
        var roadsUpdateData = new NativeList<NativeList<int2>>(Allocator.TempJob);

        var prepareJob = new PrepareRoadModificationsJob
        {
            RoadsToModify = roadsToModify,
            RoadMap = roadMap,
            Ecb = ecb,
            RoadsToDelete = roadsToDelete,
            RoadsToUpdate = roadsToUpdate,
            RoadsUpdateData = roadsUpdateData
        };
        prepareJob.Schedule(state.Dependency).Complete();

        ApplyRoadModifications(ref state,roadsToDelete, roadsToUpdate, roadsUpdateData, ecb);

        roadsToModify.Dispose();
        roadsToDelete.Dispose();
        roadsToUpdate.Dispose();
        foreach (var data in roadsUpdateData) data.Dispose();
        roadsUpdateData.Dispose();
    }

    
   void CreateNewRoadsFromPoints(ref SystemState state, NativeList<int2> uniquePoints, bool isBluePrint)
    {
        Debug.Log($"CreateNewRoadsFromPoints ВХОД: isBluePrint={isBluePrint}, точек={uniquePoints.Length}");
        
        if (uniquePoints.IsEmpty) 
        {
            Debug.Log("Нет точек для создания дорог");
            return;
        }

        using var clusterStarts = new NativeList<int2>(Allocator.TempJob);
        using var allClusterPoints = new NativeList<int2>(Allocator.TempJob);
        
        var clusterJob = new UniversalClusterJob
        {
            InputPoints = uniquePoints,
            ClusterStarts = clusterStarts,
            AllClusterPoints = allClusterPoints
        };
        clusterJob.Schedule().Complete();
        
        Debug.Log($"После кластеризации: кластеров={clusterStarts.Length}, всех точек={allClusterPoints.Length}");
        Debug.Log($"Вызываем CreateRoadEntitiesFromClusters с isBluePrint={isBluePrint}");
        
        CreateRoadEntitiesFromClusters(ref state, clusterStarts, allClusterPoints, isBluePrint);
    }
        
    [BurstCompile]
    void CreateRoadEntitiesFromClusters(ref SystemState state, NativeList<int2> clusterStarts, 
        NativeList<int2> allClusterPoints, bool isBluePrint)
    {
        var entityManager = state.EntityManager;

        for (int clusterId = 0; clusterId < clusterStarts.Length; clusterId++)
        {
            int startIndex = clusterStarts[clusterId].x;
            int count = clusterStarts[clusterId].y;
            
            var clusterEntity = entityManager.CreateEntity();
            
            
            entityManager.AddComponent<RoadTag>(clusterEntity);
            entityManager.AddComponent<CreateVisualTag>(clusterEntity);
            entityManager.AddComponentData<BuildingData>(clusterEntity, new BuildingData
            {
                BuildingIDHash = roadHash,
                UniqueIDHash = clusterEntity.Index
            });
            entityManager.AddComponent<AddToMapTag>(clusterEntity);
            
            
            if (isBluePrint) 
            {
                entityManager.AddComponent<MakePhantomTag>(clusterEntity);
            }
            
            var buffer = entityManager.AddBuffer<MapPointData>(clusterEntity);
            
            for (int i = 0; i < count; i++)
            {
                var point = allClusterPoints[startIndex + i];
                buffer.Add(new MapPointData { Value = point });
                roadMap.TryAdd(point, clusterEntity);
            }
        }
}
    [BurstCompile]
    void ApplyRoadModifications(ref SystemState state,NativeList<Entity> roadsToDelete, NativeList<Entity> roadsToUpdate,
     NativeList<NativeList<int2>> roadsUpdateData, EntityCommandBuffer ecb)
    {
        foreach (var roadEntity in roadsToDelete)
        {
            ecb.DestroyEntity(roadEntity);
        }

        for (int i = 0; i < roadsToUpdate.Length; i++)
        {
            var roadEntity = roadsToUpdate[i];
            var remainingPoints = roadsUpdateData[i];

            using var clusterStarts = new NativeList<int2>(Allocator.TempJob);
            using var allClusterPoints = new NativeList<int2>(Allocator.TempJob);

            var clusterJob = new UniversalClusterJob
            {
                InputPoints = remainingPoints,
                ClusterStarts = clusterStarts,
                AllClusterPoints = allClusterPoints
            };
            clusterJob.Schedule().Complete();

            if (clusterStarts.Length == 1)
            {
                var pointBuffer = ecb.SetBuffer<MapPointData>(roadEntity);
                pointBuffer.Clear();

                int startIndex = clusterStarts[0].x;
                int count = clusterStarts[0].y;

                for (int j = 0; j < count; j++)
                {
                    var point = allClusterPoints[startIndex + j];
                    pointBuffer.Add(new MapPointData { Value = point });
                    roadMap[point] = roadEntity;
                }
                ecb.AddComponent<ModifyVisualTag>(roadEntity);
            }
            else
            {
                CreateNewRoadEntitiesFromClusters(ref state, clusterStarts, allClusterPoints, false);
                ecb.AddComponent<DestroyEntityTag>(roadEntity);
            }
        }
    }


    [BurstCompile]
    void CreateNewRoadEntitiesFromClusters(ref SystemState state, NativeList<int2> clusterStarts, NativeList<int2> allClusterPoints, bool isBluePrint)
    {
        var entityManager = state.EntityManager;

        for (int clusterId = 0; clusterId < clusterStarts.Length; clusterId++)
        {
            int startIndex = clusterStarts[clusterId].x;
            int count = clusterStarts[clusterId].y;

            var newRoadEntity = entityManager.CreateEntity();
            entityManager.AddComponent<RoadTag>(newRoadEntity);
            entityManager.AddComponent<CreateVisualTag>(newRoadEntity);

            var newBuffer = entityManager.AddBuffer<MapPointData>(newRoadEntity);
            for (int j = 0; j < count; j++)
            {
                var point = allClusterPoints[startIndex + j];
                newBuffer.Add(new MapPointData { Value = point });
                roadMap[point] = newRoadEntity;
            }
        }
    }


    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (roadMap.IsCreated)
        {
            roadMap.Dispose();
        }
    }
    
    [BurstCompile]
    public partial struct GetNewPoints : IJobEntity
    {
        public NativeParallelHashMap<int2, Entity> RoadMap;
        public NativeList<int2>.ParallelWriter UniquePointsWriter;
        public EntityCommandBuffer.ParallelWriter ecb;
        public bool IsBlueprint;

        public void Execute(Entity entity, [EntityIndexInQuery] int entityIndexInQuery, 
            in ProcessBuildingEventComponent eventC, in CreateRoadSegmentsTag seg, 
            in DynamicBuffer<MapPointData> points)
        {
            bool anyPointAdded = false;
            
            foreach (var p in points)
            {
                if (IsBlueprint || !RoadMap.ContainsKey(p.Value))
                {
                    UniquePointsWriter.AddNoResize(p.Value);
                    anyPointAdded = true;
                }
            }

            
            ecb.RemoveComponent<CreateRoadSegmentsTag>(entityIndexInQuery, entity);
            ecb.RemoveComponent<MapPointData>(entityIndexInQuery, entity);
            
            if (anyPointAdded)
            {
                ecb.AddComponent<UpdateMapTag>(entityIndexInQuery, entity);
            }
        }
    }
        
    public partial struct RemovePointsJob : IJobEntity
    {
        public NativeParallelHashMap<int2, Entity> roadMap;
        public EntityCommandBuffer Ecb;
        [NativeDisableParallelForRestriction]
        public NativeParallelMultiHashMap<Entity, int2> roadsToModify;

        public void Execute(Entity entity, in ProcessMapEventComponent eventC, in RemoveMapPointsTag tag, in DynamicBuffer<MapPointData> points)
        {
            foreach (var pointData in points)
            {
                if (roadMap.ContainsKey(pointData.Value))
                {
                    var roadEntity = roadMap[pointData.Value];
                    roadsToModify.Add(roadEntity, pointData.Value);
                }
            }
            Ecb.RemoveComponent<RemoveMapPointsTag>(entity);
            Ecb.RemoveComponent<MapPointData>(entity);
        }
    }
    
   [BurstCompile]
    public struct PrepareRoadModificationsJob : IJob
    {
        public NativeParallelMultiHashMap<Entity, int2> RoadsToModify;
        public NativeParallelHashMap<int2, Entity> RoadMap;
        public EntityCommandBuffer Ecb;
        public NativeList<Entity> RoadsToDelete;
        public NativeList<Entity> RoadsToUpdate;
        public NativeList<NativeList<int2>> RoadsUpdateData;

        public void Execute()
        {
            var uniqueRoads = new NativeHashSet<Entity>(RoadsToModify.Count(), Allocator.Temp);

            foreach (var pair in RoadsToModify)
            {
                uniqueRoads.Add(pair.Key);
            }

            foreach (var roadEntity in uniqueRoads)
            {
                
                var pointBuffer = Ecb.SetBuffer<MapPointData>(roadEntity);
                var pointsToRemove = new NativeHashSet<int2>(10, Allocator.Temp);
                
                foreach (var point in RoadsToModify.GetValuesForKey(roadEntity))
                {
                    pointsToRemove.Add(point);
                }

                
                var remainingPoints = new NativeList<int2>(math.max(pointBuffer.Length, 1), Allocator.Temp);
                
                foreach (var pointData in pointBuffer)
                {
                    if (!pointsToRemove.Contains(pointData.Value))
                    {
                        remainingPoints.Add(pointData.Value);
                    }
                }

                
                foreach (var pointData in pointBuffer)
                {
                    if (pointsToRemove.Contains(pointData.Value))
                    {
                        RoadMap.Remove(pointData.Value);
                    }
                }

                if (remainingPoints.Length == 0)
                {
                    RoadsToDelete.Add(roadEntity);
                }
                else
                {
                    RoadsToUpdate.Add(roadEntity);
                    
                    if (RoadsUpdateData.Capacity <= RoadsUpdateData.Length)
                    {
                        RoadsUpdateData.Capacity = math.max(RoadsUpdateData.Capacity * 2, 4);
                    }
                    RoadsUpdateData.Add(remainingPoints);
                }

                pointsToRemove.Dispose();
            }

            uniqueRoads.Dispose();
        }
    }
    [BurstCompile]
    public partial struct AssignClusterIdsToRoadsJob : IJob
    {
        [ReadOnly] public NativeList<int2> ClusterStarts;
        [ReadOnly] public NativeList<int2> AllClusterPoints;
        [ReadOnly] public NativeParallelHashMap<int2, Entity> RoadMap;
        public EntityCommandBuffer.ParallelWriter Ecb;

        public void Execute()
        {
            for (int clusterId = 0; clusterId < ClusterStarts.Length; clusterId++)
            {
                int startIndex = ClusterStarts[clusterId].x;
                int count = ClusterStarts[clusterId].y;
                
                for (int i = 0; i < count; i++)
                {
                    var point = AllClusterPoints[startIndex + i];
                    if (RoadMap.ContainsKey(point))
                    {
                        var roadEntity = RoadMap[point];
                        Ecb.AddComponent(0, roadEntity, new ClusterId { Value = clusterId });
                    }
                }
            }
        }
    }

    [BurstCompile]
    public partial struct AssignClusterIdsToBuildingsJob : IJob
    {
        [ReadOnly] public NativeList<int2> ClusterStarts;
        [ReadOnly] public NativeList<int2> AllClusterPoints;
        [ReadOnly] public NativeParallelHashMap<int2, Entity> BuildingMap;
        [ReadOnly] public NativeParallelHashMap<int2, Entity> RoadMap;
        [ReadOnly] public NativeParallelHashMap<Entity, byte> PhantomFlags;
        public EntityCommandBuffer.ParallelWriter Ecb;

        public void Execute()
        {
            for (int clusterId = 0; clusterId < ClusterStarts.Length; clusterId++)
            {
                int startIndex = ClusterStarts[clusterId].x;
                int count = ClusterStarts[clusterId].y;
                
                for (int i = 0; i < count; i++)
                {
                    var roadPoint = AllClusterPoints[startIndex + i];
                    
                    for (int x = -1; x <= 1; x++)
                    {
                        for (int y = -1; y <= 1; y++)
                        {
                            var checkPoint = roadPoint + new int2(x, y);
                            if (BuildingMap.ContainsKey(checkPoint))
                            {
                                var buildingEntity = BuildingMap[checkPoint];
                                if (buildingEntity != Entity.Null)
                                {
                                    
                                    Ecb.AddComponent(0, buildingEntity, new ClusterId { 
                                        Value = clusterId,
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct UniversalClusterJob : IJob
    {
        [ReadOnly] public NativeList<int2> InputPoints;
        public NativeList<int2> ClusterStarts; 
        public NativeList<int2> AllClusterPoints; 

        public void Execute()
        {
            if (InputPoints.IsEmpty) return;

            var pointSet = new NativeHashSet<int2>(InputPoints.Length, Allocator.Temp);
            var visited = new NativeHashSet<int2>(InputPoints.Length, Allocator.Temp);
            var queue = new NativeQueue<int2>(Allocator.Temp);

            foreach (var point in InputPoints)
                pointSet.Add(point);

            foreach (var point in InputPoints)
            {
                if (visited.Contains(point)) continue;

                int startIndex = AllClusterPoints.Length;
                
                queue.Enqueue(point);
                visited.Add(point);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    AllClusterPoints.Add(current);

                    CheckNeighbor(current + new int2(1, 0), pointSet, visited, queue);
                    CheckNeighbor(current + new int2(-1, 0), pointSet, visited, queue);
                    CheckNeighbor(current + new int2(0, 1), pointSet, visited, queue);
                    CheckNeighbor(current + new int2(0, -1), pointSet, visited, queue);
                }

                ClusterStarts.Add(new int2(startIndex, AllClusterPoints.Length - startIndex));
            }

            pointSet.Dispose();
            visited.Dispose();
            queue.Dispose();
        }

        void CheckNeighbor(int2 neighbor, NativeHashSet<int2> pointSet,
            NativeHashSet<int2> visited, NativeQueue<int2> queue)
        {
            if (pointSet.Contains(neighbor) && !visited.Contains(neighbor))
            {
                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }
    }
}

[BurstCompile]
public static class NativeCollectionExtensions
{
    [BurstCompile]
    public static int GetCount<TKey, TValue>(this NativeParallelHashMap<TKey, TValue> map)
        where TKey : unmanaged, System.IEquatable<TKey>
        where TValue : unmanaged
    {
        int count = 0;
        foreach (var pair in map)
        {
            count++;
        }
        return count;
    }
}