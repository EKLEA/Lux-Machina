using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Zenject;
[DisableAutoCreation]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(BuildingCreateDestroyVisualSystem))]
public partial class BuildingChangeVisualSystem : SystemBase
{
    [Inject] VisualBuildingFactory _visualBuildingFactory;
    [Inject] IReadOnlyBuildingInfo _buildingInfo;
    [Inject] EntityManager _entityManager;
    
    protected override void OnUpdate()
    {
        
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (reference,entity) in SystemAPI.Query<BuildingOnSceneReference>().WithAll<ChangeBluePrintState>().WithEntityAccess())
        {
            ChangeBluePrintState(reference,entity,ecb);
        }
        foreach (var (reference,data,entity) in SystemAPI.Query<BuildingOnSceneReference,BuildingData>().WithAll<ChangeDemolitionStateTag>().WithEntityAccess())
        {
            ChangeDemolitionState(reference,data,entity,ecb);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    void ChangeBluePrintState(BuildingOnSceneReference gameobject, Entity building,EntityCommandBuffer ecb)
    {
        if (EntityManager.IsComponentEnabled<IsBlueprint>(building))
        {
            _visualBuildingFactory.UnPhantomizeObject(gameobject.buildingOnScene.gameObject);       
            ecb.SetComponentEnabled<IsBlueprint>(building,false);
        }
        else
        {
            _visualBuildingFactory.PhantomizeObject(gameobject.buildingOnScene.gameObject);       
            ecb.SetComponentEnabled<IsBlueprint>(building,true);
        }
        ecb.SetComponentEnabled<ChangeBluePrintState>(building,false);
    }
    void ChangeDemolitionState(BuildingOnSceneReference gameobject,BuildingData buildingData, Entity building,EntityCommandBuffer ecb)
    {
        
        if(_entityManager.HasComponent<CanCraft>(building)) ecb.SetComponentEnabled<CanCraft>(building,false);
        if (EntityManager.IsComponentEnabled<IsDemolition>(building))
        {
            _visualBuildingFactory.UnDemolitionObject(gameobject.buildingOnScene.gameObject);       
            ecb.SetComponentEnabled<IsDemolition>(building,false);
            bool IsBlueprint=false;

            var outputBuff = _entityManager.GetBuffer<OutputConstructionSlotData>(building);
            var itemRequest=_buildingInfo.BuildingItemRequestsInfos[buildingData.BuildingIDHash];

            var ecbInputBuff = ecb.SetBuffer<InputConstructionSlotData>(building);
            var ecbOutputBuff = ecb.SetBuffer<OutputConstructionSlotData>(building);
            for (int i = 0;i<itemRequest.itemsRequest.Count;i++)
            {
                if (outputBuff[i].Amount < itemRequest.itemsRequest[i].amount)
                {
                    IsBlueprint = true;
                    ecbInputBuff[i] = new InputConstructionSlotData
                    {
                        ItemId = itemRequest.itemsRequest[i].itemId,
                        Capacity = itemRequest.itemsRequest[i].amount,
                        Amount = outputBuff[i].Amount
                    };
                    ecbOutputBuff[i]= new OutputConstructionSlotData
                    {
                        ItemId = itemRequest.itemsRequest[i].itemId,
                        Capacity = itemRequest.itemsRequest[i].amount,
                        Amount = 0
                    };
                }
            }
            if (IsBlueprint)
            {
                var buildingInfo=_buildingInfo.BuildingInfos[buildingData.BuildingIDHash];
                ecb.SetComponentEnabled<ChangeBluePrintState>(building,true);
                ecb.SetComponent(building,new HealthData
                {
                    CurrHealth=buildingInfo.maxHealth,
                    MaxHealth=buildingInfo.maxHealth,
                    TimeToRestore=buildingInfo.timeToStartRestore,
                    CurrTimeToRestore=0,
                    RestoreHpPerTick=buildingInfo.restoreHealthPerSecond
                });
            }

        }
        else
        {
            _visualBuildingFactory.DemolitionObject(gameobject.buildingOnScene.gameObject);       
            ecb.SetComponentEnabled<IsDemolition>(building,true);
            var itemRequest=_buildingInfo.BuildingItemRequestsInfos[buildingData.BuildingIDHash];
            
            var inputBuff = _entityManager.GetBuffer<InputConstructionSlotData>(building);
            var ecbInputBuff = ecb.SetBuffer<InputConstructionSlotData>(building);
            var ecbOutputBuff = ecb.SetBuffer<OutputConstructionSlotData>(building);
            if (_entityManager.IsComponentEnabled<IsBlueprint>(building)&&inputBuff.Length>0)
            {
                for (int i = 0; i <itemRequest.itemsRequest.Count; i++)
                {
                    ecbOutputBuff[i] = new OutputConstructionSlotData
                    {
                        ItemId = itemRequest.itemsRequest[i].itemId,
                        Capacity = itemRequest.itemsRequest[i].amount,
                        Amount = inputBuff[i].Amount
                    };

                    ecbInputBuff[i] = new InputConstructionSlotData
                    {
                        ItemId = itemRequest.itemsRequest[i].itemId,
                        Capacity = itemRequest.itemsRequest[i].amount,
                        Amount = 0,
                    };
                }
            }
            else
            {
                float k=1;
                if (_entityManager.HasComponent<HealthData>(building))
                {
                    var healthData = _entityManager.GetComponentData<HealthData>(building);
                    if (healthData.CurrHealth != healthData.MaxHealth)
                        k=healthData.CurrHealth / healthData.MaxHealth;
                }
                for (int i = 0; i <itemRequest.itemsRequest.Count; i++)
                {
                    float amount=itemRequest.itemsRequest[i].amount*k;
                    ecbOutputBuff[i] = new OutputConstructionSlotData
                    {
                        ItemId = itemRequest.itemsRequest[i].itemId,
                        Capacity = itemRequest.itemsRequest[i].amount,
                        Amount = (int)amount
                    };
                }
            }
        }
        ecb.SetComponentEnabled<ChangeDemolitionStateTag>(building,false);
    }
}