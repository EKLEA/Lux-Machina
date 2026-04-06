
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
    EntityQuery _updateEnergyBuildings;
    EntityQuery _updateBuildings;
    EntityQuery _linkEnergyBuildings;
    EntityQuery _unlinkEnergyBuildings;
    EntityQuery _UpdateMapLinks;
    public void OnCreate(ref SystemState state)
    {
        _linkEnergyBuildings= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<LinkNetworkEnergyTo>()
            .WithNone<CreateBuildingEventData>()
            .Build(ref state);
        _unlinkEnergyBuildings= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<UnLinkNetworkEnergyTo>()
            .Build(ref state);
        _UpdateMapLinks= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<UpdateConnectionsTag,EnergyMap>()
            .Build(ref state);
        _updateEnergyBuildings= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<UpdateConnectStatus,EnergyBuildingData>()
            .Build(ref state);
        _updateBuildings= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<UpdateConnectStatus,BuildingPosData>()
            .WithNone<EnergyBuildingData>()
            .Build(ref state);
    }
    public void OnUpdate(ref SystemState state)
    {
        var energyMap = SystemAPI.GetSingletonRW<EnergyMap>();
        var buildingMap = SystemAPI.GetSingletonRW<BuildingMap>();
        var mapEntity = SystemAPI.GetSingletonEntity<BuildingMap>();
        var entitiesDictionary = SystemAPI.GetSingleton<EntitiesDictionary>();
        
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        var parallelEcb = ecb.AsParallelWriter(); 

        if (!_unlinkEnergyBuildings.IsEmpty)
        {
            state.Dependency = new UnLinkEnergyBuilding {
                ECB = ecb,
                entitiesDictionary = entitiesDictionary,
                energyMap = energyMap.ValueRW,
                mapEntity = mapEntity,
                EnergyBuildingDataLookup = state.GetComponentLookup<EnergyBuildingData>(false)
            }.Schedule(state.Dependency);
        }

        if (!_linkEnergyBuildings.IsEmpty)
        {
            state.Dependency = new LinkEnergyBuilding {
                ECB = ecb,
                entitiesDictionary = entitiesDictionary,
                energyMap = energyMap.ValueRW,
                mapEntity = mapEntity,
                EnergyBuildingDataLookup = state.GetComponentLookup<EnergyBuildingData>(false)
            }.Schedule(state.Dependency);
        }

        if (!_UpdateMapLinks.IsEmpty)
        {
            state.Dependency = new AnalyzeEnergyNetworkJob {
                CoreID = energyMap.ValueRO.CoreID,
                mapEntity=mapEntity,
                EnergyLinks = energyMap.ValueRO.EnergyLinks,
                AllEntities = entitiesDictionary.Entities,
                IsConnectedToEnegyLookup = state.GetComponentLookup<IsConnectedToEnergy>(false),
                BuildingStateDataLookup = state.GetComponentLookup<BuildingStateData>(false),
                SwitchIsOffLookup = state.GetComponentLookup<SwitchIsOff>(true),
                ECB = ecb
            }.Schedule(state.Dependency);
        }

        if (!_updateEnergyBuildings.IsEmpty)
        {
            state.Dependency = new UdpateEnergyBuilding {
                ECB = parallelEcb, 
                energyMap = energyMap.ValueRO,
                buildingMap = buildingMap.ValueRO,
                mapEntity=mapEntity,
                EntitiesDictionary = entitiesDictionary,
                UpdateConnectStatusLookup = state.GetComponentLookup<UpdateConnectStatus>(true),
                IsConnectedToEnergyLookup = state.GetComponentLookup<IsConnectedToEnergy>(true),
                SwitchIsOffLookup = state.GetComponentLookup<SwitchIsOff>(true)
            }.ScheduleParallel(state.Dependency); 
        }

        if (!_updateBuildings.IsEmpty)
        {
            state.Dependency = new UpdateBuildingConnectionStatus {
                ECB = ecb,
                energyMap = energyMap.ValueRO,
                IsConnectedToEnegyLookup = state.GetComponentLookup<IsConnectedToEnergy>(false)
            }.Schedule(state.Dependency);
        }
    }
    [BurstCompile]
    public partial struct UnLinkEnergyBuilding : IJobEntity
    {
        public EntityCommandBuffer ECB;
        public EntitiesDictionary entitiesDictionary; 
        public EnergyMap energyMap;
        public Entity mapEntity;
        public ComponentLookup<EnergyBuildingData> EnergyBuildingDataLookup;

        public void Execute(Entity entity, in DynamicBuffer<UnLinkNetworkEnergyTo> Buff)
        {
            foreach (var b in Buff)
            {
                if (!entitiesDictionary.Entities.ContainsKey(b.UnLinkFromBuilding.y) || 
                    !entitiesDictionary.Entities.ContainsKey(b.UnLinkToBuilding.y)) continue;
                if (!energyMap.EnergyLinks.ContainsKey(b.UnLinkFromBuilding)||!energyMap.EnergyLinks.ContainsKey(b.UnLinkToBuilding)) continue;


                var enFrom = entitiesDictionary.Entities[b.UnLinkFromBuilding.y];
                var enTo = entitiesDictionary.Entities[b.UnLinkToBuilding.y];

                if (!EnergyBuildingDataLookup.HasComponent(enFrom) || !EnergyBuildingDataLookup.HasComponent(enTo)) continue;

                var dataFrom = EnergyBuildingDataLookup[enFrom];
                bool changedFrom = false;

                for (int i = 0; i < dataFrom.connections.Length; i++)
                {
                    if (dataFrom.connections[i].Item1 == b.UnLinkFromBuilding.x)
                    {
                        var connection = dataFrom.connections[i];
                        connection.Item2.y = -1; 
                        dataFrom.connections[i] = connection; 
                        changedFrom = true;
                        break;
                    }
                }

                var dataTo = EnergyBuildingDataLookup[enTo];
                bool changedTo = false;

                for (int i = 0; i < dataTo.connections.Length; i++)
                {
                    if (dataTo.connections[i].Item1 == b.UnLinkToBuilding.x)
                    {
                        var connection = dataTo.connections[i];
                        connection.Item2.y = -1; 
                        dataTo.connections[i] = connection; 
                        changedTo = true;
                        break;
                    }
                }

                if (changedFrom) EnergyBuildingDataLookup[enFrom] = dataFrom;
                if (changedTo) EnergyBuildingDataLookup[enTo] = dataTo;

                energyMap.EnergyLinks.Remove(b.UnLinkFromBuilding);
                energyMap.EnergyLinks.Remove(b.UnLinkToBuilding);
                
                ECB.SetComponentEnabled<UpdateConnectStatus>(enFrom, true);
                ECB.SetComponentEnabled<UpdateConnectStatus>(enTo, true);
                ECB.SetComponentEnabled<UpdateConnectionsTag>(mapEntity, true);
            }
            
            ECB.DestroyEntity(entity);
        }
    }
    [BurstCompile]
    [WithNone(typeof(CreateBuildingEventData))]
    public partial struct LinkEnergyBuilding : IJobEntity
    {
        public EntityCommandBuffer ECB;
        public EntitiesDictionary entitiesDictionary;
        public EnergyMap energyMap;
        public Entity mapEntity;
        public ComponentLookup<EnergyBuildingData> EnergyBuildingDataLookup;

        public void Execute(Entity entity, in DynamicBuffer<LinkNetworkEnergyTo> Buff)
        {
            foreach (var b in Buff)
            {
                if (!entitiesDictionary.Entities.ContainsKey(b.LinkFromBuilding.y) || 
                    !entitiesDictionary.Entities.ContainsKey(b.LinkToBuilding.y)) continue;

                var enFrom = entitiesDictionary.Entities[b.LinkFromBuilding.y];
                var enTo = entitiesDictionary.Entities[b.LinkToBuilding.y];

                if (!EnergyBuildingDataLookup.HasComponent(enFrom) || !EnergyBuildingDataLookup.HasComponent(enTo)) continue;

                var dataFrom = EnergyBuildingDataLookup[enFrom];
                var dataTo = EnergyBuildingDataLookup[enTo];

                HandleDisconnect(b.LinkFromBuilding, ref dataFrom);
                HandleDisconnect(b.LinkToBuilding, ref dataTo);

                for (int i = 0; i < dataFrom.connections.Length; i++)
                {
                    if (dataFrom.connections[i].Item1 == b.LinkFromBuilding.x)
                    {
                        dataFrom.connections[i] = (dataFrom.connections[i].Item1, b.LinkToBuilding);
                        break;
                    }
                }

                for (int i = 0; i < dataTo.connections.Length; i++)
                {
                    if (dataTo.connections[i].Item1 == b.LinkToBuilding.x)
                    {
                        dataTo.connections[i] = (dataTo.connections[i].Item1, b.LinkFromBuilding);
                        break;
                    }
                }

                EnergyBuildingDataLookup[enFrom] = dataFrom;
                EnergyBuildingDataLookup[enTo] = dataTo;

                energyMap.EnergyLinks.Remove(b.LinkToBuilding);
                energyMap.EnergyLinks.Remove(b.LinkFromBuilding);
                energyMap.EnergyLinks.TryAdd(b.LinkToBuilding, b.LinkFromBuilding);
                energyMap.EnergyLinks.TryAdd(b.LinkFromBuilding, b.LinkToBuilding);
                
                ECB.SetComponentEnabled<UpdateConnectStatus>(enFrom, true);
                ECB.SetComponentEnabled<UpdateConnectStatus>(enTo, true);
                ECB.SetComponentEnabled<UpdateConnectionsTag>(mapEntity, true);
            }
            ECB.DestroyEntity(entity);
        }

        private void HandleDisconnect(int2 targetNode, ref EnergyBuildingData targetData)
        {
            int2 oldTarget = new int2(-1, -1);
            
            for (int i = 0; i < targetData.connections.Length; i++)
            {
                if (targetData.connections[i].Item1 == targetNode.x)
                {
                    oldTarget = targetData.connections[i].Item2;
                    break;
                }
            }

            if (oldTarget.y != -1 && entitiesDictionary.Entities.ContainsKey(oldTarget.y))
            {
                var oldEntity = entitiesDictionary.Entities[oldTarget.y];
                if (EnergyBuildingDataLookup.HasComponent(oldEntity))
                {
                    var oldData = EnergyBuildingDataLookup[oldEntity];
                    for (int j = 0; j < oldData.connections.Length; j++)
                    {
                        if (oldData.connections[j].Item1 == oldTarget.x)
                        {
                            var conn = oldData.connections[j];
                            conn.Item2 = new int2(oldTarget.x, -1);
                            oldData.connections[j] = conn;
                            break;
                        }
                    }
                    EnergyBuildingDataLookup[oldEntity] = oldData;
                    
                    energyMap.EnergyLinks.Remove(targetNode);
                    energyMap.EnergyLinks.Remove(oldTarget);
                    
                    ECB.SetComponentEnabled<UpdateConnectStatus>(oldEntity, true);
                }
            }
        }
    }
    [BurstCompile]
    public struct AnalyzeEnergyNetworkJob : IJob
    {
        [ReadOnly] public int CoreID;
        
        [ReadOnly] public NativeParallelHashMap<int2, int2> EnergyLinks;
        
        [ReadOnly] public NativeParallelHashMap<int, Entity> AllEntities;
        [ReadOnly] public ComponentLookup<IsConnectedToEnergy> IsConnectedToEnegyLookup;
        
        [ReadOnly] public ComponentLookup<SwitchIsOff> SwitchIsOffLookup;
        [ReadOnly] public ComponentLookup<BuildingStateData> BuildingStateDataLookup;
        
         [ReadOnly] public Entity mapEntity;
        
        public EntityCommandBuffer ECB; 
        
        

       
        public void Execute()
        {
            NativeHashSet<int> ConnectedSet = new(1000, Allocator.Temp);
            if (!AllEntities.ContainsKey(CoreID)) return;

            Entity coreEntity = AllEntities[CoreID];
            bool isCoreOff = SwitchIsOffLookup.HasComponent(coreEntity) && 
                            SwitchIsOffLookup.IsComponentEnabled(coreEntity);

            if (!isCoreOff)
            {
                var queue = new NativeQueue<int>(Allocator.Temp);
                queue.Enqueue(CoreID);
                ConnectedSet.Add(CoreID);

                var tempGraph = new NativeParallelMultiHashMap<int, int>(EnergyLinks.Count() * 2, Allocator.Temp);
                foreach (var pair in EnergyLinks)
                {
                    int2 from = pair.Key;
                    int2 to = pair.Value;
                    if (from.y != -1 && to.y != -1)
                    {
                        tempGraph.Add(from.y, to.y);
                        tempGraph.Add(to.y, from.y);
                    }
                }

                            while (queue.TryDequeue(out int currentID))
                {
                    // Ищем всех соседей текущего узла
                    if (tempGraph.TryGetFirstValue(currentID, out int neighborID, out var it))
                    {
                        do
                        {
                            // ПРОВЕРКА ПРИ ПЕРЕХОДЕ К СОСЕДУ
                            if (AllEntities.TryGetValue(neighborID, out Entity neighborEntity))
                            {
                                bool isNeighborOff = SwitchIsOffLookup.HasComponent(neighborEntity) && 
                                                    SwitchIsOffLookup.IsComponentEnabled(neighborEntity);

                                // Если сосед не выключен и мы его еще не посещали
                                if (!isNeighborOff && ConnectedSet.Add(neighborID))
                                {
                                    queue.Enqueue(neighborID);
                                }
                            }
                        } while (tempGraph.TryGetNextValue(out neighborID, ref it));
                    }
                }
                tempGraph.Dispose();
                queue.Dispose();
            }
            foreach (var entityPair in AllEntities)
            {
                int id = entityPair.Key;
                
                Entity entity = entityPair.Value;
                if(IsConnectedToEnegyLookup.HasComponent(entity))
                {
                    bool isActuallyConnected = ConnectedSet.Contains(id);
                    
                    ECB.SetComponentEnabled<IsConnectedToEnergy>(entity, isActuallyConnected);
                    if (!isActuallyConnected)
                    {
                        if(BuildingStateDataLookup[entity].State>(int)WorkStateEnum.DisconnectedEnergy)
                            ECB.SetComponent(entity,new BuildingStateData{State=(int)(WorkStateEnum.DisconnectedEnergy)});
                    }
                    ECB.SetComponentEnabled<UpdateConnectStatus>(entity, true);
                }
            }   
            
            ECB.SetComponentEnabled<UpdateConnectionsTag>(mapEntity, false);
            ConnectedSet.Dispose();
        }
    }
    [WithNone(typeof(MarkOnMap))]
    [WithAll(typeof(UpdateConnectStatus))]
    [BurstCompile]
    public partial struct UdpateEnergyBuilding: IJobEntity
    {
        [ReadOnly] public EnergyMap energyMap;
        [ReadOnly] public BuildingMap buildingMap;
        public Entity mapEntity;
        [ReadOnly] public EntitiesDictionary EntitiesDictionary;
        public EntityCommandBuffer.ParallelWriter ECB;
        [ReadOnly] public ComponentLookup<UpdateConnectStatus> UpdateConnectStatusLookup;
        [ReadOnly] public ComponentLookup<IsConnectedToEnergy> IsConnectedToEnergyLookup;
        [ReadOnly] public ComponentLookup<SwitchIsOff> SwitchIsOffLookup;

        public void Execute(Entity entity,in BuildingData buildingData, [ChunkIndexInQuery]int index, in EnergyBuildingData data)
        {
            
            var cells = energyMap.EnergyEntityToCellBuildingMap.GetValuesForKey(entity);
            NativeHashSet<Entity> entitiesToUpdate = new NativeHashSet<Entity>((int)(data.radius * data.radius), Allocator.Temp);
            
            foreach (var c in cells)
            {
                if (buildingMap.CellMapEntites.TryGetValue(c, out var buildingEntity)&&UpdateConnectStatusLookup.HasComponent(buildingEntity))
                {
                    entitiesToUpdate.Add(buildingEntity);
                }
            }

            foreach (var en in entitiesToUpdate)
            {
                ECB.SetComponentEnabled<UpdateConnectStatus>(index, en, true);
            }
            entitiesToUpdate.Dispose();
        }
    }

    [BurstCompile] 
    [WithNone(typeof(EnergyBuildingData))]
    public partial struct UpdateBuildingConnectionStatus : IJobEntity
    {
        
         public EntityCommandBuffer ECB; 
        [ReadOnly] public EnergyMap energyMap;
        
        [ReadOnly] public ComponentLookup<IsConnectedToEnergy> IsConnectedToEnegyLookup;
        public void Execute(Entity entity,in BuildingPosData buildingPosData,
                    EnabledRefRW<UpdateConnectStatus> updateStatus)
        {
            NativeHashSet<Entity> energyEntites=new(buildingPosData.size.x*buildingPosData.size.y,Allocator.Temp);
            for (int x = buildingPosData.LeftCornerPos.x; x < buildingPosData.LeftCornerPos.x + buildingPosData.size.x; x++)
            {
                for (int y = buildingPosData.LeftCornerPos.y; y < buildingPosData.LeftCornerPos.y + buildingPosData.size.y; y++)
                {
                    var cell = new int2(x, y);
                    if (energyMap.CellToEnergyEntityBuildingMap.ContainsKey(cell))
                    {
                        var entities=energyMap.CellToEnergyEntityBuildingMap.GetValuesForKey(cell);
                        foreach(var en in entities)
                        {
                            if(IsConnectedToEnegyLookup.HasComponent(en)&&IsConnectedToEnegyLookup.IsComponentEnabled(en)) energyEntites.Add(en);
                        }
                    }
                }
            }
            ECB.SetComponentEnabled<IsConnectedToEnergy>(entity,energyEntites.Count>0);
            updateStatus.ValueRW=false;
            energyEntites.Dispose();
        }
    }

}