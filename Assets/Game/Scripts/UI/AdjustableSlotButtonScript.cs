
using UnityEngine;

using System;
using UnityEngine.UI;
using UniRx;
using TMPro;
using Zenject;
public class AdjustableSlotButtonScript :MonoBehaviour, IDisposable,IInitializable
{
    [Inject] IReadOnlyItemsInfo itemsInfo;
    [SerializeField]  Button UpBT;
    [SerializeField]  Button DownBT;
    [SerializeField]  Button MainBT;
    [SerializeField]  Image icon;
    [SerializeField]  TextMeshProUGUI valueText;
      string format;
     ReactiveProperty<(int ItemId, int amount, int Capacity)> slotData;
    
    public void Bind(ReactiveProperty<(int ItemId, int amount, int Capacity)> slotData,string Format="")
    {
        Clear();
        this.slotData=slotData;
        
        gameObject.SetActive(true);
        format= Format==""?"{0}/{1}":Format;
        icon.sprite=itemsInfo.GetItemSprite(slotData.Value.ItemId);
        valueText.text=string.Format(format,slotData.Value.amount.ToString(),slotData.Value.Capacity.ToString());
        UpBT.onClick.AddListener(()=>ChangeCapacity(5));
        DownBT.onClick.AddListener(()=>ChangeCapacity(-5));
        //добавить обработку по нажатию в центр кнопки
    }
    void Clear()
    {
        slotData=null;
        valueText.text="";
        UpBT.onClick.RemoveAllListeners();
        DownBT.onClick.RemoveAllListeners();
        
        gameObject.SetActive(false);
    }
    public void Dispose()
    {
       Clear();
    }

    void ChangeCapacity(int Value)
    {
        var (itemId, amount, capacity) = slotData.Value;
        slotData.Value = (itemId, amount, capacity+Value);
        valueText.text=string.Format(format,slotData.Value.amount.ToString(),slotData.Value.Capacity.ToString());
        
        //добавить логику на ремув итемс
    }

    public void Initialize()
    {
        
    }
}