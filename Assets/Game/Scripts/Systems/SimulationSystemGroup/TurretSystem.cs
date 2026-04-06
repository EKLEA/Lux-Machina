
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(EnemyAISystem))]
[BurstCompile]
public partial struct TurretSystem : ISystem
{
    
    EntityQuery _IsPause;
    public void OnCreate(ref SystemState state)
    {
         _IsPause= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<IsPause,BuildingMap>()
            .Build(ref state);
    }
    public void OnUpdate(ref SystemState state)
    {
        
        if(!_IsPause.IsEmpty) return;
        var grid = SystemAPI.GetSingleton<TurretGrid>();
        var itemsConfigReference = SystemAPI.GetSingleton<ItemsConfigReference>();
        var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
        var query = state.EntityManager.CreateEntityQuery(typeof(ProjectilePrefabElement));
         var settings = SystemAPI.GetSingleton<WorldTime>(); 
        if (query.IsEmptyIgnoreFilter) return;
        
        // Получаем сущность синглтона
        Entity configEntity = query.GetSingletonEntity();
        var job = new TurretJob
        {
            turretGrid = grid,
            TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
            itemsConfigReference=itemsConfigReference,
            HealthDataLookup = SystemAPI.GetComponentLookup<HealthData>(true),
            ShooterTagLookup = SystemAPI.GetComponentLookup<ShooterTag>(true), 
            ProjectilePrefabElementLookUp = SystemAPI.GetBufferLookup<ProjectilePrefabElement>(true),
            ConfigEntity = configEntity,
            DeltaTime = SystemAPI.Time.DeltaTime*settings.SpeedMultiplier,
            ECB = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter()
        };

        job.ScheduleParallel();
    }
    [BurstCompile]
    public partial struct TurretJob : IJobEntity
    {
        [ReadOnly] public TurretGrid turretGrid;
        public ItemsConfigReference itemsConfigReference;
        [ReadOnly] public ComponentLookup<ShooterTag> ShooterTagLookup;
        [ReadOnly] public ComponentLookup<HealthData> HealthDataLookup;
        [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
         [ReadOnly] public BufferLookup<ProjectilePrefabElement> ProjectilePrefabElementLookUp;
        public Entity ConfigEntity;
        
        public float DeltaTime;
        public EntityCommandBuffer.ParallelWriter ECB;

        void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, 
        in BuildingData buildingData,
        in BuildingPosData posData,
        in DynamicBuffer<StorageSlotData> storageSlots,
        ref TurretStats stats, ref TurretTranform trans)
        {
            stats.TimeToCoolDown -= DeltaTime;

            int turretId = buildingData.BuildingUniqueID; 

            if (!turretGrid.EnemyGridMap.ContainsKey(turretId)) return;
            
            bool isShooter = ShooterTagLookup.HasComponent(entity);
            float3 bestTargetPos = float3.zero;
            float bestScore = -1f;
            bool targetFound = false;
            foreach (var enemy in turretGrid.EnemyGridMap.GetValuesForKey(turretId))
            {
                if (!HealthDataLookup.HasComponent(enemy)) continue;
                float hp = HealthDataLookup[enemy].CurrHealth;

                float score = (isShooter && trans.AttacMode == 1) ? 1f : hp;
                if (score > bestScore) 
                { 
                    bestScore = score; 
                    bestTargetPos = TransformLookup[enemy].Position; 
                    targetFound = true; 
                    if (isShooter && trans.AttacMode == 1) break;
                }
            }
            if (targetFound)
            {
                float2 c = posData.center * turretGrid.CellSize;
                float3 turretPos = new float3(c.x, 1, c.y);
                float3 dir = bestTargetPos - turretPos;

                float targetWorldAngle = math.atan2(dir.x, dir.z); 

                targetWorldAngle -= math.PI * 0.5f; 

                float diff = targetWorldAngle - trans.baseRotation;

                diff = math.atan2(math.sin(diff), math.cos(diff));

                float halfAngle = math.radians(stats.Angle * 0.5f);
                diff = math.clamp(diff, -halfAngle, halfAngle);

                trans.rotation.y = trans.baseRotation + diff;
                if (stats.TimeToCoolDown <= 0)
                {
                    if (stats.CurrAmmo > 0)
                    {
                        if(itemsConfigReference.ProjectileStructConfigs.Value.TryGetConfig(stats.AmmoID, out var cfg))
                        {
                            if (!ProjectilePrefabElementLookUp.HasBuffer(ConfigEntity)) return;
                            var enemyPrefabs = ProjectilePrefabElementLookUp[ConfigEntity];
                            Entity prefab = Entity.Null;
                            foreach(var p in enemyPrefabs)
                            {
                                if (p.ID == stats.ProjectilePrefabID)
                                {
                                    prefab = p.PrefabEntity;
                                    break;
                                }
                            }
                            
                            if(prefab == Entity.Null) return;
                            
                            Entity proj = ECB.Instantiate(chunkIndex, prefab);
                            bool isArt = stats.projectileType == ProjectileType.Arch; 
                            
                            var pos = posData.center * turretGrid.CellSize;
                            float dist = math.distance(pos, bestTargetPos.xz);

                            
                            ECB.SetComponent(chunkIndex, proj, new ProjectileData { 
                                StartPos = trans.projectTyleSpawn, 
                                TargetPos = bestTargetPos, 
                                Speed = cfg.Speed, 
                                Damage = cfg.Damage,
                                Radius = cfg.Radius,
                                ArcHeight = isArt ? dist * 0.5f : 0f,
                                Progress = 0
                            });

                            stats.TimeToCoolDown = stats.CoolDown;
                            stats.CurrAmmo-=1;
                        }
                    }
                    else
                    {
                       
                        if (storageSlots.Length > 0)
                        {
                            var slot = storageSlots[0];
                            
                            stats.AmmoID=slot.ItemId;
                            if(itemsConfigReference.ProjectileStructConfigs.Value.TryGetConfig(stats.AmmoID, out var cfg))
                            {
                                
                                stats.CurrAmmo=cfg.AmmoCount;
                            }
                            else stats.CurrAmmo=20;
                            
                            // if (slot.Amount > 0)
                            // {
                                
                            // }

                        }
                    }

                    
                }
            }  
        }
    }

}
