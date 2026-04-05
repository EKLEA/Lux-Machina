using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Collections;
using Zenject;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AttackZoneVisualizer : MonoBehaviour
{
    public Material attackZoneMaterial; // Шейдер (например, красный полупрозрачный)
    private MeshFilter meshFilter;
    private Mesh mesh;
    

    public void Init()
    {
        meshFilter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        var renderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
        mesh = new Mesh { name = "AttackZoneMesh" };
        meshFilter.mesh = mesh;
        renderer.material = attackZoneMaterial;
    }

    // Передаем сюда TurretGrid из ECS системы
public void DrawAttackZones(TurretGrid turretGrid)
{
    if (mesh == null) Init();

    // 1. Получаем все ключи (с дубликатами)
    var allKeys = turretGrid.TurretGridClaim.GetKeyArray(Allocator.Temp);
    
    if (allKeys.Length == 0) 
    {
        allKeys.Dispose();
        Clear();
        return;
    }

    // 2. Используем HashSet для фильтрации уникальных координат
    // int2 отлично подходит для HashSet, так как имеет GetHashCode
    var uniqueKeys = new NativeHashSet<int2>(allKeys.Length, Allocator.Temp);
    for (int i = 0; i < allKeys.Length; i++)
    {
        uniqueKeys.Add(allKeys[i]);
    }

    List<Vector3> verts = new List<Vector3>();
    List<int> tris = new List<int>();
    List<Color> colors = new List<Color>();
    List<Vector2> uvs = new List<Vector2>();

    float s = turretGrid.CellSize;
    Color zoneColor = new Color(1f, 0f, 0f, 0.3f);

    // 3. Перебираем только уникальные клетки
    foreach (var pos in uniqueKeys)
    {
        AddCell(pos, zoneColor, verts, tris, colors, s, uvs);
    }

    // Очистка
    allKeys.Dispose();
    uniqueKeys.Dispose();

    mesh.Clear();
    mesh.SetVertices(verts);
    mesh.SetTriangles(tris, 0);
    mesh.SetColors(colors);
    mesh.SetUVs(0, uvs);
    
    gameObject.SetActive(verts.Count > 0);
}
    void AddCell(int2 pos, Color color, List<Vector3> verts, List<int> tris, List<Color> colors, float s, List<Vector2> uvs)
    {
        int b = verts.Count;
        float y = 0.05f; // Чуть выше сетки строительства, чтобы не было Z-fight

        // Вершины клетки
        verts.Add(new Vector3(pos.x * s, y, pos.y * s));
        verts.Add(new Vector3((pos.x + 1) * s, y, pos.y * s));
        verts.Add(new Vector3(pos.x * s, y, (pos.y + 1) * s));
        verts.Add(new Vector3((pos.x + 1) * s, y, (pos.y + 1) * s));

        // Индексы треугольников
        tris.AddRange(new int[] { b, b + 2, b + 1, b + 2, b + 3, b + 1 });
        
        for (int i = 0; i < 4; i++)
        {
            colors.Add(color);
        }

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(0, 1));
        uvs.Add(new Vector2(1, 1));
    }

    public void Clear()
    {
        if (mesh != null) mesh.Clear();
        gameObject.SetActive(false);
    }
}
