
using Unity.Entities;
using UnityEngine;

public struct MakePhantomTag : IComponentData { }
public struct PhantomTag : IComponentData { }

public struct RemovePhantomTag : IComponentData { }
public class GameObjectReference : IComponentData
{
    public BuildingOnScene gameObject;
}
public struct CreateVisualTag : IComponentData { }
public struct DestroyVisualTag : IComponentData { }
public struct ModifyVisualTag: IComponentData { }
