using System;
using System.Collections.Generic;
using NUnit.Framework;
using UniRx;
using Unity.Collections;
using Unity.Entities;
using Unity.VisualScripting;
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
    [SerializeField] AttackZoneVisualizer attackZoneVisualizer;

    [SerializeField] CameraController cameraController;
    [Inject] GameController gameController;
    [Inject] GameFieldSettings GameFieldSettings;
    [Inject] IReadOnlyBuildingInfo buildingInfo;
    [Inject] PlayerPlaceBuildingSystem playerPlaceBuildingSystem;
    [Inject] PlayerPlaceManyPointSystem playerPlaceRoadSystem; 
    [Inject] PlayerDeleteBuildingsSystem playerDeleteBuildingsSystem; 
    [Inject] PlayerConnectionEnergySystem playerConnectionEnergySystem; 
    [Inject] GridUpdateSystem gridUpdateSystem; 
    InputActionMap GamePlay;
    InputActionMap Menu;
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

    //gamePlay actions
    InputAction Pause;
    //menu
    InputAction Escape;
    
    RaycastHit hit;



    public int rotation {get;private set;}
    public Vector3 posV3 {get;private set;}
    public bool isForce {get;private set;}

    public EnergyNode energyNode {get;private set;}


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
    IDisposable menuSub;
    private void OnPauseNormal(InputAction.CallbackContext context) => OnPausePerformed(context, false);
    private void OnPauseEscape(InputAction.CallbackContext context) => OnPausePerformed(context, true);

    public void Initialize(PlayerPlaceBuildingSystem buildSystem, PlayerPlaceManyPointSystem roadSystem,PlayerDeleteBuildingsSystem deleteBuildingsSystem,PlayerConnectionEnergySystem connSystem,GridUpdateSystem gridSystem)
    {
        playerPlaceBuildingSystem = buildSystem;
        playerPlaceRoadSystem = roadSystem;
        playerDeleteBuildingsSystem=deleteBuildingsSystem;
        playerConnectionEnergySystem=connSystem;
        gridUpdateSystem=gridSystem;
        SetUp();
        isLoaded = true;
        entityManager= World.DefaultGameObjectInjectionWorld.EntityManager;
        PlaceCommand=entityManager.CreateEntity();
       

        entityManager.AddComponent<PlayerCommand>(PlaceCommand);
        entityManager.AddComponent<PlayerPlacingBuilding>(PlaceCommand);
        entityManager.AddComponent<PlayerPlacingManyPointBuilding>(PlaceCommand);
        entityManager.AddComponent<PlayerDeletePoints>(PlaceCommand);
        entityManager.AddComponent<PathfindingRequest>(PlaceCommand);
        entityManager.AddComponent<PlayerConnectBuildings>(PlaceCommand);
        

        entityManager.SetComponentEnabled<PathfindingRequest>(PlaceCommand,false);
        entityManager.SetComponentEnabled<PlayerPlacingManyPointBuilding>(PlaceCommand,false);
        entityManager.SetComponentEnabled<PlayerPlacingBuilding>(PlaceCommand,false);
        entityManager.SetComponentEnabled<PlayerDeletePoints>(PlaceCommand,false);
        entityManager.SetComponentEnabled<PlayerConnectBuildings>(PlaceCommand,false);
        
        entityManager.AddBuffer<MapPoint>(PlaceCommand); 
        gridVisualizer.Init();
    }
    void OnDestroy() 
    {
        Dispose();
        
    }
    void BindMaps()
    {
        GamePlay = playerInput.FindActionMap("GamePlay");
        UI = playerInput.FindActionMap("UI");
        Building = playerInput.FindActionMap("Building");
        Menu = playerInput.FindActionMap("Menu");
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
    void BindGamePlayActions()
    {
        Pause=GamePlay.FindAction("Pause");
        Escape=Menu.FindAction("Escape");
    }

    void SetUp()
    {
        BindMaps();
        BindUIActions();
        BindBuildingActions();
        BindGamePlayActions();
        GamePlay.Enable(); 
        Menu.Enable(); 
        Building.Disable();
        UI.Disable(); 
        SwitchToUIMode();

        handler.onBuildingSelected += SetUpAction;
        
        Pause.performed += OnPauseNormal;
        Escape.performed += OnPauseEscape;
        gridUpdateSystem.SetUpGrid(gridVisualizer,this,FlowFieldVisualizer,attackZoneVisualizer);//
        menuSub = manager.pauseMenu.isOpened.Subscribe(value =>
        {
            if(value)
                GamePlay.Disable();
            else GamePlay.Enable();
        });
    }

    void Update()
    {
        if (!isLoaded) return;
        if (cachedBuilding == null) cachedBuilding = null; 
        if (energyNode == null) energyNode = null; 
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        isForce = ForceBuilding.IsPressed();
            if(playerState == PlayerState.Destroy&&playerDeleteBuildingsSystem.DeleteType==DeleteType.DeleteBuilding)
            {
                selectColor = isForce ? GameFieldSettings.forceDestoryBuidlingColor : GameFieldSettings.makeAsDemolitionBuidlingColor;
                cachedBuilding?.SetOutLine(selectColor); 
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
        
        gridUpdateSystem.SetUpGrid(gridVisualizer,this,FlowFieldVisualizer,attackZoneVisualizer);//,attackZoneVisualizer
        ClearAction();
        if(info.Contains("Delete"))
        {
            
            playerState=PlayerState.Destroy;
            playerDeleteBuildingsSystem.onBuildingDone -= SwitchToUIMode;
            playerDeleteBuildingsSystem.onBuildingDone += SwitchToUIMode;
            PlaceDelegate=playerDeleteBuildingsSystem.DeletePoints;
            BackDelegate=playerDeleteBuildingsSystem.Back;
            if (info == DeleteType.DeleteManyPointBuilding.ToString())
            {
                playerDeleteBuildingsSystem.SetUpDelete(DeleteType.DeleteManyPointBuilding,this,PlaceCommand);
                RotateDelegate=playerDeleteBuildingsSystem.Rotate;
            }
            else
            {
                playerDeleteBuildingsSystem.SetUpDelete(info==DeleteType.DeleteManyPoints.ToString()?
                DeleteType.DeleteManyPoints:DeleteType.DeleteBuilding,this,PlaceCommand);
            }
            
            entityManager.SetComponentEnabled<PlayerPlacingManyPointBuilding>(PlaceCommand,false);
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
            if (buildingInfo.BuildingInfos.TryGetValue(info.GetStableHashCode(),out var val)&&val.actionType==ActionType.TwoPointBuilding)
            {
                playerPlaceRoadSystem.onBuildingDone -= SwitchToUIMode;
                playerPlaceRoadSystem.onBuildingDone += SwitchToUIMode;
                PlaceDelegate=playerPlaceRoadSystem.PlaceManyPoint;
                BackDelegate=playerPlaceRoadSystem.Back;
                
                RotateDelegate=playerPlaceRoadSystem.Rotate;
                playerPlaceRoadSystem.SetUpBuilding(info.GetStableHashCode(),this,PlaceCommand);
                entityManager.SetComponentEnabled<PlayerPlacingManyPointBuilding>(PlaceCommand,true);
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
                entityManager.SetComponentEnabled<PlayerPlacingManyPointBuilding>(PlaceCommand,false);
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
        RotateDelegate=null;
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
    void OnPausePerformed(InputAction.CallbackContext context, bool isEscapeMenu)
    {
        if (!context.performed) return;
        if (isEscapeMenu)
        {
            if (manager.pauseMenu.isOpened.Value)
            {
                manager.ClosePauseMenu();
                gameController.SetPause(false); 
            }
            else
            {
                manager.ShowPauseMenu(PauseMenuType.pause);
                gameController.SetPause(true);  
            }
        }
        else
        {
             gameController.TogglePause();
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
                rotation%=4;
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

    

    public void Dispose()
    {
        ClearAction();
        Pause.performed -= OnPauseNormal;
        Escape.performed -= OnPauseEscape;

        UIClick.Dispose();
        PlacePoint.Dispose();
        Back.Dispose();
        RotateBuilding.Dispose();
        Pause.Dispose();
        Escape.Dispose();

        menuSub?.Dispose();
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
}
public interface IPlaceBuildingPlayerData:IPlayerData
{
    int rotation{get;}
    Vector3 posV3{get;}
}
public interface IPlayerData
{
    bool isForce{get;}
}