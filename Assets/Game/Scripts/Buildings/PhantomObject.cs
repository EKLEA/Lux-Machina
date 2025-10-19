using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PhantomObject : MonoBehaviour
{
    List<MeshRenderer> meshRenderers = new();
    List<Material[]> originalMaterials = new();
    List<Material[]> phantomMaterials = new();
    
    Material _trueMat;
    Material _falseMat;
    
    public void SetUp(Material trueMat, Material falseMat)
    {
        meshRenderers.AddRange(GetComponentsInChildren<MeshRenderer>(true));
        _trueMat = trueMat;
        _falseMat = falseMat;

        foreach (MeshRenderer mr in meshRenderers)
        {
            if (mr == null) continue;
            
            Material[] originalMats = mr.sharedMaterials;
            Material[] phantomMats = new Material[originalMats.Length];
            
            Material[] originalCopies = new Material[originalMats.Length];
            for (int i = 0; i < originalMats.Length; i++)
            {
                if (originalMats[i] != null)
                    originalCopies[i] = originalMats[i];
            }
            originalMaterials.Add(originalCopies);
            
            for (int i = 0; i < originalMats.Length; i++)
            {
                if (originalMats[i] != null)
                    phantomMats[i] = CreatePhantomMaterial(originalMats[i], _trueMat);
            }
            
            mr.materials = phantomMats;
            phantomMaterials.Add(phantomMats);
        }
    }
    
    private Material CreatePhantomMaterial(Material originalMat, Material phantomShaderMat)
    {
        if (originalMat == null || phantomShaderMat == null) 
            return null;
            
        Material newMat = new Material(phantomShaderMat);
        
        if (originalMat.HasProperty("_MainTex") && newMat.HasProperty("_MainTex") && originalMat.mainTexture != null)
        {
            newMat.SetTexture("_MainTex", originalMat.mainTexture);
        }
        // Для URP
        else if (originalMat.HasProperty("_BaseMap") && newMat.HasProperty("_BaseMap"))
        {
            Texture baseMap = originalMat.GetTexture("_BaseMap");
            if (baseMap != null)
                newMat.SetTexture("_BaseMap", baseMap);
        }
        
        return newMat;
    }
    
    public void CanBuild(bool canBuild)
    {
        Material targetPhantomMat = canBuild ? _trueMat : _falseMat;
        
        for (int mrIndex = 0; mrIndex < meshRenderers.Count; mrIndex++)
        {
            if (meshRenderers[mrIndex] == null) continue;
            
            Material[] currentPhantomMats = phantomMaterials[mrIndex];
            Material[] originalMats = originalMaterials[mrIndex];
            Material[] newMats = new Material[currentPhantomMats.Length];
            
            for (int i = 0; i < currentPhantomMats.Length; i++)
            {
                if (originalMats[i] != null)
                {
                    newMats[i] = CreatePhantomMaterial(originalMats[i], targetPhantomMat);
                    
                    // Уничтожаем старый фантомный материал
                    if (currentPhantomMats[i] != null)
                    {
                        if (Application.isPlaying)
                            Destroy(currentPhantomMats[i]);
                        else
                            DestroyImmediate(currentPhantomMats[i]);
                    }
                }
            }
            
            phantomMaterials[mrIndex] = newMats;
            meshRenderers[mrIndex].materials = newMats;
        }
    }
    
    public void UnPhantom()
    {
        for (int i = 0; i < meshRenderers.Count; i++)
        {
            if (meshRenderers[i] != null)
            {
                // Восстанавливаем оригинальные материалы
                meshRenderers[i].sharedMaterials = originalMaterials[i];
            }
            
            // Уничтожаем фантомные материалы
            foreach (Material mat in phantomMaterials[i])
            {
                if (mat != null)
                {
                    if (Application.isPlaying)
                        Destroy(mat);
                    else
                        DestroyImmediate(mat);
                }
            }
        }
        
        if (Application.isPlaying)
            Destroy(this);
        else
            DestroyImmediate(this);
    }
}