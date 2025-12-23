using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using Zenject;

public class BuildingManagementWindowView:DragableUIWindow
{
    [Inject] BuildingManagementWindowViewModel model;
    [Inject] GameController gameController;
    [Inject] IReadOnlyBuildingInfo buildingInfo;
    [Inject] IReadOnlyRecipeInfo recipeInfo;
    [Header("Simple obj")]
    [SerializeField] Transform Header;
    [SerializeField] Image indicatorOrb;
    [SerializeField] TextMeshProUGUI indicatorText;
    [SerializeField] Button DestroyBT;
    [SerializeField] Image BuildingSprite;
    [SerializeField] TextMeshProUGUI BuildingText;
    [SerializeField] TextMeshProUGUI BuildingDescriptionText;
    [Header("Heads")]
    [SerializeField] Transform StorageHead;
    [SerializeField] Transform RecipeHead;
    [SerializeField] Transform CraftHead;
    [SerializeField] Transform DestributeHead;
    [Header("Buttons")]
    [SerializeField] ToggleButtonScript ToggleInputBT;
    [SerializeField] ToggleButtonScript ToggleOutputBT;
    [SerializeField] AdjustableButtonScript PriorityBT;
    [SerializeField] AdjustableButtonScript CountOfPackBT;
    [Header("Recipe")]
    [SerializeField] Image RecipeSprite;
    [SerializeField] TextMeshProUGUI RecipeName;
    [SerializeField] Transform InputSlotsHolder;
    [SerializeField] Transform ProgressBar;
    [SerializeField] Transform OutputSlotsHolder;
    [SerializeField] Transform DestributeSlotsHolder;
    [SerializeField] Transform ChooseRecipeWindow;
    [Header("Slots")]
    [SerializeField] SlotView[] InputSlots;
    [SerializeField] SlotView[] OutputSlots;
    [SerializeField] SlotView[] DestributeSlots;
    [SerializeField] AdjustableSlotButtonScript[] StorageSlots;
   

    #region subscribes
    CompositeDisposable allSubscibes=new();
    Action destributeSlotsDisposeAction;
    Action workSlotsDisposeAction;
    Action buttonsDispose;
    #endregion
    int fC;
    int UniqueIDHash;
    BuildingViewDataResult  data;
    public void SetUpData(int id)
    {
        UniqueIDHash = id;
        var entity = gameController.GetEntity(UniqueIDHash);
        if (model.GetBuildingData(entity, out BuildingViewDataResult viewData))
        {
            data = viewData;
        }
        Open();
    }
    private void FixedUpdate() 
    {
        model.Update(data.uniqueBuilding);
    }
    public override void Open()
    {
        if (data == null) 
        {
            Close();
            return;
        }
        
        if (data is BuildingViewDataResult && data.GetType() == typeof(BuildingViewDataResult))
        {
            Close();
            return;
        }
        if(data is DefenceBuildingDataResult defence)
            SetUpDefenceBuildingWindow(defence);
        else if(data is ProcessingBuildingWithRecipeDataResult proc)
            SetUpProcessorWindow(proc);
        else if(data is BuildingWithItemsDataResult storage)
            SetUpStorageWindow(storage);
        else if(data is BuildingWithStateUnasignedRecipeViewDataResult unasigne)
            SetUpChooseRecipeWindow(unasigne);
        else if(data is BuildingWithStateViewDataResult simple)
            SetUpSimpleWindow(simple);
         
        if(data is BuildingWithItemsDataResult withItems)
        {
            SetUpDestributeSlots(withItems);
            withItems.DestributeSlots
                 .Subscribe(_ => SetUpDestributeSlots(withItems)).AddTo(allSubscibes);
        }
        
        base.Open();
    }
   
