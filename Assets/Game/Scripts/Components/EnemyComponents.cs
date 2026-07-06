using Unity.Entities;
using Unity.Rendering;

public struct EnemyStats : IComponentData
{
    public int id;
    public float Speed;
    public float AttackDamage;
    public float AttackInterval;
    public float LastAttackTime; // Таймер
}

public struct EnemyPrefabElement : IBufferElementData
{
    public int ID;
    public Entity PrefabEntity;
}
public enum MovementType
{
    Ground,
    Flying
}
[MaterialProperty("_AnimTime")]
public struct VatTimeComponent : IComponentData
{
    public float Value;
}

// Передает в шейдер оффсет выбранной анимации (например, 0.4 для ходьбы)
[MaterialProperty("_AnimOffset")]
public struct VatOffsetComponent : IComponentData
{
    public float Value;
}

// Компонент логики (ИИ нашего юнита)
public struct UnitAnimationState : IComponentData
{
    public float Speed;               // Скорость проигрывания (например, 1.0)
    public float CurrentStateOffset;  // Сюда будем копировать оффсет нужной анимации
    public float CurrentClipDuration; // Длина текущей анимации в текстуре (например, 0.3)
}