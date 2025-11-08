
using Unity.Entities;

public struct AddToClusterTag : IComponentData {}
public struct RemoveClusterTag : IComponentData { }

public struct ClusterId : IComponentData
{
    public int Value;
}