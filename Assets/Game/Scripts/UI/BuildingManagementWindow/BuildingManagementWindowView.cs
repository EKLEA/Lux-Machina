using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using Zenject;
using Unity.Entities;
using System.Linq;
using System.Collections;

public class BuildingManagementWindowView:DragableUIWindow
{
    
    [Inject] IReadOnlyBuildingInfo buildingInfo;
    [Inject] IReadOnlyRecipeInfo recipeInfo;
    [Inject] GameController gameController;
    [Header("Simple obj")]
    [SerializeField] Transform Header;
    [SerializeField] Image workIndicatorOrb;
    [SerializeField] TextMeshProUGUI workIndicatorText;
    [SerializeField] Button DestroyBT;
    [SerializeField] Image BuildingSprite;
    [SerializeField] TextMeshProUGUI BuildingText;
    [SerializeField] TextMeshProUGUI BuildingDescriptionText;
    [Header("Heads")]
    [SerializeField] Transform CraftArea;
    [SerializeField] Transform StorageArea;
    [SerializeField] Transform ConstructionArea;
    [SerializeField] Transform ExcessItemsArea;
    [SerializeField] RecipesAndItemsWindow RecipesAndItemsHead;

    [Header("CraftArea")]
    [SerializeField] Transform RecipeHead;
    [SerializeField] Transform CraftSlotsHead;
    [SerializeField] Button ChangeRecipeBT;
    [SerializeField] Transform ProgressBar;
    [SerializeField] Image ProgressFill;
    [SerializeField] Transform InputSlotsHolder;
    [SerializeField] Transform OutputSlotsHolder;
    [SerializeField] Image RecipeSprite;
    [SerializeField] Transform RecipeIndicator;
    [SerializeField] TextMeshProUGUI RecipeName;
    [SerializeField] SlotView[] InputSlots;
    [SerializeField] SlotView[] OutputSlots;
    [SerializeField] ToggleButtonScript ToggleInputBT;
    [SerializeField] ToggleButtonScript ToggleOutputBT;
    [SerializeField] AdjustableButtonScript PriorityBT;
    [SerializeField] AdjustableButtonScript CountOfPackBT;

    [Header("StorageArea")]
    [SerializeField] Transform StorageHead;
    [SerializeField] Transform StorageSlotsHead;
    [SerializeField] AdjustableSlotButtonScript[] StorageSlots;
    [SerializeField] Button AddSlotBT;

    [Header("EnergyArea")]
    [SerializeField] ToggleButtonScript ToggleSwitchBT;

    [Header("ConstructionArea")]
    [SerializeField] Transform ConstructionHead;
    [SerializeField] Transform ConstructionSlotsHead;
    [SerializeField] Transform InputConstructionSlotsHolder;
    [SerializeField] Transform OutputConstructionSlotsHolder;
    [SerializeField] ToggleButtonScript ToggleInputConstuctionBT;
    [SerializeField] ToggleButtonScript ToggleOutputConstuctionBT;
    [SerializeField] AdjustableButtonScript PriorityConstuctionBT;
    [SerializeField] Button ForceDestroyBT;
    [SerializeField] SlotView[] InputConstructionSlots;
    [SerializeField] SlotView[] OutputConstructionSlots;
    [Header("ExcessArea")]
    
    [SerializeField] Transform ExcessHead;
    [SerializeField] Transform ExcessSlotsHead;
    [SerializeField] SlotView[] ExcessItemsSlots;


    [SerializeField] Button addOne;
    [SerializeField] Button removeOne;
    
    BuildingInfoViewModel model;

    #region subscribes
    CompositeDisposable allDisposables;
    IDisposable workStateDispose;
    CompositeDisposable CraftAreaDispose;

    CompositeDisposable ConstructionAreaDispose;   
    CompositeDisposable StorageAreaDispose;

