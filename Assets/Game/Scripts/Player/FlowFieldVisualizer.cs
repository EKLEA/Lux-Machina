using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using Zenject;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FlowFieldVisualizer : MonoBehaviour
{
    private Mesh mesh;
    private MeshFilter meshFilter;
    [Inject] GameFieldSettings gameFieldSettings;

    public void Init()
    {
        meshFilter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        var renderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
        
        // Используем стандартный шейдер, поддерживающий Vertex Color
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        
        mesh = new Mesh { name = "FlowLinesMesh3D" };
        meshFilter.mesh = mesh;
    }

public void DrawFlowField(BuildingMap map)
{
    if (mesh == null) Init();

    float s = gameFieldSettings.cellSize;
    float lineLen = s * 0.4f; 
    float tipLen = s * 0.15f; 
    float tipWidth = s * 0.08f; 

    List<Vector3> verts = new List<Vector3>();
    List<int> indices = new List<int>();
    List<Color> colors = new List<Color>();

    // Вместо трехмерного цикла просто перебираем существующие направления!
    // Если нужно ограничить дистанцию, можно проверять расстояние от центра внутри цикла
    foreach (var kvp in map.CellDirections)
    {
        int3 pos = kvp.Key;
        float3 dir = kvp.Value;

        // Пропускаем пустые направления
        if (math.lengthsq(dir) < 0.01f) continue;

        // Берем вес для этой же позиции
        if (!map.CellWeights.TryGetValue(pos, out float weight)) continue;

        // Если все же нужно ограничить радиус от определенного центра:
        // if (math.distance(pos, center) > radius) continue;

        float3 normDir = math.normalize(dir);

        Vector3 start = new Vector3(
            pos.x * s + s * 0.5f, 
            pos.y * s + s * 0.5f, 
            pos.z * s + s * 0.5f
        );
        Vector3 end = start + (Vector3)(normDir * lineLen);

        Color c = Color.Lerp(Color.red, Color.blue, weight / 1000f);

        int baseIndex = verts.Count;

        // Предел вершин для одного Mesh в Unity (65,535 для UInt16). 
        // Если стрелок слишком много, старые версии Unity могут выдать ошибку.
        if (baseIndex + 10 > 65535 && mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt16)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Включаем поддержку больших мешей
        }

        // 1. Основная линия
        verts.Add(start); 
        verts.Add(end);
        indices.Add(baseIndex); 
        indices.Add(baseIndex + 1);

        // 3D-наконечник
        float3 upSpace = math.abs(normDir.y) > 0.9f ? new float3(1, 0, 0) : new float3(0, 1, 0);
        float3 right = math.normalize(math.cross(normDir, upSpace)) * tipWidth;
        float3 up = math.normalize(math.cross(right, normDir)) * tipWidth;

        Vector3 tipBase = end - (Vector3)(normDir * tipLen);

        Vector3 p0 = tipBase + (Vector3)right;
        Vector3 p1 = tipBase + (Vector3)up;
        Vector3 p2 = tipBase - (Vector3)right;
        Vector3 p3 = tipBase - (Vector3)up;

        verts.Add(end); verts.Add(p0); 
        verts.Add(end); verts.Add(p1); 
        verts.Add(end); verts.Add(p2); 
        verts.Add(end); verts.Add(p3); 

        for (int i = 2; i < 10; i++)
        {
            indices.Add(baseIndex + i);
        }

        for (int i = 0; i < 10; i++) colors.Add(c);
    }

    mesh.Clear();
    mesh.SetVertices(verts);
    mesh.SetColors(colors);
    mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
    gameObject.SetActive(true);
}
    public void Clear()
    {
        if (mesh != null) mesh.Clear();
        gameObject.SetActive(false);
    }
}
