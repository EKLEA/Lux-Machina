using System;
using Unity.Mathematics;
using UnityEngine;
[RequireComponent(typeof(Outline))]
public class EnergyNode : MonoBehaviour
{
    [SerializeField] Outline outlineScript;
    public EnergyLine energyLine;
    public Transform Connect;
    public int2 nodeData;    
    public void SetUpNode(int2 data)
    {
        nodeData=data;
    }
    public void SetOutLine(Color? color)
    {
        if(!outlineScript.SetUpded) outlineScript.SetUp();
        if(color!=null)
        {
            outlineScript.enabled=true;

            outlineScript.OutlineColor=color.Value;
        }
        else 
            outlineScript.enabled=false;
    }
}