
using Unity.Entities;


public struct ProcessBuildingEventComponent : IComponentData { }
public struct ProcessMapEventComponent : IComponentData { }

public struct BluePrint : IComponentData { }

public struct AssignLogicTag: IComponentData { }
public struct AssignHealthTag : IComponentData { }
public struct DestroyEntityTag : IComponentData { }