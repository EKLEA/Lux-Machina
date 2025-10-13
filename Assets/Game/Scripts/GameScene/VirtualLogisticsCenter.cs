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
    public Guid CenterID => Guid.Parse(UnicID);
    
    Dictionary<string, IResourceConsumer> consumers;
    Dictionary<string, IResourceProducer> producers;
    Dictionary<string, SortedSet<ItemRequest>> requests;
    Dictionary<string, int> items
    {
        get => _data.items;
        set => _data.items = value;
    }
    
    [Inject] ReadOnlyBuildingsLogicService logicService;
    [Inject] VirtualLogisticsCenterService centerService;
    
    VirtualLogisticsCenterData _data;

    public VirtualLogisticsCenter(VirtualLogisticsCenterData data)
    {
        _data = data;
        consumers = new();
        producers = new();
        requests = new();
    }

    public VirtualLogisticsCenterDataForMerge GetDataForMerge()
    {
        var data = new VirtualLogisticsCenterDataForMerge
        {
            consumers = this.consumers,
            producers = this.producers,
            requests = this.requests,
            items = this.items
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
        
        await Task.Yield();
    }

    public void RegisterBuilding(string buildingId)
    {
        if (logicService.Buildings.ContainsKey(buildingId))
        {
            if (!consumers.ContainsKey(buildingId) && logicService.Buildings[buildingId] is IResourceConsumer consumer)
            {
                consumer.OnRequestRegistered += AddRequest;
                consumers.Add(buildingId, consumer);
                consumer.CheckAndRegisterRequests();
            }
            
            if (!producers.ContainsKey(buildingId) && logicService.Buildings[buildingId] is IResourceProducer producer)
            {
                producers.Add(buildingId, producer);
            }
        }
    }

    public void UnregisterBuilding(string buildingId)
    {
        if (consumers.ContainsKey(buildingId))
        {
            var consumer = consumers[buildingId];
            consumer.OnRequestRegistered -= AddRequest;
            consumers.Remove(buildingId);
            
            foreach (var itemId in requests.Keys.ToList())
            {
                RemoveRequest(itemId, buildingId);
            }
        }
        
        if (producers.ContainsKey(buildingId))
        {
            producers.Remove(buildingId);
        }
    }

    void AddRequest(string itemID, string buildingUnicID, int amount)
    {
        if (!centerService.IsMasterCenterForBuilding(this.CenterID, buildingUnicID))
            return;

        if (!requests.ContainsKey(itemID))
            requests.Add(itemID, new SortedSet<ItemRequest>(new ItemRequestComparer()));
            
        requests[itemID].Add(new ItemRequest { 
            buildingUnicID = buildingUnicID, 
            amount = amount, 
            priority = logicService.Buildings[buildingUnicID].Priority 
        });
    }

    void RemoveRequest(string itemID, string buildingUnicID)
    {
        if (requests.ContainsKey(itemID))
            requests[itemID].RemoveWhere(f => f.buildingUnicID == buildingUnicID);
    }

    public List<string> GetAllBuildingIds()
    {
        return consumers.Keys.Union(producers.Keys).ToList();
    }

    public void DistributionPerTick(TickableEvent tickableEvent)
    {
        foreach (var p in producers.Values)
        {
            foreach (string itemID in p.GetResources())
            {
                if (requests.ContainsKey(itemID) && requests[itemID].Count > 0 && p.Has(itemID))
                {
                    int tempAmount = p.GetResourceAmount(itemID);
                    items[itemID] += tempAmount;
                    p.RemoveResources(itemID, tempAmount);
                }
            }
        }
        
        foreach (string itemID in requests.Keys)
        {
            if (requests[itemID].Count > 0 && items.ContainsKey(itemID))
            {
                while (items[itemID] > 0 && requests[itemID].Count > 0)
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
}

public class ItemRequest
{
    public string buildingUnicID;
    public Priority priority;
    public int amount;
    
    public override bool Equals(object obj)
    {
        if (obj == null || GetType() != obj.GetType())
            return false;
            
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
        return y.priority.CompareTo(x.priority);
    }
}