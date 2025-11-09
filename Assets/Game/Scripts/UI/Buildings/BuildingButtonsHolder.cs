using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zenject;


public class BuildingButtonsHolder : UIScreen
{

    public Action<int> onBuildingSelected;
    [SerializeField] Transform buttonHolder;
    [Inject] Button ButtonPrefab;
    [Inject] IReadOnlyBuildingInfo buildingInfo;
    List<BuildingConfig> buildings;
    Dictionary<int, Button> BTs;
    int prevType;
    int currType;
    public void SetUpByType(int type)
    {
        buildings.Clear();
        buildings = buildingInfo.BuildingInfos.Values.Where(f => f.buildingType == (BuildingsTypes)type).ToList();
        if (type != currType||!isOpened)
        {
            prevType = currType;
            currType = type;
            Open();
        }
        else Close();
    }
    public override void Initialize()
    {
        prevType = -2;
        currType = -1;
        BTs = new();
        buildings = new();
        for (int i = 0; i < 20; i++)
        {
            var bt = Instantiate(ButtonPrefab, buttonHolder);
            bt.interactable = false;
            bt.gameObject.SetActive(false);
            BTs.Add(i, bt);
        }
        base.Initialize();
    }
    public override void Open()
    {
        if (buildings.Count == 0)
        {
            Close();
            return;
        }
        else
        {
            
            isOpened = false;
            base.Open();
            for (int i = 0; i < buildings.Count; i++)
            {
                var bt = BTs[i];
                var buildingConfig = buildings[i];
                var buildingId = buildingConfig.id.GetStableHashCode();

                bt.image.sprite = buildingInfo.GetBuildingSprite(buildingId);
                AddButtonListener(buildingId, bt);
                bt.gameObject.SetActive(true);
                bt.interactable = true;
            }
        }
        
        
    }
    private void AddButtonListener(int id, Button button)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => InvokeMethod(id));
    }
    void InvokeMethod(int id)
    {
        onBuildingSelected?.Invoke(id);
        Close();
    }
    public override void Close()
    {
        foreach (var bt in BTs.Values)
        {
            bt.onClick.RemoveAllListeners();
            bt.interactable = false;
            bt.gameObject.SetActive(false);
        }
        base.Close();
    }
}