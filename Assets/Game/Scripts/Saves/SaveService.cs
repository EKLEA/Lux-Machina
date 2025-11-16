using System.IO;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Zenject;

public class SaveService : IGameStateSaver
{
    [Inject]
    IReadOnlyBuildingInfo buildingInfo;
    string SavePath;
    public int saveIndex;
    public GameStateData GameState { get; private set; }

    public async Task LoadGameState()
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

    public async Task SaveGameState()
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
        save.buildingDatas = new();
        save.roadPoints = new();
        save.phantomPoints = new();
        save.buildingPosDatas = new();
        save.healthDatas = new();
        save.SlotDatas = new();
        save.inputSlots = new();
        save.outputSlots = new();
        save.buildingWorkWithItemsLogicDatas = new();
        save.processBuildingDatas = new();
        save.phantomBuildings = new();
        save.camData = new PlayerCamData()
        {
            lookPointPosition = new Vector3(0, 0, 0),
            CamPosition = new Vector3(0, 5, -5),
        };
        var hash = "Core".GetStableHashCode();
        save.buildingDatas.Add(
            hash,
            new BuildingData { UniqueIDHash = hash, BuildingIDHash = hash }
        );
        var size = buildingInfo.BuildingInfos[hash].size;
        save.buildingPosDatas.Add(
            hash,
            new BuildingPosData
            {
                LeftCornerPos = new int2(-1, -1),
                Rotation = 1,
                Size = new int2(size.x, size.z),
            }
        );
        return save;
    }
}

public interface IGameStateSaver
{
    public Task SaveGameState();
}
