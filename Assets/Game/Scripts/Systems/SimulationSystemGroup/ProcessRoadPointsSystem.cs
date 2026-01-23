using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
[DisableAutoCreation]

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DeleteMapPointsSystem))]
[BurstCompile]
public partial struct ProcessRoadPointsSystem : ISystem
{
    EntityQuery _processCreateRoadPointsCommandQuery;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BuildingMap>();
        _processCreateRoadPointsCommandQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ProcessRoadPointsEventTag,MapPoint>()
            .Build(ref state);
    }
    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        
        var buildingMapRO= SystemAPI.GetSingleton<BuildingMap>();
        if (!_processCreateRoadPointsCommandQuery.IsEmptyIgnoreFilter)
        {
            state.Dependency= new ProcessRoadPoints
            {
                CellMapBuildingsIDs=buildingMapRO.CellMapBuildingsIDs,
                IsBluePrintLookUp=SystemAPI.GetComponentLookup<IsBlueprint>(false),
                ECB=ecb

            }.Schedule(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(ProcessRoadPointsEventTag))]
    public partial struct ProcessRoadPoints : IJobEntity
    {
        [ReadOnly] public NativeParallelHashMap<int2, int> CellMapBuildingsIDs;
        
        public ComponentLookup<IsBlueprint> IsBluePrintLookUp;
        public EntityCommandBuffer ECB;
        
        public void Execute( Entity entity, 
                        in DynamicBuffer<MapPoint> points)
        {
            if (points.IsEmpty) 
            {
                ECB.DestroyEntity(entity);
                return;
            }
            
            NativeList<int2> filteredPoints = new NativeList<int2>(points.Length, Allocator.Temp);
            foreach (var p in points)
            {
                if (!CellMapBuildingsIDs.ContainsKey(p.pos))
                {
                    filteredPoints.Add(p.pos);
                }
            }
            
            if (filteredPoints.IsEmpty)
            {
                ECB.DestroyEntity(entity);
                return;
            }
            var IsBluePrint =IsBluePrintLookUp.IsComponentEnabled(entity);
            ClusterPoints(filteredPoints, IsBluePrint);
            
            filteredPoints.Dispose();
            ECB.DestroyEntity(entity);
        }
        
        private void ClusterPoints(NativeList<int2> points,bool IsBluePrint)
        {
            int pointCount = points.Length;
            NativeArray<int> clusterIds = new NativeArray<int>(pointCount, Allocator.Temp);
            
            for (int i = 0; i < pointCount; i++)
                clusterIds[i] = i;
            
            bool merged;
            do
            {
                merged = false;
                for (int i = 0; i < pointCount; i++)
                {
                    for (int j = i + 1; j < pointCount; j++)
                    {
                        int dx = math.abs(points[i].x - points[j].x);
                        int dy = math.abs(points[i].y - points[j].y);
                        
                        if (dx + dy == 1)
                        {
                            int rootI = FindRoot(i, clusterIds);
                            int rootJ = FindRoot(j, clusterIds);
                            
                            if (rootI != rootJ)
                            {
                                clusterIds[rootJ] = rootI;
                                merged = true;
                            }
                        }
                    }
                }
            } while (merged);
            
            NativeParallelHashMap<int, NativeList<int2>> clusters = 
                new NativeParallelHashMap<int, NativeList<int2>>(32, Allocator.Temp);
            
            for (int i = 0; i < pointCount; i++)
            {
                int root = FindRoot(i, clusterIds);
                
                if (!clusters.TryGetValue(root, out var list))
                {
                    list = new NativeList<int2>(Allocator.Temp);
                    clusters.Add(root, list);
                }
                clusters[root].Add(points[i]);
            }
            
            foreach (var cluster in clusters)
            {
                ProcessCluster(cluster.Value,IsBluePrint);
                cluster.Value.Dispose();
            }
            
            clusters.Dispose();
            clusterIds.Dispose();
        }
        
        private int FindRoot(int index, NativeArray<int> parents)
        {
            while (parents[index] != index)
            {
                parents[index] = parents[parents[index]]; 
                index = parents[index];
            }
            return index;
        }
        
        private void ProcessCluster(NativeList<int2> clusterPoints,bool IsBluePrint)
        {
            if (clusterPoints.Length == 0) return;
            
            Entity createRoadCommand = ECB.CreateEntity();
            ECB.AddComponent<CreateRoadEventTag>(createRoadCommand);
            var buff = ECB.AddBuffer<MapPoint>(createRoadCommand);
            if(IsBluePrint) ECB.AddComponent<IsBlueprint>(createRoadCommand);
            foreach(var p in clusterPoints)
            {
                buff.Add(new MapPoint{pos=p});
            }
           
        }
    }
}