	using System.Collections;
	using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;


public class ConfigService : IReadOnlyBuildingInfo, IReadOnlyItemsInfo, IReadOnlyUIInfo
{
    public Dictionary<string, FeatureInfo> ItemsInfos => featureInfos.Where(f => f.featureType == FeatureType.Item).ToDictionary(f => f.id, f => f);

    public Dictionary<string, BuildingInfo> BuildingInfos => featureInfos.Where(f => f.featureType == FeatureType.Building).ToDictionary(f => f.id, f => f as BuildingInfo);
    public Dictionary<string, FeatureInfo> UIInfos => featureInfos.Where(f => f.featureType == FeatureType.UI).ToDictionary(f => f.id, f => f);
    private FeatureInfo[] featureInfos;
    public async Task LoadConfigs()
    {

        var loadOperations = new[]
        {
            Resources.LoadAsync<FeatureInfo>("Configs/BuildingsConfig"),
            Resources.LoadAsync<FeatureInfo>("Configs/ItemsConfig"),
            Resources.LoadAsync<FeatureInfo>("Configs/UIConfig"),
        };

        foreach (var operation in loadOperations)
        {
            while (!operation.isDone)
            {
                await Task.Yield();
            }
        }
        featureInfos = new FeatureInfo[loadOperations.Length];
        for (int i = 0; i < loadOperations.Length; i++)
        {
            featureInfos[i] = loadOperations[i].asset as FeatureInfo;
        }
       
    }
}
public interface IReadOnlyBuildingInfo
{
    public Dictionary<string,BuildingInfo> BuildingInfos { get; }
}
public interface IReadOnlyItemsInfo
{
    public Dictionary<string, FeatureInfo> ItemsInfos { get; }
}
public interface IReadOnlyUIInfo
{
    public Dictionary<string, FeatureInfo> UIInfos { get; }
}