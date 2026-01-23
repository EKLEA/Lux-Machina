using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Zenject;

public class ConfigToBlob : IInitializable
{
    EntityManager _entityManager;
    [Inject] IReadOnlyBuildingInfo _buildingInfo;
    [Inject] IReadOnlyRecipeInfo _recipeInfoInfo;
    Entity configEntity;
    public async UniTask LoadConfigs(EntityManager entityManager)
    {
        _entityManager=entityManager;
        configEntity = entityManager.CreateEntity();
        CreateBuildingConfig(configEntity,_buildingInfo);
        CreateRecipeConfig(configEntity,_recipeInfoInfo);
        await UniTask.Yield();
    }
    void CreateBuildingConfig(Entity configEntity,IReadOnlyBuildingInfo info)
    {
        
        List<BuildingBaseStructConfig> buildingBaseConfigs=new();
        List<BuildingStorageStructConfig> buildingStorageStructConfig=new();
        List<BuildingProcessionStructConfig> buildingProcessionStructConfig=new();
        List<BuildingItemRequestsStructConfig> buildingItemRequestsStructConfig=new();


        foreach(var cfg in info.BuildingInfos)
        {
            
            var sturctCFG=new BuildingBaseStructConfig
            {
                id=cfg.Key,
                buildingType=cfg.Value.buildingType,
                actionType=cfg.Value.actionType,
                size=new int3(cfg.Value.size.x,cfg.Value.size.y,cfg.Value.size.z),
                typeOfLogic=cfg.Value.typeOfLogic,
            };
            buildingBaseConfigs.Add(sturctCFG);
        }
        foreach(var cfg in info.BuildingStorageInfos)
        {
            
            FixedList64Bytes<int> requiredItemTypesGroups=new();
            foreach(var g in cfg.Value.ItemsTypes)
                requiredItemTypesGroups.Add(g);

            var sturctCFG=new BuildingStorageStructConfig
            {
                id=cfg.Key,
                maxSlots=cfg.Value.MaxSlots,
                requiredItemTypesGroups=requiredItemTypesGroups,
            };
            buildingStorageStructConfig.Add(sturctCFG);
        }
        foreach(var cfg in info.BuildingProcessionInfos)
        {
            
            FixedList64Bytes<int> requiredRecipesGroups=new();
            foreach(var g in cfg.Value.requiredRecipesGroup)
                requiredRecipesGroups.Add(g);

            var sturctCFG=new BuildingProcessionStructConfig
            {
                id=cfg.Key,
                typeOfProcession=cfg.Value.typeOfProcession,
                requiredRecipesGroups=requiredRecipesGroups,
            };
            buildingProcessionStructConfig.Add(sturctCFG);
        }
        foreach(var cfg in info.BuildingItemRequestsInfos)
        {
            
            FixedList64Bytes<RecipeIngredientStruct> itemsRequests=new();
            foreach(var g in cfg.Value.itemsRequest)
            {
                itemsRequests.Add(new RecipeIngredientStruct{ItemId=g.itemId,Amount=g.amount});
            }

            var sturctCFG=new BuildingItemRequestsStructConfig
            {
                id=cfg.Key,
                itemsRequests=itemsRequests,
            };
            buildingItemRequestsStructConfig.Add(sturctCFG);
        }
        _entityManager.AddComponentData(configEntity,new BuildingConfigReference{ 
            BuildingsBaseConfigs=CreateConfigReference(buildingBaseConfigs.ToArray()),
            BuildingStorageStructConfigs=CreateConfigReference(buildingStorageStructConfig.ToArray()),
            BuildingProcessionStructConfigs=CreateConfigReference(buildingProcessionStructConfig.ToArray()),
            BuildingItemRequestsStructConfigs=CreateConfigReference(buildingItemRequestsStructConfig.ToArray()),
            roadID="Road".GetStableHashCode()});
    }
    void CreateRecipeConfig(Entity configEntity,IReadOnlyRecipeInfo info)
    {
        List<RecipeStructConfig> recipeConfigs=new();
        foreach(var cf in info.RecipeInfos)
        {
            FixedList64Bytes<RecipeIngredientStruct> inputItems=new();
            FixedList64Bytes<RecipeIngredientStruct> outputItems=new();
            FixedList64Bytes<int> recipeGroups=new();
            foreach(var inItem in cf.Value.inputItems)
            {
                inputItems.Add(new RecipeIngredientStruct{Amount=inItem.amount,ItemId=inItem.itemId});
            }
            foreach(var outItem in cf.Value.outputItems)
            {
                outputItems.Add(new RecipeIngredientStruct{Amount=outItem.amount,ItemId=outItem.itemId});
            }

             foreach(var rGroutp in cf.Value.groupIds)
            {
                recipeGroups.Add(rGroutp);
            }
            var sturctCFG=new RecipeStructConfig
            {
                id=cf.Value.id,
                InputItems=inputItems,
                OutputItems=outputItems,
                CraftTime=cf.Value.craftTime,
                RecipesGroups=recipeGroups,
            };
            recipeConfigs.Add(sturctCFG);
        }
        _entityManager.AddComponentData(configEntity,new RecipeConfigRefernce{ 
            RecipesConfig=CreateConfigReference(recipeConfigs.ToArray())});
    }
    public BlobAssetReference<BlobLibrary<T>> CreateConfigReference<T>(T[] sourceData) where T:unmanaged,IConfigBase
    {

        using var nativeConfigs = new NativeArray<T>(sourceData, Allocator.Temp);
        nativeConfigs.Sort(new ConfigComparer<T>());

        using var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<BlobLibrary<T>>();
        
        var arrayBuilder = builder.Allocate(ref root.Configs, nativeConfigs.Length);
        for (int i = 0; i < nativeConfigs.Length; i++)
        {
            arrayBuilder[i] = nativeConfigs[i];
        }

        var blobRef = builder.CreateBlobAssetReference<BlobLibrary<T>>(Allocator.Persistent);
        return blobRef;
    }

    public void Initialize()
    {
    }

    public struct ConfigComparer<T> : IComparer<T> where T:IConfigBase
    {
        public int Compare(T x, T y) => x.id.CompareTo(y.id);
    }


}