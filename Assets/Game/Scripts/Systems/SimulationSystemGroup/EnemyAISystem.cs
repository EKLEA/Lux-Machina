using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(CraftApplySystem))]
[BurstCompile]
public partial struct EnemyAISystem : ISystem
{
   
    EntityQuery _spawnMobs;
    EntityQuery _enemies;
    
    EntityQuery _IsPause;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BuildingMap>();
        state.RequireForUpdate<SpawnMobsData>();

        _spawnMobs= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<SpawnMobsData>()
            .Build(ref state);
        _enemies= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<EnemyStats>()
            .WithDisabled<LoadInfo>()
            .Build(ref state);
         _IsPause= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<IsPause,BuildingMap>()
            .Build(ref state);

        
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        
        if(!_IsPause.IsEmpty) return;
        var map = SystemAPI.GetSingleton<BuildingMap>();
        var turretMap = SystemAPI.GetSingletonRW<TurretGrid>();
        var managerEntity = SystemAPI.GetSingletonEntity<BuildingMap>();
        var configRef = SystemAPI.GetSingleton<EnemyBaseConfigRefence>();
        
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

       
        if (!_spawnMobs.IsEmpty)
        {
           
            var kvArrays = map.CellWeights.GetKeyValueArrays(Allocator.TempJob);
            var tempPoints = new NativeList<SpawnPointElement>(kvArrays.Length, Allocator.TempJob);
            state.Dependency= new EvaluateSpawnZonesParallelJob
            {
                AllPositions = kvArrays.Keys,
                AllWeights = kvArrays.Values,
                WeightsMap = map.CellWeights,
                ResultPoints= tempPoints.AsParallelWriter() 
            }.Schedule(kvArrays.Length, 64, state.Dependency);
            kvArrays.Dispose(state.Dependency);

            var spawnMobsLookup = state.GetComponentLookup<SpawnMobsData>(false);
            state.Dependency = new IntegratedSpawnJob 
            {
                SpawnManagerEntity = managerEntity,
                SpawnMobsDataLookup = spawnMobsLookup,
                SpawnPoints = tempPoints,
                EnemyConfigs = configRef,
                ECB = ecb,
                ElapsedTime = SystemAPI.Time.ElapsedTime,
                Seed = (uint)(SystemAPI.Time.ElapsedTime * 1000)
            }.Schedule(state.Dependency);
            tempPoints.Dispose(state.Dependency);
        }   
          
        if (!_enemies.IsEmpty)
        {
             var tickData=SystemAPI.GetSingleton<WorldTime>();
            var clearJob = new ClearMultiHashMapsJob
            {
                TurretTargets = turretMap.ValueRW.EnemyGridMap,
                TargetsToTurrets = turretMap.ValueRW.EnemyToTurret,
                EnemyInCellsMap = turretMap.ValueRW.EnemyInCellsMap
            };
            JobHandle clearHandle = clearJob.Schedule(state.Dependency);

            var logicEcb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            
            state.Dependency = new EnemyLogicJob
            { 
                ECB = logicEcb.AsParallelWriter(),
                FlowDirections = map.CellDirections,
                CellEntities = map.CellMapEntites,
                TurretCells = turretMap.ValueRW.TurretGridClaim,
                EnemyInCellsMap= turretMap.ValueRW.EnemyInCellsMap.AsParallelWriter(),
                
                TargetsToTurrets = turretMap.ValueRW.EnemyToTurret.AsParallelWriter(),
                TurretTargets = turretMap.ValueRW.EnemyGridMap.AsParallelWriter(),
                
                DamageLookUp = SystemAPI.GetBufferLookup<TakeDamage>(false),
                CheckForDestroyLookUp = SystemAPI.GetComponentLookup<CheckForDestroy>(false),
                DeltaTime = SystemAPI.Time.DeltaTime*tickData.SpeedMultiplier,
                ElapsedTime = (float)SystemAPI.Time.ElapsedTime
            }.ScheduleParallel(clearHandle); 
        }
    }
}
[BurstCompile]
public struct IntegratedSpawnJob : IJob
{
    public Entity SpawnManagerEntity;
    [ReadOnly] public NativeList<SpawnPointElement> SpawnPoints;

    public ComponentLookup<SpawnMobsData> SpawnMobsDataLookup;
    [ReadOnly] public EnemyBaseConfigRefence EnemyConfigs;
    
    public EntityCommandBuffer ECB;
    public double ElapsedTime;
    public uint Seed;

