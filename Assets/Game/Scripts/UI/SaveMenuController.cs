using System;
using UnityEngine;

public class SaveMenuController : MonoBehaviour,IDisposable
{
    public SaveSlot[] slots; 
    
    public SaveService saveService; 
    public SceneLoader sceneLoader;
    public event Action<int> onSlotSelected;
    public event Action<int> onSlotDelete;

    void OnEnable()
    {
        foreach (var slot in slots)
        {
            slot.Refresh();
        }
    }
    public void SelectSlot(SaveSlot selectedSlot)
    {
        onSlotSelected?.Invoke(selectedSlot.slotIndex);
    }
    public void DeleteSlot(SaveSlot selectedSlot)
    {
        onSlotDelete?.Invoke(selectedSlot.slotIndex);
        selectedSlot.Refresh();
    }

    public void Dispose()
    {
        onSlotSelected=null;
        onSlotDelete=null;
    }
}
