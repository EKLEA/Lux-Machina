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
    EntityQuery _markManyPoint;
    EntityQuery _markBuilding;
    EntityQuery _mapUpdate;
    void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BuildingMap>();
        _markManyPoint= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ManyPointTypeBuildingTag,BuildingData,BuildingTag,MapPoint,MarkOnMap>()
            .Build(ref state);
        _markBuilding= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BuildingPosData,BuildingData,MarkOnMap>()
            .WithNone<ManyPointTypeBuildingTag,MapPoint>()
            .Build(ref state);
        _mapUpdate= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BuildingMap,EntitiesDictionary,UpdateMapTag>()
            .Build(ref state);
    }
    void OnUpdate(ref SystemState state)
    {
        bool runBuilding = !_markBuilding.IsEmpty;
        bool runManyPoint = !_markManyPoint.IsEmpty;
        bool runUpdate = !_mapUpdate.IsEmpty;

        if (!runBuilding && !runManyPoint && !runUpdate) return;

        var updateMapLookup = SystemAPI.GetComponentLookup<UpdateMapTag>(false);
        var updateClusterLookup = SystemAPI.GetComponentLookup<UpdateClustersTag>(false);
        var energyBuildingDataLookup = SystemAPI.GetComponentLookup<EnergyBuildingData>(false);
        var connectToEnegyEntitiesLookup = SystemAPI.GetComponentLookup<ConnectToEnegyEntities>(false);
        var updateConnectStatusLookup = SystemAPI.GetComponentLookup<UpdateConnectStatus>(false);
        var resourcesLinkLookup = SystemAPI.GetBufferLookup<ResourcesInChunkLink>(false);
        var TurretStatsLookup = SystemAPI.GetComponentLookup<TurretStats>(false);
        var buildingMapRW = SystemAPI.GetSingletonRW<BuildingMap>();
        var energyMapRW = SystemAPI.GetSingletonRW<EnergyMap>();
        var entitiesRW = SystemAPI.GetSingletonRW<EntitiesDictionary>();
        var turretMapRW = SystemAPI.GetSingletonRW<TurretGrid>();
        var mapEntity = SystemAPI.GetSingletonEntity<BuildingMap>();
        var config = SystemAPI.GetSingleton<BuildingConfigReference>();
        var chunkMap = SystemAPI.GetSingletonRW<ChunkMap>();
        var worldSettings = SystemAPI.GetSingleton<WorldSettings>();
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
                worldSettings=worldSettings,
                ChunkMapData=chunkMap.ValueRO.ChunkMapData,
                ResourcesLinkLookup=resourcesLinkLookup,
                UpdateMapTagLookup = updateMapLookup,
                UpdateClusterTagLookup = updateClusterLookup,
                EnergyBuildingDataLookup=energyBuildingDataLookup,
                UpdateConnectStatusLookup=updateConnectStatusLookup,
                ConnectToEnegyEntitiesLookup=connectToEnegyEntitiesLookup,
                TurretStatsLookup=TurretStatsLookup
            }.Schedule(state.Dependency);
        }
        if (runManyPoint)
        {
            updateMapLookup = SystemAPI.GetComponentLookup<UpdateMapTag>(false);
            updateClusterLookup = SystemAPI.GetComponentLookup<UpdateClustersTag>(false);

            state.Dependency = new MarkManyPointJob
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
            var ManyPointPointHealthDataLookup = SystemAPI.GetBufferLookup<ManyPointPointHealthData>(false);
             
           var weightJob = new CalculateDestructionWeightsJob
            {
                BuildingsMap = buildingMapRW.ValueRW,
                SpawnMobsDataLookup = SpawnMobsDataLookup,
                SpawnManagerEntity = mapEntity,

                buildingConfigReference = config,
                HealthDataLookup = HealthDataLookup,
                ManyPointPointHealthDataLookup = ManyPointPointHealthDataLookup,

                ChunkMap = chunkMap.ValueRO,
                BlockLookup = SystemAPI.GetBufferLookup<BlockElement>(true),
                Settings = worldSettings,

                ECB = ecb
            };
            state.Dependency = weightJob.Schedule(state.Dependency);

            var flowJob = new GenerateFlowDirectionsJob
            {
                Weights = buildingMapRW.ValueRO.CellWeights,

                ChunkMap = chunkMap.ValueRO,
                BlockLookup = SystemAPI.GetBufferLookup<BlockElement>(true),
                Settings = worldSettings,

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
        var TurretGrid=state.EntityManager.GetComponentData<TurretGrid>(mapEntity);
        if(TurretGrid.TurretGridClaim.IsCreated)TurretGrid.Dispose();
        var ChunkMap=state.EntityManager.GetComponentData<ChunkMap>(mapEntity);
        if(ChunkMap.ChunkMapData.IsCreated)ChunkMap.Dispose();
        
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
        [ReadOnly] public NativeParallelHashMap<int2, Entity> ChunkMapData; 
        public WorldSettings worldSettings;
        public TurretGrid TurretGrid;
        public EntitiesDictionary EntityDictionary; 
        public Entity MapEntity;
        public ComponentLookup<UpdateMapTag> UpdateMapTagLookup;
        public ComponentLookup<UpdateClustersTag> UpdateClusterTagLookup;
        public ComponentLookup<EnergyBuildingData> EnergyBuildingDataLookup;
        public ComponentLookup<UpdateConnectStatus> UpdateConnectStatusLookup;
        public ComponentLookup<ConnectToEnegyEntities> ConnectToEnegyEntitiesLookup;
        public BufferLookup<ResourcesInChunkLink> ResourcesLinkLookup;
        public ComponentLookup<TurretStats> TurretStatsLookup;

        public void Execute(Entity entity,in BuildingData buildingData, in BuildingPosData buildingPosData,EnabledRefRW<MarkOnMap> markOnMap)
        {
            
            for (int x = buildingPosData.LeftCornerPos.x; x < buildingPosData.LeftCornerPos.x + buildingPosData.size.x; x++)
            {
                for (int y = buildingPosData.LeftCornerPos.y; y < buildingPosData.LeftCornerPos.y + buildingPosData.size.y; y++)
                {
                    for (int z = buildingPosData.LeftCornerPos.z; z < buildingPosData.LeftCornerPos.z + buildingPosData.size.z; z++)
                    {
                        var cell = new int3(x, y,z);
                        MapData.CellMapBuildingsIDs.TryAdd(cell, buildingData.BuildingIDHash);
                        MapData.CellMapEntites.TryAdd(cell, entity);
                        MapData.CellEntityMultiMap.Add(entity, cell);
                    }
                }
            }
            if (TurretStatsLookup.HasComponent(entity))
            {
                var stats= TurretStatsLookup[entity];
                float3 forward = buildingPosData.Rotation switch
                {
                    3 => new float3(0,0, 1),  
                    0 => new float3(1,0, 0), 
                    1 => new float3(0, 0,-1), 
                    2 => new float3(-1,0, 0), 
                    _ => new float3(0, 0,1)
                };

                float cosHalfAngle = math.cos(math.radians(stats.Angle * 0.5f));
                int radiusInCells = (int)math.ceil(stats.AttackRange / TurretGrid.CellSize);
                float radiusSq = stats.AttackRange * stats.AttackRange;

                for (int x = -radiusInCells; x <= radiusInCells; x++)
                {
                    for (int y = -radiusInCells; y <= radiusInCells; y++)
                    {
                         for (int z= -radiusInCells; z <= radiusInCells; z++)
                        {
                            if (x == 0 && y == 0&&z==0) 
                            {
                                int3 cell = new int3((int)buildingPosData.center.x, (int)buildingPosData.center.y,(int)buildingPosData.center.z);
                                TurretGrid.TurretGridClaim.Add(cell, buildingData.BuildingUniqueID);
                                continue;
                            }

                            float3 relativePos = new float3(x, y,z) * TurretGrid.CellSize;
                            float distSq = math.lengthsq(relativePos);

                            if (distSq <= radiusSq)
                            {
                                float dist = math.sqrt(distSq);
                                float3 directionToCell = relativePos / dist; 
                                
                                float dot = math.dot(forward, directionToCell);

                                if (dot >= cosHalfAngle)
                                {
                                    int3 cell = new int3((int)buildingPosData.center.x + x, (int)buildingPosData.center.y + y,(int)buildingPosData.center.z + z);
                                    TurretGrid.TurretGridClaim.Add(cell, buildingData.BuildingUniqueID);
                                }
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
                        for (int z = buildingPosData.LeftCornerPos.z; z < buildingPosData.LeftCornerPos.z + buildingPosData.size.z; z++)
                        {
                            var cell = new int3(x, y,z);
                            
                            if (EnergyMap.CellToEnergyBuildingMap.ContainsKey(cell))
                            {
                                var values=EnergyMap.CellToEnergyBuildingMap.GetValuesForKey(cell);
                                foreach(var v in values) entitiesHasSet.Add(v);
                            }
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
                int3 center = (int3)buildingPosData.center;
                float radius = enData.radius;
                int radiusSq = (int)(radius * radius);
                NativeHashSet<Entity> entitiesToPing=new(radiusSq,Allocator.Temp);
                for (int x = (int)(center.x - radius); x <= center.x + radius; x++)
                {
                    for (int y = (int)(center.y - radius); y <= center.y + radius; y++)
                    {
                        for (int z = (int)(center.z - radius); z <= center.z + radius; z++)
                        {
                            int dx = x - center.x;
                            int dy = y - center.y;
                            int dz = z - center.z;
                            var pos=new int3(x, y,z);
                            if (dx * dx + dy * dy +dz*dz<= radiusSq)
                            {
                                EnergyMap.CellToEnergyBuildingMap.Add(pos, buildingData.BuildingUniqueID);
                                EnergyMap.CellToEnergyEntityBuildingMap.Add(pos, entity);
                                EnergyMap.EnergyEntityToCellBuildingMap.Add(entity,pos );
                                if(MapData.CellMapEntites.ContainsKey(pos)) entitiesToPing.Add(MapData.CellMapEntites[pos]);
                            }
                        }
                    }
                }
                foreach(var en in entitiesToPing)
                {
                    if(UpdateConnectStatusLookup.HasComponent(en)) UpdateConnectStatusLookup.SetComponentEnabled(en,true);
                }
                entitiesToPing.Dispose();
            }
            if (ResourcesLinkLookup.HasBuffer(entity))
            {
                var resourcesBuffer = ResourcesLinkLookup[entity];
                resourcesBuffer.Clear();

                var chunkGroups = new NativeParallelHashMap<int2, FixedList512Bytes<int3>>(8, Allocator.Temp);

                int mineY = buildingPosData.LeftCornerPos.y - 1;

                for (int x = buildingPosData.LeftCornerPos.x; x < buildingPosData.LeftCornerPos.x + buildingPosData.size.x; x++)
                {
                    for (int z = buildingPosData.LeftCornerPos.z; z < buildingPosData.LeftCornerPos.z + buildingPosData.size.z; z++)
                    {
                        int3 worldPos = new int3(x, mineY, z);

                        int2 chunkPos = new int2(
                            Mathf.FloorToInt((float)worldPos.x / worldSettings.Size),
                            Mathf.FloorToInt((float)worldPos.z / worldSettings.Size)
                        );

                        int3 localPos = new int3(
                            worldPos.x - (chunkPos.x * worldSettings.Size),
                            worldPos.y, 
                            worldPos.z - (chunkPos.y * worldSettings.Size)
                        );

                        if (!chunkGroups.TryGetValue(chunkPos, out var list))
                        {
                            list = new FixedList512Bytes<int3>();
                        }
                        
                        if (list.Length < list.Capacity)
                        {
                            list.Add(localPos);
                            chunkGroups[chunkPos] = list;
                        }
                    }
                }

                foreach (var pair in chunkGroups)
                {
                    resourcesBuffer.Add(new ResourcesInChunkLink
                    {
                        chunkPos = pair.Key,
                        ResourcesCells = pair.Value,
                        indexCell = 0
                    });
                }

                chunkGroups.Dispose();
            }

        }
    }
    [BurstCompile]
    public partial struct MarkManyPointJob : IJobEntity
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
        public NativeParallelHashMap<int3, int> CellMapBuildingsIDs; 
        public NativeParallelHashMap<int3, Entity> CellMapEntites;
        public NativeParallelMultiHashMap<Entity, int3> CellEntityMultiMap;
        public NativeParallelHashMap<int, Entity> Entities;
        public NativeParallelHashMap<int3, bool> IsBluePrintOrDemolitionPoints; 
        public NativeParallelHashMap<int3, float> CellWeights;    
        public NativeParallelHashMap<int3, float3> CellDirections;
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
        public BuildingMap BuildingsMap;

        public ComponentLookup<SpawnMobsData> SpawnMobsDataLookup;
        public Entity SpawnManagerEntity;

        [ReadOnly] public BuildingConfigReference buildingConfigReference;
        [ReadOnly] public ComponentLookup<HealthData> HealthDataLookup;
        [ReadOnly] public BufferLookup<ManyPointPointHealthData> ManyPointPointHealthDataLookup;

        [ReadOnly] public ChunkMap ChunkMap;
        [ReadOnly] public BufferLookup<BlockElement> BlockLookup;
        [ReadOnly] public WorldSettings Settings;

        public EntityCommandBuffer ECB;

        public void Execute()
        {
            var spawnMobsData = SpawnMobsDataLookup[SpawnManagerEntity];
            spawnMobsData.totalWeights = 0;

            BuildingsMap.CellWeights.Clear();

            var queue = new NativeQueue<int3>(Allocator.Temp);

            foreach (var building in BuildingsMap.CellMapBuildingsIDs)
            {
                int3 bPos = building.Key;
                int buildingID = building.Value;

                if (BuildingsMap.IsBluePrintOrDemolitionPoints.ContainsKey(bPos) &&
                    BuildingsMap.IsBluePrintOrDemolitionPoints[bPos])
                    continue;

                float startWeight = 0f;

                if (buildingConfigReference.BuildingsBaseConfigs.Value.TryGetConfig(buildingID, out var config))
                {
                    startWeight = GetPriorityScore(config.buildingType, config.typeOfLogic);
                }

                var en = BuildingsMap.CellMapEntites[bPos];

                float healthPercent = 1f;

                if (ManyPointPointHealthDataLookup.HasBuffer(en))
                {
                    foreach (var b in ManyPointPointHealthDataLookup[en])
                    {
                        if (bPos.Equals(b.pos))
                        {
                            healthPercent = (float)b.CurrHealth / b.MaxHealth;
                            break;
                        }
                    }
                }
                else
                {
                    healthPercent =
                        (float)HealthDataLookup[en].CurrHealth /
                        HealthDataLookup[en].MaxHealth;
                }

                float res = startWeight * healthPercent;

                spawnMobsData.totalWeights += (21f - res);

                BuildingsMap.CellWeights[bPos] = res;

                queue.Enqueue(bPos);
            }


            int maxSearchDist = 20;

            while (queue.TryDequeue(out int3 curr))
            {
                float currWeight = BuildingsMap.CellWeights[curr];

                for (int i = 0; i < directions.Length; i++)
                {
                    int3 offset = directions[i];
                    int3 neighbor = curr + offset;

                    if (IsBlocked(neighbor)) continue;

                    float distMod = math.length(offset); // 1 or 1.41 or 1.73
                    float stepCost = distMod;

                    float newWeight = currWeight + stepCost;

                    if (newWeight > maxSearchDist) continue;

                    if (!BuildingsMap.CellWeights.TryGetValue(neighbor, out float old) ||
                        newWeight < old)
                    {
                        BuildingsMap.CellWeights[neighbor] = newWeight;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            SpawnMobsDataLookup[SpawnManagerEntity] = spawnMobsData;
        }

        bool IsBlocked(int3 worldPos)
        {
            int2 chunkPos = new int2(
                (int)math.floor((float)worldPos.x / Settings.Size),
                (int)math.floor((float)worldPos.z / Settings.Size)
            );

            if (!ChunkMap.ChunkMapData.TryGetValue(chunkPos, out var chunkEntity))
                return true;

            if (!BlockLookup.HasBuffer(chunkEntity))
                return true;

            var buffer = BlockLookup[chunkEntity];

            int3 local = new int3(
                worldPos.x - chunkPos.x * Settings.Size,
                worldPos.y,
                worldPos.z - chunkPos.y * Settings.Size
            );

            if (local.x < 0 || local.z < 0 ||
                local.x >= Settings.Size ||
                local.z >= Settings.Size ||
                local.y < 0 ||
                local.y >= Settings.Height)
                return true;

            int index =
                local.x +
                local.z * Settings.Size +
                local.y * Settings.Size * Settings.Size;

            return buffer[index].BlockID != 0;
        }

        static readonly int3[] directions =
        {
            new int3(1,0,0), new int3(-1,0,0),
            new int3(0,1,0), new int3(0,-1,0),
            new int3(0,0,1), new int3(0,0,-1),

            new int3(1,1,0), new int3(-1,1,0),
            new int3(1,0,1), new int3(-1,0,1),
            new int3(0,1,1), new int3(0,-1,1),

            new int3(1,-1,0), new int3(-1,-1,0),
            new int3(1,0,-1), new int3(-1,0,-1),
            new int3(0,1,-1), new int3(0,-1,-1)
        };

        private float GetPriorityScore(BuildingsTypes type, TypeOfLogic logic)
        {
            switch (type)
            {
                case BuildingsTypes.Special: return 1f;
                case BuildingsTypes.Enegry: return 5f;
                case BuildingsTypes.Logistic: return 10f;
                case BuildingsTypes.Procession: return 15f;
                case BuildingsTypes.Defence:
                    return 20f + (logic == TypeOfLogic.WorkWithItems ? 10 : 0);
                default: return 20f;
            }
        }
    }
    [BurstCompile]
    public struct GenerateFlowDirectionsJob : IJob
    {
        [ReadOnly] public NativeParallelHashMap<int3, float> Weights;

        [ReadOnly] public ChunkMap ChunkMap;
        [ReadOnly] public BufferLookup<BlockElement> BlockLookup;
        [ReadOnly] public WorldSettings Settings;

        public NativeParallelHashMap<int3, float3> Directions;

        public void Execute()
        {
            Directions.Clear();

            foreach (var entry in Weights)
            {
                int3 curr = entry.Key;
                float currWeight = entry.Value;

                float3 dir = float3.zero;
                bool found = false;

                for (int i = 0; i < directions.Length; i++)
                {
                    int3 n = curr + directions[i];

                    if (IsBlocked(n))
                        continue;

                    if (Weights.TryGetValue(n, out float w) && w < currWeight)
                    {
                        float3 v = (float3)(n - curr);
                        float diff = currWeight - w;

                        dir += math.normalize(v) * diff;
                        found = true;
                    }
                }

                Directions[curr] =
                    (found && math.lengthsq(dir) > 0.001f)
                        ? math.normalize(dir)
                        : float3.zero;
            }
        }

        bool IsBlocked(int3 worldPos)
        {
            int2 chunkPos = new int2(
                (int)math.floor((float)worldPos.x / Settings.Size),
                (int)math.floor((float)worldPos.z / Settings.Size)
            );

            if (!ChunkMap.ChunkMapData.TryGetValue(chunkPos, out var chunkEntity))
                return true;

            if (!BlockLookup.HasBuffer(chunkEntity))
                return true;

            var buffer = BlockLookup[chunkEntity];

            int3 local = new int3(
                worldPos.x - chunkPos.x * Settings.Size,
                worldPos.y,
                worldPos.z - chunkPos.y * Settings.Size
            );

            if (local.x < 0 || local.z < 0 ||
                local.x >= Settings.Size ||
                local.z >= Settings.Size ||
                local.y < 0 ||
                local.y >= Settings.Height)
                return true;

            int index =
                local.x +
                local.z * Settings.Size +
                local.y * Settings.Size * Settings.Size;

            return buffer[index].BlockID != 0;
        }

        static readonly int3[] directions =
        {
            new int3(1,0,0), new int3(-1,0,0),
            new int3(0,1,0), new int3(0,-1,0),
            new int3(0,0,1), new int3(0,0,-1)
        };
    }
}