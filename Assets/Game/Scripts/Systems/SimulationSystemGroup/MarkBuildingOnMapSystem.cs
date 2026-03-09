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

        if (!runBuilding && !runRoad && !runUpdate) return;

        var updateMapLookup = SystemAPI.GetComponentLookup<UpdateMapTag>(false);
        var updateClusterLookup = SystemAPI.GetComponentLookup<UpdateClustersTag>(false);
        var energyBuildingDataLookup = SystemAPI.GetComponentLookup<EnergyBuildingData>(false);
        var connectToEnegyEntitiesLookup = SystemAPI.GetComponentLookup<ConnectToEnegyEntities>(false);
        var updateConnectStatusLookup = SystemAPI.GetComponentLookup<UpdateConnectStatus>(false);
        var resourcesLinkLookup = SystemAPI.GetComponentLookup<ResourcesLink>(false);
        var buildingMapRW = SystemAPI.GetSingletonRW<BuildingMap>();
        var energyMapRW = SystemAPI.GetSingletonRW<EnergyMap>();
        var entitiesRW = SystemAPI.GetSingletonRW<EntitiesDictionary>();
        var resourceMapRW = SystemAPI.GetSingletonRW<ResourceMap>();
        var mapEntity = SystemAPI.GetSingletonEntity<BuildingMap>();
        var config = SystemAPI.GetSingleton<BuildingConfigReference>();
        if (runBuilding)
        {
            state.Dependency = new MarkBuildingJob
            {
                MapData = buildingMapRW.ValueRW,
                EnergyMap=energyMapRW.ValueRW,
                EntityDictionary = entitiesRW.ValueRW,
                MapEntity = mapEntity,
                ResourceMap=resourceMapRW.ValueRO,
                ResourcesLinkLookup=resourcesLinkLookup,
                UpdateMapTagLookup = updateMapLookup,
                UpdateClusterTagLookup = updateClusterLookup,
                EnergyBuildingDataLookup=energyBuildingDataLookup,
                UpdateConnectStatusLookup=updateConnectStatusLookup,
                ConnectToEnegyEntitiesLookup=connectToEnegyEntitiesLookup
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
            var HealthDataLookup = SystemAPI.GetComponentLookup<HealthData>(false);
             
            var weightJob = new CalculateDestructionWeightsJob {
                Buildings = buildingMapRW.ValueRO.CellMapBuildingsIDs,
                //BuildingsEntities = buildingMapRW.ValueRO.CellMapEntites,
                //HealthDataLookup=HealthDataLookup,
                buildingConfigReference=config,
                //TargetPos=buildingMapRW.ValueRO.CorePos,
                Weights = buildingMapRW.ValueRW.CellWeights
            };
            state.Dependency = weightJob.Schedule(state.Dependency);


            var flowJob = new GenerateFlowDirectionsJob
            {
                Weights = buildingMapRW.ValueRO.CellWeights,
                BuildingIDs = buildingMapRW.ValueRO.CellMapBuildingsIDs,
                Directions = buildingMapRW.ValueRW.CellDirections
            };

            state.Dependency = flowJob.Schedule(state.Dependency);


            state.Dependency = new ResizeMapJob
            {
                CellMapEntites = buildingMapRW.ValueRW.CellMapEntites,
                CellMapBuildingsIDs = buildingMapRW.ValueRW.CellMapBuildingsIDs,
                CellEntityMultiMap = buildingMapRW.ValueRW.CellEntityMultiMap,
                IsBluePrintOrDemolitionPoints = buildingMapRW.ValueRW.IsBluePrintOrDemolitionPoints,
                CellWeights = buildingMapRW.ValueRW.CellWeights,
                CellDirections = buildingMapRW.ValueRW.CellDirections,
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
        var energyMap=state.EntityManager.GetComponentData<EnergyMap>(mapEntity);
        if(energyMap.EnergyLinks.IsCreated)energyMap.Dispose();
        var resourceMap=state.EntityManager.GetComponentData<ResourceMap>(mapEntity);
        if(resourceMap.ResouecesMap.IsCreated)resourceMap.Dispose();
        
        state.EntityManager.DestroyEntity(mapEntity);
    
    
        Entity configEntity = SystemAPI.GetSingletonEntity<BuildingConfigReference>();
        if (state.EntityManager.Exists(configEntity))
        {
            var buildingConfigs = state.EntityManager.GetComponentData<BuildingConfigReference>(configEntity);
            var recipeConfigs = state.EntityManager.GetComponentData<RecipeConfigRefernce>(configEntity);
            var itemsConfigs = state.EntityManager.GetComponentData<ItemsConfigReference>(configEntity);
            var enemyBaseConfig = state.EntityManager.GetComponentData<EnemyBaseConfigRefence>(configEntity);

            buildingConfigs.Dispose();
            if (recipeConfigs.RecipesConfig.IsCreated) 
                recipeConfigs.RecipesConfig.Dispose();
            if(itemsConfigs.ItemsConfigs.IsCreated)
                itemsConfigs.ItemsConfigs.Dispose();
            if(enemyBaseConfig.EnemyBaseConfigs.IsCreated)
                enemyBaseConfig.EnemyBaseConfigs.Dispose();

            state.EntityManager.DestroyEntity(configEntity);
        }
    }
    [BurstCompile]
    public partial struct MarkBuildingJob : IJobEntity
    {
        public BuildingMap MapData; 
        public EnergyMap EnergyMap; 
        public ResourceMap ResourceMap; 
        public EntitiesDictionary EntityDictionary; 
        public Entity MapEntity;
        public ComponentLookup<UpdateMapTag> UpdateMapTagLookup;
        public ComponentLookup<UpdateClustersTag> UpdateClusterTagLookup;
        public ComponentLookup<EnergyBuildingData> EnergyBuildingDataLookup;
        public ComponentLookup<UpdateConnectStatus> UpdateConnectStatusLookup;
        public ComponentLookup<ConnectToEnegyEntities> ConnectToEnegyEntitiesLookup;
        public ComponentLookup<ResourcesLink> ResourcesLinkLookup;

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

            if(ConnectToEnegyEntitiesLookup.HasComponent(entity))
            {   
                NativeHashSet<int> entitiesHasSet=new(buildingPosData.size.x*buildingPosData.size.y,Allocator.Temp);
               
                for (int x = buildingPosData.LeftCornerPos.x; x < buildingPosData.LeftCornerPos.x + buildingPosData.size.x; x++)
                {
                    for (int y = buildingPosData.LeftCornerPos.y; y < buildingPosData.LeftCornerPos.y + buildingPosData.size.y; y++)
                    {
                        var cell = new int2(x, y);
                        
                        if (EnergyMap.CellToEnergyBuildingMap.ContainsKey(cell))
                        {
                            var values=EnergyMap.CellToEnergyBuildingMap.GetValuesForKey(cell);
                            foreach(var v in values) entitiesHasSet.Add(v);
                        }
                    }
                }
                FixedList128Bytes<int> ConnectToEntites=new();
                foreach(var i in entitiesHasSet)
                {
                    ConnectToEntites.Add(i);
                }
                UpdateConnectStatusLookup.SetComponentEnabled(entity,true);
                ConnectToEnegyEntitiesLookup[entity]=new ConnectToEnegyEntities{ConnectToEntites=ConnectToEntites};
            }

            EntityDictionary.Entities.TryAdd(buildingData.BuildingUniqueID,entity);
            markOnMap.ValueRW=false;

            UpdateMapTagLookup.SetComponentEnabled(MapEntity, true);
            UpdateClusterTagLookup.SetComponentEnabled(MapEntity, true);
            if (EnergyBuildingDataLookup.HasComponent(entity))
            {
                var enData = EnergyBuildingDataLookup[entity];
                int2 center = (int2)buildingPosData.center;
                float radius = enData.radius;
                int radiusSq = (int)(radius * radius);
                NativeHashSet<Entity> entitiesToPing=new(radiusSq,Allocator.Temp);
                for (int x = (int)(center.x - radius); x <= center.x + radius; x++)
                {
                    for (int y = (int)(center.y - radius); y <= center.y + radius; y++)
                    {
                        int dx = x - center.x;
                        int dy = y - center.y;
                        var pos=new int2(x, y);
                        if (dx * dx + dy * dy <= radiusSq)
                        {
                            EnergyMap.CellToEnergyBuildingMap.Add(pos, buildingData.BuildingUniqueID);
                            EnergyMap.CellToEnergyEntityBuildingMap.Add(pos, entity);
                            EnergyMap.EnergyEntityToCellBuildingMap.Add(entity,pos );
                            if(MapData.CellMapEntites.ContainsKey(pos)) entitiesToPing.Add(MapData.CellMapEntites[pos]);
                        }
                    }
                }
                foreach(var en in entitiesToPing)
                {
                    if(UpdateConnectStatusLookup.HasComponent(en)) UpdateConnectStatusLookup.SetComponentEnabled(en,true);
                }
                entitiesToPing.Dispose();
            }
            if (ResourcesLinkLookup.HasComponent(entity))
            {
                ResourcesLink resourcesLink=new();
                resourcesLink.ResourcesCells=new();
                resourcesLink.indexCell=0;
                for (int x = buildingPosData.LeftCornerPos.x; x < buildingPosData.LeftCornerPos.x + buildingPosData.size.x; x++)
                {
                    for (int y = buildingPosData.LeftCornerPos.y; y < buildingPosData.LeftCornerPos.y + buildingPosData.size.y; y++)
                    {
                        int2 point=new(x,y);
                        if (ResourceMap.ResouecesMap.ContainsKey(point))
                        {
                            resourcesLink.ResourcesCells.Add(point);
                        }
                    }
                }
                ResourcesLinkLookup[entity]=resourcesLink;
            }
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
        public NativeParallelHashMap<int2, bool> IsBluePrintOrDemolitionPoints; 
        public NativeParallelHashMap<int2, float> CellWeights;    
        public NativeParallelHashMap<int2, float2> CellDirections;
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
                IsBluePrintOrDemolitionPoints.Capacity = IsBluePrintOrDemolitionPoints.Capacity * 2;
                CellWeights.Capacity = CellWeights.Capacity * 2;
                CellDirections.Capacity = CellDirections.Capacity * 2;
            }
            if (Entities.Count() > Entities.Capacity * 0.9f)
            {
                Entities.Capacity = Entities.Capacity * 2;
            }
            UpdateMapTagLookup.SetComponentEnabled(MapEntity,false);
        }
    }
    [BurstCompile]
    public struct CalculateDestructionWeightsJob : IJob
    {
        [ReadOnly] public NativeParallelHashMap<int2, int> Buildings;
        [ReadOnly] public BuildingConfigReference buildingConfigReference;
        public NativeParallelHashMap<int2, float> Weights;

        public void Execute()
        {
            Weights.Clear();
            var queue = new NativeQueue<int2>(Allocator.Temp);

            foreach (var building in Buildings)
            {
                int2 bPos = building.Key;
                int buildingID = building.Value;

                float startWeight = 0f;
                if (buildingConfigReference.BuildingsBaseConfigs.Value.TryGetConfig(buildingID, out var config))
                {
                    // Используем твой скоринг: чем меньше число, тем притягательнее здание
                    // Вычитаем из 0, чтобы получить приоритет
                    startWeight = GetPriorityScore(config.buildingType, config.typeOfLogic);
                }

                Weights[bPos] = startWeight; 
                queue.Enqueue(bPos);
            }

            // Радиус распространения влияния зданий
            int maxSearchDist = 20; 

            while (queue.TryDequeue(out int2 curr))
            {
                float currWeight = Weights[curr];

                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                        {
                            if (x == 0 && y == 0) continue;
                            int2 neighbor = curr + new int2(x, y);

                            // Если в клетке уже здание, мы его не перезаписываем (у них приоритет от конфига)
                            if (Buildings.ContainsKey(neighbor)) continue;

                            float distMod = (x != 0 && y != 0) ? 1.41f : 1.0f;
                            
                            // Клетки пола просто добавляют стоимость расстояния
                            float stepCost = 1.0f * distMod; 
                            float newWeight = currWeight + stepCost;

                            if (newWeight > maxSearchDist) continue;

                            if (!Weights.TryGetValue(neighbor, out float oldWeight) || newWeight < oldWeight)
                            {
                                Weights[neighbor] = newWeight;
                                queue.Enqueue(neighbor);
                            }
                        }
                }
            }
        }

        private float GetPriorityScore(BuildingsTypes type, TypeOfLogic logic)
        {
            // Твои веса: Special (1) будет "тянуть" сильнее, чем Defence (20)
            switch (type)
            {
                case BuildingsTypes.Special:    return 1f;   
                case BuildingsTypes.Enegry:     return 5f;
                case BuildingsTypes.Logistic:   return 10f;
                case BuildingsTypes.Procession: return 15f;
                case BuildingsTypes.Defence:    return 20f + (logic == TypeOfLogic.WorkWithItems ? 10 : 0);  
                default:                        return 20f;
            }
        }
    }
    [BurstCompile]
    public struct GenerateFlowDirectionsJob : IJob
    {
        [ReadOnly] public NativeParallelHashMap<int2, float> Weights; 
        [ReadOnly] public NativeParallelHashMap<int2, int> BuildingIDs; 
        public NativeParallelHashMap<int2, float2> Directions;

       public void Execute()
        {
            Directions.Clear();

            foreach (var entry in Weights)
            {
                int2 curr = entry.Key;
                if (BuildingIDs.ContainsKey(curr))
                {
                    Directions.TryAdd(curr, float2.zero);
                    continue;
                }

                float currWeight = entry.Value;
                float2 aggregateDir = float2.zero;
                bool foundLower = false;

                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        if (x == 0 && y == 0) continue;
                        int2 neighbor = curr + new int2(x, y);

                        if (Weights.TryGetValue(neighbor, out float nWeight))
                        {
                            if (nWeight < currWeight)
                            {
                                float2 toNeighbor = new float2(x, y);
                                float weightDiff = currWeight - nWeight;
                                
                                aggregateDir += math.normalize(toNeighbor) * weightDiff;
                                foundLower = true;
                            }
                        }
                    }
                }

                if (foundLower && math.lengthsq(aggregateDir) > 0.001f)
                {
                    Directions.TryAdd(curr, math.normalize(aggregateDir));
                }
                else
                {
                    Directions.TryAdd(curr, float2.zero);
                }
            }
        }
    }
}