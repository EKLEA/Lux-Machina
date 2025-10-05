using System;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using Zenject;

public class BuildingHealth
{
    public float Health => _data.Health;
    public string UnicID => _data.UnicID;
    
    private BuildingHealthData _data;
    public event Action OnDead;
    public event Action<string, bool> OnHealthStateChanged; 
    
    private float MaxHealth, RestoreHealthPerSecond, TimeToStartRestore;

    public BuildingHealth(BuildingHealthData data)
    {
        _data = data;
    }

    public void Initialize(float maxHealth, float restoreHealthPerSecond, float timeToStartRestore)
    {
        this.MaxHealth = maxHealth;
        this.RestoreHealthPerSecond = restoreHealthPerSecond;
        this.TimeToStartRestore = timeToStartRestore;
    }

    public void AddHP(float hp)
    {
        if(_data.Health + hp <= 0) OnDead?.Invoke();
        else if (_data.Health + hp >= MaxHealth)
        {
            _data.Health = MaxHealth;
            OnHealthStateChanged?.Invoke(_data.UnicID, true);
        }
        else _data.Health += hp;
    }

    public void RemoveHP(float hp)
    {
        OnHealthStateChanged?.Invoke(_data.UnicID, false);
        _data.CurrTimeToStartRestore = TimeToStartRestore;
        AddHP(-hp);
    }

    public void RestoreHealth(TickableEvent tickableEvent)
    {
        if (_data.CurrTimeToStartRestore > 0)
            _data.CurrTimeToStartRestore -= tickableEvent.DeltaTime;
        else
            AddHP(RestoreHealthPerSecond * tickableEvent.DeltaTime);
    }
}