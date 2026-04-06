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
            .WithAll<UpdateClustersTag>()
            .Build(ref state);
        _buildingsToAssignInCluster= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<NeedsClusterAssign>()
            .WithNone<LogisticTag>()
            .Build(ref state);

        

        if (SystemAPI.TryGetSingleton<BuildingConfigReference>(out var lib))
        {
            _buildingConfigs = lib;
        }
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        bool runUpdateClusters = !_updateClustersMap.IsEmpty;
        bool runAssign = !_buildingsToAssignInCluster.IsEmpty;

        if (!runUpdateClusters && !runAssign) return;
        var map = SystemAPI.GetSingletonRW<BuildingMap>();
        var clusterMapRW = SystemAPI.GetSingletonRW<ClusterMap>();
        Entity mapEntity = SystemAPI.GetSingletonEntity<BuildingMap>();
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
        if (runUpdateClusters)
        {
            // Берем Lookup-ы ОДИН раз перед джобами этого блока
            var updateTagLookup = SystemAPI.GetComponentLookup<UpdateClustersTag>(false);
            var needsAssignLookup = SystemAPI.GetComponentLookup<NeedsClusterAssign>(false);
            var clusterLinkLookup = SystemAPI.GetComponentLookup<ClusterLink>(false);
            var roadJob = new LogisticClusteringJob
            {
                CellMapEntities = map.ValueRO.CellMapEntites,
                CellEntityMultiMap=map.ValueRO.CellEntityMultiMap,
                ClusterLogisticPoints= clusterMapRW.ValueRW.logisticPoints,
                clusterIDs = clusterMapRW.ValueRW.UniqueClusterIDs,
                MapEntity = mapEntity,
                pointToClusterLink = clusterMapRW.ValueRW.pointToClusterId,
                UpdateClusterTagLookup = updateTagLookup,
                ClusterLinkLookup=clusterLinkLookup,
                LogisticTagLookup=SystemAPI.GetComponentLookup<LogisticTag>(false),
            };
            state.Dependency = roadJob.Schedule(state.Dependency);
            state.Dependency = new PingBuildingClusterID
            {
                NeedsClusterAssignLookup = needsAssignLookup
            }.Schedule(state.Dependency);
        }

        if (runAssign)
        {
            var assignJob = new AssignClusterJob
            {
                CellMapEntities = map.ValueRO.CellMapEntites,
                clusterMap = clusterMapRW.ValueRO,
                IsBlueprintLookup = SystemAPI.GetComponentLookup<IsBlueprint>(false),
                IsDemolitionLookup = SystemAPI.GetComponentLookup<IsDemolition>(false),
                NeedsClusterAssignLookup = SystemAPI.GetComponentLookup<NeedsClusterAssign>(false),
                IsLogicEnabledLookup = SystemAPI.GetComponentLookup<IsLogicEnabled>(false),
                RoadLookup = SystemAPI.GetComponentLookup<LogisticTag>(false),
            };

            state.Dependency  = assignJob.Schedule(state.Dependency );
            ecb.SetComponentEnabled<UpdateClusterSlots>(mapEntity,true);

        }
    }
    [BurstCompile]
    [WithAll(typeof(ClusterLink))]
    [WithNone(typeof(ManyPointTypeBuildingTag))]
    public partial struct PingBuildingClusterID : IJobEntity
    {
        public ComponentLookup<NeedsClusterAssign> NeedsClusterAssignLookup;
        public void Execute(Entity entity )
        {
            NeedsClusterAssignLookup.SetComponentEnabled(entity,true);
        }
    }

    [BurstCompile]
    public struct LogisticClusteringJob : IJob
    {
        [ReadOnly] public NativeParallelHashMap<int2, Entity> CellMapEntities;
        [ReadOnly] public NativeParallelMultiHashMap<Entity, int2> CellEntityMultiMap; 
        public NativeParallelMultiHashMap<int, int2> ClusterLogisticPoints;
        
        public Entity MapEntity;
        public ComponentLookup<UpdateClustersTag> UpdateClusterTagLookup;
        public ComponentLookup<LogisticTag> LogisticTagLookup;
        public ComponentLookup<ClusterLink> ClusterLinkLookup;
        public NativeList<int> clusterIDs;
        public NativeParallelHashMap<int2, int> pointToClusterLink;


        public void Execute()
        {
            clusterIDs.Clear();
            ClusterLogisticPoints.Clear();
            pointToClusterLink.Clear(); 

            NativeParallelHashSet<int2> logisticPoints = new NativeParallelHashSet<int2>(CellMapEntities.Count(), Allocator.Temp);
            
            foreach (var pair in CellMapEntities)
            {
                if (LogisticTagLookup.HasComponent(pair.Value))
                {
                    logisticPoints.Add(pair.Key);
                }
            }
            
            if (logisticPoints.IsEmpty) return;

            var currentClusterLink = 0;
            var directions = new NativeArray<int2>(4, Allocator.Temp) 
            { 
                [0] = new int2(1,0), [1] = new int2(-1,0), 
                [2] = new int2(0,1), [3] = new int2(0,-1) 
            };

            while (!logisticPoints.IsEmpty)
            {
                clusterIDs.Add(currentClusterLink); 

                var enumerator = logisticPoints.GetEnumerator();
                enumerator.MoveNext();
                var start = enumerator.Current;
                var queue = new NativeQueue<int2>(Allocator.Temp);

                queue.Enqueue(start);
                logisticPoints.Remove(start);

                while(queue.TryDequeue(out int2 pos))
                {
                    ClusterLogisticPoints.Add(currentClusterLink, pos);
                    pointToClusterLink[pos] = currentClusterLink;

                    for (int i = 0; i < 4; i++)
                    {
                        var neighbor = pos + directions[i];
                        if (logisticPoints.Contains(neighbor))
                        {
                            queue.Enqueue(neighbor);
                            logisticPoints.Remove(neighbor);
                        }
                    }
                }

                currentClusterLink++;
            }
            var entityToClusters = new NativeParallelMultiHashMap<Entity, int>(CellEntityMultiMap.Count(), Allocator.Temp);

            foreach (var entityPair in CellEntityMultiMap)
            {
                if (pointToClusterLink.TryGetValue(entityPair.Value, out int clusterId))
                {
                    entityToClusters.Add(entityPair.Key, clusterId);
                }
            }

            var entities = entityToClusters.GetKeyArray(Allocator.Temp);
            var uniqueEntities = new NativeParallelHashSet<Entity>(entities.Length, Allocator.Temp);
            for(int i = 0; i < entities.Length; i++) uniqueEntities.Add(entities[i]);

            foreach (var entity in uniqueEntities)
            {
                if (ClusterLinkLookup.HasComponent(entity))
                {
                    var link = ClusterLinkLookup[entity];
                    link.ClusterIds.Clear();
                    
                    var clusters = entityToClusters.GetValuesForKey(entity);
                    while(clusters.MoveNext())
                    {
                        if (!link.ClusterIds.Contains(clusters.Current))
                            link.ClusterIds.Add(clusters.Current);
                    }
                    ClusterLinkLookup[entity] = link;
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
    [WithNone(typeof(LogisticTag))]
    public partial struct AssignClusterJob : IJobEntity
    {
        [ReadOnly] public NativeParallelHashMap<int2, Entity> CellMapEntities;
    
        public ComponentLookup<NeedsClusterAssign> NeedsClusterAssignLookup;
        public ComponentLookup<IsLogicEnabled> IsLogicEnabledLookup;
        
        [ReadOnly] public ClusterMap clusterMap;

        [ReadOnly] public ComponentLookup<IsBlueprint> IsBlueprintLookup;
        [ReadOnly] public ComponentLookup<IsDemolition> IsDemolitionLookup;
        [ReadOnly] public ComponentLookup<LogisticTag> RoadLookup;
        void Execute(
            Entity entity, 
            in BuildingPosData buildingPosData,ref ClusterLink clusterLink,ref BuildingStateData buildingStateData)
        {
             var neighborClusters = new FixedList128Bytes<int>(); 

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
            if (IsLogicEnabledLookup.HasComponent(entity))
            {
                IsLogicEnabledLookup.SetComponentEnabled(entity, neighborClusters.Length>0);
            }
            clusterLink.ClusterIds.Clear();
            foreach(var n in neighborClusters)
            {
                clusterLink.ClusterIds.Add(n);
            }
            if (clusterLink.ClusterIds.Length <= 0)
            {
                if(buildingStateData.State>(int) WorkStateEnum.AwaitConntionToCluster) buildingStateData.State=(int)WorkStateEnum.AwaitConntionToCluster;
            }
            // else
            // {
            //      if(buildingStateData.State<=(int) WorkStateEnum.AwaitConntionToCluster)buildingStateData.State=(int)WorkStateEnum.Work;
            // }
            NeedsClusterAssignLookup.SetComponentEnabled(entity, false);
        }

        private void CheckPoint(int2 pos, ref FixedList128Bytes<int> list)
        {
            if (clusterMap.pointToClusterId.TryGetValue(pos, out int clusterId)) 
            {
                if (CellMapEntities.TryGetValue(pos, out Entity roadEntity))
                {
                    if (roadEntity == Entity.Null) return;

                    bool isDemolition = IsDemolitionLookup.HasComponent(roadEntity) && IsDemolitionLookup.IsComponentEnabled(roadEntity);
                    bool isBlueprint = IsBlueprintLookup.HasComponent(roadEntity) && IsBlueprintLookup.IsComponentEnabled(roadEntity);
                    
                    if (!isDemolition && !isBlueprint && RoadLookup.HasComponent(roadEntity))
                    {
                        if(!list.Contains(clusterId)) list.Add(clusterId);
                    }
                }
            }
        }
    }
}