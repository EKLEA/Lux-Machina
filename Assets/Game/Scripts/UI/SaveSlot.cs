using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveSlot : MonoBehaviour
{
    public int slotIndex; 
    public TextMeshProUGUI buttonText;
    public Button deleteBT;

    public void Refresh()
    {
        string path = Path.Combine(Application.persistentDataPath, $"savegame{slotIndex}.json");
        
        if (File.Exists(path))
        {
            var lastWrite = File.GetLastWriteTime(path);
            buttonText.text = $"Загрузить #{slotIndex}\n<size=70%>{lastWrite:dd.MM HH:mm}</size>";
            deleteBT.interactable=true;
        }
        else
        {
            buttonText.text = $"Создать мир #{slotIndex}";
             deleteBT.interactable=false;
        }
    }
}