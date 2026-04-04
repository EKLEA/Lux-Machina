using System;
using System.Linq;
using System.Threading;
using Kino;
using UniRx;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.IO.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using Zenject;

public class BuildingInfoViewModel
{
    EntityManager entityManager;
    World world;
    SlotViewData[] _inputSlots;    
    SlotViewData[] _outputSlots;   
    ReactiveProperty<bool> _isActiveInput;  
    ReactiveProperty<bool> _isActiveOutput;  
    ReactiveProperty<int> _priority;


    
    ReactiveProperty<SlotViewData[]> _excessSlotsRP;   

    ReactiveProperty< StorageSlotViewData[]> _storageSlots;

    ReactiveProperty<ConstructionViewData> _constructionViewDataRP;
    SlotViewData[] _inputConstructionSlots; 
    SlotViewData[] _outputConstructionSlots; 
    ReactiveProperty<bool> _isActiveConstructionInput;  
    ReactiveProperty<bool> _isActiveConstructionOutput;  
    ReactiveProperty<int> _constructionPriority;

    ReactiveProperty<int> _workState;

    ReactiveProperty<float> _timeToCraft;
    ReactiveProperty<float> _currTime;
    ReactiveProperty<int> _countInPack;

    
    ReactiveProperty<bool> _isSwitchOff;  

    CompositeDisposable disposables;
    CompositeDisposable storageDisposables;
    public Action tempUpdate;
    BuildingViewData viewData;
    
