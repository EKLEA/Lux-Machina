using UnityEngine;
[RequireComponent(typeof(LineRenderer))]
public class EnergyLine : MonoBehaviour
{
    public  LineRenderer lineRenderer;
    public void ChangeColor(Color color,Color puilseColor)
    {
        lineRenderer.material.SetColor("_Color", color);
        lineRenderer.material.SetColor("_PulseColor", puilseColor);
    }
}