using UnityEngine;

public class SunController : MonoBehaviour
{

    [Header("Lights")]
    public Light sunLight;
    public Light moonLight;
    public AnimationCurve intensityCurve; 
    public AnimationCurve moonCurve; 

    [Header("Skybox Settings")]
    public Gradient ambientColor; 
    public AnimationCurve skyExposure; 


    public void UpdateVisuals(bool isDay, float localProgress)
    {
        if (sunLight == null || moonLight == null) return;

        if (isDay)
        {
            sunLight.transform.localRotation = Quaternion.Euler(localProgress * 180f, 180f, 0f);
            sunLight.intensity = intensityCurve.Evaluate(localProgress);

            moonLight.transform.localRotation = Quaternion.Euler(-90f, 180f, 0f);
            moonLight.intensity = 0;
        }
        else
        {
            moonLight.transform.localRotation = Quaternion.Euler(localProgress * 180f, 180f, 0f);
            moonLight.intensity = moonCurve.Evaluate(localProgress);

            sunLight.transform.localRotation = Quaternion.Euler(-90f, 180f, 0f);
            sunLight.intensity = 0;
        }

        RenderSettings.ambientLight = ambientColor.Evaluate(localProgress); 

        if (RenderSettings.skybox != null)
        {
            RenderSettings.skybox.SetFloat("_Exposure", skyExposure.Evaluate(localProgress));
        }
    }
}

