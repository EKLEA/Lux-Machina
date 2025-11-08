using System;
using Unity.Collections;
using Unity.Entities;

[Serializable]
public struct BuildingLogicData : IComponentData
{
    public int Priority;
    public int RecipeIDHash;
    public int RequiredRecipesGroup;
}

[Serializable]
public struct ProducerBuildingData : IComponentData
{
    public float TimeToProduceNext;
    public float CurrTime;
}

[Serializable]
public struct InnerStorageSlotData : IBufferElementData
{
    public int ItemId;
    public int Count;
    public int Capacity;
}

public struct OutputStorageSlotData : IBufferElementData
{
    public int ItemId;
    public int Count;
    public int Capacity;
}

public struct ChangeRecipeData : IComponentData
{
    public int newRecipeID;
}

public struct ChangePriorityData : IComponentData
{
    public int newPriorityID;
}

public struct ChangeInnerSlotCapacityData : IComponentData
{
    public int SlotID;
    public int newCapacity;
}

public struct ChangeOutputSlotCapacityData : IComponentData
{
    public int SlotID;
    public int newCapacity;
}

public struct DistribureRemovedItems : IComponentData { }

public struct AnimationData : IComponentData
{
    public float AnimationProgress;
    public int AnimationState;
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
    Height = 4,
}

public enum BuildingAnimationState : int
{
    Disconnected = 0,
    Process = 1,
    Await = 2,
}
