using System;
using System.Collections.Generic;

namespace CitySim.Core;

public enum PathAlgorithm { Alt, Dijkstra }

public readonly struct PathRelaxation
{
    public long NodeId { get; }
    public double Cost { get; }

    internal PathRelaxation(long nodeId, double cost)
    {
        NodeId = nodeId;
        Cost = cost;
    }
}

public sealed class PathSearchStep
{
    public long NodeId { get; }
    public double Cost { get; }
    public double Priority { get; }
    public IReadOnlyList<PathRelaxation> Improvements { get; }

    internal PathSearchStep(long nodeId, double cost, double priority, IReadOnlyList<PathRelaxation> improvements)
    {
        NodeId = nodeId;
        Cost = cost;
        Priority = priority;
        Improvements = improvements;
    }
}

public sealed class GlobalPath
{
    public static readonly GlobalPath None = new GlobalPath(new List<long>(), double.PositiveInfinity);

    public static GlobalPath LocalOnly(double cost) => new GlobalPath(new List<long>(), cost);

    public List<long> NodeIds { get; }
    public double TotalCost { get; }
    public bool Found => !double.IsPositiveInfinity(TotalCost);

    public RouteFailure Failure { get; }
    public IReadOnlyList<RoadLane> Lanes { get; }
    public static GlobalPath Failed(RouteFailure reason, PathAlgorithm? algorithm = null) =>
        new(new List<long>(), double.PositiveInfinity, algorithm, failure: reason);

    public PathAlgorithm? Algorithm { get; }
    public IReadOnlyList<PathSearchStep> SearchSteps { get; }

    public GlobalPath(List<long> nodeIds, double totalCost, PathAlgorithm? algorithm = null,
        IReadOnlyList<PathSearchStep>? searchSteps = null, IReadOnlyList<RoadLane>? lanes = null,
        RouteFailure failure = RouteFailure.None)
    {
        NodeIds = nodeIds;
        TotalCost = totalCost;
        Algorithm = algorithm;
        Lanes = lanes ?? Array.Empty<RoadLane>();
        Failure = Found ? RouteFailure.None : failure == RouteFailure.None ? RouteFailure.NoLegalRoute : failure;
        SearchSteps = searchSteps ?? Array.Empty<PathSearchStep>();
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

    public bool Reachable => Global.Found;
    public RouteFailure Failure => Global.Failure;
    // 路段完整/部分代价和转向等待，单位随地图的路段权重；不混入楼门到路边的米数。
    public double TotalCost { get; }
    public RoadLane? DepartureLane { get; }
    public RoadLane? ArrivalLane { get; }
    internal RoadNetwork? PlannedNetwork { get; set; }
    internal long PlannedTopology { get; set; }
    internal TravelOptions? PlannedTravel { get; set; }

    public Trip(
        RouteEnd from,
        RouteEnd to,
        bool sameSegment,
        GlobalPath global,
        WorldPosition fromOnRoad,
        WorldPosition toOnRoad,
        LocalDeparture departure,
        LocalApproach approach, double? totalCost = null, RoadLane? departureLane = null, RoadLane? arrivalLane = null)
    {
        From = from;
        To = to;
        SameSegment = sameSegment;
        Global = global;
        FromOnRoad = fromOnRoad;
        ToOnRoad = toOnRoad;
        Departure = departure;
        Approach = approach;
        TotalCost = totalCost ?? global.TotalCost;
        DepartureLane = departureLane;
        ArrivalLane = arrivalLane;
    }
}
