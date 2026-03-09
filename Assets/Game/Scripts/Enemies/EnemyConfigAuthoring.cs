using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class EnemyConfigAuthoring : MonoBehaviour 
{
    public List<BaseEnemyAuthoring> allEnemyPrefabs;

    class Baker : Baker<EnemyConfigAuthoring>
    {
        public override void Bake(EnemyConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var buffer = AddBuffer<EnemyPrefabElement>(entity);
            foreach(var p in authoring.allEnemyPrefabs)
            {
                buffer.Add(new EnemyPrefabElement {
                    ID = p.stringId.GetStableHashCode(),
                    PrefabEntity = GetEntity(p.gameObject, TransformUsageFlags.Dynamic)
                });
            }
        }
    }
}
