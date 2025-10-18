using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PhantomObject : MonoBehaviour
{
    List<MeshRenderer> meshRenderers=new();
    List<Material[]> Materials = new();
    Material _trueMat;
    Material _falseMat;
    public void SetUp(Material trueMat,Material falseMat)
    {
        meshRenderers.AddRange(GetComponentsInChildren<MeshRenderer>(true));//.Where(f=>f.tag!="NotChangeMat")
        _trueMat = trueMat;
        _falseMat = falseMat;

        foreach (MeshRenderer mr in meshRenderers)
        {
            Material[] mats = mr.materials;
            Material[] matsCopy = new Material[mats.Length];

            for (int i = 0; i < mats.Length; i++)
            {
                matsCopy[i] = Instantiate(mats[i]);
                mats[i] = _trueMat;
            }
            mr.materials = mats;
            Materials.Add(matsCopy);
        }
    }
    public void CanBuild(bool CanBuild)
    {
        Material newMaterial = CanBuild ? _trueMat : _falseMat;
		foreach (MeshRenderer meshRenderer in meshRenderers)
		{
			Material[] mats = meshRenderer.materials;
			for (int i = 0; i < mats.Length; i++)
				mats[i] = newMaterial;
			
			meshRenderer.materials = mats;
		}
    }
    public void UnPhantom()
    {
        for (int i = 0; i < meshRenderers.Count; i++)
        {
            meshRenderers[i].materials = Materials[i];
        }
        DestroyImmediate(this);
    }
}
