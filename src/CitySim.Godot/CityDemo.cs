using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Core;

namespace CitySim.Godot;

/// <summary>Hand-authored demo data and the adapter to the existing trip pipeline.</summary>
public sealed class CityDemo
{
    public const int MapWidth = 1000;
    public const int MapHeight = 650;

    public RoadNetwork Network { get; } = new();
    public IReadOnlyList<DemoBuilding> Buildings { get; }
    public IReadOnlyList<DemoBlock> Blocks { get; }
    public IReadOnlyList<Building> AllBuildings { get; }
    public const int FactoryIndex = 6;
    public const int WarehouseIndex = 7;
    public IReadOnlyList<WorldPosition> Route { get; private set; } = Array.Empty<WorldPosition>();
    public IReadOnlyList<PathSearchStep> SearchSteps => Traveler.Route?.Global.SearchSteps ?? Array.Empty<PathSearchStep>();
    public GlobalPath? Baseline { get; private set; }
    public double? BaselineTripCost { get; private set; }
    public RouteFailure LastFailure { get; private set; }
    public RoadNode? SignalJunction { get; private set; }
    public Thing Traveler { get; private set; } = new();
    public int OriginIndex { get; private set; }
    public int DestinationIndex { get; private set; } = 1;
    public WorldPosition Origin => Buildings[OriginIndex].Building.Entrance;
    public WorldPosition Destination => Buildings[DestinationIndex].Building.Entrance;

    private readonly PathCenter _pathCenter;

    public CityDemo()
    {
        int[] xs = { 135, 305, 500, 690, 865 };
        int[] ys = { 125, 255, 390, 530 };
        var nodes = new RoadNode[xs.Length, ys.Length];
        for (var row = 0; row < ys.Length; row++)
        {
            for (var col = 0; col < xs.Length; col++)
                nodes[col, row] = new RoadNode(At(xs[col], ys[row]), row * xs.Length + col + 1);
        }

        for (var row = 0; row < ys.Length; row++)
        {
            for (var col = 0; col < xs.Length - 1; col++)
            {
                if ((row == 1 && col == 3) || (row == 2 && col == 0))
                    continue;

                AddRoad(nodes[col, row], nodes[col + 1, row], row == 1 ? 0.78 : 1.0, oneWay: row == 0 && col == 0);
            }
        }

        for (var col = 0; col < xs.Length; col++)
        {
            for (var row = 0; row < ys.Length - 1; row++)
            {
                if ((col == 1 && row == 0) || (col == 3 && row == 2))
                    continue;

                AddRoad(nodes[col, row], nodes[col, row + 1], col == 2 ? 0.82 : 1.05);
            }
        }

        Buildings = Array.AsReadOnly(new[]
        {
            MakeBuilding("居民公寓", 209, 186, 74, 54, 209, 159),
            MakeBuilding("市政大厅", 788, 467, 94, 56, 788, 495),
            MakeBuilding("社区诊所", 413, 187, 68, 42, 413, 208),
            MakeBuilding("市集", 594, 315, 90, 56, 549, 315),
            MakeBuilding("社区学校", 207, 462, 88, 52, 207, 488),
            MakeBuilding("公交车站", 783, 327, 72, 46, 783, 350),
            MakeBuilding("钢材工厂", 397, 453, 101, 55, 398, 480),
            MakeBuilding("货运仓库", 713, 184, 95, 48, 699, 168),
        });

        AllBuildings = Array.AsReadOnly(Buildings.Select(entry => entry.Building).ToArray());

        // Decorative land-use markers are not routing objects.
        Blocks = Array.AsReadOnly(new[]
        {
            new DemoBlock("口袋公园", At(602, 185), 118, 78),
            new DemoBlock("滨水绿地", At(790, 193), 102, 77),
            new DemoBlock("步行广场", At(409, 327), 104, 73),
            new DemoBlock("社区花园", At(411, 461), 97, 77),
        });

        InstallDemoSignal();
        _pathCenter = new PathCenter(Network, captureSearch: true);
        if (!Restart())
            throw new InvalidOperationException("The demo's default trip must have a route.");
    }

