using System;
using System.Linq;
using UniRx;
using Unity.Entities;
using Zenject;

public class BuildingManagementWindowViewModel
{
    [Inject] EntityManager entityManager;
    ReactiveProperty<bool> HasEnergy =new();

    ReactiveProperty<float> TimeToProduceNext =new();

    ReactiveProperty<float> CurrTime =new();
    ReactiveProperty<int> CountInPack =new();

    ReactiveProperty<int> Priority =new();

    ReactiveProperty<(int ItemId, int amount, int Capacity)>[] Slots;
    ReactiveProperty<SlotRange> DestributeSlots=new();
    
    ReactiveProperty<bool> IsActiveInput=new();
    ReactiveProperty<bool> IsActiveOutput=new();

    ReactiveProperty<int> WorkState=new();
    public bool GetBuildingData(Entity building, out slot,out recipe)
    {
        if (entityManager.HasComponent<BuildingData>(building))
        {
            var buildingData=entityManager.GetComponentData<BuildingData>(building);
            if (entityManager.HasComponent<PropTag>(building))
            {
                viewData = new BaseBuildingViewData(buildingData.BuildingIDHash,building);
            }
            else
            {
                if (entityManager.HasComponent<ProcessorBuildingTag>(building))
                {
                    if()
                }
            }
        }
        else
        {
             viewData=null;
            return false;
        }
    }

    ((int, int) input, (int, int) output,(int, int) distribute) GetSlotRanges(Entity building)
    {
        int inStart = -1, inEnd = -1;
        int otStart = -1, otEnd = -1;
        int dStart = -1, dEnd = -1;
        
        if (entityManager.HasComponent<InputSlots>(building))
        {
            var inData = entityManager.GetComponentData<InputSlots>(building);
            (inStart, inEnd) = (inData.StartIND, inData.EndIND);
        }
        
        if (entityManager.HasComponent<OutputSlots>(building))
        {
            var outData = entityManager.GetComponentData<OutputSlots>(building);
            (otStart, otEnd) = (outData.StartIND, outData.EndIND);
        }
        if (entityManager.HasComponent<ExcessItemSlots>(building))
        {
            var dData = entityManager.GetComponentData<ExcessItemSlots>(building);
            (dStart, dEnd) = (dData.StartIND, dData.EndIND);
        }
        
        return ((inStart, inEnd), (otStart, otEnd),(dStart,dEnd));
    }
    
    public void Update(Entity building)
    {
        
        
    }
}
public class BaseBuildingViewData
{
    public int buildingID{get;}
    public Entity buildingEntity{get;}
    public BaseBuildingViewData(int buildingID,Entity buildingEntity)
    {
        this.buildingID=buildingID;
        this.buildingEntity=buildingEntity;
    }
}

public class WorkBuildingViewData : BaseBuildingViewData
{
    public IReadOnlyReactiveProperty<int> WorkState{get;}
    
    public SlotRange excessRange{get;}
    public WorkBuildingViewData(int buildingID, 
                                Entity buildingEntity,
                                SlotRange excessRange,
                                IReadOnlyReactiveProperty<int> WorkState) : base(buildingID, buildingEntity)
    {
        this.WorkState=WorkState;
        this.excessRange=excessRange;
    }
}

public class ProcessorBuildingData : WorkBuildingViewData
{
    public int recipeIDHash{get;}
    public (int itemID, IReadOnlyReactiveProperty<int> amount, ReactiveProperty<int> capasity)[] Slots{get;}
    public SlotRange inputRange{get;}
    public SlotRange outputRange{get;}
    public IReadOnlyReactiveProperty<bool> HasEnergy{get;}
    public IReadOnlyReactiveProperty<float> TimeToProduceNext {get;}
    public IReadOnlyReactiveProperty<float> CurrTime{get;}    
    public ReactiveProperty<int> CountInPack{get;}
    public ReactiveProperty<bool> IsActiveInput{get;}
    public ReactiveProperty<bool> IsActiveOutput{get;}
    public ReactiveProperty<int> Priority{get;}
    public ProcessorBuildingData(int buildingID, 
                                Entity buildingEntity, 
                                IReadOnlyReactiveProperty<int> WorkState,
                                SlotRange excessRange,
                                int recipeIDHash,
                                (int itemID, IReadOnlyReactiveProperty<int> amount, ReactiveProperty<int> capasity)[] slots,
                                SlotRange inputRange,
                                SlotRange outputRange,
                                ReactiveProperty<bool> hasEnergy,
                                ReactiveProperty<float> TimeToProduceNext,
                                ReactiveProperty<float> CurrTime,
                                ReactiveProperty<int> CountInPack,
                                ReactiveProperty<bool> IsActiveInput,
                                ReactiveProperty<bool> IsActiveOutput,
                                ReactiveProperty<int> Priority) : base(buildingID, buildingEntity,excessRange, WorkState)
    {
        this.recipeIDHash=recipeIDHash;
        this.Slots=slots;
        this.inputRange=inputRange;
        this.outputRange=outputRange;
        HasEnergy=hasEnergy;
        this.TimeToProduceNext=TimeToProduceNext;
        this.CurrTime=CurrTime;
        this.CountInPack=CountInPack;
        this.IsActiveInput=IsActiveInput;
        this.IsActiveOutput=IsActiveOutput;
        this.Priority=Priority;
    }
}

public class StorageBuildingData : WorkBuildingViewData
{
   public bool IsDefence{get;}
    public (int itemID, IReadOnlyReactiveProperty<int> amount, ReactiveProperty<int> capasity)[] Slots{get;}
    public SlotRange inputRange{get;}
    public SlotRange outputRange{get;}
    public IReadOnlyReactiveProperty<bool> HasEnergy{get;}
    public ReactiveProperty<bool> IsActiveInput{get;}
    public ReactiveProperty<bool> IsActiveOutput{get;}
    public ReactiveProperty<int> Priority{get;}
    public StorageBuildingData(int buildingID, 
                                Entity buildingEntity, 
                                IReadOnlyReactiveProperty<int> WorkState,
                                
                                SlotRange excessRange,
                                bool IsDefence,
                                (int itemID, IReadOnlyReactiveProperty<int> amount, ReactiveProperty<int> capasity)[] slots,
                                SlotRange inputRange,
                                SlotRange outputRange,
                                ReactiveProperty<bool> hasEnergy,
                                ReactiveProperty<bool> IsActiveInput,
                                ReactiveProperty<bool> IsActiveOutput,
                                ReactiveProperty<int> Priority) : base(buildingID, buildingEntity,excessRange, WorkState)
    {
        this.IsDefence=IsDefence;
        this.Slots=slots;
        this.inputRange=inputRange;
        this.outputRange=outputRange;
        HasEnergy=hasEnergy;
        this.IsActiveInput=IsActiveInput;
        this.IsActiveOutput=IsActiveOutput;
        this.Priority=Priority;
    }
}

public class SlotRange
{
    public int Start { get; }
    public int End { get; }

    public SlotRange(int start, int end)
    {
        Start = start;
        End = end;
    }
}