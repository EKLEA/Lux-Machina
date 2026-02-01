using System;
using UnityEngine;
using Zenject;

public class VisualBuildingFactory
{
    private readonly IReadOnlyMaterialInfo _materialInfo;

    public VisualBuildingFactory(IReadOnlyMaterialInfo materialInfo)
    {
        _materialInfo = materialInfo;
    }
    public PhantomObject PhantomizeObject(GameObject gameObject)
    {
        var get = gameObject.GetComponent<PhantomObject>();
        if (get == null)
        {
            var ph = gameObject.AddComponent<PhantomObject>();
            ph.SetUp(_materialInfo.MaterialInfos["True"], _materialInfo.MaterialInfos["False"],_materialInfo.MaterialInfos["Force"]);
            return ph;
        }
        else
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
    public PhantomObject DemolitionObject(GameObject gameObject)
    {
        return null;
    }
     public void UnDemolitionObject(GameObject gameObject)
    {
        var get = gameObject.GetComponent<PhantomObject>();
        if (get != null)
        {
            get.UnPhantom();
            GameObject.DestroyImmediate(get);
        }
    }
}
