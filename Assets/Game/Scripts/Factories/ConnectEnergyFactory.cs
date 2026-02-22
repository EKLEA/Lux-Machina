using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using Zenject;

public class ConnectEnergyFactory
{
    [Inject] IReadOnlyEnergyLineConfig energyLineConfig;
    [Inject] IInstantiator instantiator;
    public EnergyLine CreateLine()
    {
        return  instantiator.InstantiatePrefabForComponent<EnergyLine>(energyLineConfig.energyLine);
    }
    private readonly Dictionary<EnergyNode, EnergyNode> _connections = new();
    public bool HasConnection(EnergyNode node)
    {
        return _connections.ContainsKey(node);
    }
    public void UpdateConnect(EnergyNode node1, EnergyNode node2,bool IsConnecting=true)
    {
        ResetLine(node1);
        ResetLine(node2);
        if (_connections.ContainsKey(node1)&&_connections[node1]!=node2 || _connections.ContainsKey(node2) && _connections[node2] != node1)
        {
            Disconnect(node1);
            Disconnect(node2);
        }
        else 
            Modifyine(node1, node2,IsConnecting);
        Connect(node1,node2,IsConnecting);
    }
    public void Connect(EnergyNode node1, EnergyNode node2,bool IsConnecting=true)
    {
        if (_connections.ContainsKey(node1) || _connections.ContainsKey(node2)) return;

        _connections.Add(node1, node2);
        _connections.Add(node2, node1); 
        Modifyine(node1, node2,IsConnecting);
    }

    public void Disconnect(EnergyNode node)
    {
        if (!_connections.TryGetValue(node, out EnergyNode connectedNode)) return;

        ResetLine(node);
        ResetLine(connectedNode);

        _connections.Remove(node);
        _connections.Remove(connectedNode);
    }
    public void Modifyine(EnergyNode owner, Vector3 target,bool IsConnecting=true)
    {
        
        ResetLine(owner);
        var line = owner.energyLine;
        Modifyine(line,owner.Connect.transform.position,target,IsConnecting);
    }
    public void Modifyine(EnergyNode owner, EnergyNode target,bool IsConnecting=true)
    {
        ResetLine(owner);
        ResetLine(target);
        var line = owner.energyLine;
        Modifyine(line,owner.Connect.transform.position,target.Connect.transform.position,IsConnecting);
    }
    public void Modifyine(EnergyLine line, Vector3 owner, Vector3 target,bool IsConnecting=true)
    {
        
        if (line == null) return;
        if(IsConnecting) 
            line.ChangeColor(energyLineConfig.ConnectionColor,energyLineConfig.ConnectionPulseColor);
        else line.ChangeColor(energyLineConfig.DisconnectColor,energyLineConfig.DisconnectPulseColor);
        line.gameObject.SetActive(true);
        line.lineRenderer.useWorldSpace = true;
        line.lineRenderer.SetPosition(0, owner);
        line.lineRenderer.SetPosition(1, target);
    }

    public void ResetLine(EnergyNode node)
    {
        if (node.energyLine.lineRenderer != null)
        {
            node.energyLine.ChangeColor(energyLineConfig.ConnectionColor,energyLineConfig.ConnectionPulseColor);
            node.energyLine.lineRenderer.SetPosition(0, node.Connect.transform.position);
            node.energyLine.lineRenderer.SetPosition(1, node.Connect.transform.position);
            node.energyLine.lineRenderer.gameObject.SetActive(false);
        }
    }
    public void ResetLine(EnergyLine line,Vector3 pos)
    {
        if (line.lineRenderer != null)
        {
            line.ChangeColor(energyLineConfig.ConnectionColor,energyLineConfig.ConnectionPulseColor);
            line.lineRenderer.SetPosition(0, pos);
            line.lineRenderer.SetPosition(1, pos);
            line.lineRenderer.gameObject.SetActive(false);
        }
    }
}