
using UnityEngine;

using System;
using UnityEngine.UI;
using UniRx;
using TMPro;
public class ToggleButtonScript :MonoBehaviour, IDisposable
{
    [SerializeField]  Button MainBT;
    [SerializeField] Image Back;
     ReactiveProperty<bool> IsActiveProperty;
     bool IsActive;
    
    public void Bind(ReactiveProperty<bool> Value)
    {
        Clear();
        gameObject.SetActive(true);
        IsActiveProperty=Value;
        IsActive=IsActiveProperty.Value;
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
        Back.color=new Color(Back.color.r,Back.color.g,Back.color.b,IsActive?1:0.5f);
    }
}