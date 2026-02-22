using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class GridUpdateSystem : SystemBase
{
     GridVisualizer _visualizer;
     IPlayerData _playerData;
     
    EntityQuery _buildQuery;
    public void SetUpGrid(GridVisualizer visualizer,IPlayerData playerData)
    {
        _visualizer=visualizer;
        _playerData =playerData;

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
