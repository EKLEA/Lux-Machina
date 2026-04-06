using Unity.Entities;

public struct PlayerPlacingBuilding : IComponentData, IEnableableComponent { }
public struct PlayerPlacingManyPointBuilding: IComponentData, IEnableableComponent { }
public struct PlayerDeletePoints: IComponentData, IEnableableComponent { }
public struct PlayerConnectBuildings: IComponentData, IEnableableComponent { }
public struct PlayerCommand: IComponentData{}