using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

public class CameraController : MonoBehaviour,IInitializable
{
    [SerializeField]
    Transform LookPoint;

    [SerializeField]
    float rotationSpeed = 100.0f;

    [SerializeField]
    float smoothTime = 0.08f;

    [SerializeField]
    float verticalAngleLimit = 80f;

    [SerializeField]
    float zoomSpeed = 100f;

    [Range(20, 100)]
    [SerializeField]
    float maxDistance = 100f;

    [Range(10, 90)]
    [SerializeField]
    float minDistance = 10f;

    [Range(1, 10)]
    [SerializeField]
    float baseSpeed;

    [SerializeField]
    InputActionReference Move;

    [SerializeField]
    InputActionReference Scrool;

    [SerializeField]
    InputActionReference CameraRotateBT;

    [SerializeField]
    InputActionReference MouseRotate;

    [SerializeField]
    InputActionReference HoldBT;

    [SerializeField]
    LayerMask layerMask;
    [Inject] World world;
    [Inject] IReadOnlySave readOnlySave;
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
        if (minDistance > maxDistance)
            maxDistance = minDistance + 1;
    }
public void SetUp(PlayerCamData playerCamData)
{
    if (playerCamData != null && playerCamData.isInitialized)
    {
        // Явное приведение float3 -> Vector3
        LookPoint.position = (Vector3)playerCamData.lookPointPosition;

        // Явное приведение float2 -> Vector2/Vector3
        currentRotation = targetRotation = (Vector2)playerCamData.cameraRotation;

        currentDistance = targetDistance = playerCamData.cameraDistance;

        ApplyCameraPosition();
    }
    else
    {
        // Твой старый блок else...
        currentDistance = targetDistance = Vector3.Distance(transform.position, LookPoint.position);
        Vector3 direction = (transform.position - LookPoint.position).normalized;
        currentRotation = targetRotation = Quaternion.LookRotation(direction).eulerAngles;
        SaveCameraState();
    }
}
    void Update()
    {
        if (CameraRotateBT.action.IsPressed())
            RotateCam();
        else
            CameraMove();

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

        Vector3 moveDirection =
            (cameraForward * dir.y + cameraRight * dir.x) * resSpeed * Time.deltaTime;

        LookPoint.transform.position += moveDirection;
    }

public void ChangeDistance()
{
    float zoomInput = Scrool.action.ReadValue<float>();

    if (zoomInput != 0)
    {
        float nextDistance = targetDistance - zoomInput * zoomSpeed * Time.deltaTime;
        nextDistance = Mathf.Clamp(nextDistance, minDistance, maxDistance);

        if (zoomInput > 0)
        {
            Quaternion rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0f);
            Vector3 direction = rotation * Vector3.forward;

            Vector3 nextCameraPosition =
                LookPoint.position - direction * nextDistance;

            bool blocked =
                Physics.CheckSphere(
                    nextCameraPosition,
                    2f,
                    layerMask);

            if (!blocked)
            {
                Ray downRay = new Ray(nextCameraPosition + Vector3.up, Vector3.down);

                if (Physics.Raycast(
                    downRay,
                    out RaycastHit hit,
                    10f,
                    layerMask))
                {
                    blocked = hit.distance <= 3f;
                }
            }

            if (blocked)
                return;
        }

        targetDistance = nextDistance;
    }

    currentDistance = Mathf.SmoothDamp(
        currentDistance,
        targetDistance,
        ref zoomVelocity,
        smoothTime);

    ApplyCameraPosition();
}


void ApplyCameraPosition()
{
    if (LookPoint == null)
        return;

    Quaternion rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0f);

    Vector3 direction = rotation * Vector3.forward;

    transform.position =
        LookPoint.position - direction * currentDistance;

    transform.LookAt(LookPoint.position);
}


   public void RotateCam()
{
    Vector2 mouseDelta = MouseRotate.action.ReadValue<Vector2>();

    float nextRotationX = targetRotation.x - mouseDelta.y * rotationSpeed * Time.deltaTime;
    nextRotationX = Mathf.Clamp(nextRotationX, -verticalAngleLimit, verticalAngleLimit);

    var query = world.EntityManager.CreateEntityQuery(typeof(PlayerRayCastData));
    if (!query.IsEmpty)
    {
        var ecsData = query.GetSingletonEntity();
        var currentEcsData = world.EntityManager.GetComponentData<PlayerRayCastData>(ecsData);

        if (currentEcsData.CamHasHit)
        {
            float groundHeight = currentEcsData.CamHitBlockPos.y + 1f;
            float minAllowedY = groundHeight + 0.5f;

            float nextRotationY = targetRotation.y + mouseDelta.x * rotationSpeed * Time.deltaTime;

            Quaternion potentialRot = Quaternion.Euler(nextRotationX, nextRotationY, 0f);
            Vector3 potentialDir = potentialRot * Vector3.forward;

            float actualDistance = targetDistance;

            float potentialY =
                LookPoint.position.y - potentialDir.y * actualDistance;

            if (potentialY < minAllowedY)
            {
                nextRotationX = targetRotation.x;
            }
        }
    }

    targetRotation.x = nextRotationX;
    targetRotation.y += mouseDelta.x * rotationSpeed * Time.deltaTime;

    currentRotation.x = Mathf.SmoothDampAngle(
        currentRotation.x,
        targetRotation.x,
        ref rotationVelocity.x,
        smoothTime);

    currentRotation.y = Mathf.SmoothDampAngle(
        currentRotation.y,
        targetRotation.y,
        ref rotationVelocity.y,
        smoothTime);

    ApplyCameraPosition();
}

void SaveCameraState()
{
    
    if (readOnlySave != null && readOnlySave.GameState != null)
    {
        if (readOnlySave.GameState.camData == null)
            readOnlySave.GameState.camData = new PlayerCamData();

        var activeCamData = readOnlySave.GameState.camData;

        // Принудительно приводим Vector3/Vector2 к float3/float2 перед записью
        activeCamData.lookPointPosition = (float3)LookPoint.position;
        activeCamData.cameraRotation = (float2)currentRotation;
        activeCamData.cameraDistance = currentDistance;
        activeCamData.CamPosition = (float3)transform.position;
        activeCamData.isInitialized = true;
    }

    var query = world.EntityManager.CreateEntityQuery(typeof(PlayerRayCastData));
    if (!query.IsEmpty) 
    {
        var entity = query.GetSingletonEntity();
        
        Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        world.EntityManager.SetComponentData(entity, new PlayerRayCastData
        {
            Origin = mouseRay.origin,
            Direction = mouseRay.direction,
            MaxDistance = 1000,
            
            // Чтобы ECS считал землю под ИДЕАЛЬНОЙ позицией камеры, а не под прижатой к стене:
            CamOrigin = new float3(LookPoint.position.x - (Quaternion.Euler(currentRotation.x, currentRotation.y, 0f) * Vector3.forward).x * targetDistance, 
                                   LookPoint.position.y + 20f, 
                                   LookPoint.position.z - (Quaternion.Euler(currentRotation.x, currentRotation.y, 0f) * Vector3.forward).z * targetDistance),
            CamDirection = new float3(0f, -1f, 0f), 
            CamMaxDistance = 100f
        });
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

    public void Initialize()
    {
        throw new NotImplementedException();
    }
}
