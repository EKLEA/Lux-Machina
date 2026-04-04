using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.XR;
using Zenject;
[DisableAutoCreation]
[UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
public partial class BuildingSaveSystem : SystemBase
{
    EntityQuery SaveLoadInfo;
    EntityQuery GameOver;
    public event Action OnGameOver;
    [Inject] IGameStateSaver saveData;
    bool isInvoked;
    protected override void OnCreate()
    {
        SaveLoadInfo= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BuildingMap,SavingMapTag>()
            .Build(World.EntityManager);
        GameOver= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<IsGameOver,BuildingMap>()
            .Build(World.EntityManager);
            isInvoked=false;
    }
    protected override void OnUpdate()
    {
        if(isInvoked) return;
        if (!GameOver.IsEmpty&&!isInvoked)
        {
            OnGameOver?.Invoke();
            isInvoked=true;
        }
        if(SaveLoadInfo.IsEmpty) return;
        var save = new GameStateData(); 

        var time=SystemAPI.GetSingleton<WorldTime>();
        save.IsGameOver=isInvoked;
        save.CurrTick=time.CurrentTick;
        save.TicksPerDay=time.TicksPerDay;
        save.dayLength=time.dayLength;
        // Camerdata
        save.CoreID=saveData.GameState.CoreID;
        save.CorePos=saveData.GameState.CorePos;
        save.EnemyAiConfig=saveData.GameState.EnemyAiConfig;
        var query = SystemAPI.QueryBuilder()
                .WithAll<SpawnMobsData>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                .Build();

        save.SpawnMobsData = query.GetSingleton<SpawnMobsData>();
        BuildingMap buildingMap = SystemAPI.GetSingleton<BuildingMap>();
        Entity mapEn = SystemAPI.GetSingletonEntity<BuildingMap>();
        int initialCount = buildingMap.CellMapEntites.Count();

        var buildings=new NativeParallelHashMap<int,BaseBuildingSaveData>(initialCount,Allocator.TempJob);
        var manyPointsBuildings=new NativeParallelHashMap<int,ManyPointsBuildingSaveData>(initialCount,Allocator.TempJob);
        var constructionSlotsSaveData=new NativeParallelHashMap<int,ConstructionSlotsSaveData>(initialCount,Allocator.TempJob);
        var excessSlotsSaveData=new NativeParallelHashMap<int,ExcessSlotsSaveData>(initialCount,Allocator.TempJob);
        var recipeBuildingSaveData=new NativeParallelHashMap<int,RecipeAndCraftBuildingSaveData>(initialCount,Allocator.TempJob);
        var storageSlotsSaveData=new NativeParallelHashMap<int,StorageSlotsSaveData>(initialCount,Allocator.TempJob);
        var buildingEnergyNetvorkLinkSaveData=new NativeParallelHashMap<int,BuildingEnergyNetvorkLinkSaveData>(initialCount,Allocator.TempJob);
        
        var IsBluePrintLookUp=SystemAPI.GetComponentLookup<IsBlueprint>();
        var IsDemolitionLookUp=SystemAPI.GetComponentLookup<IsDemolition>();

        var IsInputConstructionEnabledLookUp=SystemAPI.GetComponentLookup<IsInputConstructionEnabled>();
        var IsOutputConstuctionEnabledLookUp=SystemAPI.GetComponentLookup<IsOutputConstuctionEnabled>();
        var InputConstructionSlotDataLookUp=SystemAPI.GetBufferLookup<InputConstructionSlotData>();
        var OutputConstructionSlotDataLookUp =SystemAPI.GetBufferLookup<OutputConstructionSlotData>();
        
        var SwitchIsOffLookUp=SystemAPI.GetComponentLookup<SwitchIsOff>();

        var IsInputCraftEnabledLookUp=SystemAPI.GetComponentLookup<IsInputCraftEnabled>();
        var IsOutputCraftEnabledLookUp=SystemAPI.GetComponentLookup<IsOutputCraftEnabled>();
        var InputSlotDataLookUp=SystemAPI.GetBufferLookup<InputSlotData>();
        var OutputSlotDataLookUp =SystemAPI.GetBufferLookup<OutputSlotData>();

       var handle= new BuildingSaveJob{buildings=buildings.AsParallelWriter(),IsBluePrintLookUp=IsBluePrintLookUp,IsDemolitionLookUp=IsDemolitionLookUp}.ScheduleParallel(this.Dependency);
       handle= new RoadSaveJob{manyPointsBuildings=manyPointsBuildings.AsParallelWriter(),IsBluePrintLookUp=IsBluePrintLookUp,IsDemolitionLookUp=IsDemolitionLookUp}.ScheduleParallel(handle);
       handle= new ConstructionSlotsSaveJob{
            constructionSlotsSaveData=constructionSlotsSaveData.AsParallelWriter(),
            InputConstructionSlotDataLookUp=InputConstructionSlotDataLookUp,
            OutputConstructionSlotDataLookUp=OutputConstructionSlotDataLookUp,
            IsInputConstructionEnabledLookUp=IsInputConstructionEnabledLookUp,
            IsOutputConstuctionEnabledLookUp=IsOutputConstuctionEnabledLookUp}.ScheduleParallel(handle);
        handle= new ExcessSlotsSaveJob{excessSlotsSaveData=excessSlotsSaveData.AsParallelWriter()}.ScheduleParallel(handle);
        handle= new RecipeAdnCraftBuildingSaveData{
            recipeBuildingSaveData=recipeBuildingSaveData.AsParallelWriter(),
            InputSlotDataLookUp=InputSlotDataLookUp,
            OutputSlotDataLookUp=OutputSlotDataLookUp,
            IsInputCraftEnabledLookUp=IsInputCraftEnabledLookUp,
            IsOutputCraftEnabledLookUp=IsOutputCraftEnabledLookUp
            }.ScheduleParallel(handle);

            
        handle= new EnegrySaveJob{buildingEnergyNetvorkLinkSaveData=buildingEnergyNetvorkLinkSaveData.AsParallelWriter(),SwitchIsOffLookUp=SwitchIsOffLookUp}.ScheduleParallel(handle);
        handle= new StorageSaveJob{storageSlotsSaveData=storageSlotsSaveData.AsParallelWriter()}.ScheduleParallel(handle);
        handle.Complete();
        this.Dependency=handle;
        save.ResourcesCellsList=saveData.GameState.ResourcesCellsList;
        save.Buildings = new Dictionary<int, BaseBuildingSaveData>();
        foreach (var pair in buildings) {
            save.Buildings.Add(pair.Key, pair.Value);
        }

        save.ManyPointsBuildings = new Dictionary<int, ManyPointsBuildingSaveData>();
        foreach (var pair in manyPointsBuildings) {
            save.ManyPointsBuildings.Add(pair.Key, pair.Value);
        }

        save.constructionSlotsSaveData = new Dictionary<int, ConstructionSlotsSaveData>();
        foreach (var pair in constructionSlotsSaveData) {
            save.constructionSlotsSaveData.Add(pair.Key, pair.Value);
        }

        save.excessSlotsSaveData = new Dictionary<int, ExcessSlotsSaveData>();
        foreach (var pair in excessSlotsSaveData) {
            save.excessSlotsSaveData.Add(pair.Key, pair.Value);
        }

        save.recipeBuildingSaveData = new Dictionary<int, RecipeAndCraftBuildingSaveData>();
        foreach (var pair in recipeBuildingSaveData) {
            save.recipeBuildingSaveData.Add(pair.Key, pair.Value);
        }

        save.storageSlotsSaveData = new Dictionary<int, StorageSlotsSaveData>();
        foreach (var pair in storageSlotsSaveData) {
            save.storageSlotsSaveData.Add(pair.Key, pair.Value);
        }

        save.buildingEnergyNetvorkLinkSaveData = new Dictionary<int, BuildingEnergyNetvorkLinkSaveData>();
        foreach (var pair in buildingEnergyNetvorkLinkSaveData) {
            save.buildingEnergyNetvorkLinkSaveData.Add(pair.Key, pair.Value);
        }
        saveData.SaveGameState(save);
        EntityManager.SetComponentEnabled<SavingMapTag>(mapEn,false);
        buildings.Dispose();
        manyPointsBuildings.Dispose();
        constructionSlotsSaveData.Dispose();
        excessSlotsSaveData.Dispose();
        recipeBuildingSaveData.Dispose();
        storageSlotsSaveData.Dispose();
        buildingEnergyNetvorkLinkSaveData.Dispose();

    }
    partial struct BuildingSaveJob : IJobEntity
    {
        public NativeParallelHashMap<int,BaseBuildingSaveData>.ParallelWriter buildings;        
        [ReadOnly] public ComponentLookup<IsBlueprint> IsBluePrintLookUp;
        [ReadOnly] public ComponentLookup<IsDemolition> IsDemolitionLookUp;
        public void Execute(Entity entity, BuildingPosData buildingPosData, BuildingData buildingData)
        {
            buildings.TryAdd(buildingData.BuildingUniqueID,new BaseBuildingSaveData
            {
                buildingID=buildingData.BuildingIDHash,
                buildingPosition=buildingPosData.LeftCornerPos,
                rotation=buildingPosData.Rotation,
                isBlueprint=IsBluePrintLookUp.HasComponent(entity)&&IsBluePrintLookUp.IsComponentEnabled(entity),
                IsDemolition=IsDemolitionLookUp.HasComponent(entity)&&IsDemolitionLookUp.IsComponentEnabled(entity)
            });
        }
    }
    [WithAll(typeof(RoadTypeBuildingTag))]
    partial struct RoadSaveJob : IJobEntity
    {
        public NativeParallelHashMap<int,ManyPointsBuildingSaveData>.ParallelWriter manyPointsBuildings;        
        [ReadOnly] public ComponentLookup<IsBlueprint> IsBluePrintLookUp;
        [ReadOnly] public ComponentLookup<IsDemolition> IsDemolitionLookUp;
        public void Execute(Entity entity,  BuildingData buildingData,DynamicBuffer<MapPoint> mapPoints)
        {
            FixedList512Bytes<int2> points =new();
            foreach(var p in mapPoints) 
                points.Add(p.pos);
            manyPointsBuildings.TryAdd(buildingData.BuildingUniqueID,new ManyPointsBuildingSaveData
            {
                buildingID=buildingData.BuildingIDHash,
                points=points,
                isBlueprint=IsBluePrintLookUp.HasComponent(entity)&&IsBluePrintLookUp.IsComponentEnabled(entity),
                IsDemolition=IsDemolitionLookUp.HasComponent(entity)&&IsDemolitionLookUp.IsComponentEnabled(entity)
            });
        }
    }
    [WithAny(typeof(IsBlueprint),typeof(IsDemolition))]
    partial struct ConstructionSlotsSaveJob : IJobEntity
    {
        public NativeParallelHashMap<int,ConstructionSlotsSaveData>.ParallelWriter constructionSlotsSaveData;        
        [ReadOnly] public BufferLookup<InputConstructionSlotData> InputConstructionSlotDataLookUp;
        [ReadOnly] public BufferLookup<OutputConstructionSlotData> OutputConstructionSlotDataLookUp;
        [ReadOnly] public ComponentLookup<IsInputConstructionEnabled> IsInputConstructionEnabledLookUp;
        [ReadOnly] public ComponentLookup<IsOutputConstuctionEnabled> IsOutputConstuctionEnabledLookUp;
        public void Execute(Entity entity,BuildingData buildingData,ConstructionPriorityData constructionPriorityData)
        {
            FixedList512Bytes<InputConstructionSlotData> input=new();
            if (InputConstructionSlotDataLookUp.HasBuffer(entity))
            {
                foreach(var sIn in InputConstructionSlotDataLookUp[entity])
                {
                    input.Add(sIn);
                }
            }
            FixedList512Bytes<OutputConstructionSlotData> output=new();
            if (OutputConstructionSlotDataLookUp.HasBuffer(entity))
            {
                foreach(var sOut in OutputConstructionSlotDataLookUp[entity])
                {
                    output.Add(sOut);
                }
            }
            if(input.Length==0&&output.Length==0) return;

            constructionSlotsSaveData.TryAdd(buildingData.BuildingUniqueID,new ConstructionSlotsSaveData
            {
                isInputEnabled=IsInputConstructionEnabledLookUp.HasComponent(entity)&&IsInputConstructionEnabledLookUp.IsComponentEnabled(entity),
                isOutputEnabled=IsOutputConstuctionEnabledLookUp.HasComponent(entity)&&IsOutputConstuctionEnabledLookUp.IsComponentEnabled(entity),
                priority=(DistributionPriority)constructionPriorityData.ConstructionPriority,
                InputConstructionItems=input,
                OutputConstructionItems=output
            });
        }
    }

    partial struct ExcessSlotsSaveJob : IJobEntity
    {
        public NativeParallelHashMap<int,ExcessSlotsSaveData>.ParallelWriter excessSlotsSaveData;
        public void Execute(BuildingData buildingData,DynamicBuffer<ExcessSlotData> slots)
        {
            FixedList512Bytes<ExcessSlotData> ExcessItems=new();
            if(ExcessItems.Length<=0) return;
            foreach(var s in slots) ExcessItems.Add(s);
            excessSlotsSaveData.TryAdd(buildingData.BuildingUniqueID,new ExcessSlotsSaveData{ExcessItems=ExcessItems});
        }
    }

    partial struct RecipeAdnCraftBuildingSaveData : IJobEntity
    {
        public NativeParallelHashMap<int,RecipeAndCraftBuildingSaveData>.ParallelWriter recipeBuildingSaveData;
        [ReadOnly] public BufferLookup<InputSlotData> InputSlotDataLookUp;
        [ReadOnly] public BufferLookup<OutputSlotData> OutputSlotDataLookUp;
        [ReadOnly] public ComponentLookup<IsOutputCraftEnabled> IsOutputCraftEnabledLookUp;
        [ReadOnly] public ComponentLookup<IsInputCraftEnabled> IsInputCraftEnabledLookUp;
        public void Execute(Entity entity, CraftingPriorityData craftingPriorityData,BuildingData buildingData,RecipeBuildingData RecipeBuildingData,CountOfPackInBuildingData countOfPackInBuildingData)
        {
            if(RecipeBuildingData.RecipeIDHash==-1) return;
            FixedList512Bytes<InputSlotData> input=new();
            if (InputSlotDataLookUp.HasBuffer(entity))
            {
                foreach(var sIn in InputSlotDataLookUp[entity])
                {
                    input.Add(sIn);
                }
            }
            FixedList512Bytes<OutputSlotData> output=new();
            if (OutputSlotDataLookUp.HasBuffer(entity))
            {
                foreach(var sOut in OutputSlotDataLookUp[entity])
                {
                    output.Add(sOut);
                }
            }
            recipeBuildingSaveData.TryAdd(buildingData.BuildingUniqueID,new RecipeAndCraftBuildingSaveData
            {
                RecipeID=RecipeBuildingData.RecipeIDHash,
                CurrTime=RecipeBuildingData.CurrTime,
                TimeToCraft=RecipeBuildingData.TimeToCraft,
                isInputEnabled=IsInputCraftEnabledLookUp.HasComponent(entity)&&IsInputCraftEnabledLookUp.IsComponentEnabled(entity),
                isOutputEnabled=IsOutputCraftEnabledLookUp.HasComponent(entity)&&IsOutputCraftEnabledLookUp.IsComponentEnabled(entity),
                ContOfPack=countOfPackInBuildingData.CountOfPack,
                priority=(DistributionPriority) craftingPriorityData.CraftingPriority,
                InputCrafttems=input,
                OutputCrafttems=output
            });
        }
    }

    partial struct EnegrySaveJob : IJobEntity
    {
        
        [ReadOnly] public ComponentLookup<SwitchIsOff> SwitchIsOffLookUp;
        
        public NativeParallelHashMap<int,BuildingEnergyNetvorkLinkSaveData>.ParallelWriter buildingEnergyNetvorkLinkSaveData;
        public void Execute( Entity entity, BuildingData buildingData,EnergyBuildingData EnergyBuildingData)
        {
            FixedList128Bytes<EntityLink> entitesLink =new();
            foreach(var c in EnergyBuildingData.connections)
            {
                if(c.Item2.x==-1) continue;
                entitesLink.Add(new EntityLink{from = new int2(c.Item1,buildingData.BuildingUniqueID),to=c.Item2});
            }
            buildingEnergyNetvorkLinkSaveData.TryAdd(buildingData.BuildingUniqueID,new BuildingEnergyNetvorkLinkSaveData {entitesLink=entitesLink,isSwitchOff=SwitchIsOffLookUp.HasComponent(entity)&&SwitchIsOffLookUp.IsComponentEnabled(entity)});
        }
    }

    partial struct StorageSaveJob : IJobEntity
    {
        
        public NativeParallelHashMap<int,StorageSlotsSaveData>.ParallelWriter storageSlotsSaveData;
        public void Execute( BuildingData buildingData,CraftingPriorityData craftingPriorityData, DynamicBuffer<StorageSlotData> slotsBuff)
        {
            if(slotsBuff.Length<=0) return;
            FixedList512Bytes<StorageSlotData> slots=new();
            foreach(var s in slotsBuff)
            {
                slots.Add(s);
            }
            storageSlotsSaveData.TryAdd(buildingData.BuildingUniqueID,new StorageSlotsSaveData {slots=slots,priority=(DistributionPriority)craftingPriorityData.CraftingPriority });
        }
    }
}