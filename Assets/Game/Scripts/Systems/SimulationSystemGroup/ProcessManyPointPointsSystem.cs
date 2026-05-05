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
        [ReadOnly] public NativeParallelHashMap<int3, int> CellMapBuildingsIDs;
            
        public ComponentLookup<IsBlueprint> IsBluePrintLookUp;
        public ComponentLookup<IsDemolition> IsDemolitionLookUp;
        public BufferLookup<TransitionSlotData> TransitionSlotDataLookUp;

        [ReadOnly] public BufferLookup<ManyPointPointHealthData> SecondBufferLookUp; 

        public EntityCommandBuffer ECB;

        int pointCountInCommand;
            
        public void Execute(Entity entity, 
                            in DynamicBuffer<MapPoint> points,
                            in ProcessManyPointPointsEventTag data)
        {
            if (points.IsEmpty) 
            {
                ECB.DestroyEntity(entity);
                return;
            }
                
            var extraDataMap = new NativeParallelHashMap<int3, ManyPointPointHealthData>(points.Length, Allocator.Temp);

            if (SecondBufferLookUp.HasBuffer(entity))
            {
                var extraBuff = SecondBufferLookUp[entity];
                foreach (var item in extraBuff)
                    extraDataMap.TryAdd(item.pos, item);
            }

            var filteredPoints = new NativeList<int3>(points.Length, Allocator.Temp);

            foreach (var p in points)
            {
                if (!CellMapBuildingsIDs.ContainsKey(p.pos))
                    filteredPoints.Add(p.pos);
            }
                
            if (filteredPoints.IsEmpty)
            {
                ECB.DestroyEntity(entity);
                extraDataMap.Dispose();
                filteredPoints.Dispose();
                return;
            }

            var isBlueprint = IsBluePrintLookUp.HasComponent(entity) && IsBluePrintLookUp.IsComponentEnabled(entity);
            var isDemolition = IsDemolitionLookUp.HasComponent(entity) && IsDemolitionLookUp.IsComponentEnabled(entity);

            var hasTransitSlots = false;
            NativeArray<(int, TransitionSlotData)> items = default;

            if (TransitionSlotDataLookUp.HasBuffer(entity))
            {
                hasTransitSlots = true;

                var buff = TransitionSlotDataLookUp[entity];
                items = new NativeArray<(int, TransitionSlotData)>(buff.Length, Allocator.Temp);

                for (int i = 0; i < buff.Length; i++)
                    items[i] = (buff[i].amount, buff[i]);
            }
                
            ClusterPoints(data.buildingID, filteredPoints, isBlueprint, isDemolition, hasTransitSlots, ref items, extraDataMap);

            extraDataMap.Dispose();
            filteredPoints.Dispose();
            if (items.IsCreated) items.Dispose();

            ECB.DestroyEntity(entity);
        }
            

        private void ClusterPoints(int buildingID,
            NativeList<int3> points,
            bool isBlueprint,
            bool isDemolition,
            bool hasTransitSlots,
            ref NativeArray<(int, TransitionSlotData)> items,
            NativeParallelHashMap<int3, ManyPointPointHealthData> extraDataMap)
        {
            int count = points.Length;
            pointCountInCommand = count;

            var clusterIds = new NativeArray<int>(count, Allocator.Temp);

            for (int i = 0; i < count; i++)
                clusterIds[i] = i;

            // 🔥 3D соседство (6 направлений)
           for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    int3 diff = math.abs(points[i] - points[j]);
                    
                    // Если разница по всем осям не больше 1 (это покроет и прямые, и диагонали)
                    if (diff.x <= 1 && diff.y <= 1 && diff.z <= 1)
                    {
                        int rootI = FindRoot(i, clusterIds);
                        int rootJ = FindRoot(j, clusterIds);

                        if (rootI != rootJ)
                            clusterIds[rootJ] = rootI;
                    }
                }
            }

            var clusters = new NativeParallelHashMap<int, NativeList<int3>>(count, Allocator.Temp);

            for (int i = 0; i < count; i++)
            {
                int root = FindRoot(i, clusterIds);

                if (!clusters.TryGetValue(root, out var list))
                {
                    list = new NativeList<int3>(Allocator.Temp);
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

                ProcessCluster(buildingID, clusterList, isBlueprint, isDemolition, hasTransitSlots, ref items, extraDataMap);

                clusterList.Dispose();
            }

            clusters.Dispose();
            clusterIds.Dispose();
        }


        private int FindRoot(int index, NativeArray<int> parents)
        {
            int root = index;

            while (parents[root] != root)
                root = parents[root];

            return root;
        }


        private void ProcessCluster(
            int buildingID,
            NativeList<int3> clusterPoints,
            bool isBlueprint,
            bool isDemolition,
            bool hasTransitSlots,
            ref NativeArray<(int, TransitionSlotData)> items,
            NativeParallelHashMap<int3, ManyPointPointHealthData> extraDataMap)
        {
            if (clusterPoints.Length == 0) return;

            Entity cmd = ECB.CreateEntity();

            uint hash = math.hash(clusterPoints[0]);
            hash = math.hash(new int4((int)hash,
                clusterPoints[^1].x,
                clusterPoints[^1].y,
                clusterPoints[^1].z));

            ECB.AddComponent(cmd, new CreateManyPointEventTag
            {
                buildingID = buildingID,
                UniqueBuildingID = (int)hash
            });

            var buff = ECB.AddBuffer<MapPoint>(cmd);

            DynamicBuffer<ManyPointPointHealthData> extraBuff = default;

            if (!extraDataMap.IsEmpty)
                extraBuff = ECB.AddBuffer<ManyPointPointHealthData>(cmd);

            if (isBlueprint) ECB.AddComponent<IsBlueprint>(cmd);
            if (isDemolition) ECB.AddComponent<IsDemolition>(cmd);

            foreach (var p in clusterPoints)
            {
                buff.Add(new MapPoint { pos = p });

                if (extraBuff.IsCreated && extraDataMap.TryGetValue(p, out var data))
                    extraBuff.Add(data);
            }

            if (hasTransitSlots)
            {
                var itemBuff = ECB.AddBuffer<TransitionSlotData>(cmd);

                float percent = (float)clusterPoints.Length / pointCountInCommand;

                for (int i = 0; i < items.Length; i++)
                {
                    var pair = items[i];
                    var slot = pair.Item2;

                    int amount = (int)math.ceil(pair.Item1 * percent);
                    amount = math.min(amount, slot.amount);

                    slot.amount = math.max(slot.amount - amount, 0);

                    itemBuff.Add(new TransitionSlotData
                    {
                        itemID = slot.itemID,
                        amount = amount
                    });

                    items[i] = (pair.Item1, slot);
                }
            }
        }
    }
}