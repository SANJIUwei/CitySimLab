using System.Diagnostics;
using Xunit.Abstractions;

namespace CitySim.Tests;

public sealed class RoadNetworkTests
{
    readonly ITestOutputHelper _output;

    public RoadNetworkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void FindPath_PrefersLowerCostDetour()
    {
        var net = new RoadNetwork();
        var a = new RoadNode(new World.WorldPosition { x = 0, y = 0, z = 0 }, 1);
        var b = new RoadNode(new World.WorldPosition { x = 1, y = 0, z = 0 }, 2);
        var c = new RoadNode(new World.WorldPosition { x = 2, y = 0, z = 0 }, 3);
        net.AddSegment(a, b, cost: 100);
        net.AddSegment(a, c, cost: 1);
        net.AddSegment(c, b, cost: 1);

        var path = net.FindPath(1, 2);

        Assert.True(path.Found);
        Assert.Equal(new long[] { 1, 3, 2 }, path.NodeIds);
        Assert.Equal(2.0, path.TotalCost);
    }

    [Fact]
    public void BuildingToBuilding_UsesAttachedSegmentEndpointsThenGlobalPath()
    {
        var net = new RoadNetwork();
        var n1 = new RoadNode(new World.WorldPosition { x = 0, y = 0, z = 0 }, 1);
        var n2 = new RoadNode(new World.WorldPosition { x = 10, y = 0, z = 0 }, 2);
        var n3 = new RoadNode(new World.WorldPosition { x = 20, y = 0, z = 0 }, 3);
        var s12 = net.AddSegment(n1, n2, 10);
        var s23 = net.AddSegment(n2, n3, 10);

        var houseA = new Building(
            new World.WorldPosition { x = 1, y = 2, z = 0 },
            new World.WorldPosition { x = 1, y = 0, z = 0 });
        var houseB = new Building(
            new World.WorldPosition { x = 19, y = 2, z = 0 },
            new World.WorldPosition { x = 19, y = 0, z = 0 });
        houseA.AttachTo(s12, 0.1f);
        houseB.AttachTo(s23, 0.9f);

        Assert.Equal(1, net.FindEntryNode(houseA).ID);
        Assert.Equal(3, net.FindEntryNode(houseB).ID);

        var path = net.FindPath(houseA, houseB);
        Assert.Equal(new long[] { 1, 2, 3 }, path.NodeIds);
        Assert.Equal(20.0, path.TotalCost);

        var onRoad = s12.PointAt(0.1f);
        Assert.Equal(1f, onRoad.x, 3);
    }

    [Fact]
    public void PlanTrip_SameSegment_StaysLocalAndDoesNotEnterGlobalGraph()
    {
        var net = new RoadNetwork();
        var n1 = new RoadNode(new World.WorldPosition { x = 0, y = 0, z = 0 }, 1);
        var n2 = new RoadNode(new World.WorldPosition { x = 10, y = 0, z = 0 }, 2);
        var segment = net.AddSegment(n1, n2, 10);

        var houseA = new Building(
            new World.WorldPosition { x = 1, y = 2, z = 0 },
            new World.WorldPosition { x = 1, y = 0, z = 0 });
        var houseB = new Building(
            new World.WorldPosition { x = 9, y = 2, z = 0 },
            new World.WorldPosition { x = 9, y = 0, z = 0 });
        houseA.AttachTo(segment, 0.1f);
        houseB.AttachTo(segment, 0.9f);

        var trip = net.PlanTrip(houseA, houseB);

        Assert.True(trip.SameSegment);
        Assert.True(trip.Reachable);
        Assert.Empty(trip.Global.NodeIds);
        Assert.Equal(8.0, trip.Global.TotalCost, 5);
        Assert.Equal(1f, trip.FromOnRoad.x, 3);
        Assert.Equal(9f, trip.ToOnRoad.x, 3);
    }

