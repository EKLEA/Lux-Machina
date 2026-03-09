using Unity.Entities;

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