    /// <summary>Plans the selected trip and rebuilds its trace. Invalid indices leave the last trip intact.</summary>
    public bool Restart(int originIndex = 0, int destinationIndex = 1)
    {
        if ((uint)originIndex >= (uint)Buildings.Count ||
            (uint)destinationIndex >= (uint)Buildings.Count ||
            originIndex == destinationIndex)
            return false;

        OriginIndex = originIndex;
        DestinationIndex = destinationIndex;
        LastFailure = RouteFailure.None;
        Route = Array.Empty<WorldPosition>();
        Baseline = null;
        BaselineTripCost = null;
        var origin = Buildings[originIndex].Building;
        var destination = Buildings[destinationIndex].Building;
        var traveler = new Thing();
        Traveler = traveler;
        var travel = new TravelOptions(originIndex == FactoryIndex || destinationIndex == FactoryIndex
            ? VehicleKind.Truck : VehicleKind.Car);
        if (!TripStarter.Request(traveler, _pathCenter, origin, destination, travel: travel))
        {
            LastFailure = RouteFailure.NoBuildingAccess;
            return false;
        }

        _pathCenter.Process(1);
        if (traveler.Route is not { Reachable: true } trip)
        {
            LastFailure = traveler.Route?.Failure ?? RouteFailure.NoLegalRoute;
            return false;
        }

        var points = new List<WorldPosition> { traveler.Position };
        while (traveler.Advance())
            points.Add(traveler.Position);

        // Reuse the computed trip, not a second search. Leave the live Thing at its origin.
        traveler.PrepareTravel(Network);
        Route = points.AsReadOnly();
        // Compare on the same complete request, including access and turns.
        var baseline = Network.PlanTrip(origin, destination, PathAlgorithm.Dijkstra, captureSearch: true, travel: travel);
        Baseline = baseline.Global;
        BaselineTripCost = baseline.TotalCost;
        return true;
    }

    public void RecheckBuildingAccess()
    {
        foreach (var building in AllBuildings)
            if (building.Attachment != null && !building.HasActiveAttachment(Network))
                building.AttachNearest(Network, 85);
    }

    private void InstallDemoSignal()
    {
        var prototype = Network.PlanTrip(Buildings[0].Building, Buildings[1].Building, PathAlgorithm.Dijkstra);
        if (prototype.Global.NodeIds.Count < 2 ||
            !Network.TryGetNode(prototype.Global.NodeIds[1], out var junction))
            return;

        var lanes = Network.Segments.SelectMany(segment => segment.Lanes).ToArray();
        var phases = lanes.Where(lane => lane.ToId == junction.ID)
            .Select(incoming => lanes.Where(outgoing => outgoing.FromId == junction.ID && outgoing.ToId != incoming.FromId)
                .Select(outgoing => new SignalMovement(incoming, outgoing)).ToArray())
            .Where(movements => movements.Length > 0)
            // Timings belong to this demonstration, not a local engineering standard.
            .Select(movements => new SignalPhase(3, 1, .5, movements)).ToArray();
        if (phases.Length == 0) return;

        Network.SetSignalPlan(junction, new SignalPlan(phases, offsetSeconds: 1));
        SignalJunction = junction;
    }

    private void AddRoad(RoadNode start, RoadNode end, double weight, bool oneWay = false)
    {
        var cost = WorldPosition.Distance(start.Position, end.Position) * weight;
        var segment = Network.AddSegment(start, end, cost);
        segment.AddLane(LaneDirection.Forward);
        if (!oneWay) segment.AddLane(LaneDirection.Reverse);
    }

    private DemoBuilding MakeBuilding(string name, int x, int y, int width, int height, int doorX, int doorY)
    {
        var building = new Building(At(x, y), At(doorX, doorY));
        building.AttachNearest(Network, 85);
        return new DemoBuilding(name, building, width, height);
    }

    private static WorldPosition At(float x, float y) => new() { x = x, y = y, z = 0 };
}

public sealed record DemoBuilding(string Name, Building Building, float Width, float Height);
public sealed record DemoBlock(string Name, WorldPosition Position, float Width, float Height);
