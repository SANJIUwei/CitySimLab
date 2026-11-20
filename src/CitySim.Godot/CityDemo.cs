using System;
using System.Collections.Generic;
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
    public IReadOnlyList<WorldPosition> Route { get; private set; } = Array.Empty<WorldPosition>();
    public IReadOnlyList<PathSearchStep> SearchSteps => Traveler.Route?.Global.SearchSteps ?? Array.Empty<PathSearchStep>();
    public GlobalPath? Baseline { get; private set; }
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

                AddRoad(nodes[col, row], nodes[col + 1, row], row == 1 ? 0.78 : 1.0);
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
        });

        // Decorative land-use markers are not routing objects.
        Blocks = Array.AsReadOnly(new[]
        {
            new DemoBlock("口袋公园", At(602, 185), 118, 78),
            new DemoBlock("滨水绿地", At(790, 193), 102, 77),
            new DemoBlock("步行广场", At(409, 327), 104, 73),
            new DemoBlock("社区花园", At(411, 461), 97, 77),
        });

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

        var origin = Buildings[originIndex].Building;
        var destination = Buildings[destinationIndex].Building;
        var traveler = new Thing();
        if (!TripStarter.Request(traveler, _pathCenter, origin, destination))
            return false;

        _pathCenter.Process(1);
        if (traveler.Route is not { Reachable: true } trip)
            return false;

        var points = new List<WorldPosition> { traveler.Position };
        while (traveler.Advance())
            points.Add(traveler.Position);

        // Reuse the computed trip, not a second search. Leave the live Thing at its origin.
        traveler.PrepareTravel(Network);
        Traveler = traveler;
        Route = points.AsReadOnly();
        // Optional, demo-only comparison on the exact same graph endpoints.
        var ids = trip.Global.NodeIds;
        Baseline = ids.Count > 0 ? Network.FindPath(ids[0], ids[^1], PathAlgorithm.Dijkstra, captureSearch: true) : null;
        OriginIndex = originIndex;
        DestinationIndex = destinationIndex;
        return true;
    }

    private void AddRoad(RoadNode start, RoadNode end, double weight)
    {
        var cost = WorldPosition.Distance(start.Position, end.Position) * weight;
        var segment = Network.AddSegment(start, end, cost);
        segment.AddLane(LaneDirection.Forward);
        segment.AddLane(LaneDirection.Reverse);
    }

    private DemoBuilding MakeBuilding(string name, int x, int y, int width, int height, int doorX, int doorY)
    {
        var building = new Building(At(x, y), At(doorX, doorY), Network);
        return new DemoBuilding(name, building, width, height);
    }

    private static WorldPosition At(float x, float y) => new() { x = x, y = y, z = 0 };
}

public sealed record DemoBuilding(string Name, Building Building, float Width, float Height);
public sealed record DemoBlock(string Name, WorldPosition Position, float Width, float Height);
