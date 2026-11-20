namespace CitySim.Tests;

public sealed class AltPathTests
{
    [Fact]
    public void DefaultAlt_AgreesWithDijkstraOnWeightedGrid()
    {
        const int size = 9;
        var net = new RoadNetwork();
        var grid = new RoadNode[size, size];
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            grid[x, y] = Node((y * size + x + 1) * 5L, x * 100, y * 100);
            if (x > 0 && (x + y) % 4 != 0)
                net.AddSegment(grid[x - 1, y], grid[x, y], (x + 2 * y) % 5 + 0.25);
            if (y > 0)
                net.AddSegment(grid[x, y - 1], grid[x, y], (2 * x + y) % 6 + 0.125);
        }

        Assert.Equal(4, net.PrepareLandmarks());
        var rng = new Random(691);
        for (int i = 0; i < 100; i++)
        {
            long from = grid[rng.Next(size), rng.Next(size)].ID;
            long to = grid[rng.Next(size), rng.Next(size)].ID;
            var expected = net.FindPath(from, to, PathAlgorithm.Dijkstra);
            var actual = net.FindPath(from, to);
            Assert.Equal(expected.Found, actual.Found);
            Assert.Equal(expected.TotalCost, actual.TotalCost, 9);
            if (from != to)
                Assert.Equal(PathAlgorithm.Alt, actual.Algorithm);
            Assert.Empty(actual.SearchSteps);
        }
    }

    [Fact]
    public void AddingCheapRoad_InvalidatesPreparedLandmarks()
    {
        var net = new RoadNetwork();
        var a = Node(1, 0, 0);
        var b = Node(2, 10, 0);
        var c = Node(3, 0, 10);
        var d = Node(4, 10, 10);
        net.AddSegment(a, b, 10);
        net.AddSegment(a, c, 2);
        net.AddSegment(c, d, 100);
        net.AddSegment(d, b, 100);
        Assert.Equal(4, net.PrepareLandmarks());
        Assert.Equal(10, net.FindPath(1, 2).TotalCost);

        net.AddSegment(c, b, 1);
        var result = net.FindPath(1, 2);
        Assert.Equal(new long[] { 1, 3, 2 }, result.NodeIds);
        Assert.Equal(3, result.TotalCost);
        Assert.Equal(net.FindPath(1, 2, PathAlgorithm.Dijkstra).TotalCost, result.TotalCost);
    }

    [Fact]
    public void LandmarkBoundsUseAdjacencyRatherThanMutableDisplayData()
    {
        var net = new RoadNetwork();
        var a = Node(1, 0, 0);
        var b = Node(2, 1, 0);
        var c = Node(3, 2, 0);
        var street = net.AddSegment(a, b, 1);
        net.AddSegment(b, c, 1);
        net.AddSegment(a, c, 5);
        street.Cost = 50;
        b.Position = new WorldPosition { x = 1_000_000 };
        Assert.Equal(3, net.PrepareLandmarks());
        Assert.Equal(2, net.FindPath(1, 3).TotalCost);
    }

    [Fact]
    public void MissingAndDisconnectedEndpoints_AndSameNodeBehaveDistinctly()
    {
        var net = new RoadNetwork();
        var a = Node(1, 0, 0);
        var b = Node(2, 1, 0);
        var c = Node(3, 2, 0);
        var d = Node(4, 3, 0);
        net.AddSegment(a, b, 1);
        net.AddSegment(c, d, 1);
        net.AddNode(Node(5, 4, 0));

        var unreachable = net.FindPath(1, 4, captureSearch: true);
        Assert.False(unreachable.Found);
        Assert.Equal(new long[] { 1, 2 }, unreachable.SearchSteps.Select(x => x.NodeId));
        Assert.False(net.FindPath(1, 5).Found);
        Assert.False(net.FindPath(1, 99).Found);
        var same = net.FindPath(5, 5, captureSearch: true);
        Assert.Equal(new long[] { 5 }, same.NodeIds);
        Assert.Equal(0, same.TotalCost);
        Assert.Equal(5L, Assert.Single(same.SearchSteps).NodeId);
        Assert.Null(same.Algorithm);
    }

    [Fact]
    public void TripPipeline_UsesAltAndReportsRealPriorities()
    {
        var net = new RoadNetwork();
        var a = Node(1, 0, 0);
        var b = Node(2, 1, 0);
        var c = Node(3, 2, 0);
        var from = RouteEnd.OnRoad(new RoadAttachment(net.AddSegment(a, b, 1), 0));
        var to = RouteEnd.OnRoad(new RoadAttachment(net.AddSegment(b, c, 2), 1));
        var center = new PathCenter(net, captureSearch: true);
        var thing = new Thing();

        Assert.True(TripStarter.Request(thing, center, from, to));
        Assert.Equal(1, center.Process(1));
        var global = Assert.IsType<Trip>(thing.Route).Global;
        Assert.Equal(PathAlgorithm.Alt, global.Algorithm);
        Assert.Contains(global.SearchSteps, step => step.NodeId == 2);
        Assert.True(global.SearchSteps[0].Priority > global.SearchSteps[0].Cost);
        Assert.Equal(thing.Route!.TotalCost, global.SearchSteps[^1].Priority, 8);
        Assert.Null(net.PlanTrip(from, from, captureSearch: true).Global.Algorithm);
    }

    [Fact]
    public void DijkstraTrace_SkipsStaleEntries_AndEqualsActualDistanceForPriority()
    {
        var net = new RoadNetwork();
        var a = Node(1, 0, 0);
        var b = Node(2, 1, 0);
        var c = Node(3, 2, 0);
        var d = Node(4, 3, 0);
        net.AddSegment(a, b, 8);
        net.AddSegment(a, c, 1);
        net.AddSegment(c, b, 2);
        net.AddSegment(b, d, 10);
        var result = net.FindPath(1, 4, PathAlgorithm.Dijkstra, captureSearch: true);
        Assert.Equal(PathAlgorithm.Dijkstra, result.Algorithm);
        Assert.Equal(new long[] { 1, 3, 2, 4 }, result.SearchSteps.Select(x => x.NodeId));
        Assert.All(result.SearchSteps, step => Assert.Equal(step.Cost, step.Priority));
        Assert.Equal(3, Assert.Single(result.SearchSteps[1].Improvements).Cost);
    }

    [Fact]
    public void NegativeAndNonFiniteRoadCostsAreRejected()
    {
        var net = new RoadNetwork();
        var a = Node(1, 0, 0);
        var b = Node(2, 1, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => net.AddSegment(a, b, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => net.AddSegment(a, b, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => net.AddSegment(a, b, double.PositiveInfinity));
        Assert.Empty(net.Nodes);
    }

    private static RoadNode Node(long id, float x, float y) => new(new WorldPosition { x = x, y = y }, id);
}
