public class TurretBuildingLogic : BuildingLogic
{
    private string _ammoType = "Ammo";
    private int _minAmmoThreshold = 20;
    private bool _hasRegisteredDemand = false;
    private float _shootCooldown = 1f;
    private float _currentCooldown = 0f;
    
    public TurretBuildingLogic(BuildingLogicData data, BuildingOnScene buildingVisual) 
        : base(data, buildingVisual) { }
    
    public override void LogicPerTick(TickableEvent tickEvent)
    {
        if (CurrentState == BuildingState.Work)
        {
            _currentCooldown -= tickEvent.DeltaTime;
            if (_currentCooldown <= 0f)
            {
                ConsumeResources(); 
                _currentCooldown = _shootCooldown;
            }
        }
        
        CheckAndRegisterRequests();
    }
    
    public override void CheckAndRegisterRequests()
    {
        if (!IsEnergyConnected) return;
        
        int currentAmmo = GetItemCountInSlots(_ammoType);
        if (currentAmmo < _minAmmoThreshold && !_hasRegisteredDemand)
        {
            int needed = 100 - currentAmmo; 
            //OnDemandRegistered?.Invoke(UnicID, _ammoType, needed);
            _hasRegisteredDemand = true;
            UpdateState(BuildingState.Await);
        }
    }
    
    public override void ReceiveResources(string resourceType, int amount)
    {
        if (resourceType == _ammoType)
        {
            AddItemToSlots(resourceType, amount);
            _hasRegisteredDemand = false;
           // OnDemandFulfilled?.Invoke(UnicID, resourceType, amount);
            
            if (GetItemCountInSlots(_ammoType) >= _minAmmoThreshold)
            {
                UpdateState(BuildingState.Work);
            }
        }
    }
    
    private void ConsumeResources()
    {
        if (GetItemCountInSlots(_ammoType) > 0)
        {
            RemoveItemFromSlots(_ammoType, 1);
            
            if (GetItemCountInSlots(_ammoType) < _minAmmoThreshold)
            {
                CheckAndRegisterRequests();
            }
        }
    }
}