
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ProcessRoadPointsSystem))]
 [BurstCompile]
public partial struct BuildingCreateSystem : ISystem
{
    BuildingConfigReference _buildingConfigs;
    EntityQuery _createRoadQuery;
    EntityQuery _createBuildingQuery;
    EntityArchetype _roadArchetype;
    EntityArchetype _simpleBuildingArchetype;
    EntityArchetype _energyBuildingArchetype;
    EntityArchetype _propBuildingArchetype;
    EntityArchetype _prodecerBuildingArchetype;
    EntityArchetype _consumerBuildingArchetype;
    EntityArchetype _processorBuildingArchetype;
    EntityArchetype _storageBuildingArchetype;
    EntityArchetype _defenceBuildingArchetype;
    EntityArchetype _coreBuildingArchetype;
    EntityArchetype _createVisualCommand;
    

    ArchetypeInfo _simpleBuildingArchetypeInfo;
    ArchetypeInfo _energyBuildingArchetypeInfo;
    ArchetypeInfo _propBuildingArchetypeInfo;
    ArchetypeInfo _prodecerBuildingArchetypeInfo;
    ArchetypeInfo _consumerBuildingArchetypeInfo;
    ArchetypeInfo _processorBuildingArchetypeInfo;
    ArchetypeInfo _storageBuildingArchetypeInfo;
    ArchetypeInfo  _defenceBuildingArchetypeInfo;
    ArchetypeInfo _coreBuildingArchetypeInfo;
    ArchetypeInfo _createVisualCommandInfo;
    public void OnCreate(ref SystemState state)
    {
        // state.RequireForUpdate<BuildingMap>();
        // state.RequireForUpdate<BuildingConfigReference>();
        if (SystemAPI.TryGetSingleton<BuildingConfigReference>(out var lib))
        {
            _buildingConfigs = lib;
        }
        _roadArchetype=state.EntityManager.CreateArchetype(
            typeof(BuildingTag),
            typeof(LogisticTag),
            typeof(RoadTypeBuildingTag),
            typeof(BuildingData),
            typeof(BuildingStateData),
            typeof(ChangeBluePrintState),
            typeof(IsBlueprint),
            typeof(ChangeDemolitionStateTag),
            typeof(IsDemolition),
            typeof(MapPoint),
            typeof(IsInputConstructionEnabled),
            typeof(IsOutputConstuctionEnabled),
            typeof(ConstructionPriorityData),
            typeof(InputConstructionSlotData),
            typeof(OutputConstructionSlotData),
            typeof(IsConstuctionSlotsAssigned),
            typeof(BuildingOnSceneReference),
            typeof(MarkOnMap),
            typeof(UpdateRoad),
            typeof(ExcessSlotData),
            
            typeof(CreateVisualTag),
            typeof(ClusterLink),
            typeof(SavableTag),
            typeof(LoadInfo),
            typeof(TakeDamage),
            typeof(RoadPointHealthData),
            
            
            typeof(ForceDestroyTag));
        
        _simpleBuildingArchetype=state.EntityManager.CreateArchetype(
            typeof(BuildingTag),
            typeof(BuildingData),
            typeof(BuildingStateData),
            typeof(ChangeBluePrintState),
            typeof(IsBlueprint),
            typeof(ChangeDemolitionStateTag),
            typeof(IsDemolition),
            typeof(BuildingPosData),
            typeof(IsInputConstructionEnabled),
            typeof(IsOutputConstuctionEnabled),
            typeof(ConstructionPriorityData),
            typeof(InputConstructionSlotData),
            typeof(OutputConstructionSlotData),
            typeof(IsConstuctionSlotsAssigned),
            typeof(BuildingOnSceneReference),
            typeof(MarkOnMap),
            typeof(ExcessSlotData),
            typeof(CreateVisualTag),
            typeof(ClusterLink),
            typeof(NeedsClusterAssign),
            typeof(SavableTag),
            typeof(LoadInfo),
            typeof(TakeDamage),
            typeof(HealthData),
            
            typeof(ForceDestroyTag));
        _simpleBuildingArchetypeInfo= new ArchetypeInfo{Archetype=_simpleBuildingArchetype,Types=_simpleBuildingArchetype.GetComponentTypes(Allocator.Persistent)};

        _energyBuildingArchetype=state.EntityManager.CreateArchetype(
            typeof(BuildingTag),
            typeof(BuildingData),
            typeof(EnergyTypeBuildingTag),
            typeof(BuildingStateData),
            typeof(ChangeBluePrintState),
            typeof(IsBlueprint),
            typeof(ChangeDemolitionStateTag),
            typeof(IsDemolition),
            typeof(BuildingPosData),
            typeof(IsInputConstructionEnabled),
            typeof(IsOutputConstuctionEnabled),
            typeof(ConstructionPriorityData),
            typeof(InputConstructionSlotData),
            typeof(OutputConstructionSlotData),
            typeof(IsConstuctionSlotsAssigned),
            typeof(BuildingOnSceneReference),
            typeof(MarkOnMap),
            typeof(ExcessSlotData),
            typeof(CreateVisualTag),
            typeof(ClusterLink),
            typeof(NeedsClusterAssign), 
            typeof(IsConnectedToEnergy),
            typeof(SwitchIsOff),
            typeof(EnergyBuildingData),
            typeof(UpdateConnectStatus),
            typeof(SavableTag),
            typeof(LoadInfo),
            typeof(TakeDamage),
            typeof(HealthData),
            
            typeof(ForceDestroyTag));
        _energyBuildingArchetypeInfo= new ArchetypeInfo{Archetype=_energyBuildingArchetype,Types=_energyBuildingArchetype.GetComponentTypes(Allocator.Persistent)};

        _propBuildingArchetype=state.EntityManager.CreateArchetype(
            typeof(PropTag),
            typeof(BuildingData),
            typeof(BuildingStateData),
            typeof(ChangeBluePrintState),
            typeof(IsBlueprint),
            typeof(ChangeDemolitionStateTag),
            typeof(IsDemolition),
            typeof(BuildingPosData),
            typeof(IsInputConstructionEnabled),
            typeof(InputConstructionSlotData),
            typeof(IsOutputConstuctionEnabled),
            typeof(ConstructionPriorityData),
            typeof(OutputConstructionSlotData),
            typeof(IsConstuctionSlotsAssigned),
            typeof(BuildingOnSceneReference),
            typeof(MarkOnMap),
            typeof(ExcessSlotData),
            typeof(CreateVisualTag),
            typeof(ClusterLink),
            typeof(NeedsClusterAssign),
            typeof(SavableTag),
            typeof(LoadInfo),
            typeof(TakeDamage),
            typeof(HealthData),
            
            typeof(ForceDestroyTag));
        _propBuildingArchetypeInfo= new ArchetypeInfo{Archetype=_propBuildingArchetype,Types=_propBuildingArchetype.GetComponentTypes(Allocator.Persistent)};

        _processorBuildingArchetype=state.EntityManager.CreateArchetype(
            typeof(BuildingTag),
            typeof(ProcessorTypeBuildingTag),
            typeof(BuildingData),
            typeof(BuildingStateData),
            typeof(ChangeBluePrintState),
            typeof(IsBlueprint),
            typeof(ChangeDemolitionStateTag),
            typeof(IsDemolition),
            typeof(BuildingPosData),
            typeof(IsInputConstructionEnabled),
            typeof(IsOutputConstuctionEnabled),
            typeof(ConstructionPriorityData),
            typeof(InputConstructionSlotData),
            typeof(OutputConstructionSlotData),
            typeof(IsConstuctionSlotsAssigned),
            typeof(IsInputCraftEnabled),
            typeof(IsOutputCraftEnabled),
            typeof(CraftingPriorityData),
            typeof(InputSlotData),
            typeof(OutputSlotData),
            typeof(ExcessSlotData),
            typeof(RecipeBuildingData),
            typeof(IsRecipeAssigned),
            typeof(BuildingRequiredRecipesGroupData),
            typeof(CountOfPackInBuildingData),
            typeof(IsConnectedToEnergy),
            typeof(UpdateConnectStatus),
            typeof(ConnectToEnegyEntities),
            typeof(ClusterLink),
            typeof(NeedsClusterAssign),
            typeof(CanCraft),
            typeof(IsLogicEnabled),
            typeof(BuildingOnSceneReference),
            typeof(MarkOnMap),
            typeof(CreateVisualTag),
            
            typeof(SavableTag),
            typeof(LoadInfo),
            typeof(TakeDamage),
            typeof(HealthData),
            
            typeof(ForceDestroyTag));
        _processorBuildingArchetypeInfo= new ArchetypeInfo{Archetype=_processorBuildingArchetype,Types=_processorBuildingArchetype.GetComponentTypes(Allocator.Persistent)};

        _prodecerBuildingArchetype=state.EntityManager.CreateArchetype(
            typeof(BuildingTag),
            typeof(ProducerTypeBuildingTag),
            typeof(BuildingData),
            typeof(BuildingStateData),
            typeof(ChangeBluePrintState),
            typeof(IsBlueprint),
            typeof(ChangeDemolitionStateTag),
            typeof(IsDemolition),
            typeof(BuildingPosData),
            typeof(ResourcesLink),
            typeof(IsInputConstructionEnabled),
            typeof(IsOutputConstuctionEnabled),
            typeof(IsConstuctionSlotsAssigned),
            typeof(ConstructionPriorityData),
            typeof(InputConstructionSlotData),
            typeof(OutputConstructionSlotData),
            typeof(IsOutputCraftEnabled),
            typeof(CraftingPriorityData),
            typeof(OutputSlotData),
            typeof(ExcessSlotData),
            typeof(RecipeBuildingData),
            typeof(IsRecipeAssigned),
            typeof(BuildingRequiredRecipesGroupData),
            typeof(CountOfPackInBuildingData),
             typeof(IsConnectedToEnergy),
            typeof(UpdateConnectStatus),
            typeof(ConnectToEnegyEntities),
            typeof(ClusterLink),
            typeof(NeedsClusterAssign),
            typeof(CanCraft),
            typeof(IsLogicEnabled),
            typeof(BuildingOnSceneReference),
            typeof(MarkOnMap),
            typeof(CreateVisualTag),
            
            typeof(SavableTag),
            typeof(LoadInfo),
            typeof(TakeDamage),
            typeof(HealthData),
            typeof(ForceDestroyTag));
        _prodecerBuildingArchetypeInfo= new ArchetypeInfo{Archetype=_prodecerBuildingArchetype,Types=_prodecerBuildingArchetype.GetComponentTypes(Allocator.Persistent)};

         _consumerBuildingArchetype=state.EntityManager.CreateArchetype(
            typeof(BuildingTag),
            typeof(ProcessorTypeBuildingTag),
            typeof(BuildingData),
            typeof(BuildingStateData),
            typeof(ChangeBluePrintState),
            typeof(IsBlueprint),
            typeof(ChangeDemolitionStateTag),
            typeof(IsDemolition),
            typeof(BuildingPosData),
            typeof(IsInputConstructionEnabled),
            typeof(IsOutputConstuctionEnabled),
            typeof(IsConstuctionSlotsAssigned),
            typeof(ConstructionPriorityData),
            typeof(InputConstructionSlotData),
            typeof(OutputConstructionSlotData),
            typeof(IsInputCraftEnabled),
            typeof(CraftingPriorityData),
            typeof(InputSlotData),
            typeof(ExcessSlotData),
            typeof(RecipeBuildingData),
            typeof(IsRecipeAssigned),
            typeof(BuildingRequiredRecipesGroupData),
            typeof(CountOfPackInBuildingData),
             typeof(IsConnectedToEnergy),
            typeof(UpdateConnectStatus),
            typeof(ConnectToEnegyEntities),
            typeof(ClusterLink),
            typeof(NeedsClusterAssign),
            typeof(CanCraft),
            typeof(IsLogicEnabled),
            typeof(BuildingOnSceneReference),
            typeof(MarkOnMap),
            
            typeof(CreateVisualTag),
            
            typeof(SavableTag),
            typeof(LoadInfo),
            typeof(TakeDamage),
            typeof(HealthData),
            
            typeof(ForceDestroyTag));
       _consumerBuildingArchetypeInfo= new ArchetypeInfo{Archetype=_consumerBuildingArchetype,Types=_consumerBuildingArchetype.GetComponentTypes(Allocator.Persistent)};

        _storageBuildingArchetype=state.EntityManager.CreateArchetype(
            typeof(BuildingTag),
            typeof(StorageTypeBuildingTag),
            typeof(BuildingData),
            typeof(BuildingStateData),
            typeof(ChangeBluePrintState),
            typeof(IsBlueprint),
            typeof(ChangeDemolitionStateTag),
            typeof(IsDemolition),
            typeof(BuildingPosData),
            typeof(IsInputConstructionEnabled),
            typeof(IsOutputConstuctionEnabled),
            typeof(IsConstuctionSlotsAssigned),
            typeof(ConstructionPriorityData),
            typeof(InputConstructionSlotData),
            typeof(OutputConstructionSlotData),
            typeof(CraftingPriorityData),
            typeof(StorageSlotData),
            typeof(ExcessSlotData),
            typeof(BuildingRequiredStorageGroupData),
            typeof(ClusterLink),
            typeof(NeedsClusterAssign),
            typeof(BuildingOnSceneReference),
            typeof(MarkOnMap),
            
            typeof(CreateVisualTag),
            
            typeof(SavableTag),
            typeof(LoadInfo),
            typeof(TakeDamage),
            typeof(HealthData),
            
            typeof(ForceDestroyTag) );
       _storageBuildingArchetypeInfo= new ArchetypeInfo{Archetype=_storageBuildingArchetype,Types=_storageBuildingArchetype.GetComponentTypes(Allocator.Persistent)};
       
        _defenceBuildingArchetype=state.EntityManager.CreateArchetype(
            typeof(BuildingTag),
            typeof(DefenceTypeBuildingTag),
            typeof(BuildingData),
            typeof(BuildingStateData),
            typeof(ChangeBluePrintState),
            typeof(IsBlueprint),
            typeof(ChangeDemolitionStateTag),
            typeof(IsDemolition),
            typeof(BuildingPosData),
            typeof(ExcessSlotData),
            typeof(IsInputConstructionEnabled),
            typeof(IsOutputConstuctionEnabled),
            typeof(IsConstuctionSlotsAssigned),
            typeof(ConstructionPriorityData),
            typeof(InputConstructionSlotData),
            typeof(OutputConstructionSlotData),
            typeof(StorageSlotData),
            typeof(StorageTypeBuildingTag),
            typeof(CraftingPriorityData),
             typeof(IsConnectedToEnergy),
            typeof(UpdateConnectStatus),
            typeof(ConnectToEnegyEntities),
            //доп компоненты для оружия
            
            typeof(TurretStats),
            typeof(TurretTranform),
            
            typeof(BuildingRequiredStorageGroupData),
            typeof(ClusterLink),
            typeof(NeedsClusterAssign),
            typeof(IsLogicEnabled),
            typeof(BuildingOnSceneReference),
            typeof(MarkOnMap),
            
            typeof(CreateVisualTag),
            
            typeof(SavableTag),
            typeof(LoadInfo),
            typeof(TakeDamage),
            typeof(HealthData),
            
            typeof(ForceDestroyTag));
       _defenceBuildingArchetypeInfo= new ArchetypeInfo{Archetype=_defenceBuildingArchetype,Types=_defenceBuildingArchetype.GetComponentTypes(Allocator.Persistent)};
        

        _coreBuildingArchetype=state.EntityManager.CreateArchetype(
            typeof(BuildingTag),
            typeof(BuildingData),
            typeof(BuildingStateData),
            typeof(BuildingPosData),
            typeof(BuildingOnSceneReference),
            typeof(MarkOnMap),
            typeof(ExcessSlotData),
            typeof(CreateVisualTag),
            typeof(ClusterLink),
            typeof(NeedsClusterAssign),
            typeof(SavableTag),
            typeof(LoadInfo),
            typeof(TakeDamage),
            typeof(HealthData),
            typeof(CraftingPriorityData),
            typeof(StorageSlotData),
            typeof(LogisticTag),
            typeof(StorageTypeBuildingTag),
            typeof(EnergyTypeBuildingTag),

            typeof(IsConnectedToEnergy),
            typeof(SwitchIsOff),
            typeof(EnergyBuildingData),
            typeof(UpdateConnectStatus));
       _coreBuildingArchetypeInfo= new ArchetypeInfo{Archetype=_coreBuildingArchetype,Types=_coreBuildingArchetype.GetComponentTypes(Allocator.Persistent)};

        _createRoadQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<CreateRoadEventTag,MapPoint>()
            .Build(ref state);
        _createBuildingQuery= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<CreateBuildingEventData>()
            .Build(ref state);

    }
    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        
        Entity mapEntity = SystemAPI.GetSingletonEntity<BuildingMap>();
       
