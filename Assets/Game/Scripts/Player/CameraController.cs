using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

public class CameraController : MonoBehaviour
{
    [SerializeField] Transform LookPoint;
    
    [SerializeField] float rotationSpeed = 100.0f;
    [SerializeField] float smoothTime = 0.08f;
    [SerializeField] float verticalAngleLimit = 80f;
    
    [SerializeField] float zoomSpeed = 100f;
    [Range(20,100)] [SerializeField] float maxDistance = 100f;
    [Range(10, 90)][SerializeField] float minDistance = 10f;
    
    [Range(1, 10)][SerializeField] float baseSpeed;
    
    [SerializeField] InputActionReference Move;
    [SerializeField] InputActionReference Scrool;
    [SerializeField] InputActionReference CameraRotateBT;
    [SerializeField] InputActionReference MouseRotate;
    [SerializeField] InputActionReference HoldBT;
    [SerializeField] LayerMask layerMask;
    Vector2 currentRotation;
    Vector2 targetRotation;
    Vector2 rotationVelocity;
    float currentDistance;
    float targetDistance;
    float zoomVelocity;
    float resSpeed;
    
    PlayerCamData camData;

    void OnValidate()
    {
        if (minDistance > maxDistance) maxDistance = minDistance + 1;
    }
    
    public void SetUp(PlayerCamData playerCamData)
    {
        camData = playerCamData;
        
        if (camData != null && camData.isInitialized)
        {
            LookPoint.position = camData.lookPointPosition;
            
            currentRotation = targetRotation = camData.cameraRotation;
            
            currentDistance = targetDistance = camData.cameraDistance;
            
            ApplyCameraPosition();
        }
        else
        {
            currentDistance = targetDistance = Vector3.Distance(transform.position, LookPoint.position);
            
            Vector3 direction = (transform.position - LookPoint.position).normalized;
            currentRotation = targetRotation = Quaternion.LookRotation(direction).eulerAngles;
            
            SaveCameraState();
        }
    }
    
    void Update()
    {
        if (CameraRotateBT.action.IsPressed()) RotateCam();
        else  CameraMove();
        
        ChangeDistance();
        GetGroundHeight(transform.position);
        
        SaveCameraState();
    }
    
    public void CameraMove()
    {
        if (HoldBT.action.IsPressed()) 
            resSpeed = 2 * baseSpeed; 
        else 
            resSpeed = baseSpeed;
            
        var dir = Move.action.ReadValue<Vector2>();
        
        Vector3 cameraForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 cameraRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        
        Vector3 moveDirection = (cameraForward * dir.y + cameraRight * dir.x) * resSpeed * Time.deltaTime;
        
        LookPoint.transform.position += moveDirection;
    }
    
    public void ChangeDistance()
    {
        float zoomInput = Scrool.action.ReadValue<float>();
        if (zoomInput != 0)
        {
            targetDistance -= zoomInput * zoomSpeed * Time.deltaTime;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
            currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref zoomVelocity, smoothTime);

            ApplyCameraPosition();
        }
    }
    
    void ApplyCameraPosition()
    {
        if (LookPoint != null)
        {
            Quaternion rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0f);
            Vector3 direction = rotation * Vector3.forward;
            
            transform.position = LookPoint.position - direction * currentDistance;
            transform.LookAt(LookPoint);
        }
    }
    
    public void RotateCam()
    {
        Vector2 mouseDelta = MouseRotate.action.ReadValue<Vector2>();
        
        targetRotation.y += mouseDelta.x * rotationSpeed * Time.deltaTime;
        targetRotation.x -= mouseDelta.y * rotationSpeed * Time.deltaTime;
        
        targetRotation.x = Mathf.Clamp(targetRotation.x, -verticalAngleLimit, verticalAngleLimit);
        
        currentRotation.x = Mathf.SmoothDampAngle(currentRotation.x, targetRotation.x, ref rotationVelocity.x, smoothTime);
        currentRotation.y = Mathf.SmoothDampAngle(currentRotation.y, targetRotation.y, ref rotationVelocity.y, smoothTime);
        
        ApplyCameraPosition();
    }
    
    void SaveCameraState()
    {
        if (camData != null)
        {
            camData.lookPointPosition = LookPoint.position;
            camData.cameraRotation = currentRotation;
            camData.cameraDistance = currentDistance;
            camData.CamPosition = transform.position;
            camData.isInitialized = true;
        }
    }
    
    float GetGroundHeight(Vector3 position)
    {
        position = position + 100 * Vector3.up;
        Ray ray = new Ray(position, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 100, layerMask))
        {
            return hit.point.y;
        }
        return 0;
    }
    
    void OnEnable()
    {
        Move.action.Enable();
        Scrool.action.Enable();
        CameraRotateBT.action.Enable();
        MouseRotate.action.Enable();
        HoldBT.action.Enable();
    }
    
    void OnDisable()
    {
        Move.action.Disable();
        Scrool.action.Disable();
        CameraRotateBT.action.Disable();
        MouseRotate.action.Disable();
        HoldBT.action.Disable();
    }
}