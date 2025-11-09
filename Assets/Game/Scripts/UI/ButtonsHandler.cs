using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class ButtonsHandler : MonoBehaviour,IInitializable
{
    Dictionary<int, UIScreen> Screens;
    [SerializeField] ScreenPair[] Pairs;
    public void OpenWindow(int id)
    {
        Screens[id].Open();
    }

    public void Initialize()
    {
        Screens = new();
        Screens = Pairs.ToDictionary(f => f.key, f => f.UIScreen);
        foreach (var s in Pairs)
            s.UIScreen.Initialize();
    }
}
[Serializable]
public class ScreenPair
{
    public int key;
    public UIScreen UIScreen;
}

