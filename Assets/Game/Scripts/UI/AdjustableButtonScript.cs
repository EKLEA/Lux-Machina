
using UnityEngine;

using System;
using UnityEngine.UI;
using UniRx;
using TMPro;
public class AdjustableButtonScript :MonoBehaviour, IDisposable
{
    [SerializeField]  Button UpBT;
    [SerializeField]  Button DownBT;
    [SerializeField]  Button MainBT;
    [SerializeField]  TextMeshProUGUI valueText;
      string format;
     ReactiveProperty<int> Value;
    
    public void Bind(ReactiveProperty<int> Value,string Format="")
    {
        Clear();
        gameObject.SetActive(true);
        this.Value=Value;
        format= Format==""?"{0}":Format;
        valueText.text=string.Format(format,Value.Value.ToString());
        UpBT.onClick.AddListener(()=>ChangeValue(1));
        DownBT.onClick.AddListener(()=>ChangeValue(-1));
        //добавить обработку по нажатию в центр кнопки
    }
    void Clear()
    {
        gameObject.SetActive(false);
        Value=null;
        valueText.text="";
        UpBT.onClick.RemoveAllListeners();
        DownBT.onClick.RemoveAllListeners();
    }
    public void Dispose()
    {
       Clear();
    }

    void ChangeValue(int Value)
    {
        this.Value.Value+=Value;
        valueText.text=string.Format(format,this.Value.Value.ToString());
    }
}