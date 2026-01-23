using System;
using System.Collections.Generic;
using NUnit.Framework;
using UniRx;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zenject;

public class PlayerController : MonoBehaviour, IDisposable,IPlaceBuildingPlayerData,IPlaceRoadPlayerData

{

    [SerializeField] UIManager manager;
    [SerializeField] ConstructionButtonHandler handler;
    [SerializeField] InputActionAsset playerInput;
    [SerializeField] int distanceToBuildingWithOpenWindow;
    [SerializeField] LayerMask BuildingMask;
    [SerializeField] LayerMask GroundMask;

    [SerializeField] CameraController cameraController;
    [Inject] GameController gameController;
    [Inject] PlayerPlaceBuildingSystem playerPlaceBuildingSystem;
    [Inject] PlayerPlaceRoadSystem playerPlaceRoadSystem;
    InputActionMap GamePlay;
    InputActionMap UI;
    InputActionMap Building;

    // UI actions
    InputAction UIClick;
    
    // Building actions 
    InputAction PlacePoint;
    InputAction Back;
    InputAction RotateBuilding;
    InputAction ForceBuilding;
    InputAction Hold;
    
    RaycastHit hit;


    public int rotation {get;private set;}
    public Vector2Int pos {get;private set;}
    public bool isForce {get;private set;}
    Action<bool,bool> PlaceDelegate;
    Action BackDelegate;

    bool isLoaded;
    Entity PlaceCommand;
    EntityManager entityManager;
    IDisposable disposUI;
    bool UiState;
    public void Initialize(PlayerPlaceBuildingSystem buildSystem, PlayerPlaceRoadSystem roadSystem)
    {
        playerPlaceBuildingSystem = buildSystem;
        playerPlaceRoadSystem = roadSystem;
    }
    void Start()
    {
        SetUp();
        isLoaded = true;
        entityManager= World.DefaultGameObjectInjectionWorld.EntityManager;
        PlaceCommand=entityManager.CreateEntity();
       

        entityManager.AddComponent<PlayerCommand>(PlaceCommand);
        entityManager.AddComponent<PlayerPlacingBuilding>(PlaceCommand);
        entityManager.AddComponent<PlayerPlacingRoad>(PlaceCommand);
        entityManager.AddComponent<PathfindingRequest>(PlaceCommand);
        

        entityManager.SetComponentEnabled<PathfindingRequest>(PlaceCommand,false);
        entityManager.SetComponentEnabled<PlayerPlacingRoad>(PlaceCommand,false);
        entityManager.SetComponentEnabled<PlayerPlacingBuilding>(PlaceCommand,false);
        entityManager.AddBuffer<MapPoint>(PlaceCommand); 
    }
    void BindMaps()
    {
        GamePlay = playerInput.FindActionMap("GamePlay");
        UI = playerInput.FindActionMap("UI");
        Building = playerInput.FindActionMap("Building");
    }

    void BindUIActions()
    {
        UIClick = UI.FindAction("Click");
    }

    void BindBuildingActions()
    {
        PlacePoint = Building.FindAction("PlacePoint");
        Back = Building.FindAction("Back");
        RotateBuilding = Building.FindAction("Rotate");
        ForceBuilding = Building.FindAction("ForceBuilding");
        Hold = GamePlay.FindAction("Hold");
    }

    void SetUp()
    {
        BindMaps();
        BindUIActions();
        BindBuildingActions();

        Building.Disable();
        UI.Disable(); 
        SwitchToUIMode();

        handler.onBuildingSelected += SetUpAction;
    }

