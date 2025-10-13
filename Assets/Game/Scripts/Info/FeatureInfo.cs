using UnityEngine;

public class FeatureInfo : ScriptableObject
{
	public Sprite icon;
	public string id;
	public string title;
	public string description;
	public FeatureType featureType;
}
public enum FeatureType
{
	Item,
	Building,
	UI
}