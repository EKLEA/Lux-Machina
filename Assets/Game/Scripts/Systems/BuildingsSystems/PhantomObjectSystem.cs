using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class PhantomObjectSystem : SystemBase
{
    private PhantomObjectFactory _phantomFactory;
    
    [Inject]
    public void Construct(PhantomObjectFactory phantomFactory)
    {
        _phantomFactory = phantomFactory;
    }

    protected override void OnUpdate()
    {
        if (_phantomFactory == null) return;

        Entities.WithNone<PhantomTag>().WithAll<PosData>().ForEach((
            Entity entity,
            in PosData posData,
            in GameObjectReference gameObjectRef) =>
        {
            if (posData.IsPhantom)
            {
                MakePhantom(entity, gameObjectRef.BuildingOnScene);
            }
        }).WithoutBurst().Run();

        Entities.WithAll<PhantomTag>().WithAll<PosData>().ForEach((
            Entity entity,
            in PosData posData,
            in GameObjectReference gameObjectRef) =>
        {
            if (!posData.IsPhantom)
            {
                MakeNormal(entity, gameObjectRef.BuildingOnScene);
            }
        }).WithoutBurst().Run();
    }

    private void MakePhantom(Entity entity, GameObject gameObject)
    {
        if (gameObject != null)
        {
            _phantomFactory.PhantomizeObject(gameObject);
            EntityManager.AddComponent<PhantomTag>(entity);
        }
    }

    private void MakeNormal(Entity entity, GameObject gameObject)
    {
        if (gameObject != null)
        {
            _phantomFactory.UnPhantomizeObject(gameObject);
            EntityManager.RemoveComponent<PhantomTag>(entity);
        }
    }
}