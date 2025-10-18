
using System;
using Unity.Entities;
[Serializable]
public struct BuildingLogicData : IComponentData
{
    public int Priority;
    public int RecipeIDHash;
    public int RequiredRecipesGroup;
}
[Serializable]
public struct ConsumerBuildingData : IComponentData
{

}
[Serializable]
public struct ProducerBuildingData : IComponentData
{
    public float TimeToProduceNext;
    public float CurrTime;
}
[Serializable]
public struct StorageSlotData : IBufferElementData
{
    public int ItemId;
    public int Count;
    public int Capacity;
}
[Serializable]
public struct InputStorageSlotData
{
    public StorageSlotData[] Slots;
}
[Serializable]
public struct OutputStorageSlotData
{
    public StorageSlotData[] Slots; 
}

public struct ProductionAnimationData : IComponentData
{
    public float AnimationProgress;
    public int AnimationStateHash;  
}
public struct ConsumerBuildingTag : IComponentData { }
public struct ProducerBuildingTag : IComponentData { }
public struct ProcessorBuildingTag : IComponentData { }
public enum DistributionPriority : int
{
    Low = 0,
    MiddleLow = 1,
    Middle = 2,
    MiddleHeight = 3,
    Height=4
}