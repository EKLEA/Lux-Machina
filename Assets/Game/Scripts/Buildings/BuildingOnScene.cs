using UnityEngine;

public class BuildingOnScene : MonoBehaviour
{
    public int id;
    public int clusterID = -1;
    public Renderer clusterIndicator; 
    
    public virtual void SetCluster(int newClusterID, Color clusterColor)
    {
        clusterID = newClusterID;
        
        if (clusterIndicator != null)
        {
            clusterIndicator.material.color = clusterColor;
            clusterIndicator.enabled = (clusterID != -1);
        }
        
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
}