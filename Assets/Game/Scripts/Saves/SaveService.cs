using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class SaveService : IGameStateSaver
{
    string SavePath;
    public int saveIndex;
    public GameStateData GameState { get; private set; }
    public async Task LoadGameState()
    {
        SavePath = Path.Combine(Application.persistentDataPath, string.Format("savegame{0}.json", saveIndex));
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
        return new GameStateData();
    }
}
public interface IGameStateSaver
{
    public Task SaveGameState();
}