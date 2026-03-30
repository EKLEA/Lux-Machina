using Unity.Entities;

[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial struct TickCleanerSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var query = SystemAPI.QueryBuilder().WithAll<IsTickFrame>().Build();
        if (!query.IsEmpty)
        {
            state.EntityManager.SetComponentEnabled<IsTickFrame>(query,false);
        }
    }
}
