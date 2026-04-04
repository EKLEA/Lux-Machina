using System;
using UnityEngine;
using Zenject;

public class PauseMenu : UIScreen
{
    public event Action onReturnToMenu;
    public void SetUp(PauseMenuType type)
    {
        onReturnToMenu=null;
        Open();
    }
    public void GoToMenu()
    {
        onReturnToMenu?.Invoke();
    }
}
public enum PauseMenuType
{
    pause,
    gameOver
}