        if (!_createRoadQuery.IsEmpty)
        {
            var CreateRoadJob=new CreateRoad
            {
                ECB=ecb,
                RoadArchetype=_roadArchetype,
                MapEntity=mapEntity,
                config=_buildingConfigs,
                IsBluePrintLookup=SystemAPI.GetComponentLookup<IsBlueprint>(false),
                IsDemolitionLookup=SystemAPI.GetComponentLookup<IsDemolition>(false),
                TransitionSlotDataLookup=SystemAPI.GetBufferLookup<TransitionSlotData>(false),
                RoadPointHealthDataBufferLookUp=SystemAPI.GetBufferLookup<RoadPointHealthData>(false),
            };
            state.Dependency=CreateRoadJob.Schedule(state.Dependency);
        }
        if (!_createBuildingQuery.IsEmpty)
        {
             var CreateBuildingJob=new CreateBuilding
            {
                ECB=ecb,
                config=_buildingConfigs,
                SimpleBuildingArchetypeInfo=_simpleBuildingArchetypeInfo,
                EnergyBuildingArchetypeInfo=_energyBuildingArchetypeInfo,
                PropBuildingArchetypeInfo=_propBuildingArchetypeInfo,
                ProdecerBuildingArchetypeInfo=_prodecerBuildingArchetypeInfo,
                ConsumerBuildingArchetypeInfo=_consumerBuildingArchetypeInfo,
                ProcessorBuildingArchetypeInfo=_processorBuildingArchetypeInfo,
                StorageBuildingArchetypeInfo=_storageBuildingArchetypeInfo,
                DefenceBuildingArchetypeInfo=_defenceBuildingArchetypeInfo,
                CoreBuildingArchetype=_coreBuildingArchetypeInfo,
                IsBluePrintLookup=SystemAPI.GetComponentLookup<IsBlueprint>(true),
                IsDemolitionLookup=SystemAPI.GetComponentLookup<IsDemolition>(true),
                SwitchIsOffCreateDataLookup=SystemAPI.GetComponentLookup<SwitchIsOffCreateData>(true),
                LinkNetworkEnergyToLookup=SystemAPI.GetBufferLookup<LinkNetworkEnergyTo>(true),
            };
            state.Dependency=CreateBuildingJob.Schedule(state.Dependency);
        }
    }
    void OnDestroy(ref SystemState state)
    {
        
        _simpleBuildingArchetypeInfo.Dispose();
        _energyBuildingArchetypeInfo.Dispose();
        _propBuildingArchetypeInfo.Dispose();
        _prodecerBuildingArchetypeInfo.Dispose();
        _consumerBuildingArchetypeInfo.Dispose();
        _processorBuildingArchetypeInfo.Dispose();
        _storageBuildingArchetypeInfo.Dispose();
        _defenceBuildingArchetypeInfo.Dispose();
        _coreBuildingArchetypeInfo.Dispose();
        _createVisualCommandInfo.Dispose();
    }
    
    [BurstCompile]
    public partial struct CreateRoad : IJobEntity
    {
        public EntityCommandBuffer ECB;
        public EntityArchetype RoadArchetype;
        
        public BuildingConfigReference config;
        public Entity MapEntity;
        
        [ReadOnly] public ComponentLookup<IsBlueprint> IsBluePrintLookup;
        [ReadOnly] public ComponentLookup<IsDemolition> IsDemolitionLookup;
        [ReadOnly] public BufferLookup<TransitionSlotData> TransitionSlotDataLookup;
        [ReadOnly] public BufferLookup<RoadPointHealthData> RoadPointHealthDataBufferLookUp;
        [ReadOnly] public DynamicBuffer<ProjectilePrefabElement> projectilePrefabElements;

        public void Execute(
                    Entity entity,
                    in CreateRoadEventTag roadData,
                    in DynamicBuffer<MapPoint> points
        )
        {
            if(!config.BuildingsBaseConfigs.Value.TryGetConfig(config.roadID,out var rCFG)) return;
            Entity road = ECB.CreateEntity(RoadArchetype);
            var buffer = ECB.AddBuffer<MapPoint>(road);
            
            
            foreach(var p in points)
            {
                buffer.Add(p);
            }
            if (RoadPointHealthDataBufferLookUp.HasBuffer(entity))
            {
                
                var healthBuffer = ECB.AddBuffer<RoadPointHealthData>(road);
                 foreach(var p in RoadPointHealthDataBufferLookUp[entity])
                {
                    healthBuffer.Add(p);
                }
            }
           
            
            ECB.SetComponentEnabled<MarkOnMap>(road,true);


            if (IsBluePrintLookup.HasComponent(entity)&&IsBluePrintLookup.IsComponentEnabled(entity))
            {
                if (TransitionSlotDataLookup.HasBuffer(entity))
                {
                  
                    var buff=ECB.AddBuffer<TransitionSlotData>(road);
                    var slots=TransitionSlotDataLookup[entity];
                    foreach(var sl in slots)
                    {
                        buff.Add(sl);
                    }
                    ECB.SetComponentEnabled<LoadInfo>(road, false);
                }
                ECB.SetComponentEnabled<ChangeBluePrintState>(road, true);
                ECB.SetComponentEnabled<IsBlueprint>(road, false);
                ECB.SetComponentEnabled<IsConstuctionSlotsAssigned>(road, false);
                ECB.SetComponentEnabled<IsInputConstructionEnabled>(road, false);
                ECB.SetComponentEnabled<IsOutputConstuctionEnabled>(road, false);
                ECB.SetComponent(road, new ConstructionPriorityData { ConstructionPriority = 2 });
            }
            else
            {
                ECB.SetComponentEnabled<ChangeBluePrintState>(road, false);
                ECB.SetComponentEnabled<IsBlueprint>(road, false);
            }
            
            if (IsDemolitionLookup.HasComponent(entity)&&IsDemolitionLookup.IsComponentEnabled(entity))
            {
                ECB.SetComponentEnabled<ChangeDemolitionStateTag>(road, true);
                ECB.SetComponentEnabled<IsDemolition>(road, false);
            }
            else
            {
                ECB.SetComponentEnabled<ChangeDemolitionStateTag>(road, false);
                ECB.SetComponentEnabled<IsDemolition>(road, false);
            }
            
            ECB.SetComponentEnabled<LoadInfo>(road, true);
            ECB.SetComponentEnabled<UpdateRoad>(road, false);
            
            ECB.SetComponent(road, new BuildingData { BuildingIDHash = config.roadID, BuildingUniqueID = roadData.UniqueBuildingID });

            ECB.SetComponent(road, new ClusterLink{ClusterIds=new()});

            ECB.SetComponentEnabled<CreateVisualTag>(road, true);
            ECB.SetComponentEnabled<ForceDestroyTag>(road, false);
            
            // 5. Удаляем команду
            ECB.DestroyEntity(entity);
        }
    }
    
    
    [BurstCompile]
    public partial struct CreateBuilding: IJobEntity
    {
        
        public EntityCommandBuffer ECB;
        public BuildingConfigReference config;
        [ReadOnly] public ComponentLookup<IsBlueprint> IsBluePrintLookup;
        [ReadOnly] public ComponentLookup<IsDemolition> IsDemolitionLookup;
        [ReadOnly] public ComponentLookup<SwitchIsOffCreateData> SwitchIsOffCreateDataLookup;
        [ReadOnly] public BufferLookup<LinkNetworkEnergyTo> LinkNetworkEnergyToLookup;
        [ReadOnly] public DynamicBuffer<ProjectilePrefabElement> projectilePrefabElements;
        
        public ArchetypeInfo SimpleBuildingArchetypeInfo;
        public ArchetypeInfo EnergyBuildingArchetypeInfo;
        public ArchetypeInfo PropBuildingArchetypeInfo;
        public ArchetypeInfo ProdecerBuildingArchetypeInfo;
        public ArchetypeInfo ConsumerBuildingArchetypeInfo;
        public ArchetypeInfo ProcessorBuildingArchetypeInfo;
        public ArchetypeInfo StorageBuildingArchetypeInfo;
        public ArchetypeInfo DefenceBuildingArchetypeInfo;
        public ArchetypeInfo CoreBuildingArchetype;
        public void Execute(
            Entity entity,
            in CreateBuildingEventData data)
        {
            if (!config.BuildingsBaseConfigs.Value.TryGetConfig(
                    data.buildingID, out var BConfig))
            {
                ECB.DestroyEntity(entity);
                return;
            }
            
            ArchetypeInfo info=GetBuildingType(BConfig);
            Entity building = ECB.CreateEntity(info.Archetype);
            
            if (building != Entity.Null)
            {
                var size = (data.rotation & 1) != 0
                ? new int2(BConfig.size.z, BConfig.size.x)
                : new int2(BConfig.size.x, BConfig.size.z);

                ECB.SetComponent(building, new BuildingPosData
                {
                    LeftCornerPos = data.buildingPosition,
                    Rotation = data.rotation,
                    size = size,
                    center=data.buildingPosition+(float2)size/2
                });
                ECB.SetComponentEnabled<MarkOnMap>(building,true);

                ECB.SetComponent(building, new BuildingData
                {
                    BuildingIDHash = data.buildingID,
                    BuildingUniqueID = data.UniqueBuildingID
                });
                ECB.SetComponent(building, new HealthData
                {
                    CurrHealth=BConfig.MaxHealth,
                    MaxHealth=BConfig.MaxHealth,
                    CurrTimeToRestore=0,
                    TimeToRestore=BConfig.TimeToRestore,
                    RestoreHpPerTick=BConfig.RestoreHpPerTick,
                });
                    
                ECB.SetComponentEnabled<CreateVisualTag>(building, true);
                ECB.SetComponent(building, new ClusterLink{ClusterIds=new()});
                ECB.SetComponentEnabled<NeedsClusterAssign>(building, true);
                ECB.SetComponentEnabled<LoadInfo>(building, true);

                HandleBase(entity,building,info.Types);
                HandleEnergy(entity,building,info.Types,BConfig.id);
                HandleResources(building,info.Types,BConfig.id);
                HandleDefence(building,info.Types,BConfig.id,ECB);
            }
            ECB.DestroyEntity(entity);
        }
        void HandleBase(Entity entity,Entity building,NativeArray<ComponentType> types)
        {
            if (HasType(types, ComponentType.ReadWrite<ForceDestroyTag>()))
            {
                ECB.SetComponentEnabled<ForceDestroyTag>(building, false);
            }
            if(HasType(types, ComponentType.ReadWrite<IsBlueprint>()))
            {
                if (IsBluePrintLookup.HasComponent(entity)&&IsBluePrintLookup.IsComponentEnabled(entity))
                {
                    ECB.SetComponentEnabled<ChangeBluePrintState>(building, true);
                    ECB.SetComponentEnabled<IsBlueprint>(building, false);
                    ECB.SetComponentEnabled<IsConstuctionSlotsAssigned>(building, false);
                    ECB.SetComponentEnabled<IsInputConstructionEnabled>(building, false);
                    ECB.SetComponentEnabled<IsOutputConstuctionEnabled>(building, false);
                    ECB.SetComponent(building, new ConstructionPriorityData { ConstructionPriority = 2 });
                }
                else
                {
                    ECB.SetComponentEnabled<ChangeBluePrintState>(building, false);
                    ECB.SetComponentEnabled<IsBlueprint>(building, false);
                }
            }
            if(HasType(types, ComponentType.ReadWrite<IsDemolition>()))
            {
                if (IsDemolitionLookup.HasComponent(entity)&&IsDemolitionLookup.IsComponentEnabled(entity))
                {
                    ECB.SetComponentEnabled<ChangeDemolitionStateTag>(building, true);
                    ECB.SetComponentEnabled<IsDemolition>(building, false);
                }
                else
                {
                    ECB.SetComponentEnabled<ChangeDemolitionStateTag>(building, false);
                    ECB.SetComponentEnabled<IsDemolition>(building, false);
                }
            }

        }

        void HandleEnergy(Entity entity,Entity building,NativeArray<ComponentType> types,int buildingID)
        {
            if (HasType(types, ComponentType.ReadWrite<EnergyBuildingData>()))
            {
                if (config.BuildingEnergyStructConfig.Value.TryGetConfig(
                buildingID, out var enConfig))
                {
                    
                    FixedList128Bytes<(int,int2)> connections=new();
                    for(int i=0;i<enConfig.maxConnections;i++)
                            connections.Add((i,-1));
                    if (LinkNetworkEnergyToLookup.HasBuffer(entity) && LinkNetworkEnergyToLookup[entity].Length > 0)
                    {
                        var buff=LinkNetworkEnergyToLookup[entity];
                        foreach(var b in buff)
                        {
                            for(int i = 0; i < enConfig.maxConnections; i++)
                            {
                                if(connections[i].Item1==b.LinkFromBuilding.x)
                                {
                                    var c =connections[i];
                                    c.Item2=b.LinkToBuilding;
                                    connections[i]=c;
                                }
                            }
                        }
                    }
                    ECB.SetComponent<EnergyBuildingData>(building,new EnergyBuildingData{radius=enConfig.radius,maxConnections=enConfig.maxConnections,connections=connections});
                    
                }
                
                ECB.SetComponentEnabled<IsConnectedToEnergy>(building, buildingID==config.CoreID);
            }
            if (HasType(types, ComponentType.ReadWrite<SwitchIsOff>()))
            {
                ECB.SetComponentEnabled<SwitchIsOff>(building,SwitchIsOffCreateDataLookup.HasComponent(entity)&&SwitchIsOffCreateDataLookup[entity].SwitchIsOff);
            }
            if (HasType(types, ComponentType.ReadWrite<IsConnectedToEnergy>()))
            {
                ECB.SetComponentEnabled<IsConnectedToEnergy>(building, buildingID==config.CoreID);
                ECB.SetComponentEnabled<UpdateConnectStatus>(building, true);
            }
        }

        void HandleResources(Entity building,NativeArray<ComponentType> types,int buildingID)
        {
            if (HasType(types, ComponentType.ReadWrite<ProcessorTypeBuildingTag>()))
            {
    
                ECB.SetComponentEnabled<IsInputCraftEnabled>(building, false);
                ECB.SetComponentEnabled<IsOutputCraftEnabled>(building, false);
            }
            else if (HasType(types, ComponentType.ReadWrite<ProducerTypeBuildingTag>()))
            {
                ECB.SetComponentEnabled<IsOutputCraftEnabled>(building, false);
            }
            else if (HasType(types, ComponentType.ReadWrite<ConsumerTypeBuildingTag>()))
            {
                 ECB.SetComponentEnabled<IsInputCraftEnabled>(building, false);
            }
            else if (HasType(types, ComponentType.ReadWrite<BuildingRequiredStorageGroupData>()))
            {
                config.BuildingStorageStructConfigs.Value.TryGetConfig(buildingID,out var storageConfig);
                ECB.SetComponent(building,
                    new BuildingRequiredStorageGroupData
                    { RequiredStorageGroup = storageConfig.requiredItemTypesGroups });
            }

            if (HasType(types, ComponentType.ReadWrite<IsRecipeAssigned>()))
            {
                
                config.BuildingProcessionStructConfigs.Value.TryGetConfig(buildingID,out var processConfig);
                ECB.SetComponent(building,
                    new BuildingRequiredRecipesGroupData
                    { RequiredRecipesGroups = processConfig.requiredRecipesGroups });


                ECB.SetComponentEnabled<IsRecipeAssigned>(building, false);
                ECB.SetComponentEnabled<IsConnectedToEnergy>(building, false);
                
                ECB.SetComponentEnabled<UpdateConnectStatus>(building, true);
            }
        }

        void HandleDefence(Entity building,NativeArray<ComponentType> types,int buildingID,EntityCommandBuffer ecb)
        {
            if (HasType(types, ComponentType.ReadWrite<TurretStats>()))
            {
                if(config.TurretStructConfig.Value.TryGetConfig(buildingID,out var turretStructConfig))
                {
                    switch (turretStructConfig.projectileType)
                    {
                        case ProjectileType.Directly:
                            ecb.AddComponent<ShooterTag>(building);
                            break;
                        case ProjectileType.Arch:
                            ecb.AddComponent<ArtilleryTag>(building);
                            break;
                    }
                    ecb.AddComponent(building, new TurretStats
                    {
                        AttackRange=turretStructConfig.AttackRange,
                        projectileType=turretStructConfig.projectileType,
                        Angle=turretStructConfig.Angle,
                        CoolDown=turretStructConfig.CoolDown,
                        TimeToCoolDown=0,
                        ProjectilePrefabID=turretStructConfig.ProjectilePrefabID,
                    });
                }
            }
        }
        ArchetypeInfo GetBuildingType(BuildingBaseStructConfig BConfig)
        {
            if (BConfig.buildingType == BuildingsTypes.Special)
            {
                if (BConfig.id == config.CoreID)
                    return CoreBuildingArchetype;
            }
            else if(BConfig.buildingType == BuildingsTypes.Prop)
            {
                return PropBuildingArchetypeInfo;
            }
            else if(BConfig.typeOfLogic == TypeOfLogic.None)
            {
                if (BConfig.buildingType == BuildingsTypes.Enegry)
                    return EnergyBuildingArchetypeInfo;
                else
                    return SimpleBuildingArchetypeInfo;
            }
            else if (BConfig.typeOfLogic == TypeOfLogic.WorkWithItems)
            {
                if( BConfig.buildingType == BuildingsTypes.Procession)
                {
                    config.BuildingProcessionStructConfigs.Value.TryGetConfig(BConfig.id,out var processConfig);
                    switch (processConfig.typeOfProcession)
                    {
                        case TypeOfProcession.Consumer:
                            return ConsumerBuildingArchetypeInfo;

                        case TypeOfProcession.Generate:
                            return ProdecerBuildingArchetypeInfo;

                        case TypeOfProcession.Processing:
                            return ProcessorBuildingArchetypeInfo;
                    }
                }
                else if (BConfig.buildingType == BuildingsTypes.Defence)
                {
                    return DefenceBuildingArchetypeInfo;
                }
                else
                {
                    return StorageBuildingArchetypeInfo;
                }
            }

                       
            return SimpleBuildingArchetypeInfo;
        }

        bool HasType(NativeArray<ComponentType> types, ComponentType typeToFind)
        {
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i] == typeToFind)
                    return true;
            }
            return false;
        }
            
    }

    public struct ArchetypeInfo:IDisposable
    {
        public EntityArchetype Archetype;
        public NativeArray<ComponentType> Types;

        public void Dispose()
        {
            Types.Dispose();
        }
    }

}
