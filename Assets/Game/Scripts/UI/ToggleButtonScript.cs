
using UnityEngine;

using System;
using UnityEngine.UI;
using UniRx;
using TMPro;
public class ToggleButtonScript :MonoBehaviour, IDisposable
{
    [SerializeField]  Button MainBT;
    [SerializeField] Image icon;
    ReactiveProperty<bool> IsActiveProperty;
    bool IsActive;
    
    public void Bind(ReactiveProperty<bool> Value)
    {
        Clear();
        gameObject.SetActive(true);
        IsActiveProperty=Value;
        IsActive=IsActiveProperty.Value;
        icon.color=new Color(icon.color.r,icon.color.g,icon.color.b,IsActive?1:0.5f);
        MainBT.onClick.AddListener(()=>ChangeValue());
        //добавить обработку по нажатию в центр кнопки
    }
    void Clear()
    {
        gameObject.SetActive(false);
        IsActiveProperty=null;
         MainBT.onClick.RemoveAllListeners();
    }
    public void Dispose()
    {
       Clear();
    }

    void ChangeValue()
    {
        IsActive=!IsActive;
        IsActiveProperty.Value=IsActive;
        icon.color=new Color(icon.color.r,icon.color.g,icon.color.b,IsActive?1:0.5f);
    }
}