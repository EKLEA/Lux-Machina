using System;
using System.Collections.Generic;
using System.Linq;

public abstract class BuildingLogic : IResourceConsumer
{
    public string UnicID => _data.UnicID;
    public string BuildingID => _data.buildingID;
    public Priority Priority => _data.priority;
    public bool IsEnergyConnected { get; protected set; }
    public Dictionary<int, SlotData> InputSlots => _data.innerStorageSlots;
    
    protected BuildingLogicData _data;
    protected BuildingOnScene _buildingVisual;
    
    public BuildingLogic(BuildingLogicData data, BuildingOnScene buildingVisual)
    {
        _data = data;
        _buildingVisual = buildingVisual;
    }
    
    public abstract void LogicPerTick(TickableEvent tickEvent);
    public abstract void CheckAndRegisterRequests();
    public abstract void ReceiveResources(string resourceType, int amount);
    
    public BuildingState CurrentState { get; protected set; }
    
   
    public event Action<string, string, int> OnRequestRegistered;
    public event Action<string> OnStateChanged;
    
   
    public event Action<string> OnSubscriptionShouldChange;
    
    public void SetConnected(bool connected)
    {
        if (IsEnergyConnected != connected)
        {
            IsEnergyConnected = connected;
            if (connected) CheckAndRegisterRequests();
            else  UpdateState(BuildingState.PowerOff);
            
        }
    }
    
    protected void UpdateState(BuildingState newState)
    {
        if (CurrentState != newState)
        {
            CurrentState = newState;
            OnStateChanged?.Invoke(UnicID);
            OnSubscriptionShouldChange?.Invoke(UnicID);
        }
    }
    
    public bool ShouldSubscribeToTicks()
    {
        return IsEnergyConnected && CurrentState != BuildingState.PowerOff;
    }
  
    protected int GetItemCountInSlots(string itemID)
    {
        return InputSlots.Values
            .Where(slot => slot.itemID == itemID)
            .Sum(slot => slot.Amount);
    }
    
    protected virtual void AddItemToSlots(string itemID, int amount)
    {
       
    }
    
    protected virtual void RemoveItemFromSlots(string itemID, int amount)
    {
        
    }
}
public enum BuildingState
{
    Await,
    Work,
    PowerOff
}