    void Update()
    {
        if (!isLoaded)
            return;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out hit))
        {
            pos=gameController.GetMapPos(hit.point);
            isForce=ForceBuilding.IsPressed();
        }
    }

   
    public void SetUpAction(int buildingID)
    {
        ClearAction();
        Debug.Log(buildingID);
        if (buildingID == "Road".GetStableHashCode())
        {
            playerPlaceRoadSystem.onBuildingDone -= SwitchToUIMode;
            playerPlaceRoadSystem.onBuildingDone += SwitchToUIMode;
            PlaceDelegate=playerPlaceRoadSystem.PlaceRoad;
            BackDelegate=playerPlaceRoadSystem.Back;
            playerPlaceRoadSystem.SetUpBuilding(buildingID,this,PlaceCommand);
            entityManager.SetComponentEnabled<PlayerPlacingRoad>(PlaceCommand,true);
            entityManager.SetComponentEnabled<PlayerPlacingBuilding>(PlaceCommand,false);
        }
        else
        {
            
            playerPlaceBuildingSystem.onBuildingDone -= SwitchToUIMode;
            playerPlaceBuildingSystem.onBuildingDone += SwitchToUIMode;
            PlaceDelegate=playerPlaceBuildingSystem.PlaceBuilding;
            BackDelegate=playerPlaceBuildingSystem.Back;
            //неучитываыет коннектед
            playerPlaceBuildingSystem.SetUpBuilding(buildingID,true,this,PlaceCommand);
            entityManager.SetComponentEnabled<PlayerPlacingRoad>(PlaceCommand,false);
            entityManager.SetComponentEnabled<PlayerPlacingBuilding>(PlaceCommand,true);
            
        }

        SwitchToBuildingMode();

        PlacePoint.performed += OnPlacePerformed;
        Back.performed += OnBackPerformed;
        RotateBuilding.performed += OnRotateBuildingPerformed;
    }

    void SwitchToBuildingMode()
    {
        UiState=false;
        UI.Disable();
        UIClick.performed -= OnUIClickPerformed;
        Building.Enable();
    }

    void SwitchToUIMode()
    {
        UiState=true;
        Debug.Log("свитч");
        Building.Disable();
        PlacePoint.performed-= OnPlacePerformed;
        Back.performed -= OnBackPerformed;
        RotateBuilding.performed -= OnRotateBuildingPerformed;
        playerPlaceRoadSystem.onBuildingDone -= SwitchToUIMode;
        playerPlaceBuildingSystem.onBuildingDone -= SwitchToUIMode;
        PlaceDelegate=null;
        BackDelegate=null;
        UI.Enable();
        UIClick.performed += OnUIClickPerformed;
    }
    private bool IsPointerOverUI()
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Mouse.current.position.ReadValue();
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0; // Если в списке есть объекты, значит клик попал в UI
    }
    void OnUIClickPerformed(InputAction.CallbackContext context)
    {
        if (context.performed )
        {
            if (IsPointerOverUI()) return; 

            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out hit,BuildingMask))
            {
                if (hit.collider != null)
                {
                    var building = hit.collider.GetComponent<BuildingOnScene>();
                    if (building != null)
                    {
                        OpenBuildingMenu(building.id);
                        return;
                    }
                }
            }
        }
    }

    void OpenBuildingMenu(int buildingID)
    {
        if (manager != null)
        {
            manager.buildingManagmentWindowManager.OpenBuilding(buildingID,Hold.IsPressed());
            disposUI=manager.buildingManagmentWindowManager.isOpened.Subscribe(value =>
            {
                if(!manager.buildingManagmentWindowManager.isOpened.Value) CloseBuildingMenu();
            });
        }

        // UI.Disable();
        // UIClick.performed -= OnUIClickPerformed;
    }

    public void CloseBuildingMenu()
    {
        if (UiState)
        {
            disposUI?.Dispose();
            disposUI=null;
            SwitchToUIMode();
        }
    }

    void OnPlacePerformed(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
           PlaceDelegate?.Invoke(Hold.IsPressed(),isForce);
        }
    }

    void OnBackPerformed(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
           BackDelegate?.Invoke();
        }
    }

    void OnRotateBuildingPerformed(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            rotation++;
            rotation%=4;
        }
    }

    void ClearAction()
    {
        UIClick.performed -= OnUIClickPerformed;
        PlacePoint.performed-= OnPlacePerformed;
        Back.performed -= OnBackPerformed;
        RotateBuilding.performed -= OnRotateBuildingPerformed;

        Building.Disable();
        UI.Disable();

        playerPlaceRoadSystem.onBuildingDone -= SwitchToUIMode;
        playerPlaceBuildingSystem.onBuildingDone -= SwitchToUIMode;
    }

    void OnDestroy()
    {
        Dispose();
    }

    public void Dispose()
    {
        ClearAction();

        if (handler != null)
            handler.onBuildingSelected -= SetUpAction;
    }
}


public interface IPlaceBuildingPlayerData
{
    int rotation{get;}
    Vector2Int pos{get;}
    bool isForce{get;}
}
public interface IPlaceRoadPlayerData
{
    Vector2Int pos{get;}
    bool isForce{get;}
}