    public BuildingInfoViewModel(World world)
    {
        entityManager=world.EntityManager;
        this.world=world;
    }
    void ClearViewModel()
    {
        tempUpdate=null;
        disposables?.Dispose();
        disposables=new();
        _inputSlots=null;    
        _outputSlots=null;   
        _isActiveInput?.Dispose();
        _isActiveInput=null; 
        _isActiveOutput?.Dispose();
        _isActiveOutput=null;  
        _priority?.Dispose();
        _priority=null;
        _excessSlotsRP?.Dispose();  
        _excessSlotsRP=null;   
        _inputConstructionSlots=null;    
        _outputConstructionSlots=null;  
        _isActiveConstructionInput?.Dispose();
        _isActiveConstructionInput=null;  
        _isActiveConstructionOutput?.Dispose();
        _isActiveConstructionOutput=null;  
        _constructionPriority?.Dispose();
        _constructionPriority=null;
        _workState=null;
        _workState?.Dispose();
        
        _storageSlots?.Dispose(); 
        _storageSlots=null;  

        _timeToCraft?.Dispose();
        _timeToCraft=null;
        _currTime?.Dispose();
        _currTime=null;
        _countInPack?.Dispose();
        _countInPack=null;
    }
    public void GetBuildingData(Entity building,
                                out BuildingViewData buildingViewData,
                                out ReactiveProperty<ConstructionViewData> constructionViewDataRP,
                                out ReactiveProperty<SlotViewData[]> excessItems,
                                out DistribuitionViewData distribuitionViewData,
                                out ReactiveProperty<int> priority, 
                                out (bool,BuildingCraftViewData) recipeViewData,
                                out ReactiveProperty<StorageSlotViewData[]>StorageSlots,
                                out bool CanDestory,
                                out ReactiveProperty<bool> SwitchData)
    { 
        ClearViewModel();
        _constructionViewDataRP=new();
        _excessSlotsRP=new();
        _workState=new();
        _workState.Value=entityManager.GetComponentData<BuildingStateData>(building).State;
        viewData=new BuildingViewData{buildingID=entityManager.GetComponentData<BuildingData>(building).BuildingIDHash,buildingEntity=building,WorkState=_workState};
        buildingViewData=viewData;
        CanDestory=entityManager.HasComponent<ForceDestroyTag>(building);
        _inputConstructionSlots=GetSlotsFromBuff<InputConstructionSlotData>(building);
        _outputConstructionSlots=GetSlotsFromBuff<OutputConstructionSlotData>(building);
        _isActiveConstructionInput= entityManager.HasComponent<IsInputConstructionEnabled>(building)?new(entityManager.IsComponentEnabled<IsInputConstructionEnabled>(building)):null;
        _isActiveConstructionOutput= entityManager.HasComponent<IsOutputConstuctionEnabled>(building)?new(entityManager.IsComponentEnabled<IsOutputConstuctionEnabled>(building)):null;
        _constructionPriority=entityManager.HasComponent<IsOutputConstuctionEnabled>(building)?new(entityManager.GetComponentData<ConstructionPriorityData>(building).ConstructionPriority):null;
        _constructionPriority?.Subscribe(value =>
        {
            ChangePriority(true,value);
        });
        _isActiveConstructionInput?.Subscribe(value=>ChangeBuildingAccess(true,true,value)).AddTo(disposables);
        _isActiveConstructionOutput?.Subscribe(value=>ChangeBuildingAccess(true,false,value)).AddTo(disposables);
        ConstructionViewData constViewData = new()
        {
            InputConstructionSlots=_inputConstructionSlots,
            OutputConstructionSlots=_outputConstructionSlots,
            IsActiveConstructionInput=_isActiveConstructionInput,
            IsActiveConstructionOutput=_isActiveConstructionOutput,
            ConstructionPriority= _constructionPriority
        };
        _constructionViewDataRP.Value=constViewData;
        constructionViewDataRP=_constructionViewDataRP;
        
        _excessSlotsRP.Value=GetSlotsFromBuff<ExcessSlotData>(building);
        excessItems=_excessSlotsRP;

        _inputSlots=GetSlotsFromBuff<InputSlotData>(building);
        _outputSlots=GetSlotsFromBuff<OutputSlotData>(building);
        if (entityManager.HasComponent<EnergyBuildingData>(building))
        {
            _isSwitchOff=new(entityManager.IsComponentEnabled<SwitchIsOff>(building));
            SwitchData=_isSwitchOff;
            _isSwitchOff.Subscribe((value)=>ChangeEnergySwitch()).AddTo(disposables);
        }
        else SwitchData=null;
        if (_inputSlots!=null|| _outputSlots!=null)
        {
            _isActiveInput=entityManager.HasComponent<IsInputCraftEnabled>(building)? new(entityManager.IsComponentEnabled<IsInputCraftEnabled>(building)) :null;
            _isActiveOutput=entityManager.HasComponent<IsOutputCraftEnabled>(building)? new(entityManager.IsComponentEnabled<IsOutputCraftEnabled>(building)):null;
            _isActiveInput?.Subscribe(value=>ChangeBuildingAccess(false,true,value)).AddTo(disposables);
            _isActiveOutput?.Subscribe(value=>ChangeBuildingAccess(false,false,value)).AddTo(disposables);
            distribuitionViewData = new()
            {
                InputSlots=_inputSlots,
                OutputSlots=_outputSlots,
                IsActiveInput=_isActiveInput,
                IsActiveOutput=_isActiveOutput,
            };
           
        }
        else distribuitionViewData=null;
        _priority=entityManager.HasComponent<CraftingPriorityData>(building)? new(entityManager.GetComponentData<CraftingPriorityData>(building).CraftingPriority):null;
        _priority?.Subscribe(
            value =>
            {
                ChangePriority(false,value);
                
            }).AddTo(disposables);;
        priority=_priority;
        recipeViewData=(false,null);
        if (entityManager.HasComponent<IsRecipeAssigned>(building) && entityManager.IsComponentEnabled<IsRecipeAssigned>(building))
        {
            recipeViewData.Item1=true;
            var recipeData=entityManager.GetComponentData<RecipeBuildingData>(building);
            _timeToCraft=new(recipeData.TimeToCraft);
            _currTime=new(recipeData.CurrTime);
            _countInPack=new(entityManager.GetComponentData<CountOfPackInBuildingData>(building).CountOfPack);
            _countInPack.Subscribe(value =>
            {
                ChangeCountOfPack(value);
            }).AddTo(disposables);;
            recipeViewData.Item2 = new()
            {
                recipeIDHash=recipeData.RecipeIDHash,
                CountInPack=_countInPack,
                TimeToCraft=_timeToCraft,
                CurrTime=_currTime
            };
        }
        if (entityManager.HasComponent<StorageTypeBuildingTag>(building))
        {
            if (entityManager.HasBuffer<StorageSlotData>(building))
            {
                var buff = entityManager.GetBuffer<StorageSlotData>(building);
                var slots = new StorageSlotViewData[buff.Length];
                storageDisposables?.Dispose();
                storageDisposables=new();
                for(int i = 0; i < buff.Length;i++)
                {
                    
                        int indexForLambda = i; 
                        
                    ReactiveProperty<int>  ItemID=new(buff[i].ItemId);
                    ReactiveProperty<int> Amount=new(buff[i].Amount);
                    ReactiveProperty<int> Capacity=new(buff[i].Capacity);
                    Capacity.Subscribe(value=> ChangeStorageSlotCapacity(indexForLambda,value)).AddTo(storageDisposables);
                    ReactiveProperty<bool> IsActiveSlotInput=new(buff[i].IsInputEnabled);
                    ReactiveProperty<bool>  IsActiveSlotOutput=new(buff[i].IsOutputEnabled);

                    IsActiveSlotInput.Subscribe(value=>ChangeStorageSlotAccess(indexForLambda,true,value)).AddTo(storageDisposables);
                    IsActiveSlotOutput.Subscribe(value=>ChangeStorageSlotAccess(indexForLambda,false,value)).AddTo(storageDisposables);
                    slots[i]=new StorageSlotViewData{
                        ItemID=ItemID,
                        Amount=Amount,
                        Capacity=Capacity,
                        IsActiveInput=IsActiveSlotInput,
                        IsActiveOutput=IsActiveSlotOutput};
                }
                _storageSlots=new(slots);
                StorageSlots=_storageSlots;
                storageDisposables.AddTo(disposables);
            }
            else StorageSlots=null;
        }
        else StorageSlots=null;

    }
    
