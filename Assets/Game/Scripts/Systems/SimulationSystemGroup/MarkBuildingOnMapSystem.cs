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
        var TurretStatsLookup = SystemAPI.GetComponentLookup<TurretStats>(false);
        var buildingMapRW = SystemAPI.GetSingletonRW<BuildingMap>();
        var energyMapRW = SystemAPI.GetSingletonRW<EnergyMap>();
        var entitiesRW = SystemAPI.GetSingletonRW<EntitiesDictionary>();
        var turretMapRW = SystemAPI.GetSingletonRW<TurretGrid>();
        var resourceMapRW = SystemAPI.GetSingletonRW<ResourceMap>();
        var mapEntity = SystemAPI.GetSingletonEntity<BuildingMap>();
        var config = SystemAPI.GetSingleton<BuildingConfigReference>();
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        if (runBuilding)
        {
            state.Dependency = new MarkBuildingJob
            {
                MapData = buildingMapRW.ValueRW,
                EnergyMap=energyMapRW.ValueRW,
                EntityDictionary = entitiesRW.ValueRW,
                TurretGrid=turretMapRW.ValueRW,
                MapEntity = mapEntity,
                ResourceMap=resourceMapRW.ValueRO,
                ResourcesLinkLookup=resourcesLinkLookup,
                UpdateMapTagLookup = updateMapLookup,
                UpdateClusterTagLookup = updateClusterLookup,
                EnergyBuildingDataLookup=energyBuildingDataLookup,
                UpdateConnectStatusLookup=updateConnectStatusLookup,
                ConnectToEnegyEntitiesLookup=connectToEnegyEntitiesLookup,
                TurretStatsLookup=TurretStatsLookup
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
            var SpawnMobsDataLookup = SystemAPI.GetComponentLookup<SpawnMobsData>(false);
            var RoadPointHealthDataLookup = SystemAPI.GetBufferLookup<RoadPointHealthData>(false);
             
            var weightJob = new CalculateDestructionWeightsJob {
                BuildingsMap = buildingMapRW.ValueRW,
                HealthDataLookup=HealthDataLookup,
                RoadPointHealthDataLookup=RoadPointHealthDataLookup,
                buildingConfigReference=config,
                SpawnManagerEntity=mapEntity,
                SpawnMobsDataLookup=SpawnMobsDataLookup
                
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
        var TurretGrid=state.EntityManager.GetComponentData<TurretGrid>(mapEntity);
        if(TurretGrid.TurretGridClaim.IsCreated)TurretGrid.Dispose();
        
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
                recipeConfigs.Dispose();
            if(itemsConfigs.ItemsConfigs.IsCreated)
                itemsConfigs.Dispose();
            if(enemyBaseConfig.EnemyBaseConfigs.IsCreated)
                enemyBaseConfig.Dispose();
            
            state.EntityManager.DestroyEntity(configEntity);
        }
    }
    [BurstCompile]
    public partial struct MarkBuildingJob : IJobEntity
    {
        public BuildingMap MapData; 
        public EnergyMap EnergyMap; 
        public ResourceMap ResourceMap; 
        public TurretGrid TurretGrid;
        public EntitiesDictionary EntityDictionary; 
        public Entity MapEntity;
        public ComponentLookup<UpdateMapTag> UpdateMapTagLookup;
        public ComponentLookup<UpdateClustersTag> UpdateClusterTagLookup;
        public ComponentLookup<EnergyBuildingData> EnergyBuildingDataLookup;
        public ComponentLookup<UpdateConnectStatus> UpdateConnectStatusLookup;
        public ComponentLookup<ConnectToEnegyEntities> ConnectToEnegyEntitiesLookup;
        public ComponentLookup<ResourcesLink> ResourcesLinkLookup;
        public ComponentLookup<TurretStats> TurretStatsLookup;

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
            if (TurretStatsLookup.HasComponent(entity))
            {
                var stats= TurretStatsLookup[entity];
                float2 forward = buildingPosData.Rotation switch
                {
                    1 => new float2(0, 1),  
                    2 => new float2(1, 0), 
                    3 => new float2(0, -1), 
                    4 => new float2(-1, 0), 
                    _ => new float2(0, 1)
                };

                float cosHalfAngle = math.cos(math.radians(stats.Angle * 0.5f));
                int radiusInCells = (int)math.ceil(stats.AttackRange / TurretGrid.CellSize);
                float radiusSq = stats.AttackRange * stats.AttackRange;

                for (int x = -radiusInCells; x <= radiusInCells; x++)
                {
                    for (int y = -radiusInCells; y <= radiusInCells; y++)
                    {
                        // Если это сама клетка турели, всегда помечаем
                        if (x == 0 && y == 0) 
                        {
                            int2 cell = new int2((int)buildingPosData.center.x, (int)buildingPosData.center.y);
                            TurretGrid.TurretGridClaim.Add(cell, buildingData.BuildingUniqueID);
                            continue;
                        }

                        float2 relativePos = new float2(x, y) * TurretGrid.CellSize;
                        float distSq = math.lengthsq(relativePos);

                        if (distSq <= radiusSq)
                        {
                            // Используем math.dot для проверки угла без лишних тригонометрических функций
                            // Нормализуем вручную, чтобы избежать проблем с нулем
                            float dist = math.sqrt(distSq);
                            float2 directionToCell = relativePos / dist; 
                            
                            float dot = math.dot(forward, directionToCell);

                            if (dot >= cosHalfAngle)
                            {
                                int2 cell = new int2((int)buildingPosData.center.x + x, (int)buildingPosData.center.y + y);
                                TurretGrid.TurretGridClaim.Add(cell, buildingData.BuildingUniqueID);
                            }
                        }
                    }
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
        public  BuildingMap BuildingsMap;
        public ComponentLookup<SpawnMobsData> SpawnMobsDataLookup;
        public Entity SpawnManagerEntity;        
        [ReadOnly] public BuildingConfigReference buildingConfigReference;
        [ReadOnly] public ComponentLookup<HealthData> HealthDataLookup;
        [ReadOnly] public BufferLookup<RoadPointHealthData> RoadPointHealthDataLookup;
        public EntityCommandBuffer ECB;

        public void Execute()
        {
            var spawnMobsData = SpawnMobsDataLookup[SpawnManagerEntity];
            spawnMobsData.totalWeights=0;
            BuildingsMap.CellWeights.Clear();
            var queue = new NativeQueue<int2>(Allocator.Temp);

            foreach (var building in BuildingsMap.CellMapBuildingsIDs)
            {
                int2 bPos = building.Key;
                int buildingID = building.Value;

                float startWeight = 0f;
                if (buildingConfigReference.BuildingsBaseConfigs.Value.TryGetConfig(buildingID, out var config))
                {
                    
                    startWeight = GetPriorityScore(config.buildingType, config.typeOfLogic);
                }
                var en=BuildingsMap.CellMapEntites[bPos];
                float procent=1f;
                if(RoadPointHealthDataLookup.HasBuffer(en))
                {
                    foreach(var b in RoadPointHealthDataLookup[en])
                    {
                        if (bPos.x == b.pos.x && bPos.y == b.pos.y)
                        {
                            procent = (float)b.CurrHealth / b.MaxHealth;
                            break;
                        }
                    }
                }
                else
                {
                    procent= (float)(HealthDataLookup[en].CurrHealth)/HealthDataLookup[en].MaxHealth;
                }
                float res=startWeight*procent;
                spawnMobsData.totalWeights+=(21f-res);
                BuildingsMap.CellWeights[bPos] =  res;
                queue.Enqueue(bPos);
            }

            int maxSearchDist = 20; 

            while (queue.TryDequeue(out int2 curr))
            {
                float currWeight = BuildingsMap.CellWeights[curr];

                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                        {
                            if (x == 0 && y == 0) continue;
                            int2 neighbor = curr + new int2(x, y);

                            if (BuildingsMap.CellMapBuildingsIDs.ContainsKey(neighbor)) continue;

                            float distMod = (x != 0 && y != 0) ? 1.41f : 1.0f;
                            
                            float stepCost = 1.0f * distMod; 
                            float newWeight = currWeight + stepCost;

                            if (newWeight > maxSearchDist) continue;

                            if (!BuildingsMap.CellWeights.TryGetValue(neighbor, out float oldWeight) || newWeight < oldWeight)
                            {
                                BuildingsMap.CellWeights[neighbor] = newWeight;
                                queue.Enqueue(neighbor);
                            }
                        }
                }
            }
            SpawnMobsDataLookup[SpawnManagerEntity] = spawnMobsData;
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