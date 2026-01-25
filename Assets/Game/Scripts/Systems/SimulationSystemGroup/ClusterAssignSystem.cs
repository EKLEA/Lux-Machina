using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
[DisableAutoCreation]

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(EnergySystem))]
[BurstCompile]
public partial struct ClusterAssignSystem : ISystem
{
    
    BuildingConfigReference _buildingConfigs;
    EntityQuery _updateClustersMap;
    EntityQuery _buildingsToAssignInCluster;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BuildingMap>();
        state.RequireForUpdate<ClusterMap>();
        state.RequireForUpdate<BuildingConfigReference>();

        _updateClustersMap= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BuildingMap,ClusterMap>()
            .WithAll<UpdateCLustersTag>()
            .Build(ref state);
        _buildingsToAssignInCluster= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<NeedsClusterAssign>()
            .Build(ref state);

        

        if (SystemAPI.TryGetSingleton<BuildingConfigReference>(out var lib))
        {
            _buildingConfigs = lib;
        }
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var map = SystemAPI.GetSingletonRW<BuildingMap>();
        var clusterMapRW = SystemAPI.GetSingletonRW<ClusterMap>();
        Entity mapEntity = SystemAPI.GetSingletonEntity<BuildingMap>();
        
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        if (!_updateClustersMap.IsEmptyIgnoreFilter)
        {
            var updateTagLookup = SystemAPI.GetComponentLookup<UpdateCLustersTag>(false);
            var needsClusterAssignLookup = SystemAPI.GetComponentLookup<NeedsClusterAssign>(false);
            var roadJob = new RoadClusteringJob
            {
                CellMapBuildingsIDs = map.ValueRO.CellMapBuildingsIDs,
                CellEntityMultiMap=map.ValueRO.CellEntityMultiMap,
                ClusterRoadsPoints = clusterMapRW.ValueRW.roadsPoints,
                clusterIDs = clusterMapRW.ValueRW.UniqueClusterIDs,
                RoadTypeId = _buildingConfigs.roadID,
                MapEntity = mapEntity,
                pointToClusterId = clusterMapRW.ValueRW.pointToClusterId,
                UpdateClusterTagLookup = updateTagLookup,
                ClusterIdLookup=SystemAPI.GetComponentLookup<ClusterId>(false),
            };
            state.Dependency = roadJob.Schedule(state.Dependency);
            var pingJob=new PingBuildingClusterID
            {
                NeedsClusterAssignLookup = needsClusterAssignLookup
            };
            
            state.Dependency = pingJob.Schedule(state.Dependency);
        }

