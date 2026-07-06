using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
    if (!_IsPause.IsEmpty) return;

    var map = SystemAPI.GetSingleton<BuildingMap>();
    var turretMap = SystemAPI.GetSingletonRW<TurretGrid>();
    var managerEntity = SystemAPI.GetSingletonEntity<BuildingMap>();
    var configRef = SystemAPI.GetSingleton<EnemyBaseConfigRefence>();
    var chunkMap = SystemAPI.GetSingleton<ChunkMap>();
    var worldSettings = SystemAPI.GetSingleton<WorldSettings>();
    var blockLookup = SystemAPI.GetBufferLookup<BlockElement>(true); 

    var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
    var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
    JobHandle currentFrameHandle = state.Dependency;

    if (!_spawnMobs.IsEmpty)
{
    
    var spawnMobsLookup = state.GetComponentLookup<SpawnMobsData>(false);

    
    var calcWeightsJob = new CalculateTotalWeightsJob
    {
        CellWeights = map.CellWeights, 
        SpawnMobsDataLookup = spawnMobsLookup,
        CellMapBuildingsIDs = map.CellMapBuildingsIDs, 
        SpawnManagerEntity = managerEntity
    };
    var calcWeightsHandle = calcWeightsJob.Schedule(currentFrameHandle);

    
var kvArrays = map.CellDirections.GetKeyValueArrays(Allocator.TempJob);
var tempPoints = new NativeList<SpawnPointElement>(kvArrays.Length, Allocator.TempJob);

var evalJobHandle = new EvaluateSpawnZonesParallelJob
{
    AllPositions = kvArrays.Keys,
    WeightsMap = map.CellWeights,
    FlowDirections = map.CellDirections, 
    ChunkMap = chunkMap,
    BlockLookup = blockLookup,
    Settings = worldSettings,
    IsFlyingEnemy = false,
    ResultPoints = tempPoints.AsParallelWriter()
}.Schedule(kvArrays.Length, 64, calcWeightsHandle);

kvArrays.Dispose(evalJobHandle);

    
    var spawnHandle = new IntegratedSpawnJob
    {
        SpawnManagerEntity = managerEntity,
        SpawnMobsDataLookup = spawnMobsLookup,
        SpawnPoints = tempPoints,
        EnemyConfigs = configRef,
        ChunkMap = chunkMap,
        BlockLookup = blockLookup,
        Settings = worldSettings,
        ECB = ecb,
        ElapsedTime = SystemAPI.Time.ElapsedTime,
        Seed = (uint)(SystemAPI.Time.ElapsedTime * 1000)
    }.Schedule(evalJobHandle);

    tempPoints.Dispose(spawnHandle);
    currentFrameHandle = spawnHandle;
}

    if (!_enemies.IsEmpty)
    {
        
        var tickData = SystemAPI.GetSingleton<WorldTime>();
        var clearJob = new ClearMultiHashMapsJob
        {
            TurretTargets = turretMap.ValueRW.EnemyGridMap,
            TargetsToTurrets = turretMap.ValueRW.EnemyToTurret,
            EnemyInCellsMap = turretMap.ValueRW.EnemyInCellsMap
        };
        JobHandle clearHandle = clearJob.Schedule(currentFrameHandle);

        var logicEcb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        var logicHandle = new EnemyLogicJob
        { 
            ECB = logicEcb.AsParallelWriter(),
            FlowDirections = map.CellDirections,
            CellEntities = map.CellMapEntites,
            IsBluePrintOrDemolition = map.IsBluePrintOrDemolitionPoints,
            TurretCells = turretMap.ValueRW.TurretGridClaim,
            ChunkMap = chunkMap,
            BlockLookup = blockLookup,
            Settings = worldSettings,
            EnemyInCellsMap = turretMap.ValueRW.EnemyInCellsMap.AsParallelWriter(),
            TargetsToTurrets = turretMap.ValueRW.EnemyToTurret.AsParallelWriter(),
            TurretTargets = turretMap.ValueRW.EnemyGridMap.AsParallelWriter(),
            DamageLookUp = SystemAPI.GetBufferLookup<TakeDamage>(false),
            CheckForDestroyLookUp = SystemAPI.GetComponentLookup<CheckForDestroy>(false),
            DeltaTime = SystemAPI.Time.DeltaTime * tickData.SpeedMultiplier,
            ElapsedTime = (float)SystemAPI.Time.ElapsedTime
        }.ScheduleParallel(clearHandle); 

        currentFrameHandle = logicHandle;
    }

    state.Dependency = currentFrameHandle;
}

}
[BurstCompile]
public struct CalculateTotalWeightsJob : IJob
{
    [ReadOnly] public NativeParallelHashMap<int3, float> CellWeights;
    
