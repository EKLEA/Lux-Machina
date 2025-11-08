using Unity.Collections;
using Unity.Entities;

public partial struct EventCleanupSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        //CleanEmptyEntities(ref state, ecb);
        CleanProcessedBuildingEvents(ref state, ecb);

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    // void CleanEmptyEntities(ref SystemState state, EntityCommandBuffer ecb)
    // {
    //     var query = new EntityQueryBuilder(Allocator.Temp)
    //         .WithNone<IComponentData, IBufferElementData>()
    //         .Build(ref state);

    //     var entities = query.ToEntityArray(Allocator.Temp);
    //     foreach (var entity in entities)
    //     {
    //         ecb.DestroyEntity(entity);
    //     }
    //     entities.Dispose();
    // }
    void CleanProcessedBuildingEvents(ref SystemState state, EntityCommandBuffer ecb)
    {
        foreach (
            var (_, entity) in SystemAPI
                .Query<CreateBuildingEventComponent>()
                .WithNone<AddToMapTag, CreateVisualTag>()
                .WithEntityAccess()
        )
        {
            ecb.DestroyEntity(entity);
        }
    }
}
