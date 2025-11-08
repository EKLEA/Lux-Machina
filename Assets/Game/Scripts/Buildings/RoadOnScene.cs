using System.Collections.Generic;
using UnityEngine;

public class RoadOnScene : BuildingOnScene
{
    [Header("Road Settings")]
    public float roadWidth = 1f;
    public float uvScale = 1f;
    public Material roadMaterial;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh mesh;
    float _currentCellSize;
    public Renderer roadRenderer;
    public Vector2Int[] _cellCenters;

    public override void SetCluster(int newClusterID, Color clusterColor)
    {
        base.SetCluster(newClusterID, clusterColor);

        if (roadRenderer != null)
        {
            var newMaterial = new Material(roadRenderer.material);
            newMaterial.color = Color.Lerp(newMaterial.color, clusterColor, 0.3f);
            roadRenderer.material = newMaterial;
        }
    }

    public void CreateRoadClusterIndicator()
    {
        CreateClusterIndicator(1f);
    }

    public void Init(float cellSize)
    {
        _currentCellSize = cellSize;

        roadWidth = cellSize;

        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

        mesh = new Mesh();
        meshFilter.mesh = mesh;

        if (roadMaterial != null)
            meshRenderer.material = roadMaterial;
    }

    public void GenerateRoadMesh(Vector2Int[] cellCenters)
    {
        if (cellCenters == null || cellCenters.Length == 0)
            return;
        _cellCenters = cellCenters;

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uvs = new List<Vector2>();

        foreach (var cellCenter in cellCenters)
        {
            AddRoadQuadForCell(cellCenter, vertices, triangles, uvs);
        }

        if (vertices.Count == 0)
            return;

        Vector3 centroid = Vector3.zero;
        for (int i = 0; i < vertices.Count; i++)
            centroid += vertices[i];
        centroid /= vertices.Count;

        for (int i = 0; i < vertices.Count; i++)
            vertices[i] -= centroid;

        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        transform.position = centroid;
        transform.rotation = Quaternion.identity;
    }

