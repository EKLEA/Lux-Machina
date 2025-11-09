using System;
using UnityEngine;
using Zenject;

public class UIManager : MonoBehaviour
{

    //создание фабрик и назначение окон, регулировка открытия окон
    [SerializeField] ButtonsHandler buttonsHandler;

    public void Initialize()
    {
        buttonsHandler.Initialize();
    }

    public void OpenWindow(UIScreen uIScreen)
    {
        uIScreen.Open();
    }
}
