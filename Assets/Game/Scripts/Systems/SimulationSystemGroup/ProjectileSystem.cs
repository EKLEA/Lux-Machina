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
    
    EntityQuery _IsPause;
    public void OnCreate(ref SystemState state)
    {
         _IsPause= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<IsPause,BuildingMap>()
            .Build(ref state);
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        
        if(!_IsPause.IsEmpty) return;
        var grid = SystemAPI.GetSingleton<TurretGrid>();
        var ecbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

        var healthLookup = SystemAPI.GetComponentLookup<HealthData>(true);
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        
         var settings = SystemAPI.GetSingleton<WorldTime>(); 
        state.Dependency = new ProjectileMovementJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime*settings.SpeedMultiplier,
            CellSize = grid.CellSize,
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
    [NativeDisableContainerSafetyRestriction] 
    public ComponentLookup<LocalTransform> TransformLookup; 
    
    [ReadOnly] public ComponentLookup<HealthData> HealthLookup;

    void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, ref ProjectileData data, ref LocalTransform transform)
    {
        float distXZ = math.distance(data.StartPos.xz, data.TargetPos.xz);
        data.Progress += (data.Speed / math.max(0.1f, distXZ)) * DeltaTime;

        float3 nextPos = math.lerp(data.StartPos, data.TargetPos, math.min(1.0f, data.Progress));
        if (data.ArcHeight > 0) nextPos.y += math.sin(data.Progress * math.PI) * data.ArcHeight;
        transform.Position = nextPos;

        if (data.Progress >= 1.0f)
        {
            int2 targetCell = (int2)math.floor(data.TargetPos.xz / CellSize);
            bool isAreaEffect = data.ArcHeight > 0;
            
            // 1. Увеличиваем порог для одиночного снаряда (например, до 1.5 метра)
            // И используем квадрат расстояния
            float radius = isAreaEffect ? data.Radius : 1.2f; 
            float thresholdSq = radius * radius;

            // 2. Даже для обычного снаряда смотрим соседние ячейки (range минимум 1)
            int range = isAreaEffect ? (int)math.ceil(data.Radius / CellSize) : 1;

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
                            
                            // Берем радиус врага из компонента (если его нет, используем 0.5f по умолчанию)
                            float victimRadius = 0.9f; // Здесь можно вытянуть из ComponentLookup<EnemyData>
                            
                            // Считаем дистанцию в 2D (XZ)
                            float dist = math.distance(data.TargetPos.xz, victimPos.xz);

                            // Условие: дистанция минус радиус врага должна быть меньше радиуса снаряда
                            if (dist - victimRadius <= data.Radius) 
                            {
                                ECB.AppendToBuffer(chunkIndex, victim, new TakeDamage { Damage = data.Damage, pos = currentCell });
                                
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
            
            ECB.DestroyEntity(chunkIndex, entity);
        }
    }
}
}