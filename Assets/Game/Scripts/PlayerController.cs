using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] CameraController cameraController;
    [SerializeField] float MoveSpeed;
    bool isMoving;
    void Update()
    {
        if (isMoving)
        {
            transform.position += MoveSpeed * Time.deltaTime * new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")).normalized;
            cameraController.RotateCam(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse y")));
            cameraController.ChangeDistance(Input.GetAxis("Mouse ScrollWheel"));
        }
    }
}