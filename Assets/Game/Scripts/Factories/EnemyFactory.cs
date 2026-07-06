using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Zenject;

public class EnemyFactory : MonoBehaviour
{
    [Inject] IReadOnlyEnemyBaseConfig _enemyBaseConfig;

    public void CreateEnemy(int enemyID, Vector3 pos)
    {
        var world = World.DefaultGameObjectInjectionWorld;
        var em = world.EntityManager;

        var ecbSystem = world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
        var ecb = ecbSystem.CreateCommandBuffer();

        var query = em.CreateEntityQuery(typeof(EnemyPrefabElement));
        if (query.IsEmpty) return;
        
        var buffer = query.GetSingletonBuffer<EnemyPrefabElement>();

        Entity prefabEntity = Entity.Null;
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i].ID == enemyID)
            {
                prefabEntity = buffer[i].PrefabEntity;
                break;
            }
        }
        if (prefabEntity != Entity.Null)
        {
            // Спавним сущность (Unity автоматически скопирует правильный MaterialMeshInfo из префаба)
            Entity newEnemy = ecb.Instantiate(prefabEntity);
            
            // Читаем трансформ префаба (сохраняя скейл 0.2 и поворот 90)
            LocalTransform prefabTransform = em.GetComponentData<LocalTransform>(prefabEntity);
            prefabTransform.Position = pos; // Меняем только позицию
            prefabTransform.Scale=0.2f;
            // Замените строчку с Euler на эту, она работает с градусами надежнее в ECS:
prefabTransform.Rotation = quaternion.AxisAngle(new float3(1, 0, 0), math.radians(90f));

            // Записываем трансформ обратно
            ecb.SetComponent(newEnemy, prefabTransform);
            
            ecb.SetComponentEnabled<ForceDestroyTag>(newEnemy, false);
            
            // Настройки конфигов статистики и здоровья
            if (_enemyBaseConfig.EnemyBaseConfigs.TryGetValue(enemyID, out var config))
            {
                ecb.SetComponent(newEnemy, new EnemyStats
                {
                    id = enemyID,
                    Speed = config.speed,
                    AttackDamage = config.attackDamage,
                    AttackInterval = config.attackInterval,
                    LastAttackTime = config.attackInterval
                });
                ecb.SetComponent(newEnemy, new HealthData
                {
                    CurrHealth=config.maxHealth,
                    MaxHealth=config.maxHealth,
                    TimeToRestore=config.timeToStartRestore,
                    CurrTimeToRestore=config.timeToStartRestore,
                    RestoreHpPerTick=config.restoreHealthPerSecond
                });
            }
        }
    }
}
