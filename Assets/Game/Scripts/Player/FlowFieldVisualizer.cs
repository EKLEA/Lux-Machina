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
        
        mesh = new Mesh { name = "FlowLinesMesh" };
        meshFilter.mesh = mesh;
    }

    public void DrawFlowField(Vector2Int center, int radius, BuildingMap map)
    {
        if (mesh == null) Init();

        float s = gameFieldSettings.cellSize;
        float lineLen = s * 0.4f; // Длина основной линии
        float tipLen = s * 0.15f; // Длина "усиков" стрелки

        List<Vector3> verts = new List<Vector3>();
        List<int> indices = new List<int>();
        List<Color> colors = new List<Color>();

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                int2 pos = new int2(center.x + x, center.y + y);

                if (!map.CellDirections.TryGetValue(pos, out float2 dir)) continue;
                if (math.lengthsq(dir) < 0.01f) continue;

                map.CellWeights.TryGetValue(pos, out float weight);

                // Центр клетки со смещением по Y, чтобы не проваливалось в пол
                Vector3 start = new Vector3(pos.x * s + s/2, 0.2f, pos.y * s + s/2);
                Vector3 direction = new Vector3(dir.x, 0, dir.y);
                Vector3 end = start + direction * lineLen;

                // Цвет: у цели (вес 0) - Циан, далеко - тускло-синий
                Color c = Color.Lerp(Color.cyan, new Color(0, 0.2f, 0.5f, 0.5f), weight / 1000f);
                if (map.CellMapBuildingsIDs.ContainsKey(pos)) c = Color.red; // Поток в стене

                int b = verts.Count;

                // 1. Основная линия
                verts.Add(start); verts.Add(end);
                indices.Add(b); indices.Add(b + 1);

                // 2. Левый усик стрелки (поворот на 150 градусов)
                float2 leftDir = math.mul(float2x2.Rotate(math.radians(150)), dir) * tipLen;
                verts.Add(end);
                verts.Add(end + new Vector3(leftDir.x, 0, leftDir.y));
                indices.Add(b + 2); indices.Add(b + 3);

                // 3. Правый усик стрелки (поворот на -150 градусов)
                float2 rightDir = math.mul(float2x2.Rotate(math.radians(-150)), dir) * tipLen;
                verts.Add(end);
                verts.Add(end + new Vector3(rightDir.x, 0, rightDir.y));
                indices.Add(b + 4); indices.Add(b + 5);

                for (int i = 0; i < 6; i++) colors.Add(c);
            }
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        // Рисуем именно линиями
        mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
        gameObject.SetActive(true);
    }

    public void Clear()
    {
        if (mesh != null) mesh.Clear();
        gameObject.SetActive(false);
    }
}