    CompositeDisposable ExcessAreaDispose;
    #endregion
    int fC;
    public BuildingViewData  buildingViewData{get;private set;}
    DistribuitionViewData distribuitionViewData;
    ReactiveProperty<SlotViewData[]> excessItems;
    ReactiveProperty<int> priority;
    ReactiveProperty<ConstructionViewData> constructionViewData;
    (bool,BuildingCraftViewData) recipeViewData;
    (ReactiveProperty<StorageSlotViewData[]>,int)storageSlots;
    bool windowChanged;
    bool CanDestory;
    ReactiveProperty<bool> SwitchData;
    public void BindModel(BuildingInfoViewModel model)
    {
        this.model=model;
        RecipesAndItemsHead.Initialize();
    }
    public void SetUpData(Entity entity)
    { 
        model.GetBuildingData(entity,out BuildingViewData buildingViewDataS,out constructionViewData,out excessItems,out distribuitionViewData,out priority,out recipeViewData,out storageSlots,out CanDestory,out SwitchData);
        buildingViewData=buildingViewDataS;
        Open();
    }
    public override void Open()
    {
        if (buildingViewData == null) 
        {
            Debug.Log(buildingViewData);
            Close();
            return;
        }
        
        ResetWindow();
        ShowBaseWindow();
        ShowCraftArea();
        ShowStorageArea();

        if(distribuitionViewData!=null||storageSlots.Item1!=null)
        {
            PriorityBT.Bind(priority);
            allDisposables.Add(PriorityBT);
        }
        ShowConstructionArea();
        //подписка метода для кнопки форса ForceDestroyBT.onClick.RemoveAllListeners();
        ShowExcessArea();
        if (buildingViewData.WorkState != null)
        {
            workStateDispose= buildingViewData.WorkState.Subscribe(state =>
            {
                UpdateState(state);
            });
        }
        fC=0;
        base.Open();
        
        StartCoroutine(DeferredResize());
    }
    void UpdateState(int state)
    {
        //model.GetStateInfo(state);
    }
    void ShowBaseWindow()
    {
        allDisposables?.Dispose();
        allDisposables=null;
        allDisposables=new();
        addOne.onClick.RemoveAllListeners();
        removeOne.onClick.RemoveAllListeners();
        model.tempUpdate+=()=>{Close();SetUpData(buildingViewData.buildingEntity);};
        addOne.onClick.AddListener(()=>model.AddAmount(1));
        removeOne.onClick.AddListener(()=>model.AddAmount(-1));
        BuildingSprite.sprite=buildingInfo.GetBuildingSprite(buildingViewData.buildingID);
        BuildingText.text=buildingInfo.BuildingInfos[buildingViewData.buildingID].title;
        BuildingDescriptionText.text=buildingInfo.BuildingInfos[buildingViewData.buildingID].description;
        DestroyBT.gameObject.SetActive(CanDestory);
        ToggleSwitchBT.gameObject.SetActive(SwitchData!=null);
        if (SwitchData != null)
        {
            
            ToggleSwitchBT.Bind(SwitchData);
            allDisposables.Add(ToggleSwitchBT);
        }
    }
    void ShowCraftArea()
    {
        if (distribuitionViewData != null)
        {
            CraftAreaDispose=new();
            CraftArea.gameObject.SetActive(true);
            RecipeHead.gameObject.SetActive(true);
            RecipeName.text="Выбор рецепта";
            RecipeIndicator.gameObject.SetActive(false);
            ChangeRecipeBT.onClick.AddListener(()=>
            {

                if(!recipeViewData.Item1&&RecipesAndItemsHead.isOpened.Value) return;

                if (recipeViewData.Item1&&recipeViewData.Item2.recipeIDHash!=-1)
                    model.SetRecipe( -1);
                
                windowChanged=true;
                CraftSlotsHead.gameObject.SetActive(false);
                RecipesAndItemsHead.SetUpWindowAsRecipesByRecipeGroup(
                    buildingInfo.BuildingProcessionInfos[buildingViewData.buildingID].requiredRecipesGroup.ToHashSet());
                UpdateWithResize();
            });     
            RecipesAndItemsHead.onItemChoosed += (value) => 
            {
                model.SetRecipe( value);
                windowChanged=true;
                RecipesAndItemsHead.Close();
            };
            
            if (recipeViewData.Item1)
            {
                CraftSlotsHead.gameObject.SetActive(true);
                if(distribuitionViewData.OutputSlots != null)
                {
                    OutputSlotsHolder.gameObject.SetActive(true);
                    ToggleOutputBT.Bind(distribuitionViewData.IsActiveOutput);
                    CraftAreaDispose.Add(ToggleOutputBT);

                    for(int i=0;i<distribuitionViewData.OutputSlots.Length;i++)
                    {
                        OutputSlots[i].Bind((distribuitionViewData.OutputSlots[i].ItemID.Value,distribuitionViewData.OutputSlots[i].Amount,distribuitionViewData.OutputSlots[i].Capacity));
                        CraftAreaDispose.Add(OutputSlots[i]);
                    }
                }

                if(distribuitionViewData.InputSlots!=null)
                {
                    InputSlotsHolder.gameObject.SetActive(true);
                    ToggleInputBT.Bind(distribuitionViewData.IsActiveInput);
                    CraftAreaDispose.Add(ToggleInputBT);
                    for(int i=0;i<distribuitionViewData.InputSlots.Length;i++)
                    {
                        InputSlots[i].Bind((distribuitionViewData.InputSlots[i].ItemID.Value,distribuitionViewData.InputSlots[i].Amount,distribuitionViewData.InputSlots[i].Capacity));
                        CraftAreaDispose.Add(InputSlots[i]);
                    }
                }
                RecipeIndicator.gameObject.SetActive(true);
                RecipeName.text=recipeInfo.RecipeInfos[recipeViewData.Item2.recipeIDHash].title;
                RecipeSprite.sprite=recipeInfo.GetRecipeSprite(recipeViewData.Item2.recipeIDHash);
                ProgressBar.gameObject.SetActive(true);
                recipeViewData.Item2.CurrTime.Subscribe((value) =>
                {
                    
                    ProgressFill.fillAmount=value/recipeViewData.Item2.TimeToCraft.Value;
                }).AddTo(CraftAreaDispose);
                CountOfPackBT.Bind(recipeViewData.Item2.CountInPack);
                CraftAreaDispose.Add(CountOfPackBT);
            }
            else
            {
                RecipesAndItemsHead.SetUpWindowAsRecipesByRecipeGroup(
                buildingInfo.BuildingProcessionInfos[buildingViewData.buildingID].requiredRecipesGroup.ToHashSet());
            }
            allDisposables.Add(CraftAreaDispose);
        }
    }
    void ShowStorageArea()
    { 
        if (storageSlots.Item1 != null&&storageSlots.Item1 .Value!=null&&distribuitionViewData==null)
        {
            AddSlotBT.onClick.AddListener(()=>
            {
                if(RecipesAndItemsHead.isOpened.Value) RecipesAndItemsHead.Close();
                else
                    RecipesAndItemsHead.SetUpWindowAsItemsByItemClasses(
                        buildingInfo.BuildingStorageInfos[buildingViewData.buildingID].ItemsTypes.Count>0?
                        buildingInfo.BuildingStorageInfos[buildingViewData.buildingID].ItemsTypes.ToHashSet():
                        Enum.GetValues(typeof(ItemType)).Cast<ItemType>().Where(f=>f!=ItemType.None)
                                 .ToHashSet());
                
                UpdateWithResize();
            });

            RecipesAndItemsHead.onItemChoosed += (value) => 
            {
                model.AddStorageSlot( value, 5);
                windowChanged=true;
                AddSlotBT.transform.SetAsLastSibling();
                RecipesAndItemsHead.Close();
                
            }; 
            StorageAreaDispose=new();
            StorageArea.gameObject.SetActive(true);
            StorageHead.gameObject.SetActive(true);
            StorageSlotsHead.gameObject.SetActive(true);
            storageSlots.Item1 .Subscribe(value =>
            {
                UpdateStorageSlots(value,storageSlots.Item2);
            }).AddTo(StorageAreaDispose);
            Debug.Log(storageSlots.Item2);
            UpdateStorageSlots(storageSlots.Item1.Value,storageSlots.Item2);
            allDisposables.Add(StorageAreaDispose);
        }
    }
    void ShowConstructionArea()
    {
        if (constructionViewData != null)
        {
            
            ConstructionAreaDispose=new();
            constructionViewData.Subscribe(value =>
            {
                UpdateConstuctionSlots(value);
            }).AddTo(allDisposables);
            
            UpdateConstuctionSlots(constructionViewData.Value);
            allDisposables.Add(ConstructionAreaDispose);
        }
    } 
    void ShowExcessArea()
    { 
        if (excessItems == null)
        {
            
            ExcessHead.gameObject.SetActive(false);
            ExcessSlotsHead.gameObject.SetActive(false);
            ExcessItemsArea.gameObject.SetActive(false);
            return;
        }
        else
        {
            
            ExcessAreaDispose=new();
            excessItems.Subscribe(value =>
            {
                UpdateExcessSlots(value);
            }).AddTo(ExcessAreaDispose);
            UpdateExcessSlots(excessItems.Value);
        }
        allDisposables.Add(ExcessAreaDispose);
    }
    void UpdateConstuctionSlots(ConstructionViewData constructionViewData)
    {
    
        foreach(var s in InputConstructionSlots)
        {
            if (s.gameObject.activeInHierarchy)
            {
                ConstructionAreaDispose.Remove(s);
                s.Dispose();
            }
        }
        foreach(var s in OutputConstructionSlots)
        {
            if (s.gameObject.activeInHierarchy)
            {
                ConstructionAreaDispose.Remove(s);
                s.Dispose();
            }
        }
        ToggleOutputConstuctionBT.Dispose();
        ToggleInputConstuctionBT.Dispose();
        bool shouldShow=false;
        if(constructionViewData.OutputConstructionSlots != null&&constructionViewData.OutputConstructionSlots.Length>0)
        {
            ToggleOutputConstuctionBT.Bind(constructionViewData.IsActiveConstructionOutput);
            ConstructionAreaDispose.Add(ToggleOutputConstuctionBT);
            
            for(int i=0;i<constructionViewData.OutputConstructionSlots.Length;i++)
            {
                OutputConstructionSlots[i].Bind((constructionViewData.OutputConstructionSlots[i].ItemID.Value,
                                    constructionViewData.OutputConstructionSlots[i].Amount,
                                    constructionViewData.OutputConstructionSlots[i].Capacity));
                ConstructionAreaDispose.Add(OutputConstructionSlots[i]);
            }
            shouldShow=true;
        }
        if(constructionViewData.InputConstructionSlots!=null&&constructionViewData.InputConstructionSlots.Length > 0)
        {
            ToggleInputConstuctionBT.Bind(constructionViewData.IsActiveConstructionInput);
            ConstructionAreaDispose.Add(ToggleInputConstuctionBT);

            for(int i=0;i<constructionViewData.InputConstructionSlots.Length;i++)
            {
                InputConstructionSlots[i].Bind((constructionViewData.InputConstructionSlots[i].ItemID.Value,
                                    constructionViewData.InputConstructionSlots[i].Amount,
                                    constructionViewData.InputConstructionSlots[i].Capacity));
                ConstructionAreaDispose.Add(InputConstructionSlots[i]);
            }
            shouldShow=true;
        }
         
        if (shouldShow)
        {
            
            ConstructionArea.gameObject.SetActive(true);
            ConstructionSlotsHead.gameObject.SetActive(true);
            ConstructionHead.gameObject.SetActive(true);
            
            InputConstructionSlotsHolder.gameObject.SetActive(true);
            OutputConstructionSlotsHolder.gameObject.SetActive(true);
            PriorityConstuctionBT.Bind(constructionViewData.ConstructionPriority);
            ConstructionAreaDispose.Add(PriorityConstuctionBT);
        }
        else
        {
            ConstructionArea.gameObject.SetActive(false);
            ConstructionSlotsHead.gameObject.SetActive(false);
            ConstructionHead.gameObject.SetActive(false);
            //ConstructionAreaDispose.Dispose();
        }
    }
   