    [ReadOnly] public NativeParallelHashMap<int3, int> CellMapBuildingsIDs;
    public ComponentLookup<SpawnMobsData> SpawnMobsDataLookup;
    public Entity SpawnManagerEntity;

    public void Execute()
    {
        var spawnMobsData = SpawnMobsDataLookup[SpawnManagerEntity];
        float sum = 0f;
        
        var kvArrays = CellWeights.GetKeyValueArrays(Allocator.Temp);
        try
        {
            for (int i = 0; i != kvArrays.Length; i++)
            {
                int3 cellPos = kvArrays.Keys[i];
                float w = kvArrays.Values[i];


                float contribution = 21f - w;
                if (contribution > 0f)
                {
                    sum += contribution;
                }
            }
        }
        finally
        {
            kvArrays.Dispose();
        }

        spawnMobsData.totalWeights = sum;
        SpawnMobsDataLookup[SpawnManagerEntity] = spawnMobsData; 
    }
}

[BurstCompile]
public struct IntegratedSpawnJob : IJob
{
    public Entity SpawnManagerEntity;
    [ReadOnly] public NativeList<SpawnPointElement> SpawnPoints;
    public ComponentLookup<SpawnMobsData> SpawnMobsDataLookup;
    [ReadOnly] public EnemyBaseConfigRefence EnemyConfigs;
    
    [ReadOnly] public ChunkMap ChunkMap;
    [ReadOnly] public BufferLookup<BlockElement> BlockLookup;
    [ReadOnly] public WorldSettings Settings;
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
            
        int spawnedCountInThisFrame = 0;
        const int MAX_SPAWNS_PER_FRAME = 30;

        for (int i = 0; i < directionsCount; i++)
        {
            if (budgetToSpend <= 0 || spawnedCountInThisFrame >= MAX_SPAWNS_PER_FRAME) break;

            var spawnPoint = sortedPoints[i];
            float3 basePos = new float3(spawnPoint.Position.x, spawnPoint.Position.y, spawnPoint.Position.z);
            float sectorBudget = math.min(budgetToSpend, 100f); 

            while (sectorBudget > 0 && spawnedCountInThisFrame < MAX_SPAWNS_PER_FRAME)
            {
                int enemyIdx = PickEnemyIndex(ref rnd, isAdvanced, sectorBudget);
                if (enemyIdx == -1) break;

                var enemyCfg = EnemyConfigs.EnemyBaseConfigs.Value.Configs[enemyIdx];
                if (sectorBudget < enemyCfg.costInPoints) break;

                float2 noise2D = rnd.NextFloat2Direction() * rnd.NextFloat(1f, 5f);
                float3 finalSpawnPos = basePos + new float3(noise2D.x, 0f, noise2D.y);
                int3 checkIntPos = (int3)math.floor(finalSpawnPos);

                
                bool groundFound = false;
                for (int yOffset = 5; yOffset >= -5; yOffset--)
                {
                    int3 testPos = new int3(checkIntPos.x, checkIntPos.y + yOffset, checkIntPos.z);
                    if (!IsBlocked(testPos) && !IsBlocked(testPos + new int3(0, 1, 0)) && IsBlocked(testPos + new int3(0, -1, 0)))
                    {
                        finalSpawnPos.y = testPos.y;
                        checkIntPos = testPos;
                        groundFound = true;
                        break;
                    }
                }

                if (!groundFound)
                {
                    sectorBudget -= 0.1f; 
                    continue;
                }

                Entity eventEntity = ECB.CreateEntity();
                ECB.AddComponent(eventEntity, new CreateEnemyEventData 
                { 
                    EnemyID = enemyCfg.id, 
                    pos = finalSpawnPos 
                });

                sectorBudget -= enemyCfg.costInPoints;
                budgetToSpend -= enemyCfg.costInPoints;
                spawnedCountInThisFrame++;
            }
        }