    public void Execute()
    {
        var config = SpawnMobsDataLookup[SpawnManagerEntity];

        config.playerProgress = config.totalWeights / EnemyConfigs.ProgressThreshold;
        float powerIncome = math.sqrt(math.max(0f, config.totalWeights)) * EnemyConfigs.PowerMultiplier;
        float timeMultiplier = 1f + (config.CountOfCicle * EnemyConfigs.TimeDifficultyFactor);
        
        config.pointsPerCicle = (EnemyConfigs.BaseIncome + powerIncome) * timeMultiplier;
        config.pointsToSpawnMobs += config.pointsPerCicle;
        // config.pointsToSpawnMobs=config.pointsToSpawnMobs/100;
        if (config.pointsToSpawnMobs >= config.AttackThreshold && !SpawnPoints.IsEmpty)
        {
            var sortedPoints = new NativeArray<SpawnPointElement>(SpawnPoints.AsArray(), Allocator.Temp);
                
            sortedPoints.Sort(new SpawnPointComparer());

            var rnd = Unity.Mathematics.Random.CreateFromIndex(Seed ^ (uint)(ElapsedTime * 1000));
            float totalBudget = config.pointsToSpawnMobs;
            bool isAdvanced = config.playerProgress > 100f;
            
            float budgetToSpend = totalBudget * rnd.NextFloat(0.8f, 1.0f);
            float remainingBudget = totalBudget - budgetToSpend;

            int directionsCount = isAdvanced 
                ? math.min(rnd.NextInt(6, 9), sortedPoints.Length) 
                : sortedPoints.Length;

            for (int i = 0; i < directionsCount; i++)
            {
                if (budgetToSpend <= 0) break;


                var spawnPoint = sortedPoints[i];
                float3 basePos = new float3(spawnPoint.Position.x, 0, spawnPoint.Position.y);
                float sectorBudget = budgetToSpend / (directionsCount - i);
                
                while (sectorBudget > 0)
                {
                    int enemyIdx = PickEnemyIndex(ref rnd, isAdvanced, sectorBudget);
                    if (enemyIdx == -1) break;

                    var enemyCfg = EnemyConfigs.EnemyBaseConfigs.Value.Configs[enemyIdx];
                    if (sectorBudget < enemyCfg.costInPoints) break;

                    Entity eventEntity = ECB.CreateEntity();
                    float2 noise = rnd.NextFloat2Direction() * rnd.NextFloat(1f, 5f);
                    
                    ECB.AddComponent(eventEntity, new CreateEnemyEventData { 
                        EnemyID = enemyCfg.id, 
                        pos = basePos + new float3(noise.x, 0, noise.y) 
                    });

                    sectorBudget -= enemyCfg.costInPoints;
                    budgetToSpend -= enemyCfg.costInPoints;
                }
            }
            config.pointsToSpawnMobs = budgetToSpend + remainingBudget;
            config.CountOfCicle++;
        }

        ECB.SetComponent(SpawnManagerEntity, config);
        ECB.SetComponentEnabled<SpawnMobsData>(SpawnManagerEntity,false);
        ECB.SetBuffer<SpawnPointElement>(SpawnManagerEntity).Clear();
    }

    private int PickEnemyIndex(ref Unity.Mathematics.Random rnd, bool isAdvanced, float currentSectorBudget)
    {
        int maxAffordableIndex = -1;
        
        for (int i = EnemyConfigs.EnemyBaseConfigs.Value.Configs.Length - 1; i >= 0; i--)
        {
            if (EnemyConfigs.EnemyBaseConfigs.Value.Configs[i].costInPoints <= currentSectorBudget)
            {
                maxAffordableIndex = i;
                break;
            }
        }

        if (maxAffordableIndex == -1) return -1;

        if (isAdvanced && rnd.NextFloat() > 0.5f)
        {
            int lowerBound = math.max(0, (maxAffordableIndex * 2) / 3);
            return rnd.NextInt(lowerBound, maxAffordableIndex + 1);
        }

        return rnd.NextInt(0, maxAffordableIndex + 1);
    }
}
[BurstCompile]
public struct EvaluateSpawnZonesParallelJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<int2> AllPositions;
    [ReadOnly] public NativeArray<float> AllWeights;
    [ReadOnly] public NativeParallelHashMap<int2, float> WeightsMap; 
    
    public NativeList<SpawnPointElement>.ParallelWriter ResultPoints;

    public void Execute(int index)
    {
        int2 centerPos = AllPositions[index];
        float currentWeight = AllWeights[index];

        if (currentWeight < 18f || currentWeight > 20f) return;

        float areaSum = 0f;
        const int searchRadius = 30;
        const int step = 6; 

        for (int x = -searchRadius; x <= searchRadius; x += step)
        {
            for (int y = -searchRadius; y <= searchRadius; y += step)
            {
                if (x * x + y * y > searchRadius * searchRadius) continue;

                int2 neighbor = centerPos + new int2(x, y);
                if (WeightsMap.TryGetValue(neighbor, out float nWeight))
                {
                    areaSum += (21f - nWeight); 
                }
            }
        }

        if (areaSum > 0.5f)
        {
            ResultPoints.AddNoResize(new SpawnPointElement 
            { 
                Position = centerPos, 
                Weight = areaSum 
            });
        }
    }
}

