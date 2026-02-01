using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BuildingCreateSystem))]
[BurstCompile]

public partial struct EnergySystem : ISystem
{
    EntityQuery _connectCommand;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<IsConnectedToEnegy>();
        _connectCommand= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ConnectEntities,EntityToConnect>()
            .Build(ref state);
        
    }
    public void OnUpdate(ref SystemState state)
    {
        if (!_connectCommand.IsEmpty)
        {
            state.Dependency=new ConnectEnergyJob().ScheduleParallel(state.Dependency);
        }
    }
    //переделать
    [BurstCompile]
    public partial struct ConnectEnergyJob : IJobEntity
    {
        public void Execute(
                    EnabledRefRW<IsConnectedToEnegy> connectedState,
                    EnabledRefRW<IsLogicEnabled> logicState,
                    EnabledRefRO<IsBlueprint> bluePrintState,
                    EnabledRefRO<IsDemolition> demolitionState)
        {
           connectedState.ValueRW=true;
           if(!bluePrintState.ValueRO&&!demolitionState.ValueRO)logicState.ValueRW=true;
        }
    }
}