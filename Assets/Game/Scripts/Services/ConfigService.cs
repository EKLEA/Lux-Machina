using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;


public class ConfigService : IReadOnlyBuildingInfo, IReadOnlyItemsInfo, IReadOnlyUIInfo,IReadOnlyMaterialInfo
{
    public Dictionary<int, FeatureInfo> ItemsInfos => featureInfos.Where(f => f.featureType == FeatureType.Item).ToDictionary(f => f.id.GetHashCode(), f => f);

    public Dictionary<int, BuildingInfo> BuildingInfos => featureInfos.Where(f => f.featureType == FeatureType.Building).ToDictionary(f => f.id.GetHashCode(), f => f as BuildingInfo);
    public Dictionary<int, FeatureInfo> UIInfos => featureInfos.Where(f => f.featureType == FeatureType.UI).ToDictionary(f => f.id.GetHashCode(), f => f);
    public Dictionary<string, MaterialInfo> MaterialInfos{ get; private set; }
    private FeatureInfo[] featureInfos;
    public async Task LoadConfigs()
    {
        MaterialInfos = Resources.LoadAll<MaterialInfo>("Configs/").ToDictionary(f => f.id, f => f);
        featureInfos = Resources.LoadAll<FeatureInfo>("Configs/");
        await Task.Yield();
    }
}
public interface IReadOnlyBuildingInfo
{
    public Dictionary<int,BuildingInfo> BuildingInfos { get; }
}
public interface IReadOnlyItemsInfo
{
    public Dictionary<int, FeatureInfo> ItemsInfos { get; }
}
public interface IReadOnlyUIInfo
{
    public Dictionary<int, FeatureInfo> UIInfos { get; }
}
public interface IReadOnlyMaterialInfo
{
    public Dictionary<string, MaterialInfo> MaterialInfos { get; }
}