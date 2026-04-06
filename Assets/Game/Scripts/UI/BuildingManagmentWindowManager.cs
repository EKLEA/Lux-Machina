using System;
using System.Collections.Generic;
using UniRx;
using Unity.Entities;
using UnityEngine;
using Zenject;

public class BuildingManagmentWindowManager : UIScreen, IInitializable
{
    [Inject] private GameController gameController;
    [SerializeField] private BuildingManagementWindowView screen1;
    [SerializeField] private BuildingManagementWindowView screen2;

    private List<BuildingManagementWindowView> activeScreens;
    private Action UpdateAction;
    private Dictionary<BuildingManagementWindowView, IDisposable> disposables;

    public override void Initialize()
    {
        base.Initialize(); 
        activeScreens = new List<BuildingManagementWindowView>();
        disposables = new Dictionary<BuildingManagementWindowView, IDisposable>();

        screen1.Initialize();
        screen2.Initialize();
        
        
        screen1.BindModel(new BuildingInfoViewModel(gameController.World));
        screen2.BindModel(new BuildingInfoViewModel(gameController.World));
    }

    private void Update()
    {
        UpdateAction?.Invoke();
    }

    public void OpenBuilding(int id, bool secondWindow)
    {
        if (gameController.GetEntity(id, out Entity entity))
        {
            BuildingManagementWindowView targetView = secondWindow ? screen2 : screen1;
            BuildingManagementWindowView otherView = secondWindow ? screen1 : screen2;

            
            if (otherView.buildingViewData != null && otherView.buildingViewData.buildingEntity == entity)
                return;

            
            if (targetView.buildingViewData != null && targetView.buildingViewData.buildingEntity == entity && activeScreens.Contains(targetView))
            {
                targetView.SetUpData(entity);
                return;
            }

            
            if (disposables.ContainsKey(targetView))
            {
                disposables[targetView]?.Dispose();
                disposables.Remove(targetView);
                UpdateAction -= targetView.UpdateView;
            }

            
            if (activeScreens.Count == 0) 
                base.Open();

            
            targetView.SetUpData(entity);

            if (!activeScreens.Contains(targetView))
                activeScreens.Add(targetView);

            UpdateAction += targetView.UpdateView;

            
            var closeSubscription = targetView.isOpened
                .SkipLatestValueOnSubscribe() 
                .Where(isOpen => !isOpen)
                .Subscribe(_ => CloseWindow(targetView));

            disposables.Add(targetView, closeSubscription);
        }
    }

    public void CloseWindow(BuildingManagementWindowView view)
    {
        
        if (disposables.TryGetValue(view, out var subscription))
        {
            subscription?.Dispose();
            disposables.Remove(view);
        }

        
        UpdateAction -= view.UpdateView;
        
        if (activeScreens.Contains(view))
            activeScreens.Remove(view);

        
        if (view.windowToMove != null)
            view.windowToMove.anchoredPosition = view.defaultPos;

        view.Close();

        
        if (activeScreens.Count == 0) 
            base.Close();
    }
}
