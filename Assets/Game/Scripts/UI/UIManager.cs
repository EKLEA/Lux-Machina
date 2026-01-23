using System;
using UniRx;
using Unity.Entities;
using UnityEngine;
using Zenject;

public class UIManager : MonoBehaviour
{

    //создание фабрик и назначение окон, регулировка открытия окон
    [SerializeField] ButtonsHandler buttonsHandler;
    [field: SerializeField] public BuildingManagmentWindowManager buildingManagmentWindowManager{get;private set;}
    
    public BuildingInfoViewModel model{get;private set;}

    public void Initialize()
    {
        buttonsHandler.Initialize();
        buildingManagmentWindowManager.Initialize();
    }
}
