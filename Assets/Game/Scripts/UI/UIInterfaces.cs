using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;


public abstract class UIScreen :MonoBehaviour, IInitializable
{
    public bool isOpened{ get; protected set; }
    public virtual void Close()
    {
        isOpened = false;
        gameObject.SetActive(false);
    }

    public virtual void Initialize()
    {
        
    }

    public virtual void Open()
    {
        if (isOpened) Close();
        else
        {
            isOpened = true;
            gameObject.SetActive(true);
        }
    }
}
public interface IDragableWindow: IDragHandler, IBeginDragHandler, IEndDragHandler
{
    
}