        config.pointsToSpawnMobs = budgetToSpend + remainingBudget;
        config.CountOfCicle++;
        sortedPoints.Dispose();
    }

    
    ECB.SetComponentEnabled<SpawnMobsData>(SpawnManagerEntity, false);
    ECB.SetBuffer<SpawnPointElement>(SpawnManagerEntity).Clear();
    ECB.SetComponent(SpawnManagerEntity, config);
}


    bool IsBlocked(int3 worldPos)
    {
        if (worldPos.y < 0 || worldPos.y >= Settings.Height) return true; 

        int2 chunkPos = new int2(
            (int)math.floor((float)worldPos.x / Settings.Size),
            (int)math.floor((float)worldPos.z / Settings.Size)
        );

        if (!ChunkMap.ChunkMapData.TryGetValue(chunkPos, out var chunkEntity)) return true; 
        if (!BlockLookup.HasBuffer(chunkEntity)) return true;

        var buffer = BlockLookup[chunkEntity];

        int3 local = new int3(
            worldPos.x - chunkPos.x * Settings.Size,
            worldPos.y,
            worldPos.z - chunkPos.y * Settings.Size
        );

        if (local.x < 0 || local.z < 0 || local.x >= Settings.Size || local.z >= Settings.Size) return true;

        int index = local.x + Settings.Size * (local.y + Settings.Height * local.z);
        if (index < 0 || index >= buffer.Length) return true;

        return buffer[index].BlockID != 0;
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
    [ReadOnly] public NativeArray<int3> AllPositions;
    [ReadOnly] public NativeParallelHashMap<int3, float> WeightsMap; 
    [ReadOnly] public NativeParallelHashMap<int3, float3> FlowDirections; 
    [ReadOnly] public ChunkMap ChunkMap;
    [ReadOnly] public BufferLookup<BlockElement> BlockLookup;
    [ReadOnly] public WorldSettings Settings;
    
    public bool IsFlyingEnemy; 
    public NativeList<SpawnPointElement>.ParallelWriter ResultPoints;

    public void Execute(int index)
    {
        int3 centerPos = AllPositions[index];
    
        
        if (IsBlocked(centerPos)) return;

        
        if (!WeightsMap.TryGetValue(centerPos, out float currentWeight)) return;

        
        
        if (currentWeight < 25f) return;

        
        if (!IsFlyingEnemy)
        {
            if (centerPos.y < 1 || centerPos.y >= Settings.Height - 1) return;
            int3 underPos = centerPos + new int3(0, -1, 0);
            int3 abovePos = centerPos + new int3(0, 1, 0);

            if (!IsBlocked(underPos)) return; 
            if (IsBlocked(abovePos)) return;  
        }

        
       
      if (FlowDirections.TryGetValue(centerPos, out float3 moveDir) && math.lengthsq(moveDir) > 0.001f)
        {
            float3 awayDir = -math.normalize(moveDir);
            const int STRICT_CHECK_DIST = 25; 
            bool trappedInsideBase = false;

            for (int stepIdx = 1; stepIdx <= STRICT_CHECK_DIST; stepIdx++)
            {
                int3 checkPos = (int3)math.floor((float3)centerPos + awayDir * stepIdx);

                if (IsBlocked(checkPos))
                {
                    trappedInsideBase = true;
                    break;
                }

                if (WeightsMap.TryGetValue(checkPos, out float outerWeight))
                {
                    // ИСПРАВЛЕНО: Маленький вес означает, что луч прилетел обратно на базу
                    // Замените 5.0f на минимально допустимый вес "чистого поля" за стенами
                    if (outerWeight <= 5.0f) 
                    {
                        trappedInsideBase = true;
                        break;
                    }
                }

                if (FlowDirections.TryGetValue(checkPos, out float3 outerDir) && math.lengthsq(outerDir) > 0.001f)
                {
                    if (math.dot(math.normalize(outerDir), awayDir) < -0.5f)
                    {
                        trappedInsideBase = true;
                        break;
                    }
                }
            }

            // ИСПРАВЛЕНО: Проверка находится строго после завершения цикла for
            if (trappedInsideBase) return;
        }


        
        float spawnScore = currentWeight; 
            if (spawnScore > 0.5f)
            {
                ResultPoints.AddNoResize(new SpawnPointElement
                { 
                    Position = centerPos, 
                    Weight = spawnScore 
                });
            }
    }

    bool IsBlocked(int3 worldPos)
    {
        if (worldPos.y < 0 || worldPos.y >= Settings.Height) return true; 
        
        int2 chunkPos = new int2(
            (int)math.floor((float)worldPos.x / Settings.Size),
            (int)math.floor((float)worldPos.z / Settings.Size)
        );
        
        if (!ChunkMap.ChunkMapData.TryGetValue(chunkPos, out var chunkEntity)) return true; 
        if (!BlockLookup.HasBuffer(chunkEntity)) return true;
        
        var buffer = BlockLookup[chunkEntity];
        int3 local = new int3(
            worldPos.x - chunkPos.x * Settings.Size,
            worldPos.y,
            worldPos.z - chunkPos.y * Settings.Size
        );
        
        if (local.x < 0 || local.z < 0 || local.x >= Settings.Size || local.z >= Settings.Size) return true;
        
        int index = local.x + Settings.Size * (local.y + Settings.Height * local.z);
        if (index < 0 || index >= buffer.Length) return true;
        
        return buffer[index].BlockID != 0;
    }
}


