// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Threading.Tasks;
// using UnityEngine;
// using Zenject;

// public class VirtualLogisticsCenterService:ReadOnlyVirtualLogistucsCCenterService
// {
//     GameStateData _gameStateData;
//     Dictionary<string, VirtualLogisticsCenter> _logisticsCenters;
//     Dictionary<Vector2Int, HashSet<Road>> _roadGraph;
//     Dictionary<string, Road> _allRoads;
//     Dictionary<string, HashSet<Guid>> _buildingToCentersMap;
//     Dictionary<string, Guid> _buildingMasterCenter;

//     RoadFactory _roadFactory;
//     ReadOnlyBuildingsVisualService _visualService;
//     ReadOnlyBuildingsLogicService _logicService;

//     [Inject] IReadOnlyBuildingInfo buildingInfo;

//     public VirtualLogisticsCenterService(GameStateData data,
//                                        ReadOnlyBuildingsVisualService visualService,
//                                        ReadOnlyBuildingsLogicService logicService)
//     {
//         _gameStateData = data;
//         _visualService = visualService;
//         _logicService = logicService;

//         _logisticsCenters = new();
//         _roadFactory = new();
//         _roadGraph = new();
//         _allRoads = new();
//         _buildingToCentersMap = new();
//         _buildingMasterCenter = new();
//     }

//     public async Task LoadCenters()
//     {
//         if (_gameStateData.virtualLogisticsCentersData.Count > 0)
//         {
//             var tasks = _gameStateData.virtualLogisticsCentersData.Values
//                 .Select(virtualLogisticsCentersData => CreateCenter(virtualLogisticsCentersData))
//                 .ToArray();

//             await Task.WhenAll(tasks);
//         }
//     }

//     async Task CreateCenter(VirtualLogisticsCenterData data)
//     {
//         var vc = new VirtualLogisticsCenter(data);
//         _logisticsCenters.Add(vc.UnicID, vc);
//         await Task.Yield();
//     }

//     public async Task CreateRoad(RoadData data)
//     {
//         var road = _roadFactory.Create(data);
//         _allRoads.Add(road.UnicID, road);
//         AddRoadToGraph(road);

//         var connectedCenters = FindConnectedCenters(road);
//         var targetCenter = connectedCenters.Count > 0 ? await MergeAllCenters(connectedCenters) : await CreateNewCenter();

//         _gameStateData.virtualLogisticsCentersData[targetCenter.UnicID].roads.Add(road.UnicID, data);
//         await ConnectBuildingsAlongRoad(road, targetCenter);
//     }

//     public async void RemoveRoadCells(Vector2Int startPoint, Vector2Int endPoint)
//     {
//         var cellsToRemove = GetRoadOccupedCells(startPoint, endPoint);
//         var affectedRoads = cellsToRemove
//             .Where(cell => _roadGraph.ContainsKey(cell))
//             .SelectMany(cell => _roadGraph[cell])
//             .ToHashSet();

//         foreach (var road in affectedRoads)
//             RemoveRoadFromGraph(road);

//         var clusters = FindRoadClusters(affectedRoads);

//         foreach (var road in affectedRoads)
//             AddRoadToGraph(road);

//         if (clusters.Count > 1)
//             await SplitVLC(clusters, affectedRoads);

//         foreach (var road in affectedRoads)
//             UpdateBuildingConnectionsForRoad(road);
//     }

//     public bool IsMasterCenterForBuilding(Guid centerId, string buildingId)
//     {
//         return _buildingMasterCenter.ContainsKey(buildingId) &&
//                _buildingMasterCenter[buildingId] == centerId;
//     }

//     public void RegisterNewBuilding(string buildingUnicID)
//     {
//         if (!_visualService.Buildings.ContainsKey(buildingUnicID))
//         {
//             Debug.LogWarning($"Building with UnicID {buildingUnicID} not found in visual service");
//             return;
//         }


//         if (_buildingToCentersMap.ContainsKey(buildingUnicID))
//         {
//             Debug.Log($"Building {buildingUnicID} is already registered with centers");
//             return;
//         }


//         var accessibleCenters = FindAccessibleCentersForBuilding(buildingUnicID);

//         if (accessibleCenters.Count > 0)
//         {

