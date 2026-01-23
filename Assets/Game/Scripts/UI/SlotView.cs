
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using Zenject;
using System;
using TMPro;

public class SlotView : MonoBehaviour,IDisposable,IInitializable
{
    [SerializeField] private Image icon;
    [SerializeField] private Image back;
    [SerializeField] private TextMeshProUGUI CountText;
    [Inject] IReadOnlyItemsInfo itemsInfo;
    CompositeDisposable disposable;
    public void Initialize()
    {
        icon.gameObject.SetActive(false);
        CountText.text = "";
        
    }
    public void Bind((int ItemId, IReadOnlyReactiveProperty<int> amount, ReactiveProperty<int> capacity) slotData)
    {
        
        if(disposable!=null) disposable.Dispose();
        disposable=new();
       
        slotData.amount.Subscribe(data =>
        {
            UpdateAmount(slotData.amount.Value,slotData.capacity.Value);
        }).AddTo(disposable);
        
        UpdateAllSlot(slotData.ItemId,slotData.amount.Value,slotData.capacity.Value);
        
        gameObject.SetActive(true);
    }
    
    void UpdateAmount(int amount,int capacity)
    {
        CountText.text = string.Format("{0}/{1}",amount.ToString(),capacity.ToString());
    }
    void UpdateAllSlot(int itemId,int amount,int capacity)
    {
        if (itemId > 0)
        {
            icon.sprite = itemsInfo.GetItemSprite(itemId);
            icon.gameObject.SetActive(true);
            CountText.text = string.Format("{0}/{1}",amount.ToString(),capacity.ToString());
        }
        else
        {
            icon.gameObject.SetActive(false);
            CountText.text = "";
        }
    }
    

    public void Dispose()
    {
        disposable?.Dispose();
        UpdateAllSlot(-1,0,0);
        gameObject.SetActive(false);
    }
}