    SlotViewData[] GetSlotsFromBuff<T>(Entity building) where T:unmanaged,ISlot,IBufferElementData
    {
        if (entityManager.HasBuffer<T>(building))
        {
            var buff = entityManager.GetBuffer<T>(building);
            SlotViewData[]  res = new SlotViewData[buff.Length];
            for(int i = 0; i < buff.Length;i++)
            {
                res[i]=new SlotViewData{ItemID=new(buff[i].ItemId),Amount=new(buff[i].Amount),Capacity=new(buff[i].Capacity)};
            }
            return res;
        }
        return null;
    }
    void UpdateBuff<T>(Entity building,SlotViewData[] slotViews) where T : unmanaged, ISlot, IBufferElementData
    {
        if (entityManager.HasBuffer<T>(building))
        {
            
            var buff = entityManager.GetBuffer<T>(building);
            for(int i = 0; i < buff.Length;i++)
            {
                slotViews[i].Amount.Value=buff[i].Amount;
                slotViews[i].Capacity.Value=buff[i].Capacity;
                slotViews[i].ItemID.Value=buff[i].ItemId;
            }
        }
    }
    
    public void FixedUpdate()
    {
         if (!entityManager.Exists(viewData.buildingEntity)) 
        {
            return; 
        }
        if (_inputSlots != null)
            UpdateBuff<InputSlotData>(viewData.buildingEntity, _inputSlots);
    
        if (_outputSlots != null)
            UpdateBuff<OutputSlotData>(viewData.buildingEntity, _outputSlots);
        _workState.Value=entityManager.GetComponentData<BuildingStateData>(viewData.buildingEntity).State;
        if (entityManager.HasComponent<IsRecipeAssigned>(viewData.buildingEntity)&&entityManager.IsComponentEnabled<IsRecipeAssigned>(viewData.buildingEntity))
        {
            var data=entityManager.GetComponentData<RecipeBuildingData>(viewData.buildingEntity);
            
            
            _timeToCraft.Value=data.TimeToCraft;
            _currTime.Value=data.CurrTime;
        }


        UpdateStorageSlots();

        UpdateExcessSlots();
        UpdateConstructionSlots();
    }
    void UpdateConstructionSlots()
    {
        var entity = viewData.buildingEntity;
        var val=_constructionViewDataRP.Value;
        
        if(!entityManager.HasComponent<ConstructionPriorityData>(entity)) return;
        var inbuff = entityManager.GetBuffer<InputConstructionSlotData>(entity);
        
        if (val.InputConstructionSlots.Length != inbuff.Length)
        {
            _inputConstructionSlots=GetSlotsFromBuff<InputConstructionSlotData>(entity);
            val.InputConstructionSlots=_inputConstructionSlots;
            _constructionViewDataRP.SetValueAndForceNotify(val);
        }
        else
            UpdateBuff<InputConstructionSlotData>(viewData.buildingEntity, _inputConstructionSlots);
        var outbuff = entityManager.GetBuffer<OutputConstructionSlotData>(entity);
        
        if (val.OutputConstructionSlots.Length != outbuff.Length)
        {
            _outputConstructionSlots=GetSlotsFromBuff<OutputConstructionSlotData>(entity);
            val.OutputConstructionSlots=_outputConstructionSlots;
            _constructionViewDataRP.SetValueAndForceNotify(val);
        }
        else 
            UpdateBuff<OutputConstructionSlotData>(viewData.buildingEntity, _outputConstructionSlots);


    }
    void UpdateStorageSlots()
    {
        var entity = viewData.buildingEntity;

        if (!entityManager.HasComponent<StorageTypeBuildingTag>(entity) || 
            !entityManager.HasBuffer<StorageSlotData>(entity)) return;

        var buff = entityManager.GetBuffer<StorageSlotData>(entity);
        
        if (_storageSlots.Value == null || _storageSlots.Value.Length != buff.Length)
        {
            var newSlots = new StorageSlotViewData[buff.Length];
            storageDisposables?.Dispose();
            storageDisposables=new();
            for (int i = 0; i < buff.Length; i++)
            {
                var data = buff[i];
                int indexForLambda = i; 
                ReactiveProperty<int>  ItemID=new(buff[i].ItemId);
                ReactiveProperty<int> Amount=new(buff[i].Amount);
                ReactiveProperty<int> Capacity=new(buff[i].Capacity);
                Capacity.Subscribe(value=> ChangeStorageSlotCapacity(indexForLambda,value)).AddTo(storageDisposables);
                ReactiveProperty<bool> IsActiveSlotInput=new(buff[i].IsInputEnabled);
                ReactiveProperty<bool>  IsActiveSlotOutput=new(buff[i].IsOutputEnabled);

                IsActiveSlotInput.Subscribe(value=>ChangeStorageSlotAccess(indexForLambda,true,value)).AddTo(storageDisposables);
                IsActiveSlotOutput.Subscribe(value=>ChangeStorageSlotAccess(indexForLambda,false,value)).AddTo(storageDisposables);
                newSlots[i] = new StorageSlotViewData {
                    ItemID=ItemID,
                            Amount=Amount,
                            Capacity=Capacity,
                            IsActiveInput=IsActiveSlotInput,
                            IsActiveOutput=IsActiveSlotOutput};
            }
            _storageSlots.Value = newSlots; 
            
            storageDisposables.AddTo(disposables);
        }
        else
        {
            var currentSlots = _storageSlots.Value;
            for (int i = 0; i < buff.Length; i++)
            {
                var data = buff[i];
                var slot = currentSlots[i];

                slot.ItemID.Value = data.ItemId;
                slot.Amount.Value = data.Amount;
            }
        }
    }
    void UpdateExcessSlots()
    {
        var entity = viewData.buildingEntity;

        if (!entityManager.HasBuffer<ExcessSlotData>(entity)) return;

        var exbuff = entityManager.GetBuffer<ExcessSlotData>(entity);
        
        bool isSizeChanged = _excessSlotsRP.Value == null || _excessSlotsRP.Value.Length != exbuff.Length;

        SlotViewData[] currentSlots = isSizeChanged 
            ? new SlotViewData[exbuff.Length] 
            : _excessSlotsRP.Value;

        for (int i = 0; i < exbuff.Length; i++)
        {
            var data = exbuff[i];

            if (currentSlots[i] == null)
            {
                currentSlots[i] = new SlotViewData 
                {
                    ItemID = new(data.ItemId),
                    Amount = new(data.Amount),
                    Capacity = new(data.Capacity)
                };
            }
            else
            {
                currentSlots[i].ItemID.Value = data.ItemId;
                currentSlots[i].Amount.Value = data.Amount;
                currentSlots[i].Capacity.Value = data.Capacity;
            }
        }

        if (isSizeChanged)
        {
            _excessSlotsRP.Value = currentSlots;
        }
        else
        {
            _excessSlotsRP.SetValueAndForceNotify(currentSlots);
        }
    }
    