//             var targetCenter = accessibleCenters.First();
//             ConnectBuildingToCenter(buildingUnicID, targetCenter);
//             Debug.Log($"Building {buildingUnicID} connected to center {targetCenter.UnicID}");
//         }
//         else
//         {
//             Debug.Log($"Building {buildingUnicID} has no connected roads, skipping registration");
//         }
//     }
//     public void UnregisterBuilding(string buildingUnicID)
//     {
//         if (_buildingToCentersMap.ContainsKey(buildingUnicID))
//         {
//             var connectedCenters = _buildingToCentersMap[buildingUnicID].ToList();
//             foreach (var centerId in connectedCenters)
//             {
//                 var center = _logisticsCenters.Values.FirstOrDefault(c => c.CenterID == centerId);
//                 if (center != null)
//                 {
//                     DisconnectBuildingFromCenter(buildingUnicID, center);
//                 }
//             }

//             _buildingToCentersMap.Remove(buildingUnicID);
//             _buildingMasterCenter.Remove(buildingUnicID);
//             Debug.Log($"Building {buildingUnicID} completely unregistered from all centers");
//         }
//     }
//     void AddRoadToGraph(Road road)
//     {
//         foreach (var cell in road.GetRoadOccupedCells())
//         {
//             if (!_roadGraph.ContainsKey(cell))
//                 _roadGraph[cell] = new HashSet<Road>();
//             _roadGraph[cell].Add(road);
//         }
//     }

//     void RemoveRoadFromGraph(Road road)
//     {
//         foreach (var cell in road.GetRoadOccupedCells())
//         {
//             if (_roadGraph.ContainsKey(cell))
//             {
//                 _roadGraph[cell].Remove(road);
//                 if (_roadGraph[cell].Count == 0)
//                     _roadGraph.Remove(cell);
//             }
//         }
//     }

//     List<VirtualLogisticsCenter> FindConnectedCenters(Road road)
//     {
//         return road.GetRoadOccupedCells()
//             .Where(cell => _roadGraph.ContainsKey(cell))
//             .SelectMany(cell => _roadGraph[cell])
//             .Select(FindCenterForRoad)
//             .Where(center => center != null)
//             .Distinct()
//             .ToList();
//     }

//     VirtualLogisticsCenter FindCenterForRoad(Road road)
//     {
//         var centerData = _gameStateData.virtualLogisticsCentersData.Values
//             .FirstOrDefault(data => data.roads.ContainsKey(road.UnicID));
//         return centerData != null ? _logisticsCenters[centerData.UnicID] : null;
//     }

//     List<HashSet<Road>> FindRoadClusters(HashSet<Road> roads)
//     {
//         var visited = new HashSet<Road>();
//         var clusters = new List<HashSet<Road>>();

//         foreach (var road in roads.Where(road => !visited.Contains(road)))
//         {
//             var cluster = new HashSet<Road>();
//             DFSRoad(road, visited, cluster, roads);
//             clusters.Add(cluster);
//         }

//         return clusters;
//     }

//     void DFSRoad(Road current, HashSet<Road> visited, HashSet<Road> cluster, HashSet<Road> availableRoads)
//     {
//         visited.Add(current);
//         cluster.Add(current);

//         foreach (var neighbor in current.GetRoadOccupedCells()
//                      .Where(cell => _roadGraph.ContainsKey(cell))
//                      .SelectMany(cell => _roadGraph[cell])
//                      .Where(neighbor => availableRoads.Contains(neighbor) && !visited.Contains(neighbor)))
//         {
//             DFSRoad(neighbor, visited, cluster, availableRoads);
//         }
//     }

//     async Task SplitVLC(List<HashSet<Road>> clusters, HashSet<Road> affectedRoads)
//     {
//         var originalCenter = FindCenterForRoad(affectedRoads.First());
//         if (originalCenter == null) return;

//         var newCenters = new List<VirtualLogisticsCenter>();
//         UpdateCenterRoads(originalCenter, clusters[0]);

//         for (int i = 1; i < clusters.Count; i++)
//         {
//             var newCenter = await CreateNewCenter();
//             UpdateCenterRoads(newCenter, clusters[i]);
//             newCenters.Add(newCenter);
//         }

//         RedistributeBuildings(originalCenter, newCenters);
//     }

//     async Task<VirtualLogisticsCenter> CreateNewCenter()
//     {
//         var newData = new VirtualLogisticsCenterData { UnicID = Guid.NewGuid().ToString() };
//         _gameStateData.virtualLogisticsCentersData.Add(newData.UnicID, newData);
//         var newCenter = new VirtualLogisticsCenter(newData);
//         _logisticsCenters.Add(newCenter.UnicID, newCenter);
//         await Task.Yield();
//         return newCenter;
//     }

//     async Task<VirtualLogisticsCenter> MergeAllCenters(List<VirtualLogisticsCenter> centers)
//     {
//         var targetCenter = centers.First();
//         foreach (var center in centers.Skip(1))
//         {
//             await MergeCenters(targetCenter, center);
//         }
//         return targetCenter;
//     }

