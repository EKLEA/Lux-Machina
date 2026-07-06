using Unity.Entities;
using UnityEngine;

public class BaseEnemyAuthoring : MonoBehaviour
{
    public string stringId;

    [Header("Настройки VAT Анимации")]
    public float defaultAnimSpeed = 1.0f;
    [Tooltip("Стартовый оффсет первой анимации (например, Idle) из лога бейкера")]
    public float initialClipOffset = 0.0f;
    [Tooltip("Длина первой анимации (например, Idle) из лога бейкера")]
    public float initialClipDuration = 0.4f;

    public class EnemyBaker : Baker<BaseEnemyAuthoring>
    {
        public override void Bake(BaseEnemyAuthoring authoring)
        {
            // Для графических объектов критически важно использовать флаг Renderable,
            // чтобы Unity автоматически создал MaterialMeshInfo и подключил рендеринг.
            Entity entity = GetEntity(TransformUsageFlags.Renderable); 
            
            int bakedId = authoring.stringId.GetStableHashCode();
            
            // --- Ваши старые компоненты ---
            AddComponent(entity, new EnemyStats { id = bakedId }); 
            AddComponent<Prefab>(entity);
            AddBuffer<TakeDamage>(entity);
            AddComponent<HealthData>(entity);
            AddComponent<ForceDestroyTag>(entity);
            AddComponent<SavableTag>(entity);
            AddComponent<LoadInfo>(entity);

            SetComponentEnabled<LoadInfo>(entity, false);
            SetComponentEnabled<ForceDestroyTag>(entity, false);

            // --- Компоненты для VAT анимации ---
            AddComponent(entity, new VatTimeComponent { Value = 0.0f });
            AddComponent(entity, new VatOffsetComponent { Value = authoring.initialClipOffset });

            AddComponent(entity, new UnitAnimationState 
            { 
                Speed = authoring.defaultAnimSpeed,
                CurrentStateOffset = authoring.initialClipOffset,
                CurrentClipDuration = authoring.initialClipDuration
            });

            // --- ПРАВИЛЬНАЯ РЕГИСТРАЦИЯ ЗАВИСИМОСТЕЙ ---
            var renderer = authoring.GetComponent<MeshRenderer>();
            var meshFilter = authoring.GetComponent<MeshFilter>();
            
            if (renderer != null && meshFilter != null)
            {
                // Говорим бейкеру, что наша Entity жестко зависит от этих ассетов.
                // При сборке SubScene движок сам подтянет их ID внутрь MaterialMeshInfo.
                DependsOn(meshFilter.sharedMesh);
                DependsOn(renderer.sharedMaterial);
            }
        }
    }

}
