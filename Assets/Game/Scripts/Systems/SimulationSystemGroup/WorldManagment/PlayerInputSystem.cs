
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Zenject;
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TickGeneratorSystem))]
[BurstCompile]

public partial struct PlayerInputSystem : ISystem 
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<WorldSettings>();
    }

    
    [BurstCompile]
    public void OnUpdate(ref SystemState state) 
    {
        var settings = SystemAPI.GetSingleton<WorldSettings>();
        var cMap = SystemAPI.GetSingleton<ChunkMap>();
        state.Dependency= new VoxelRaycastJob {
            World = settings,
            ChunkMap=cMap.ChunkMapData,
            BlockLookup=SystemAPI.GetBufferLookup<BlockElement>(true)
        }.Schedule(state.Dependency);
    }
    [BurstCompile]
    public partial struct VoxelRaycastJob : IJobEntity
    {
        [ReadOnly] public WorldSettings World;
        [ReadOnly] public NativeParallelHashMap<int2, Entity> ChunkMap;
        [ReadOnly] public BufferLookup<BlockElement> BlockLookup;

      public void Execute(ref PlayerRayCastData data)
{
    data.HasHit = false;

    // 1. Безопасность: cellSize не может быть 0
    float cell = math.max(World.cellSize, 0.001f);
    
    // 2. Переводим Origin в пространство индексов сетки
    float3 scaledOrigin = data.Origin / cell;
    
    int3 currentPos = (int3)math.floor(scaledOrigin);
    int3 step = (int3)math.sign(data.Direction);
    
    // Направление нормализуем, чтобы tDelta считался корректно
    float3 rayDir = math.normalize(data.Direction);
    float3 safeDir = math.select(rayDir, new float3(0.00001f), math.abs(rayDir) < 0.00001f);
    float3 tDelta = math.abs(1.0f / safeDir); 
    
    float3 posF = (float3)currentPos;
    float3 tMax = math.select((scaledOrigin - posF) * tDelta, (posF + 1.0f - scaledOrigin) * tDelta, step > 0);
    tMax = math.abs(tMax); 

    float distance = 0;
    float scaledMaxDist = data.MaxDistance / cell;
    int3 lastNormal = int3.zero;

    int2 lastChunkCoord = new int2(int.MinValue);
    bool hasCurrentChunk = false;
    DynamicBuffer<BlockElement> currentBlocks = default;

while (distance < scaledMaxDist)
{
    // 1. ПРАВИЛЬНЫЙ КЛЮЧ ЧАНКА (для словаря)
    // Используем float деление и floor, чтобы -1/32 стало -1, а не 0.
    int2 chunkCoord = new int2(
        (int)math.floor(currentPos.x / (float)World.Size),
        (int)math.floor(currentPos.z / (float)World.Size)
    );

    if (!chunkCoord.Equals(lastChunkCoord))
    {
        lastChunkCoord = chunkCoord;
        hasCurrentChunk = ChunkMap.TryGetValue(chunkCoord, out Entity chunkEntity) && BlockLookup.HasBuffer(chunkEntity);
        if (hasCurrentChunk) currentBlocks = BlockLookup[chunkEntity];
    }

    if (hasCurrentChunk)
    {
        // 2. ПРАВИЛЬНЫЙ ЛОКАЛЬНЫЙ ИНДЕКС
        // Вычитаем глобальную позицию чанка из глобальной позиции вокселя.
        // Если воксель -38, а чанк -2 (начало на -64), то: -38 - (-2 * 32) = -38 + 64 = 26. Это верно.
        int localX = currentPos.x - (chunkCoord.x * World.Size);
        int localZ = currentPos.z - (chunkCoord.y * World.Size);

        if (currentPos.y >= 0 && currentPos.y < World.Height)
        {
            // 3. ПРАВИЛЬНАЯ ФОРМУЛА ИНДЕКСА
            // Важно, чтобы порядок осей совпадал с тем, как ты заполнял чанк в GenerateChunkJob
            int index = localX + (currentPos.y * World.Size) + (localZ * World.Size * World.Height);
            
            if (index >= 0 && index < currentBlocks.Length)
            {
                if (currentBlocks[index].BlockID != 0)
                {
                    data.HasHit = true;
                    data.HitBlockPos = currentPos;
                    data.PlaceBlockPos = currentPos + lastNormal;
                    data.HitBlockID = (int)currentBlocks[index].BlockID;
                    return;
                }
            }
        }
    }

    // --- ШАГ DDA (проверь, нет ли тут путаницы с осями) ---
    if (tMax.x < tMax.y) {
        if (tMax.x < tMax.z) {
            distance = tMax.x; tMax.x += tDelta.x; currentPos.x += step.x;
            lastNormal = new int3(-step.x, 0, 0);
        } else {
            distance = tMax.z; tMax.z += tDelta.z; currentPos.z += step.z;
            lastNormal = new int3(0, 0, -step.z);
        }
    } else {
        if (tMax.y < tMax.z) {
            distance = tMax.y; tMax.y += tDelta.y; currentPos.y += step.y;
            lastNormal = new int3(0, -step.y, 0);
        } else {
            distance = tMax.z; tMax.z += tDelta.z; currentPos.z += step.z;
            lastNormal = new int3(0, 0, -step.z);
        }
    }
}
}
    }

}