using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ItemDistributionSystem))]
[BurstCompile]
public partial struct ProcessSlotModificationsSystem : ISystem
{
    [BurstCompile]
    public partial struct ProcessSlotModificationsJob : IJobEntity
    {
        void Execute(DynamicBuffer<SlotModification> modifications, DynamicBuffer<SlotData> slotBuffer)
        {
            foreach (var modification in modifications)
            {
                if (modification.SlotIndex < slotBuffer.Length)
                {
                    var slot = slotBuffer[modification.SlotIndex];
                    
                    if (modification.ItemId == -1) 
                    {
                        slot.Amount = math.max(0, slot.Amount - modification.Amount);
                        if (slot.Amount == 0) slot.ItemId = 0;
                    }
                    else 
                    {
                        if (slot.ItemId == 0 || slot.ItemId == modification.ItemId)
                        {
                            slot.ItemId = modification.ItemId;
                            slot.Amount = math.min(slot.Capacity, slot.Amount + modification.Amount);
                        }
                    }
                    
                    slotBuffer[modification.SlotIndex] = slot;
                }
            }
            
            modifications.Clear();
        }
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var processJob = new ProcessSlotModificationsJob();
        state.Dependency = processJob.Schedule(state.Dependency);
    }
}