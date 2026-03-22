using UnityEngine;
using Unity.Entities;

public class ProjectileAuthoring : MonoBehaviour
{
    public string stringId; // Например: "ArtilleryShell"
    public float baseDamage = 10f;
    public float explosionRadius = 0f; // 0 для обычных пуль, > 0 для артиллерии

    class ProjectileBaker : Baker<ProjectileAuthoring>
    {
        public override void Bake(ProjectileAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            AddComponent(entity, new ProjectileData
            {
                Damage = authoring.baseDamage,
                Radius = authoring.explosionRadius,
                Progress = 0f 
            });

        }
    }
}
