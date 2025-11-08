using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

public class BuildingAction
{
    [Inject]
    IReadOnlyBuildingInfo _buildingInfo;

    [Inject]
    IReadOnlyGameFieldSettings _gameFieldSettings;

    [Inject]
    BuildingObjectFactorty _buildingObjectFactorty;

    [Inject]
    PhantomObjectFactory _phantomObjectFactory;

    [Inject]
    GameController _gameController;

    public event Action OnActionDone;
    public bool IsAction;

    int rotation;
    Vector2Int currPos;
    Vector2Int cachedPos;
    int cachedRot;
    Vector2Int lastRoadPoint;
    bool roadStarted;

    TempPosData posData;
    PlaceDelegate placeMethod;
    UpdateDelegate updateMethod;
    GetPoints getPoints;

    PhantomObject phantomObject;
    BuildingOnScene buildingOnScene;
    List<Vector2Int> visualPoints = new();

    delegate void PlaceDelegate(bool manyPoint, bool force);
    delegate void UpdateDelegate(Vector3 pos, bool force);
    delegate Vector2Int[] GetPoints();

    public void SetUp(int buildingID)
    {
        posData = CreateVisualData(buildingID);

        if (posData is TempBuildingPosData)
        {
            placeMethod = PlaceOnePointBuilding;
            updateMethod = UpdateSimpleBuilding;
            getPoints = GetBuildingPoints;
        }
        else if (posData is TempRoadPosData)
        {
            Debug.Log("ДАДАДАДАДА");
            placeMethod = PlaceRoadBuilding;
            updateMethod = UpdateRoadBuilding;
            getPoints = GetRoadPoints;
            roadStarted = false;
        }

        CreatePreview();
        IsAction = true;
    }

    public void DrawDebugGridAroundPlayer(Vector3 playerPosition, int radius = 10)
    {
        int centerX = Mathf.FloorToInt(playerPosition.x / _gameFieldSettings.cellSize);
        int centerZ = Mathf.FloorToInt(playerPosition.z / _gameFieldSettings.cellSize);

        for (int x = centerX - radius; x <= centerX + radius; x++)
        {
            for (int z = centerZ - radius; z <= centerZ + radius; z++)
            {
                Vector2Int cell = new Vector2Int(x, z);
                Vector3 center = GetCellWorldCenter(cell);

                bool canBuildHere = _gameController.CanBuildHereMany(
                    new List<Vector2Int> { cell },
                    posData is TempRoadPosData
                );

                Color color = canBuildHere ? Color.green : Color.red;

                Debug.DrawLine(
                    center - Vector3.forward * 0.2f,
                    center + Vector3.forward * 0.2f,
                    color,
                    0.1f
                );
                Debug.DrawLine(
                    center - Vector3.right * 0.2f,
                    center + Vector3.right * 0.2f,
                    color,
                    0.1f
                );
            }
        }
    }

    Vector3 GetCellWorldCenter(Vector2Int cell)
    {
        float half = _gameFieldSettings.cellSize * 0.5f;
        return new Vector3(
            cell.x * _gameFieldSettings.cellSize + half,
            0.05f,
            cell.y * _gameFieldSettings.cellSize + half
        );
    }