[BurstCompile]
public struct ClearMultiHashMapsJob : IJob
{
    public NativeParallelMultiHashMap<int, Entity> TurretTargets;
    public NativeParallelMultiHashMap<Entity, int> TargetsToTurrets;
    
    public NativeParallelMultiHashMap<int3, Entity> EnemyInCellsMap;
    

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
    [ReadOnly] public NativeParallelHashMap<int3, float3> FlowDirections;
    [ReadOnly] public NativeParallelHashMap<int3, Entity> CellEntities;
    [ReadOnly] public NativeParallelMultiHashMap<int3, int> TurretCells;
    [ReadOnly] public BufferLookup<TakeDamage> DamageLookUp;
    [ReadOnly] public ComponentLookup<CheckForDestroy> CheckForDestroyLookUp;
    [ReadOnly] public NativeParallelHashMap<int3, bool> IsBluePrintOrDemolition;
    [ReadOnly] public ChunkMap ChunkMap;
    [ReadOnly] public BufferLookup<BlockElement> BlockLookup;
    [ReadOnly] public WorldSettings Settings;
    public EntityCommandBuffer.ParallelWriter ECB; 
    public NativeParallelMultiHashMap<int, Entity>.ParallelWriter TurretTargets;
    public NativeParallelMultiHashMap<Entity, int>.ParallelWriter TargetsToTurrets;
    public NativeParallelMultiHashMap<int3, Entity>.ParallelWriter EnemyInCellsMap;
    public float DeltaTime;
    public float ElapsedTime;

