
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
        // 1. Первый запуск: Рейкаст мыши (оригинальная логика)
        data.HasHit = false;
        RunDDA(data.Origin, data.Direction, data.MaxDistance, out data.HasHit, out data.HitBlockPos, out data.PlaceBlockPos, out data.HitBlockID);

        // 2. Второй запуск: Рейкаст камеры строго вниз
        data.CamHasHit = false;
        int3 dummyPlace; // нам не важно, где воздух перед блоком для камеры
        int dummyID;
        RunDDA(data.CamOrigin, data.CamDirection, data.CamMaxDistance, out data.CamHasHit, out data.CamHitBlockPos, out dummyPlace, out dummyID);
    }

    // Твой оригинальный алгоритм DDA, просто упакованный в функцию для переиспользования
    private void RunDDA(float3 origin, float3 direction, float maxDist, out bool hasHit, out int3 hitBlockPos, out int3 placeBlockPos, out int hitBlockID)
    {
        hasHit = false;
        hitBlockPos = int3.zero;
        placeBlockPos = int3.zero;
        hitBlockID = 0;

        float cell = math.max(World.cellSize, 0.001f);
        float3 scaledOrigin = origin / cell;
        int3 currentPos = (int3)math.floor(scaledOrigin);
        int3 step = (int3)math.sign(direction);
        
        float3 rayDir = math.normalize(direction);
        float3 safeDir = math.select(rayDir, new float3(0.00001f), math.abs(rayDir) < 0.00001f);
        float3 tDelta = math.abs(1.0f / safeDir);
        float3 posF = (float3)currentPos;
        
        float3 tMax = math.select((scaledOrigin - posF) * tDelta, (posF + 1.0f - scaledOrigin) * tDelta, step > 0);
        tMax = math.abs(tMax);
        
        float distance = 0;
        float scaledMaxDist = maxDist / cell;
        int3 lastNormal = int3.zero;
        int2 lastChunkCoord = new int2(int.MinValue);
        bool hasCurrentChunk = false;
        DynamicBuffer<BlockElement> currentBlocks = default;

        while (distance < scaledMaxDist)
        {
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
                int localX = currentPos.x - (chunkCoord.x * World.Size);
                int localZ = currentPos.z - (chunkCoord.y * World.Size);

                if (currentPos.y >= 0 && currentPos.y < World.Height)
                {
                    int index = localX + (currentPos.y * World.Size) + (localZ * World.Size * World.Height);
                    
                    if (index >= 0 && index < currentBlocks.Length)
                    {
                        if (currentBlocks[index].BlockID != 0)
                        {
                            hasHit = true;
                            hitBlockPos = currentPos;
                            placeBlockPos = currentPos + lastNormal;
                            hitBlockID = (int)currentBlocks[index].BlockID;
                            return;
                        }
                    }
                }
            }

            // ШАГ DDA
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