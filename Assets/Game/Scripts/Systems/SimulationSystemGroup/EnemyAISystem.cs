using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(CraftApplySystem))]
[BurstCompile]
public partial struct EnemyAISystem : ISystem
{
   
    EntityQuery _spawnEnemies;
    EntityQuery _enemies;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BuildingMap>();
        state.RequireForUpdate<SpawnMobs>();

        _spawnEnemies= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<SpawnMobs>()
            .Build(ref state);
        _enemies= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<EnemyStats>()
            .WithDisabled<LoadInfo,SaveInfo>()
            .Build(ref state);

        
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var map = SystemAPI.GetSingleton<BuildingMap>();
        var configRef = SystemAPI.GetSingleton<EnemyBaseConfigRefence>();
        var damageLookUp = SystemAPI.GetBufferLookup<TakeDamage>(true); 
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        var parallelEcb = ecb.AsParallelWriter(); 
        float time=(float)SystemAPI.Time.ElapsedTime;

        if (!_spawnEnemies.IsEmpty)
        {
            var spawnJob = new SpawnMobsJob
            {
                ECB = ecb,
                enemyBaseConfig = configRef,
                ElapsedTime =time
            };
            state.Dependency = spawnJob.Schedule(state.Dependency);
        }

        if (!_enemies.IsEmpty)
        {

            var logicJob = new EnemyLogicJob
            { 
                ECB = parallelEcb,
                FlowDirections = map.CellDirections,
                CellEntities = map.CellMapEntites,
                DamageLookUp = damageLookUp,
                DeltaTime = SystemAPI.Time.DeltaTime,
                ElapsedTime = time
            };
            state.Dependency = logicJob.ScheduleParallel(state.Dependency);
        }
    }
}
[BurstCompile]
public partial struct SpawnMobsJob : IJobEntity
{
    
    public EntityCommandBuffer ECB;
    public EnemyBaseConfigRefence enemyBaseConfig;
    public float ElapsedTime;
    public void Execute(Entity entity,in SpawnMobs spawnMobs)
    {
        
        for(int i = 0; i < spawnMobs.points;i++)
        {
            if (enemyBaseConfig.EnemyBaseConfigs.Value.GetIdByPos(0) != -1)
            {
               
                var command= ECB.CreateEntity();
                 uint seed = (uint)command.Index + (uint)(ElapsedTime * 1000);
                var random = new Unity.Mathematics.Random(seed);
                float2 noise = random.NextFloat2(new float2(-40f, -40f), new float2(40f, 40f));
                ECB.AddComponent(command,new CreateEnemyEventData{EnemyID=enemyBaseConfig.EnemyBaseConfigs.Value.GetIdByPos(0),pos=new float3(noise.x,1,noise.y)});
            }
          
        }
        ECB.SetComponentEnabled<SpawnMobs>(entity,false);

    }
}
[BurstCompile]
[WithDisabled(typeof(LoadInfo),typeof(SaveInfo))]
public partial struct EnemyLogicJob : IJobEntity
{
    [ReadOnly] public NativeParallelHashMap<int2, float2> FlowDirections;
    [ReadOnly] public NativeParallelHashMap<int2, Entity> CellEntities;
    [ReadOnly] public BufferLookup<TakeDamage> DamageLookUp;
    public EntityCommandBuffer.ParallelWriter ECB;   
     public float DeltaTime;
    public float ElapsedTime;

    public void Execute(Entity entity,  [ChunkIndexInQuery] int chunkIndex,ref LocalTransform transform, ref EnemyStats stats)
    {
        float2 currentPos = transform.Position.xz;
        int2 cellPos = (int2)math.floor(currentPos);

        if (FlowDirections.TryGetValue(cellPos, out float2 moveDir))
        {
            int2 nextCell = (int2)math.floor(currentPos + moveDir * 0.5f); 

            if (CellEntities.TryGetValue(nextCell, out Entity targetBuilding) && !nextCell.Equals(cellPos))
            {
                if (ElapsedTime > stats.LastAttackTime + stats.AttackInterval)
                {
                    
                    if (DamageLookUp.HasBuffer(targetBuilding))
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
