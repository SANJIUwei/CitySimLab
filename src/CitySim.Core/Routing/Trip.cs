using System.Collections.Generic;

namespace CitySim.Core;

public sealed class GlobalPath
{
    public static readonly GlobalPath None = new GlobalPath(new List<long>(), double.PositiveInfinity);

    public static GlobalPath LocalOnly(double cost) => new GlobalPath(new List<long>(), cost);

    public List<long> NodeIds { get; }
    public double TotalCost { get; }
    public bool Found => !double.IsPositiveInfinity(TotalCost);

    public GlobalPath(List<long> nodeIds, double totalCost)
    {
        NodeIds = nodeIds;
        TotalCost = totalCost;
    }
}

// 一次出行：两端是 RouteEnd，不是建筑。路上的车可以从路上某点出发。
public sealed class Trip
{
    public RouteEnd From { get; }
    public RouteEnd To { get; }
    public bool SameSegment { get; }
    public GlobalPath Global { get; }
    public WorldPosition FromOnRoad { get; }
    public WorldPosition ToOnRoad { get; }
    public LocalDeparture Departure { get; }
    public LocalApproach Approach { get; }

    public bool Reachable => SameSegment || Global.Found;

    public Trip(
        RouteEnd from,
        RouteEnd to,
        bool sameSegment,
        GlobalPath global,
        WorldPosition fromOnRoad,
        WorldPosition toOnRoad,
        LocalDeparture departure,
        LocalApproach approach)
    {
        From = from;
        To = to;
        SameSegment = sameSegment;
        Global = global;
        FromOnRoad = fromOnRoad;
        ToOnRoad = toOnRoad;
        Departure = departure;
        Approach = approach;
    }
}