    [Fact]
    public void PlanTrip_DifferentSegments_UsesEntryNodesThenGlobalPath()
    {
        var net = new RoadNetwork();
        var n1 = new RoadNode(new World.WorldPosition { x = 0, y = 0, z = 0 }, 1);
        var n2 = new RoadNode(new World.WorldPosition { x = 10, y = 0, z = 0 }, 2);
        var n3 = new RoadNode(new World.WorldPosition { x = 20, y = 0, z = 0 }, 3);
        var s12 = net.AddSegment(n1, n2, 10);
        var s23 = net.AddSegment(n2, n3, 10);

        var houseA = new Building(
            new World.WorldPosition { x = 1, y = 2, z = 0 },
            new World.WorldPosition { x = 1, y = 0, z = 0 });
        var houseB = new Building(
            new World.WorldPosition { x = 19, y = 2, z = 0 },
            new World.WorldPosition { x = 19, y = 0, z = 0 });
        houseA.AttachTo(s12, 0.1f);
        houseB.AttachTo(s23, 0.9f);

        var trip = net.PlanTrip(houseA, houseB);

        Assert.False(trip.SameSegment);
        Assert.Equal(new long[] { 1, 2, 3 }, trip.Global.NodeIds);
        Assert.Equal(20.0, trip.Global.TotalCost);
        Assert.Same(n1, trip.Departure.EntryNode);
        Assert.Same(n3, trip.Approach.ExitNode);
    }

    [Fact]
    public void FindEntryNode_UsesCachedTNotStraightLineToEntrance()
    {
        var net = new RoadNetwork();
        var n1 = new RoadNode(new World.WorldPosition { x = 0, y = 0, z = 0 }, 1);
        var n2 = new RoadNode(new World.WorldPosition { x = 10, y = 0, z = 0 }, 2);
        var segment = net.AddSegment(n1, n2, 10);
        var house = new Building(
            new World.WorldPosition { x = 1, y = 0, z = 0 },
            new World.WorldPosition { x = 9, y = 0, z = 0 });
        house.AttachTo(segment, 0.1f);

        Assert.Equal(1, net.FindEntryNode(house).ID);
    }

    [Fact]
    public void LengthBetween_UsesGeometryNotPathCost()
    {
        var start = new RoadNode(new World.WorldPosition { x = 0, y = 0, z = 0 }, 1);
        var end = new RoadNode(new World.WorldPosition { x = 10, y = 0, z = 0 }, 2);
        var segment = new RoadSegment(start, end, cost: 100);

        Assert.Equal(10.0, segment.Length, 5);
        Assert.Equal(100.0, segment.Cost);
        Assert.Equal(8.0, segment.LengthBetween(0.1f, 0.9f), 5);
    }

    [Fact]
    public void FindPath_GridPerformanceSnapshot()
    {
        const int n = 40;
        var net = BuildGrid(n);
        int queries = 50;
        var sw = Stopwatch.StartNew();
        int found = 0;
        for (int i = 0; i < queries; i++)
        {
            long from = 1;
            long to = n * n;
            if (net.FindPath(from, to).Found)
                found++;
        }
        sw.Stop();
        double msEach = sw.Elapsed.TotalMilliseconds / queries;
        _output.WriteLine($"grid={n}x{n} nodes={n * n} queries={queries} found={found} avgMs={msEach:F3} totalMs={sw.Elapsed.TotalMilliseconds:F1}");
        Assert.Equal(queries, found);
        Assert.True(msEach < 50, $"avg {msEach:F3} ms per query is too slow for this grid size");
    }

    static RoadNetwork BuildGrid(int n)
    {
        var net = new RoadNetwork();
        var nodes = new RoadNode[n, n];
        long id = 1;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                nodes[x, y] = new RoadNode(new World.WorldPosition { x = x, y = y, z = 0 }, id++);
                net.AddNode(nodes[x, y]);
            }
        }
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                if (x + 1 < n) net.AddSegment(nodes[x, y], nodes[x + 1, y], 1);
                if (y + 1 < n) net.AddSegment(nodes[x, y], nodes[x, y + 1], 1);
            }
        }
        return net;
    }
}
