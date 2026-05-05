
using Unity.Entities;
using Unity.Mathematics;

public struct OreSettings
{
    public float Frequency;  // Частота появления пятен (аналог "Частоты" в Factorio)
    public float Size;       // Радиус/Размер пятна (аналог "Размера")
    public float Richness;   // Базовое кол-во ресурсов (аналог "Богатства")
}

public struct WorldSettings : IComponentData
{
    public uint Seed;
    public int Size;      
    public int Height;
    public float cellSize;
     public float SafeZoneRadius;
    public float TerrainScale;
    public float BiomeScale;
    public float HeightMultiplier;
    public float TerraceSteps;
    public float PlainsHeight;
    public float Smoothness;
    public float DetailScale;       
    public float ErosionThreshold;

    // Настройки конкретных руд (как в Factorio)
    public OreSettings Iron;   // ID 1
    public OreSettings Copper; // ID 2
    public OreSettings Tin;    // ID 3
    public OreSettings Coal;   // ID 4
    public OreSettings Stone;  // ID 5
}
public struct ResourceElement : IBufferElementData
{
    public int3 LocalPos; // Позиция внутри чанка (0..15)
    public int ID;        // ID из конфига
    public int Amount;    // Количество
}
public struct ChunkData : IComponentData
{
    public int2 Position;  
}

public struct ChunkMeshState : IComponentData
{
    public int CurrentLOD;   
}
public struct PlayerData : IComponentData
{
    public float2 rawPos;
    public int2 blockedPos;
    public int2 chunkPos;
}
public struct PlayerRayCastData : IComponentData {
    public float3 Origin;
    public float3 Direction;
    public float MaxDistance;
    
    public bool HasHit;
    public int3 HitBlockPos;   // Где блок
    public int3 PlaceBlockPos; // Где воздух перед блоком
    public int HitBlockID;    // Тип блока
}

public struct UpdateChunkDataTag: IComponentData, IEnableableComponent {}
public struct UpdateVisualTag : IComponentData, IEnableableComponent {}
public struct IsVisibleTag : IComponentData, IEnableableComponent {}
public struct ChangeVisibleChunkState : IComponentData,IEnableableComponent{}
public struct ChangeLODChunkState : IComponentData,IEnableableComponent{ public int newLIOD;}
public struct VertexElement : IBufferElementData { public float3 Position;  public float2 UV;  public float3 Normal;    // Для атласа
    public int BlockID;}
public struct IndexElement : IBufferElementData { public int Value; }
public struct CreateChunk: IComponentData
{
    public int2 Position;  
    public bool isVisible;
}
public struct ModifiedBlockElement : IBufferElementData
{
    public int Index;   
    public byte NewID; 
}
public struct BlockElement : IBufferElementData
{
    public byte BlockID; 
}