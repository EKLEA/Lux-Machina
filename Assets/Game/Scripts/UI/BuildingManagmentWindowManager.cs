using System;
using System.Collections.Generic;
using UniRx;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using UnityEngine;
using Zenject;

public class BuildingManagmentWindowManager : UIScreen,IInitializable
{
    
    [Inject] GameController gameController;
    [SerializeField] BuildingManagementWindowView screen1;
    [SerializeField] BuildingManagementWindowView screen2;
    List<BuildingManagementWindowView > activeScreens;
    Action UpdateAction;
    Dictionary<BuildingManagementWindowView,IDisposable> disposables;
    public override void Initialize()
    {
        activeScreens=new();
        disposables=new();
        screen1.Initialize();
        screen2.Initialize();
        screen1.BindModel(new BuildingInfoViewModel(gameController.World));
        screen2.BindModel(new BuildingInfoViewModel(gameController.World));
        base.Initialize();
    }
    public void Update()
    {
        UpdateAction?.Invoke();
    }
    public void OpenBuilding(int id,bool secondWindow)
    {
        
        if(activeScreens.Count==0) 
            base.Open();
        BuildingManagementWindowView firstView;
        BuildingManagementWindowView secondView;
        if(gameController.GetEntity(id,out Entity entity))
        {
            if (secondWindow)
            {
                firstView=screen2;
                secondView=screen1;
            }
            else
            {
                firstView=screen1;
                secondView=screen2;
            }
            
                
            if(secondView.buildingViewData!=null&&secondView.buildingViewData.buildingEntity==entity)
                return;
            if(!activeScreens.Contains(firstView))
            {
                firstView.SetUpData(entity);
                activeScreens.Add(firstView);
                UpdateAction+=firstView.UpdateView;
                disposables.Add(firstView,firstView.isOpened
                .Where(isOpened=> isOpened==false)
                .Subscribe(_ =>
                {
                    CloseWindow(firstView);
                }));
            }
            else
                firstView.SetUpData(entity);
        }
    }
    public void CloseWindow(BuildingManagementWindowView view)
    {
        disposables[view]?.Dispose();
        disposables.Remove(view);
        UpdateAction-=view.UpdateView;
        activeScreens.Remove(view);
        view.windowToMove.anchoredPosition=view.defaultPos;
        view.Close();
        if(activeScreens.Count==0) base.Close();
    }
}