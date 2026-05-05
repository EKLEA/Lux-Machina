using UnityEngine;

[CreateAssetMenu(menuName = "GameFieldSettings")]
public class GameFieldSettingsSO : ScriptableObject
{
    [Range(0, 5)]
    [SerializeField]
    public float cellSize;
    public Material chumkMat;

    [Range(10, 30)]
    [SerializeField]
    public int tickPerSecond;
    public int range;
    public DistributionPriority defaultDistributionPriority;
    public Color selectBuildingColor;
    public Color makeAsDemolitionBuidlingColor;
    public Color forceDestoryBuidlingColor;
    public PhantomConfig BluePrintPhantomConfig;
    public PhantomConfig DemolitionAndFalsePhantomConfig;
    public PhantomConfig ForceDestroyPhantomConfig;
    public LayerMask removeLayer;
    [ColorUsage(true, true)] 
    public Color ConnectionColor;
    [ColorUsage(true, true)] 
    public Color ConnectionPulseColor;
    [ColorUsage(true, true)] 
    public Color DisconnectColor;
    [ColorUsage(true, true)] 
    public Color DisconnectPulseColor;
    public EnergyLine EnergyLine;
}
