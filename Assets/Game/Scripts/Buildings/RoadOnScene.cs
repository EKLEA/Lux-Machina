using UnityEngine;
[RequireComponent(typeof(SplineObj))]
public class RoadOnScene: BuildingOnScene
{
    [SerializeField] SplineObj splineObj;
    public void DrawRoad(SplineState state, (Vector3 pos, Quaternion rot) firstPoint, (Vector3 pos, Quaternion rot) secondPoint)
    {
        splineObj.Initialize();
        splineObj.SetFirstPointSpline(firstPoint.pos, firstPoint.rot);
        splineObj.SetSecondPointSpline(secondPoint.pos, secondPoint.rot);
        splineObj.DrawSpline(state);
    }
    public void Modify((Vector3 pos, Quaternion rot) firstPoint, (Vector3 pos, Quaternion rot) secondPoint)
    {
        
        splineObj.Reset();
        splineObj.SetFirstPointSpline(firstPoint.pos, firstPoint.rot);
        splineObj.SetSecondPointSpline(secondPoint.pos, secondPoint.rot);
        splineObj.DrawSpline(SplineState.Passive);
    }
}