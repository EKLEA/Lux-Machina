using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]

[BurstCompile]

[UpdateAfter(typeof(DestroyBuildingsSystem))]
public partial struct  MarkBuildingOnMapSystem: ISystem
{
    EntityQuery _markRoad;
    EntityQuery _markBuilding;
    EntityQuery _mapUpdate;
    void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BuildingMap>();
        _markRoad= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<RoadTypeBuildingTag,BuildingData,BuildingTag,MapPoint,MarkOnMap>()
            .Build(ref state);
        _markBuilding= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BuildingPosData,BuildingData,MarkOnMap>()
            .WithNone<RoadTypeBuildingTag,MapPoint>()
            .Build(ref state);
        _mapUpdate= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BuildingMap,EntitiesDictionary,UpdateMapTag>()
            .Build(ref state);
    }
    void OnUpdate(ref SystemState state)
    {
        bool runBuilding = !_markBuilding.IsEmpty;
        bool runRoad = !_markRoad.IsEmpty;
        bool runUpdate = !_mapUpdate.IsEmpty;

        // Если делать нечего - выходим сразу
        if (!runBuilding && !runRoad && !runUpdate) return;

        // 2. Получаем данные
        var updateMapLookup = SystemAPI.GetComponentLookup<UpdateMapTag>(false);
        var updateClusterLookup = SystemAPI.GetComponentLookup<UpdateClustersTag>(false);
        var buildingMapRW = SystemAPI.GetSingletonRW<BuildingMap>();
        var entitiesRW = SystemAPI.GetSingletonRW<EntitiesDictionary>();
        var mapEntity = SystemAPI.GetSingletonEntity<BuildingMap>();
        if (runBuilding)
        {
            state.Dependency = new MarkBuildingJob
            {
                MapData = buildingMapRW.ValueRW,
                EntityDictionary = entitiesRW.ValueRW,
                MapEntity = mapEntity,
                UpdateMapTagLookup = updateMapLookup,
                UpdateClusterTagLookup = updateClusterLookup,
            }.Schedule(state.Dependency);
        }
        if (runRoad)
        {
            updateMapLookup = SystemAPI.GetComponentLookup<UpdateMapTag>(false);
            updateClusterLookup = SystemAPI.GetComponentLookup<UpdateClustersTag>(false);

            state.Dependency = new MarkRoadJob
            {
                MapData = buildingMapRW.ValueRW,
                MapEntity = mapEntity,
                EntityDictionary = entitiesRW.ValueRW,
                UpdateMapTagLookup = updateMapLookup,
                UpdateClusterTagLookup = updateClusterLookup,
            }.Schedule(state.Dependency);
        }
        if (runUpdate)
        {
            updateMapLookup = SystemAPI.GetComponentLookup<UpdateMapTag>(false);

            state.Dependency = new ResizeMapJob
            {
                CellMapEntites = buildingMapRW.ValueRW.CellMapEntites,
                CellMapBuildingsIDs = buildingMapRW.ValueRW.CellMapBuildingsIDs,
                CellEntityMultiMap = buildingMapRW.ValueRW.CellEntityMultiMap,
                Entities = entitiesRW.ValueRW.Entities,
                MapEntity = mapEntity,
                UpdateMapTagLookup = updateMapLookup,
            }.Schedule(state.Dependency);
        }
    }
    void OnDestroy(ref SystemState state)
    {
        
        Entity mapEntity = SystemAPI.GetSingletonEntity<BuildingMap>();

        var buildingMap= state.EntityManager.GetComponentData<BuildingMap>(mapEntity);

        if(buildingMap.CellEntityMultiMap.IsCreated)buildingMap.Dispose();
        var entDic=state.EntityManager.GetComponentData<EntitiesDictionary>(mapEntity);
        if(entDic.Entities.IsCreated)entDic.Dispose();
        var clusterMap=state.EntityManager.GetComponentData<ClusterMap>(mapEntity);
        if(clusterMap.UniqueClusterIDs.IsCreated)clusterMap.Dispose();
        var productionTable=state.EntityManager.GetComponentData<ProductionTable>(mapEntity);
        if(productionTable.produced.IsCreated)productionTable.Dispose();
        state.EntityManager.DestroyEntity(mapEntity);
    
        Entity configEntity = SystemAPI.GetSingletonEntity<BuildingConfigReference>();
        if (state.EntityManager.Exists(configEntity))
        {
            var buildingConfigs = state.EntityManager.GetComponentData<BuildingConfigReference>(configEntity);
            var recipeConfigs = state.EntityManager.GetComponentData<RecipeConfigRefernce>(configEntity);

            buildingConfigs.Dispose();
            if (recipeConfigs.RecipesConfig.IsCreated) 
                recipeConfigs.RecipesConfig.Dispose();

            state.EntityManager.DestroyEntity(configEntity);
        }
    }
    [BurstCompile]
    public partial struct MarkBuildingJob : IJobEntity
    {
        public BuildingMap MapData; 
        public EntitiesDictionary EntityDictionary; 
        public Entity MapEntity;
        public ComponentLookup<UpdateMapTag> UpdateMapTagLookup;
        public ComponentLookup<UpdateClustersTag> UpdateClusterTagLookup;

        public void Execute(Entity entity,in BuildingData buildingData, in BuildingPosData buildingPosData,EnabledRefRW<MarkOnMap> markOnMap)
        {
            for (int x = buildingPosData.LeftCornerPos.x; x < buildingPosData.LeftCornerPos.x + buildingPosData.size.x; x++)
            {
                 for (int y = buildingPosData.LeftCornerPos.y; y < buildingPosData.LeftCornerPos.y + buildingPosData.size.y; y++)
                {
                    var cell = new int2(x, y);
                    MapData.CellMapBuildingsIDs.TryAdd(cell, buildingData.BuildingIDHash);
                    MapData.CellMapEntites.TryAdd(cell, entity);
                    MapData.CellEntityMultiMap.Add(entity, cell);
                }
            }
            EntityDictionary.Entities.TryAdd(buildingData.BuildingUniqueID,entity);
            markOnMap.ValueRW=false;
            UpdateMapTagLookup.SetComponentEnabled(MapEntity, true);
            UpdateClusterTagLookup.SetComponentEnabled(MapEntity, true);
        }
    }
    [BurstCompile]
    public partial struct MarkRoadJob : IJobEntity
    {
        public BuildingMap MapData; 
        public Entity MapEntity;

        public EntitiesDictionary EntityDictionary;         
        public ComponentLookup<UpdateMapTag> UpdateMapTagLookup;
        public ComponentLookup<UpdateClustersTag> UpdateClusterTagLookup;
        public void Execute(Entity entity,in BuildingData buildingData, in DynamicBuffer<MapPoint> mapPoints,EnabledRefRW<MarkOnMap> markOnMap )
        {
            foreach(var p in mapPoints)
            {
                MapData.CellMapBuildingsIDs.TryAdd(p.pos, buildingData.BuildingIDHash);
                MapData.CellMapEntites.TryAdd(p.pos, entity); 
                 MapData.CellEntityMultiMap.Add(entity, p.pos);
            }
            
            markOnMap.ValueRW=false;
            UpdateMapTagLookup.SetComponentEnabled(MapEntity, true);
            
            EntityDictionary.Entities.TryAdd(buildingData.BuildingUniqueID,entity);
            UpdateClusterTagLookup.SetComponentEnabled(MapEntity, true);
        }
    }
    
    [BurstCompile]
    public partial struct ResizeMapJob : IJob
    {
        public NativeParallelHashMap<int2, int> CellMapBuildingsIDs; 
        public NativeParallelHashMap<int2, Entity> CellMapEntites;
        public NativeParallelMultiHashMap<Entity, int2> CellEntityMultiMap;
        public NativeParallelHashMap<int, Entity> Entities;
        public Entity MapEntity;
        public ComponentLookup<UpdateMapTag> UpdateMapTagLookup;
        public void Execute(
                    
        )
        {
            if (CellEntityMultiMap.Count() > CellEntityMultiMap.Capacity * 0.9f)
            {
                 CellMapEntites.Capacity = CellMapEntites.Capacity * 2;
                CellMapBuildingsIDs.Capacity = CellMapBuildingsIDs.Capacity * 2;
                CellEntityMultiMap.Capacity = CellEntityMultiMap.Capacity * 2;
            }
            if (Entities.Count() > Entities.Capacity * 0.9f)
            {
                Entities.Capacity = Entities.Capacity * 2;
            }
            UpdateMapTagLookup.SetComponentEnabled(MapEntity,false);
        }
    }
}