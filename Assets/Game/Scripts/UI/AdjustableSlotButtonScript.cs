
using UnityEngine;

using System;
using UnityEngine.UI;
using UniRx;
using TMPro;
using Zenject;
using Unity.Mathematics;
using Unity.Entities.UniversalDelegates;
public class AdjustableSlotButtonScript :MonoBehaviour, IDisposable,IInitializable
{
    [Inject] IReadOnlyItemsInfo itemsInfo;
    [SerializeField]  Button UpBT;
    [SerializeField]  Button DownBT;
    [SerializeField]  Button MainBT;
    [SerializeField]  Button CloseBT;
    [SerializeField]  Image icon;
    [SerializeField]  TextMeshProUGUI valueText;
    public event Action onSlotDeleted;

    string format;
    int state;
     int _min,_max;
    (int ItemId, 
                        IReadOnlyReactiveProperty<int> amount, 
                        ReactiveProperty<int> capacity, 
                        ReactiveProperty<bool> IsInputEnabled,
                        ReactiveProperty<bool> IsOutputEnabled) slotData;
    CompositeDisposable disposable;
    public void Bind((int ItemId, 
                        IReadOnlyReactiveProperty<int> amount, 
                        ReactiveProperty<int> capacity, 
                        ReactiveProperty<bool> IsInputEnabled,
                        ReactiveProperty<bool> IsOutputEnabled) slotData,int min=1,int max=5,string Format="")
    {
        Clear();
        // state=0;
        _min=min;
        _max=max;
        this.slotData=slotData;
        disposable=new();
        format= Format==""?"{0}/{1}":Format;

        MainBT.onClick.AddListener(ChangeStateAndNotify);
        CloseBT.onClick.AddListener(() =>
        {
            onSlotDeleted?.Invoke();
            Dispose();
        });
        slotData.amount.Subscribe(data =>
        {
            UpdateAmount(slotData.amount.Value,slotData.capacity.Value);
        }).AddTo(disposable);
        
        bool currentIn = slotData.IsInputEnabled.Value;
        bool currentOut = slotData.IsOutputEnabled.Value;

        if (currentIn && currentOut) state = 0;
        else if (currentIn && !currentOut) state = 1;
        else if (!currentIn && currentOut) state = 2;
        else state = 3; 

        ApplyVisualState(false);
        UpdateAllSlot(slotData.ItemId,slotData.amount.Value,slotData.capacity.Value);
        UpBT.onClick.AddListener(()=>ChangeCapacity(5));
        DownBT.onClick.AddListener(()=>ChangeCapacity(-5));
        
        gameObject.SetActive(true);
    }
    void Clear()
    {
        
        if(disposable!=null) disposable.Dispose();
        valueText.text="";
        onSlotDeleted=null;
        UpBT.onClick.RemoveAllListeners();
        DownBT.onClick.RemoveAllListeners();
        MainBT.onClick.RemoveAllListeners();
        CloseBT.onClick.RemoveAllListeners();
        gameObject.SetActive(false);
    }
    void ChangeStateAndNotify()
{
        state = (state + 1) % 4;
        ApplyVisualState(true);
    }

    void ApplyVisualState(bool userChange)
    {
        switch (state)
        {
            case 0: // Оба включены
                if (userChange)
                {
                    slotData.IsInputEnabled.Value = true;
                    slotData.IsOutputEnabled.Value = true;
                }
                icon.color = new Color(0, 0.8f, 0, 0.5f);
                break;
            case 1:
                if (userChange)
                {
                    slotData.IsInputEnabled.Value = true;
                    slotData.IsOutputEnabled.Value = false;
                }
                icon.color = new Color(1, 0.5647f, 0, 0.5f);
                break;
            case 2:
                if (userChange)
                {
                    slotData.IsInputEnabled.Value = false;
                    slotData.IsOutputEnabled.Value = true;
                }
                icon.color = new Color(0.1f, 0.11f, 0.7f, 0.5f);
                break;
            case 3: // Оба выключены
                if (userChange)
                {
                    slotData.IsInputEnabled.Value = false;
                    slotData.IsOutputEnabled.Value = false;
                }
                icon.color = new Color(0, 0, 0, 0.5f);
                break;
        }
    }

    public void Dispose()
    {
       Clear();
    }
    void UpdateAmount(int amount,int capacity)
    {
        valueText.text = string.Format(format,amount.ToString(),capacity.ToString());
    }
    void UpdateAllSlot(int itemId,int amount,int capacity)
    {
        if (itemId > 0)
        {
            icon.sprite = itemsInfo.GetItemSprite(itemId);
            icon.gameObject.SetActive(true);
            valueText.text = string.Format(format,amount.ToString(),capacity.ToString());
        }
        else
        {
            icon.gameObject.SetActive(false);
            valueText.text = "";
        }
    }
    void ChangeCapacity(int Value)
    {
        int next = slotData.capacity.Value + Value;

        if (next > _max)
            slotData.capacity.Value = _min;
        else if (next < _min)
            slotData.capacity.Value = _max;
        else
            slotData.capacity.Value = next;

        UpdateAmount(slotData.amount.Value, slotData.capacity.Value);
    }

    public void Initialize()
    {
        
    }
}