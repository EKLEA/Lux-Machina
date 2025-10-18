using UnityEngine;

[CreateAssetMenu(menuName = "Infos/BuildingInfo")]
public class BuildingInfo : FeatureInfo
{
	public BuildingOnScene prefab;
	public BuildingsTypes buildingType;
	public ActionType actionType;
	public Vector3Int size;
	public TypeOfLogic typeOfLogic;
	public RequiredRecipesGroup requiredRecipesGroup;
	public float MaxHealth;
	public float TimeToStartRestore;
	public float RestoreHealthPerSecond;
}
public enum RequiredRecipesGroup : int
{
	Smeleting,
	Processing
}
public enum TypeOfLogic
{
	None,
	Production,
	Consuming,
	Procession,
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