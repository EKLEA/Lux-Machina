using System;
using UniRx;

using Zenject;
using UnityEngine;
using UnityEngine.EventSystems;


public abstract class UIScreen :MonoBehaviour, IInitializable,IDisposable
{
    public ReactiveProperty<bool> isOpened{ get; protected set; }
    public virtual void Close()
    {
        isOpened.Value = false;
        gameObject.SetActive(false);
    }

    public void Dispose()
    {
        isOpened=null;
    }
    void OnDestroy()
    {
        Dispose();
    }

    public virtual void Initialize()
    {
        isOpened=new ReactiveProperty<bool>(false);
    }

    public virtual void Open()
    {
        isOpened.Value = true;
        gameObject.SetActive(true);
    }
}


public interface IDragableWindow: IDragHandler, IBeginDragHandler, IEndDragHandler
{
    
}