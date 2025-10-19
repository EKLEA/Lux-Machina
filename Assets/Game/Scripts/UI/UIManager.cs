using System;
using UnityEngine;

public class UIManager:MonoBehaviour
{
    public event Action<string> OnInfoInvoked;
    public void InvokeInfo(string id)
    {
        OnInfoInvoked?.Invoke(id);
    }
}