//     async Task MergeCenters(VirtualLogisticsCenter target, VirtualLogisticsCenter source)
//     {
//         await target.MergeData(source.GetDataForMerge());

//         foreach (var roadData in _gameStateData.virtualLogisticsCentersData[source.UnicID].roads)
//             _gameStateData.virtualLogisticsCentersData[target.UnicID].roads.Add(roadData.Key, roadData.Value);

//         _logisticsCenters.Remove(source.UnicID);
//         _gameStateData.virtualLogisticsCentersData.Remove(source.UnicID);
//     }

//     void UpdateCenterRoads(VirtualLogisticsCenter center, HashSet<Road> roads)
//     {
//         var centerData = _gameStateData.virtualLogisticsCentersData[center.UnicID];
//         centerData.roads.Clear();

//         foreach (var road in roads)
//         {
//             var roadData = FindRoadData(road);
//             if (roadData != null)
//                 centerData.roads.Add(road.UnicID, roadData);
//         }
//     }

//     RoadData FindRoadData(Road road)
//     {
//         return _gameStateData.virtualLogisticsCentersData.Values
//             .FirstOrDefault(data => data.roads.ContainsKey(road.UnicID))
//             ?.roads[road.UnicID];
//     }

//     void ConnectBuildingToCenter(string buildingId, VirtualLogisticsCenter center)
//     {
//         if (!_buildingToCentersMap.ContainsKey(buildingId))
//             _buildingToCentersMap[buildingId] = new HashSet<Guid>();

//         _buildingToCentersMap[buildingId].Add(center.CenterID);
//         UpdateBuildingMasterCenter(buildingId);
//         center.RegisterBuilding(buildingId);
//     }

//     void DisconnectBuildingFromCenter(string buildingId, VirtualLogisticsCenter center)
//     {
//         if (_buildingToCentersMap.ContainsKey(buildingId))
//         {
//             _buildingToCentersMap[buildingId].Remove(center.CenterID);
//             if (_buildingToCentersMap[buildingId].Count == 0)
//                 _buildingToCentersMap.Remove(buildingId);
//         }

//         UpdateBuildingMasterCenter(buildingId);
//         center.UnregisterBuilding(buildingId);
//     }

//     void UpdateBuildingMasterCenter(string buildingId)
//     {
//         if (!_buildingToCentersMap.ContainsKey(buildingId) || _buildingToCentersMap[buildingId].Count == 0)
//         {
//             _buildingMasterCenter.Remove(buildingId);
//             return;
//         }

//         var newMaster = _buildingToCentersMap[buildingId].OrderBy(id => id).First();
//         _buildingMasterCenter[buildingId] = newMaster;
//     }

//     void RedistributeBuildings(VirtualLogisticsCenter originalCenter, List<VirtualLogisticsCenter> newCenters)
//     {
//         var allCenters = new List<VirtualLogisticsCenter> { originalCenter };
//         allCenters.AddRange(newCenters);

//         foreach (var buildingId in originalCenter.GetAllBuildingIds())
//         {
//             var accessibleCenters = allCenters.Where(center => IsBuildingConnectedToCenter(buildingId, center)).ToList();

//             if (accessibleCenters.Count == 0)
//             {
//                 originalCenter.UnregisterBuilding(buildingId);
//                 _buildingToCentersMap.Remove(buildingId);
//                 _buildingMasterCenter.Remove(buildingId);
//             }
//             else
//             {
//                 var currentMaster = accessibleCenters.OrderBy(c => c.CenterID).First();
//                 _buildingMasterCenter[buildingId] = currentMaster.CenterID;
//             }
//         }
//     }

//     bool IsBuildingConnectedToCenter(string buildingId, VirtualLogisticsCenter center)
//     {
//         return _gameStateData.virtualLogisticsCentersData[center.UnicID].roads.Values
//             .Select(roadData => _allRoads[roadData.UnicID])
//             .SelectMany(road => road.GetRoadOccupedCells())
//             .Any(cell => _visualService.GetUnicIDsAroundPoint(cell).Contains(buildingId));
//     }

//     void UpdateBuildingConnectionsForRoad(Road road)
//     {
//         var affectedBuildings = road.GetRoadOccupedCells()
//             .SelectMany(cell => _visualService.GetUnicIDsAroundPoint(cell))
//             .ToHashSet();

//         foreach (var buildingId in affectedBuildings)
//         {
//             var accessibleCenters = FindAccessibleCentersForBuilding(buildingId);

