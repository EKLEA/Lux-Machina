using System;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class BuildingHealth
{
    public float Health
    {
        get => _data.Health;
    }
    public string UnicID
    {
        get => _data.UnicID;
    }
    BuildingHealthData _data;
    public event Action OnDead;
    public event Action<string,bool> IHealAllHP;
    public bool ISubscibed;
    float MaxHealth,RestoreHealthPerSecond,TimeToStartRestore;
    public BuildingHealth(BuildingHealthData data)
    {
        _data = data;
    }
    public void Initialize(float MaxHealth, float RestoreHealthPerSecond, float TimeToStartRestore)
    {
        this.MaxHealth = MaxHealth;
        this.RestoreHealthPerSecond = RestoreHealthPerSecond;
        this.TimeToStartRestore = TimeToStartRestore;

        _data.Health = MaxHealth;
        _data.CurrTimeToStartRestore = TimeToStartRestore;
    }
    public void AddHP(float hp)
    {

        if (_data.Health + hp <= 0) OnDead?.Invoke();
        else if (_data.Health + hp >= MaxHealth)
        {
            _data.Health = MaxHealth;
            if (ISubscibed) IHealAllHP?.Invoke(_data.UnicID, true);
        }
        else _data.Health += hp;
    }
    public void RemoveHP(float hp)
    {
        if (!ISubscibed) IHealAllHP?.Invoke(_data.UnicID,false);
        _data.CurrTimeToStartRestore = TimeToStartRestore;
        AddHP(-hp);
    }
    public void RestoreHealth(TickableEvent tickEvent)
    {
        if (_data.CurrTimeToStartRestore > 0) _data.CurrTimeToStartRestore -= tickEvent.DeltaTime;
        else AddHP(RestoreHealthPerSecond * tickEvent.DeltaTime);
    }
}