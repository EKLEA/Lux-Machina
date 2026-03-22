
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
    public void OnUpdate(ref SystemState state)
    {
        var grid = SystemAPI.GetSingleton<TurretGrid>();
        var itemsConfigReference = SystemAPI.GetSingleton<ItemsConfigReference>();
        var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
        var query = state.EntityManager.CreateEntityQuery(typeof(ProjectilePrefabElement));
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
            DeltaTime = SystemAPI.Time.DeltaTime,
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
                float3 dir = bestTargetPos - new float3(c.x, 1, c.y);
                
                // 1. Считаем угол на цель в мировых координатах
                float targetAngle = math.atan2(dir.x, dir.z);

                // 2. Определяем центр сектора атаки на основе поворота здания (как в сетке)
                // 1: Z+, 2: X+, 3: Z-, 4: X-
                Debug.Log(posData.Rotation );
                float baseAngle = posData.Rotation switch
                {
                    1 => 0f,
                    2 => math.PI * 0.5f,
                    3 => math.PI,
                    4 => -math.PI * 0.5f,
                    _ => 0f
                };

                // 3. Вычисляем разницу и нормализуем её в диапазон -PI...PI
                float angleDiff = targetAngle - baseAngle;
                angleDiff = math.atan2(math.sin(angleDiff), math.cos(angleDiff));

                // 4. Ограничиваем поворот головы половиной угла из статов (stats.Angle)
                float halfAngleRad = math.radians(stats.Angle * 0.5f);
                float clampedDiff = math.clamp(angleDiff, -halfAngleRad, halfAngleRad);

                // 5. Сохраняем финальный угол (в радианах)
                // Если модель в префабе смотрит боком, добавьте смещение здесь (напр. + math.PI * 0.5f)
                trans.rotation.y = baseAngle + clampedDiff;

                if (stats.TimeToCoolDown <= 0)
                {
                    if (storageSlots.Length > 0)
                    {
                        var slot = storageSlots[0];
                        if(itemsConfigReference.ProjectileStructConfigs.Value.TryGetConfig(slot.ItemId, out var cfg))
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
                            // Дистанция в мировых метрах (без повторного умножения на CellSize)
                            float dist = math.distance(pos, bestTargetPos.xz);

                            // Поворот турели для расчета точки вылета снаряда
                            quaternion turretRot = quaternion.Euler(0, trans.rotation.y, 0);
                            // Точка спавна (база + смещение дула из конфига)
                            float3 spawnPos = new float3(pos.x, 1f, pos.y) + math.rotate(turretRot, trans.projectTyleSpawn);
                            
                            ECB.SetComponent(chunkIndex, proj, new ProjectileData { 
                                StartPos = spawnPos, 
                                TargetPos = bestTargetPos, 
                                Speed = cfg.Speed, 
                                Damage = cfg.Damage,
                                Radius = cfg.Radius,
                                ArcHeight = isArt ? dist * 0.5f : 0f,
                                Progress = 0
                            });

                            stats.TimeToCoolDown = stats.CoolDown;
                        }
                    }
                }
            }  
        }
    }

}
