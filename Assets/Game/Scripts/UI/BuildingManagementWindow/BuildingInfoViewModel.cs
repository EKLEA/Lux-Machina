using System;
using System.Linq;
using System.Threading;
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
    SlotViewData[] InputSlots;    
    SlotViewData[] OutputSlots;   
    ReactiveProperty<bool> IsActiveInput;  
    ReactiveProperty<bool> IsActiveOutput;  
    ReactiveProperty<int> Priority;


    
    ReactiveProperty<SlotViewData[]>ExcessSlots;   
    SlotViewData[] InputConstructionSlots;    
    SlotViewData[] OutputConstructionSlots;  

   ReactiveProperty< StorageSlotViewData[]> StorageSlots;  
    ReactiveProperty<bool> IsActiveConstructionInput;  
    ReactiveProperty<bool> IsActiveConstructionOutput;  
    ReactiveProperty<int> ConstructionPriority;

    ReactiveProperty<int> WorkState;

    ReactiveProperty<float> TimeToCraft;
    ReactiveProperty<float> CurrTime;
    ReactiveProperty<int> CountInPack;
    CompositeDisposable disposables;
    CompositeDisposable storageDisposables;
    public Action tempUpdate;
    
    public BuildingInfoViewModel(World world)
    {
        entityManager=world.EntityManager;
        this.world=world;
    }
    public void GetBuildingData(Entity building,
                out BuildingViewData buildingViewData,
                out ReactiveProperty<int> priority, 
                out DistribuitionViewData distribuitionViewData,
                out ReactiveProperty<SlotViewData[]> excessItems,
                out ConstructionViewData placeDestroyViewData,
                out (bool,BuildingCraftViewData) recipeViewData,
                out ReactiveProperty<StorageSlotViewData[]>StorageSlots)
    {
        tempUpdate=null;
        disposables?.Dispose();
        disposables=new();
        buildingViewData=null;
        distribuitionViewData=null;
        excessItems=null;
        StorageSlots=null;
        placeDestroyViewData=null;
        priority=null;
        recipeViewData=(false,null);
        if (entityManager.HasComponent<PropTag>(building) || entityManager.HasComponent<BuildingTag>(building))
        {
            this.WorkState=new(entityManager.GetComponentData<BuildingStateData>(building).State);
            buildingViewData=new BuildingViewData{buildingID=entityManager.GetComponentData<BuildingData>(building).BuildingIDHash,buildingEntity=building,WorkState=WorkState};
            
            InputSlots=null;    
            OutputSlots=null;   
            InputSlots=GetSlotsFromBuff<InputSlotData>(building);
            OutputSlots=GetSlotsFromBuff<OutputSlotData>(building);
            
            if (InputSlots != null || OutputSlots != null)
            {
                IsActiveInput=entityManager.HasComponent<IsInputCraftEnabled>(building)? new(entityManager.IsComponentEnabled<IsInputCraftEnabled>(building)) :null;
                IsActiveOutput=entityManager.HasComponent<IsOutputCraftEnabled>(building)? new(entityManager.IsComponentEnabled<IsOutputCraftEnabled>(building)):null;
                IsActiveInput?.Subscribe(value=>ChangeBuildingAccess(building,false,true,value)).AddTo(disposables);
                IsActiveOutput?.Subscribe(value=>ChangeBuildingAccess(building,false,false,value)).AddTo(disposables);
                distribuitionViewData = new()
                {
                    IsProcessor=entityManager.HasComponent<ConsumerTypeBuildingTag>(building)||
                                entityManager.HasComponent<ProducerTypeBuildingTag>(building)||
                                entityManager.HasComponent<ProcessorTypeBuildingTag>(building),
                                InputSlots=InputSlots,
                    OutputSlots=OutputSlots,
                    IsActiveInput=IsActiveInput,
                    IsActiveOutput=IsActiveOutput,
                };

            }
            else distribuitionViewData =null;
            Priority=entityManager.HasComponent<CraftingPriorityData>(building)? new(entityManager.GetComponentData<CraftingPriorityData>(building).CraftingPriority):null;
            Priority?.Subscribe(
                value =>
                {
                    ChangePriority(building,false,value);
                    
                }).AddTo(disposables);;
            priority=Priority;
            if(entityManager.HasComponent<ExcessSlotData>(building)) ExcessSlots=new(GetSlotsFromBuff<ExcessSlotData>(building));
            else ExcessSlots=null;
            excessItems=ExcessSlots;
           

            if (entityManager.IsComponentEnabled<IsBlueprint>(building)||entityManager.IsComponentEnabled<IsDemolition>(building))
            {
                InputConstructionSlots=null;    
                OutputConstructionSlots=null;   
                InputConstructionSlots=GetSlotsFromBuff<InputConstructionSlotData>(building);
                OutputConstructionSlots=GetSlotsFromBuff<OutputConstructionSlotData>(building);
                IsActiveConstructionInput=entityManager.HasComponent<IsInputConstructionEnabled>(building)? new(entityManager.IsComponentEnabled<IsInputConstructionEnabled>(building)) :null;
                IsActiveConstructionOutput=entityManager.HasComponent<IsOutputConstuctionEnabled>(building)? new(entityManager.IsComponentEnabled<IsOutputConstuctionEnabled>(building)):null;
                ConstructionPriority=entityManager.HasComponent<ConstructionPriorityData>(building)? new(entityManager.GetComponentData<ConstructionPriorityData>(building).ConstructionPriority):null;
                ConstructionPriority?.Subscribe(value =>
                {
                    ChangePriority(building,true,value);
                    
                    
                });
                IsActiveConstructionInput?.Subscribe(value=>ChangeBuildingAccess(building,true,true,value)).AddTo(disposables);
                IsActiveConstructionOutput?.Subscribe(value=>ChangeBuildingAccess(building,true,false,value)).AddTo(disposables);
                placeDestroyViewData = new()
                {
                    InputConstructionSlots=InputConstructionSlots,
                    OutputConstructionSlots=OutputConstructionSlots,
                    IsActiveConstructionInput=IsActiveConstructionInput,
                    IsActiveConstructionOutput=IsActiveConstructionOutput,
                    ConstructionPriority= ConstructionPriority
                };
            }
            else placeDestroyViewData=null;
            recipeViewData=(false,null);
            if (entityManager.HasComponent<IsRecipeAssigned>(building) && entityManager.IsComponentEnabled<IsRecipeAssigned>(building))
            {
                recipeViewData.Item1=true;
                var recipeData=entityManager.GetComponentData<RecipeBuildingData>(building);
                TimeToCraft=new(recipeData.TimeToCraft);
                CurrTime=new(recipeData.CurrTime);
                CountInPack=new(entityManager.GetComponentData<CountOfPackInBuildingData>(building).CountOfPack);
                CountInPack.Subscribe(value =>
                {
                    ChangeCountOfPack(building,value);
                }).AddTo(disposables);;
                recipeViewData.Item2 = new()
                {
                    recipeIDHash=recipeData.RecipeIDHash,
                    CountInPack=CountInPack,
                    TimeToCraft=TimeToCraft,
                    CurrTime=CurrTime
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
                        Capacity.Subscribe(value=> ChangeStorageSlotCapacity(building,indexForLambda,value)).AddTo(storageDisposables);
                        ReactiveProperty<bool> IsActiveSlotInput=new(buff[i].IsInputEnabled);
                        ReactiveProperty<bool>  IsActiveSlotOutput=new(buff[i].IsOutputEnabled);

                        IsActiveSlotInput.Subscribe(value=>ChangeStorageSlotAccess(building,indexForLambda,true,value)).AddTo(storageDisposables);
                        IsActiveSlotOutput.Subscribe(value=>ChangeStorageSlotAccess(building,indexForLambda,false,value)).AddTo(storageDisposables);
                        slots[i]=new StorageSlotViewData{
                            ItemID=ItemID,
                            Amount=Amount,
                            Capacity=Capacity,
                            IsActiveInput=IsActiveSlotInput,
                            IsActiveOutput=IsActiveSlotOutput};
                    }
                    this.StorageSlots=new(slots);
                    StorageSlots=this.StorageSlots;
                }
            }
        }
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
    
    public void FixedUpdate(BuildingViewData viewDataData)
    {
        if (InputSlots != null)
            UpdateBuff<InputSlotData>(viewDataData.buildingEntity, InputSlots);
    
        if (OutputSlots != null)
            UpdateBuff<OutputSlotData>(viewDataData.buildingEntity, OutputSlots);
        
        
        if (InputConstructionSlots != null)
            UpdateBuff<InputConstructionSlotData>(viewDataData.buildingEntity, InputConstructionSlots);
        
        if (OutputConstructionSlots != null)
            UpdateBuff<OutputConstructionSlotData>(viewDataData.buildingEntity, OutputConstructionSlots);
        if (entityManager.HasComponent<PropTag>(viewDataData.buildingEntity) || entityManager.HasComponent<BuildingTag>(viewDataData.buildingEntity))
        {
            
            WorkState.Value=entityManager.GetComponentData<BuildingStateData>(viewDataData.buildingEntity).State;
            if (entityManager.HasComponent<IsRecipeAssigned>(viewDataData.buildingEntity)&&entityManager.IsComponentEnabled<IsRecipeAssigned>(viewDataData.buildingEntity))
            {
                var data=entityManager.GetComponentData<RecipeBuildingData>(viewDataData.buildingEntity);
                
               
                TimeToCraft.Value=data.TimeToCraft;
                CurrTime.Value=data.CurrTime;
            }


            UpdateStorageSlots(viewDataData);

           UpdateExcessSlots(viewDataData);
        }
    }
    private void UpdateStorageSlots(BuildingViewData viewDataData)
    {
        var entity = viewDataData.buildingEntity;

        if (!entityManager.HasComponent<StorageTypeBuildingTag>(entity) || 
            !entityManager.HasBuffer<StorageSlotData>(entity)) return;

        var buff = entityManager.GetBuffer<StorageSlotData>(entity);
        
        // 1. Если размер изменился — создаем НОВЫЙ массив, заполняем и ОДИН раз присваиваем
        if (StorageSlots.Value == null || StorageSlots.Value.Length != buff.Length)
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
                Capacity.Subscribe(value=> ChangeStorageSlotCapacity(entity,indexForLambda,value)).AddTo(storageDisposables);
                ReactiveProperty<bool> IsActiveSlotInput=new(buff[i].IsInputEnabled);
                ReactiveProperty<bool>  IsActiveSlotOutput=new(buff[i].IsOutputEnabled);

                IsActiveSlotInput.Subscribe(value=>ChangeStorageSlotAccess(entity,indexForLambda,true,value)).AddTo(storageDisposables);
                IsActiveSlotOutput.Subscribe(value=>ChangeStorageSlotAccess(entity,indexForLambda,false,value)).AddTo(storageDisposables);
                newSlots[i] = new StorageSlotViewData {
                    ItemID=ItemID,
                            Amount=Amount,
                            Capacity=Capacity,
                            IsActiveInput=IsActiveSlotInput,
                            IsActiveOutput=IsActiveSlotOutput};
            }
            // Только это действие триггерит обновление списка в UI (создание новых плашек)
            StorageSlots.Value = newSlots; 
        }
        else
        {
            // 2. Если размер ТОТ ЖЕ — просто обновляем значения внутри существующих объектов
            // Свойство StorageSlots.Value МЫ НЕ ТРОГАЕМ. 
            // UI-список не перерисовывается, обновляются только цифры в текстовых полях.
            var currentSlots = StorageSlots.Value;
            for (int i = 0; i < buff.Length; i++)
            {
                var data = buff[i];
                var slot = currentSlots[i];

                // Обновляем только примитивы внутри реактивных свойств
                slot.ItemID.Value = data.ItemId;
                slot.Amount.Value = data.Amount;
            }
        }
    }
    private void UpdateExcessSlots(BuildingViewData viewDataData)
    {
        var entity = viewDataData.buildingEntity;

        if (!entityManager.HasBuffer<ExcessSlotData>(entity)) return;

        var exbuff = entityManager.GetBuffer<ExcessSlotData>(entity);
        
        bool isSizeChanged = ExcessSlots.Value == null || ExcessSlots.Value.Length != exbuff.Length;

        SlotViewData[] currentSlots = isSizeChanged 
            ? new SlotViewData[exbuff.Length] 
            : ExcessSlots.Value;

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
            ExcessSlots.Value = currentSlots;
        }
        else
        {
            ExcessSlots.SetValueAndForceNotify(currentSlots);
        }
    }
    public void AddAmount(Entity entity,int amount)
    {
        
        if (entityManager.HasComponent<IsDemolition>(entity)&&entityManager.IsComponentEnabled<IsDemolition>(entity))
        {
            var buff = entityManager.GetBuffer<OutputConstructionSlotData>(entity);
            if(buff.Length<1) return;
            for(int i =0; i < buff.Length; i++)
            {
                var b = buff[i];
                b.Amount=math.clamp(b.Amount+amount,0,b.Capacity);
                buff[i]=b;
                if(b.Amount==b.Capacity) tempUpdate?.Invoke();
            }
        }
        if (entityManager.HasComponent<IsBlueprint>(entity)&&entityManager.IsComponentEnabled<IsBlueprint>(entity))
        {
            var buff = entityManager.GetBuffer<InputConstructionSlotData>(entity);
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
    public void SetRecipe(Entity entity, int RecipeID)
    {
        var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
               .CreateCommandBuffer();
        Entity Command=ecb.CreateEntity();
        ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=entity});
        ecb.AddComponent(Command,new SetRecipeData{RecipeID=RecipeID});
        
    }

    public void MarkAsDemolition(Entity entity,bool IsDemolition)
    {
        var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
               .CreateCommandBuffer();
        ecb.SetComponentEnabled<ChangeDemolitionStateTag>(entity,IsDemolition);
    }
    public void MarkAsForceDestory(Entity entity)
    {
        var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
               .CreateCommandBuffer();
        if(!entityManager.HasComponent<IsDemolition>(entity)) return;
        Entity Command=ecb.CreateEntity();
        ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=entity});
        ecb.AddComponent(Command,new MarkAsForceDestoroyData());
    }
    public void AddStorageSlot(Entity entity,int ItemID, int Capacity)
    {
        if (entityManager.HasBuffer<StorageSlotData>(entity))
        {
            var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
                .CreateCommandBuffer();
            var buff=entityManager.GetBuffer<StorageSlotData>(entity);
            if(buff.Length==buff.Capacity) return;
            
            Entity Command=ecb.CreateEntity();
            ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=entity});
            ecb.AddComponent(Command,new AddStorageSlotData{ItemID=ItemID,Capacity=Capacity});
        }
    }
    public void RemoveStorageSlot(Entity entity,int slotIND)
    {
        if (entityManager.HasBuffer<StorageSlotData>(entity))
        {     var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
                .CreateCommandBuffer();
            var buff=entityManager.GetBuffer<StorageSlotData>(entity);
            if(buff.Length<=slotIND) return;
            
            Entity Command=ecb.CreateEntity();
            ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=entity});
            ecb.AddComponent(Command,new RemoveStorageSlotData{slotIND=slotIND});
        }
    }   
    void ChangePriority(Entity entity,bool isConstrucionPriority,int priorityValue)
    {
       
        var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
            .CreateCommandBuffer();
        Entity Command=ecb.CreateEntity();
        ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=entity});
        if (isConstrucionPriority)
            ecb.AddComponent(Command,new ChangeConstructionPriotiyData{newPriority=priorityValue});
        else
            ecb.AddComponent(Command,new ChangeCraftPriotiyData{newPriority=priorityValue});
    }
    void ChangeBuildingAccess(Entity entity,bool isConstrucionAccess,bool IsInput,bool value)
    {
        var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
            .CreateCommandBuffer();
        Entity Command=ecb.CreateEntity();
        ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=entity});
        if (isConstrucionAccess)
            ecb.AddComponent(Command,new ChangeConstructionBuildingAccessData{IsInput=IsInput,IsEnabled=value});
        else
            ecb.AddComponent(Command,new ChangeProcessorBuildingAccessData{IsInput=IsInput,IsEnabled=value});
    }
    void ChangeStorageSlotAccess(Entity entity,int SlotIND,bool IsInput,bool value)
    {
        var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
            .CreateCommandBuffer();
        Entity Command=ecb.CreateEntity();
        ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=entity});
        ecb.AddComponent(Command,new ChangeStorageSlotAccessData{SlotIND=SlotIND,IsInput=IsInput,IsEnabled=value});
    }
    void ChangeStorageSlotCapacity(Entity entity,int SlotIND,int newCapacity)
    {
        var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
            .CreateCommandBuffer();
         Entity Command=ecb.CreateEntity();
        ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=entity});
        ecb.AddComponent(Command,new ChangeStorageSlotCapacityData{SlotIND=SlotIND,newCapacity=newCapacity});
    }
    void ChangeCountOfPack(Entity entity,int newCapacity)
    {
        var ecb= world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>()
            .CreateCommandBuffer();
        Entity Command=ecb.CreateEntity();
        ecb.AddComponent(Command,new ChangeBuildingData{targetEntity=entity});
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
    public bool IsProcessor;
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