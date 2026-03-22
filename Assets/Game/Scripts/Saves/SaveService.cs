using System.IO;
using Cysharp.Threading.Tasks;
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

    public async UniTask LoadGameState()
    {
        SavePath = Path.Combine(
            Application.persistentDataPath,
            string.Format("savegame{0}.json", saveIndex)
        );
        try
        {
            if (!File.Exists(SavePath))
            {
                Debug.Log("Файл сохранения не найден, создается новый");
                GameState = GenerateDefault();
            }
            else
            {
                string jsonData;
                using (StreamReader reader = new StreamReader(SavePath))
                {
                    jsonData = await reader.ReadToEndAsync();
                }
                GameStateData loadedData = JsonUtility.FromJson<GameStateData>(jsonData);
                Debug.Log("Игра загружена успешно");
            }
        }
        catch (System.Exception e)
        {
                Debug.LogError($"Ошибка загрузки: {e.Message}");
            GameState = GenerateDefault();
        }
    }

    public async UniTask SaveGameState()
    {
        try
        {
            string jsonData = JsonUtility.ToJson(GameState, true);

            using (StreamWriter writer = new StreamWriter(SavePath))
            {
                await writer.WriteAsync(jsonData);
            }

            Debug.Log($"Игра сохранена: {SavePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка сохранения: {e.Message}");
        }
    }

    public void DeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("Сохранение удалено");
        }
    }

    GameStateData GenerateDefault()
    {
        var save = new GameStateData();
        
        save.EnemyAiConfig=new();
        save.Buildings=new();
        save.RoadsBuildings=new();
        save.constructionSlotsSaveData=new();
        save.excessSlotsSaveData=new();
        save.recipeBuildingSaveData=new();
        save.storageSlotsSaveData=new();
        save.buildingEnergyNetvorkLinkSaveData=new();
        save.ResourcesCells=new();
        save.ResourcesCells[new int2(5,5)]=new int2(1,2);
        save.ResourcesCells[new int2(5,6)]=new int2(1,2);
        save.ResourcesCells[new int2(6,5)]=new int2(1,2);
        save.ResourcesCells[new int2(6,6)]=new int2(1,2);

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
public interface IGameStateSaver
{
    public UniTask SaveGameState();
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
