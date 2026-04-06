using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
[DisableAutoCreation]

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(MarkBuildingOnMapSystem))]
[BurstCompile]
public partial struct ProcessManyPointPointsSystem : ISystem
{
    EntityQuery _processCreateManyPointPointsCommandQuery;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BuildingMap>();
        _processCreateManyPointPointsCommandQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ProcessManyPointPointsEventTag,MapPoint>()
            .Build(ref state);
    }
    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        
        var buildingMapRO= SystemAPI.GetSingleton<BuildingMap>();
        if (!_processCreateManyPointPointsCommandQuery.IsEmpty)
        {
            state.Dependency= new ProcessManyPointPoints
            {
                CellMapBuildingsIDs=buildingMapRO.CellMapBuildingsIDs,
                IsBluePrintLookUp=SystemAPI.GetComponentLookup<IsBlueprint>(false),
                IsDemolitionLookUp=SystemAPI.GetComponentLookup<IsDemolition>(false),
                TransitionSlotDataLookUp=SystemAPI.GetBufferLookup<TransitionSlotData>(false),
                SecondBufferLookUp=SystemAPI.GetBufferLookup<ManyPointPointHealthData>(true),
                ECB=ecb

            }.Schedule(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct ProcessManyPointPoints : IJobEntity
    {
        [ReadOnly] public NativeParallelHashMap<int2, int> CellMapBuildingsIDs;
        
        public ComponentLookup<IsBlueprint> IsBluePrintLookUp;
        public ComponentLookup<IsDemolition> IsDemolitionLookUp;
        public BufferLookup<TransitionSlotData> TransitionSlotDataLookUp;
        [ReadOnly] public BufferLookup<ManyPointPointHealthData> SecondBufferLookUp; 
        public EntityCommandBuffer ECB;
        int pointCountInCommand;
        
        public void Execute( Entity entity, 
                        in DynamicBuffer<MapPoint> points,in ProcessManyPointPointsEventTag data)
        {
            if (points.IsEmpty) 
            {
                ECB.DestroyEntity(entity);
                return;
            }
            
            NativeParallelHashMap<int2, ManyPointPointHealthData> extraDataMap = new(points.Length, Allocator.Temp);
            if (SecondBufferLookUp.HasBuffer(entity))
            {
                var extraBuff = SecondBufferLookUp[entity];
                foreach (var item in extraBuff) extraDataMap.TryAdd(item.pos, item);
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
            var IsBluePrint =IsBluePrintLookUp.HasComponent(entity)&&IsBluePrintLookUp.IsComponentEnabled(entity);
            var IsDemolition =IsDemolitionLookUp.HasComponent(entity)&&IsDemolitionLookUp.IsComponentEnabled(entity);
            var hasTransitSlots =false;
            NativeArray<(int, TransitionSlotData)> items = default; 
            if (TransitionSlotDataLookUp.HasBuffer(entity))
            {
                hasTransitSlots=true;
                var buff=TransitionSlotDataLookUp[entity];
                items=new(buff.Length,Allocator.Temp);
                for(int i=0;i<buff.Length;i++)
                {
                    items[i]=(buff[i].amount,buff[i]);
                }
            }
            
            ClusterPoints(data.buildingID,filteredPoints, IsBluePrint,IsDemolition,hasTransitSlots,ref items,extraDataMap);

            extraDataMap.Dispose();
            filteredPoints.Dispose();
            items.Dispose();
            ECB.DestroyEntity(entity);
        }
        
        private void ClusterPoints(int buildingID,NativeList<int2> points, bool IsBluePrint, bool IsDemolition, bool hasTransitSlots,ref NativeArray<(int, TransitionSlotData)> items, NativeParallelHashMap<int2, ManyPointPointHealthData> extraDataMap)
        {
            int pointCount = points.Length;
            pointCountInCommand = pointCount;
            NativeArray<int> clusterIds = new NativeArray<int>(pointCount, Allocator.Temp);
            
            for (int i = 0; i < pointCount; i++)
                clusterIds[i] = i;

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
                        if (rootI != rootJ) clusterIds[rootJ] = rootI;
                    }
                }
            }

            NativeParallelHashMap<int, NativeList<int2>> clusters = new(pointCount, Allocator.Temp);
            
            for (int i = 0; i < pointCount; i++)
            {
                int root = FindRoot(i, clusterIds);
                
                if (!clusters.TryGetValue(root, out var list))
                {
                    list = new NativeList<int2>(Allocator.Temp);
                    list.Add(points[i]);
                    clusters.Add(root, list);
                }
                else
                {
                    list.Add(points[i]);
                    clusters[root] = list; 
                }
            }

            var enumerator = clusters.GetEnumerator();
            while (enumerator.MoveNext())
            {
                
                var clusterList = enumerator.Current.Value;
                ProcessCluster(buildingID,clusterList, IsBluePrint, IsDemolition, hasTransitSlots,ref items,extraDataMap);
                clusterList.Dispose(); 
            }
            
            clusters.Dispose();
            clusterIds.Dispose();
        }

        
        private int FindRoot(int index, NativeArray<int> parents)
        {
            int root = index;
            while (parents[root] != root)
            {
                root = parents[root];
            }
            return root;
        }
        
        private void ProcessCluster(int buildingID,NativeList<int2> clusterPoints,bool IsBluePrint,bool IsDemolition,bool hasTransitSlots,ref NativeArray<(int, TransitionSlotData)> items, NativeParallelHashMap<int2, ManyPointPointHealthData> extraDataMap)
        {
            if (clusterPoints.Length == 0) return;
           
            Entity createManyPointCommand = ECB.CreateEntity();
            
            uint hash = math.hash(clusterPoints[0]);
            hash = math.hash(new int3((int)hash, clusterPoints[clusterPoints.Length - 1].x, clusterPoints[clusterPoints.Length - 1].y));
            ECB.AddComponent(createManyPointCommand,new CreateManyPointEventTag{buildingID=buildingID,UniqueBuildingID=(int)hash});

            
            var buff = ECB.AddBuffer<MapPoint>(createManyPointCommand);
            DynamicBuffer<ManyPointPointHealthData> extraBuff = default;
            if (!extraDataMap.IsEmpty) extraBuff = ECB.AddBuffer<ManyPointPointHealthData>(createManyPointCommand);
            if(IsBluePrint) ECB.AddComponent<IsBlueprint>(createManyPointCommand);
            if(IsDemolition) ECB.AddComponent<IsDemolition>(createManyPointCommand);
            foreach(var p in clusterPoints)
            {
                buff.Add(new MapPoint{pos=p});
                if (extraBuff.IsCreated && extraDataMap.TryGetValue(p, out var data))
                {
                    extraBuff.Add(data);
                }
            }
            if (hasTransitSlots)
            {
                var itemBuff=ECB.AddBuffer<TransitionSlotData>(createManyPointCommand);
                float procent=(float)clusterPoints.Length / pointCountInCommand;
                for(int i=0;i<items.Length;i++)
                {
                    var pair=items[i];
                    var slot=pair.Item2;
                    int amount = (int)math.ceil(pair.Item1* procent);
                    amount = math.min(amount, slot.amount); 
                    slot.amount= math.max(slot.amount-amount,0);
                    itemBuff.Add(new TransitionSlotData{itemID=slot.itemID,amount=amount});
                    items[i]=(pair.Item1,slot);
                }
            }
           
        }
    }
}