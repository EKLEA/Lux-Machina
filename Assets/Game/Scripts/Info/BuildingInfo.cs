using UnityEngine;

[CreateAssetMenu(menuName = "BuildingInfo")]
public class BuildingInfo : FeatureInfo
{
	public BuildingOnScene prefab;
	public BuildingsTypes buildingType;
	public ActionType actionType;
	public TypeOfLogic typeOfLogic;
	public Vector3Int size;
	public float MaxHealth;
	public float TimeToStartRestore;
	public float RestoreHealthPerSecond;
}
public enum TypeOfLogic
{
	None,
	Production,
	Defence
}
public enum BuildingsTypes
{
	Production,
	Enegry,
	Logistic,
	Defence
}
public enum ActionType
{
    Building,
    TwoPointBuilding
}