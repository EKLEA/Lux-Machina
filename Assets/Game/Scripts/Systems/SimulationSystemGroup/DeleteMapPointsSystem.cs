using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DestroyBuildingsSystem))]
[BurstCompile]

public partial struct DeleteMapPointsSystem : ISystem
{
    EntityQuery _deleteMapPointsFromMapQuery;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BuildingMap>();
        _deleteMapPointsFromMapQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<DeletePointFromMapTag,MapPoint>()
            .Build(ref state);
        
    }
    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        
        var buildingMapRW = SystemAPI.GetSingletonRW<BuildingMap>();
        Entity mapEntity = SystemAPI.GetSingletonEntity<BuildingMap>();
        if (!_deleteMapPointsFromMapQuery.IsEmptyIgnoreFilter)
        {
            var deleteJoB=new DeletePointsJob
            {
                MapData=buildingMapRW.ValueRW,
                MapEntity=mapEntity,
                UpdateMapTagLookup=SystemAPI.GetComponentLookup<UpdateMapTag>(false),
                UpdateClusterTagLookup=SystemAPI.GetComponentLookup<UpdateCLustersTag>(false),
                ECB=ecb
            };
            state.Dependency=deleteJoB.Schedule(state.Dependency);
        }
    }
    //пока что временно
    [BurstCompile]
    [WithAll(typeof(DeletePointFromMapTag))]
    public partial struct DeletePointsJob : IJobEntity
    {
        public BuildingMap MapData; 
        public EntityCommandBuffer ECB;
        
        public Entity MapEntity;
        public ComponentLookup<UpdateMapTag> UpdateMapTagLookup;
        public ComponentLookup<UpdateCLustersTag> UpdateClusterTagLookup;

        public void Execute(
                    Entity entity,
                    in DynamicBuffer<MapPoint> points
        )
        {
            bool removedPoint=false;
            NativeList<Entity> entitiesToRemove=new(Allocator.Temp);
            NativeList<int2> pointsToRemove=new(Allocator.Temp);;
            foreach (var point in points)
            {
                if(pointsToRemove.Contains(point.pos)) continue;
                if (MapData.CellMapBuildingsIDs.ContainsKey(point.pos))
                {
                    var en = MapData.CellMapEntites[point.pos];
                    foreach (var kvp in MapData.CellMapEntites)
                    {
                        if (kvp.Value == en)
                        {
                            pointsToRemove.Add(kvp.Key);
                        }
                    }
                    entitiesToRemove.Add(en);
                }
            }
            for (int i = 0; i < pointsToRemove.Length; i++)
            {
                MapData.CellMapBuildingsIDs.Remove(pointsToRemove[i]);
                MapData.CellMapEntites.Remove(pointsToRemove[i]);
            }

            for (int i = 0; i < entitiesToRemove.Length; i++)
                ECB.DestroyEntity(entitiesToRemove[i]);

            if (removedPoint)
            {
                UpdateMapTagLookup.SetComponentEnabled(MapEntity, true);
                UpdateClusterTagLookup.SetComponentEnabled(MapEntity, true);
            }
            ECB.DestroyEntity(entity);
        }
    }
}