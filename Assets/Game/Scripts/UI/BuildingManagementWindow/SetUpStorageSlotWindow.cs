using System;

public class SetUpStorageSlotWindow : UIScreen
{
    public event Action<int,int> OnSlotCreated;
    public override void Open()
    {
        OnSlotCreated?.Invoke(1,5);
        base.Open();
        base.Close();
    }
    public void Clear()
    {
        OnSlotCreated=null;
    }
}