    public void AddAmount(int amount)
    {
        
        if (entityManager.HasComponent<IsDemolition>(viewData.buildingEntity)&&entityManager.IsComponentEnabled<IsDemolition>(viewData.buildingEntity))
        {
            var buff = entityManager.GetBuffer<OutputConstructionSlotData>(viewData.buildingEntity);
            if(buff.Length<1) return;
            for(int i =0; i < buff.Length; i++)
            {
                var b = buff[i];
                b.Amount=math.clamp(b.Amount+amount,0,b.Capacity);
                buff[i]=b;
                if(b.Amount==b.Capacity) tempUpdate?.Invoke();
            }
        }
        if (entityManager.HasComponent<IsBlueprint>(viewData.buildingEntity)&&entityManager.IsComponentEnabled<IsBlueprint>(viewData.buildingEntity))
        {
            var buff = entityManager.GetBuffer<InputConstructionSlotData>(viewData.buildingEntity);
            if(buff.Length<1) return;
            for(int i =0; i < buff.Length; i++)
            {
                var b = buff[i];
                b.Amount=math.clamp(b.Amount+amount,0,b.Capacity);
                buff[i]=b;
                if(b.Amount==b.Capacity) tempUpdate?.Invoke();
            }
        }
    }
    public void SetRecipe(int RecipeID)
    {
        var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
               .CreateCommandBuffer();
        Entity Command=ecb.CreateEntity();
        ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=viewData.buildingEntity});
        ecb.AddComponent(Command,new SetRecipeData{RecipeID=RecipeID});
        
    }

    public void MarkAsDemolition(bool IsDemolition)
    {
        var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
               .CreateCommandBuffer();
        ecb.SetComponentEnabled<ChangeDemolitionStateTag>(viewData.buildingEntity,IsDemolition);
    }
    public void MarkAsForceDestory()
    {
        var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
               .CreateCommandBuffer();
        if(!entityManager.HasComponent<IsDemolition>(viewData.buildingEntity)) return;
        Entity Command=ecb.CreateEntity();
        ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=viewData.buildingEntity});
        ecb.AddComponent(Command,new MarkAsForceDestoroyData());
    }
    public void AddStorageSlot(int ItemID, int Capacity)
    {
        
        if (entityManager.HasBuffer<StorageSlotData>(viewData.buildingEntity))
        {
            var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
                .CreateCommandBuffer();
            var buff=entityManager.GetBuffer<StorageSlotData>(viewData.buildingEntity);
            if(buff.Length==buff.Capacity) return;
            
            Entity Command=ecb.CreateEntity();
            ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=viewData.buildingEntity});
            ecb.AddComponent(Command,new AddStorageSlotData{ItemID=ItemID,Capacity=Capacity});
        }
    }
    public void RemoveStorageSlot(int slotIND)
    {
        if (entityManager.HasBuffer<StorageSlotData>(viewData.buildingEntity))
        {     var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
                .CreateCommandBuffer();
            var buff=entityManager.GetBuffer<StorageSlotData>(viewData.buildingEntity);
            if(buff.Length<=slotIND) return;
            
            Entity Command=ecb.CreateEntity();
            ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=viewData.buildingEntity});
            ecb.AddComponent(Command,new RemoveStorageSlotData{slotIND=slotIND});
        }
    }   
    void ChangeEnergySwitch()
    {
        if (entityManager.HasComponent<SwitchIsOff>(viewData.buildingEntity))
        {
            if(_isSwitchOff.Value==entityManager.IsComponentEnabled<SwitchIsOff>(viewData.buildingEntity))  return;
             var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
            .CreateCommandBuffer();
            
            ecb.SetComponentEnabled<SwitchIsOff>(viewData.buildingEntity,!entityManager.IsComponentEnabled<SwitchIsOff>(viewData.buildingEntity));
            ecb.SetComponentEnabled<UpdateConnectStatus>(viewData.buildingEntity,true);
            var buildingMap = entityManager.CreateEntityQuery(typeof(BuildingMap)).GetSingletonEntity();
            ecb.SetComponentEnabled<UpdateConnectionsTag>(buildingMap,true);
        }
    }
    void ChangePriority(bool isConstrucionPriority,int priorityValue)
    {
       
        var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
            .CreateCommandBuffer();
        Entity Command=ecb.CreateEntity();
        ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=viewData.buildingEntity});
        if (isConstrucionPriority)
            ecb.AddComponent(Command,new ChangeConstructionPriotiyData{newPriority=priorityValue});
        else
            ecb.AddComponent(Command,new ChangeCraftPriotiyData{newPriority=priorityValue});
    }
    void ChangeBuildingAccess(bool isConstrucionAccess,bool IsInput,bool value)
    {
        var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
            .CreateCommandBuffer();
        Entity Command=ecb.CreateEntity();
        ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=viewData.buildingEntity});
        if (isConstrucionAccess)
            ecb.AddComponent(Command,new ChangeConstructionBuildingAccessData{IsInput=IsInput,IsEnabled=value});
        else
            ecb.AddComponent(Command,new ChangeProcessorBuildingAccessData{IsInput=IsInput,IsEnabled=value});
    }
    void ChangeStorageSlotAccess(int SlotIND,bool IsInput,bool value)
    {
        var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
            .CreateCommandBuffer();
        Entity Command=ecb.CreateEntity();
        ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=viewData.buildingEntity});
        ecb.AddComponent(Command,new ChangeStorageSlotAccessData{SlotIND=SlotIND,IsInput=IsInput,IsEnabled=value});
    }
    void ChangeStorageSlotCapacity(int SlotIND,int newCapacity)
    {
        var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
            .CreateCommandBuffer();
         Entity Command=ecb.CreateEntity();
        ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=viewData.buildingEntity});
        ecb.AddComponent(Command,new ChangeStorageSlotCapacityData{SlotIND=SlotIND,newCapacity=newCapacity});
    }
    void ChangeCountOfPack(int newCapacity)
    {
        var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
            .CreateCommandBuffer();
        Entity Command=ecb.CreateEntity();
        ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=viewData.buildingEntity});
        ecb.AddComponent(Command,new ChangeCountOfPackData{newCapacity=newCapacity});
    }
}
    public struct RemoveStorageSlotData : IComponentData
    {
        public int slotIND;
    }
    public struct SetRecipeData : IComponentData
    {
        public int RecipeID;
    }
    public struct AddStorageSlotData : IComponentData
    {
        public int ItemID;
        public int Capacity;
    }
    public struct MarkAsForceDestoroyData : IComponentData
    {
    }
    public struct ChangeBuildingData: IComponentData
    {
        public Entity targetEntity;
    }
    public struct ChangeStorageSlotCapacityData : IComponentData
    {
        public int SlotIND;
        public int newCapacity;
    }
    public struct ChangeStorageSlotAccessData : IComponentData
    {
        public int SlotIND;
        public bool IsEnabled;
        public bool IsInput;
    }
    public struct ChangeProcessorBuildingAccessData : IComponentData
    {
        public bool IsEnabled;
        public bool IsInput;
    }
    public struct ChangeConstructionBuildingAccessData : IComponentData
    {
        public bool IsEnabled;
        public bool IsInput;
    }
    public struct ChangeCountOfPackData : IComponentData
    {
        public int newCapacity;
    }
    public struct ChangeCraftPriotiyData : IComponentData
    {
        public int newPriority;
    }
    public struct ChangeConstructionPriotiyData : IComponentData
    {
        public int newPriority;
    }
