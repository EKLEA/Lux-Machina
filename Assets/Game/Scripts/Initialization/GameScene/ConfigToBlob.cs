
using System.Collections.Generic;
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
    [Inject] IReadOnlyItemsInfo _itemsInfo;
    [Inject] IReadOnlyEnemyBaseConfig _enemyBaseConfig;
    [Inject] GameFieldSettings _gameFieldSetting;
    [Inject] IEnemyAIConfig _enemyAIConfig;
    Entity configEntity;
    public async UniTask LoadConfigs(EntityManager entityManager)
    {
        _entityManager=entityManager;
        configEntity = entityManager.CreateEntity();
        CreateBuildingConfig(configEntity,_buildingInfo);
        CreateRecipeConfig(configEntity,_recipeInfoInfo);
        CreateItemsConfig(configEntity,_itemsInfo);
        CreateEnemyBaseConfig(configEntity,_enemyBaseConfig, _enemyAIConfig.EnemyAiConfig);
        await UniTask.Yield();
    }

    private void CreateEnemyBaseConfig(Entity configEntity, IReadOnlyEnemyBaseConfig enemyBaseConfig,EnemyAIConfig enemyAIConfig)
    { 
        List<EnemyBaseStructConfig> enemyBaseStructConfigs=new();
        foreach(var cfg in enemyBaseConfig.EnemyBaseConfigs)
        {
            var str = new EnemyBaseStructConfig
            {
                id=cfg.Key,
                costInPoints=cfg.Value.pointAmount,
            };
            enemyBaseStructConfigs.Add(str);
            
        }
        enemyBaseStructConfigs.Sort((a, b) => a.costInPoints.CompareTo(b.costInPoints));
        _entityManager.AddComponentData(configEntity,new EnemyBaseConfigRefence
        {
            EnemyBaseConfigs=CreateConfigReference(enemyBaseStructConfigs.ToArray()),
            ProgressThreshold=enemyAIConfig.ProgressThreshold,
            BaseIncome=enemyAIConfig.BaseIncome,
            PowerMultiplier=enemyAIConfig.PowerMultiplier,
            TimeDifficultyFactor=enemyAIConfig.TimeDifficultyFactor,
        });
    }

    void CreateBuildingConfig(Entity configEntity,IReadOnlyBuildingInfo info)
    {
        
        List<BuildingBaseStructConfig> buildingBaseConfigs=new();
        List<BuildingStorageStructConfig> buildingStorageStructConfig=new();
        List<BuildingProcessionStructConfig> buildingProcessionStructConfig=new();
        List<BuildingItemRequestsStructConfig> buildingItemRequestsStructConfig=new();
        List<BuildingEnergyStructConfig> buildingEnergyStructConfig=new();
        List<TurretStructConfig> turretStructConfigs=new();


        foreach(var cfg in info.BuildingInfos)
        {
            
            var sturctCFG=new BuildingBaseStructConfig
            {
                id=cfg.Key,
                buildingType=cfg.Value.buildingType,
                actionType=cfg.Value.actionType,
                size=new int3(cfg.Value.size.x,cfg.Value.size.y,cfg.Value.size.z),
                typeOfLogic=cfg.Value.typeOfLogic,
                MaxHealth=cfg.Value.maxHealth,
                TimeToRestore=cfg.Value.timeToStartRestore,
                RestoreHpPerTick=cfg.Value.restoreHealthPerSecond
            };
            buildingBaseConfigs.Add(sturctCFG);
        }
        foreach(var cfg in info.BuildingStorageInfos)
        {
            
            FixedList64Bytes<int> requiredItemTypesGroups=new();
            foreach(var g in cfg.Value.ItemsTypes)
                requiredItemTypesGroups.Add((int)g);

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
                requiredRecipesGroups.Add((int)g);

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
        foreach(var cfg in info.BuildingEnegryConfigs)
        {
             var sturctCFG=new BuildingEnergyStructConfig
            {
                id=cfg.Key,
                radius=cfg.Value.radius,
                maxConnections=cfg.Value.maxConnections
            };
            buildingEnergyStructConfig.Add(sturctCFG);
        }
        foreach(var cfg in info.TurretsConfigs)
        {
             var sturctCFG=new TurretStructConfig
            {
                id=cfg.Key,
                AttackRange=cfg.Value.AttackRange,
                CoolDown=cfg.Value.CoolDown,
                Angle=cfg.Value.Angle,
                ProjectilePrefabID=cfg.Value.ProjectilePrefabID.GetStableHashCode(),
            };
            turretStructConfigs.Add(sturctCFG);
        }
        _entityManager.AddComponentData(configEntity,new BuildingConfigReference{ 
            BuildingsBaseConfigs=CreateConfigReference(buildingBaseConfigs.ToArray()),
            BuildingStorageStructConfigs=CreateConfigReference(buildingStorageStructConfig.ToArray()),
            BuildingProcessionStructConfigs=CreateConfigReference(buildingProcessionStructConfig.ToArray()),
            BuildingItemRequestsStructConfigs=CreateConfigReference(buildingItemRequestsStructConfig.ToArray()),
            BuildingEnergyStructConfig=CreateConfigReference(buildingEnergyStructConfig.ToArray()),
            TurretStructConfig=CreateConfigReference(turretStructConfigs.ToArray()),
            roadID="Road".GetStableHashCode(),
            CoreID="Core".GetStableHashCode(),
            range=_gameFieldSetting.range});
    }
    void CreateItemsConfig(Entity configEntity,IReadOnlyItemsInfo info)
    {
        List<ItemsStructConfig> itemsStructConfig=new();
        List<ProjectileStructConfig> ProjectileStructConfigs=new();
        foreach(var cfg in info.ItemsInfos)
        {
            var str = new ItemsStructConfig
            {
                id=cfg.Key,
                ItemClass=(int) cfg.Value.ItemClass,
                ItemType=(int) cfg.Value.ItemType
            };
            itemsStructConfig.Add(str);
        }
        foreach(var cfg in info.ProjectileConfigs)
        {
            var str = new ProjectileStructConfig
            {
                id=cfg.Key,
                AmmoCount=cfg.Value.AmmoCount,
                Damage=cfg.Value.Damage,
                Radius=cfg.Value.Radius,
                Speed=cfg.Value.Speed,
            };
            ProjectileStructConfigs.Add(str);
        }
        _entityManager.AddComponentData(configEntity,new ItemsConfigReference
        {
            ItemsConfigs=CreateConfigReference(itemsStructConfig.ToArray()),
            ProjectileStructConfigs=CreateConfigReference(ProjectileStructConfigs.ToArray()),
        });
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

             foreach(var rGroutp in cf.Value.RecipesGroupIds)
            {
                recipeGroups.Add((int)rGroutp);
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