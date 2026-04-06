using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;


public struct BuildingMap : IComponentData, IDisposable
{
    public NativeParallelHashMap<int2, int> CellMapBuildingsIDs; 
    public NativeParallelHashMap<int2, Entity> CellMapEntites;
    public NativeParallelMultiHashMap<Entity, int2> CellEntityMultiMap;
    public NativeParallelHashMap<int2, bool> IsBluePrintOrDemolitionPoints; //true blueprint false demolition
    public NativeParallelHashMap<int2, float> CellWeights;    
    public NativeParallelHashMap<int2, float2> CellDirections;
    public int2 CorePos;

    public void Dispose()
    {
        CellMapBuildingsIDs.Dispose();
        CellMapEntites.Dispose();
        CellEntityMultiMap.Dispose();
        IsBluePrintOrDemolitionPoints.Dispose();
        CellWeights.Dispose();
        CellDirections.Dispose();
    }
}
public struct EnergyMap: IComponentData, IDisposable
{
    
    public NativeParallelMultiHashMap<int2,int> CellToEnergyBuildingMap; 
    public NativeParallelMultiHashMap<int2,Entity> CellToEnergyEntityBuildingMap; 
    public NativeParallelMultiHashMap<Entity,int2> EnergyEntityToCellBuildingMap; 
    public NativeParallelHashMap<int2,int2> EnergyLinks; 
    
    public int CoreID;

    public void Dispose()
    {
       CellToEnergyBuildingMap.Dispose();
       CellToEnergyEntityBuildingMap.Dispose();
       EnergyEntityToCellBuildingMap.Dispose();
       EnergyLinks.Dispose();
    }
}

public struct ResourceMap : IComponentData, IDisposable
{
    
    public NativeParallelHashMap<int2,int2> ResouecesMap; //x- айди предмета, y количество
    public void Dispose()
    {
        ResouecesMap.Dispose();
    }
}
public struct TurretGrid : IComponentData, IDisposable
{
    public NativeParallelMultiHashMap<int, Entity> EnemyGridMap;
    public NativeParallelMultiHashMap<Entity,int> EnemyToTurret;
    public NativeParallelMultiHashMap<int2, int> TurretGridClaim;
    public NativeParallelMultiHashMap<int2, Entity> EnemyInCellsMap; 
    public float CellSize;
    public void Dispose()
    {
        EnemyGridMap.Dispose();
        TurretGridClaim.Dispose();
        EnemyToTurret.Dispose();
        EnemyInCellsMap.Dispose();
    }

}
public struct SpawnMobsData : IComponentData, IEnableableComponent
{
    public int CountOfCicle;
    public float pointsToSpawnMobs;
    public float pointsPerCicle;
    public float totalWeights;
    public float playerProgress;
    public float AttackThreshold => math.max(50f, playerProgress * 0.8f); 

}
[InternalBufferCapacity(128)]
public struct SpawnPointElement : IBufferElementData
{
    public int2 Position;
    public float Weight;
}

public struct SpawnPointComparer : IComparer<SpawnPointElement>
{
    public int Compare(SpawnPointElement x, SpawnPointElement y) => y.Weight.CompareTo(x.Weight);
}
public struct EntitiesDictionary: IComponentData, IDisposable
{
    public NativeParallelHashMap<int, Entity> Entities;

    public void Dispose()
    {
        Entities.Dispose();
    }
}
public struct ClusterMap : IComponentData, IDisposable
{
    public NativeList<int> UniqueClusterIDs;
    public NativeParallelHashMap<int2, int> pointToClusterId;
    public NativeParallelMultiHashMap<int, int2> logisticPoints;
    public NativeParallelMultiHashMap<int, SlotReference> ClusterToProducers;
    public NativeParallelMultiHashMap<int, SlotReference> ClusterToConsumers;
    public NativeParallelMultiHashMap<Entity, SlotReference> EntityInputSlots;
    public NativeParallelMultiHashMap<Entity, SlotReference> EntityOutputSlots;
    
    public NativeParallelMultiHashMap<SlotReference, SlotReference> SlotGraph;
    public NativeParallelMultiHashMap<SlotReference, SlotReference> ReverseSlotGraph;
    public NativeList<SlotReference> AllProducersList; 
    public NativeParallelHashMap<SlotReference, FixedList32Bytes<int>> SlotToClusters;


    public ClusterMap(Allocator allocator)
    {
        UniqueClusterIDs = new NativeList<int>(5000,allocator);
        ClusterToProducers = new NativeParallelMultiHashMap<int, SlotReference>(5000, allocator);
        ClusterToConsumers = new NativeParallelMultiHashMap<int, SlotReference>(5000, allocator);
        SlotGraph = new NativeParallelMultiHashMap<SlotReference, SlotReference>(5000, allocator);
        ReverseSlotGraph = new NativeParallelMultiHashMap<SlotReference, SlotReference>(5000, allocator);
        EntityInputSlots = new NativeParallelMultiHashMap<Entity, SlotReference>(5000, allocator);
        EntityOutputSlots = new NativeParallelMultiHashMap<Entity, SlotReference>(5000, allocator);
        pointToClusterId = new NativeParallelHashMap<int2, int> (5000, allocator);
        logisticPoints = new NativeParallelMultiHashMap<int, int2> (5000, allocator);
        AllProducersList = new NativeList<SlotReference>(10000, allocator);
        SlotToClusters = new NativeParallelHashMap<SlotReference, FixedList32Bytes<int>>(10000, allocator);
    }

