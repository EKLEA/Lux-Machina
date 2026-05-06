
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Splines.ExtrusionShapes;
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PlayerInputSystem))]
[BurstCompile]

public partial struct TerrainSystem : ISystem
{
    EntityQuery GenerateChunk;
    EntityQuery GenerateMeshChunk;
    EntityQuery RegisterChunk;
    
    EntityArchetype _chunkArchetype;
    public void OnCreate(ref SystemState state)
    {
         _chunkArchetype = state.EntityManager.CreateArchetype(
            typeof(ChunkData),
            typeof(BlockElement),
            typeof(ResourceElement),
            typeof(ChunkMeshState),
            
            typeof(MarkOnMap),
            typeof(UpdateChunkDataTag),
            typeof(UpdateVisualTag),
            typeof(IsVisibleTag), 
            typeof(NeedsCleanupTag), 
            typeof(VertexElement),
            typeof(IndexElement), 
            typeof(ChangeVisibleChunkState),
            typeof(ChangeLODChunkState),
            typeof(LocalTransform), 
            typeof(RenderBounds)
        );
        GenerateChunk= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<CreateChunk,ModifiedBlockElement>()
            .Build(ref state);
        GenerateMeshChunk= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<UpdateVisualTag,ChunkMeshState>()
            .Build(ref state);
         RegisterChunk= new EntityQueryBuilder(Allocator.Temp)
            .WithAll<ChunkData,MarkOnMap>()
            .Build(ref state);
        
        state.RequireForUpdate<PlayerData>();
        
    }
    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var settings = SystemAPI.GetSingleton<WorldSettings>();
        var cMap = SystemAPI.GetSingletonRW<ChunkMap>(); 
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
        if (!RegisterChunk.IsEmpty)
        {
            state.Dependency=new RegisterChunksOnMapJob{ChunkMap=cMap.ValueRW.ChunkMapData.AsParallelWriter(),ECB=ecb}.ScheduleParallel(state.Dependency);
        }
       if (!GenerateChunk.IsEmpty)
        {
            state.Dependency = new GenerateChunkJob {
                World = settings,
                chunkArchetype = _chunkArchetype,
                ECB = ecb,
                ChangeLODLookup = SystemAPI.GetComponentLookup<ChangeLODChunkState>(true),
            }.ScheduleParallel(state.Dependency);
        }
        if (!GenerateMeshChunk.IsEmpty)
        {
            state.Dependency = new GenerateMeshBuffersJob{World=settings}.ScheduleParallel(state.Dependency);
        }
        
    }
    
    [BurstCompile]
    [WithAll(typeof(MarkOnMap))]
    public partial struct RegisterChunksOnMapJob : IJobEntity
    {
        public NativeParallelHashMap<int2, Entity>.ParallelWriter ChunkMap;
        public EntityCommandBuffer.ParallelWriter ECB;
        public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, in ChunkData chunkData)
        {
            ChunkMap.TryAdd(chunkData.Position, entity);

            ECB.SetComponentEnabled<MarkOnMap>(chunkIndex, entity,false);
        }
    }


    
    [BurstCompile]
    public partial struct GenerateChunkJob : IJobEntity
    {
        [ReadOnly] public WorldSettings World;
        public EntityArchetype chunkArchetype;
        public EntityCommandBuffer.ParallelWriter ECB;
        [ReadOnly] public ComponentLookup<ChangeLODChunkState> ChangeLODLookup;

       public void Execute(Entity entity, ref DynamicBuffer<ModifiedBlockElement> modifiedBlocks, in CreateChunk chunkData, [ChunkIndexInQuery] int chunkIndex)
        {
            Entity chunk = ECB.CreateEntity(chunkIndex, chunkArchetype);
            if (ChangeLODLookup.HasComponent(entity))
            {
                ECB.SetComponent(chunkIndex, chunk, new ChunkMeshState { CurrentLOD = ChangeLODLookup[entity].newLIOD });
            }
            var resBuffer = ECB.AddBuffer<ResourceElement>(chunkIndex, chunk);
            ECB.SetComponent(chunkIndex, chunk, new ChunkData { Position = chunkData.Position });
            float3 worldOrigin = new float3(chunkData.Position.x * World.Size, 0, chunkData.Position.y * World.Size);
            ECB.SetComponent(chunkIndex, chunk, LocalTransform.FromPosition(worldOrigin));

            var blocks = ECB.SetBuffer<BlockElement>(chunkIndex, chunk);
            blocks.ResizeUninitialized(World.Size * World.Height * World.Size);

            float offsetX = chunkData.Position.x * World.Size;
            float offsetZ = chunkData.Position.y * World.Size;

           for (int z = 0; z < World.Size; z++)
            {
                for (int x = 0; x < World.Size; x++)
                {
                    float2 worldXZ = new float2(offsetX + x, offsetZ + z);
                    float dist = math.length(worldXZ);
                    
                    
                    float plainMask = math.smoothstep(40f, 60f, dist); 

                    
                    float rawNoise = (noise.snoise(worldXZ * World.TerrainScale + World.Seed) + 1f) * 0.5f;
                    float terraceInput = rawNoise * World.TerraceSteps;
                    float terraceMask = (math.floor(terraceInput) + math.smoothstep(0.05f, 0.15f, terraceInput % 1f)) / World.TerraceSteps;
                    
                    
                    float mountainHeight = terraceMask * World.HeightMultiplier * plainMask;
                    float biomeNoise = noise.snoise(worldXZ * World.BiomeScale + (World.Seed + 100));
                    float biomeWeight = math.saturate(biomeNoise * 2.5f) * plainMask; 

                    
                    float surfaceHeight = World.PlainsHeight + (mountainHeight * biomeWeight);

                    
                    float2 step = new float2(0.1f, 0.1f);
                    float rawNoiseNext = (noise.snoise((worldXZ + step) * World.TerrainScale + World.Seed) + 1f) * 0.5f;
                    float terraceNext = (math.floor(rawNoiseNext * World.TerraceSteps) + math.smoothstep(0.05f, 0.15f, (rawNoiseNext * World.TerraceSteps) % 1f)) / World.TerraceSteps;
                    float surfaceHeightNext = World.PlainsHeight + (terraceNext * World.HeightMultiplier * biomeWeight);
                    float steepness = math.abs(surfaceHeight - surfaceHeightNext);

                    float dirtNoise = noise.snoise(worldXZ * 0.02f + (World.Seed + 123));

                    for (int y = 0; y < World.Height; y++)
                    {
                        byte blockID = 0;
                        if (y <= (int)surfaceHeight)
                        {
                            int depth = (int)surfaceHeight - y;
                            
                            // ТОЛЬКО ПОВЕРХНОСТЬ (верхний блок)
                            if (depth == 0)
                            {
                                bool isStartingArea = dist < 70f;
                                bool isFlatLowland = (int)surfaceHeight <= (int)World.PlainsHeight;

                                byte oreBlockID = 0;
                                int oreAmount = 0;
                                int resourceID = 0; // Для буфера ResourceElement

                                if (isFlatLowland) 
                                {
                                    // Проверяем руды и сразу сопоставляем ID ресурса
                                    if (CheckOre(worldXZ, World.Iron, World.Seed + 1, dist, out oreAmount, isStartingArea, 0)) { oreBlockID = 4; resourceID = 1; }
                                    else if (CheckOre(worldXZ, World.Copper, World.Seed + 2, dist, out oreAmount, isStartingArea, 1)) { oreBlockID = 5; resourceID = 2; }
                                    else if (CheckOre(worldXZ, World.Tin, World.Seed + 3, dist, out oreAmount, isStartingArea, 2)) { oreBlockID = 6; resourceID = 3; }
                                    else if (CheckOre(worldXZ, World.Coal, World.Seed + 4, dist, out oreAmount, isStartingArea, 3)) { oreBlockID = 7; resourceID = 4; }
                                    else if (CheckOre(worldXZ, World.Stone, World.Seed + 5, dist, out oreAmount, isStartingArea, 4)) { oreBlockID = 1; resourceID = 5; }
                                }

                                if (oreBlockID != 0)
                                {
                                    blockID = oreBlockID;
                                    // Спавним данные для добычи только если это руда
                                    SpawnOre(new int3(x, y, z), resourceID, oreAmount, ref resBuffer); 
                                }
                                else 
                                {
                                    // Обычный ландшафт, если руды нет
                                    if (steepness > 0.15f) blockID = 1;
                                    else if (dirtNoise > 0.75f) blockID = 2; 
                                    else blockID = 3; 
                                }
                            }
                            // ВСЁ ЧТО НИЖЕ (depth > 0) — только камень или земля
                            else if (depth < 3)
                            {
                                blockID = (steepness > 0.7f) ? (byte)1 : (byte)2;
                            }
                            else 
                            {
                                blockID = 1; // Глубинный камень
                            }
                        }

                        // ВАЖНО: Твоя формула индекса X -> Y -> Z
                        int index = x + World.Size * (y + World.Height * z);
                        blocks[index] = new BlockElement { BlockID = blockID };
                    }
                }
            }
            ECB.DestroyEntity(chunkIndex, entity);
        }
        bool CheckOre(float2 pos, OreSettings settings, uint seed, float dist, out int amount, bool isStartingArea, int oreIndex)
        {
            amount = 0;
            float baseAmountPerBlock = 500f; 

            if (isStartingArea)
            {
                float angle = (oreIndex * (math.PI * 2f) / 5f); 
                float2 oreCenter = new float2(math.cos(angle), math.sin(angle)) * 25f;
                float distToPatchCenter = math.distance(pos, oreCenter);
                
                if (distToPatchCenter < 6f) 
                {
                    
                    float patchFalloff = math.lerp(1.0f, 0.1f, distToPatchCenter / 6f);
                    amount = (int)(baseAmountPerBlock * settings.Richness * patchFalloff);
                    return true;
                }
            }
            else
            {
                float n = (noise.snoise(pos * settings.Size + seed) + 1f) * 0.5f;
                float baseThreshold = 0.95f; 
                float activeThreshold = baseThreshold - settings.Frequency;

                if (n > activeThreshold)
                {
                    
                    float worldDistFactor = 1.0f + (dist / 1000f); 
                    float patchDensity = (n - activeThreshold) / (1.0f - activeThreshold);
                    
                    float patchFalloff = math.smoothstep(0f, 1f, patchDensity);

                    amount = (int)(baseAmountPerBlock * settings.Richness * worldDistFactor * patchFalloff);
                    return true;
                }
            }
            return false;
        }
        void SpawnOre(int3 pos, int configID, int amount, ref DynamicBuffer<ResourceElement> resBuffer)
        {
           resBuffer.Add(new ResourceElement { 
                LocalPos = pos, 
                ID = configID, 
                Amount = amount 
            });
        }
    }



    [BurstCompile]
    [WithAll(typeof(UpdateVisualTag))]
    public partial struct GenerateMeshBuffersJob : IJobEntity 
    {
        [ReadOnly] public WorldSettings World;
        
         public void Execute(ref DynamicBuffer<VertexElement> vertices, ref DynamicBuffer<IndexElement> indices,
            in DynamicBuffer<BlockElement> blocks, in ChunkMeshState meshState)
        {
            vertices.Clear();
            indices.Clear();

            int step = (int)math.pow(2, meshState.CurrentLOD);
            int vCount = 0;

            for (int z = 0; z < World.Size; z += step) {
                for (int x = 0; x < World.Size; x += step) {
                    
                    for (int y = World.Height - step; y >= 0; y -= step) {

                        if (IsAir(blocks, x, y, z)) continue;

                        int trueSurfaceY = GetSurfaceY(blocks, x, z, 1); 

                        int blockID = 0;
                        if (trueSurfaceY >= 0) {
                            blockID = (int)blocks[GetIndex(x, trueSurfaceY, z)].BlockID;
                        }

                        
                        float off00 = GetSlopeOffset(blocks, x, z, y, step);
                        float off01 = GetSlopeOffset(blocks, x, z + step, y, step);
                        float off11 = GetSlopeOffset(blocks, x + step, z + step, y, step);
                        float off10 = GetSlopeOffset(blocks, x + step, z, y, step);

                        
                        if (IsAir(blocks, x, y + step, z)) {
                            bool flip = math.abs(off00 - off11) < math.abs(off01 - off10);
                            AddQuad(vertices, indices, ref vCount, 
                                new float3(x, y + step + off00, z), 
                                new float3(x, y + step + off01, z + step), 
                                new float3(x + step, y + step + off11, z + step), 
                                new float3(x + step, y + step + off10, z),
                                blockID, flip);
                        }

                        int sRight = GetSurfaceY(blocks, x + step, z,step);
                        if (sRight < y) {
                            
                            float nOff00 = GetSlopeOffset(blocks, x + step, z, sRight, step);
                            float nOff01 = GetSlopeOffset(blocks, x + step, z + step, sRight, step);
                            float bottomY = sRight + 1; 

                            AddQuad(vertices, indices, ref vCount, 
                                new float3(x + step, bottomY + nOff00, z), 
                                new float3(x + step, y + step + off10, z), 
                                new float3(x + step, y + step + off11, z + step), 
                                new float3(x + step, bottomY + nOff01, z + step),
                                blockID);
                        }

                        int sLeft = GetSurfaceY(blocks, x - step, z,step);
                        if (sLeft < y) {
                            float nOff00 = GetSlopeOffset(blocks, x - step, z, sLeft, step);
                            float nOff01 = GetSlopeOffset(blocks, x - step, z + step, sLeft, step);
                            float bottomY = sLeft + 1;

                            AddQuad(vertices, indices, ref vCount, 
                                new float3(x, bottomY + nOff01, z + step), 
                                new float3(x, y + step + off01, z + step), 
                                new float3(x, y + step + off00, z), 
                                new float3(x, bottomY + nOff00, z),
                                blockID);
                        }

                        
                        int sForward = GetSurfaceY(blocks, x, z + step,step);
                        if (sForward < y) {
                            float nOff01 = GetSlopeOffset(blocks, x, z + step, sForward, step);
                            float nOff11 = GetSlopeOffset(blocks, x + step, z + step, sForward, step);
                            float bottomY = sForward + 1;

                            AddQuad(vertices, indices, ref vCount, 
                                new float3(x + step, bottomY + nOff11, z + step), 
                                new float3(x + step, y + step + off11, z + step), 
                                new float3(x, y + step + off01, z + step), 
                                new float3(x, bottomY + nOff01, z + step),
                                blockID);
                        }

                        
                        int sBack = GetSurfaceY(blocks, x, z - step,step);
                        if (sBack < y) {
                            float nOff00 = GetSlopeOffset(blocks, x, z - step, sBack, step);
                            float nOff10 = GetSlopeOffset(blocks, x + step, z - step, sBack, step);
                            float bottomY = sBack + 1;

                            AddQuad(vertices, indices, ref vCount, 
                                new float3(x, bottomY + nOff00, z), 
                                new float3(x, y + step + off00, z), 
                                new float3(x + step, y + step + off10, z), 
                                new float3(x + step, bottomY + nOff10, z),
                                blockID);
                        }

                        
                        if (IsAir(blocks, x, y - step, z)) {
                            AddQuad(vertices, indices, ref vCount,
                                new float3(x, y, z + step),
                                new float3(x + step, y, z + step),
                                new float3(x + step, y, z),
                                new float3(x, y, z),
                                blockID);
                        }
                        
                        
                        break; 
                    }
                }
            }
        }

        
        int GetIndex(int x, int y, int z) => x + World.Size * (y + World.Height * z);

        bool IsAir(DynamicBuffer<BlockElement> blocks, int x, int y, int z) {
            if (x < 0 || x >= World.Size || z < 0 || z >= World.Size) return true;
            if (y < 0) return false;
            if (y >= World.Height) return true;
            return blocks[GetIndex(x, y, z)].BlockID == 0;
        }

        int GetSurfaceY(DynamicBuffer<BlockElement> blocks, int x, int z, int step) {
            if (x < 0 || x >= World.Size || z < 0 || z >= World.Size) return -step;
            
            for (int y = World.Height - 1; y >= 0; y--) {
                if (blocks[GetIndex(x, y, z)].BlockID != 0) {
                    return (y / step) * step; 
                }
            }
            return -step;
        }

        float GetSlopeOffset(DynamicBuffer<BlockElement> blocks, int vx, int vz, int currentY, int step) {
            bool n0 = GetHeightDiff(blocks, vx, vz, currentY, step) > 0;
            bool n1 = GetHeightDiff(blocks, vx - step, vz, currentY, step) > 0;
            bool n2 = GetHeightDiff(blocks, vx, vz - step, currentY, step) > 0;
            bool n3 = GetHeightDiff(blocks, vx - step, vz - step, currentY, step) > 0;
            
            return (n0 || n1 || n2 || n3) ? (float)step : 0f;
        }

        int GetHeightDiff(DynamicBuffer<BlockElement> blocks, int nx, int nz, int currentY, int step) {
            if (nx < 0 || nx >= World.Size || nz < 0 || nz >= World.Size) return 0;
            int neighborY = GetSurfaceY(blocks, nx, nz, step);
            return (neighborY > currentY) ? 1 : 0;
        }

        void AddQuad(DynamicBuffer<VertexElement> vertices, DynamicBuffer<IndexElement> indices, ref int vCount, 
            float3 v0, float3 v1, float3 v2, float3 v3, int blockID, bool flipDiagonal = false) 
        {
            float3 edge1 = v1 - v0;
            float3 edge3 = v3 - v0;
            float3 normal = math.cross(edge1, edge3);

            if (math.lengthsq(normal) < 0.001f) {
                normal = new float3(0, 1, 0);
            } else {
                normal = math.normalize(normal);
            }

            float tileSize = 0.2f; 
            float padding = 0.02f; 
            float uMin = (blockID % 5) * tileSize + padding;
            float vMin = math.floor(blockID / 5f) * tileSize + padding;
            float uMax = uMin + tileSize - (padding * 2);
            float vMax = vMin + tileSize - (padding * 2);
            
            vertices.Add(new VertexElement { Position = v0, Normal = normal, UV = new float2(uMin, vMin), BlockID = blockID });
            vertices.Add(new VertexElement { Position = v1, Normal = normal, UV = new float2(uMin, vMax), BlockID = blockID });
            vertices.Add(new VertexElement { Position = v2, Normal = normal, UV = new float2(uMax, vMax), BlockID = blockID });
            vertices.Add(new VertexElement { Position = v3, Normal = normal, UV = new float2(uMax, vMin), BlockID = blockID });

            if (!flipDiagonal) {
                indices.Add(new IndexElement { Value = vCount + 0 });
                indices.Add(new IndexElement { Value = vCount + 1 });
                indices.Add(new IndexElement { Value = vCount + 2 });
                indices.Add(new IndexElement { Value = vCount + 0 });
                indices.Add(new IndexElement { Value = vCount + 2 });
                indices.Add(new IndexElement { Value = vCount + 3 });
            } else {
                indices.Add(new IndexElement { Value = vCount + 1 });
                indices.Add(new IndexElement { Value = vCount + 2 });
                indices.Add(new IndexElement { Value = vCount + 3 });
                indices.Add(new IndexElement { Value = vCount + 1 });
                indices.Add(new IndexElement { Value = vCount + 3 });
                indices.Add(new IndexElement { Value = vCount + 0 });
            }

            vCount += 4;
        }
    }
}