    void AddRoadQuadForCell(
        Vector2Int cellCenter,
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector2> uvs
    )
    {
        float yPos = 0.05f;
        float halfRoadWidth = roadWidth * 0.5f;

        Vector3 worldCenter = new Vector3(
            cellCenter.x * _currentCellSize + _currentCellSize * 0.5f,
            yPos,
            cellCenter.y * _currentCellSize + _currentCellSize * 0.5f
        );

        int baseIndex = vertices.Count;

        Vector3 bottomLeft = new Vector3(
            worldCenter.x - halfRoadWidth,
            yPos,
            worldCenter.z - halfRoadWidth
        );
        Vector3 bottomRight = new Vector3(
            worldCenter.x + halfRoadWidth,
            yPos,
            worldCenter.z - halfRoadWidth
        );
        Vector3 topLeft = new Vector3(
            worldCenter.x - halfRoadWidth,
            yPos,
            worldCenter.z + halfRoadWidth
        );
        Vector3 topRight = new Vector3(
            worldCenter.x + halfRoadWidth,
            yPos,
            worldCenter.z + halfRoadWidth
        );

        vertices.Add(bottomLeft);
        vertices.Add(bottomRight);
        vertices.Add(topLeft);
        vertices.Add(topRight);

        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 1);

        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 3);
        triangles.Add(baseIndex + 1);

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(0, 1));
        uvs.Add(new Vector2(1, 1));
    }

    public void GenerateSmoothRoadMesh(Vector2Int[] cellCenters)
    {
        if (cellCenters == null || cellCenters.Length < 2)
            return;

        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uvs = new List<Vector2>();

        for (int i = 0; i < cellCenters.Length - 1; i++)
        {
            AddRoadSegment(cellCenters[i], cellCenters[i + 1], vertices, triangles, uvs, i);
        }

        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void AddRoadSegment(
        Vector2Int startCell,
        Vector2Int endCell,
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector2> uvs,
        int segmentIndex
    )
    {
        float yPos = 0.05f;

        Vector3 startWorld = new Vector3(
            startCell.x * _currentCellSize,
            yPos,
            startCell.y * _currentCellSize
        );

        Vector3 endWorld = new Vector3(
            endCell.x * _currentCellSize,
            yPos,
            endCell.y * _currentCellSize
        );

        Vector3 direction = (endWorld - startWorld).normalized;
        Vector3 perpendicular = new Vector3(-direction.z, 0, direction.x) * roadWidth * 0.5f;

        int baseIndex = vertices.Count;

        Vector3 leftStart = startWorld - perpendicular;
        Vector3 rightStart = startWorld + perpendicular;
        Vector3 leftEnd = endWorld - perpendicular;
        Vector3 rightEnd = endWorld + perpendicular;

        vertices.Add(leftStart);
        vertices.Add(rightStart);
        vertices.Add(leftEnd);
        vertices.Add(rightEnd);

        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 2);

        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 3);

        float segmentLength = Vector3.Distance(startWorld, endWorld);
        float startU = segmentIndex * uvScale;
        float endU = startU + segmentLength * uvScale;

        uvs.Add(new Vector2(startU, 0));
        uvs.Add(new Vector2(startU, 1));
        uvs.Add(new Vector2(endU, 0));
        uvs.Add(new Vector2(endU, 1));
    }

    public void ClearMesh()
    {
        if (mesh != null)
        {
            mesh.Clear();
        }
    }

    public void DrawDebugBounds(Vector2Int[] cellCenters)
    {
        Vector3 objectWorldPos = transform.position;

        Debug.Log($"DrawDebugBounds: Object at {objectWorldPos}");

        foreach (var cell in cellCenters)
        {
            Vector3 worldCenter = new Vector3(
                cell.x * _currentCellSize,
                0.2f,
                cell.y * _currentCellSize
            );

            Vector3 cellBottomLeft = new Vector3(
                cell.x * _currentCellSize - _currentCellSize * 0.5f,
                0,
                cell.y * _currentCellSize - _currentCellSize * 0.5f
            );

            Vector3 cellBottomRight = new Vector3(
                cell.x * _currentCellSize + _currentCellSize * 0.5f,
                0,
                cell.y * _currentCellSize - _currentCellSize * 0.5f
            );

            Vector3 cellTopLeft = new Vector3(
                cell.x * _currentCellSize - _currentCellSize * 0.5f,
                0,
                cell.y * _currentCellSize + _currentCellSize * 0.5f
            );

            Vector3 cellTopRight = new Vector3(
                cell.x * _currentCellSize + _currentCellSize * 0.5f,
                0,
                cell.y * _currentCellSize + _currentCellSize * 0.5f
            );

            float halfRoadWidth = roadWidth * 0.5f;
            Vector3 roadBottomLeft = new Vector3(
                worldCenter.x - halfRoadWidth,
                0.1f,
                worldCenter.z - halfRoadWidth
            );

            Vector3 roadBottomRight = new Vector3(
                worldCenter.x + halfRoadWidth,
                0.1f,
                worldCenter.z - halfRoadWidth
            );

            Vector3 roadTopLeft = new Vector3(
                worldCenter.x - halfRoadWidth,
                0.1f,
                worldCenter.z + halfRoadWidth
            );

            Vector3 roadTopRight = new Vector3(
                worldCenter.x + halfRoadWidth,
                0.1f,
                worldCenter.z + halfRoadWidth
            );

            Debug.DrawLine(cellBottomLeft, cellBottomRight, Color.green, 2f);
            Debug.DrawLine(cellBottomRight, cellTopRight, Color.green, 2f);
            Debug.DrawLine(cellTopRight, cellTopLeft, Color.green, 2f);
            Debug.DrawLine(cellTopLeft, cellBottomLeft, Color.green, 2f);

            Debug.DrawLine(roadBottomLeft, roadBottomRight, Color.blue, 2f);
            Debug.DrawLine(roadBottomRight, roadTopRight, Color.blue, 2f);
            Debug.DrawLine(roadTopRight, roadTopLeft, Color.blue, 2f);
            Debug.DrawLine(roadTopLeft, roadBottomLeft, Color.blue, 2f);

            Debug.DrawLine(
                worldCenter - Vector3.forward * 0.3f,
                worldCenter + Vector3.forward * 0.3f,
                Color.red,
                2f
            );
            Debug.DrawLine(
                worldCenter - Vector3.right * 0.3f,
                worldCenter + Vector3.right * 0.3f,
                Color.red,
                2f
            );
        }
    }
}
