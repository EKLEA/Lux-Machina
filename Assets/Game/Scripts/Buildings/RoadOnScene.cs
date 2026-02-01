using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoadOnScene : BuildingOnScene
{
    [Header("Road Settings")]
    public float roadHeight = 0.5f;
    public Material roadMaterial;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;
    private MeshCollider staticMeshCollider;
    private float _currentCellSize;
    private HashSet<Vector2Int> _cellSet;

    public void Init(float cellSize)
    {
        _currentCellSize = cellSize;
        meshFilter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
        staticMeshCollider = GetComponent<MeshCollider>() ?? gameObject.AddComponent<MeshCollider>();

        mesh = new Mesh { name = "RoadMesh" };
        meshFilter.sharedMesh = mesh;

        if (roadMaterial) meshRenderer.material = roadMaterial;
    }

    public void GenerateRoadMesh(Vector2Int[] cellCenters, Dictionary<Vector2Int, bool> neighborsMap)
    {
        if (cellCenters == null || cellCenters.Length == 0) return;
        _cellSet = new HashSet<Vector2Int>(cellCenters);
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uvs = new List<Vector2>();
        var vertexCache = new Dictionary<Vector3, int>();

        foreach (var cell in cellCenters)
        {
            AddOptimizedRoadBlock(cell, neighborsMap, vertices, triangles, uvs, vertexCache);
        }

        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        staticMeshCollider.sharedMesh = mesh;
        SetupOutline();
    }

    private void SetupOutline()
    {
        Outline outline = GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = false;
        }
    }

    void AddOptimizedRoadBlock(Vector2Int cell, Dictionary<Vector2Int, bool> neighbors, 
        List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, Dictionary<Vector3, int> cache)
    {
        float h = roadHeight;
        float s = _currentCellSize;
        float xMin = cell.x * s; float xMax = (cell.x + 1) * s;
        float zMin = cell.y * s; float zMax = (cell.y + 1) * s;

        int GetVertex(Vector3 pos)
        {
            if (cache.TryGetValue(pos, out int index)) return index;
            int newIndex = vertices.Count;
            vertices.Add(pos);
            uvs.Add(new Vector2(pos.x / s, pos.z / s)); 
            cache[pos] = newIndex;
            return newIndex;
        }

        void AddFace(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            int i0 = GetVertex(p0); int i1 = GetVertex(p1);
            int i2 = GetVertex(p2); int i3 = GetVertex(p3);
            
            triangles.Add(i0); triangles.Add(i2); triangles.Add(i1);
            triangles.Add(i2); triangles.Add(i3); triangles.Add(i1);
        }

        Vector3 v0 = new Vector3(xMin, h, zMin); Vector3 v1 = new Vector3(xMax, h, zMin);
        Vector3 v2 = new Vector3(xMin, h, zMax); Vector3 v3 = new Vector3(xMax, h, zMax);
        Vector3 v4 = new Vector3(xMin, 0, zMin); Vector3 v5 = new Vector3(xMax, 0, zMin);
        Vector3 v6 = new Vector3(xMin, 0, zMax); Vector3 v7 = new Vector3(xMax, 0, zMax);

        // Верх
        AddFace(v0, v1, v2, v3);

        if (!IsOccupied(cell + Vector2Int.down, neighbors))  AddFace(v4, v5, v0, v1); 
        if (!IsOccupied(cell + Vector2Int.up, neighbors))    AddFace(v7, v6, v3, v2); 
        if (!IsOccupied(cell + Vector2Int.left, neighbors))  AddFace(v6, v4, v2, v0); 
        if (!IsOccupied(cell + Vector2Int.right, neighbors)) AddFace(v5, v7, v1, v3); 
    }

    private bool IsOccupied(Vector2Int pos, Dictionary<Vector2Int, bool> neighbors)
    {
        if (_cellSet.Contains(pos)) return true;
        return neighbors != null && neighbors.TryGetValue(pos, out bool isRoad) && isRoad;
    }
}