    void SetUpSimpleWindow(BuildingWithStateViewDataResult data)
    {
        ResetWindow();
        BuildingSprite.sprite=buildingInfo.GetBuildingSprite(data.buildingID);
        BuildingText.text=buildingInfo.BuildingInfos[data.buildingID].title;
        BuildingDescriptionText.text=buildingInfo.BuildingInfos[data.buildingID].description;
        DestroyBT.gameObject.SetActive(true);
        workSlotsDisposeAction=null;
        destributeSlotsDisposeAction=null;
    }
    void SetUpProcessorWindow(ProcessingBuildingWithRecipeDataResult data)//добавить подписки
    {
        SetUpSimpleWindow(data);
        CraftHead.gameObject.SetActive(true);
        if(data.OutSlots != null)
        {
            OutputSlotsHolder.gameObject.SetActive(true);
            ToggleOutputBT.Bind(data.IsActiveOutput);
            buttonsDispose+=ToggleOutputBT.Dispose;
            for(int i=0;i<data.OutSlots.End-data.OutSlots.Start;i++)
            {
                OutputSlots[i].Bind(data.Slots[data.OutSlots.Start+i]);
                OutputSlots[i].gameObject.SetActive(true);
                workSlotsDisposeAction+=OutputSlots[i].Dispose;
            }
        }
        if(data.inputSlots!=null)
        {
            InputSlotsHolder.gameObject.SetActive(true);
            ToggleInputBT.Bind(data.IsActiveInput);
            buttonsDispose+=ToggleInputBT.Dispose;
            for(int i=0;i<data.inputSlots.End-data.inputSlots.Start;i++)
            {
                InputSlots[i].Bind(data.Slots[data.inputSlots.Start+i]);
                InputSlots[i].gameObject.SetActive(true);
                workSlotsDisposeAction=InputSlots[i].Dispose;
            }
        }
        ProgressBar.gameObject.SetActive(true);
        PriorityBT.Bind(data.Priority);
        buttonsDispose+=PriorityBT.Dispose;

        CountOfPackBT.Bind(data.CountInPack);
        buttonsDispose+=CountOfPackBT.Dispose;
        
        RecipeHead.gameObject.SetActive(true);
        RecipeName.text=recipeInfo.RecipeInfos[data.RecipeIDHash.Value].title;
        RecipeSprite.sprite=recipeInfo.GetRecipeSprite(data.RecipeIDHash.Value);
    }
    void SetUpDefenceBuildingWindow(DefenceBuildingDataResult data)
    {
        SetUpStorageWindow(data);
    }
    void SetUpStorageWindow(BuildingWithItemsDataResult data)//добавить подписки
    {
        SetUpSimpleWindow(data);
        StorageHead.gameObject.SetActive(true);
        ToggleInputBT.Bind(data.IsActiveInput);
        ToggleOutputBT.Bind(data.IsActiveOutput);
        for(int i = 0; i < data.Slots.Length; i++)
        {
            StorageSlots[i].Bind(data.Slots[i]);
            workSlotsDisposeAction+=StorageSlots[i].Dispose;
        }
    }
    void SetUpChooseRecipeWindow(BuildingWithStateUnasignedRecipeViewDataResult data)
    {
        SetUpSimpleWindow(data);
        
    }
    void SetUpDestributeSlots(BuildingWithItemsDataResult data)//добавить подписки
    {
        if (data.DestributeSlots.Value!=null)
        {
            DestributeSlotsHolder.gameObject.SetActive(true);
            for(int i=0;i<data.DestributeSlots.Value.End-data.DestributeSlots.Value.Start;i++)
            {
                DestributeSlots[i].Bind(data.Slots[data.DestributeSlots.Value.Start+i]);
                destributeSlotsDisposeAction+=DestributeSlots[i].Dispose;
            }
        }
        else
        {
            HideDestributeSlots();
        }
    }
    void HideDestributeSlots()
    {
        destributeSlotsDisposeAction?.Invoke();
        destributeSlotsDisposeAction=null;
        DestributeSlotsHolder.gameObject.SetActive(false);
    }    
    void HideCraftHead()
    {
        
        HideRecipeFill();
        CraftHead.gameObject.SetActive(false);
        RecipeHead.gameObject.SetActive(false);
        RecipeName.text="";
        RecipeSprite.sprite=null;
    }
    void HideRecipeFill()
    {
        workSlotsDisposeAction?.Invoke();
        workSlotsDisposeAction=null;
        OutputSlotsHolder.gameObject.SetActive(false);
        ToggleOutputBT.gameObject.SetActive(false);
        InputSlotsHolder.gameObject.SetActive(false);
        ToggleInputBT.gameObject.SetActive(false);
        ProgressBar.gameObject.SetActive(false);
    }
    void ResetWindow()
    {
        HideDestributeSlots();
        HideCraftHead();
        allSubscibes.Dispose();
    }
    public void Update()
    {
        if(!isOpened) return;
        if(fC%(int)(gameController.Timestep*.2)==0)   model.Update(gameController.GetEntity(UniqueIDHash));
    }
}