//             if (accessibleCenters.Count > 0)
//             {
//                 var newMaster = accessibleCenters.OrderBy(c => c.CenterID).First();
//                 _buildingMasterCenter[buildingId] = newMaster.CenterID;

//                 foreach (var center in _logisticsCenters.Values)
//                 {
//                     if (accessibleCenters.Contains(center))
//                         ConnectBuildingToCenter(buildingId, center);
//                     else
//                         DisconnectBuildingFromCenter(buildingId, center);
//                 }
//             }
//             else
//             {
//                 foreach (var center in _logisticsCenters.Values)
//                     DisconnectBuildingFromCenter(buildingId, center);
//             }
//         }
//     }

//     List<VirtualLogisticsCenter> FindAccessibleCentersForBuilding(string buildingId)
//     {
//         if (!_visualService.Buildings.ContainsKey(buildingId))
//             return new List<VirtualLogisticsCenter>();

//         var buildingPosition = _visualService.Buildings[buildingId].leftCornerPos;
//         var checkCells = GetCellsAroundBuilding(buildingPosition, buildingId);

//         return checkCells
//             .Where(cell => _roadGraph.ContainsKey(cell))
//             .SelectMany(cell => _roadGraph[cell])
//             .Select(FindCenterForRoad)
//             .Where(center => center != null)
//             .Distinct()
//             .ToList();
//     }

//     async Task ConnectBuildingsAlongRoad(Road road, VirtualLogisticsCenter center)
//     {
//         var connectedBuildings = road.GetRoadOccupedCells()
//             .SelectMany(cell => _visualService.Buildings.Values
//                 .Where(building => GetCellsAroundBuilding(building.leftCornerPos, building.UnicID).Contains(cell))
//                 .Select(building => building.UnicID))
//             .Where(buildingId => _logicService.Buildings.ContainsKey(buildingId))
//             .ToHashSet();

//         foreach (var buildingId in connectedBuildings)
//             ConnectBuildingToCenter(buildingId, center);

//         await Task.Yield();
//     }

//     List<Vector2Int> GetCellsAroundBuilding(Vector2Int position, string buildingId)
//     {
//         var cells = new List<Vector2Int>();

//         if (!_visualService.Buildings.ContainsKey(buildingId))
//             return cells;

//         var buildingData = _visualService.Buildings[buildingId];
//         var size = GetBuildingSize(buildingData.buildingID, buildingData.rotation);

//         for (int x = 0; x < size.x; x++)
//         {
//             for (int y = 0; y < size.y; y++)
//             {
//                 cells.Add(new Vector2Int(position.x + x, position.y + y));
//             }
//         }

//         AddPerimeterCells(cells, position, size);
//         return cells;
//     }

//     Vector2Int GetBuildingSize(string buildingID, int rotation)
//     {
//         var baseSize = new Vector2Int(buildingInfo.BuildingInfos[buildingID].size.x, buildingInfo.BuildingInfos[buildingID].size.z);
//         return (rotation % 180 == 90) ? new Vector2Int(baseSize.y, baseSize.x) : baseSize;
//     }

//     void AddPerimeterCells(List<Vector2Int> cells, Vector2Int position, Vector2Int size)
//     {
//         for (int x = -1; x <= size.x; x++)
//         {
//             for (int y = -1; y <= size.y; y++)
//             {
//                 var cell = new Vector2Int(position.x + x, position.y + y);
//                 if (!cells.Contains(cell))
//                     cells.Add(cell);
//             }
//         }
//     }

//     Vector2Int[] GetRoadOccupedCells(Vector2Int startPoint, Vector2Int endPoint)
//     {
//         var points = new List<Vector2Int>();
//         int dx = Math.Abs(endPoint.x - startPoint.x);
//         int dy = Math.Abs(endPoint.y - startPoint.y);
//         int steps = Math.Max(dx, dy);

//         for (int i = 1; i < steps; i++)
//         {
//             float t = (float)i / steps;
//             int x = (int)Math.Round(startPoint.x + (endPoint.x - startPoint.x) * t);
//             int y = (int)Math.Round(startPoint.y + (endPoint.y - startPoint.y) * t);
//             points.Add(new Vector2Int(x, y));
//         }

//         return points.ToArray();
//     }
// }
// public interface ReadOnlyVirtualLogistucsCCenterService
// {
//     public  Task CreateRoad(RoadData data);

//     public  void RemoveRoadCells(Vector2Int startPoint, Vector2Int endPoint);

//     public bool IsMasterCenterForBuilding(Guid centerId, string buildingId);

//     public void RegisterNewBuilding(string buildingUnicID);
//     public void UnregisterBuilding(string buildingUnicID);
// }