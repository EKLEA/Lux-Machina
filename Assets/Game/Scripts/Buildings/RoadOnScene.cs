using System.Collections.Generic;
using System.Linq;
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
    
    
    MeshCollider staticMeshCollider;
    
    
    private List<BoxCollider> debugSegmentColliders = new List<BoxCollider>();

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
            
        
        AddStaticMeshCollider();
        
        
        gameObject.isStatic = true;
    }

    void AddStaticMeshCollider()
    {
        
        var oldCollider = GetComponent<MeshCollider>();
        if (oldCollider != null)
            Destroy(oldCollider);
            
        
        staticMeshCollider = gameObject.AddComponent<MeshCollider>();
        staticMeshCollider.sharedMesh = null; 
        
        
        staticMeshCollider.isTrigger = false;
        
        
        
    }

    void UpdateStaticMeshCollider()
    {
        if (staticMeshCollider != null && mesh != null)
        {
            staticMeshCollider.sharedMesh = mesh;
        }
    }

    void ClearDebugColliders()
    {
        
        foreach (var collider in debugSegmentColliders)
        {
            if (collider != null)
                Destroy(collider);
        }
        debugSegmentColliders.Clear();
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
        
        
        UpdateStaticMeshCollider();
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

        _cellCenters = cellCenters;

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
        
        
        UpdateStaticMeshCollider();
        
        
        if (Application.isEditor)
        {
            CreateDebugColliders();
        }
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

    
    void CreateDebugColliders()
    {
        ClearDebugColliders();
        
        if (_cellCenters == null || _cellCenters.Length < 2)
            return;
            
        
        for (int i = 0; i < _cellCenters.Length - 1; i++)
        {
            Vector2Int startCell = _cellCenters[i];
            Vector2Int endCell = _cellCenters[i + 1];
            
            Vector3 startWorld = new Vector3(
                startCell.x * _currentCellSize,
                0.05f,
                startCell.y * _currentCellSize
            );

            Vector3 endWorld = new Vector3(
                endCell.x * _currentCellSize,
                0.05f,
                endCell.y * _currentCellSize
            );
            
            
            var debugCollider = gameObject.AddComponent<BoxCollider>();
            debugCollider.isTrigger = true;
            debugCollider.enabled = false; 
            
            
            Vector3 segmentCenter = (startWorld + endWorld) * 0.5f;
            Vector3 direction = endWorld - startWorld;
            float segmentLength = direction.magnitude;
            
            if (segmentLength > 0)
            {
                direction.Normalize();
                debugCollider.center = segmentCenter - transform.position;
                debugCollider.size = new Vector3(roadWidth, 0.1f, segmentLength);
                
                
                Quaternion rotation = Quaternion.LookRotation(direction);
                var debugColliderObj = new GameObject("DebugCollider_" + i);
                debugColliderObj.transform.SetParent(transform);
                debugColliderObj.transform.position = segmentCenter;
                debugColliderObj.transform.rotation = rotation;
                var tempCollider = debugColliderObj.AddComponent<BoxCollider>();
                tempCollider.size = new Vector3(roadWidth, 0.1f, segmentLength);
                tempCollider.isTrigger = true;
                
                debugSegmentColliders.Add(tempCollider);
                
                
                Destroy(debugCollider);
            }
        }
    }

    public void ClearMesh()
    {
        if (mesh != null)
        {
            mesh.Clear();
        }
        
        if (staticMeshCollider != null)
        {
            staticMeshCollider.sharedMesh = null;
        }
        
        ClearDebugColliders();
    }

    
    public bool IsPointOnRoad(Vector3 worldPoint)
    {
        if (_cellCenters == null || _cellCenters.Length == 0)
            return false;

        
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        
        
        foreach (var cell in _cellCenters)
        {
            Vector3 cellCenterLocal = new Vector3(
                cell.x * _currentCellSize,
                0.05f,
                cell.y * _currentCellSize
            );

            float halfWidth = roadWidth * 0.5f;
            float halfLength = roadWidth * 0.5f;
            
            if (Mathf.Abs(localPoint.x - cellCenterLocal.x) < halfWidth &&
                Mathf.Abs(localPoint.z - cellCenterLocal.z) < halfLength)
            {
                return true;
            }
        }
        
        return false;
    }

    void OnDestroy()
    {
        
        ClearMesh();
    }
    
    
}