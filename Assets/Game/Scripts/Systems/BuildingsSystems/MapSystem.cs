using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PathfindingSystem))]
[UpdateBefore(typeof(RoadSystem))]
public partial struct MapSystem : ISystem
{
    EntityQuery _addBuildingPointsToMap;
    EntityQuery _removeBuildingPointsfromMap;
    Entity BuildingMapEntity;
    bool _isInitialized;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        BuildingMapEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(
            BuildingMapEntity,
            new BuildingMap
            {
                CellMapBuildings = new NativeParallelHashMap<int2, int>(1000, Allocator.Persistent),
                CellMapIDs = new NativeParallelHashMap<int2, int>(1000, Allocator.Persistent),
                CellEntity = new NativeParallelHashMap<int2, Entity>(1000, Allocator.Persistent),
            }
        );

        _addBuildingPointsToMap = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BuildingData, AddToMapTag>()
            .WithAll<MapPointData>()
            .Build(ref state);

        _removeBuildingPointsfromMap = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ProcessMapEventComponent, RemoveMapPointsTag>()
            .WithAll<MapPointData>()
            .Build(ref state);

        _isInitialized = true;
    }

    [BurstCompile]
    void ResizeMap(ref SystemState state, int newCapacity)
    {
        var mapData = state.EntityManager.GetComponentData<BuildingMap>(BuildingMapEntity);

        if (mapData.CellMapBuildings.Capacity >= newCapacity)
            return;

        var newCellBuildMap = new NativeParallelHashMap<int2, int>(
            newCapacity,
            Allocator.Persistent
        );
        var newCellIDMap = new NativeParallelHashMap<int2, int>(newCapacity, Allocator.Persistent);
        var newCellEntity = new NativeParallelHashMap<int2, Entity>(
            newCapacity,
            Allocator.Persistent
        );

        foreach (var pair in mapData.CellMapBuildings)
            newCellBuildMap.TryAdd(pair.Key, pair.Value);
        foreach (var pair in mapData.CellMapIDs)
            newCellIDMap.TryAdd(pair.Key, pair.Value);
        foreach (var pair in mapData.CellEntity)
            newCellEntity.TryAdd(pair.Key, pair.Value);

        var oldBuildings = mapData.CellMapBuildings;
        var oldIDs = mapData.CellMapIDs;
        var oldEntities = mapData.CellEntity;

        var newBuildingMap = new BuildingMap
        {
            CellMapBuildings = newCellBuildMap,
            CellMapIDs = newCellIDMap,
            CellEntity = newCellEntity,
        };

        state.EntityManager.SetComponentData(BuildingMapEntity, newBuildingMap);

        if (oldBuildings.IsCreated)
            oldBuildings.Dispose();
        if (oldIDs.IsCreated)
            oldIDs.Dispose();
        if (oldEntities.IsCreated)
            oldEntities.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!_isInitialized)
            return;

        var buildingMapRW = SystemAPI.GetSingletonRW<BuildingMap>();

        if (
            buildingMapRW.ValueRO.CellMapBuildings.GetCount()
            > buildingMapRW.ValueRO.CellMapBuildings.Capacity * 0.8f
        )
        {
            ResizeMap(ref state, buildingMapRW.ValueRO.CellMapBuildings.Capacity * 2);
        }

        bool isUpdated = false;
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        if (!_addBuildingPointsToMap.IsEmptyIgnoreFilter)
        {
            var AddPointsToMapJob = new AddPointsToMapJob
            {
                buildingMap = buildingMapRW.ValueRW.CellMapBuildings,
                idMap = buildingMapRW.ValueRW.CellMapIDs,
                ecb = ecb.AsParallelWriter(),
            };

            state.Dependency = AddPointsToMapJob.Schedule(
                _addBuildingPointsToMap,
                state.Dependency
            );
            isUpdated = true;
        }

        if (!_removeBuildingPointsfromMap.IsEmptyIgnoreFilter)
        {
            var removeJob = new RemovePointsFromMapJob
            {
                buildingMap = buildingMapRW.ValueRW.CellMapBuildings,
                idMap = buildingMapRW.ValueRW.CellMapIDs,
                ecb = ecb.AsParallelWriter(),
            };

            state.Dependency = removeJob.Schedule(_removeBuildingPointsfromMap, state.Dependency);
            isUpdated = true;
        }

        state.Dependency.Complete();

        if (isUpdated)
        {
            ecb.Playback(state.EntityManager);
            state.EntityManager.AddComponentData(BuildingMapEntity, new UpdateMapTag());
        }

        ecb.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (!_isInitialized)
            return;

        if (state.EntityManager.Exists(BuildingMapEntity))
        {
            var mapData = state.EntityManager.GetComponentData<BuildingMap>(BuildingMapEntity);

            if (mapData.CellMapBuildings.IsCreated)
                mapData.CellMapBuildings.Dispose();

            if (mapData.CellMapIDs.IsCreated)
                mapData.CellMapIDs.Dispose();

            if (mapData.CellEntity.IsCreated)
                mapData.CellEntity.Dispose();
        }

        _isInitialized = false;
    }

    [BurstCompile]
    public partial struct AddPointsToMapJob : IJobEntity
    {
        public NativeParallelHashMap<int2, int> buildingMap;
        public NativeParallelHashMap<int2, int> idMap;
        public EntityCommandBuffer.ParallelWriter ecb;

        public void Execute(
            Entity entity,
            in BuildingData buildingData,
            in AddToMapTag tag,
            in DynamicBuffer<MapPointData> points
        )
        {
            bool addedPoint = false;
            foreach (var point in points)
            {
                if (!buildingMap.ContainsKey(point.Value) && !idMap.ContainsKey(point.Value))
                {
                    if (buildingMap.TryAdd(point.Value, buildingData.BuildingIDHash))
                    {
                        idMap.TryAdd(point.Value, buildingData.UniqueIDHash);
                        addedPoint = true;
                    }
                }
            }
            if (addedPoint)
            {
                ecb.RemoveComponent<AddToMapTag>(0, entity);
                ecb.AddComponent<UpdateMapTag>(0, entity);
            }
        }
    }

    [BurstCompile]
    public partial struct RemovePointsFromMapJob : IJobEntity
    {
        public NativeParallelHashMap<int2, int> buildingMap;
        public NativeParallelHashMap<int2, int> idMap;
        public EntityCommandBuffer.ParallelWriter ecb;

        public void Execute(
            Entity entity,
            in ProcessMapEventComponent eventC,
            in RemoveMapPointsTag tag,
            in DynamicBuffer<MapPointData> points
        )
        {
            bool removedPoints = false;
            foreach (var point in points)
            {
                if (buildingMap.ContainsKey(point.Value))
                {
                    buildingMap.Remove(point.Value);
                    removedPoints = true;
                }
                if (idMap.ContainsKey(point.Value))
                {
                    idMap.Remove(point.Value);
                    removedPoints = true;
                }
            }
            if (removedPoints)
                ecb.AddComponent<UpdateMapTag>(0, entity);
        }
    }
}
