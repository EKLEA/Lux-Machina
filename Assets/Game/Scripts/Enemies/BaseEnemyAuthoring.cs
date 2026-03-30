using Unity.Entities;
using UnityEngine;

public class BaseEnemyAuthoring : MonoBehaviour
{
    public string stringId;

    public class EnemyBaker : Baker<BaseEnemyAuthoring>
    {
        public override void Bake(BaseEnemyAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            
            int bakedId = authoring.stringId.GetStableHashCode();
            
            // Добавляем компоненты
            AddComponent(entity, new EnemyStats { id = bakedId }); 
             AddComponent<Prefab>(entity);
            AddBuffer<TakeDamage>(entity);
            AddComponent<HealthData>(entity);
            AddComponent<ForceDestroyTag>(entity);
            AddComponent<SavableTag>(entity);
            AddComponent<LoadInfo>(entity);

            SetComponentEnabled<LoadInfo>(entity, false);
            SetComponentEnabled<ForceDestroyTag>(entity, false);
        }
    }
}