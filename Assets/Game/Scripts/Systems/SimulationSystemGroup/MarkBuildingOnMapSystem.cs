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
        var TurretTranformLookup = SystemAPI.GetComponentLookup<TurretTranform>(false);
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
        var buildingConfigRef = SystemAPI.GetSingleton<BuildingConfigReference>();

        var healthDataLookup = SystemAPI.GetComponentLookup<HealthData>(true);
        var manyPointHealthLookup = SystemAPI.GetBufferLookup<ManyPointPointHealthData>(true);
        var blockLookup = SystemAPI.GetBufferLookup<BlockElement>(true);

        if (runBuilding)
        {
            state.Dependency = new MarkBuildingJob
            {
                MapData = buildingMapRW.ValueRW,
                EnergyMap = energyMapRW.ValueRW,
                EntityDictionary = entitiesRW.ValueRW,
                TurretGrid = turretMapRW.ValueRW,
                MapEntity = mapEntity,
                worldSettings = worldSettings,
                ResourcesLinkLookup = resourcesLinkLookup,
                UpdateMapTagLookup = updateMapLookup,
                UpdateClusterTagLookup = updateClusterLookup,
                EnergyBuildingDataLookup = energyBuildingDataLookup,
                UpdateConnectStatusLookup = updateConnectStatusLookup,
                ConnectToEnegyEntitiesLookup = connectToEnegyEntitiesLookup,
                TurretStatsLookup = TurretStatsLookup,
                TurretTranformLookup=TurretTranformLookup,

                buildingConfigReference = buildingConfigRef,
                HealthDataLookup = healthDataLookup,
                ManyPointPointHealthDataLookup = manyPointHealthLookup,
                ChunkMap = chunkMap.ValueRO,
                BlockLookup = blockLookup,
            }.Schedule(state.Dependency);
        }

        if (runManyPoint)
        {
            
            updateMapLookup = SystemAPI.GetComponentLookup<UpdateMapTag>(false);
            updateClusterLookup = SystemAPI.GetComponentLookup<UpdateClustersTag>(false);
            
            healthDataLookup = SystemAPI.GetComponentLookup<HealthData>(true);
            manyPointHealthLookup = SystemAPI.GetBufferLookup<ManyPointPointHealthData>(true);
            blockLookup = SystemAPI.GetBufferLookup<BlockElement>(true);

            state.Dependency = new MarkManyPointJob
            {
                MapData = buildingMapRW.ValueRW,
                MapEntity = mapEntity,
                EntityDictionary = entitiesRW.ValueRW,
                UpdateMapTagLookup = updateMapLookup,
                UpdateClusterTagLookup = updateClusterLookup,
                worldSettings = worldSettings,

                buildingConfigReference = buildingConfigRef,
                HealthDataLookup = healthDataLookup,
                ManyPointPointHealthDataLookup = manyPointHealthLookup,
                ChunkMap = chunkMap.ValueRO,
                BlockLookup = blockLookup,
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
        public WorldSettings worldSettings;
        public TurretGrid TurretGrid;
        public EntitiesDictionary EntityDictionary; 
        public Entity MapEntity;
        public ComponentLookup<UpdateClustersTag> UpdateClusterTagLookup;
        public ComponentLookup<EnergyBuildingData> EnergyBuildingDataLookup;
        public ComponentLookup<UpdateConnectStatus> UpdateConnectStatusLookup;
        public ComponentLookup<ConnectToEnegyEntities> ConnectToEnegyEntitiesLookup;
        public BufferLookup<ResourcesInChunkLink> ResourcesLinkLookup;
        public ComponentLookup<TurretStats> TurretStatsLookup;
        public ComponentLookup<TurretTranform> TurretTranformLookup;
        

        public ComponentLookup<UpdateMapTag> UpdateMapTagLookup;
        [ReadOnly] public BuildingConfigReference buildingConfigReference;
        [ReadOnly] public ComponentLookup<HealthData> HealthDataLookup;
        [ReadOnly] public BufferLookup<ManyPointPointHealthData> ManyPointPointHealthDataLookup;
        [ReadOnly] public ChunkMap ChunkMap;
        [ReadOnly] public BufferLookup<BlockElement> BlockLookup;


        private const int MaxSearchDist = 20;

        struct DirectionDataWeights
        {
            public int3 Offset;
            public float StepCost;
            public DirectionDataWeights(int3 offset) { Offset = offset; StepCost = math.length(new float3(offset)); }
        }

        struct DirectionDataFlow
        {
            public int3 Offset;
            public float3 Normalized;
            public DirectionDataFlow(int3 offset) { Offset = offset; Normalized = math.normalize((float3)offset); }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static DirectionDataWeights GetWeightDirection(int index)
        {
            switch (index)
            {
                case 0: return new DirectionDataWeights(new int3(1, 0, 0));
                case 1: return new DirectionDataWeights(new int3(-1, 0, 0));
                case 2: return new DirectionDataWeights(new int3(0, 1, 0));
                case 3: return new DirectionDataWeights(new int3(0, -1, 0));
                case 4: return new DirectionDataWeights(new int3(0, 0, 1));
                case 5: return new DirectionDataWeights(new int3(0, 0, -1));
                case 6: return new DirectionDataWeights(new int3(1, 1, 0));
                case 7: return new DirectionDataWeights(new int3(-1, 1, 0));
                case 8: return new DirectionDataWeights(new int3(1, 0, 1));
                case 9: return new DirectionDataWeights(new int3(-1, 0, 1));
                case 10: return new DirectionDataWeights(new int3(0, 1, 1));
                case 11: return new DirectionDataWeights(new int3(0, -1, 1));
                case 12: return new DirectionDataWeights(new int3(1, -1, 0));
                case 13: return new DirectionDataWeights(new int3(-1, -1, 0));
                case 14: return new DirectionDataWeights(new int3(1, 0, -1));
                case 15: return new DirectionDataWeights(new int3(-1, 0, -1));
                case 16: return new DirectionDataWeights(new int3(0, 1, -1));
                case 17: return new DirectionDataWeights(new int3(0, -1, -1));
                default: return new DirectionDataWeights(new int3(0, 0, 0));
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static DirectionDataFlow GetFlowDirection(int index)
        {
            switch (index)
            {
                case 0: return new DirectionDataFlow(new int3(1, 0, 0));
                case 1: return new DirectionDataFlow(new int3(-1, 0, 0));
                case 2: return new DirectionDataFlow(new int3(0, 1, 0));
                case 3: return new DirectionDataFlow(new int3(0, -1, 0));
                case 4: return new DirectionDataFlow(new int3(0, 0, 1));
                case 5: return new DirectionDataFlow(new int3(0, 0, -1));
                case 6: return new DirectionDataFlow(new int3(1, 1, 0));
                case 7: return new DirectionDataFlow(new int3(-1, 1, 0));
                case 8: return new DirectionDataFlow(new int3(1, -1, 0));
                case 9: return new DirectionDataFlow(new int3(-1, -1, 0));
                case 10: return new DirectionDataFlow(new int3(1, 0, 1));
                case 11: return new DirectionDataFlow(new int3(0, 1, 1));
                case 12: return new DirectionDataFlow(new int3(1, 0, -1));
                case 13: return new DirectionDataFlow(new int3(0, -1, 1));
                case 14: return new DirectionDataFlow(new int3(-1, 0, 1));
                case 15: return new DirectionDataFlow(new int3(0, 1, -1));
                case 16: return new DirectionDataFlow(new int3(-1, 0, -1));
                case 17: return new DirectionDataFlow(new int3(0, -1, -1));
                case 18: return new DirectionDataFlow(new int3(1, 1, 1));
                case 19: return new DirectionDataFlow(new int3(-1, 1, 1));
                case 20: return new DirectionDataFlow(new int3(1, -1, 1));
                case 21: return new DirectionDataFlow(new int3(-1, -1, 1));
                case 22: return new DirectionDataFlow(new int3(1, 1, -1));
                case 23: return new DirectionDataFlow(new int3(-1, 1, -1));
                case 24: return new DirectionDataFlow(new int3(1, -1, -1));
                case 25: return new DirectionDataFlow(new int3(-1, -1, -1));
                default: return new DirectionDataFlow(new int3(0, 0, 0));
            }
        }

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

        bool IsBlocked(int3 worldPos, ref int2 lastChunkPos, ref DynamicBuffer<BlockElement> lastBuffer, ref bool hasLastBuffer)
        {
            if (worldPos.y < 0 || worldPos.y >= worldSettings.Height) return true;
            
            int2 chunkPos = new int2(
                worldPos.x >= 0 ? worldPos.x / worldSettings.Size : (worldPos.x - worldSettings.Size + 1) / worldSettings.Size,
                worldPos.z >= 0 ? worldPos.z / worldSettings.Size : (worldPos.z - worldSettings.Size + 1) / worldSettings.Size
            );
            
            if (!hasLastBuffer || !chunkPos.Equals(lastChunkPos))
            {
                if (!ChunkMap.ChunkMapData.TryGetValue(chunkPos, out var chunkEntity) || !BlockLookup.HasBuffer(chunkEntity))
                {
                    hasLastBuffer = false; 
                    return true;
                }
                lastBuffer = BlockLookup[chunkEntity]; 
                lastChunkPos = chunkPos; 
                hasLastBuffer = true;
            }
            
            int3 local = new int3(worldPos.x - chunkPos.x * worldSettings.Size, worldPos.y, worldPos.z - chunkPos.y * worldSettings.Size);
            if (local.x < 0 || local.z < 0 || local.x >= worldSettings.Size || local.z >= worldSettings.Size) return true;
            
            
            int index = local.x + worldSettings.Size * (local.y + worldSettings.Height * local.z);
            if (index < 0 || index >= lastBuffer.Length) return true;
            
            return lastBuffer[index].BlockID != 0;
        }

        public void Execute(Entity entity,in BuildingData buildingData, in BuildingPosData buildingPosData,EnabledRefRW<MarkOnMap> markOnMap)
        {
            
            UpdateMapTagLookup.SetComponentEnabled(MapEntity,true);
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
                var stats = TurretStatsLookup[entity];
                
                float3 forward = buildingPosData.Rotation switch
                {
                    3 => new float3(0, 0, 1),   
                    0 => new float3(1, 0, 0),   
                    1 => new float3(0, 0, -1),  
                    2 => new float3(-1, 0, 0),
                    _ => new float3(0, 0, 1)
                };

                float spawnYaw = math.atan2(forward.x, forward.z);

                if (TurretTranformLookup.HasComponent(entity))
                {
                    var trans = TurretTranformLookup[entity];
                    trans.baseRotation = spawnYaw;
                    trans.rotation.y = spawnYaw;
                    TurretTranformLookup[entity] = trans; 
                }

                float cosHalfAngle = math.cos(math.radians(stats.Angle * 0.5f));
                int radiusInCells = (int)math.ceil(stats.AttackRange / TurretGrid.CellSize);

                float radiusSqInCells = radiusInCells * radiusInCells;

                for (int x = -radiusInCells; x <= radiusInCells; x++)
                {
                    for (int y = -radiusInCells; y <= radiusInCells; y++)
                    {
                        for (int z = -radiusInCells; z <= radiusInCells; z++)
                        {
                            float distSq = x * x + y * y + z * z;

                            if (distSq <= radiusSqInCells)
                            {
                                if (x == 0 && y == 0 && z == 0) 
                                {
                                    int3 cell = new int3((int)buildingPosData.center.x, (int)buildingPosData.center.y, (int)buildingPosData.center.z);
                                    TurretGrid.TurretGridClaim.Add(cell, buildingData.BuildingUniqueID);
                                    continue;
                                }

                                float dist = math.sqrt(distSq);
                                float3 directionToCell = new float3(x, y, z) / dist; 
                                
                                float dot = math.dot(forward, directionToCell);

                                if (dot >= cosHalfAngle)
                                {
                                    int3 cell = new int3(
                                        (int)buildingPosData.center.x + x, 
                                        (int)buildingPosData.center.y + y, 
                                        (int)buildingPosData.center.z + z
                                    );
                                    
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
 
            ref var baseConfigs = ref buildingConfigReference.BuildingsBaseConfigs.Value;
            float startWeight = 0f;
            if (baseConfigs.TryGetConfig(buildingData.BuildingIDHash, out var config))
            {
                startWeight = GetPriorityScore(config.buildingType, config.typeOfLogic);
            }

            
            float healthPercent = 1f;
            if (ManyPointPointHealthDataLookup.HasBuffer(entity))
            {
                var buffer = ManyPointPointHealthDataLookup[entity];
                int3 centerPos = (int3)buildingPosData.center;
                
                for (int j = 0; j != buffer.Length; j++)
                {
                    if (centerPos.Equals(buffer[j].pos)) 
                    { 
                        healthPercent = (float)buffer[j].CurrHealth / buffer[j].MaxHealth; 
                        break; 
                    }
                }
            }
            else if (HealthDataLookup.HasComponent(entity))
            {
                var healthData = HealthDataLookup[entity];
                healthPercent = (float)healthData.CurrHealth / healthData.MaxHealth;
            }

            float coreWeight = startWeight * healthPercent;

            int2 lastChunkPos = new int2(int.MinValue, int.MinValue);
            DynamicBuffer<BlockElement> lastBuffer = default;
            bool hasLastBuffer = false;

            var globalQueue = new NativeQueue<int3>(Allocator.Temp);
            var affectedCells = new NativeList<int3>(Allocator.Temp);
            var affectedSet = new NativeHashSet<int3>(1024, Allocator.Temp);

            var fillVisited = new NativeHashSet<int3>(1024, Allocator.Temp);

           for (int x = buildingPosData.LeftCornerPos.x; x < buildingPosData.LeftCornerPos.x + buildingPosData.size.x; x++)
            {
                
                int y = buildingPosData.LeftCornerPos.y; 
                
                for (int z = buildingPosData.LeftCornerPos.z; z < buildingPosData.LeftCornerPos.z + buildingPosData.size.z; z++)
                {
                    int3 buildingCell = new int3(x, y, z);
                    for (int i = 0; i < 18; i++)
                    {
                        DirectionDataWeights dir = GetWeightDirection(i);
                        int3 outerNeighbor = buildingCell + dir.Offset;
                        
                        
                        
                        if (dir.Offset.y != 0) continue; 

                        if (MapData.CellMapEntites.TryGetValue(outerNeighbor, out _)) continue;
                        if (IsBlocked(outerNeighbor, ref lastChunkPos, ref lastBuffer, ref hasLastBuffer)) continue;

                        float targetCellWeight = coreWeight + dir.StepCost;
                        if (!MapData.CellWeights.TryGetValue(outerNeighbor, out float old) || targetCellWeight < old - 0.001f)
                        {
                            MapData.CellWeights[outerNeighbor] = targetCellWeight;
                            if (fillVisited.Add(outerNeighbor))
                            {
                                globalQueue.Enqueue(outerNeighbor);
                            }
                        }
                    }
                }
            }
            
            
            
            while (globalQueue.TryDequeue(out int3 curr))
            {
                if (!MapData.CellWeights.TryGetValue(curr, out float currWeight))
                    continue;

                if (affectedSet.Add(curr))
                    affectedCells.Add(curr);

                for (int i = 0; i < 18; i++)
                {
                    DirectionDataWeights dir = GetWeightDirection(i);
                    int3 neighbor = curr + dir.Offset;

                    if (MapData.CellMapEntites.TryGetValue(neighbor, out _))
                        continue;

                    if (IsBlocked(neighbor, ref lastChunkPos, ref lastBuffer, ref hasLastBuffer))
                        continue;

                    float newWeight = currWeight + dir.StepCost;

                    
                    if (newWeight > coreWeight + MaxSearchDist)
                        continue;

                    if (!MapData.CellWeights.TryGetValue(neighbor, out float old))
                    {
                        MapData.CellWeights.Add(neighbor, newWeight);
                        if (fillVisited.Add(neighbor))
                        {
                            globalQueue.Enqueue(neighbor);
                        }
                    }
                    else if (newWeight < old - 0.001f)
                    {
                        MapData.CellWeights[neighbor] = newWeight;
                        if (fillVisited.Add(neighbor))
                        {
                            globalQueue.Enqueue(neighbor);
                        }
                    }
                }
            }

            globalQueue.Dispose();
            fillVisited.Dispose();

            
            
            
             for (int i = 0; i < affectedCells.Length; i++)
            {
                int3 curr = affectedCells[i];
                if (!MapData.CellWeights.TryGetValue(curr, out float currWeight))
                    continue;

                float3 flowDir = float3.zero;
                bool foundLowerWeight = false;

                for (int d = 0; d < 26; d++)
                {
                    DirectionDataFlow dirData = GetFlowDirection(d);
                    int3 n = curr + dirData.Offset;

                    if (IsBlocked(n, ref lastChunkPos, ref lastBuffer, ref hasLastBuffer))
                        continue;

                    if (MapData.CellWeights.TryGetValue(n, out float w))
                    {
                        if (w < currWeight)
                        {
                            float diff = currWeight - w;
                            flowDir += dirData.Normalized * diff;
                            foundLowerWeight = true;
                        }
                    }
                }

                
                if (foundLowerWeight && math.lengthsq(flowDir) > 0.001f)
                {
                    MapData.CellDirections[curr] = math.normalize(flowDir);
                }
                else
                {
                    MapData.CellDirections[curr] = float3.zero;
                }
            }

            affectedSet.Dispose();
            affectedCells.Dispose();
        }
    }

    [BurstCompile]
    public partial struct MarkManyPointJob : IJobEntity
    {
        public BuildingMap MapData; 
        public Entity MapEntity;
        public EntitiesDictionary EntityDictionary;         
        public ComponentLookup<UpdateClustersTag> UpdateClusterTagLookup;
        
        public ComponentLookup<UpdateMapTag> UpdateMapTagLookup;
        [ReadOnly] public BuildingConfigReference buildingConfigReference;
        [ReadOnly] public ComponentLookup<HealthData> HealthDataLookup;
        [ReadOnly] public BufferLookup<ManyPointPointHealthData> ManyPointPointHealthDataLookup;
        [ReadOnly] public ChunkMap ChunkMap;
        [ReadOnly] public BufferLookup<BlockElement> BlockLookup;
        [ReadOnly] public WorldSettings worldSettings;


        private const int MaxSearchDist = 20;

        struct DirectionDataWeights
        {
            public int3 Offset;
            public float StepCost;
            public DirectionDataWeights(int3 offset) { Offset = offset; StepCost = math.length(new float3(offset)); }
        }

        struct DirectionDataFlow
        {
            public int3 Offset;
            public float3 Normalized;
            public DirectionDataFlow(int3 offset) { Offset = offset; Normalized = math.normalize((float3)offset); }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static DirectionDataWeights GetWeightDirection(int index)
        {
            switch (index)
            {
                case 0: return new DirectionDataWeights(new int3(1, 0, 0));
                case 1: return new DirectionDataWeights(new int3(-1, 0, 0));
                case 2: return new DirectionDataWeights(new int3(0, 1, 0));
                case 3: return new DirectionDataWeights(new int3(0, -1, 0));
                case 4: return new DirectionDataWeights(new int3(0, 0, 1));
                case 5: return new DirectionDataWeights(new int3(0, 0, -1));
                case 6: return new DirectionDataWeights(new int3(1, 1, 0));
                case 7: return new DirectionDataWeights(new int3(-1, 1, 0));
                case 8: return new DirectionDataWeights(new int3(1, 0, 1));
                case 9: return new DirectionDataWeights(new int3(-1, 0, 1));
                case 10: return new DirectionDataWeights(new int3(0, 1, 1));
                case 11: return new DirectionDataWeights(new int3(0, -1, 1));
                case 12: return new DirectionDataWeights(new int3(1, -1, 0));
                case 13: return new DirectionDataWeights(new int3(-1, -1, 0));
                case 14: return new DirectionDataWeights(new int3(1, 0, -1));
                case 15: return new DirectionDataWeights(new int3(-1, 0, -1));
                case 16: return new DirectionDataWeights(new int3(0, 1, -1));
                case 17: return new DirectionDataWeights(new int3(0, -1, -1));
                default: return new DirectionDataWeights(new int3(0, 0, 0));
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static DirectionDataFlow GetFlowDirection(int index)
        {
            switch (index)
            {
                case 0: return new DirectionDataFlow(new int3(1, 0, 0));
                case 1: return new DirectionDataFlow(new int3(-1, 0, 0));
                case 2: return new DirectionDataFlow(new int3(0, 1, 0));
                case 3: return new DirectionDataFlow(new int3(0, -1, 0));
                case 4: return new DirectionDataFlow(new int3(0, 0, 1));
                case 5: return new DirectionDataFlow(new int3(0, 0, -1));
                case 6: return new DirectionDataFlow(new int3(1, 1, 0));
                case 7: return new DirectionDataFlow(new int3(-1, 1, 0));
                case 8: return new DirectionDataFlow(new int3(1, -1, 0));
                case 9: return new DirectionDataFlow(new int3(-1, -1, 0));
                case 10: return new DirectionDataFlow(new int3(1, 0, 1));
                case 11: return new DirectionDataFlow(new int3(0, 1, 1));
                case 12: return new DirectionDataFlow(new int3(1, 0, -1));
                case 13: return new DirectionDataFlow(new int3(0, -1, 1));
                case 14: return new DirectionDataFlow(new int3(-1, 0, 1));
                case 15: return new DirectionDataFlow(new int3(0, 1, -1));
                case 16: return new DirectionDataFlow(new int3(-1, 0, -1));
                case 17: return new DirectionDataFlow(new int3(0, -1, -1));
                case 18: return new DirectionDataFlow(new int3(1, 1, 1));
                case 19: return new DirectionDataFlow(new int3(-1, 1, 1));
                case 20: return new DirectionDataFlow(new int3(1, -1, 1));
                case 21: return new DirectionDataFlow(new int3(-1, -1, 1));
                case 22: return new DirectionDataFlow(new int3(1, 1, -1));
                case 23: return new DirectionDataFlow(new int3(-1, 1, -1));
                case 24: return new DirectionDataFlow(new int3(1, -1, -1));
                case 25: return new DirectionDataFlow(new int3(-1, -1, -1));
                default: return new DirectionDataFlow(new int3(0, 0, 0));
            }
        }

       private float GetPriorityScore(BuildingsTypes type, TypeOfLogic logic)
        {
            switch (type)
            {
                case BuildingsTypes.Special:
                    return 1f;

                case BuildingsTypes.Enegry:
                    return 5f;

                case BuildingsTypes.Logistic:
                    return 10f;

                case BuildingsTypes.Procession:
                    return 15f;

                case BuildingsTypes.Defence:
                    return 20f + (logic == TypeOfLogic.WorkWithItems ? 10f : 0f);

                default:
                    return 20f;
            }
        }

        bool IsBlocked(int3 worldPos, ref int2 lastChunkPos, ref DynamicBuffer<BlockElement> lastBuffer, ref bool hasLastBuffer)
        {
            if (worldPos.y != math.clamp(worldPos.y, 0, worldSettings.Height - 1)) return true;
            
            int2 chunkPos = new int2(
                worldPos.x >= 0 ? worldPos.x / worldSettings.Size : (worldPos.x - worldSettings.Size + 1) / worldSettings.Size,
                worldPos.z >= 0 ? worldPos.z / worldSettings.Size : (worldPos.z - worldSettings.Size + 1) / worldSettings.Size
            );
            
            if (!hasLastBuffer || !chunkPos.Equals(lastChunkPos))
            {
                if (!ChunkMap.ChunkMapData.TryGetValue(chunkPos, out var chunkEntity) || !BlockLookup.HasBuffer(chunkEntity))
                {
                    hasLastBuffer = false; 
                    return true;
                }
                lastBuffer = BlockLookup[chunkEntity]; 
                lastChunkPos = chunkPos; 
                hasLastBuffer = true;
            }
            
            int3 local = new int3(worldPos.x - chunkPos.x * worldSettings.Size, worldPos.y, worldPos.z - chunkPos.y * worldSettings.Size);
            if (local.x != math.clamp(local.x, 0, worldSettings.Size - 1) || local.z != math.clamp(local.z, 0, worldSettings.Size - 1)) return true;
            
            int index = local.x + worldSettings.Size * (local.y + worldSettings.Height * local.z);
            if (index != math.clamp(index, 0, lastBuffer.Length - 1)) return true;
            
            return lastBuffer[index].BlockID != 0;
        }

        public void Execute(Entity entity, in BuildingData buildingData, in DynamicBuffer<MapPoint> mapPoints, EnabledRefRW<MarkOnMap> markOnMap)
        {
            UpdateMapTagLookup.SetComponentEnabled(MapEntity, true);

            for (int i = 0; i != mapPoints.Length; i++)
            {
                var p = mapPoints[i];

                MapData.CellMapBuildingsIDs[p.pos] = buildingData.BuildingIDHash;
                MapData.CellMapEntites[p.pos] = entity;
                MapData.CellEntityMultiMap.Add(entity, p.pos);
            }

            markOnMap.ValueRW = false;
            EntityDictionary.Entities.TryAdd(buildingData.BuildingUniqueID, entity);
            UpdateClusterTagLookup.SetComponentEnabled(MapEntity, true);

            if (mapPoints.Length == 0)
                return;

            ref var baseConfigs = ref buildingConfigReference.BuildingsBaseConfigs.Value;

            float startWeight = 0f;
            if (baseConfigs.TryGetConfig(buildingData.BuildingIDHash, out var config))
            {
                startWeight = GetPriorityScore(config.buildingType, config.typeOfLogic);
            }

            int2 lastChunkPos = new int2(int.MinValue, int.MinValue);
            DynamicBuffer<BlockElement> lastBuffer = default;
            bool hasLastBuffer = false;

            var globalQueue = new NativeQueue<int3>(Allocator.Temp);
            var affectedCells = new NativeList<int3>(Allocator.Temp);
            var affectedSet = new NativeHashSet<int3>(1024, Allocator.Temp);

            bool hasBufferHealth = ManyPointPointHealthDataLookup.HasBuffer(entity);
            DynamicBuffer<ManyPointPointHealthData> healthBuffer = default;

            if (hasBufferHealth)
            {
                healthBuffer = ManyPointPointHealthDataLookup[entity];
            }

            
            
            
            for (int i = 0; i != mapPoints.Length; i++)
            {
                int3 buildingCell = mapPoints[i].pos;

                float cellHealthPercent = 1f;

                if (hasBufferHealth)
                {
                    for (int j = 0; j != healthBuffer.Length; j++)
                    {
                        if (!buildingCell.Equals(healthBuffer[j].pos))
                            continue;

                        if (healthBuffer[j].MaxHealth > 0)
                        {
                            cellHealthPercent =
                                healthBuffer[j].CurrHealth /
                                healthBuffer[j].MaxHealth;
                        }

                        break;
                    }
                }
                else if (HealthDataLookup.HasComponent(entity))
                {
                    var healthData = HealthDataLookup[entity];

                    if (healthData.MaxHealth > 0)
                    {
                        cellHealthPercent =
                            (float)healthData.CurrHealth /
                            healthData.MaxHealth;
                    }
                }

                float buildingCellWeight = startWeight * cellHealthPercent;

                MapData.CellWeights[buildingCell] = buildingCellWeight;
                MapData.CellDirections[buildingCell] = float3.zero;
            }

            
            
            
           var fillVisited = new NativeHashSet<int3>(1024, Allocator.Temp);

            for (int i = 0; i != mapPoints.Length; i++)
            {
                int3 buildingCell = mapPoints[i].pos;
                float buildingCellWeight = MapData.CellWeights[buildingCell];
                
                for (int w = 0; w != 18; w++)
                {
                    DirectionDataWeights dir = GetWeightDirection(w);
                    
                    
                    if (dir.Offset.y != 0) 
                        continue;
                        
                    int3 outerNeighbor = buildingCell + dir.Offset;
                    
                    
                    
                    if (MapData.CellMapEntites.ContainsKey(outerNeighbor))
                        continue;
                        
                    if (IsBlocked(outerNeighbor, ref lastChunkPos, ref lastBuffer, ref hasLastBuffer))
                        continue;
                        
                    float targetCellWeight = buildingCellWeight + dir.StepCost;
                    if (!MapData.CellWeights.TryGetValue(outerNeighbor, out float old) || targetCellWeight < old - 0.001f)
                    {
                        MapData.CellWeights[outerNeighbor] = targetCellWeight;
                        if (fillVisited.Add(outerNeighbor))
                        {
                            globalQueue.Enqueue(outerNeighbor);
                        }
                    }
                }
            }
            
            
            
            while (globalQueue.TryDequeue(out int3 curr))
            {
                if (!MapData.CellWeights.TryGetValue(curr, out float currWeight))
                    continue;

                if (affectedSet.Add(curr))
                    affectedCells.Add(curr);

                for (int i = 0; i < 18; i++)
                {
                    DirectionDataWeights dir = GetWeightDirection(i);
                    int3 neighbor = curr + dir.Offset;

                    if (MapData.CellMapEntites.TryGetValue(neighbor, out _))
                        continue;

                    if (IsBlocked(neighbor, ref lastChunkPos, ref lastBuffer, ref hasLastBuffer))
                        continue;

                    float newWeight = currWeight + dir.StepCost;

                    if (newWeight > startWeight + MaxSearchDist)
                        continue;

                    if (!MapData.CellWeights.TryGetValue(neighbor, out float old))
                    {
                        MapData.CellWeights.Add(neighbor, newWeight);
                        if (fillVisited.Add(neighbor))
                        {
                            globalQueue.Enqueue(neighbor);
                        }
                    }
                    else if (newWeight < old - 0.001f)
                    {
                        MapData.CellWeights[neighbor] = newWeight;
                        if (fillVisited.Add(neighbor))
                        {
                            globalQueue.Enqueue(neighbor);
                        }
                    }
                }
            }

            globalQueue.Dispose();
            fillVisited.Dispose();

            
            
            
           for (int i = 0; i < affectedCells.Length; i++)
{
    int3 curr = affectedCells[i];
    if (!MapData.CellWeights.TryGetValue(curr, out float currWeight))
        continue;

    float3 flowDir = float3.zero;
    bool foundLowerWeight = false;

    for (int d = 0; d < 26; d++)
    {
        DirectionDataFlow dirData = GetFlowDirection(d);
        
        
        if (dirData.Offset.y != 0)
            continue;

        int3 n = curr + dirData.Offset;

        if (IsBlocked(n, ref lastChunkPos, ref lastBuffer, ref hasLastBuffer))
            continue;

        if (MapData.CellWeights.TryGetValue(n, out float w))
        {
            if (w < currWeight)
            {
                float diff = currWeight - w;
                
                
                float2 flatDir2D = math.normalize(new float2(dirData.Offset.x, dirData.Offset.z));
                float3 flatDir3D = new float3(flatDir2D.x, 0f, flatDir2D.y);

                flowDir += flatDir3D * diff;
                foundLowerWeight = true;
            }
        }
    }

    
    if (foundLowerWeight && math.lengthsq(flowDir) > 0.001f)
    {
        MapData.CellDirections[curr] = math.normalize(flowDir);
    }
    else
    {
        MapData.CellDirections[curr] = float3.zero;
    }
}

            affectedSet.Dispose();
            affectedCells.Dispose();
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
}

























































            












        

















































        



























































































































































































































































