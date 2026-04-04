using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Zenject;

public class SaveService : IGameStateSaver,IReadOnlySave,IEnemyAIConfig
{
    [Inject]
    IReadOnlyBuildingInfo buildingInfo;
    public EnemyAIConfig EnemyAiConfig=>GameState.EnemyAiConfig;
    string SavePath;
    public int saveIndex;
    public GameStateData GameState { get; private set; }
    private readonly JsonSerializerSettings _settings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        // Если ты используешь Unity.Mathematics (int2, float3), 
        // Newtonsoft обычно "ест" их как обычные структуры с полями x,y
    };
   public async UniTask LoadGameState()
{
    SavePath = Path.Combine(
        Application.persistentDataPath,
        string.Format("savegame{0}.json", saveIndex)
    );
    
    Debug.Log($"[LOAD] Попытка загрузки по пути: {Path.GetFullPath(SavePath)}");

    try
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("[LOAD] Файл сохранения не найден, генерируется стандартный мир");
            GameState = GenerateDefault();
            return;
        }

        string jsonData = await File.ReadAllTextAsync(SavePath);

        // 4. КРИТИЧЕСКИ ВАЖНО: Добавляем конвертер в настройки загрузки
        var settings = new JsonSerializerSettings 
        { 
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = new List<JsonConverter> { new UnityMathematicsConverter() }
            
        };

        // 5. Десериализуем (теперь Newtonsoft передаст управление FixedList нашему конвертеру)
        GameStateData loadedData = await UniTask.RunOnThreadPool(() => 
            JsonConvert.DeserializeObject<GameStateData>(jsonData, settings)
        );

        if (loadedData != null)
        {
            GameState = loadedData;
            Debug.Log("[LOAD] Игра загружена успешно!");
        }
    }
    catch (System.Exception e)
    {
        // Выводим e.ToString(), чтобы видеть полную цепочку ошибок
        Debug.LogError($"[LOAD КРИТИЧЕСКАЯ ОШИБКА]: {e}");
        GameState = GenerateDefault();
    }
}
  public async UniTask SaveGameState(GameStateData gameStateData)
{
    try
    {
        Debug.Log("Начало сериализации...");
        var settings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            // Проверьте, что вы создаете НОВЫЙ экземпляр конвертера здесь
            Converters = { new UnityMathematicsConverter() } 
        };

        string jsonData = await UniTask.RunOnThreadPool(() => 
            JsonConvert.SerializeObject(gameStateData, Formatting.Indented, settings)
        );

        await File.WriteAllTextAsync(SavePath, jsonData);
        Debug.Log($"Файл сохранен успешно: {SavePath}");
    }
    catch (System.Exception e)
    {
        // Выводим e.ToString(), чтобы видеть ПОЛНЫЙ текст ошибки и строку кода
        Debug.LogError($"[ОШИБКА]: {e}");
    }
}


    public void DeleteSave(int index)
    {
        SavePath = Path.Combine(
        Application.persistentDataPath,
        string.Format("savegame{0}.json", index)
        );
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("Сохранение удалено");
        }
    }

    GameStateData GenerateDefault()
    {
        var save = new GameStateData();
        save.IsGameOver=false;
        save.CurrTick=0;
        save.EnemyAiConfig=new();
        save.Buildings=new();
        save.ManyPointsBuildings=new();
        save.constructionSlotsSaveData=new();
        save.excessSlotsSaveData=new();
        save.recipeBuildingSaveData=new();
        save.storageSlotsSaveData=new();
        save.buildingEnergyNetvorkLinkSaveData=new();
        save.ResourcesCellsList=new();
        save.ResourcesCellsList.Add(new ResourceCellSave{pos= new int2(5,5),val=new int2(1,2)});
        save.ResourcesCellsList.Add(new ResourceCellSave{pos= new int2(5,6),val=new int2(1,2)});
        save.ResourcesCellsList.Add(new ResourceCellSave{pos= new int2(6,5),val=new int2(1,2)});
        save.ResourcesCellsList.Add(new ResourceCellSave{pos= new int2(6,6),val=new int2(1,2)});

        // save.buildingDatas = new();
        // save.roadPoints = new();
        // save.phantomPoints = new();
        // save.buildingPosDatas = new();
        // save.healthDatas = new();
        // save.slotDatas = new();
        // save.inputSlots = new();
        // save.outputSlots = new();
        // save.buildingsPriorityDatas = new();
        // save.processBuildingDatas = new();
        // save.phantomBuildings = new();
        save.camData = new PlayerCamData()
        {
            lookPointPosition = new Vector3(0, 0, 0),
            CamPosition = new Vector3(0, 25, -25),
        };
        var hash = "Core".GetStableHashCode();
        save.CoreID=hash;
        save.CorePos=new int2(-1, -1);
        save.Buildings.Add(
            hash,
            new BaseBuildingSaveData
            {
                buildingID=hash,
                buildingPosition= save.CorePos,
                rotation = 1,
                isBlueprint=false,
            }
        );

        return save;
    }
}
public interface  IEnemyAIConfig
{
    public EnemyAIConfig EnemyAiConfig{get;}
}
public interface IReadOnlySave
{
    public GameStateData GameState{get;}
}
public interface IGameStateSaver:IReadOnlySave
{
    public UniTask SaveGameState(GameStateData gameStateData);
}
public class EnemyAIConfig
{
    public float ProgressThreshold {get;private set;}
    public float BaseIncome{get;private set;}
    public float PowerMultiplier{get;private set;}
    public float TimeDifficultyFactor{get;private set;}  

    public EnemyAIConfig(float ProgressThreshold=10000, float BaseIncome=40, float PowerMultiplier=1.5f, float TimeDifficultyFactor=0.1f)
    {
        this.ProgressThreshold=ProgressThreshold;
        this.BaseIncome=BaseIncome;
        this.PowerMultiplier=PowerMultiplier;
        this.TimeDifficultyFactor=TimeDifficultyFactor;
    }
}
