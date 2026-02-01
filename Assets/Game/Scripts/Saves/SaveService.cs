using System.IO;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Zenject;

public class SaveService : IGameStateSaver,IReadOnlySave
{
    [Inject]
    IReadOnlyBuildingInfo buildingInfo;
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
        save.Buildings=new();
        save.RoadsBuildings=new();
        save.constructionSlotsSaveData=new();
        save.excessSlotsSaveData=new();
        save.recipeBuildingSaveData=new();
        save.storageSlotsSaveData=new();

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
        save.Buildings.Add(
            123,
            new BaseBuildingSaveData
            {
                buildingID=hash,
                buildingPosition= new int2(-1, -1),
                rotation = 1,
                isConnected=false,
                isBlueprint=false,
            }
        );
        return save;
    }
}
public interface IReadOnlySave
{
    public GameStateData GameState{get;}
}
public interface IGameStateSaver
{
    public UniTask SaveGameState();
}
