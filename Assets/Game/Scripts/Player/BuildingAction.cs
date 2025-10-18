using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using Zenject;

public class BuildingAction
{
    [Inject] IReadOnlyBuildingInfo _buildingInfo;
    [Inject] IReadOnlyGameFieldSettings _gameFieldSettings;
    [Inject] PhantomObjectFactory _phantomObjectFactory;
    [Inject] GameController _gameController;

    public event Action OnActionDone;
    public bool IsAction;
    int rotation;
    int point;
    Vector2Int currPos;
    PlaceDelegate placeMethod;
    UpdateDelegate updateMethod;
    PhantomObject phantomObject;
    CanBuildDelegate canBuild;
    int x, y;
    TempPosData posData;
    delegate void PlaceDelegate(bool manyPoint, bool force);
    delegate void UpdateDelegate(Vector3 pos, bool force);
    delegate bool CanBuildDelegate();



    public void SetUp(int buildingID)
    {
        posData = CreateVisualData(buildingID);

        if (posData is TempBuildingPosData buildingData)
        {
            placeMethod = PlaceOnePointBuilding;
            updateMethod = UpdateSimpleBuilding;
            canBuild = CanBuildThere;
            CreatePreview(buildingData);
        }
        else if (posData is TempRoadPosData roadData)
        {
            placeMethod = PlaceTwoPointBuilding;
            updateMethod = UpdateRoadBuilding;
            point = 0;
        }

        Debug.Log($"Building setup completed: {buildingID}");
        IsAction = true;
    }

    TempPosData CreateVisualData(int buildingID)
    {
        var building = _buildingInfo.BuildingInfos[buildingID];
        var uniqueID = Guid.NewGuid().ToString();
        
        if (building.actionType == ActionType.Building)
        {
            return new TempBuildingPosData
            {
                UnicIDHash = uniqueID.GetStableHashCode(),
                IsPhantom = true,
                BuildingIDHash = buildingID,
                LeftCornerPos = Vector2Int.zero,
                Rotation = 0,
                Size = building.size 
            };
        }
        else // Для дорог
        {
            return new TempRoadPosData
            {
                UnicIDHash = uniqueID.GetStableHashCode(),
                IsPhantom = true,
                BuildingIDHash = buildingID,
                FirstPoint = Vector2Int.zero,
                EndPoint = Vector2Int.zero
            };
        }
    }

    void CreatePreview(TempBuildingPosData visualData)
    {
        var buildingPrefab = _buildingInfo.BuildingInfos[visualData.BuildingIDHash].prefab;
        if (buildingPrefab == null)
        {
            Debug.LogError("Building prefab is null!");
            return;
        }
        Debug.Log("фантом");
        var previewObject = UnityEngine.Object.Instantiate(buildingPrefab);

        var buildingOnScene = previewObject.GetComponent<BuildingOnScene>();
        buildingOnScene.transform.localScale = _gameFieldSettings.cellSize * (Vector3)_buildingInfo.BuildingInfos[visualData.BuildingIDHash].size;
        if (buildingOnScene != null)
        {
            phantomObject = _phantomObjectFactory.PhantomizeObject(buildingOnScene.gameObject);
        }
    }

    public void Update(Vector3 pos, bool force)
    {
        updateMethod?.Invoke(pos, force);
    }

    public void UpdateSimpleBuilding(Vector3 pos, bool force)
    {
        var buildingPosData = posData as TempBuildingPosData;
        var bSize = _buildingInfo.BuildingInfos[buildingPosData.BuildingIDHash].size;
        var size = rotation % 2 != 0 ? new Vector2Int(bSize.z, bSize.x) : new Vector2Int(bSize.x, bSize.z);

        int cellX = Mathf.FloorToInt(pos.x / _gameFieldSettings.cellSize);
        int cellZ = Mathf.FloorToInt(pos.z / _gameFieldSettings.cellSize);

        if (size.x % 2 == 0)
            x = cellX - (size.x / 2) + 1;
        else
            x = cellX - (size.x / 2);

        if (size.y % 2 == 0)
            y = cellZ - (size.y / 2) + 1;
        else
            y = cellZ - (size.y / 2);

        currPos = new Vector2Int(x, y);
        buildingPosData.LeftCornerPos = currPos;
        buildingPosData.Rotation = rotation;

        if (phantomObject != null)
        {
            float worldX, worldZ;

            if (size.x % 2 == 0)
                worldX = (currPos.x + size.x / 2f) * _gameFieldSettings.cellSize;
            else
                worldX = (currPos.x + size.x / 2f) * _gameFieldSettings.cellSize;


            if (size.y % 2 == 0)
                worldZ = (currPos.y + size.y / 2f) * _gameFieldSettings.cellSize;
            else
                worldZ = (currPos.y + size.y / 2f) * _gameFieldSettings.cellSize;

            var worldPos = new Vector3(
                worldX,
                bSize.y * _gameFieldSettings.cellSize / 2f,
                worldZ
            );

            phantomObject.transform.position = worldPos;
            phantomObject.transform.rotation = Quaternion.Euler(0, rotation * 90f, 0f);

            bool canBuildHere = canBuild() || force;
            phantomObject.CanBuild(canBuildHere);
        }
    }

