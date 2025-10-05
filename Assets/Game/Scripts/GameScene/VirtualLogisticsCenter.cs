using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ModestTree;
using Mono.Cecil;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using Zenject;

public class VirtualLogisticsCenter
{
    public string UnicID => _data.UnicID;
    Dictionary<string, IResourceConsumer> consumers;
    Dictionary<string, IResourceProducer> producers;
    Dictionary<string, SortedSet<ItemRequest>> requests;
    Dictionary<string, int> items
    {
        get => _data.items;
        set => _data.items = value;
    }
    [Inject] ReadOnlyBuildingsLogicService logicService;
    [Inject] ReadOnlyBuildingsVisualService visualService;
    public ReadOnlyDictionary<string, Road> Roads => new(roads);
    Dictionary<string, Road> roads;
    VirtualLogisticsCenterData _data;
    RoadFactory _roadFactory;
    public VirtualLogisticsCenter(
        VirtualLogisticsCenterData data,
         RoadFactory roadFactory)
    {
        _data = data;
        _roadFactory = roadFactory;
        roads = new();
        consumers = new();
        producers = new();
        requests = new();
    }
    public VirtualLogisticsCenterDataForMerge GetDataForMerge()
    {
        var data= new VirtualLogisticsCenterDataForMerge
        {
            consumers = this.consumers,
            producers = this.producers,
            requests = this.requests,
            items = this.items,
            roads = this.roads,
            roadsData=_data.roads
        };
        foreach (var c in consumers)
            c.Value.OnRequestRegistered -= AddRequest;
        return data;
    }
    public async Task MergeData(VirtualLogisticsCenterDataForMerge data)
    {
        consumers.AddRange(data.consumers);
        foreach (var c in data.consumers)
            c.Value.OnRequestRegistered += AddRequest;
        producers.AddRange(data.producers);
        requests.AddRange(data.requests);
        items.AddRange(data.items);
        roads.AddRange(data.roads);
        _data.roads.AddRange(data.roadsData);
        await Task.Yield();
    }
    public async Task LoadRoads()
    {
        foreach (var r in _data.roads)
        {
            await CreateRoad(r.Value);
        }
    }
    public async Task CreateRoad(RoadData roadData)
    {
        var c = _roadFactory.Create(roadData);
        foreach (var v in c.GetRoadOccupedCells())
        {
            foreach (var id in visualService.GetUnicIDsAroundPoint(v))
            {
                if (logicService.Buildings.ContainsKey(id))
                {
                    RegisterConsumer(id);
                    if (logicService.Buildings[id] is IResourceProducer) RegisterProducer(id);
                }
            }
        }
        roads.Add(c.UnicID, c);
        await Task.Yield();

    }
    public void DeleteRoad(string unicID,bool DestroyOBJ)
    {
        if (roads.ContainsKey(unicID))
        {
            var r = roads[unicID];
            foreach (var v in r.GetRoadOccupedCells())
            {
                foreach (var id in visualService.GetUnicIDsAroundPoint(v))
                {
                    if (logicService.Buildings.ContainsKey(id))
                    {
                        UnregisterConsumer(id);
                        if (logicService.Buildings[id] is IResourceProducer) UnregisterProducer(id);
                    }
                }
            }
            _data.roads.Remove(unicID);
            roads.Remove(unicID);
            if(DestroyOBJ)GameObject.Destroy(r.RoadOnScene);
       }
    }
    public void CheckRoads()
    {
        foreach(var r in roads){}
    }
    public void ModifyRoad(string unicID, int countOfCells, bool fromEnd)
    {
        if (roads.ContainsKey(unicID))
        {
            var r = roads[unicID];
            if (fromEnd)
                r.endPoint += r.endPoint.x == r.startPoint.x ? Vector2Int.up * countOfCells : Vector2Int.right * countOfCells;
            else
                r.startPoint += r.endPoint.x == r.startPoint.x ? Vector2Int.up * countOfCells : Vector2Int.right * countOfCells;
            if (Vector2Int.Distance(r.startPoint, r.endPoint) != 0)
                _roadFactory.Modify(r);
            else DeleteRoad(unicID, true);
            CheckAllBuildings();
        }
    }
    public void SplitRoad(string unicID, Vector2Int endOfFirstSegment, Vector2Int startOfSecondSegment)
    {
        if (roads.ContainsKey(unicID))
        {
            var r = roads[unicID];
            var newdata = new RoadData { UnicID = Guid.NewGuid().ToString(), startPoint = startOfSecondSegment, endPoint = r.endPoint };
            var c = _roadFactory.Create(newdata);
            roads.Add(c.UnicID, c);
            c.RoadOnScene.gameObject.SetActive(false);
            ModifyRoad(unicID, (int)Vector2Int.Distance(endOfFirstSegment, r.endPoint), true);
            c.RoadOnScene.gameObject.SetActive(true);
        }
    }
    public void RemoveCell(string unicID, Vector2Int cell)
    {
        if (roads.ContainsKey(unicID))
        {
            var r = roads[unicID];
            Vector2Int delta = new Vector2Int(
                Math.Sign(r.endPoint.x - r.startPoint.x),
                Math.Sign(r.endPoint.y - r.startPoint.y)
                );

            if ((cell.x - r.startPoint.x) / (r.endPoint.x - r.startPoint.x) == (cell.y - r.startPoint.y) / (r.endPoint.y - r.startPoint.y))
            {
                if (cell == r.startPoint) ModifyRoad(unicID, -1, false);
                else if (cell == r.endPoint) ModifyRoad(unicID, -1, true);
                else
                {
                    SplitRoad(unicID, cell - delta, cell + delta);
                }
            }
        }
    }
    void CheckAllBuildings()
    {
        List<string> newConsID = new();
        List<string> newProdID = new();
        foreach (var r in roads.Values)
        {
            foreach (var v in r.GetRoadOccupedCells())
            {
                foreach (var id in visualService.GetUnicIDsAroundPoint(v))
                {
                    if (logicService.Buildings.ContainsKey(id))
                    {
                        newConsID.Add(id);
                        if (logicService.Buildings[id] is IResourceProducer) newProdID.Add(id);
                    }
                }
            }
        }
        if (newConsID.Count != consumers.Count)
        {
            if (newConsID.Count > consumers.Count)
            {
                foreach (var cID in newConsID)
                    if (!consumers.ContainsKey(cID)) RegisterConsumer(cID);
            }
            else if (newConsID.Count < consumers.Count)
                foreach (var c in consumers.Keys)
                    if (!newConsID.Contains(c)) UnregisterConsumer(c);
        }
        else
        {
            var itemsToRemove = consumers.Keys.Except(newConsID).ToList();
            var itemsToAdd = newConsID.Except(consumers.Keys).ToList();
            foreach (var r in itemsToRemove)
                UnregisterConsumer(r);
             foreach (var r in itemsToAdd)
                RegisterConsumer(r);  
        }
        if (newProdID.Count != producers.Count)
        { 
            if (newProdID.Count > producers.Count)
            {
                foreach (var cID in newProdID)
                    if (!producers.ContainsKey(cID)) RegisterProducer(cID);
            }
            else if (newProdID.Count < producers.Count)
                foreach (var c in producers.Keys)
                    if (!newProdID.Contains(c)) UnregisterProducer(c);
        }
        else
        {
            var itemsToRemove = producers.Keys.Except(newProdID).ToList();
            var itemsToAdd = newProdID.Except(producers.Keys).ToList();
            foreach (var r in itemsToRemove)
                UnregisterProducer(r);
            foreach (var r in itemsToAdd)
                RegisterProducer(r);
        }
    }
    void AddRequest(string itemID, string unicID, int amount)
    {

        if (!requests.ContainsKey(itemID))
            requests.Add(itemID, new SortedSet<ItemRequest>(new ItemRequestComparer()));
        requests[itemID].Add(new ItemRequest { buildingUnicID = unicID, amount = amount, priority = logicService.Buildings[unicID].Priority });
    }
    void RemoveRequest(string itemID, string unicID)
    {
        if (requests.ContainsKey(itemID))
            requests[itemID].RemoveWhere(f => f.buildingUnicID == unicID);
    }
    void RegisterConsumer(string unicID)
    {
        if (logicService.Buildings.ContainsKey(unicID))
        {
            if (!consumers.ContainsKey(unicID))
            {
                var b = logicService.Buildings[unicID] as IResourceConsumer;
                b.OnRequestRegistered += AddRequest;
                b.CheckAndRegisterRequests();
                consumers.Add(unicID, b);
            }
        }
    }
    void UnregisterConsumer(string unicID)
    {
        if (consumers.ContainsKey(unicID))
        {
            foreach (var s in requests.Keys)
                RemoveRequest(s, unicID);
            var b = consumers[unicID];
            b.OnRequestRegistered -= AddRequest;
            consumers.Remove(unicID);
        }
    }
    void RegisterProducer(string unicID)
    {
        if (logicService.Buildings.ContainsKey(unicID))
        {
            if (!producers.ContainsKey(unicID))
            {
                var b = logicService.Buildings[unicID];
                if (b is IResourceProducer)
                    producers.Add(b.UnicID, b as IResourceProducer);
            }
        }
    }
    void UnregisterProducer(string unicID)
    {
        if (producers.ContainsKey(unicID))
            producers.Remove(unicID);
    }
    public List<string> ContainInOtherRoadsThisCell(Vector2Int cell)
    {
        List<string> result = new();
        foreach (var r in roads.Values)
        {
            foreach (var c in r.GetRoadOccupedCells())
            {
                if (c == cell) result.Add(r.UnicID);
            }
        }
        return result;
    }
    public void DistributionPerTick(TickableEvent tickableEvent)
    {
        foreach (var p in producers.Values)
        {
            int tempAmount;
            foreach (string itemID in p.GetResources())
            {
                if (requests[itemID].Count() > 0 && p.Has(itemID))
                {
                    tempAmount = p.GetResourceAmount(itemID);
                    items[itemID] += tempAmount;
                    p.RemoveResources(itemID, tempAmount);
                }
            }
        }
        foreach (string itemID in requests.Keys)
        {
            if (requests[itemID].Count() > 0)
            {
                while (items[itemID] > 0)
                {

                    var first = requests[itemID].First();
                    if (items[itemID] >= first.amount)
                    {
                        consumers[first.buildingUnicID].ReceiveResources(itemID, first.amount);
                        items[itemID] -= first.amount;
                        requests[itemID].Remove(first);
                    }
                    else
                    {
                        consumers[first.buildingUnicID].ReceiveResources(itemID, items[itemID]);
                        items[itemID] = 0;
                    }
                }
            }
        }
    }
}
public class VirtualLogisticsCenterDataForMerge
{
    public Dictionary<string, IResourceConsumer> consumers;
    public Dictionary<string, IResourceProducer> producers;
    public Dictionary<string, SortedSet<ItemRequest>> requests;
    public Dictionary<string, int> items;
    public Dictionary<string, Road> roads;
    public Dictionary<string, RoadData> roadsData;
}
public class ItemRequest
{
    public string buildingUnicID;
    public Priority priority;
    public int amount;
    public override bool Equals(object obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }
        var t = obj as ItemRequest;
        return t.buildingUnicID == buildingUnicID && priority == t.priority && t.amount == amount;
    }
    
    public override int GetHashCode()
    {
        return buildingUnicID.GetHashCode() ^ priority.GetHashCode() ^ amount.GetHashCode();
    }
}
    public class ItemRequestComparer : IComparer<ItemRequest>
    {
        public int Compare(ItemRequest x, ItemRequest y)
        {
            return  y.priority.CompareTo(x.priority);
        }
    }