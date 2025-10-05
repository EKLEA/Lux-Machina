
using System;

public interface IResourceConsumer
{
    string UnicID { get; }
    public abstract void CheckAndRegisterRequests();
    public abstract void ReceiveResources(string resourceType, int amount);
    public event Action<string, string, int> OnRequestRegistered;
}

public interface IResourceProducer:IResourceConsumer
{
    bool Has(string resourceType);
    string[] GetResources();
    int GetResourceAmount(string resourceType);
    void RemoveResources(string resourceType, int amount);
    
}