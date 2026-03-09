using Unity.Entities;
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
            Entity newEnemy = ecb.Instantiate(prefabEntity);
            
            ecb.SetComponent(newEnemy, LocalTransform.FromPosition(pos));
            ecb.SetComponentEnabled<ForceDestroyTag>(newEnemy,false);
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
