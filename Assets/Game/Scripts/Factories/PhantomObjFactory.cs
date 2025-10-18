using System;
using UnityEngine;
using Zenject;

public class PhantomObjectFactory
{
    [Inject] IReadOnlyMaterialInfo materialInfo;
    public PhantomObject PhantomizeObject(GameObject gameObject)
    {
        var get = gameObject.GetComponent<PhantomObject>();
        if (get == null)
        {
            var ph = gameObject.AddComponent<PhantomObject>();
            ph.SetUp(materialInfo.MaterialInfos["True"].material, materialInfo.MaterialInfos["False"].material);
            return ph;
        }
        else return get;
    }
    public void UnPhantomizeObject(GameObject gameObject)
    {
        var get = gameObject.GetComponent<PhantomObject>();
        if (get!=null)
        {
            get.UnPhantom();
            GameObject.DestroyImmediate(get);
        }
    }
}