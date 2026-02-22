using System;
using UnityEngine;
using Zenject;

public class VisualBuildingFactory
{
    [Inject] IReadOnlyPhantomConfig _PhantomConfig;

    public PhantomObject PhantomizeObject(GameObject gameObject)
    {
        var get = gameObject.GetComponent<PhantomObject>();
        if (get == null)
        {
            get = gameObject.AddComponent<PhantomObject>();
            
        }
        
        get.SetUp(_PhantomConfig);
        get.SetPhantomMode(true,true);
        return get;
    }
    public void UnPhantomizeObject(GameObject gameObject)
    {
        var get = gameObject.GetComponent<PhantomObject>();
        if (get != null)
        {
            get.UnPhantom();
            GameObject.DestroyImmediate(get);
        }
    }
    public void SetProgress(GameObject gameObject, float progress)
    {
        var get = gameObject.GetComponent<PhantomObject>();
        if (get == null)
        {
            get= gameObject.AddComponent<PhantomObject>();
        }
        
        
        get.SetProgress(progress);
    }
    public PhantomObject DemolitionObject(GameObject gameObject,bool Demolition)
    {
        var get = gameObject.GetComponent<PhantomObject>();
        if (get == null)
        {
            get= gameObject.AddComponent<PhantomObject>();
        }
        
        
        get.SetUp(_PhantomConfig);
        get.SetPhantomMode(Demolition,false);
        return get;
    }
}
