
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
     
     EntityQuery _raycastQuery;
    EntityQuery _buildQuery;
    int c=0;
    int a=0;
    bool b=true;
    public void SetUpGrid(GridVisualizer visualizer,IPlayerData playerData,FlowFieldVisualizer flowFieldVisualizer, AttackZoneVisualizer attackZoneVisualizer)//
    {
        _visualizer=visualizer;
        _playerData =playerData;
        this.flowFieldVisualizer=flowFieldVisualizer;
       _attackZoneVisualizer=attackZoneVisualizer;
      

    }
     protected override void OnCreate()
    {
        _buildQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<PlayerCommand>()
        .WithAny<PlayerPlacingBuilding,PlayerDeletePoints,PlayerPlacingManyPointBuilding>()
        .Build(this);
          _raycastQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<PlayerRayCastData>()
        .Build(this);
        RequireForUpdate(_buildQuery);
    }
    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingleton<BuildingMap>(out var map)) return;
      
         // 1. Завершаем только то, что связано с рейкастом (быстро, так как query готов)
        _raycastQuery.GetDependency().Complete();

        // 2. Теперь спокойно читаем
        var data = SystemAPI.GetSingleton<PlayerRayCastData>();
        
        if (!_buildQuery.IsEmpty)
        {
             _visualizer.DrawGrid(
                data.PlaceBlockPos, 
                15, 
                map
            );
        }
      
        else _visualizer?.Clear();
        //   if (a % 500 == 0)
        // {
        //     _attackZoneVisualizer.DrawAttackZones(SystemAPI.GetComponent<TurretGrid>(SystemAPI.GetSingletonEntity<BuildingMap>()));
        // }
        // else a++;
        // c++;
        // if(c<100) return;
        // if (b)
        // {
        //       flowFieldVisualizer.DrawFlowField(
        //         map
        //     );
        //     b=false;
        // }
    }
}
