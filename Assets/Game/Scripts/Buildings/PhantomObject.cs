using System;
using System.Collections.Generic;
using UnityEngine;

public class PhantomObject : MonoBehaviour
{
    private static readonly int IsPhantomID = Shader.PropertyToID("_IsPhantom");
    private static readonly int MainColorID = Shader.PropertyToID("_PhantomColor");
    private static readonly int LineColorID = Shader.PropertyToID("_LineColor");
    private static readonly int ProgressID = Shader.PropertyToID("_PhantomProcent");

    private List<Renderer> _renderers = new();
    private MaterialPropertyBlock _propBlock;       
    private IReadOnlyPhantomConfig _config;

    // Ссылка на инстанс-билдинг
    private ManyPointsBuildingInstanced _instancedBuilding;

    // Текущие значения для синхронизации
    private bool _isPhantom;
    private Color _mainColor;
    private Color _lineColor;
    private float _progress;

    public void SetUp(IReadOnlyPhantomConfig config)
    {
        _config = config;
        _renderers.Clear();
        _renderers.AddRange(GetComponentsInChildren<Renderer>(true));
        _propBlock = new MaterialPropertyBlock();
        
        // Пытаемся найти компонент инстансинга на этом же объекте
        _instancedBuilding = GetComponent<ManyPointsBuildingInstanced>();
    }

    public void SetPhantomMode(bool isPhantom, bool isBlueprint)
    {
        var activeConfig = isBlueprint ? _config.BluePrintPhantomConfig : _config.DemolitionAndFalsePhantomConfig;
        
        _isPhantom = isPhantom;
        if (isPhantom)
        {
            _mainColor = activeConfig.MainColor;
            _lineColor = activeConfig.LineColor;
        }

        UpdateVisuals(block => {
            block.SetFloat(IsPhantomID, isPhantom ? 1f : 0f);
            if (isPhantom)
            {
                block.SetColor(MainColorID, _mainColor);
                block.SetColor(LineColorID, _lineColor);
            }
        });

        SyncInstanced();
    }

    public void CanBuild(bool canBuild, bool force)
    {
        PhantomConfig targetConfig;

        if (force)
            targetConfig = _config.ForceDestroyPhantomConfig;
        else
            targetConfig = canBuild ? _config.BluePrintPhantomConfig : _config.DemolitionAndFalsePhantomConfig;

        _mainColor = targetConfig.MainColor;
        _lineColor = targetConfig.LineColor;

        UpdateVisuals(block => {
            block.SetColor(MainColorID, _mainColor);
            block.SetColor(LineColorID, _lineColor);
        });

        SyncInstanced();
    }

    public void SetProgress(float value)
    {
        _progress = value;
        UpdateVisuals(block => {
            block.SetFloat(ProgressID, value); 
        });

        SyncInstanced();
    }

    // Тот самый метод синхронизации с вашим ManyPointsBuildingInstanced
    private void SyncInstanced()
    {
        if (_instancedBuilding != null)
        {
            _instancedBuilding.UpdatePhantomParams(_isPhantom, _mainColor, _lineColor, _progress);
        }
    }

    private void UpdateVisuals(Action<MaterialPropertyBlock> action)
    {
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_propBlock);
            action.Invoke(_propBlock);
            r.SetPropertyBlock(_propBlock);
        }
    }

    public void UnPhantom()
    {
        _isPhantom = false;
        UpdateVisuals(block => block.SetFloat(IsPhantomID, 0f));
        SyncInstanced();
        
        _renderers.Clear();
        Destroy(this);
    }
}
