using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ChunkZonesTest : MonoBehaviour
{
    public Material atlasMaterial;
    [Header("Terrain Settings")]
    public float waveHeight = 50.0f;  // Высота волн
    public float waveScale = 0.2f;   // Масштаб (частота) волн
    public int subdivisions = 10;    // Плотность сетки внутри каждой зоны

    void Start()
    {
        GenerateZones();
    }

    void GenerateZones()
    {
        Mesh mesh = new Mesh();
        mesh.name = "WavyZonesChunk";

        // Количество вершин на одну зону: (sub + 1) * (sub + 1)
        int vertsPerZone = (subdivisions + 1) * (subdivisions + 1);
        int trisPerZone = subdivisions * subdivisions * 6;

        Vector3[] vertices = new Vector3[vertsPerZone * 4];
        Vector2[] uvs = new Vector2[vertsPerZone * 4];
        int[] triangles = new int[trisPerZone * 4];

        float zoneSize = 5f;
        float tileSize = 0.2f;
        float padding = 0.02f;

        for (int zoneIdx = 0; zoneIdx < 4; zoneIdx++)
        {
            int vOffset = zoneIdx * vertsPerZone;
            int tOffset = zoneIdx * trisPerZone;

            float xStart = (zoneIdx % 2) * zoneSize;
            float zStart = (zoneIdx / 2) * zoneSize;

            // Расчет UV для текущего ID блока
            float uMin = (zoneIdx % 5) * tileSize + padding;
            float vMin = Mathf.Floor(zoneIdx / 5f) * tileSize + padding;
            float uMax = uMin + tileSize - (padding * 2);
            float vMax = vMin + tileSize - (padding * 2);

            // Генерируем сетку вершин внутри зоны
            for (int z = 0; z <= subdivisions; z++)
            {
                for (int x = 0; x <= subdivisions; x++)
                {
                    int i = vOffset + z * (subdivisions + 1) + x;
                    
                    float xPos = xStart + (x / (float)subdivisions) * zoneSize;
                    float zPos = zStart + (z / (float)subdivisions) * zoneSize;

                    // ПЕРЛИН: Создаем волнистость
                    float yPos = Mathf.PerlinNoise(xPos * waveScale, zPos * waveScale) * waveHeight;

                    vertices[i] = new Vector3(xPos, yPos, zPos);

                    // Распределяем UV по сетке
                    float u = Mathf.Lerp(uMin, uMax, x / (float)subdivisions);
                    float v = Mathf.Lerp(vMin, vMax, z / (float)subdivisions);
                    uvs[i] = new Vector2(u, v);
                }
            }

            // Генерируем треугольники для сетки
            int triIdx = tOffset;
            for (int z = 0; z < subdivisions; z++)
            {
                for (int x = 0; x < subdivisions; x++)
                {
                    int root = vOffset + z * (subdivisions + 1) + x;
                    triangles[triIdx++] = root;
                    triangles[triIdx++] = root + subdivisions + 1;
                    triangles[triIdx++] = root + 1;

                    triangles[triIdx++] = root + 1;
                    triangles[triIdx++] = root + subdivisions + 1;
                    triangles[triIdx++] = root + subdivisions + 2;
                }
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshRenderer>().material = atlasMaterial;

        // Обновляем коллайдер
        var mc = GetComponent<MeshCollider>();
        if (mc == null) mc = gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = null;
        mc.sharedMesh = mesh;
    }
}