    Vector3 GetCellsCentroid(IEnumerable<Vector2Int> cells)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (var c in cells)
        {
            sum += GetCellWorldCenter(c);
            count++;
        }
        return count > 0 ? sum / count : Vector3.zero;
    }

    TempPosData CreateVisualData(int buildingID)
    {
        var building = _buildingInfo.BuildingInfos[buildingID];
        if (building.actionType == ActionType.Building)
        {
            return new TempBuildingPosData
            {
                IsPhantom = true,
                BuildingIDHash = buildingID,
                LeftCornerPos = currPos,
                Rotation = 0,
                Size = building.size,
            };
        }
        else
        {
            Debug.Log("dsdssddsdssddssd");
            return new TempRoadPosData
            {
                IsPhantom = true,
                BuildingIDHash = buildingID,
                Points = new List<Vector2Int>(),
            };
        }
    }

    void CreatePreview()
    {
        if (buildingOnScene != null)
        {
            UnityEngine.Object.Destroy(buildingOnScene.gameObject);
            buildingOnScene = null;
        }

        if (posData is TempBuildingPosData bPos)
            buildingOnScene = _buildingObjectFactorty.CreateBuilding(
                bPos.BuildingIDHash,
                bPos.LeftCornerPos,
                rotation
            );
        else if (posData is TempRoadPosData rPos)
            buildingOnScene = _buildingObjectFactorty.CreateRoad(
                rPos.BuildingIDHash,
                rPos.Points.ToArray()
            );

        if (buildingOnScene != null)
        {
            phantomObject = buildingOnScene.GetComponent<PhantomObject>();
            if (phantomObject == null)
                phantomObject = _phantomObjectFactory.PhantomizeObject(buildingOnScene.gameObject);

            phantomObject.transform.position = GetCellWorldCenter(currPos);
        }
    }

    public void Update(Vector3 pos, bool force)
    {
        if (posData == null)
            return;
        currPos = new Vector2Int(
            Mathf.FloorToInt(pos.x / _gameFieldSettings.cellSize),
            Mathf.FloorToInt(pos.z / _gameFieldSettings.cellSize)
        );

        updateMethod?.Invoke(pos, force);
        cachedPos = currPos;
        cachedRot = rotation;
    }

    void UpdateSimpleBuilding(Vector3 pos, bool force)
    {
        var buildingPosData = posData as TempBuildingPosData;
        var size =
            rotation % 2 != 0
                ? new Vector2Int(buildingPosData.Size.z, buildingPosData.Size.x)
                : new Vector2Int(buildingPosData.Size.x, buildingPosData.Size.z);

        int x = size.x % 2 == 0 ? currPos.x - size.x / 2 + 1 : currPos.x - size.x / 2;
        int y = size.y % 2 == 0 ? currPos.y - size.y / 2 + 1 : currPos.y - size.y / 2;

        buildingPosData.LeftCornerPos = new Vector2Int(x, y);
        buildingPosData.Rotation = rotation;

        if (phantomObject != null)
        {
            phantomObject.transform.position = new Vector3(
                (x + size.x * 0.5f) * _gameFieldSettings.cellSize,
                buildingPosData.Size.y * _gameFieldSettings.cellSize * 0.5f,
                (y + size.y * 0.5f) * _gameFieldSettings.cellSize
            );
            phantomObject.transform.rotation = Quaternion.Euler(0, rotation * 90f, 0f);
            phantomObject.CanBuild(CanBuild(false) || force);
        }
    }

    void UpdateRoadBuilding(Vector3 pos, bool force)
    {
        var roadData = posData as TempRoadPosData;

        currPos = new Vector2Int(
            Mathf.FloorToInt(pos.x / _gameFieldSettings.cellSize),
            Mathf.FloorToInt(pos.z / _gameFieldSettings.cellSize)
        );

        if (!roadStarted)
        {
            visualPoints.Clear();
            visualPoints.Add(currPos);
            UpdatePhantomVisual(visualPoints);
            return;
        }

        if (currPos == lastRoadPoint)
            return;

        // Сохраняем локальный снимок точек для коллбэка
        var lastPoint = lastRoadPoint;
        _gameController.RequestPath(
            lastPoint,
            currPos,
            pathList =>
            {
                if (pathList == null || pathList.Count == 0)
                    pathList = new List<Vector2Int> { lastPoint, currPos };

                visualPoints.Clear();
                visualPoints.Add(lastPoint);
                visualPoints.AddRange(pathList.Skip(1));

                UpdatePhantomVisual(visualPoints);

                // Локальная проверка CanBuild для этого сегмента
                bool canBuildSegment = _gameController.CanBuildHereMany(visualPoints, true);
                phantomObject?.CanBuild(canBuildSegment || force);
            }
        );
    }

    void UpdatePhantomVisual(List<Vector2Int> points)
    {
        if (phantomObject != null)
            phantomObject.transform.position = GetCellsCentroid(points);
        else
            return;
        if (buildingOnScene is RoadOnScene roadOnScene)
            roadOnScene.GenerateRoadMesh(points.ToArray());
    }

    Vector2Int[] GetBuildingPoints()
    {
        if (!(cachedPos == currPos && rotation == cachedRot))
        {
            var data = posData as TempBuildingPosData;
            var size =
                rotation % 2 != 0
                    ? new Vector2Int(data.Size.z, data.Size.x)
                    : new Vector2Int(data.Size.x, data.Size.z);

            visualPoints.Clear();
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                visualPoints.Add(data.LeftCornerPos + new Vector2Int(x, y));
        }
        return visualPoints.ToArray();
    }

    Vector2Int[] GetRoadPoints() => (posData as TempRoadPosData).Points.ToArray();

    public void PlaceBuilding(bool manyPoint, bool force) => placeMethod?.Invoke(manyPoint, force);

    void PlaceOnePointBuilding(bool manyPoint, bool force)
    {
        var data = posData as TempBuildingPosData;
        if (!force && !CanBuild(false))
            return;

        if (phantomObject != null)
            UnityEngine.Object.Destroy(phantomObject.gameObject);

        _gameController.PlaceBuilding(
            new BuildingData
            {
                UniqueIDHash = Guid.NewGuid().ToString().GetStableHashCode(),
                BuildingIDHash = data.BuildingIDHash,
            },
            new BuildingPosData
            {
                LeftCornerPos = new int2(data.LeftCornerPos.x, data.LeftCornerPos.y),
                Size = new int2(data.Size.x, data.Size.z),
                Rotation = data.Rotation,
            },
            data.IsPhantom
        );

        if (!manyPoint)
        {
            ClearData();
            OnActionDone?.Invoke();
        }
        else
        {
            posData = CreateVisualData(data.BuildingIDHash);
            CreatePreview();
        }
    }

    void PlaceRoadBuilding(bool manyPoint, bool force)
    {
        var roadData = posData as TempRoadPosData;

        if (!roadStarted)
        {
            lastRoadPoint = currPos;
            roadData.Points.Clear();
            roadData.Points.Add(lastRoadPoint);
            visualPoints.Clear();
            visualPoints.Add(lastRoadPoint);
            roadStarted = true;
            return;
        }

        // Копируем визуальные точки в данные дороги
        roadData.Points = new List<Vector2Int>(visualPoints);
        Debug.Log("бУилдинг нажал");
        _gameController.PlaceRoad(roadData.Points.ToHashSet(), roadData.IsPhantom);

        if (!manyPoint)
        {
            ClearData();
            OnActionDone?.Invoke();
        }
        else
        {
            lastRoadPoint = roadData.Points.Last();

            // Создаем новый временный объект дороги для следующего сегмента
            var newRoadData = CreateVisualData(roadData.BuildingIDHash) as TempRoadPosData;
            newRoadData.Points.Add(lastRoadPoint);
            posData = newRoadData;
            roadStarted = true;
            CreatePreview();
        }
    }

    bool CanBuild(bool isRoad)
    {
        var pts = getPoints();
        if (pts == null)
        {
            Debug.LogError("[BuildingAction] getPoints() вернул null");
            return false;
        }

        return _gameController.CanBuildHereMany(pts.ToList(), isRoad);
    }

    public void RotateBuilding()
    {
        rotation = (rotation + 1) % 4;
        if (phantomObject != null && posData is TempBuildingPosData data)
        {
            data.Rotation = rotation;
            phantomObject.transform.rotation = Quaternion.Euler(0, rotation * 90f, 0f);
        }
    }

    public void Back()
    {
        var roadData = posData as TempRoadPosData;

        if (roadData != null)
        {
            if (!roadStarted)
            {
                roadData.Points.Clear();
                lastRoadPoint = Vector2Int.zero;
                if (phantomObject != null)
                {
                    UnityEngine.Object.Destroy(phantomObject.gameObject);
                    phantomObject = null;
                }
                ClearData();
                OnActionDone?.Invoke();
            }
            else
            {
                roadData.Points.Clear();
                roadData.Points.Add(currPos);
                lastRoadPoint = currPos;
                roadStarted = false;
                UpdatePhantomVisual(roadData.Points);
            }
        }
        else
        {
            if (phantomObject != null)
            {
                UnityEngine.Object.Destroy(phantomObject.gameObject);
                phantomObject = null;
            }
            ClearData();
            OnActionDone?.Invoke();
        }
    }

    void ClearData()
    {
        placeMethod = null;
        updateMethod = null;
        getPoints = null;
        cachedPos = Vector2Int.zero;
        cachedRot = 0;
        rotation = 0;
        roadStarted = false;
        lastRoadPoint = Vector2Int.zero;
        visualPoints.Clear();

        if (phantomObject != null)
            UnityEngine.Object.Destroy(phantomObject.gameObject);
        if (buildingOnScene != null)
            UnityEngine.Object.Destroy(buildingOnScene.gameObject);

        posData = null;
        IsAction = false;
    }

    class TempPosData
    {
        public int BuildingIDHash;
        public bool IsPhantom;
    }

    class TempBuildingPosData : TempPosData
    {
        public Vector2Int LeftCornerPos;
        public int Rotation;
        public Vector3Int Size;
    }

    class TempRoadPosData : TempPosData
    {
        public List<Vector2Int> Points;
    }
}
