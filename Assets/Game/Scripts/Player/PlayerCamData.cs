using System;
using UnityEngine;

[System.Serializable]
public class PlayerCamData
{
    public Vector3 lookPointPosition;
    public Vector2 cameraRotation;
    public float cameraDistance;
    public Vector3 CamPosition;
    public bool isInitialized = false;
}