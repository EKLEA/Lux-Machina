using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering; // Для доступа к свету
using Unity.Transforms;


[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial struct TickGeneratorSystem : ISystem
{
    private float accumulator;
    private bool wasDayLastTick;
    private bool isFirstFrame; 
    public void OnCreate(ref SystemState state)
    {
        isFirstFrame=true;
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonRW<WorldTime>(out var timeHandle)) return;
        if (isFirstFrame)
        {
            wasDayLastTick = timeHandle.ValueRO.IsDay;
            isFirstFrame = false;
        }
        Entity mapEntity = SystemAPI.GetSingletonEntity<BuildingMap>();
        var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
        Entity timeHandleEntity = SystemAPI.GetSingletonEntity<WorldTime>();
        float baseTick = timeHandle.ValueRO.baseTick;
        float speedMult = timeHandle.ValueRO.SpeedMultiplier;

        accumulator += SystemAPI.Time.DeltaTime * speedMult;

        while (accumulator >= baseTick)
        {
            timeHandle.ValueRW.CurrentTick++;
            bool isNowDay = timeHandle.ValueRO.IsDay;
            state.EntityManager.SetComponentEnabled<IsTickFrame>(timeHandleEntity, true);
            if (isNowDay != wasDayLastTick) 
            {
                if (isNowDay) 
                {
                    // Сработает ровно в 0.15 (или вашу границу Sunrise)
                    //Debug.Log("Рассвет: Удаляем врагов");
                }
                else 
                {
                    // Сработает ровно в 0.85 (или вашу границу Sunset)
                     //Debug.Log("Закат: Спавним врагов");
                    ecb.SetComponentEnabled<SpawnMobsData>(mapEntity,true);
                    ecb.SetComponentEnabled<SavingMapTag>(mapEntity,true);
                }

                wasDayLastTick = isNowDay;
            }

            accumulator -= baseTick;
        }
    }
}
