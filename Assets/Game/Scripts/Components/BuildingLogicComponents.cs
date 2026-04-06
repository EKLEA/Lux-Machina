    using System;
using NUnit.Framework;
using Unity.Collections;
    using Unity.Entities;
using Unity.Mathematics;

#region классификация построек
public struct BuildingTag : IComponentData{}
    
    public struct CoreBuildingTag : IComponentData{}
    public struct ManyPointTypeBuildingTag : IComponentData{}
    public struct ProcessorTypeBuildingTag:IComponentData{}
    public struct ProducerTypeBuildingTag:IComponentData{}
    public struct ConsumerTypeBuildingTag:IComponentData{}
    public struct DefenceTypeBuildingTag: IComponentData{}
    public struct StorageTypeBuildingTag: IComponentData{}
    public struct EnergyTypeBuildingTag: IComponentData{}
    public struct PropTag : IComponentData{}
    public struct LogisticTag : IComponentData{}
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
    public struct ForceDestroyTag: IComponentData, IEnableableComponent{}
    public struct CheckForDestroy: IComponentData, IEnableableComponent{}
    public struct MarkOnMap: IComponentData, IEnableableComponent{}
    public struct UpdateManyPoint: IComponentData, IEnableableComponent{}
    
   

    #endregion

    #region распределение ресурсов
    public struct ResourcesLink : IComponentData
    {
        public FixedList512Bytes<int2> ResourcesCells;
        public int indexCell;
    }

    public struct TransitionSlotData : IBufferElementData
    {
        public int itemID;
        public int amount;
    }

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
    [Serializable]
    public struct StorageBuildingData : IComponentData, IEnableableComponent
    {
        public int MaxSlots;
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
    public struct IsConnectedToEnergy:IComponentData, IEnableableComponent
    {
    }
    public struct SwitchIsOff:IComponentData, IEnableableComponent
    {
    }
    public struct ConnectToEnegyEntities:IComponentData
    {
        public FixedList128Bytes<int> ConnectToEntites; 
    }
    public struct EnergyBuildingData : IComponentData
    {
        public float radius;
        public FixedList128Bytes<(int,int2)> connections; //x-node y -entity
        public int maxConnections;
    }
    public struct UpdateConnectStatus:IComponentData, IEnableableComponent{}
    #endregion
    #region кластеризация
    public struct ClusterLink : IComponentData
    {
        public FixedList64Bytes<int> ClusterIds; 
    }
    public struct NeedsClusterAssign : IComponentData, IEnableableComponent {}
    public struct IsLogicEnabled : IComponentData, IEnableableComponent{}
    #endregion
    #region Загрузка
    public struct SavableTag : IComponentData{}
    public struct LoadInfo : IComponentData, IEnableableComponent{}
    #endregion
    #region  Здоровье
    public struct HealthData: IComponentData, IEnableableComponent
    {
        public float CurrHealth;
        public float MaxHealth;
        public float CurrTimeToRestore;
        public float RestoreHpPerTick;        
        public float TimeToRestore;

    }
    public struct TakeDamage : IBufferElementData
    {
        public int2 pos;
        public float Damage;
    }
    
    public struct ManyPointPointHealthData: IBufferElementData
    {
        public int2 pos;
        public float CurrHealth;
        public float MaxHealth;
        public float CurrTimeToRestore;
        public float RestoreHpPerTick;        
        public float TimeToRestore;
    }
    #endregion
    #region оборона
    public struct ShooterTag : IComponentData { }
    public struct ArtilleryTag : IComponentData { }
    public struct TurretStats : IComponentData
    {
        public bool isEnergyAmmo;
        public float AttackRange;
        public ProjectileType projectileType;
        public float Angle;
        public float CoolDown;
        public float TimeToCoolDown;
        public int ProjectilePrefabID;
        public int CurrAmmo;
        public int AmmoID;
    }
    public struct TurretTranform : IComponentData
    {
        public int AttacMode;
        public float3 projectTyleSpawn;
        public float2 rotation; 
        public float baseRotation;
        
        
    }
    
    public struct ProjectileData : IComponentData
    {
        public float3 StartPos;
        public float3 TargetPos;
        public float Speed;
        public float Progress;
        public float ArcHeight;
        public float Damage;   
        public float Radius;   
    }
    public struct ProjectilePrefabElement : IBufferElementData
    {
        public int ID; 
        public Entity PrefabEntity;
    }
    #endregion
    
    [Serializable]
    public enum AttakMode 
    {
        Distance=1,
        Health=2
    }
    [Serializable]
    public enum DistributionPriority 
    {
        Low = 1,
        MiddleLow = 2,
        Middle = 3,
        MiddleHeight = 4,
        Height = 5,
    }

    public enum WorkStateEnum 
    {
        Phantom=0,
        Demolition=1,
        AwaitConntionToCluster=2,
        DisconnectedEnergy=3,
        Await=4,
        Work=5,
    }