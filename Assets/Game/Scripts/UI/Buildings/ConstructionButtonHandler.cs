using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
[Serializable]
public class ConstructionButtonHandler : UIScreen
{
    [SerializeField] BuildingButtonsHolder BuildingButtonsHolder;
    [SerializeField] Transform buttonHolder;
    [Inject] Button ButtonPrefab;
    [Inject] IReadOnlyTypeBuildingButtonInfo typeBuildingButtonInfo;
    Dictionary<int, Button> typeBts;
    public Action<int> onBuildingSelected;
    public override void Initialize()
    {
        typeBts = new();
        foreach(var t in typeBuildingButtonInfo.TypeBuildingButtonConfig)
        {
            var bt = Instantiate(ButtonPrefab, buttonHolder);
            bt.image.sprite = typeBuildingButtonInfo.GetBuildingTypeBTSprite(t.Key);
            typeBts.Add(t.Key,bt );
        }
        BuildingButtonsHolder.Initialize();
        base.Initialize();

    }
    void InvokeData(int data) => onBuildingSelected?.Invoke(data);
    public override void Close()
    {
        BuildingButtonsHolder.Close();
        BuildingButtonsHolder.onBuildingSelected -= InvokeData;
        foreach (var bt in typeBts)
        {
            bt.Value.onClick.RemoveAllListeners();
            bt.Value.interactable = false;
        }
        base.Close();
    }
    void OpenBuildingButtonsHolder(int type)
    {
        BuildingButtonsHolder.SetUpByType(type);
        BuildingButtonsHolder.onBuildingSelected+=InvokeData;
    }
   
    public override void Open()
    {
        
        base.Open();
        foreach (var bt in typeBts)
        {
            AddButtonListener(bt.Key, bt.Value);
            bt.Value.interactable = true;
        }
    }

    private void AddButtonListener(int type, Button button)
    {
        button.onClick.RemoveAllListeners(); 
        button.onClick.AddListener(() => OpenBuildingButtonsHolder(type));
    }
}
