using Unity.Entities;
using UnityEngine;
using Zenject;

public partial class PhantomObjectSystem : SystemBase
{
    [Inject] PhantomObjectFactory _phantomFactory;
    EndFixedStepSimulationEntityCommandBufferSystem _ecbSystem;

    protected override void OnCreate()
    {
        base.OnCreate();
        _ecbSystem = World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        if (_phantomFactory == null) return;

        var ecb = _ecbSystem.CreateCommandBuffer();
        
        Entities
            .WithAll<MakePhantomTag>()
            .WithNone<PhantomTag>()
            .ForEach((
                Entity entity,
                in GameObjectReference gameObjectRef) =>
            {
                MakePhantom(entity, gameObjectRef.BuildingOnScene, ecb);
            }).WithoutBurst().Run();

        Entities
            .WithAll<RemovePhantomTag>()
            .WithAll<PhantomTag>()
            .ForEach((
                Entity entity,
                in GameObjectReference gameObjectRef) =>
            {
                MakeNormal(entity, gameObjectRef.BuildingOnScene, ecb);
            }).WithoutBurst().Run();
        
        _ecbSystem.AddJobHandleForProducer(this.Dependency);
    }

    void MakePhantom(Entity entity, GameObject gameObject, EntityCommandBuffer ecb)
    {
        if (gameObject != null && gameObject.scene.IsValid())
        {
            _phantomFactory.PhantomizeObject(gameObject);
            ecb.AddComponent<PhantomTag>(entity);
            ecb.RemoveComponent<MakePhantomTag>(entity);
        }
    }

    void MakeNormal(Entity entity, GameObject gameObject, EntityCommandBuffer ecb)
    {
        if (gameObject != null && gameObject.scene.IsValid())
        {
            _phantomFactory.UnPhantomizeObject(gameObject);
            ecb.RemoveComponent<PhantomTag>(entity);
            ecb.RemoveComponent<RemovePhantomTag>(entity);
        }
    }
}

// Компоненты-команды
public struct MakePhantomTag : IComponentData { }
public struct RemovePhantomTag : IComponentData { }