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
        if (!_processCreateRoadPointsCommandQuery.IsEmpty)
        {
            state.Dependency= new ProcessRoadPoints
            {
                CellMapBuildingsIDs=buildingMapRO.CellMapBuildingsIDs,
                IsBluePrintLookUp=SystemAPI.GetComponentLookup<IsBlueprint>(false),
                IsDemolitionLookUp=SystemAPI.GetComponentLookup<IsDemolition>(false),
                TransitionSlotDataLookUp=SystemAPI.GetBufferLookup<TransitionSlotData>(false),
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
        public ComponentLookup<IsDemolition> IsDemolitionLookUp;
        public BufferLookup<TransitionSlotData> TransitionSlotDataLookUp;
        public EntityCommandBuffer ECB;
        int pointCountInCommand;
        
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
            ClusterPoints(filteredPoints, IsBluePrint,IsDemolition,hasTransitSlots,ref items);
            
            filteredPoints.Dispose();
            items.Dispose();
            ECB.DestroyEntity(entity);
        }
        
        private void ClusterPoints(NativeList<int2> points, bool IsBluePrint, bool IsDemolition, bool hasTransitSlots,ref NativeArray<(int, TransitionSlotData)> items)
        {
            int pointCount = points.Length;
            pointCountInCommand = pointCount;
            NativeArray<int> clusterIds = new NativeArray<int>(pointCount, Allocator.Temp);
            
            for (int i = 0; i < pointCount; i++)
                clusterIds[i] = i;

            // 1. Связываем только соседей (dx+dy == 1)
            // Убираем do-while, одного прохода по парам достаточно для Union-Find
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

            // 2. Группируем точки по их финальному корню
            // Важно: создаем список СНАРУЖИ мапы, чтобы не было проблем с копированием структур
            NativeParallelHashMap<int, NativeList<int2>> clusters = new(pointCount, Allocator.Temp);
            
            for (int i = 0; i < pointCount; i++)
            {
                int root = FindRoot(i, clusterIds);
                
                if (!clusters.TryGetValue(root, out var list))
                {
                    // Создаем новый список для нового кластера
                    list = new NativeList<int2>(Allocator.Temp);
                    list.Add(points[i]);
                    clusters.Add(root, list);
                }
                else
                {
                    // Добавляем в существующий и ОБЯЗАТЕЛЬНО перезаписываем в мапе
                    list.Add(points[i]);
                    clusters[root] = list; 
                }
            }

            // 3. Запускаем создание сущностей
            var enumerator = clusters.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var clusterList = enumerator.Current.Value;
                ProcessCluster(clusterList, IsBluePrint, IsDemolition, hasTransitSlots,ref items);
                clusterList.Dispose(); // Чистим каждый список
            }
            
            clusters.Dispose();
            clusterIds.Dispose();
        }

        
        private int FindRoot(int index, NativeArray<int> parents)
        {
            // Классический FindRoot без лишних мутаций внутри условия
            int root = index;
            while (parents[root] != root)
            {
                root = parents[root];
            }
            return root;
        }
        
        private void ProcessCluster(NativeList<int2> clusterPoints,bool IsBluePrint,bool IsDemolition,bool hasTransitSlots,ref NativeArray<(int, TransitionSlotData)> items)
        {
            if (clusterPoints.Length == 0) return;
           
            Entity createRoadCommand = ECB.CreateEntity();
            
            uint hash = math.hash(clusterPoints[0]);
            hash = math.hash(new int3((int)hash, clusterPoints[clusterPoints.Length - 1].x, clusterPoints[clusterPoints.Length - 1].y));
            ECB.AddComponent(createRoadCommand,new CreateRoadEventTag{UniqueBuildingID=(int)hash});

            
            var buff = ECB.AddBuffer<MapPoint>(createRoadCommand);
            if(IsBluePrint) ECB.AddComponent<IsBlueprint>(createRoadCommand);
            if(IsDemolition) ECB.AddComponent<IsDemolition>(createRoadCommand);
            foreach(var p in clusterPoints)
            {
                buff.Add(new MapPoint{pos=p});
            }
            if (hasTransitSlots)
            {
                var itemBuff=ECB.AddBuffer<TransitionSlotData>(createRoadCommand);
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