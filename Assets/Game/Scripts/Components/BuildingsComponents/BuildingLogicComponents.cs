    using System;
    using Unity.Collections;
    using Unity.Entities;
 

    #region компоненты логики построеки
    [Serializable]
    public struct BuildingPriorityData : IComponentData
    {
        public int Priority;
        
    }
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
    public struct ProcessBuildingData : IComponentData
    {
        public int RecipeIDHash;
        public float TimeToProduceNext;
        public float CurrTime;
    }
    
    public struct RoadTag : IComponentData{}
    public struct BuildingTag : IComponentData{}
    public struct ProcessorBuildingTag:IComponentData{}
    public struct DefenceBuildingTag: IComponentData{}
    public struct StorageBuildingTag: IComponentData{}
    public struct PropTag : IComponentData{}
    public struct BuildingStateData : IComponentData
    {
        public int State;
    }
    #endregion
    #region компоненты управления логикой
    public struct CreateStorageSlot: IComponentData
    {
        public int ItemId;
        public int Capacity;
    }
    public struct DeleteStorageSlot: IComponentData
    {
        public int SlotIND;
    }
    public struct ChangeRecipeData : IComponentData
    {
        public int newRecipeID;
    }

    public struct ChangePriorityData : IComponentData
    {
        public int newPriorityID;
    }

    public struct ChangeSlotCapacityData : IBufferElementData
    {
        public int SlotIND;
        public int newCapacity;
    }
    public struct ChangeBuildingCountOfPackData : IComponentData
    {
        public int newCountOfPack;
    }
    public struct AssingRecipeTag:IComponentData{}
    public struct CanResoucesBeAddedTag: IComponentData{}
    public struct CanResoucesBeRemovedTag: IComponentData{}


    public struct CanAnimateTag : IComponentData{}
    #endregion
    #region Слоты 
        
    [Serializable]
    public struct SlotData : IBufferElementData

    {
        public int ItemId;
        public int Amount;
        public int Capacity;
    }
    public struct InputSlots: IComponentData
    {
        public int StartIND;
        public int EndIND;
    }
    public struct OutputSlots : IComponentData
    {
        public int StartIND;
        public int EndIND;
    }
    public struct ExcessItemSlots : IComponentData
    {
        public int StartIND;
        public int EndIND;
    }
    #endregion
    

    public enum DistributionPriority : int
    {
        Low = 0,
        MiddleLow = 1,
        Middle = 2,
        MiddleHeight = 3,
        Height = 4,
    }

    public enum WorkStateEnum : int
    {
        Phantom=0,
        DisconnectedEnergy=1,
        Await=2,
        Work=3,
    }