    float GetGroundHeight(float3 position)
    {
        int3 cell = (int3)math.floor(position);
        for (int y = cell.y; y >= 0; y--)
        {
            if (IsBlocked(new int3(cell.x, y, cell.z)))
            {
                return (float)(y + 1); 
            }
        }
        return 0f; 
    }

    bool IsBlocked(int3 worldPos)
    {
        if (worldPos.y < 0 || worldPos.y >= Settings.Height)
            return true; 
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
        if (local.x < 0 || local.z < 0 || local.x >= Settings.Size || local.z >= Settings.Size)
            return true;
        int index = local.x + Settings.Size * (local.y + Settings.Height * local.z);
        if (index < 0 || index >= buffer.Length)
            return true;
        return buffer[index].BlockID != 0;
    }

    // ДОБАВИЛИ: ref-параметры анимации прямо в метод Execute
    public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, 
        ref LocalTransform transform, 
        ref EnemyStats stats,
        ref VatTimeComponent vatTime,
        ref VatOffsetComponent vatOffset,
        ref UnitAnimationState animState)
    {
        float3 currentPos = transform.Position;
        if (math.isnan(currentPos.x) || math.isinf(currentPos.x)) return;
        int3 cellPos = (int3)math.floor(currentPos);
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

        // ПЕРЕМЕННЫЕ ДЛЯ ОПРЕДЕЛЕНИЯ ТЕКУЩЕГО СОСТОЯНИЯ В ЭТОМ КАДРЕ
        float targetOffset = animState.CurrentStateOffset;
        float targetDuration = animState.CurrentClipDuration;
        float targetAnimSpeed = animState.Speed;

        bool hasMoved = false;
        bool isAttacking = false;
        
        if (FlowDirections.TryGetValue(cellPos, out float3 moveDir) && math.lengthsq(moveDir) > 0.001f)
        {
           int3 nextCell = (int3)math.floor(currentPos + moveDir * 1.3f); 
            if (CellEntities.TryGetValue(nextCell, out Entity targetBuilding))
            {
                bool isBlueprint = IsBluePrintOrDemolition.ContainsKey(nextCell) && IsBluePrintOrDemolition[nextCell];
                if (!isBlueprint) 
                {
                    if (ElapsedTime > stats.LastAttackTime + stats.AttackInterval)
                    {
                        if (DamageLookUp.HasBuffer(targetBuilding) && !CheckForDestroyLookUp.IsComponentEnabled(targetBuilding))
                        {
                            ECB.AppendToBuffer(chunkIndex, targetBuilding, new TakeDamage { Damage = stats.AttackDamage, pos = nextCell });
                        }
                        stats.LastAttackTime = ElapsedTime;
                    }
                    
                    isAttacking = true; 

                    // ИСПРАВЛЕНО: Разворачиваем паука лицом к зданию, не ломая вертикальную ось
                    float3 flatMoveDir = moveDir;
                    flatMoveDir.y = 0f; 
                    if (math.lengthsq(flatMoveDir) > 0.001f)
                    {
                        quaternion targetLook = quaternion.LookRotation(math.normalize(flatMoveDir), math.up());
                        transform.Rotation = targetLook;
                    }
                    
                    ApplyAnimationState(isAttacking, hasMoved, ref targetOffset, ref targetDuration, ref targetAnimSpeed);
                    UpdateVatPlayback(ref vatTime, ref vatOffset, targetOffset, targetDuration, targetAnimSpeed);
                    
                    // Важно: так как сработал return, паук застынет на месте и не пойдет дальше внутрь стены
                    return; 
                }
            }
            
            // Обычное движение вперед
            hasMoved = true;
            float pseudoRandom = math.sin(entity.Index + ElapsedTime) * 0.1f;
            float3 sideDir = new float3(-moveDir.z, 0f, moveDir.x) * pseudoRandom;
            float3 finalMove = moveDir + sideDir;

            if (math.lengthsq(finalMove) > 0.001f)
            {
                finalMove = math.normalize(finalMove);
            }
            else
            {
                finalMove = math.normalize(moveDir);
            }

            float3 nextPosition = transform.Position + (finalMove * stats.Speed * DeltaTime);

            // ИСПРАВЛЕНИЕ: Считаем упреждающие точки коллизии слева, справа и впереди врага
            // Увеличиваем радиус с ~0.45f до 0.9f (в 2 раза), чтобы он не врезался в стены
            float enemyRadius = 0.9f; 
            float3 forwardCheck = nextPosition + finalMove * enemyRadius;
            float3 rightCheck = nextPosition + new float3(-finalMove.z, 0f, finalMove.x) * (enemyRadius * 0.5f);
            float3 leftCheck = nextPosition + new float3(finalMove.z, 0f, -finalMove.x) * (enemyRadius * 0.5f);

            // Если любая из точек заходит в стену — блокируем движение в эту сторону
            if (IsBlocked((int3)math.floor(forwardCheck)) || 
                IsBlocked((int3)math.floor(rightCheck)) || 
                IsBlocked((int3)math.floor(leftCheck)))
            {
                // Не даем пройти сквозь текстуру стен
                nextPosition = transform.Position; 
            }
            else
            {
                nextPosition.y = GetGroundHeight(nextPosition); 
            }

            if (!math.isnan(nextPosition.x) && !math.isinf(nextPosition.x))
            {
                transform.Position = nextPosition;

                quaternion targetLook = quaternion.LookRotation(finalMove, math.up());
                transform.Rotation = targetLook; 
            }

            ApplyAnimationState(isAttacking, hasMoved, ref targetOffset, ref targetDuration, ref targetAnimSpeed);
            UpdateVatPlayback(ref vatTime, ref vatOffset, targetOffset, targetDuration, targetAnimSpeed);
            return;
        }

        // 2. ЛОГИКА АТАКЫ БЛИЖАЙШЕЙ ЦЕЛИ
        float3 nearestTargetPos = float3.zero;
        float minDistance = float.MaxValue;
        bool targetFound = false;
        foreach (var building in CellEntities)
        {
            float3 bPos = new float3(building.Key.x, building.Key.y, building.Key.z);
            float dist = math.distancesq(currentPos, bPos);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearestTargetPos = bPos;
                targetFound = true;
            }
        }
        
        float3 targetDirection = new float3(0f, 0f, 1f); 
        if (targetFound)
        {
            float3 vectorToTarget = nearestTargetPos - currentPos;
            float distToTarget = math.length(vectorToTarget);
            float3 flatTargetDir = vectorToTarget;
            flatTargetDir.y = 0f;
            if (math.lengthsq(flatTargetDir) > 0.001f)
            {
                targetDirection = math.normalize(flatTargetDir);
            }
            
            int3 cellUnder = (int3)math.floor(currentPos);
            float3 surfaceNormal = new float3(0f, 1f, 0f); 
            int3 cellForward = cellUnder + (int3)math.forward();
            int3 cellBackward = cellUnder + (int3)math.back();
            int3 cellRight = cellUnder + (int3)math.right();
            int3 cellLeft = cellUnder + (int3)math.left();
            float dz = (IsBlocked(cellForward) ? 1f : 0f) - (IsBlocked(cellBackward) ? 1f : 0f);
            float dx = (IsBlocked(cellRight) ? 1f : 0f) - (IsBlocked(cellLeft) ? 1f : 0f);
            float3 slopeNormal = new float3(-dx, 2f, -dz);
            if (math.lengthsq(slopeNormal) > 0.001f)
            {
                surfaceNormal = math.normalize(slopeNormal);
            }
            
            int3 targetCell = (int3)math.floor(nearestTargetPos);
            if (distToTarget <= 1.5f) 
            {
                if (CellEntities.TryGetValue(targetCell, out Entity targetBuilding))
                {
                    if (ElapsedTime > stats.LastAttackTime + stats.AttackInterval)
                    {
                        if (DamageLookUp.HasBuffer(targetBuilding) && !CheckForDestroyLookUp.IsComponentEnabled(targetBuilding))
                        {
                            ECB.AppendToBuffer(chunkIndex, targetBuilding, new TakeDamage { Damage = stats.AttackDamage, pos = targetCell });
                        }
                        stats.LastAttackTime = ElapsedTime;
                    }
                }
                
                isAttacking = true; // Стоит вплотную и бьет цель

                if (math.lengthsq(targetDirection) > 0.001f)
                {
                    transform.Rotation = quaternion.LookRotation(targetDirection, surfaceNormal);
                }

                ApplyAnimationState(isAttacking, hasMoved, ref targetOffset, ref targetDuration, ref targetAnimSpeed);
                UpdateVatPlayback(ref vatTime, ref vatOffset, targetOffset, targetDuration, targetAnimSpeed);
                return; 
            }
        }
        else
        {
            float3 fallback = float3.zero - currentPos;
            if (math.lengthsq(fallback) > 0.001f)
            {
                targetDirection = math.normalize(fallback);
            }
        }
        
        // 3. ФОЛБЕК-ДВИЖЕНИЕ (ЕСЛИ ЦЕЛЬ ДАЛЕКО)
        hasMoved = true;
        float pseudoRandomFallback = math.sin(entity.Index * 0.5f) * 0.15f;
        float3 sideDirFallback = new float3(-targetDirection.z, 0f, targetDirection.x) * pseudoRandomFallback;
        float3 finalMoveFallback = targetDirection + sideDirFallback;
        if (math.lengthsq(finalMoveFallback) > 0.001f)
        {
            finalMoveFallback = math.normalize(finalMoveFallback);
        }
        else
        {
            finalMoveFallback = targetDirection;
        }
        float3 nextPosFallback = transform.Position + (finalMoveFallback * stats.Speed * DeltaTime);
        nextPosFallback.y = GetGroundHeight(nextPosFallback);
        
        if (!math.isnan(nextPosFallback.x) && !math.isinf(nextPosFallback.x))
        {
            transform.Position = nextPosFallback;
            transform.Rotation = quaternion.LookRotation(finalMoveFallback, math.up());
        }

        ApplyAnimationState(isAttacking, hasMoved, ref targetOffset, ref targetDuration, ref targetAnimSpeed);
        UpdateVatPlayback(ref vatTime, ref vatOffset, targetOffset, targetDuration, targetAnimSpeed);
    }

   private void ApplyAnimationState(bool isAttacking, bool hasMoved, ref float offset, ref float duration, ref float animSpeed)
    {
        if (isAttacking)
        {
            // Анимация [Armature|Attack]
            offset = 0.5000f;   
            duration = 0.5000f; 
            animSpeed = 0.5f; // Базовая скорость атаки (можно увеличить, если бьет слишком медленно)
        }
        else if (hasMoved)
        {
            // Анимация [Armature|ArmatureAction] во время бега
            offset = 0.0000f;   
            duration = 0.5000f; 
            animSpeed = 0.05f; // Немного ускоряем анимацию ног при движении, чтобы не "скользил"
        }
        else
        {
            // Анимация [Armature|ArmatureAction] когда паук просто стоит
            offset = 0.0000f;   
            duration = 0.5000f; 
            animSpeed = 0.5f;
        }
    }
    private void UpdateVatPlayback(ref VatTimeComponent vatTime, ref VatOffsetComponent vatOffset, float targetOffset, float targetDuration, float speed)
    {
        if (vatOffset.Value != targetOffset){vatTime.Value = 0.0f;vatOffset.Value = targetOffset;}
        vatTime.Value += DeltaTime * speed;
        if (vatTime.Value >= targetDuration){vatTime.Value = 0.0f;}
    }
}


