using System;
using Unity.Collections;
using Unity.Entities;

[Serializable]
public struct BuildingWorkWithItemsLogicData : IComponentData
{
    public int Priority;
    public int RequiredRecipesGroup;
    public int CountOfPack;
}

[Serializable]
public struct ProcessBuildingData : IComponentData
{
    public int RecipeIDHash;
    public float TimeToProduceNext;
    public float CurrTime;
}

[Serializable]
public struct SlotData : IBufferElementData
{
    public int ItemId;
    public int Count;
    public int Capacity;
}
#region change
public struct ChangeRecipeData : IComponentData
{
    public int newRecipeID;
}

public struct ChangePriorityData : IComponentData
{
    public int newPriorityID;
}

public struct ChangeSlotCapacityData : IComponentData
{
    public int SlotIND;
    public int newCapacity;
}
public struct ChangeBuildingCountOfPackData : IComponentData
{
    public int newCountOfPack;
}
#endregion chage



public struct AnimationData : IComponentData
{
    public float AnimationProgress;
    public int AnimationState;
}
public struct HasInputSlots: IComponentData
{
    public int StartIND;
    public int EndIND;
}

public struct HasOutputSlots : IComponentData
{
    public int StartIND;
    public int EndIND;
}
public struct DistribureRemovedItems : IComponentData
{
     public int StartIND;
    public int EndIND;
}

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
