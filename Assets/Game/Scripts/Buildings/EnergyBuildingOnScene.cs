using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class EnergyBuildingOnScene : BuildingOnScene
{
    
    [SerializeField] EnergyNode[] energyNodes;
    public Dictionary<int,EnergyNode> nodes{get;private set;}
    public void SetUpNodes()
    {
        nodes=new();
        for(int i = 0; i < energyNodes.Length; i++)
        {
            int node=i;
            energyNodes[i].SetUpNode(new int2(node,id));
            nodes.Add(i,energyNodes[i]);
        }
    }
}