
using System;
using Unity.Entities;
[Serializable]
public class HealthData : IComponentData
{
    public float Health;
    public float MaxHealth;
    public float timeToRestore;
    public float currTime;
}