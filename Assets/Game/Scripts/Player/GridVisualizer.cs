using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using Zenject;
[RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
public class GridVisualizer : MonoBehaviour
{
    public Material gridMaterial; // Шейдер, поддерживающий Vertex Color
    private MeshFilter meshFilter;
    private Mesh mesh;
    [Inject] GameFieldSettings gameFieldSettings;

    public void Init()
    {
        meshFilter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        var renderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
        mesh = new Mesh { name = "GridVisual" };
        meshFilter.mesh = mesh;
        renderer.material = gridMaterial;
    }

    public void DrawGrid(Vector2Int center, int radius, BuildingMap map)
    {
        if (mesh == null) Init();

        // Центрируем объект
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        var cellSize = gameFieldSettings.cellSize;
        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();
        List<Color> colors = new List<Color>();

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                Vector2Int cellPos = new Vector2Int(center.x + x, center.y + y);
                int2 ecsPos = new int2(cellPos.x, cellPos.y);

                // Если клетка занята — СКИПАЕМ её (не рисуем ничего)
                if (map.CellMapBuildingsIDs.ContainsKey(ecsPos)) continue;

                // Если свободная — рисуем зеленую
                AddCell(cellPos, Color.green, verts, tris, colors, cellSize, uvs);
            }
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        gameObject.SetActive(true);
    }
    void AddCell(Vector2Int pos, Color color, List<Vector3> verts, List<int> tris, List<Color> colors, float s,List<Vector2> uvs)
    {
        int b = verts.Count;
        float y = 0.02f; // Чуть выше для исключения мерцания

        verts.Add(new Vector3(pos.x * s, y, pos.y * s));
        verts.Add(new Vector3((pos.x + 1) * s, y, pos.y * s));
        verts.Add(new Vector3(pos.x * s, y, (pos.y + 1) * s));
        verts.Add(new Vector3((pos.x + 1) * s, y, (pos.y + 1) * s));

        tris.AddRange(new int[] { b, b + 2, b + 1, b + 2, b + 3, b + 1 });
        
        for (int i = 0; i < 4; i++) colors.Add(color);

        // Добавляем UV координаты для каждой вершины клетки
        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(0, 1));
        uvs.Add(new Vector2(1, 1));
        
    }
    public void Clear()
    {
        if (mesh != null)
        {
            mesh.Clear();
        }
        
        gameObject.SetActive(false);
    }
}
