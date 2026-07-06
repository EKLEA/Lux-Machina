
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.VisualScripting;
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

        void Execute(
            Entity entity,
            [ChunkIndexInQuery] int chunkIndex,
            in BuildingData buildingData,
            in BuildingPosData posData,
            ref DynamicBuffer<StorageSlotData> storageSlots,
            ref TurretStats stats,
            ref TurretTranform trans)
        {
            stats.TimeToCoolDown -= DeltaTime;

            int turretId = buildingData.BuildingUniqueID;

            if (!turretGrid.EnemyGridMap.ContainsKey(turretId))
                return;

            bool isShooter = ShooterTagLookup.HasComponent(entity);

            float3 bestTargetPos = float3.zero;
            float bestScore = -1f;
            bool targetFound = false;

            foreach (var enemy in turretGrid.EnemyGridMap.GetValuesForKey(turretId))
            {
                if (!HealthDataLookup.HasComponent(enemy))
                    continue;

                float hp = HealthDataLookup[enemy].CurrHealth;

                float score = (isShooter && trans.AttacMode == 1) ? 1f : hp;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTargetPos = TransformLookup[enemy].Position;
                    targetFound = true;

                    if (isShooter && trans.AttacMode == 1)
                        break;
                }
            }

            if (!targetFound)
                return;

            float3 c = posData.center * turretGrid.CellSize;
            float3 turretPos = new float3(c.x, c.y, c.z);

           float3 dir = bestTargetPos - turretPos; 

            // 2. Рассчитываем желаемый угол (Yaw)
            float targetYaw = math.atan2(dir.x, dir.z);

            // Дальнейший код разворота остается прежним
            float diff = targetYaw - trans.rotation.y;
            diff = math.atan2(math.sin(diff), math.cos(diff));

            float maxRotationThisFrame = math.PI * DeltaTime; 
            float step = math.clamp(diff, -maxRotationThisFrame, maxRotationThisFrame);
            float newRotationY = trans.rotation.y + step;

            float angleFromBase = newRotationY - trans.baseRotation;
            angleFromBase = math.atan2(math.sin(angleFromBase), math.cos(angleFromBase));

            float halfAngle = math.radians(stats.Angle * 0.5f);
            angleFromBase = math.clamp(angleFromBase, -halfAngle, halfAngle);

            // Записываем финальный поворот
            trans.rotation.y = trans.baseRotation + angleFromBase;
            float finalRotationY = trans.rotation.y;

            if (stats.CurrAmmo <= 0 && storageSlots.Length > 0)
            {
                for (int i = 0; i < storageSlots.Length; i++)
                {
                    var slot = storageSlots[i];
                    if (slot.Amount > 0)
                    {
                        stats.AmmoID = slot.ItemId;
                        if (itemsConfigReference.ProjectileStructConfigs.Value.TryGetConfig(stats.AmmoID, out var cfg))
                            stats.CurrAmmo = cfg.AmmoCount;
                        else
                            stats.CurrAmmo = 20; 
                        
                        slot.Amount -= 1;
                        storageSlots[i] = slot;
                        break;
                    }
                }
            }

            if (stats.TimeToCoolDown > 0) return;

            float finalDiff = targetYaw - finalRotationY;
            finalDiff = math.atan2(math.sin(finalDiff), math.cos(finalDiff));

            if (math.abs(finalDiff) > math.radians(5f))
                return;

            if (stats.CurrAmmo > 0)
            {
                // Проверяем буфер префабов сначала на ConfigEntity, затем на самой турели (entity)
                Entity targetBufferEntity = Entity.Null;
                if (ProjectilePrefabElementLookUp.HasBuffer(ConfigEntity))
                    targetBufferEntity = ConfigEntity;
                else if (ProjectilePrefabElementLookUp.HasBuffer(entity))
                    targetBufferEntity = entity;

                if (targetBufferEntity == Entity.Null)
                    return;

                var enemyPrefabs = ProjectilePrefabElementLookUp[targetBufferEntity];
                if (enemyPrefabs.Length == 0)
                    return;

                Entity prefab = Entity.Null;

                foreach (var p in enemyPrefabs)
                {
                    if (p.ID == stats.ProjectilePrefabID)
                    {
                        prefab = p.PrefabEntity;
                        break;
                    }
                }

                // ФОЛБЕК: Если конкретный ID префаба не найден, берем первый попавшийся из буфера
                if (prefab == Entity.Null)
                {
                    prefab = enemyPrefabs[0].PrefabEntity;
                }

                if (prefab == Entity.Null)
                    return;

                float speed = 10f;
                float damage = 10f;
                float radius = 0.5f;

                if (itemsConfigReference.ProjectileStructConfigs.Value.TryGetConfig(stats.AmmoID, out var cfg))
                {
                    speed = cfg.Speed;
                    damage = cfg.Damage;
                    radius = cfg.Radius;
                }

               Entity proj = ECB.Instantiate(chunkIndex, prefab);
                bool isArt = stats.projectileType == ProjectileType.Arch;
                float dist = math.distance(turretPos, bestTargetPos);

                // 1. Считаем вектор направления полёта пули
                float3 projectileDir = bestTargetPos - trans.projectTyleSpawn;

                if (!isArt) 
                {
                    projectileDir.y = 0f; 
                }

                // 2. Вычисляем базовый поворот «лицом к цели»
                quaternion baseRotation = math.lengthsq(projectileDir) > 0.001f 
                    ? quaternion.LookRotation(math.normalize(projectileDir), math.up()) 
                    : quaternion.identity;

                // 3. ИСПРАВЛЕНО: Доворачиваем пулю на 90 градусов влево (против часовой стрелки)
                quaternion finalProjectileRotation = math.mul(baseRotation, quaternion.RotateY(math.radians(-90f)));

                // 4. Применяем итоговый поворот к LocalTransform
                ECB.SetComponent(chunkIndex, proj, new LocalTransform
                {
                    Position = trans.projectTyleSpawn,
                    Rotation = finalProjectileRotation,
                    Scale = 0.35f
                });
                // 3. Заполняем ваши данные для логики полёта
                ECB.SetComponent(chunkIndex, proj, new ProjectileData
                {
                    StartPos = trans.projectTyleSpawn,
                    TargetPos = bestTargetPos,
                    Speed = speed,
                    Damage = damage,
                    Radius = radius,
                    ArcHeight = isArt ? dist * 0.5f : 0f,
                    Progress = 0
                });

                stats.TimeToCoolDown = stats.CoolDown;
                stats.CurrAmmo -= 1;
            }
        }

    }


}
