using System.Collections.Generic;
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

    private HashSet<Vector2Int> cellSet;
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
            
            // ЖИРНАЯ ЛИНИЯ: Ставим 10, но убедись, что в самом компоненте Outline на префабе 
            // не стоит ограничение. Mode All заставляет рисовать поверх всего.
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

    public void Generate(Vector2Int[] cells, Dictionary<Vector2Int, bool> neighbors)
    {
        if (_currentCellSize <= 0 || cells == null || cells.Length == 0) return;

        ClearData();
        cellSet = new HashSet<Vector2Int>(cells);
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

            if (generateColliders)
            {
                if (_meshCollider == null) _meshCollider = gameObject.AddComponent<MeshCollider>();
                _meshCollider.sharedMesh = finalMesh;
            }

            if (_outlineMeshFilter != null) _outlineMeshFilter.sharedMesh = finalMesh;
            
            if (outline != null && outline.enabled)
                SetOutLine(outline.OutlineColor);
        }
    }

    private void ClearData()
    {
        straightMatrices.Clear();
        cornerMatrices.Clear();
        tMatrices.Clear();
        crossMatrices.Clear();
        singleMatrices.Clear();
        foundationMatrices.Clear();
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

    void BuildCell(Vector2Int cell, Dictionary<Vector2Int, bool> neighbors, List<CombineInstance> combineList)
    {
        Vector3 pos = new Vector3((cell.x + 0.5f) * _currentCellSize, 0, (cell.y + 0.5f) * _currentCellSize);
        bool up = IsOccupied(cell + Vector2Int.up, neighbors);
        bool down = IsOccupied(cell + Vector2Int.down, neighbors);
        bool left = IsOccupied(cell + Vector2Int.left, neighbors);
        bool right = IsOccupied(cell + Vector2Int.right, neighbors);

        Matrix4x4 worldToLocal = transform.worldToLocalMatrix;

        // 1. ФУНДАМЕНТ
        if (spawnFoundation && foundationMesh != null && !(up && down && left && right))
        {
            Vector3 meshSize = foundationMesh.bounds.size;
            Vector3 fScale = new Vector3(_currentCellSize / meshSize.x, 1f, _currentCellSize / meshSize.z);
            Matrix4x4 fTrs = Matrix4x4.TRS(pos, Quaternion.identity, fScale);
            foundationMatrices.Add(fTrs);

            combineList.Add(new CombineInstance { 
                mesh = foundationMesh, 
                transform = worldToLocal * fTrs 
            });
        }

        // 2. ШАПКА
        if (!spawnTop) return;

        Mesh targetMesh = null;
        List<Matrix4x4> targetList = null;
        Quaternion logicRot = Quaternion.identity;

        int conCount = (up ? 1 : 0) + (right ? 1 : 0) + (down ? 1 : 0) + (left ? 1 : 0);
        if (!useConnections) { targetMesh = singleMesh; targetList = singleMatrices; }
        else 
        {
            switch (conCount) 
            {
                case 4: targetMesh = crossMesh; targetList = crossMatrices; break;
                case 3:
                    targetMesh = tMesh; targetList = tMatrices;
                    if (!up) logicRot = Quaternion.Euler(0, 180, 0);
                    else if (!right) logicRot = Quaternion.Euler(0, 270, 0);
                    else if (!down) logicRot = Quaternion.identity;
                    else if (!left) logicRot = Quaternion.Euler(0, 90, 0);
                    break;
                case 2:
                    if (up && down) { targetMesh = straightMesh; targetList = straightMatrices; }
                    else if (left && right) { targetMesh = straightMesh; targetList = straightMatrices; logicRot = Quaternion.Euler(0, 90, 0); }
                    else {
                        targetMesh = cornerMesh; targetList = cornerMatrices;
                        if (up && right) logicRot = Quaternion.identity;
                        else if (right && down) logicRot = Quaternion.Euler(0, 90, 0);
                        else if (down && left) logicRot = Quaternion.Euler(0, 180, 0);
                        else if (left && up) logicRot = Quaternion.Euler(0, 270, 0);
                    }
                    break;
                case 1:
                    targetMesh = straightMesh; targetList = straightMatrices;
                    if (up) logicRot = Quaternion.identity;
                    else if (right) logicRot = Quaternion.Euler(0, 90, 0);
                    else if (down) logicRot = Quaternion.Euler(0, 180, 0);
                    else if (left) logicRot = Quaternion.Euler(0, 270, 0);
                    break;
                default: targetMesh = singleMesh; targetList = singleMatrices; break;
            }
        }

        if (targetMesh != null && targetList != null)
        {
            float finalY = spawnFoundation ? topMeshYOffset : 0f;
            Vector3 topPos = pos + Vector3.up * finalY;
            Quaternion finalRot = logicRot * Quaternion.Euler(topMeshCorrection);
            Matrix4x4 trs = Matrix4x4.TRS(topPos, finalRot, Vector3.one * _currentCellSize);
            targetList.Add(trs);

            combineList.Add(new CombineInstance { 
                mesh = targetMesh, 
                transform = worldToLocal * trs 
            });
        }
    }

    bool IsOccupied(Vector2Int pos, Dictionary<Vector2Int, bool> neighbors)
    {
        if (cellSet != null && cellSet.Contains(pos)) return true;
        return neighbors != null && neighbors.TryGetValue(pos, out bool val) && val;
    }
}
