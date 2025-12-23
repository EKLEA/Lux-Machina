using System;
using Unity.Entities;
using UnityEngine;
using Zenject;

public class UIManager : MonoBehaviour
{

    //создание фабрик и назначение окон, регулировка открытия окон
    [SerializeField] ButtonsHandler buttonsHandler;
    [SerializeField] BuildingManagementWindowView buildingManagementWindowView;
    
    public BuildingManagementWindowViewModel model{get;private set;}

    public void Initialize()
    {
        buttonsHandler.Initialize();
        buildingManagementWindowView.Initialize();
    }

    public void OpenWindow(UIScreen uIScreen)
    {
        uIScreen.Open();
    }
}
