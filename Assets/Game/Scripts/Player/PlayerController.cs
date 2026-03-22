using System;
using System.Collections.Generic;
using NUnit.Framework;
using UniRx;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Zenject;

public class PlayerController : MonoBehaviour, IDisposable,IPlayerConnectData

{

    [SerializeField] UIManager manager;
    [SerializeField] ConstructionButtonHandler handler;
    [SerializeField] InputActionAsset playerInput;
    [SerializeField] int distanceToBuildingWithOpenWindow;
    [SerializeField] LayerMask BuildingMask;
    [SerializeField] LayerMask GroundMask;
    [SerializeField] LayerMask EnergyNodeMask;
    [SerializeField] GridVisualizer gridVisualizer;
    [SerializeField] FlowFieldVisualizer FlowFieldVisualizer;

    [SerializeField] CameraController cameraController;
    [Inject] GameController gameController;
    [Inject] GameFieldSettings GameFieldSettings;
    [Inject] PlayerPlaceBuildingSystem playerPlaceBuildingSystem;
    [Inject] PlayerPlaceRoadSystem playerPlaceRoadSystem; 
    [Inject] PlayerDeleteBuildingsSystem playerDeleteBuildingsSystem; 
    [Inject] PlayerConnectionEnergySystem playerConnectionEnergySystem; 
    [Inject] GridUpdateSystem gridUpdateSystem; 
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
    InputAction spawn;
    
    RaycastHit hit;


    public int rotation {get;private set;}
    public Vector2Int pos {get;private set;}
    public bool isForce {get;private set;}

    public EnergyNode energyNode {get;private set;}

    public Vector3 posV3  {get;private set;}

    Action<bool,bool> PlaceDelegate;
    Action BackDelegate;
    Action<bool> RotateDelegate;

