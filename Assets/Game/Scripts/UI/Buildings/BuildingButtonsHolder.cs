using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zenject;


public class BuildingButtonsHolder : UIScreen
{

    public Action<string> onBuildingSelected;
    [SerializeField] Transform buttonHolder;
    [Inject] Button ButtonPrefab;
    [Inject] IReadOnlyBuildingInfo buildingInfo;
    List<string> ids;
    Dictionary<int, Button> BTs;
    int prevType;
    int currType;
    Func<string, Sprite> GetSprite;
    
    public void SetUpByType(int type)
    {
        foreach (var bt in BTs.Values)
        {
            bt.onClick.RemoveAllListeners();
            bt.interactable = false;
            bt.gameObject.SetActive(false);
        }
        
        ids.Clear();
        if ((BuildingsTypes)type == BuildingsTypes.DeleteBuilding)
        {
            foreach(DeleteType v in Enum.GetValues(typeof(DeleteType)))
            {
                ids.Add(v.ToString());
            }
            
             GetSprite = (string name) => 
            {
                DeleteType enumValue = (DeleteType)Enum.Parse(typeof(DeleteType), name);
                
                return buildingInfo.GetEnumSprite<DeleteType>((int)enumValue);
            };
        }
        else if((BuildingsTypes)type == BuildingsTypes.ConnectBuilding)
        {
            foreach(ConnectType v in Enum.GetValues(typeof(ConnectType)))
            {
                ids.Add(v.ToString());
            }
            
           GetSprite = (string name) => 
            {
                ConnectType enumValue = (ConnectType)Enum.Parse(typeof(ConnectType), name);
                
                return buildingInfo.GetEnumSprite<ConnectType>((int)enumValue);
            };
        }
        else
        {
            var buildings = buildingInfo.BuildingInfos.Values.Where(f => f.buildingType == (BuildingsTypes)type).ToList();
            foreach(var b in buildings)
            {
                ids.Add(b.id);
            }
            GetSprite=(string id)=> buildingInfo.GetBuildingSprite(id.GetStableHashCode());
        }
       
        if (type != currType||!isOpened.Value)
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
        ids = new();
        
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
        if (ids.Count == 0)
        {
            Close();
            return;
        }
        else
        {
            
            isOpened.Value = false;
            base.Open();
            for (int i = 0; i < ids.Count; i++)
            {
                var bt = BTs[i];

                bt.image.sprite =GetSprite(ids[i]);
                AddButtonListener(ids[i], bt);
                bt.gameObject.SetActive(true);
                bt.interactable = true;
            }
        }
        
        
    }
    private void AddButtonListener(string id, Button button)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => InvokeMethod(id));
    }
    void InvokeMethod(string id)
    {
        onBuildingSelected?.Invoke(id);
        Close();
    }
    public override void Close()
    {
        GetSprite=null;
        ids.Clear();
        foreach (var bt in BTs.Values)
        {
            
            bt.onClick.RemoveAllListeners();
            bt.interactable = false;
            bt.gameObject.SetActive(false);
        }
        onBuildingSelected=null;
        base.Close();
    }
}