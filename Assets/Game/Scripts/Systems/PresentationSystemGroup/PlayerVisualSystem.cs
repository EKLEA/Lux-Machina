
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
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(BuildingLoadSystem))]
public partial class PlayerVisualSystem : SystemBase
{
    [Inject] GameFieldSettings gameFieldSettings;

    protected override void OnUpdate()
    {
        // 1. Поиск сущностей без графики
        var query = SystemAPI.QueryBuilder().WithAll<ChunkData>().WithNone<RenderMeshArray>().Build();
        
        // Получаем массив сущностей ПЕРЕД тем как что-то менять
        if (!query.IsEmpty)
        {
            var entitiesToInit = query.ToEntityArray(Allocator.Temp);
            
            // Теперь итерируемся по обычному массиву. 
            // EntityManager.AddComponents разрешен, так как мы не в Query.ForEach
            foreach (var entity in entitiesToInit)
            {
                Mesh mesh = new Mesh { name = "ChunkMesh" };
                mesh.MarkDynamic();

                var desc = new RenderMeshDescription { FilterSettings = RenderFilterSettings.Default };
                var meshArray = new RenderMeshArray(new[] { gameFieldSettings.chunkMat }, new[] { mesh });
                var meshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0);

                // Структурное изменение разрешено здесь
                RenderMeshUtility.AddComponents(entity, EntityManager, desc, meshArray, meshInfo);

                
                EntityManager.SetComponentData(entity, meshInfo);
                EntityManager.AddComponentData(entity, new RenderBounds { Value = new AABB { Center = float3.zero, Extents = new float3(20, 20, 20) } });
            }
            entitiesToInit.Dispose();
        }

        // 2. Обновление геометрии (здесь структурных изменений нет)
        foreach (var (meshState, vertexBuffer, indices, entity) in SystemAPI.Query<RefRO<ChunkMeshState>, DynamicBuffer<VertexElement>, DynamicBuffer<IndexElement>>()
             .WithAll<UpdateVisualTag>() 
             .WithEntityAccess())
        {
            if (vertexBuffer.IsEmpty) continue;

            var meshArray = EntityManager.GetSharedComponentManaged<RenderMeshArray>(entity);
            Mesh targetMesh = meshArray.MeshReferences[0]; 

            targetMesh.Clear();

            int vCount = vertexBuffer.Length;
            // Создаем массивы под все данные вершины
            var positions = new NativeArray<float3>(vCount, Allocator.Temp);
            var normals = new NativeArray<float3>(vCount, Allocator.Temp); // НОВОЕ
            var uvs0 = new NativeArray<float2>(vCount, Allocator.Temp);
            var uvs1 = new NativeArray<float2>(vCount, Allocator.Temp); 

            for (int i = 0; i < vCount; i++)
            {
                var v = vertexBuffer[i];
                positions[i] = v.Position; // Убедись, что в VertexElement поле называется Position, а не Value
                normals[i] = v.Normal;     // Берем нормаль из Job
                uvs0[i] = v.UV;
                uvs1[i] = new float2(v.BlockID, 0); 
            }

            targetMesh.SetVertices(positions);
            targetMesh.SetNormals(normals); // Передаем наши честные нормали
            targetMesh.SetUVs(0, uvs0);
            targetMesh.SetUVs(1, uvs1);
            targetMesh.SetIndices(indices.Reinterpret<int>().AsNativeArray(), MeshTopology.Triangles, 0);

            // ВАЖНО: RecalculateNormals больше не нужен, так как мы их передали сами.
            // Но тангенты для параллакса пересчитать НУЖНО.
            targetMesh.RecalculateTangents(); 
            targetMesh.RecalculateBounds();

            var b = targetMesh.bounds;
            EntityManager.SetComponentData(entity, new RenderBounds { Value = new AABB { Center = b.center, Extents = b.extents } });

            SystemAPI.SetComponentEnabled<UpdateVisualTag>(entity, false);

            positions.Dispose();
            normals.Dispose();
            uvs0.Dispose();
            uvs1.Dispose();
        }

    }
}
