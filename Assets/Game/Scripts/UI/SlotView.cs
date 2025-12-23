
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
    
    IDisposable disposable;
    
    public void Initialize()
    {
        icon.gameObject.SetActive(false);
        CountText.text = "";
    }

    public void Bind(IReadOnlyReactiveProperty<(int ItemId, int amount, int Capacity)> slotData)
    {
        if(disposable!=null) disposable.Dispose();
        disposable=slotData.Subscribe(data =>
        {
            UpdateView(data.ItemId, data.amount, data.Capacity);
        });
        
        gameObject.SetActive(true);
        UpdateView(slotData.Value.ItemId, slotData.Value.amount, slotData.Value.Capacity);
    }
    
    private void UpdateView(int itemId, int amount, int capacity)
    {
        if (itemId > 0)
        {
            icon.sprite = GetSprite(itemId);
            icon.gameObject.SetActive(true);
            CountText.text = string.Format("{0}/{1}",amount.ToString(),capacity.ToString());
        }
        else
        {
            icon.gameObject.SetActive(false);
            CountText.text = "";
        }
    }
    
    private Sprite GetSprite(int itemId) => itemsInfo.GetItemSprite(itemId);

    public void Dispose()
    {
        disposable.Dispose();
        UpdateView(-1,0,0);
        gameObject.SetActive(false);
    }
}