public class BuildingViewData
{
    public int buildingID;
    public Entity buildingEntity;
    public IReadOnlyReactiveProperty<int> WorkState;
}
public class DistribuitionViewData
{
    public SlotViewData[] InputSlots;    
    public SlotViewData[] OutputSlots;   
    public ReactiveProperty<bool> IsActiveInput;
    public ReactiveProperty<bool> IsActiveOutput;
}

public class ConstructionViewData
{
    public SlotViewData[] InputConstructionSlots;    
    public SlotViewData[] OutputConstructionSlots;    
    public ReactiveProperty<bool> IsActiveConstructionInput;
    public ReactiveProperty<bool> IsActiveConstructionOutput; 
    public ReactiveProperty<int> ConstructionPriority;
}
public class BuildingCraftViewData
{
    public int recipeIDHash;
    public ReactiveProperty<int> CountInPack;
    public IReadOnlyReactiveProperty<float> TimeToCraft;
    public IReadOnlyReactiveProperty<float> CurrTime;
}

public class DefenceBuildingViewData 
{
  
}

public class SlotViewData
{
    public ReactiveProperty<int> Amount;
    public ReactiveProperty<int> Capacity;
    public ReactiveProperty<int> ItemID;
}
public class StorageSlotViewData:SlotViewData
{
    public ReactiveProperty<bool> IsActiveInput;
    public ReactiveProperty<bool> IsActiveOutput;
}