    bool isLoaded;
    Entity PlaceCommand;
    EntityManager entityManager;
    IDisposable disposUI;
    bool UiState;
    Color selectColor;
    BuildingOnScene cachedBuilding;
    PlayerState playerState;
    public void Initialize(PlayerPlaceBuildingSystem buildSystem, PlayerPlaceRoadSystem roadSystem,PlayerDeleteBuildingsSystem deleteBuildingsSystem,PlayerConnectionEnergySystem connSystem,GridUpdateSystem gridSystem)
    {
        playerPlaceBuildingSystem = buildSystem;
        playerPlaceRoadSystem = roadSystem;
        playerDeleteBuildingsSystem=deleteBuildingsSystem;
        playerConnectionEnergySystem=connSystem;
        gridUpdateSystem=gridSystem;
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
        entityManager.AddComponent<PlayerDeletePoints>(PlaceCommand);
        entityManager.AddComponent<PathfindingRequest>(PlaceCommand);
        entityManager.AddComponent<PlayerConnectBuildings>(PlaceCommand);
        

        entityManager.SetComponentEnabled<PathfindingRequest>(PlaceCommand,false);
        entityManager.SetComponentEnabled<PlayerPlacingRoad>(PlaceCommand,false);
        entityManager.SetComponentEnabled<PlayerPlacingBuilding>(PlaceCommand,false);
        entityManager.SetComponentEnabled<PlayerDeletePoints>(PlaceCommand,false);
        entityManager.SetComponentEnabled<PlayerConnectBuildings>(PlaceCommand,false);
        
        entityManager.AddBuffer<MapPoint>(PlaceCommand); 
        gridVisualizer.Init();
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
        spawn = GamePlay.FindAction("Spawn");
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
        spawn.performed+=Spawn;
        
        gridUpdateSystem.SetUpGrid(gridVisualizer,this,FlowFieldVisualizer);
    }
    void Spawn(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
           gameController.SpawnMobs();
        }
    }

    void Update()
    {
        if (!isLoaded) return;
        if (cachedBuilding == null) cachedBuilding = null; 
        if (energyNode == null) energyNode = null; 
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // 1. Логика координат (земля)
        if (Physics.Raycast(ray, out hit, Mathf.Infinity,GroundMask))
        {
            pos = gameController.GetMapPos(hit.point);
            posV3=hit.point;
            isForce = ForceBuilding.IsPressed();
            if(playerState == PlayerState.Destroy&&playerDeleteBuildingsSystem.DeleteType==DeleteType.DeleteBuilding)
            {
                selectColor = isForce ? GameFieldSettings.forceDestoryBuidlingColor : GameFieldSettings.makeAsDemolitionBuidlingColor;
                cachedBuilding?.SetOutLine(selectColor); 
            }
        }
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, EnergyNodeMask)&&playerState==PlayerState.Energy)
        {
            if(!(playerState==PlayerState.Building||playerState == PlayerState.Destroy&&playerDeleteBuildingsSystem.DeleteType!=DeleteType.DeleteBuilding))
            {
                var node = hit.collider.GetComponent<EnergyNode>();
                if (node != null)
                {
                    
                    if (energyNode != node)
                    {
                        energyNode?.SetOutLine(null);
                        energyNode = node;
                        energyNode.SetOutLine(selectColor); 
                    }
                }
            }
        }
        else
        {
            if (energyNode != null)
            {
                energyNode.SetOutLine(null);
                energyNode = null;
            }
        }
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, BuildingMask))
        {
            if(!(playerState==PlayerState.Building||playerState == PlayerState.Destroy && playerDeleteBuildingsSystem.DeleteType != DeleteType.DeleteBuilding))
            {
                var building = hit.collider.GetComponent<BuildingOnScene>();
                posV3=hit.point;
                if (building != null)
                {
                    
                    if (cachedBuilding != building)
                    {
                        cachedBuilding?.SetOutLine(null);
                        cachedBuilding = building;
                        cachedBuilding.SetOutLine(selectColor); 
                    }
                }
            }
            else
            {
                if (cachedBuilding != null)
                {
                    cachedBuilding.SetOutLine(null);
                    cachedBuilding = null;
                }
            }
            
        }
        else
        {
            if (cachedBuilding != null)
            {
                cachedBuilding.SetOutLine(null);
                cachedBuilding = null;
            }
        }
        
        
    }

   
    public void SetUpAction(string info)
    {
        
        gridUpdateSystem.SetUpGrid(gridVisualizer,this,FlowFieldVisualizer);
        ClearAction();
        if(info.Contains("Delete"))
        {
            
            playerState=PlayerState.Destroy;
            playerDeleteBuildingsSystem.onBuildingDone -= SwitchToUIMode;
            playerDeleteBuildingsSystem.onBuildingDone += SwitchToUIMode;
            PlaceDelegate=playerDeleteBuildingsSystem.DeletePoints;
            BackDelegate=playerDeleteBuildingsSystem.Back;
            if (info == DeleteType.DeleteRoadPoints.ToString())
            {
                playerDeleteBuildingsSystem.SetUpDelete(DeleteType.DeleteRoadPoints,this,PlaceCommand);
            }
            else
            {
                playerDeleteBuildingsSystem.SetUpDelete(info==DeleteType.DeleteManyPoints.ToString()?
                DeleteType.DeleteManyPoints:DeleteType.DeleteBuilding,this,PlaceCommand);
            }
            
            entityManager.SetComponentEnabled<PlayerPlacingRoad>(PlaceCommand,false);
            entityManager.SetComponentEnabled<PlayerPlacingBuilding>(PlaceCommand,false);
            entityManager.SetComponentEnabled<PlayerDeletePoints>(PlaceCommand,true);
        }
        else if (info.Contains("Energy"))
        {
            playerState=PlayerState.Energy;
            playerConnectionEnergySystem.onActionDone -= SwitchToUIMode;
            playerConnectionEnergySystem.onActionDone += SwitchToUIMode;
            PlaceDelegate=playerConnectionEnergySystem.ConnectBuildings;
            BackDelegate=playerConnectionEnergySystem.Back;
            RotateDelegate=playerConnectionEnergySystem.Rotate;
             playerConnectionEnergySystem.SetUpBuilding(( ConnectType)Enum.Parse(typeof(ConnectType), info),this,PlaceCommand);
        }
        else
        {
            
            playerState=PlayerState.Building;
            if (info == "Road")
            {
                playerPlaceRoadSystem.onBuildingDone -= SwitchToUIMode;
                playerPlaceRoadSystem.onBuildingDone += SwitchToUIMode;
                PlaceDelegate=playerPlaceRoadSystem.PlaceRoad;
                BackDelegate=playerPlaceRoadSystem.Back;
                playerPlaceRoadSystem.SetUpBuilding(info.GetStableHashCode(),this,PlaceCommand);
                entityManager.SetComponentEnabled<PlayerPlacingRoad>(PlaceCommand,true);
                entityManager.SetComponentEnabled<PlayerPlacingBuilding>(PlaceCommand,false);
                entityManager.SetComponentEnabled<PlayerDeletePoints>(PlaceCommand,false);
                
            }
            else
            {
                playerPlaceBuildingSystem.onBuildingDone -= SwitchToUIMode;
                playerPlaceBuildingSystem.onBuildingDone += SwitchToUIMode;
                PlaceDelegate=playerPlaceBuildingSystem.PlaceBuilding;
                BackDelegate=playerPlaceBuildingSystem.Back;
                //неучитываыет коннектед
                playerPlaceBuildingSystem.SetUpBuilding(info.GetStableHashCode(),this,PlaceCommand);
                entityManager.SetComponentEnabled<PlayerPlacingRoad>(PlaceCommand,false);
                entityManager.SetComponentEnabled<PlayerPlacingBuilding>(PlaceCommand,true);
                entityManager.SetComponentEnabled<PlayerDeletePoints>(PlaceCommand,false);
                
            }
        }
        

        SwitchToBuildingMode();

        PlacePoint.performed += OnBuildingPlacePerformed;
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
        gridVisualizer.Clear();
        selectColor=GameFieldSettings.selectBuildingColor;
        playerState=PlayerState.UiMode;
        UiState=true;
        Building.Disable();
        PlacePoint.performed-= OnBuildingPlacePerformed;
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
            if (cachedBuilding != null)
            {
                OpenBuildingMenu(cachedBuilding.id);
                return;
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

    void OnBuildingPlacePerformed(InputAction.CallbackContext context)
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
            if (Hold.IsPressed())
            { 
                RotateDelegate?.Invoke(true);
            }
            else
            {
                rotation++;
                rotation%=8;
            }
        }
    }

    void ClearAction()
    {
        UIClick.performed -= OnUIClickPerformed;
        PlacePoint.performed-= OnBuildingPlacePerformed;
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
    enum PlayerState
    {
        UiMode,
        Building,
        Energy,
        Destroy
    }
   
}

public interface IPlayerConnectData:IPlaceBuildingPlayerData
{
    EnergyNode energyNode{get;}
    Vector3 posV3{get;}
}
public interface IPlaceBuildingPlayerData:IPlayerData
{
    int rotation{get;}
}
public interface IPlayerData
{
    Vector2Int pos{get;}
    bool isForce{get;}
}