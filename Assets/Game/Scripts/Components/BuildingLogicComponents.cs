    using System;
    using Unity.Collections;
    using Unity.Entities;

    #region классификация построек
    public struct BuildingTag : IComponentData{}
    
    public struct RoadTypeBuildingTag : IComponentData{}
    public struct ProcessorTypeBuildingTag:IComponentData{}
    public struct ProducerTypeBuildingTag:IComponentData{}
    public struct ConsumerTypeBuildingTag:IComponentData{}
    public struct DefenceTypeBuildingTag: IComponentData{}
    public struct StorageTypeBuildingTag: IComponentData{}
    public struct PropTag : IComponentData{}
    #endregion

    #region визуал
    
    public struct ChangeBluePrintState: IComponentData, IEnableableComponent { }
    public struct IsBlueprint : IComponentData, IEnableableComponent{}

    public struct ChangeDemolitionStateTag : IComponentData , IEnableableComponent{}
    public struct IsDemolition : IComponentData, IEnableableComponent{}
    
    public struct BuildingStateData : IComponentData
    {
        public int State;
    }
    public struct CreateVisualTag : IComponentData, IEnableableComponent{}
    public struct DestroyVisualTag : IComponentData, IEnableableComponent{}
    
    public struct ForceDestroyTag: IComponentData, IEnableableComponent{}
    public struct MarkOnMap: IComponentData, IEnableableComponent{}
    
    

    #endregion

    #region распределение ресурсов

    [Serializable]
    public struct CraftingPriorityData : IComponentData
    {
        public int CraftingPriority;
    }

    [Serializable]
    public struct ConstructionPriorityData : IComponentData, IEnableableComponent
    {
        public int ConstructionPriority;
    }
    
    public interface ISlot
    {
        public int ItemId{get;set;}
        public int Amount{get;set;}
        public int Capacity{get;set;}
    }
    [Serializable]
    [InternalBufferCapacity(4)] 
    public struct InputSlotData : IBufferElementData,ISlot
    {
        public int ItemId{get;set;}
        public int Amount{get;set;}
        public int Capacity{get;set;}
    }
    [Serializable]
    [InternalBufferCapacity(4)] 
    public struct OutputSlotData : IBufferElementData,ISlot
    {
        public int ItemId{get;set;}
        public int Amount{get;set;}
        public int Capacity{get;set;}
    }
    [Serializable]
    [InternalBufferCapacity(6)] 
    public struct ExcessSlotData : IBufferElementData,ISlot
    {
        public int ItemId{get;set;}
        public int Amount{get;set;}
        public int Capacity{get;set;}
    }
    [Serializable]
    [InternalBufferCapacity(10)] 
    public struct StorageSlotData : IBufferElementData,ISlot
    {
        public bool IsInputEnabled{get;set;}
        public bool IsOutputEnabled{get;set;}
        public int ItemId{get;set;}
        public int Amount{get;set;}
        public int Capacity{get;set;}
    }
    [Serializable]
    [InternalBufferCapacity(4)] 
    public struct InputConstructionSlotData : IBufferElementData,ISlot
    {
        public int ItemId{get;set;}
        public int Amount{get;set;}
        public int Capacity{get;set;}
    }
    [Serializable]
    [InternalBufferCapacity(4)] 
    public struct OutputConstructionSlotData : IBufferElementData,ISlot
    {
        public int ItemId{get;set;}
        public int Amount{get;set;}
        public int Capacity{get;set;}
    }
    public struct BuildingRequiredStorageGroupData:IComponentData,IEnableableComponent
    {
        public FixedList32Bytes<int> RequiredStorageGroup;
    }
    public struct IsInputCraftEnabled : IComponentData, IEnableableComponent {}
    public struct IsOutputCraftEnabled : IComponentData, IEnableableComponent {}
    public struct IsInputConstructionEnabled : IComponentData, IEnableableComponent {}
    public struct IsOutputConstuctionEnabled : IComponentData, IEnableableComponent {}
    public struct IsConstuctionSlotsAssigned:IComponentData, IEnableableComponent{}
    #endregion
    
    #region переработка ресурсов
    [Serializable]
    public struct CountOfPackInBuildingData : IComponentData
    {
        public int CountOfPack; 
    }
    public struct BuildingRequiredRecipesGroupData:IComponentData
    {
        public FixedList32Bytes<int> RequiredRecipesGroups;
    }
    
    [Serializable]
    public struct RecipeBuildingData : IComponentData
    {
        public int RecipeIDHash;
        public float TimeToCraft;
        public float CurrTime;
    }
    
    public struct IsRecipeAssigned : IComponentData, IEnableableComponent {}
    public struct CanCraft : IComponentData, IEnableableComponent {}
    #endregion
    
    #region электричество
    public struct IsConnectedToEnegy:IComponentData, IEnableableComponent
    {
        //ссылка на башню   
    }
    #endregion
    #region кластеризация
    public struct ClusterId : IComponentData
    {
        public int Value;
    }

    public struct NeedsClusterAssign : IComponentData, IEnableableComponent {}
    public struct IsLogicEnabled : IComponentData, IEnableableComponent{}
    #endregion
    #region Загрузка
    public struct SaveInfo : IComponentData, IEnableableComponent{}
    public struct LoadInfo : IComponentData, IEnableableComponent{}
    #endregion
   
    public struct HealthData: IComponentData, IEnableableComponent
    {
        public float CurrHealth;
        public float MaxHealth;
        public float CurrTimeToRestore;
        public float RestoreHpPerTick;        
        public float TimeToRestore;

    }
    

    public enum DistributionPriority : int
    {
        Low = 1,
        MiddleLow = 2,
        Middle = 3,
        MiddleHeight = 4,
        Height = 5,
    }

    public enum WorkStateEnum : int
    {
        Phantom=0,
        Demolition=1,
        DisconnectedEnergy=2,
        Await=3,
        Work=4,
    }