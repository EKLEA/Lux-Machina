
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Zenject;

[DisableAutoCreation]
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class SunUpdateSystem : SystemBase
{
    [Inject] SunController sunController;
    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingleton<WorldTime>(out var worldTime)) return;

        sunController.UpdateVisuals(worldTime.IsDay,worldTime.LocalProgress);
    }
}
