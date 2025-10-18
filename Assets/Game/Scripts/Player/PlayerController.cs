using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

public class PlayerController : MonoBehaviour
{
    
    BuildingAction _buildingAction;
    [SerializeField] UIManager manager;
    //[Inject] UIManager UIManager;
    [SerializeField] InputActionAsset playerInput;
    [SerializeField] CameraController cameraController;
    [Inject] IInstantiator instantiator;
    InputActionMap GamePlay;
    InputActionMap UI;
    InputActionMap Building;
    //building
    InputAction PlacePoint;
    InputAction Back;
    InputAction RotateBuilding;
    InputAction ForceBuilding;
    InputAction Hold;
    RaycastHit hit;
    bool isLoaded = false;
    void Start()
    {
        SetUp();
        isLoaded = true;
    }
    public void SetUp()
    {
        
        _buildingAction = instantiator.Instantiate<BuildingAction>();
        BindMaps();
        BindBuildingActions();
        Building.Disable();
        manager.OnInfoInvoked += SetUpAction;
    }
    void Update()
    {
        if (!isLoaded) return;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray,out hit))
        {
            
            if (_buildingAction.IsAction)
            {
               _buildingAction.Update(hit.point,ForceBuilding.IsPressed());
            }
            else
            {
                //hit.collider/получение постройки и ее открытие
            }
        }
    }
    void BindMaps()
    {
        GamePlay = playerInput.FindActionMap("GamePlay");
        UI = playerInput.FindActionMap("UI");
        Building = playerInput.FindActionMap("Building");
    }
    void BindBuildingActions()
    {
        PlacePoint = Building.FindAction("PlacePoint");
        Back = Building.FindAction("Back");
        RotateBuilding = Building.FindAction("Rotate");
        ForceBuilding = Building.FindAction("ForceBuilding");
        Hold = Building.FindAction("Hold");
    }
    public void SetUpAction(string buildingID)
    {
        Debug.Log("id recived");
        _buildingAction.SetUp(buildingID.GetStableHashCode());
        Building.Enable();
        _buildingAction.OnActionDone += ClearAction;
        PlacePoint.performed += OnPlacePointPerformed;
        Back.performed += OnBackPerformed;
        RotateBuilding.performed += OnRotateBuildingPerformed;
    }
    
    void ClearAction()
    {
        PlacePoint.performed -= OnPlacePointPerformed;
        Back.performed -= OnBackPerformed;
        RotateBuilding.performed -= OnRotateBuildingPerformed;

        Building.Disable();
        _buildingAction.OnActionDone -= ClearAction;
    }
    
    private void OnPlacePointPerformed(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            bool isForceBuildingPressed = ForceBuilding.IsPressed();
            bool isManyPointPressed = Hold.IsPressed();
            _buildingAction.PlaceBuilding(isManyPointPressed,isForceBuildingPressed);
        }
    }
    
    private void OnBackPerformed(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
             _buildingAction.Back();
        }
    }
    
    private void OnRotateBuildingPerformed(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _buildingAction.RotateBuilding();
        }
    }
}