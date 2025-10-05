using System;
using System.Collections.Generic;

[Serializable]
public class VirtualLogisticsCenterData
{
    public string UnicID;
    public Dictionary<string, int> items;
    public Dictionary<string, RoadData> roads;
}