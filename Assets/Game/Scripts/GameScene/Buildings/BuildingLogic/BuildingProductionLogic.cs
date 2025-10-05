public class BuildingProductionLogic : BuildingLogic,IResourceProducer
{
    public BuildingProductionLogic(BuildingLogicData data, BuildingOnScene buildingOnScene) : base(data, buildingOnScene)
    {
    }

    public override void CheckAndRegisterRequests()
    {
        throw new System.NotImplementedException();
    }

    public int GetResourceAmount(string resourceType)
    {
        throw new System.NotImplementedException();
    }

    public string[] GetResources()
    {
        throw new System.NotImplementedException();
    }

    public bool Has(string resourceType)
    {
        throw new System.NotImplementedException();
    }

    public override void LogicPerTick(TickableEvent tickEvent)
    {
        throw new System.NotImplementedException();
    }

    public override void ReceiveResources(string resourceType, int amount)
    {
        throw new System.NotImplementedException();
    }

    public void RemoveResources(string resourceType, int amount)
    {
        throw new System.NotImplementedException();
    }
}