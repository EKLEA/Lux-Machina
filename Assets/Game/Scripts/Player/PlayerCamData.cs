using System;
using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public class PlayerCamData
{
    public float3  lookPointPosition;
    public float2  cameraRotation;
    public float cameraDistance;
    public float3  CamPosition;
    public bool isInitialized = false;
}
