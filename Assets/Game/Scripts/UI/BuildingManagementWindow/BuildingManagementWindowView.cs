using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using Zenject;
using Unity.Entities;
using System.Linq;

public class BuildingManagementWindowView:DragableUIWindow
{
    
    [Inject] IReadOnlyBuildingInfo buildingInfo;
    [Inject] IReadOnlyRecipeInfo recipeInfo;
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
    ConstructionViewData constructionViewData;
    (bool,BuildingCraftViewData) recipeViewData;
     ReactiveProperty<StorageSlotViewData[]> storageSlots;
    bool windowChanged;
    public void BindModel(BuildingInfoViewModel model)
    {
        this.model=model;
        RecipesAndItemsHead.Initialize();
    }
    public void SetUpData(Entity entity)
    { 
        model.GetBuildingData(entity,out BuildingViewData buildingViewDataS,out priority,out distribuitionViewData,out excessItems,out constructionViewData,out recipeViewData,out storageSlots);
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
        if(distribuitionViewData!=null||storageSlots!=null)
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
        BuildingSprite.sprite=buildingInfo.GetBuildingSprite(buildingViewData.buildingID);
        BuildingText.text=buildingInfo.BuildingInfos[buildingViewData.buildingID].title;
        BuildingDescriptionText.text=buildingInfo.BuildingInfos[buildingViewData.buildingID].description;
        DestroyBT.gameObject.SetActive(true);
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
                    model.SetRecipe(buildingViewData.buildingEntity, -1);
                
                windowChanged=true;
                CraftSlotsHead.gameObject.SetActive(false);
                RecipesAndItemsHead.SetUpWindowAsRecipesByRecipeGroup(
                    buildingInfo.BuildingProcessionInfos[buildingViewData.buildingID].requiredRecipesGroup.ToHashSet());
            });     
            RecipesAndItemsHead.onItemChoosed += (value) => 
            {
                model.SetRecipe(buildingViewData.buildingEntity, value);
                windowChanged=true;
                RecipesAndItemsHead.Close();
            };
            
            if (distribuitionViewData.IsProcessor&&recipeViewData.Item1)
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
        if (storageSlots != null&&storageSlots.Value!=null&&distribuitionViewData==null)
        {
            AddSlotBT.onClick.AddListener(()=>
            {
                if(RecipesAndItemsHead.isOpened.Value) RecipesAndItemsHead.Close();
                else
                    RecipesAndItemsHead.SetUpWindowAsItemsByItemClasses(
                        buildingInfo.BuildingStorageInfos[buildingViewData.buildingID].ItemsTypes.Count>0?
                        buildingInfo.BuildingStorageInfos[buildingViewData.buildingID].ItemsTypes.ToHashSet():
                        Enum.GetValues(typeof(ItemClass)).Cast<ItemClass>()
                                 .ToHashSet());
            });

            RecipesAndItemsHead.onItemChoosed += (value) => 
            {
                model.AddStorageSlot(buildingViewData.buildingEntity, value, 5);
                windowChanged=true;
                AddSlotBT.transform.SetAsLastSibling();
                RecipesAndItemsHead.Close();
                
            }; 
            StorageAreaDispose=new();
            StorageArea.gameObject.SetActive(true);
            StorageHead.gameObject.SetActive(true);
            StorageSlotsHead.gameObject.SetActive(true);
            storageSlots.Subscribe(value =>
            {
                UpdateStorageSlots(value);
            }).AddTo(StorageAreaDispose);
            UpdateStorageSlots(storageSlots.Value);
            allDisposables.Add(StorageAreaDispose);
        }
    }
    void ShowConstructionArea()
    {
        if (constructionViewData != null&&(constructionViewData.InputConstructionSlots!=null||constructionViewData.OutputConstructionSlots!=null))
        {
            
            ConstructionAreaDispose=new();
            ConstructionArea.gameObject.SetActive(true);
            ConstructionSlotsHead.gameObject.SetActive(true);
            ConstructionHead.gameObject.SetActive(true);

            if(constructionViewData.OutputConstructionSlots != null)
            {
                OutputConstructionSlotsHolder.gameObject.SetActive(true);
                ToggleOutputConstuctionBT.Bind(constructionViewData.IsActiveConstructionOutput);
                ConstructionAreaDispose.Add(ToggleOutputConstuctionBT);
                
                for(int i=0;i<constructionViewData.OutputConstructionSlots.Length;i++)
                {
                    OutputConstructionSlots[i].Bind((constructionViewData.OutputConstructionSlots[i].ItemID.Value,
                                        constructionViewData.OutputConstructionSlots[i].Amount,
                                        constructionViewData.OutputConstructionSlots[i].Capacity));
                    ConstructionAreaDispose.Add(OutputConstructionSlots[i]);
                }
            }
            if(constructionViewData.InputConstructionSlots!=null)
            {
                InputConstructionSlotsHolder.gameObject.SetActive(true);
                ToggleInputConstuctionBT.Bind(constructionViewData.IsActiveConstructionInput);
                ConstructionAreaDispose.Add(ToggleInputBT);

                for(int i=0;i<constructionViewData.InputConstructionSlots.Length;i++)
                {
                    InputConstructionSlots[i].Bind((constructionViewData.InputConstructionSlots[i].ItemID.Value,
                                        constructionViewData.InputConstructionSlots[i].Amount,
                                        constructionViewData.InputConstructionSlots[i].Capacity));
                    ConstructionAreaDispose.Add(InputConstructionSlots[i]);
                }
            }
            PriorityConstuctionBT.Bind(constructionViewData.ConstructionPriority);
            ConstructionAreaDispose.Add(PriorityConstuctionBT);
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
    void UpdateStorageSlots(StorageSlotViewData[] slots)
    {
        foreach(var s in StorageSlots)
        {
            StorageAreaDispose.Remove(s);
            s.Dispose();
        }
        if (slots.Length == 20)
        {
            AddSlotBT.interactable=false;
            AddSlotBT.gameObject.SetActive(false);
        }
        else
        {
            AddSlotBT.interactable=true;
            AddSlotBT.gameObject.SetActive(true);
        }
        Debug.Log(slots.Length);
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
                    model.RemoveStorageSlot(buildingViewData.buildingEntity, indexForLambda);
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
        InputConstructionSlotsHolder.gameObject.SetActive(false);
        OutputConstructionSlotsHolder.gameObject.SetActive(false);
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
        workStateDispose?.Dispose();
        workStateDispose=null;
        allDisposables?.Dispose();
        allDisposables=null;
        HideCraftArea();
        HideStorageArea();
        HideConstructionArea();
        HideExcessArea();
    }
    public override void Close()
    {
        ResetWindow();
        RecipesAndItemsHead.Close();
        base.Close();
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
            model.FixedUpdate(buildingViewData);
            fC = 0;
        }
        else fC++;
    }
}