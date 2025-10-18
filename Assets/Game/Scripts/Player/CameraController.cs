using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

public class CameraController : MonoBehaviour
{
    [SerializeField] Transform LookPoint;
    [SerializeField] float rotationSpeed = 2.0f;
    [SerializeField] float zoomSpeed = 50f;
    [SerializeField] float smoothTime = 0.1f;
    [Range(20,100)] [SerializeField] float maxDistance = 100f;
    [Range(10, 90)][SerializeField] float minDistance = 10f;
    [Range(1, 10)][SerializeField] float baseSpeed;
    [SerializeField] private float verticalAngleLimit = 80f;
    float resSpeed;
    
    [SerializeField] InputActionReference Move;
    [SerializeField] InputActionReference Scrool;
    [SerializeField] InputActionReference CameraRotateBT;
    [SerializeField] InputActionReference MouseRotate;
    [SerializeField] InputActionReference HoldBT;
    [SerializeField] LayerMask layerMask;
    void OnValidate()
    {
        if (minDistance > maxDistance) maxDistance = minDistance + 1;
    }
    
    PlayerCamData camData;
    private Vector3 currentCamPosition;
    private Vector3 targetCamPosition;
    private Vector3 currentLookPoint;
    private Vector3 targetLookPoint;
    
    public void SetUp(PlayerCamData playerCamData)
    {
        camData = playerCamData;
        currentLookPoint = targetLookPoint = camData.lookPointPosition;
        currentCamPosition = targetCamPosition = camData.CamPosition;
        
        LookPoint.transform.position = currentLookPoint;
        transform.localPosition = currentCamPosition;
        transform.LookAt(LookPoint);
    }
    
    void Update()
    {
        if (CameraRotateBT.action.IsPressed())  RotateCam();
        else CameraMove();
        
        ChangeDistance();
        
        currentLookPoint = Vector3.Lerp(currentLookPoint, targetLookPoint, smoothTime * 10 * Time.deltaTime);
        currentCamPosition = Vector3.Lerp(currentCamPosition, targetCamPosition, smoothTime * 10 * Time.deltaTime);
        
        LookPoint.transform.position = currentLookPoint;
        transform.localPosition = currentCamPosition;
        transform.LookAt(LookPoint);
        
        camData.lookPointPosition = currentLookPoint;
        camData.CamPosition = currentCamPosition;
    }
    
    public void CameraMove()
    {
        if (HoldBT.action.IsPressed()) resSpeed = 2 * baseSpeed; 
        else resSpeed = baseSpeed;
        
        var dir = Move.action.ReadValue<Vector2>();
        
        Vector3 cameraForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 cameraRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        
        Vector3 moveDirection = (cameraForward * dir.y + cameraRight * dir.x) * resSpeed * Time.deltaTime;
        targetLookPoint += moveDirection;
        
    }
    
    public void ChangeDistance()
    {
        float zoomInput = Scrool.action.ReadValue<float>();
        if (zoomInput != 0)
        {
            Vector3 directionToLookPoint = (targetCamPosition - targetLookPoint).normalized;
            float currentDistance = Vector3.Distance(targetCamPosition, targetLookPoint);
            float newDistance = currentDistance - zoomInput * zoomSpeed * Time.deltaTime;
            newDistance = Mathf.Clamp(newDistance, minDistance, maxDistance);
            
            targetCamPosition = targetLookPoint + directionToLookPoint * newDistance;
        }
    }
    
    public void RotateCam()
    {
        Vector2 mouseDelta = MouseRotate.action.ReadValue<Vector2>() * rotationSpeed * Time.deltaTime;
        
        Vector3 direction = targetCamPosition - targetLookPoint;
        float currentDistance = direction.magnitude;
        
        Quaternion horizontalRotation = Quaternion.AngleAxis(mouseDelta.x, Vector3.up);
        Quaternion verticalRotation = Quaternion.AngleAxis(-mouseDelta.y, transform.right);
        
        Vector3 rotatedDirection = horizontalRotation * verticalRotation * direction;
        
        float verticalAngle = Vector3.Angle(rotatedDirection.normalized, Vector3.up);
        if (verticalAngle < verticalAngleLimit || verticalAngle > (180 - verticalAngleLimit)) rotatedDirection = direction; 
        
        targetCamPosition = targetLookPoint + rotatedDirection.normalized * currentDistance;
    }
    
    float GetGroundHeight(Vector3 position)
    {
        position = position + 100 * Vector3.up;
        Ray ray = new Ray(position, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask))
        {
            return hit.point.y;
        }
        return 0;
    }
}