[BurstCompile]
public struct ClearMultiHashMapsJob : IJob
{
    public NativeParallelMultiHashMap<int, Entity> TurretTargets;
    public NativeParallelMultiHashMap<Entity, int> TargetsToTurrets;
    
    public NativeParallelMultiHashMap<int2, Entity> EnemyInCellsMap;
    

    public void Execute()
    {
        TurretTargets.Clear();
        TargetsToTurrets.Clear();
        EnemyInCellsMap.Clear();
    }
}
[BurstCompile]
[WithDisabled(typeof(LoadInfo))]
public partial struct EnemyLogicJob : IJobEntity
{
    [ReadOnly] public NativeParallelHashMap<int2, float2> FlowDirections;
    [ReadOnly] public NativeParallelHashMap<int2, Entity> CellEntities;
    

    [ReadOnly] public NativeParallelMultiHashMap<int2, int> TurretCells;

    [ReadOnly] public BufferLookup<TakeDamage> DamageLookUp;
    [ReadOnly] public ComponentLookup<CheckForDestroy> CheckForDestroyLookUp;
    public EntityCommandBuffer.ParallelWriter ECB;   
    public NativeParallelMultiHashMap<int, Entity>.ParallelWriter TurretTargets;
    public NativeParallelMultiHashMap<Entity, int>.ParallelWriter TargetsToTurrets;
    
     public NativeParallelMultiHashMap<int2, Entity>.ParallelWriter  EnemyInCellsMap;
    public float DeltaTime;
    public float ElapsedTime;

    public void Execute(Entity entity,  [ChunkIndexInQuery] int chunkIndex,ref LocalTransform transform, ref EnemyStats stats)
    {
        float2 currentPos = transform.Position.xz;
        int2 cellPos = (int2)math.floor(currentPos);
         EnemyInCellsMap.Add(cellPos, entity);
        if (TurretCells.TryGetFirstValue(cellPos, out int turretIndex, out var it))
        {
            do
            {
                TurretTargets.Add(turretIndex, entity);
                TargetsToTurrets.Add(entity, turretIndex);
            } 
            while (TurretCells.TryGetNextValue(out turretIndex, ref it));
        }
        
        if (FlowDirections.TryGetValue(cellPos, out float2 moveDir))
        {
            int2 nextCell = (int2)math.floor(currentPos + moveDir * 0.5f); 

            if (CellEntities.TryGetValue(nextCell, out Entity targetBuilding) && !nextCell.Equals(cellPos))
            {
                if (ElapsedTime > stats.LastAttackTime + stats.AttackInterval)
                {
                    
                    if (DamageLookUp.HasBuffer(targetBuilding)&&!CheckForDestroyLookUp.IsComponentEnabled(targetBuilding))
                    {
                        ECB.AppendToBuffer(chunkIndex, targetBuilding, new TakeDamage { Damage = stats.AttackDamage,pos=nextCell });
                        stats.LastAttackTime = ElapsedTime;
                    }
                    
                    stats.LastAttackTime = ElapsedTime;
                }
                
                transform.Rotation = quaternion.LookRotation(new float3(moveDir.x, 0, moveDir.y), math.up());
                return; 
            }

            transform.Position += new float3(moveDir.x, 0, moveDir.y) * stats.Speed * DeltaTime;
            transform.Rotation = quaternion.LookRotation(new float3(moveDir.x, 0, moveDir.y), math.up());
        }
        else
        {
            float2 toCenter = math.normalize(float2.zero - currentPos);
            transform.Position += new float3(toCenter.x, 0, toCenter.y) * stats.Speed * DeltaTime;
        }
    }
}
