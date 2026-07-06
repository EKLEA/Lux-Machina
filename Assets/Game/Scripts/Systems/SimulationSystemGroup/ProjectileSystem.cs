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

        [ReadOnly] public NativeParallelMultiHashMap<int3, Entity> EnemyInCellsMap;
        
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
                int3 targetCell = (int3)math.floor(data.TargetPos / CellSize);
                bool isAreaEffect = data.ArcHeight > 0;
                
                // Для пуль даем щедрый радиус поиска, так как враг мог сместиться
                float searchRadius = isAreaEffect ? data.Radius : 2.0f; 
                int range = (int)math.ceil(searchRadius / CellSize);

                for (int x = -range; x <= range; x++)
                {
                    for (int y = -range; y <= range; y++)
                    {
                        for (int z = -range; z <= range; z++)
                        {
                            int3 currentCell = targetCell + new int3(x, y,z);

                            if (EnemyInCellsMap.TryGetFirstValue(currentCell, out Entity victim, out var it))
                            {
                                do
                                {
                                    if (!HealthLookup.HasComponent(victim)) continue;

                                    float3 victimPos = TransformLookup[victim].Position;
                                    float distSq = math.distancesq(data.TargetPos.xz, victimPos.xz);
                                    
                                    // Эффективный радиус: радиус взрыва/пули + толщина врага (0.9f)
                                    float combinedRadius = searchRadius + 1.8f;

                                    if (distSq <= (combinedRadius * combinedRadius)) 
                                    {
                                        ECB.AppendToBuffer(chunkIndex, victim, new TakeDamage { 
                                            Damage = data.Damage, 
                                            pos = currentCell 
                                        });
                                        
                                        // Если это пуля, она исчезает после первого попадания
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
                }
                
                // Уничтожаем снаряд, если он долетел, даже если никого не задел
                ECB.DestroyEntity(chunkIndex, entity);
            }
        }
    }
}