    void UpdateRoadBuilding(Vector3 pos, bool force)
    {
        // Логика для дорог (если нужно)
    }

    public void PlaceBuilding(bool manyPoint, bool force) => placeMethod?.Invoke(manyPoint, force);

    void PlaceOnePointBuilding(bool manyPoint, bool force)
    {
        if (!(posData is TempBuildingPosData visual)) return;

        if (!force && !canBuild())
        {
            Debug.Log("Cannot build here! Buildings in the way.");
            return;
        }

        if (force)
        {
            var Entitys = GetBuildingsInPlaceArea();
            foreach (var en in Entitys)
            {
               // en.AddComponent<DestroyComponent>(new DestroyComponent { IsInvokedByPlayer = true });
            }
        }

        if (phantomObject != null)
        {
            UnityEngine.Object.Destroy(phantomObject.gameObject);
            phantomObject = null;
        }
        Debug.Log("Скреатил");
        _gameController.PlaceBuilding(
            new PosData
            {
                UnicIDHash = visual.UnicIDHash,
                IsPhantom = true,
                BuildingIDHash = visual.BuildingIDHash
            },
            new BuildingPosData
            {
                LeftCornerPos = new int2(visual.LeftCornerPos.x, visual.LeftCornerPos.y),
                Size = new int2(visual.Size.x, visual.Size.z),
                Rotation = visual.Rotation,
            });

        if (!manyPoint)
        {
            Clear();
            OnActionDone?.Invoke();
        }
        else
        {
            var currentBuildingID = visual.UnicIDHash;
            ClearData();
            posData = CreateVisualData(currentBuildingID);

            if (posData is TempBuildingPosData newVisual)
            {
                CreatePreview(newVisual);
            }
        }
    }

    void PlaceTwoPointBuilding(bool manyPoint, bool force)
    {
        if (point == 0)
        {
            point = 1;
        }
        else if (point == 1)
        {
            if (!manyPoint)
            {
                Clear();
                OnActionDone?.Invoke();
            }
            else
            {
                point = 0;
                ClearData();
            }
        }
    }

    int[] GetBuildingsInPlaceArea()
    {
        if (!(posData is TempBuildingPosData visual)) return new int[] { -1 };

        var size = _buildingInfo.BuildingInfos[posData.BuildingIDHash].size;
        var adjustedSize = rotation % 2 != 0 ? new Vector2Int(size.y, size.x) : new Vector2Int(size.x, size.y);

        HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();

        for (int x = 0; x < adjustedSize.x; x++)
        {
            for (int y = 0; y < adjustedSize.y; y++)
            {
                occupiedCells.Add(visual.LeftCornerPos + new Vector2Int(x, y));
            }
        }

        // ДОБАВЬТЕ ОТЛАДОЧНУЮ ИНФОРМАЦИЮ
        Debug.Log($"Checking cells for building at {visual.LeftCornerPos}, size: {adjustedSize}");
        Debug.Log($"Occupied cells count: {occupiedCells.Count}");

        var entities = _gameController.GetBuildingInThereMany(occupiedCells.ToArray());
        Debug.Log($"Found entities: {entities.Length}");

        return entities;
    }
    bool CanBuildThere()
    {
        if (!(posData is TempBuildingPosData visual)) return false;

        var size = _buildingInfo.BuildingInfos[posData.BuildingIDHash].size;
        var adjustedSize = rotation % 2 != 0 ? new Vector2Int(size.y, size.x) : new Vector2Int(size.x, size.y);

        HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();

        for (int x = 0; x < adjustedSize.x; x++)
        {
            for (int y = 0; y < adjustedSize.y; y++)
            {
                occupiedCells.Add(visual.LeftCornerPos + new Vector2Int(x, y));
            }
        }

        return _gameController.CanBuildHereMany(occupiedCells.ToArray());
    }
    public void Back()
    {
        if (posData is TempRoadPosData && point == 1)
        {
            point = 0;
        }
        else
        {
            if (phantomObject != null)
            {
                UnityEngine.Object.Destroy(phantomObject.gameObject);
                phantomObject = null;
            }
            Clear();
            OnActionDone?.Invoke();
        }
    }

    public void RotateBuilding()
    {
        rotation = (rotation + 1) % 4;

        if (phantomObject != null && posData is TempBuildingPosData visual)
        {
            var currentWorldPos = new Vector3(
                currPos.x * _gameFieldSettings.cellSize,
                0,
                currPos.y * _gameFieldSettings.cellSize
            );
            UpdateSimpleBuilding(currentWorldPos, false);
        }
    }

    void Clear()
    {
        placeMethod = null;
        updateMethod = null;
        canBuild = null;
        point = 0;
        rotation = 0;
        ClearData();
    }

    void ClearData()
    {
        if (phantomObject != null)
        {
            UnityEngine.Object.Destroy(phantomObject.gameObject);
            phantomObject = null;
        }
        posData = null;

        IsAction = false;
    }
}

public class TempPosData
{
    public int BuildingIDHash;
    public int UnicIDHash;
    public bool IsPhantom;
}
public class TempBuildingPosData: TempPosData
{
    public Vector2Int LeftCornerPos;
    public int Rotation;
    public Vector3Int Size;
}
public class TempRoadPosData : TempPosData
{
    public Vector2Int FirstPoint;
    public Vector2Int EndPoint;
}