using Unity.Entities;
using UnityEngine;

public class ECSSystemsManager
{
    readonly World _world;
    bool _systemsCreated = false;

    public ECSSystemsManager()
    {
        _world = World.DefaultGameObjectInjectionWorld;
    }

    public void EnableGameplaySystems()
    {
        if (_world == null || !_world.IsCreated)
        {
            Debug.LogWarning("ECS World is not available");
            return;
        }

        try
        {
            if (!_systemsCreated)
            {
                _systemsCreated = true;
            }

            SetSystemGroupEnabled<InitializationSystemGroup>(true);
            SetSystemGroupEnabled<SimulationSystemGroup>(true);
            SetSystemGroupEnabled<PresentationSystemGroup>(true);

            SetSystemEnabled<PublicBuildingMapSystem>(true);
            SetSystemEnabled<PathfindingSystem>(true);
            SetSystemEnabled<BuildingVisualSystem>(true);
            SetSystemEnabled<BuildingLogicAssignSystem>(true);

            Debug.Log("Gameplay systems enabled successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to enable gameplay systems: {e.Message}");
        }
    }

    void CreateGameplaySystems()
    {
        try
        {
            _world.CreateSystem<PublicBuildingMapSystem>();

            _world.CreateSystem<RoadSystem>();

            _world.GetOrCreateSystemManaged<BuildingVisualSystem>();
            _world.GetOrCreateSystemManaged<PathfindingSystem>();
            _world.GetOrCreateSystemManaged<PublicBuildingMapSystem>();
            _world.GetOrCreateSystemManaged<BuildingLogicAssignSystem>();

            //Debug.Log("Gameplay systems created successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to create gameplay systems: {e.Message}");
        }
    }

    public void DisableGameplaySystems()
    {
        if (_world == null)
            return;

        try
        {
            SetSystemGroupEnabled<InitializationSystemGroup>(false);
            SetSystemGroupEnabled<SimulationSystemGroup>(false);
            SetSystemGroupEnabled<PresentationSystemGroup>(false);

            Debug.Log("Gameplay systems disabled");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to disable gameplay systems: {e.Message}");
        }
    }

    void SetSystemGroupEnabled<T>(bool enabled)
        where T : ComponentSystemGroup
    {
        try
        {
            var group = _world.GetExistingSystemManaged<T>();
            if (group != null)
            {
                group.Enabled = enabled;
                Debug.Log($"System group {typeof(T).Name} {(enabled ? "enabled" : "disabled")}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to set system group {typeof(T).Name}: {e.Message}");
        }
    }

    void SetSystemEnabled<T>(bool enabled)
        where T : ComponentSystemBase
    {
        try
        {
            var system = _world.GetExistingSystemManaged<T>();
            if (system != null)
            {
                system.Enabled = enabled;
                Debug.Log($"System {typeof(T).Name} {(enabled ? "enabled" : "disabled")}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to set system {typeof(T).Name}: {e.Message}");
        }
    }
}
