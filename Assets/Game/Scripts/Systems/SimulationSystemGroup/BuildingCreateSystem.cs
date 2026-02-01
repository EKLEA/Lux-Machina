using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
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
    EntityArchetype _propBuildingArchetype;
    EntityArchetype _prodecerBuildingArchetype;
    EntityArchetype _consumerBuildingArchetype;
    EntityArchetype _processorBuildingArchetype;
    EntityArchetype _storageBuildingArchetype;
    EntityArchetype _defenceBuildingArchetype;
    EntityArchetype _createVisualCommand;
    
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
            typeof(SaveInfo),
            typeof(LoadInfo),
            typeof(DestroyVisualTag),
            
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
            typeof(SaveInfo),
            typeof(LoadInfo),
            typeof(DestroyVisualTag),
            
            typeof(ForceDestroyTag));

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
            typeof(SaveInfo),
            typeof(LoadInfo),
            typeof(DestroyVisualTag),
            
            typeof(ForceDestroyTag));

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
            typeof(IsConnectedToEnegy),
            typeof(ClusterLink),
            typeof(NeedsClusterAssign),
            typeof(CanCraft),
            typeof(IsLogicEnabled),
            typeof(BuildingOnSceneReference),
            typeof(MarkOnMap),
            
            typeof(CreateVisualTag),
            typeof(DestroyVisualTag),
            typeof(SaveInfo),
            typeof(LoadInfo),
            
            typeof(ForceDestroyTag));

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
            typeof(IsConnectedToEnegy),
            typeof(ClusterLink),
            typeof(NeedsClusterAssign),
            typeof(CanCraft),
            typeof(IsLogicEnabled),
            typeof(BuildingOnSceneReference),
            typeof(MarkOnMap),
            
            typeof(CreateVisualTag),
            typeof(DestroyVisualTag),
            typeof(SaveInfo),
            typeof(LoadInfo),
            
            typeof(ForceDestroyTag));

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
            typeof(IsConnectedToEnegy),
            typeof(ClusterLink),
            typeof(NeedsClusterAssign),
            typeof(CanCraft),
            typeof(IsLogicEnabled),
            typeof(BuildingOnSceneReference),
            typeof(MarkOnMap),
            
            typeof(CreateVisualTag),
            typeof(DestroyVisualTag),
            typeof(SaveInfo),
            typeof(LoadInfo),
            
            typeof(ForceDestroyTag));

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
            typeof(DestroyVisualTag),
            typeof(SaveInfo),
            typeof(LoadInfo),
            
            typeof(ForceDestroyTag) );
       
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
            typeof(CraftingPriorityData),
            typeof(IsConnectedToEnegy),
            //доп компоненты для оружия
            typeof(BuildingRequiredStorageGroupData),
            typeof(ClusterLink),
            typeof(NeedsClusterAssign),
            typeof(IsLogicEnabled),
            typeof(BuildingOnSceneReference),
            typeof(MarkOnMap),
            
            typeof(CreateVisualTag),
            typeof(DestroyVisualTag),
            typeof(SaveInfo),
            typeof(LoadInfo),
            
            typeof(ForceDestroyTag));
        
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
            };
            state.Dependency=CreateRoadJob.Schedule(state.Dependency);
        }
        if (!_createBuildingQuery.IsEmpty)
        {
             var CreateBuildingJob=new CreateBuilding
            {
                ECB=ecb,
                config=_buildingConfigs,
                MapEntity=mapEntity,
                SimpleBuildingArchetype=_simpleBuildingArchetype,
                PropBuildingArchetype=_propBuildingArchetype,
                ProdecerBuildingArchetype=_prodecerBuildingArchetype,
                ConsumerBuildingArchetype=_consumerBuildingArchetype,
                ProcessorBuildingArchetype=_processorBuildingArchetype,
                StorageBuildingArchetype=_storageBuildingArchetype,
                DefenceBuildingArchetype=_defenceBuildingArchetype,
                IsBluePrintLookup=SystemAPI.GetComponentLookup<IsBlueprint>(false),
                IsDemolitionLookup=SystemAPI.GetComponentLookup<IsDemolition>(false),
            };
            state.Dependency=CreateBuildingJob.Schedule(state.Dependency);
        }
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

        public void Execute(
                    Entity entity,
                    in CreateRoadEventTag roadData,
                    in DynamicBuffer<MapPoint> points
        )
        {
            Entity road = ECB.CreateEntity(RoadArchetype);
            var buffer = ECB.AddBuffer<MapPoint>(road);
            
            
            foreach(var p in points)
            {
                buffer.Add(p);
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
            ECB.SetComponentEnabled<DestroyVisualTag>(road, false);
            ECB.SetComponentEnabled<ForceDestroyTag>(road, false);
            
            // 5. Удаляем команду
            ECB.DestroyEntity(entity);
        }
    }

    [BurstCompile]
    public partial struct CreateBuilding : IJobEntity
    {
        public EntityCommandBuffer ECB;
        public BuildingConfigReference config;
        public Entity MapEntity;

        public EntityArchetype SimpleBuildingArchetype;
        public EntityArchetype PropBuildingArchetype;
        public EntityArchetype ProdecerBuildingArchetype;
        public EntityArchetype ConsumerBuildingArchetype;
        public EntityArchetype ProcessorBuildingArchetype;
        public EntityArchetype StorageBuildingArchetype;
        public EntityArchetype DefenceBuildingArchetype;

        [ReadOnly] public ComponentLookup<IsBlueprint> IsBluePrintLookup;
        [ReadOnly] public ComponentLookup<IsDemolition> IsDemolitionLookup;

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

            Entity building;
           

            if (BConfig.buildingType == BuildingsTypes.Prop)
            {
                building = ECB.CreateEntity(PropBuildingArchetype);
            }
            else if (BConfig.typeOfLogic == TypeOfLogic.None)
            {
                building = ECB.CreateEntity(SimpleBuildingArchetype);
            }
            else if (BConfig.typeOfLogic == TypeOfLogic.WorkWithItems &&
                    BConfig.buildingType == BuildingsTypes.Procession)
            {
                config.BuildingProcessionStructConfigs.Value.TryGetConfig(data.buildingID,out var processConfig);
                switch (processConfig.typeOfProcession)
                {
                    case TypeOfProcession.Consumer:
                        building = ECB.CreateEntity(ConsumerBuildingArchetype);
                        ECB.SetComponentEnabled<IsInputCraftEnabled>(building, false);
                        break;

                    case TypeOfProcession.Generate:
                        building = ECB.CreateEntity(ProdecerBuildingArchetype);
                        ECB.SetComponentEnabled<IsOutputCraftEnabled>(building, false);
                        break;

                    case TypeOfProcession.Processing:
                        building = ECB.CreateEntity(ProcessorBuildingArchetype);
                        ECB.SetComponentEnabled<IsInputCraftEnabled>(building, false);
                        ECB.SetComponentEnabled<IsOutputCraftEnabled>(building, false);
                        break;

                    default:
                        ECB.DestroyEntity(entity);
                        return;
                }

                ECB.SetComponent(building,
                    new BuildingRequiredRecipesGroupData
                    { RequiredRecipesGroups = processConfig.requiredRecipesGroups });


                ECB.SetComponentEnabled<IsRecipeAssigned>(building, false);
                ECB.SetComponentEnabled<IsConnectedToEnegy>(building, data.isConnected);
                    
            }
            else
            {
                if (BConfig.buildingType == BuildingsTypes.Defence)
                {
                    building = ECB.CreateEntity(DefenceBuildingArchetype);
                    ECB.SetComponentEnabled<IsConnectedToEnegy>(building, data.isConnected);
                }
                else
                {
                    building = ECB.CreateEntity(StorageBuildingArchetype);
                }
                
                config.BuildingStorageStructConfigs.Value.TryGetConfig(data.buildingID,out var storageConfig);
                ECB.SetComponent(building,
                    new BuildingRequiredStorageGroupData
                    { RequiredStorageGroup = storageConfig.requiredItemTypesGroups });
                

                    
            }
            var size = (data.rotation & 1) != 0
                ? new int2(BConfig.size.z, BConfig.size.x)
                : new int2(BConfig.size.x, BConfig.size.z);

            ECB.SetComponent(building, new BuildingPosData
            {
                LeftCornerPos = data.buildingPosition,
                Rotation = data.rotation,
                size = size
            });
            ECB.SetComponentEnabled<MarkOnMap>(building,true);

            ECB.SetComponent(building, new BuildingData
            {
                BuildingIDHash = data.buildingID,
                BuildingUniqueID = data.UniqueBuildingID
            });

            ECB.SetComponentEnabled<CreateVisualTag>(building, true);
            ECB.SetComponentEnabled<DestroyVisualTag>(building, false);
            ECB.SetComponentEnabled<ForceDestroyTag>(building, false);
            ECB.SetComponent(building, new ClusterLink{ClusterIds=new()});
            ECB.SetComponentEnabled<NeedsClusterAssign>(building, true);

         

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
            
            ECB.SetComponentEnabled<LoadInfo>(building, true);
            
            

            ECB.DestroyEntity(entity);
        }
    }

}