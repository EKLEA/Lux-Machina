using UnityEngine;
public class LuxBall : MonoBehaviour
{
    public Renderer _renderer;
    public void ChangeColor(Color color,Color puilseColor)
    {
        _renderer.material.SetColor("_Color", color);
        _renderer.material.SetColor("_PulseColor", puilseColor);
    }
}  