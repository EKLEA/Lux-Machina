using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]

[UpdateAfter(typeof(TurretSystem))]
[BurstCompile]
public partial struct ProjectileSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var grid = SystemAPI.GetSingleton<TurretGrid>();
        var ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

        // ВАЖНО: Везде ставим true (ReadOnly), чтобы не было ошибок доступа
        var healthLookup = SystemAPI.GetComponentLookup<HealthData>(true);
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);

        state.Dependency = new ProjectileMovementJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            CellSize = grid.CellSize,
            // Здесь должна быть карта [int2 -> Entity врага]
            // Если её нет, снаряд не сможет найти кого ударить в клетке
            EnemyInCellsMap = grid.EnemyInCellsMap, 
            HealthLookup = healthLookup,
            TransformLookup = transformLookup,
            ECB = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter()
        }.ScheduleParallel(state.Dependency);
    }

   [BurstCompile]
public partial struct ProjectileMovementJob : IJobEntity
{
    public float DeltaTime;
    public float CellSize;
    public EntityCommandBuffer.ParallelWriter ECB;

    [ReadOnly] public NativeParallelMultiHashMap<int2, Entity> EnemyInCellsMap;
    
    [ReadOnly] 
    [NativeDisableContainerSafetyRestriction] // Игнорируем конфликт, так как пишем в снаряд, а читаем врагов
    public ComponentLookup<LocalTransform> TransformLookup; 
    
    [ReadOnly] public ComponentLookup<HealthData> HealthLookup;

    void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, ref ProjectileData data, ref LocalTransform transform)
    {
        // 1. Движение снаряда
        float distXZ = math.distance(data.StartPos.xz, data.TargetPos.xz);
        data.Progress += (data.Speed / math.max(0.1f, distXZ)) * DeltaTime;

        float3 nextPos = math.lerp(data.StartPos, data.TargetPos, math.min(1.0f, data.Progress));
        if (data.ArcHeight > 0) nextPos.y += math.sin(data.Progress * math.PI) * data.ArcHeight;
        transform.Position = nextPos;

        // 2. Логика попадания
        if (data.Progress >= 1.0f)
        {
            int2 targetCell = (int2)math.floor(data.TargetPos.xz / CellSize);
            bool isAreaEffect = data.ArcHeight > 0;
            float thresholdSq = isAreaEffect ? (data.Radius * data.Radius) : 0.5f;

            // Если это взрыв, проверяем область 3x3 клетки вокруг (или больше, если радиус огромен)
            int range = isAreaEffect ? math.max(1, (int)math.ceil(data.Radius / CellSize)) : 0;

            for (int x = -range; x <= range; x++)
            {
                for (int y = -range; y <= range; y++)
                {
                    int2 currentCell = targetCell + new int2(x, y);

                    if (EnemyInCellsMap.TryGetFirstValue(currentCell, out Entity victim, out var it))
                    {
                        do
                        {
                            if (!HealthLookup.HasComponent(victim)) continue;

                            float3 victimPos = TransformLookup[victim].Position;
                            float distSq = math.distancesq(data.TargetPos, victimPos);

                            if (distSq <= thresholdSq)
                            {
                                ECB.AppendToBuffer(chunkIndex, victim, new TakeDamage { Damage = data.Damage, pos = currentCell });
                                
                                // Пуля (не арт) исчезает после первого попадания и не проверяет соседей
                                if (!isAreaEffect) 
                                {
                                    ECB.DestroyEntity(chunkIndex, entity);
                                    return; 
                                }
                            }
                        } 
                        while (EnemyInCellsMap.TryGetNextValue(out victim, ref it));
                    }
                }
            }
            
            // Уничтожаем снаряд (артиллерия уничтожается после проверки всех клеток)
            ECB.DestroyEntity(chunkIndex, entity);
        }
    }
}

}
