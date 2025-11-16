using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public partial class PublicBuildingMapSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
    }

    protected override void OnUpdate() { }

    

    public bool GetEntity(int2 pos, out Entity entity)
    {
        entity = Entity.Null;
        
        if (!SystemAPI.TryGetSingleton<BuildingMap>(out var buildingMap))
            return false;
        
        if (buildingMap.CellEntity.TryGetValue(pos, out entity))
        {
            return entity != Entity.Null;
        }
        
        return false;
    }
    public bool GetEntity(int id, out Entity entity)
    {
        entity = Entity.Null;
        
        if (!SystemAPI.TryGetSingleton<BuildingMap>(out var buildingMap))
            return false;
        
        if (buildingMap.Entities.TryGetValue(id, out entity))
        {
            return entity != Entity.Null;
        }
        
        return false;
    }
    public bool CanBuildAt(NativeArray<int2> positions, bool isRoad)
    {
        CompleteDependency();

        if (!SystemAPI.TryGetSingleton<BuildingMap>(out var buildingMap))
            return false;

        for (int i = 0; i < positions.Length; i++)
        {
            var position = positions[i];

            if (buildingMap.CellMapBuildings.TryGetValue(position, out var existingBuildingID))
            {
                if (isRoad)
                {
                    if (!IsRoadBuilding(existingBuildingID))
                        return false;
                }
                else
                {
                    return false;
                }
            }
        }

        return true;
    }

    public int GetBuildingAt(int2 position)
    {
        CompleteDependency();

        if (!SystemAPI.TryGetSingleton<BuildingMap>(out var buildingMap))
            return -1;

        return buildingMap.CellMapBuildings.TryGetValue(position, out var buildingID)
            ? buildingID
            : -1;
    }

    public int GetUniqueIDAt(int2 position)
    {
        CompleteDependency();

        if (!SystemAPI.TryGetSingleton<BuildingMap>(out var buildingMap))
            return -1;

        return buildingMap.CellMapIDs.TryGetValue(position, out var uniqueID) ? uniqueID : -1;
    }

    public bool IsPositionOccupiedByBuilding(int2 position)
    {
        CompleteDependency();

        if (!SystemAPI.TryGetSingleton<BuildingMap>(out var buildingMap))
            return false;

        if (buildingMap.CellMapBuildings.TryGetValue(position, out var buildingID))
        {
            return !IsRoadBuilding(buildingID);
        }

        return false;
    }

    public bool IsPositionOccupiedByRoad(int2 position)
    {
        CompleteDependency();

        if (!SystemAPI.TryGetSingleton<BuildingMap>(out var buildingMap))
            return false;

        if (buildingMap.CellMapBuildings.TryGetValue(position, out var buildingID))
        {
            return IsRoadBuilding(buildingID);
        }

        return false;
    }

    bool IsRoadBuilding(int buildingID)
    {
        return buildingID == "Road".GetStableHashCode();
    }
}