    void UpdateExcessSlots(SlotViewData[] slots)
    {
        foreach(var s in ExcessItemsSlots)
        {
            if (s.gameObject.activeInHierarchy)
            {
                ExcessAreaDispose.Remove(s);
                s.Dispose();
            }
        }
        if (slots.Length > 0)
        {
            ExcessHead.gameObject.SetActive(true);
            ExcessSlotsHead.gameObject.SetActive(true);
            ExcessItemsArea.gameObject.SetActive(true);
        }
        else
        {
            ExcessHead.gameObject.SetActive(false);
            ExcessSlotsHead.gameObject.SetActive(false );
            ExcessItemsArea.gameObject.SetActive(false  );
        }
        for(int i = 0; i < slots.Length; i++)
        {
            
            int indexForLambda = i; 
            ExcessItemsSlots[indexForLambda].Bind((slots[indexForLambda].ItemID.Value,slots[indexForLambda].Amount,slots[indexForLambda].Capacity));
            ExcessAreaDispose.Add(ExcessItemsSlots[indexForLambda]);

        }
        
    }
    void UpdateStorageSlots(StorageSlotViewData[] slots,int maxLength)
    {
        foreach(var s in StorageSlots)
        {
            StorageAreaDispose.Remove(s);
            s.Dispose();
        }
        if (slots.Length == maxLength)
        {
            AddSlotBT.interactable=false;
            AddSlotBT.gameObject.SetActive(false);
        }
        else
        {
            AddSlotBT.interactable=true;
            AddSlotBT.gameObject.SetActive(true);
        }
        if(slots.Length>0)
        {
            for(int i = 0; i < slots.Length; i++)
            {
                int indexForLambda = i; 
                var slotUI = StorageSlots[i];

                // ВАЖНО: Сначала Bind, который внутри себя ВЫЗЫВАЕТ Clear() и обнуляет onSlotDeleted
                slotUI.Bind((
                    slots[i].ItemID.Value,
                    slots[i].Amount,
                    slots[i].Capacity,
                    slots[i].IsActiveInput,
                    slots[i].IsActiveOutput
                ),5,100);
                

                // Теперь подписываемся на ЧИСТОЕ событие. На кнопке будет ровно ОДИН обработчик.
                slotUI.onSlotDeleted += () =>
                {
                    model.RemoveStorageSlot(indexForLambda);
                };

                StorageAreaDispose.Add(slotUI);
            }
        }
        
    }
   
    
    void HideCraftArea()
    {
        CraftAreaDispose?.Dispose();
        CraftAreaDispose=null;
        RecipesAndItemsHead.Close();
        ChangeRecipeBT.onClick.RemoveAllListeners();
        RecipeHead.gameObject.SetActive(false);
        CraftSlotsHead.gameObject.SetActive(false);
        ProgressBar.gameObject.SetActive(false);
        InputSlotsHolder.gameObject.SetActive(false);
        OutputSlotsHolder.gameObject.SetActive(false);
        CraftArea.gameObject.SetActive(false);
    }  
    void HideStorageArea()
    {
        StorageAreaDispose?.Dispose();
        StorageAreaDispose=null;
        StorageHead.gameObject.SetActive(false);
        StorageSlotsHead.gameObject.SetActive(false);
        AddSlotBT.onClick.RemoveAllListeners();
        RecipesAndItemsHead.Close();
        StorageArea.gameObject.SetActive(false);
    }  
    void HideConstructionArea()
    {
        ConstructionAreaDispose?.Dispose();
        ConstructionAreaDispose=null;
        ConstructionHead.gameObject.SetActive(false);
        ConstructionSlotsHead.gameObject.SetActive(false);
        //ForceDestroyBT.onClick.RemoveAllListeners();
        ConstructionArea.gameObject.SetActive(false);
    }  
    void HideExcessArea()
    {
        ExcessAreaDispose?.Dispose();
        ExcessAreaDispose=null;
        ExcessHead.gameObject.SetActive(false);
        ExcessSlotsHead.gameObject.SetActive(false);
        ExcessItemsArea.gameObject.SetActive(false);
    }
    void ResetWindow()
    {
        
        RecipesAndItemsHead.Close();
        workStateDispose?.Dispose();
        workStateDispose=null;
        allDisposables?.Dispose();
        allDisposables=null;
        
        ToggleSwitchBT.gameObject.SetActive(false);  
        HideCraftArea();
        HideStorageArea();
        HideConstructionArea();
        HideExcessArea();
    }
    public override void Close()
    {
        ResetWindow();
        base.Close();
    }
    
    IEnumerator DeferredResize()
    {
        yield return new WaitForEndOfFrame(); 
        UpdateWithResize(); 
    }
   private bool _pendingSetup = false;
    public void UpdateView()
    {
        
        if(model == null || !isOpened.Value || buildingViewData == null) return;
        
        if (windowChanged)
        {
            windowChanged = false;
            
            _pendingSetup = true; 
            return; 
        }

        if (_pendingSetup)
        {
            SetUpData(buildingViewData.buildingEntity);
            _pendingSetup = false;
            return; 
        }

        if (fC % 4 == 0)
        {
            if(gameController.GetEntity(buildingViewData.buildingID,out var en)&&en!=buildingViewData.buildingEntity) Close();
            
            model.FixedUpdate();
            fC = 0;
        }
        else fC++;
    }
}