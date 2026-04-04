using System;
using UniRx;
using Unity.Entities;
using Unity.VisualScripting;
using UnityEngine;
using Zenject;

public class UIManager : MonoBehaviour
{

    //создание фабрик и назначение окон, регулировка открытия окон
    [SerializeField] ButtonsHandler buttonsHandler;
    [SerializeField] GameObject GamePlayInterface;
    public PauseMenu pauseMenu;
    public TimeManagerUI timeManagerUI;
    [field: SerializeField] public BuildingManagmentWindowManager buildingManagmentWindowManager{get;private set;}
    
    public BuildingInfoViewModel model{get;private set;}
    public event Action onReturnToMenu;
    IDisposable disposable;
    public void Initialize()
    {
        buttonsHandler.Initialize();
        pauseMenu.Initialize();
        timeManagerUI.Initialize();
        buildingManagmentWindowManager.Initialize();
    }
    public void ShowPauseMenu(PauseMenuType pauseMenuType)
    {
        
        GamePlayInterface.SetActive(false);
        pauseMenu.SetUp(pauseMenuType);
        disposable?.Dispose();
        disposable=pauseMenu.isOpened.Subscribe(IsOpened =>
        {
            if(IsOpened==false) ClosePauseMenu();
        });
        pauseMenu.onReturnToMenu+=()=>onReturnToMenu?.Invoke();
    }
    public void ClosePauseMenu()
    {
        pauseMenu.Close();
        GamePlayInterface.SetActive(true);
        disposable?.Dispose();
    }

}
