using System;
using Unity.Mathematics;
using UnityEngine;
[RequireComponent(typeof(Outline))]
public class BuildingOnScene : MonoBehaviour,IDisposable
{
    public int id;
    public int[] clusterID;
    public Renderer clusterIndicator;
    [SerializeField] protected Outline outline;

    public virtual void SetOutLine(Color? color)
    {
        if(!outline.SetUpded) outline.SetUp();
        if(color!=null)
        {
            outline.enabled=true;

            outline.OutlineColor=color.Value;
        }
        else 
            outline.enabled=false;
    }
   
    public void CreateClusterIndicator(float height = 2f)
    {
        var indicatorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        indicatorObject.name = "ClusterIndicator";
        indicatorObject.transform.SetParent(transform);
        indicatorObject.transform.localPosition = new Vector3(0, height, 0);
        indicatorObject.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        var renderer = indicatorObject.GetComponent<Renderer>();
        var material = new Material(Shader.Find("Standard"));
        material.color = Color.gray;
        renderer.material = material;

        clusterIndicator = renderer;
        clusterIndicator.enabled = false;
    }

    public void Dispose()
    {
       // throw new NotImplementedException();
    }
}