using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using Zenject;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(MapSystem))]
[UpdateAfter(typeof(RoadSystem))]
public partial class BuildingVisualSystem : SystemBase
{
    [Inject] BuildingObjectFactorty _buildingFactory;
    [Inject] PhantomObjectFactory _phantomFactory;
    
    Dictionary<int, GameObject> _buildingVisuals = new Dictionary<int, GameObject>();
    Dictionary<int, RoadOnScene> _roadVisuals = new Dictionary<int, RoadOnScene>();

    protected override void OnCreate()
    {
        
    }

    protected override void OnUpdate()
    {
        if (!SystemAPI.HasSingleton<BuildingMap>())
            return;
        if (_buildingFactory == null || _phantomFactory == null) return;
        
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        var buildingMap = SystemAPI.GetSingletonRW<BuildingMap>();
        
        if (!buildingMap.ValueRO.CellEntity.IsCreated)
            return;

        using (var mapCopy = new NativeParallelHashMap<int2, Entity>(buildingMap.ValueRO.CellEntity.Count(), Allocator.TempJob))
        {
            foreach (var pair in buildingMap.ValueRO.CellEntity)
                mapCopy.TryAdd(pair.Key, pair.Value);

            CreateVisuals(ecb, mapCopy);
            ModifyVisual(ecb);
            HandlePhantomization(ecb);
            DestroyVisuals(ecb);
            
            buildingMap.ValueRW.CellEntity.Clear();
            foreach (var pair in mapCopy)
                buildingMap.ValueRW.CellEntity.TryAdd(pair.Key, pair.Value);
        } 

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    void ModifyVisual(EntityCommandBuffer ecb)
    {
       foreach (var (gameObjectRef, mewpoints, entity) in SystemAPI.Query<GameObjectReference,DynamicBuffer<MapPointData>>()
                     .WithAll<RoadTag,ModifyVisualTag>()
                     .WithEntityAccess())
        {
            var road = gameObjectRef.gameObject as RoadOnScene;
             var points = new Vector2Int[mewpoints.Length];
            for (int i = 0; i < mewpoints.Length; i++)
                points[i] = new Vector2Int(mewpoints[i].Value.x, mewpoints[i].Value.y);
            road.GenerateRoadMesh(points);
        }
    }
    void CreateVisuals(EntityCommandBuffer ecb,NativeParallelHashMap<int2,Entity> map)
    {


        foreach (var (buildingPoints,buildingData, posData, entity) in SystemAPI.Query<DynamicBuffer<MapPointData>,BuildingData, BuildingPosData>()
                    .WithAll<CreateVisualTag, ProcessBuildingEventComponent>()
                    .WithNone<GameObjectReference>()
                    .WithEntityAccess())
        {
            CreateBuildingVisual(entity, buildingData, posData,buildingPoints, ecb,map);
        }
         foreach (var (buildingPoints, buildingData, entity) in SystemAPI.Query<DynamicBuffer<MapPointData>, BuildingData>()
                    .WithAll<CreateVisualTag, RoadTag>()
                    .WithNone<GameObjectReference>()
                    .WithEntityAccess())
        {
            CreateRoadVisual(entity,buildingData,buildingPoints, ecb,map);
            
        }
        
    }

    void HandlePhantomization(EntityCommandBuffer ecb)
    {
        foreach (var (gameObjectRef, entity) in SystemAPI.Query<GameObjectReference>()
                .WithAll<MakePhantomTag>()
                .WithNone<PhantomTag>()
                .WithEntityAccess())
        {
            MakePhantom(entity, gameObjectRef, ecb);
        }

        foreach (var (gameObjectRef, entity) in SystemAPI.Query<GameObjectReference>()
                .WithAll<RemovePhantomTag, PhantomTag>()
                .WithEntityAccess())
        {
            MakeNormal(entity, gameObjectRef, ecb);
        }

        foreach (var (gameObjectRef, entity) in SystemAPI.Query<GameObjectReference>()
                .WithAll<RemovePhantomTag>()
                .WithNone<PhantomTag>() 
                .WithEntityAccess())
        {
            MakeNormal(entity, gameObjectRef, ecb);
        }
    }

    void DestroyVisuals(EntityCommandBuffer ecb)
    {
        foreach (var (buildingData,gameObjectRef, entity) in SystemAPI.Query<BuildingData,GameObjectReference>()
                     .WithAll<DestroyVisualTag>()
                     .WithEntityAccess())
        {
            DestroyVisual(entity, buildingData,gameObjectRef, ecb);
        }
    }

    void CreateBuildingVisual(Entity entity, BuildingData buildingData, BuildingPosData posData,
    DynamicBuffer<MapPointData> buildingPoints, EntityCommandBuffer ecb,
    NativeParallelHashMap<int2,Entity> map)
    {
        var building = EntityManager.CreateEntity();
        ecb.AddComponent(building, buildingData);
        ecb.AddComponent(building, posData);
        
        Vector2Int pos = new Vector2Int(posData.LeftCornerPos.x, posData.LeftCornerPos.y);
        var buildingVisual = _buildingFactory.CreateBuilding(buildingData.BuildingIDHash, pos, posData.Rotation);
        var buildingOnScene = buildingVisual.GetComponent<BuildingOnScene>();
        if (buildingOnScene == null)
            buildingOnScene = buildingVisual.gameObject.AddComponent<BuildingOnScene>();

        buildingOnScene.id = buildingData.UniqueIDHash;
        buildingOnScene.CreateClusterIndicator();

        _buildingVisuals[buildingData.UniqueIDHash] = buildingVisual.gameObject;

        ecb.AddComponent(building, new GameObjectReference
        {
            gameObject = buildingOnScene
        });
        foreach (var p in buildingPoints)
                map.TryAdd(p.Value, building);
        ecb.RemoveComponent<CreateVisualTag>(entity);
        ecb.RemoveComponent<BuildingPosData>(entity);
        ecb.RemoveComponent<BuildingData>(entity);
        ecb.RemoveComponent<MapPointData>(entity);
        if (EntityManager.HasComponent<BluePrint>(entity)) ecb.AddComponent(building, new MakePhantomTag());

        ecb.RemoveComponent<BluePrint>(entity);
        ecb.AddComponent(building, new AssignLogicTag());
        ecb.AddComponent(building, new AssignHealthTag());
    }

    void CreateRoadVisual(Entity entity, BuildingData buildingData, DynamicBuffer<MapPointData> roadPoints, EntityCommandBuffer ecb,
    NativeParallelHashMap<int2,Entity> map)
    {
        
        var points = new Vector2Int[roadPoints.Length];
        for (int i = 0; i < roadPoints.Length; i++)
        {
            points[i] = new Vector2Int(roadPoints[i].Value.x, roadPoints[i].Value.y);
        }

        var roadVisual = _buildingFactory.CreateRoad(buildingData.BuildingIDHash, points);
        var roadOnScene = roadVisual.GetComponent<RoadOnScene>();
            if (roadOnScene == null)
                roadOnScene = roadVisual.gameObject.AddComponent<RoadOnScene>();

            roadOnScene.id = buildingData.UniqueIDHash;
            roadOnScene.CreateRoadClusterIndicator();

            _roadVisuals[buildingData.UniqueIDHash] = roadOnScene;
            ecb.AddComponent(entity, new GameObjectReference
            {
                gameObject = roadOnScene
            });
            foreach (var p in roadPoints)
                map.TryAdd(p.Value, entity);
            ecb.RemoveComponent<CreateVisualTag>(entity);
    }

    void MakePhantom(Entity entity, GameObjectReference gameObjectRef, EntityCommandBuffer ecb)
    {
        GameObject visualObject = gameObjectRef.gameObject.gameObject;

        if (visualObject != null && visualObject.scene.IsValid())
        {
            _phantomFactory.PhantomizeObject(visualObject);
            ecb.AddComponent<PhantomTag>(entity);
            ecb.RemoveComponent<MakePhantomTag>(entity);
        }
    }

    void MakeNormal(Entity entity, GameObjectReference gameObjectRef, EntityCommandBuffer ecb)
    {
         GameObject visualObject = gameObjectRef.gameObject.gameObject;

        if (visualObject != null && visualObject.scene.IsValid())
        {
            _phantomFactory.UnPhantomizeObject(visualObject);
            ecb.RemoveComponent<PhantomTag>(entity);
            ecb.RemoveComponent<RemovePhantomTag>(entity);
        }
    }

    void DestroyVisual(Entity entity,BuildingData buildingData, GameObjectReference gameObjectRef, EntityCommandBuffer ecb)
    {
        if (_buildingVisuals.TryGetValue(buildingData.UniqueIDHash, out var buildingVisual))
        {
            if (buildingVisual != null)
                Object.Destroy(buildingVisual);
            _buildingVisuals.Remove(buildingData.UniqueIDHash);
            ecb.RemoveComponent<BuildingPosData>(entity);
        }

        if (_roadVisuals.TryGetValue(buildingData.UniqueIDHash, out var roadVisual))
        {
            if (roadVisual != null)
                Object.Destroy(roadVisual.gameObject);
            _roadVisuals.Remove(buildingData.UniqueIDHash);
            ecb.RemoveComponent<RoadTag>(entity);
            ecb.RemoveComponent<MapPointData>(entity);
        }

        ecb.RemoveComponent<BuildingData>(entity);
        ecb.RemoveComponent<GameObjectReference>(entity);
        ecb.RemoveComponent<DestroyVisualTag>(entity);
    }
    
    protected override void OnDestroy()
    {
        foreach (var visual in _buildingVisuals.Values)
            if (visual != null) Object.Destroy(visual);
        foreach (var roadVisual in _roadVisuals.Values)
            if (roadVisual != null) Object.Destroy(roadVisual.gameObject);
        
        _buildingVisuals.Clear();
        _roadVisuals.Clear();
        base.OnDestroy();
    }
}