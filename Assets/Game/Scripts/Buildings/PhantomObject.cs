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

    public void SetUp(IReadOnlyPhantomConfig config)
    {
        _config = config;
        _renderers.Clear();
        _renderers.AddRange(GetComponentsInChildren<Renderer>(true));
        _propBlock = new MaterialPropertyBlock();
    }
    public void SetPhantomMode(bool isPhantom, bool isBlueprint)
    {
        var activeConfig = isBlueprint ? _config.BluePrintPhantomConfig : _config.DemolitionAndFalsePhantomConfig;
        
        UpdateVisuals(block => {
            block.SetFloat(IsPhantomID, isPhantom ? 1f : 0f);
            if (isPhantom)
            {
                block.SetColor(MainColorID, activeConfig.MainColor);
                block.SetColor(LineColorID, activeConfig.LineColor);
            }
        });
    }

    public void CanBuild(bool canBuild, bool force)
    {
        PhantomConfig targetConfig;

        if (force)
            targetConfig = _config.ForceDestroyPhantomConfig;
        else
            targetConfig = canBuild ? _config.BluePrintPhantomConfig : _config.DemolitionAndFalsePhantomConfig;

        UpdateVisuals(block => {
            block.SetColor(MainColorID, targetConfig.MainColor);
            block.SetColor(LineColorID, targetConfig.LineColor);
        });
    }
    public void SetProgress(float value)
    {
        UpdateVisuals(block => {
            block.SetFloat(ProgressID, value); 
        });
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
        UpdateVisuals(block => block.SetFloat(IsPhantomID, 0f));
        _renderers.Clear();
        Destroy(this);
    }
}
