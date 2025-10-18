using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public struct BuildingMap : IComponentData
{
    public NativeParallelHashMap<int2, int> CellToBuildingMap;
}

[BurstCompile]
public partial class BuildingPositionSystem : SystemBase
{
    private EntityQuery _buildingQuery;
    private EntityQuery _roadQuery;
    private Entity _buildingMapEntity;
    private NativeParallelHashMap<int2, int> _readOnlyMap;
    private bool _needsRebuild;

    protected override void OnCreate()
    {
        var query = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BuildingMap>()
            .Build(this);
            
        if (query.IsEmpty)
        {
            _buildingMapEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(_buildingMapEntity, new BuildingMap
            {
                CellToBuildingMap = new NativeParallelHashMap<int2, int>(1000, Allocator.Persistent)
            });
        }
        else
        {
            _buildingMapEntity = query.GetSingletonEntity();
        }
        
        // Создаем отдельную карту для чтения
        _readOnlyMap = new NativeParallelHashMap<int2, int>(1000, Allocator.Persistent);
        
        _buildingQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<BuildingPosData, PosData>()
            .WithNone<PhantomTag>()
            .Build(this);
            
        _roadQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<RoadPosData, PosData>()
            .Build(this);
            
        _needsRebuild = true;
    }

    protected override void OnUpdate()
    {
        if (!_needsRebuild) return;
        
        var buildingMap = EntityManager.GetComponentData<BuildingMap>(_buildingMapEntity);
        buildingMap.CellToBuildingMap.Clear();
        
        var processBuildingsJob = new ProcessBuildingsJob
        {
            CellToBuildingMap = buildingMap.CellToBuildingMap.AsParallelWriter()
        };
        
        var processRoadsJob = new ProcessRoadsJob
        {
            CellToBuildingMap = buildingMap.CellToBuildingMap.AsParallelWriter()
        };
        
        var buildingsHandle = processBuildingsJob.ScheduleParallel(_buildingQuery, Dependency);
        var roadsHandle = processRoadsJob.ScheduleParallel(_roadQuery, Dependency);
        
        Dependency = JobHandle.CombineDependencies(buildingsHandle, roadsHandle);
        Dependency.Complete(); // Завершаем job'ы
        
        // Копируем данные в read-only карту
        _readOnlyMap.Clear();
        foreach (var pair in buildingMap.CellToBuildingMap)
        {
            _readOnlyMap.TryAdd(pair.Key, pair.Value);
        }
        
        EntityManager.SetComponentData(_buildingMapEntity, buildingMap);
        _needsRebuild = false;
    }

    public void MarkForRebuild()
    {
        _needsRebuild = true;
    }

    public bool CanBuildHere(int2 position)
    {
        return !_readOnlyMap.ContainsKey(position);
    }
    
    public int GetBuildingInThere(int2 position)
    {
        if (_readOnlyMap.ContainsKey(position))
            return _readOnlyMap[position];
        return -1;
    }

    protected override void OnDestroy()
    {
        if (EntityManager.Exists(_buildingMapEntity))
        {
            var buildingMap = EntityManager.GetComponentData<BuildingMap>(_buildingMapEntity);
            buildingMap.CellToBuildingMap.Dispose();
        }
        _readOnlyMap.Dispose();
    }
}

[BurstCompile]
public partial struct ProcessBuildingsJob : IJobEntity
{
    public NativeParallelHashMap<int2, int>.ParallelWriter CellToBuildingMap;
    
    [BurstCompile]
    private void Execute(in BuildingPosData buildingPos, in PosData posData)
    {
        var leftCorner = buildingPos.LeftCornerPos;
        var size = buildingPos.Size;
        
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                var cellPos = new int2(leftCorner.x + x, leftCorner.y + y);
                CellToBuildingMap.TryAdd(cellPos, posData.BuildingIDHash);
            }
        }
    }
}

[BurstCompile]
public partial struct ProcessRoadsJob : IJobEntity
{
    public NativeParallelHashMap<int2, int>.ParallelWriter CellToBuildingMap;
    
    [BurstCompile]
    private void Execute(in RoadPosData roadPos, in PosData posData)
    {
        var start = roadPos.FirstPoint;
        var end = roadPos.EndPoint;
        
        int steps = math.max(math.abs(end.x - start.x), math.abs(end.y - start.y)) + 1;
        
        for (int i = 0; i < steps; i++)
        {
            float t = steps > 1 ? (float)i / (steps - 1) : 0f;
            var cellPos = new int2(
                (int)math.lerp(start.x, end.x, t),
                (int)math.lerp(start.y, end.y, t)
            );
            CellToBuildingMap.TryAdd(cellPos, posData.BuildingIDHash);
        }
    }
}