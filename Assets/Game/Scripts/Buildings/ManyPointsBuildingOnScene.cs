using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class ManyPointsBuildingInstanced : BuildingOnScene
{
    [Header("Mode")]
    public bool useConnections = true;
    public bool spawnTop = true;
    public bool spawnFoundation = true;
    public bool generateColliders = false;

    [Header("Import Corrections")]
    public Vector3 topMeshCorrection = new Vector3(-90, 0, 0);

    [Header("Top Meshes")]
    public Mesh straightMesh;
    public Mesh cornerMesh;
    public Mesh tMesh;
    public Mesh crossMesh;
    public Mesh singleMesh;
    public Mesh slopeMesh;

    [Header("Foundation")]
    public Mesh foundationMesh;

    [Header("Material")]
    public Material material;

    [Header("Offsets")]
    public float topMeshYOffset = 0.1f;

    private float _currentCellSize = -1;
    private const int batchSize = 1023;

    private List<Matrix4x4> straightMatrices = new();
    private List<Matrix4x4> cornerMatrices = new();
    private List<Matrix4x4> tMatrices = new();
    private List<Matrix4x4> crossMatrices = new();
    private List<Matrix4x4> singleMatrices = new();
    private List<Matrix4x4> foundationMatrices = new();
    private List<Matrix4x4> slopeMatrices = new();
    private HashSet<Vector3Int> cellSet;
    private MaterialPropertyBlock _instancedBlock;
    private MeshCollider _meshCollider;
    private MeshFilter _outlineMeshFilter;
    private MeshRenderer _outlineRenderer;
    private GameObject _outlineObj;

    void Awake()
    {
        _instancedBlock = new MaterialPropertyBlock();
        _meshCollider = GetComponent<MeshCollider>();

        if (_outlineObj == null)
        {
            _outlineObj = new GameObject("Outline_Visual");
            _outlineObj.transform.SetParent(transform);
            _outlineObj.transform.localPosition = Vector3.zero;
            _outlineObj.transform.localRotation = Quaternion.identity;
            _outlineObj.transform.localScale = Vector3.one;

            _outlineMeshFilter = _outlineObj.AddComponent<MeshFilter>();
            _outlineRenderer = _outlineObj.AddComponent<MeshRenderer>();
            
            _outlineRenderer.sharedMaterial = material;
            _outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _outlineRenderer.enabled = false; 
        }

        outline = _outlineObj.GetComponent<Outline>() ?? _outlineObj.AddComponent<Outline>();
        outline.enabled = false;
    }

    public override void SetOutLine(Color? color)
    {
        if (outline == null) return;

        if (color.HasValue)
        {
            _outlineRenderer.enabled = true;
            outline.OutlineColor = color.Value;
            
            
            
            outline.OutlineWidth = 10f; 
            outline.OutlineMode = Outline.Mode.OutlineAll;

            outline.enabled = true;
            if (!outline.SetUpded) outline.SetUp();
            outline.SendMessage("OnEnable", SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            outline.enabled = false;
            if (_outlineRenderer != null) _outlineRenderer.enabled = false;
        }
    }

    public void Init(float currentCellSize)
    {
        _currentCellSize = currentCellSize;
        SetDefaultOpaqueMode();
    }

    public void SetDefaultOpaqueMode()
    {
        UpdatePhantomParams(false, Color.white, Color.white, 1f);
    }

    public void UpdatePhantomParams(bool isPhantom, Color mainColor, Color lineColor, float progress)
    {
        if (_instancedBlock == null) _instancedBlock = new MaterialPropertyBlock();
        _instancedBlock.SetFloat("_IsPhantom", isPhantom ? 1f : 0f);
        _instancedBlock.SetColor("_PhantomColor", mainColor);
        _instancedBlock.SetColor("_LineColor", lineColor);
        _instancedBlock.SetFloat("_PhantomProcent", progress);
    }

     public void Generate(Vector3Int[] cells, Dictionary<Vector3Int, bool> neighbors)
    {
        if (_currentCellSize <= 0 || cells == null || cells.Length == 0) return;

        ClearData();
        cellSet = new HashSet<Vector3Int>(cells);
        List<CombineInstance> combineList = new List<CombineInstance>();

        foreach (var cell in cells)
        {
            BuildCell(cell, neighbors, combineList);
        }

         if (combineList.Count > 0)
        {
            Mesh finalMesh = new Mesh();
            finalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            finalMesh.CombineMeshes(combineList.ToArray(), true, true);
            
            // Оптимизация меша для коллайдера
            finalMesh.RecalculateBounds();

            if (generateColliders)
            {
                if (_meshCollider == null) _meshCollider = gameObject.AddComponent<MeshCollider>();
                
                // Сначала обнуляем, потом назначаем — это заставляет Unity пересчитать физику
                _meshCollider.sharedMesh = null;
                _meshCollider.sharedMesh = finalMesh;
                
                // Если коллайдер должен быть выпуклым (для Rigidbody), раскомментируй:
                // _meshCollider.convex = true;
            }
            
            if (_outlineMeshFilter != null) _outlineMeshFilter.sharedMesh = finalMesh;
        }
    }
    bool TryGetSlope(Vector3Int cell, Dictionary<Vector3Int, bool> neighbors, out int3 dir)
    {
        // проверяем 4 направления вниз
        Vector3Int[] slopeDirs =
        {
            new Vector3Int(1,-1,0),
            new Vector3Int(-1,-1,0),
            new Vector3Int(0,-1,1),
            new Vector3Int(0,-1,-1),
        };

        foreach (var d in slopeDirs)
        {
            if (IsOccupied(cell + (Vector3Int)d, neighbors))
            {
                dir = new int3(d.x,d.y,d.z);
                return true;
            }
        }

        dir = int3.zero;
        return false;
}
    private void ClearData()
    {
        straightMatrices.Clear();
        cornerMatrices.Clear();
        tMatrices.Clear();
        crossMatrices.Clear();
        singleMatrices.Clear();
        foundationMatrices.Clear();
        slopeMatrices.Clear();
        if (_meshCollider != null) _meshCollider.sharedMesh = null;
        if (_outlineMeshFilter != null) _outlineMeshFilter.sharedMesh = null;
    }

    void Update() => RenderAll();

    void RenderAll()
    {
        int layer = gameObject.layer;
        RenderBatch(straightMatrices, straightMesh, layer);
        RenderBatch(cornerMatrices, cornerMesh, layer);
        RenderBatch(tMatrices, tMesh, layer);
        RenderBatch(crossMatrices, crossMesh, layer);
        RenderBatch(singleMatrices, singleMesh, layer);
        RenderBatch(foundationMatrices, foundationMesh, layer);
        RenderBatch(slopeMatrices, slopeMesh, layer);
    }

    void RenderBatch(List<Matrix4x4> matrices, Mesh mesh, int layer)
    {
        if (mesh == null || matrices.Count == 0 || material == null) return;
        int count = matrices.Count;
        for (int i = 0; i < count; i += batchSize)
        {
            int length = Mathf.Min(batchSize, count - i);
            var batch = matrices.GetRange(i, length).ToArray();
            Graphics.DrawMeshInstanced(mesh, 0, material, batch, length, _instancedBlock, 
                UnityEngine.Rendering.ShadowCastingMode.On, true, layer);
        }
    }

private void BuildCell(Vector3Int cell, Dictionary<Vector3Int, bool> neighbors, List<CombineInstance> combineList)
{
    Vector3 pos = new Vector3(
        (cell.x + 0.5f) * _currentCellSize, 
        (cell.y * _currentCellSize), 
        (cell.z + 0.5f) * _currentCellSize
    );

    // 1. ПРАВКА СЛОПОВ: Поворот на 180 градусов
    if (TryGetSlope(cell, neighbors, out int3 slopeDir))
    {
        // Внимание: если AddSlope принимает Quaternion, добавь поворот там.
        // Если нет — залезь в AddSlope и в конце вычисления finalRot добавь:
        // finalRot *= Quaternion.Euler(0, 180, 0);
        AddSlope(cell, slopeDir, combineList); 
        return; 
    }
    
    bool up = IsOccupied(cell + new Vector3Int(0, 0, 1), neighbors);
    bool down = IsOccupied(cell + new Vector3Int(0, 0, -1), neighbors);
    bool left = IsOccupied(cell + new Vector3Int(-1, 0, 0), neighbors);
    bool right = IsOccupied(cell + new Vector3Int(1, 0, 0), neighbors);
    
    bool hasBelow = IsOccupied(cell + Vector3Int.down, neighbors);
    Matrix4x4 worldToLocal = transform.worldToLocalMatrix;

    // 2. ФУНДАМЕНТ: Оставляем Quaternion.identity (НЕ ПОВОРАЧИВАЕМ)
    if (spawnFoundation && foundationMesh != null && !hasBelow)
    {
        Vector3 meshSize = foundationMesh.bounds.size;
        Vector3 fScale = new Vector3(_currentCellSize / meshSize.x, _currentCellSize, _currentCellSize / meshSize.z);
        Matrix4x4 fTrs = Matrix4x4.TRS(pos, Quaternion.identity, fScale);
        foundationMatrices.Add(fTrs);
        combineList.Add(new CombineInstance { mesh = foundationMesh, transform = worldToLocal * fTrs });
    }

    if (!spawnTop) return;

    Vector3 topPos = pos + Vector3.up * topMeshYOffset;
    Mesh targetMesh = null;
    List<Matrix4x4> targetList = null;
    Quaternion logicRot = Quaternion.identity;

    int conCount = (up ? 1 : 0) + (right ? 1 : 0) + (down ? 1 : 0) + (left ? 1 : 0);

    if (!useConnections) { targetMesh = singleMesh; targetList = singleMatrices; }
    else 
    {
        if (conCount == 4) { targetMesh = crossMesh; targetList = crossMatrices; }

        else if (conCount == 3) 
        {
            targetMesh = tMesh; targetList = tMatrices;
            if (!up) logicRot = Quaternion.Euler(0, 90, 0);
            else if (!right) logicRot = Quaternion.Euler(0, 180, 0);
            else if (!down) logicRot = Quaternion.Euler(0, 270, 0);
            logicRot*= Quaternion.Euler(0,180,0);
        }
        else if (conCount == 2) 
        {
            if (up && down) { targetMesh = straightMesh; targetList = straightMatrices; }
            else if (left && right) { targetMesh = straightMesh; targetList = straightMatrices; logicRot = Quaternion.Euler(0, 90, 0); }
            else {
                targetMesh = cornerMesh; targetList = cornerMatrices;
                if (up && right) logicRot = Quaternion.Euler(0, 90, 0);
                else if (right && down) logicRot = Quaternion.Euler(0, 180, 0);
                else if (down && left) logicRot = Quaternion.Euler(0, 270, 0);
            }
        }
        else if (conCount == 1) {
            targetMesh = straightMesh; targetList = straightMatrices;
            if (left || right) logicRot = Quaternion.Euler(0, 90, 0);
        }
        else { targetMesh = singleMesh; targetList = singleMatrices; }
    }

    if (targetMesh != null)
{
    Quaternion finalRot = logicRot * Quaternion.Euler(topMeshCorrection);
    
    // Если это угол, доворачиваем на 90 градусов по оси Z
    if (targetMesh == cornerMesh) 
    {
        finalRot *= Quaternion.Euler(0, 0, 90); 
    }

    Matrix4x4 trs = Matrix4x4.TRS(topPos, finalRot, Vector3.one * _currentCellSize);
    targetList.Add(trs);
    combineList.Add(new CombineInstance { mesh = targetMesh, transform = worldToLocal * trs });
}
}


void AddSlope(Vector3Int cell, int3 dir, List<CombineInstance> combineList)
{
    // 1. Логика замены на singleMesh, если слопа нет
    if (slopeMesh == null) 
    {
        if (singleMesh != null)
        {
            Vector3 singlePos = new Vector3(
                (cell.x + 0.5f) * _currentCellSize,
                (cell.y * _currentCellSize) + topMeshYOffset,
                (cell.z + 0.5f) * _currentCellSize
            );
            Quaternion singleRot = Quaternion.Euler(topMeshCorrection);
            Matrix4x4 singleTrs = Matrix4x4.TRS(singlePos, singleRot, Vector3.one * _currentCellSize);
            
            if (singleMatrices == null) singleMatrices = new List<Matrix4x4>();
            singleMatrices.Add(singleTrs);
            combineList.Add(new CombineInstance { mesh = singleMesh, transform = transform.worldToLocalMatrix * singleTrs });
        }
        return;
    }

    // 2. Ставим слоп-меш
    Vector3 pos = new Vector3(
        (cell.x + 0.5f) * _currentCellSize,
        (cell.y * _currentCellSize) + topMeshYOffset, 
        (cell.z + 0.5f) * _currentCellSize
    );

    Quaternion rot = Quaternion.identity;
    if (dir.x == 1) rot = Quaternion.Euler(0, 90, 0);
    else if (dir.x == -1) rot = Quaternion.Euler(0, -90, 0);
    else if (dir.z == 1) rot = Quaternion.Euler(0, 0, 0);
    else if (dir.z == -1) rot = Quaternion.Euler(0, 180, 0);

    // ПОВОРАЧИВАЕМ ПО X:
    // Сначала база, потом коррекция (-90 по X), и финальный переворот (180 по X), чтобы легло на дорогу
    Quaternion finalRot = rot * Quaternion.Euler(topMeshCorrection) * Quaternion.Euler(90, 0, 0);

    Matrix4x4 trs = Matrix4x4.TRS(pos, finalRot, Vector3.one * _currentCellSize);
    slopeMatrices.Add(trs);

    combineList.Add(new CombineInstance
    {
        mesh = slopeMesh,
        transform = transform.worldToLocalMatrix * trs
    });
}
    private bool IsOccupied(Vector3Int pos, Dictionary<Vector3Int, bool> neighbors)
    {
        if (cellSet != null && cellSet.Contains(pos)) return true;
        return neighbors != null && neighbors.TryGetValue(pos, out bool val) && val;
    }
}
