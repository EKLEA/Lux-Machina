using System;
using System.Collections;
using System.Collections.Generic;
using SplineMeshTools.Colliders;
using SplineMeshTools.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(SplineMeshResolution), typeof(SplineBoxColliderGenerator))]

public class SplineOnScene : MonoBehaviour
{
    Spline spline;
    [SerializeField] protected SplineInstantiate splineInstantiate;
    [SerializeField] protected SplineContainer splineContainer;
    public SplineMeshResolution resolution;
    [SerializeField] protected SplineBoxColliderGenerator splineBoxColliderGenerator;
    public void Initialize()
    {
        spline = splineContainer[0];
        splineBoxColliderGenerator.GenerateAndAssignMesh();
    }
    public float GetLenght()
    {
        return spline.GetLength();
    }
    public void SetFirstPointSpline(Vector3 pos, Quaternion rot)
    {
        Vector3 localPos = splineContainer.transform.InverseTransformPoint(pos);
        Quaternion localRot = Quaternion.Inverse(splineContainer.transform.rotation) * rot;
        spline[0] = new BezierKnot(localPos, Vector3.zero, Vector3.zero, localRot);
    }

    public void SetSecondPointSpline(Vector3 pos, Quaternion rot)
    {
        Vector3 localPos = splineContainer.transform.InverseTransformPoint(pos);
        Quaternion localRot = Quaternion.Inverse(splineContainer.transform.rotation) * rot;
        spline[1] = new BezierKnot(localPos, Vector3.zero, Vector3.zero, localRot);
    }

    public void DrawSpline(SplineState state)
    {
        resolution.meshResolution[0] = state.GetResolution(spline);
        resolution.GenerateMeshAlongSpline();
        splineBoxColliderGenerator.GenerateAndAssignMesh();
    }
    public void Reset()
    {
        spline[0] = new BezierKnot(Vector3.zero, Vector3.zero, Vector3.zero);
        spline[1] = new BezierKnot(Vector3.forward, Vector3.zero, Vector3.zero);
        DrawSpline(SplineState.Active);
    }
}
public enum SplineState
{
    Active,
    Passive
}
public static class SplineExtensons
{
    public static int GetResolution(this SplineState type,Spline spline)
    {
        return type switch
        {
            SplineState.Active=>2,
            SplineState.Passive=>math.max((int)(spline.GetLength()/5),1),
            _=>2
        };
    }
}