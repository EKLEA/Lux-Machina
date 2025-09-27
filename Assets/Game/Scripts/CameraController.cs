using System;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] Transform LookPoint;
     [SerializeField]  float rotationSpeed = 100.0f;
	public float zoomSpeed = 100f;
	[Range(20,100)] [SerializeField]  float maxDistance= 100f;
	[Range(10,90)] [SerializeField]  float minDistance= 10f;
    public void ChangeDistance(float scroll)
    {
        
    }
    public void RotateCam(Vector2 rotVec)
    {
        
    }
    float GetGroundHeight( Vector3 position)
	{
		position=position+100*Vector3.up;
		Ray ray = new Ray(position,Vector3.down);
		if (Physics.Raycast(ray, out RaycastHit hit))
		{
			return hit.point.y;
		}
		return 0;
	}
}