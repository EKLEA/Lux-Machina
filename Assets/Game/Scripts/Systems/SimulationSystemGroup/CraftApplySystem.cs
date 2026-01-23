using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(CraftSystem))]
[BurstCompile]

public partial struct CraftApplySystem : ISystem
{
    
    // EntityQuery _generatorsQuery;
    // EntityQuery _consumerQuery;
    // EntityQuery _generatorsQuery;
    // public void OnCreate(ref SystemState state)
    // {
    //     state.RequireForUpdate<IsConnectedToEnegy>();
    //     _connectCommand= new EntityQueryBuilder(Allocator.Temp)
    //         .WithAll<ConnectEntities,EntityToConnect>()
    //         .Build(ref state);
        
    // }
    public void OnUpdate(ref SystemState state)
    {
       
    }
//     [BurstCompile]
//     public partial struct ConnectEnergyJob : IJobEntity
//     {
       
//     }
}