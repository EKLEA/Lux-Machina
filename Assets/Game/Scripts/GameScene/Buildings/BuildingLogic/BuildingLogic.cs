using System;
using System.Collections.Generic;

public abstract class BuildingLogic
{
    public string UnicID { get => _data.UnicID; }
    public string buildingID { get => _data.buildingID; }
    public Dictionary<int, SlotData> innerStorageSlots { get => _data.innerStorageSlots; }
    public Priority priority
    {
        get => _data.priority;
        set
        {
            if (value < Priority.Low)
                _data.priority = Priority.Low;
            else if (value > Priority.Hight)
                _data.priority = Priority.Hight;
            else
                _data.priority = value;
        }
    }
    public bool IsConnected { get => _data.IsConnected; set => _data.IsConnected = value; }
    public bool IsHaveEnergy { get => _data.IsHaveEnergy; set => _data.IsHaveEnergy = value; }
    protected BuildingLogicData _data;
    protected BuildingOnScene building;
    public BuildingLogic(BuildingLogicData data, BuildingOnScene buildingOnScene)
    {
        _data = data;
        building = buildingOnScene;
    }
    public BuildingStatus CurrState { get; private set; }
    public abstract void LogicPerTick(TickableEvent tickEvent);
    public event Action<string, bool> IChangeWorkStatusTo;
    public bool ISubscibed;
}
public enum BuildingStatus
{
    Await,
    Work,
    PowerOff
}