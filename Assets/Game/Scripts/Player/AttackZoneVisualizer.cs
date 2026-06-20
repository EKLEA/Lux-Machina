using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Collections;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AttackZoneVisualizer : MonoBehaviour
{
    public Material attackZoneMaterial; // Шейдер (например, розовый/красный полупрозрачный)
    private MeshFilter meshFilter;
    private Mesh mesh;

    public void Init()
    {
        meshFilter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        var renderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
        mesh = new Mesh { name = "AttackZoneMesh" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Поддержка больших сеток (>65k вершин)
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

        var uniqueKeys = new NativeHashSet<int3>(allKeys.Length, Allocator.Temp);
        for (int i = 0; i < allKeys.Length; i++)
        {
            uniqueKeys.Add(allKeys[i]);
        }

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Color> colors = new List<Color>();
        List<Vector2> uvs = new List<Vector2>();

        float s = turretGrid.CellSize;
        Color zoneColor = new Color(1f, 0f, 1f, 0.25f); // Розовый полупрозрачный, как на вашем скриншоте

        // 3. Перебираем уникальные 3D-клетки
        foreach (var pos in uniqueKeys)
        {
            Add3DCell(pos, zoneColor, verts, tris, colors, s, uvs);
        }

        // Очистка
        allKeys.Dispose();
        uniqueKeys.Dispose();

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals(); // Важно для корректного отображения 3D шейдеров
        
        gameObject.SetActive(verts.Count > 0);
    }

    void Add3DCell(int3 pos, Color color, List<Vector3> verts, List<int> tris, List<Color> colors, float s, List<Vector2> uvs)
    {
        // Вычисляем мировые координаты минимального угла вокселя/куба
        float minX = pos.x * s;
        float minY = pos.y * s;
        float minZ = pos.z * s; // Исправлено: теперь честный Z вместо pos.y

        float maxX = minX + s;
        float maxY = minY + s;
        float maxZ = minZ + s;

        // Массивы шаблонов для создания 6 граней полноценного 3D-куба
        Vector3[] cubeVertices = new Vector3[]
        {
            // Передняя грань (Z-)
            new Vector3(minX, minY, minZ), new Vector3(maxX, minY, minZ), new Vector3(minX, maxY, minZ), new Vector3(maxX, maxY, minZ),
            // Задняя грань (Z+)
            new Vector3(maxX, minY, maxZ), new Vector3(minX, minY, maxZ), new Vector3(maxX, maxY, maxZ), new Vector3(minX, maxY, maxZ),
            // Левая грань (X-)
            new Vector3(minX, minY, maxZ), new Vector3(minX, minY, minZ), new Vector3(minX, maxY, maxZ), new Vector3(minX, maxY, minZ),
            // Правая грань (X+)
            new Vector3(maxX, minY, minZ), new Vector3(maxX, minY, maxZ), new Vector3(maxX, maxY, minZ), new Vector3(maxX, maxY, maxZ),
            // Верхняя грань (Y+)
            new Vector3(minX, maxY, minZ), new Vector3(maxX, maxY, minZ), new Vector3(minX, maxY, maxZ), new Vector3(maxX, maxY, maxZ),
            // Нижняя грань (Y-)
            new Vector3(minX, minY, maxZ), new Vector3(maxX, minY, maxZ), new Vector3(minX, minY, minZ), new Vector3(maxX, minY, minZ)
        };

        int vStart = verts.Count;
        verts.AddRange(cubeVertices);

        // Индексы треугольников для всех 6 граней (с правильным направлением обхода по часовой стрелке)
        for (int face = 0; face < 6; face++)
        {
            int b = vStart + face * 4;
            tris.AddRange(new int[] { b, b + 2, b + 1, b + 2, b + 3, b + 1 });

            // Заполнение UV и цветов для вершин текущей грани
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector2(0, 1));
            uvs.Add(new Vector2(1, 1));

            for (int i = 0; i < 4; i++)
            {
                colors.Add(color);
            }
        }
    }

    public void Clear()
    {
        if (mesh != null) mesh.Clear();
        gameObject.SetActive(false);
    }
}
