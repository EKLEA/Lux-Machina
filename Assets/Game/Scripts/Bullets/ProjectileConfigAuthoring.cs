using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class ProjectileConfigAuthoring : MonoBehaviour
{
    public List<ProjectileAuthoring> allProjectilePrefabs;

    class Baker : Baker<ProjectileConfigAuthoring>
    {
        public override void Bake(ProjectileConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var buffer = AddBuffer<ProjectilePrefabElement>(entity);

            foreach (var p in authoring.allProjectilePrefabs)
            {
                if (p == null) continue;
                buffer.Add(new ProjectilePrefabElement
                {
                    ID = p.stringId.GetStableHashCode(),
                    PrefabEntity = GetEntity(p.gameObject, TransformUsageFlags.Dynamic)
                });
            }
        }
    }
}