    public void Dispose()
    {
        if (UniqueClusterIDs.IsCreated) UniqueClusterIDs.Dispose();
        if (pointToClusterId.IsCreated) pointToClusterId.Dispose();
        if (ClusterToProducers.IsCreated) ClusterToProducers.Dispose();
        if (ClusterToConsumers.IsCreated) ClusterToConsumers.Dispose();
        if (EntityInputSlots.IsCreated) EntityInputSlots.Dispose();
        if (EntityOutputSlots.IsCreated) EntityOutputSlots.Dispose();
        if (SlotGraph.IsCreated) SlotGraph.Dispose();
        if (ReverseSlotGraph.IsCreated) ReverseSlotGraph.Dispose();
        if (logisticPoints.IsCreated) logisticPoints.Dispose();
        if (AllProducersList.IsCreated) AllProducersList.Dispose();
        if (SlotToClusters.IsCreated) SlotToClusters.Dispose();
    }
}
public enum SlotType : byte 
{
    Input, Output, Excess, StorageInput,StorageOutput, InputConstruction, OutputConstruction
}
public struct SlotReference : IEquatable<SlotReference>, IComparable<SlotReference>
{
    public Entity Owner;
    public SlotType Type;
    public int ItemID;
    public int Index;
    public byte Priority; 
    // для продусеров 0 -лишние, 1-10 - конструктион, 11-20 - производители, 21-30 - сундук 
    // для получателей 1-10 - конструктион, 11-20 - производители, 21-30 - сундук если максимум + 100
    public bool Equals(SlotReference other) => 
        Owner.Equals(other.Owner) && Type == other.Type && Index == other.Index&&ItemID==other.ItemID;

    public override int GetHashCode() => 
        HashCode.Combine(Owner, (int)Type, Index,ItemID);

    public int CompareTo(SlotReference other)
    {
        int result = Priority.CompareTo(other.Priority);
        
        if (result == 0)
            result = Owner.Index.CompareTo(other.Owner.Index);
            
        return result;
    }
}
public struct UpdateMapTag : IComponentData, IEnableableComponent { }
public struct UpdateClustersTag:IComponentData, IEnableableComponent{}
public struct UpdateClusterSlots:IComponentData, IEnableableComponent{}
public struct UpdateConnectionsTag:IComponentData, IEnableableComponent{}
public struct WorldTime : IComponentData
{
    public long CurrentTick;       
    public int TicksPerDay;       
    public float dayLength;
    
    public float SpeedMultiplier;  
    public float baseTick;
    public float acceleretedTick=>baseTick*SpeedMultiplier;
    public int CurrentDay => (int)(CurrentTick / TicksPerDay);
    public float DayProgress => (float)(CurrentTick % TicksPerDay) / TicksPerDay;
    public float Sunrise => 0.5f - (dayLength / 2f);
    public float Sunset => 0.5f + (dayLength / 2f);

    public bool IsDay => DayProgress >= Sunrise && DayProgress <= Sunset;

    public float LocalProgress
    {
        get
        {
            if (IsDay)
            {
                return (DayProgress - Sunrise) / dayLength;
            }
            else
            {
                float nightDuration = 1f - dayLength;
                if (DayProgress >= Sunset)
                    return (DayProgress - Sunset) / nightDuration;
                return (DayProgress + (1f - Sunset)) / nightDuration;
            }
        }
    }
}
public struct IsTickFrame :IComponentData,IEnableableComponent{}
public struct IsPause:IComponentData,IEnableableComponent{}
public struct IsGameOver:IComponentData,IEnableableComponent{}
public struct LoadingMapTag : IComponentData, IEnableableComponent { }
public struct SavingMapTag : IComponentData, IEnableableComponent { }
public struct ProductionTable : IComponentData, IDisposable
{
    public NativeParallelMultiHashMap<int, RecipeIngredientStruct> produced;
    public NativeParallelMultiHashMap<int, RecipeIngredientStruct> consumed;

    public void Dispose()
    {
        produced.Dispose();
        consumed.Dispose();
    }
}
public struct MapPoint : IBufferElementData
{
    public int2 pos;
}
public struct PathfindingRequest : IComponentData, IEnableableComponent
{
    public int BuildingID;
    public int2 Start;
    public int2 End;
    public bool SamePerfer;
}
public struct DisablePathfindingTag : IComponentData, IEnableableComponent { }
public struct MapUpdateRequest : IBufferElementData
{
    public int2 Pos;
    public int BuildingHash;
    public Entity Entity;
}