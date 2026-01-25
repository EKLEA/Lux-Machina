
using UnityEngine;

using System;
using UnityEngine.UI;
using UniRx;
using TMPro;
using Unity.Mathematics;
public class AdjustableButtonScript :MonoBehaviour, IDisposable
{
    [SerializeField]  Button UpBT;
    [SerializeField]  Button DownBT;
    [SerializeField]  Button MainBT;
    [SerializeField]  TextMeshProUGUI valueText;
      string format;
     ReactiveProperty<int> Value;
     int _min,_max;
    
    public void Bind(ReactiveProperty<int> Value,int min=1,int max=5,string Format="")
    {
        Clear();
        _min=min;
        _max=max;
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
        int next = this.Value.Value + Value;
        if (next > _max)
            this.Value.Value = _min;
        else if (next < _min)
            this.Value.Value = _max;
        else
            this.Value.Value = next;
        valueText.text=string.Format(format,this.Value.Value.ToString());
    }
}