        if (!_buildingsToAssignInCluster.IsEmptyIgnoreFilter)
        {

            var assignJob = new AssignClusterJob
            {
                CellMapEntities = map.ValueRO.CellMapEntites,
                clusterMap = clusterMapRW.ValueRO,
                IsBlueprintLookup = SystemAPI.GetComponentLookup<IsBlueprint>(false),
                IsDemolitionLookup = SystemAPI.GetComponentLookup<IsDemolition>(false),
                ClusterIDLookup = SystemAPI.GetComponentLookup<ClusterId>(false),
                NeedsClusterAssignLookup = SystemAPI.GetComponentLookup<NeedsClusterAssign>(false),
                IsLogicEnabledLookup = SystemAPI.GetComponentLookup<IsLogicEnabled>(false),
                RoadLookup = SystemAPI.GetComponentLookup<RoadTypeBuildingTag>(false),
            };

            state.Dependency  = assignJob.Schedule(state.Dependency );
            ecb.SetComponentEnabled<UpdateClusterSlots>(mapEntity,true);

        }
    }
    [BurstCompile]
    [WithAll(typeof(ClusterId))]
    [WithNone(typeof(RoadTypeBuildingTag))]
    public partial struct PingBuildingClusterID : IJobEntity
    {
        public ComponentLookup<NeedsClusterAssign> NeedsClusterAssignLookup;
        public void Execute(Entity entity )
        {
            NeedsClusterAssignLookup.SetComponentEnabled(entity,true);
        }
    }

    [BurstCompile]
    public struct RoadClusteringJob : IJob
    {
        [ReadOnly] public NativeParallelHashMap<int2, int> CellMapBuildingsIDs;
        [ReadOnly] public NativeParallelMultiHashMap<Entity, int2> CellEntityMultiMap; 
        public NativeParallelMultiHashMap<int, int2> ClusterRoadsPoints;
        
        public Entity MapEntity;
        public ComponentLookup<UpdateCLustersTag> UpdateClusterTagLookup;
        public ComponentLookup<ClusterId> ClusterIdLookup;
        public NativeList<int> clusterIDs;
        public NativeParallelHashMap<int2, int> pointToClusterId;

        public int RoadTypeId;

        public void Execute()
        {
            clusterIDs.Clear();
            ClusterRoadsPoints.Clear();
            pointToClusterId.Clear(); 

            var roadPoints = new NativeParallelHashSet<int2>(CellMapBuildingsIDs.Count(), Allocator.Temp);
            
            foreach (var pair in CellMapBuildingsIDs)
            {
                if (pair.Value == RoadTypeId)
                {
                    roadPoints.Add(pair.Key);
                }
            }
            
            if (roadPoints.IsEmpty) return;

            var currentClusterId = 0;
            var directions = new NativeArray<int2>(4, Allocator.Temp) 
            { 
                [0] = new int2(1,0), [1] = new int2(-1,0), 
                [2] = new int2(0,1), [3] = new int2(0,-1) 
            };

            while (!roadPoints.IsEmpty)
            {
                clusterIDs.Add(currentClusterId); 

                var enumerator = roadPoints.GetEnumerator();
                enumerator.MoveNext();
                var start = enumerator.Current;
                var queue = new NativeQueue<int2>(Allocator.Temp);

                queue.Enqueue(start);
                roadPoints.Remove(start);

                while(queue.TryDequeue(out int2 pos))
                {
                    ClusterRoadsPoints.Add(currentClusterId, pos);
                    pointToClusterId[pos] = currentClusterId;

                    for (int i = 0; i < 4; i++)
                    {
                        var neighbor = pos + directions[i];
                        if (roadPoints.Contains(neighbor))
                        {
                            queue.Enqueue(neighbor);
                            roadPoints.Remove(neighbor);
                        }
                    }
                }

                currentClusterId++;
            }
            foreach (var entityPair in CellEntityMultiMap)
            {
                Entity entity = entityPair.Key;
                int2 pos = entityPair.Value;

                if (pointToClusterId.TryGetValue(pos, out int clusterId))
                {
                 
                    if (ClusterIdLookup.HasComponent(entity))
                    {
                        ClusterIdLookup[entity] = new ClusterId { Value = clusterId };
                    }
                }
            }
            if (UpdateClusterTagLookup.HasComponent(MapEntity))
            {
                UpdateClusterTagLookup.SetComponentEnabled(MapEntity, false);
            }
        }
    }


    [BurstCompile]
    [WithAll(typeof(NeedsClusterAssign))]
    public partial struct AssignClusterJob : IJobEntity
    {
        [ReadOnly] public NativeParallelHashMap<int2, Entity> CellMapEntities;
        
        public ComponentLookup<ClusterId> ClusterIDLookup;
        public ComponentLookup<NeedsClusterAssign> NeedsClusterAssignLookup;
        public ComponentLookup<IsLogicEnabled> IsLogicEnabledLookup;
        public ClusterMap clusterMap;
        
        

        [ReadOnly] public ComponentLookup<IsBlueprint> IsBlueprintLookup;
        [ReadOnly] public ComponentLookup<IsDemolition> IsDemolitionLookup;
        [ReadOnly] public ComponentLookup<RoadTypeBuildingTag> RoadLookup;
        void Execute(
            Entity entity, 
            in BuildingPosData buildingPosData)
        {
            var neighborClusters = new FixedList32Bytes<int>(); 

            for(int x = buildingPosData.LeftCornerPos.x; x < buildingPosData.LeftCornerPos.x + buildingPosData.size.x; x++)
            {
                CheckPoint(new int2(x,buildingPosData.LeftCornerPos.y-1),ref neighborClusters);
                CheckPoint(new int2(x,buildingPosData.LeftCornerPos.y+buildingPosData.size.y),ref neighborClusters);
            }
             for(int y= buildingPosData.LeftCornerPos.y; y < buildingPosData.LeftCornerPos.y + buildingPosData.size.y; y++)
            {
                CheckPoint(new int2(buildingPosData.LeftCornerPos.x-1,y),ref neighborClusters);
                CheckPoint(new int2(buildingPosData.LeftCornerPos.x+buildingPosData.size.x,y),ref neighborClusters);
            }
            
            
            if (neighborClusters.Length == 1)
            {
                if (ClusterIDLookup[entity].Value == neighborClusters[0]) {
                        NeedsClusterAssignLookup.SetComponentEnabled(entity, false);
                        return; 
                    }
                if(IsLogicEnabledLookup.HasComponent(entity)) IsLogicEnabledLookup.SetComponentEnabled(entity,true);
                Debug.Log("true");
                ClusterId data = ClusterIDLookup[entity];
                data.Value = neighborClusters[0];
                ClusterIDLookup[entity] = data;
            }
            else
            {
                ClusterId data = ClusterIDLookup[entity];
                data.Value = -1;
                ClusterIDLookup[entity] = data;
                if(IsLogicEnabledLookup.HasComponent(entity)) IsLogicEnabledLookup.SetComponentEnabled(entity,false);
                NeedsClusterAssignLookup.SetComponentEnabled(entity, true);
            }
        }

        private void CheckPoint(int2 pos, ref FixedList32Bytes<int> list)
        {
            if (clusterMap.pointToClusterId.TryGetValue(pos, out int clusterId))
            {
               if (CellMapEntities.TryGetValue(pos, out Entity roadEntity))
                {
                    if (roadEntity == Entity.Null || roadEntity.Index < 0) return;
                    bool isDemolition = IsDemolitionLookup.HasComponent(roadEntity) && IsDemolitionLookup.IsComponentEnabled(roadEntity);
                    bool isBlueprint = IsBlueprintLookup.HasComponent(roadEntity) && IsBlueprintLookup.IsComponentEnabled(roadEntity);
                    if (!isDemolition && !isBlueprint&& RoadLookup.HasComponent(roadEntity))
                    {
                        if (!list.Contains(clusterId))
                        {
                            if (list.Length < list.Capacity) 
                                list.Add(clusterId);
                        }
                    }
                }
            }
        }
    }
}