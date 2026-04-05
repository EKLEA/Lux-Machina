
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
[DisableAutoCreation]
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class GridUpdateSystem : SystemBase
{
     GridVisualizer _visualizer;
     FlowFieldVisualizer flowFieldVisualizer;
     AttackZoneVisualizer _attackZoneVisualizer;
     IPlayerData _playerData;
     
    EntityQuery _buildQuery;
    public void SetUpGrid(GridVisualizer visualizer,IPlayerData playerData,FlowFieldVisualizer flowFieldVisualizer)//, AttackZoneVisualizer attackZoneVisualizer
    {
        _visualizer=visualizer;
        _playerData =playerData;
        this.flowFieldVisualizer=flowFieldVisualizer;
       // _attackZoneVisualizer=attackZoneVisualizer;

    }
     protected override void OnCreate()
    {
        _buildQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<PlayerCommand>()
        .WithAny<PlayerPlacingBuilding,PlayerDeletePoints,PlayerPlacingRoad>()
        
        .Build(this);
        RequireForUpdate(_buildQuery);
    }
    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingleton<BuildingMap>(out var map)) return;

        // flowFieldVisualizer.DrawFlowField(
        //       new Vector2Int(0,0), 
        //         100, 
        //         map
        // );

        //_attackZoneVisualizer.DrawAttackZones(SystemAPI.GetComponent<TurretGrid>(SystemAPI.GetSingletonEntity<BuildingMap>()));
        if (!_buildQuery.IsEmpty)
        {
             _visualizer.DrawGrid(
                _playerData.pos, 
                15, 
                map
            );
        }
        else _visualizer?.Clear();
    }
}
