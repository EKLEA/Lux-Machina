
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ProjectileSystem))]
[BurstCompile]
public partial struct   HealthSystem: ISystem
{
   
    EntityQuery _deadlyOBJs;
    EntityQuery _enemiesForDead;
    
    EntityQuery _IsPause;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BuildingMap>();
        state.RequireForUpdate<SpawnMobsData>();

        _deadlyOBJs= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<HealthData,TakeDamage>()
            .Build(ref state);
        _enemiesForDead= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<EnemyStats>()
            .Build(ref state);
         _IsPause= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<IsPause,BuildingMap>()
            .Build(ref state);

        
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        
        if(!_IsPause.IsEmpty) return;
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        var parallelEcb = ecb.AsParallelWriter(); 
        var buildingConfig=SystemAPI.GetSingleton<BuildingConfigReference>();
        var health=SystemAPI.GetComponentLookup<HealthData>(true);
        var road=SystemAPI.GetComponentLookup<RoadTypeBuildingTag>(true);
        var roadHealth=SystemAPI.GetBufferLookup<RoadPointHealthData>(true);
        var ForceDestroyTagLookup=SystemAPI.GetComponentLookup<ForceDestroyTag>(true);
        if (!_deadlyOBJs.IsEmpty)
        {
            state.Dependency =new TakeDamageJob
            {
                ECB=parallelEcb,
                buildingBaseConfig=buildingConfig,
                HealthDataLookup= health,
                RoadLookUP=road,
                ForceDestroyTagLookup = ForceDestroyTagLookup,
                RoadPointHealthDataLookup=roadHealth
            }.ScheduleParallel(state.Dependency);
        }
        if (!_enemiesForDead.IsEmpty)
        {
            state.Dependency =new DestroyEnemies
            {
                ECB=parallelEcb
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(EnemyStats), typeof(ForceDestroyTag))]
    partial struct DestroyEnemies : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB;
        
        public void Execute([ChunkIndexInQuery] int sortKey, Entity entity, EnabledRefRO<ForceDestroyTag> forcedestory)
        {
            if (forcedestory.ValueRO) 
            {
                ECB.DestroyEntity(sortKey, entity);
            }
        }
    }
    [BurstCompile]
    partial struct TakeDamageJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB;
        [ReadOnly] public ComponentLookup<RoadTypeBuildingTag> RoadLookUP;
        public BuildingConfigReference buildingBaseConfig;
        [ReadOnly] public ComponentLookup<HealthData> HealthDataLookup;
        [ReadOnly] public BufferLookup<RoadPointHealthData> RoadPointHealthDataLookup;
        [ReadOnly] public ComponentLookup<ForceDestroyTag> ForceDestroyTagLookup;
        public void Execute([ChunkIndexInQuery] int sortKey,Entity entity,ref DynamicBuffer<TakeDamage> takeDamage)
        {
            var buff=takeDamage;
            if (takeDamage.Length == 0) return;
            if(RoadLookUP.HasComponent(entity))
            {
                if(buildingBaseConfig.BuildingsBaseConfigs.Value.TryGetConfig(buildingBaseConfig.roadID,out var cfg))
                {
                    var destroyCellCommand=ECB.CreateEntity(sortKey);
                    ECB.AddComponent(sortKey,destroyCellCommand,new DeleteRoadPointsFromMap{isForce=true});
                    var cellToDelete=ECB.AddBuffer<MapPoint>(sortKey,destroyCellCommand);
                    var damagePerCell=new NativeHashMap<int2, float>(takeDamage.Length, Allocator.Temp);
                    var healthCell=new NativeHashMap<int2, float>(takeDamage.Length, Allocator.Temp);
                    var healthList = new NativeList<RoadPointHealthData>(RoadPointHealthDataLookup[entity].Length, Allocator.Temp);
                    healthList.AddRange(RoadPointHealthDataLookup[entity].AsNativeArray());
                    foreach (var h in healthList)
                    {
                        healthCell.Add(h.pos,h.CurrHealth);
                    }
                    foreach(var d in buff)
                    {
                        if(damagePerCell.ContainsKey(d.pos)) damagePerCell[d.pos]+=d.Damage;
                        else damagePerCell.Add(d.pos,d.Damage);
                    }
                    var cellArray=damagePerCell.GetKeyArray(Allocator.Temp);
                    foreach(var c in cellArray)
                    {
                        float hp=healthCell.ContainsKey(c)?healthCell[c]:cfg.MaxHealth;
                        if (hp-damagePerCell[c] <=0)
                        {
                            cellToDelete.Add(new MapPoint{pos=c});
                        }
                        else
                        {
                            if(healthCell.ContainsKey(c))
                            {
                                
                                healthCell[c]=healthCell[c]-damagePerCell[c];
                            }
                            else
                                healthCell.Add(c,cfg.MaxHealth-damagePerCell[c]);
                        }
                    }
                    var b=ECB.SetBuffer<RoadPointHealthData>(sortKey,entity);
                    foreach(var h in healthCell)
                    {
                        b.Add(new RoadPointHealthData{pos=h.Key,CurrHealth=h.Value,MaxHealth=cfg.MaxHealth,RestoreHpPerTick=cfg.RestoreHpPerTick,TimeToRestore=cfg.TimeToRestore,CurrTimeToRestore=cfg.TimeToRestore});
                    }
                }
              
            }
            else
            {
                if (HealthDataLookup.HasComponent(entity))
                {
                    var health=HealthDataLookup[entity];
                    health.CurrTimeToRestore=0;
                    foreach(var d in buff)
                    {
                        if (health.CurrHealth - d.Damage <= 0)
                        {
                            health.CurrHealth=0;
                            if (ForceDestroyTagLookup.HasComponent(entity))
                            {
                                ECB.SetComponentEnabled<ForceDestroyTag>(sortKey,entity,true);
                            }
                            break;
                        }
                        else
                        {
                            health.CurrHealth=health.CurrHealth-d.Damage;
                        }
                    }
                    
                    ECB.SetComponent<HealthData>(sortKey,entity,health);
                }
                
            }
            buff.Clear();
            ECB.SetBuffer<TakeDamage>(sortKey,entity);
        }
    }
}
