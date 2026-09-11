namespace CitySim.Tests;

public sealed class PathfinderTests
{
    [Fact]
    public void Submit_QueuesUntilProcessSendsRouteBackToThing()
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

        var pathfinder = new Pathfinder(net);
        var thing = new Thing();
        Assert.True(TripStarter.Request(thing, pathfinder, houseA, houseB));

        Assert.True(thing.WaitingForRoute);
        Assert.Null(thing.Route);
        Assert.Equal(1, pathfinder.PendingCount);

        int processed = pathfinder.Process(1);

        Assert.Equal(1, processed);
        Assert.Equal(0, pathfinder.PendingCount);
        Assert.False(thing.WaitingForRoute);
        Assert.NotNull(thing.Route);
        Assert.False(thing.Route.SameSegment);
        Assert.Equal(new long[] { 1, 2, 3 }, thing.Route.Global.NodeIds);
        Assert.Same(n3, thing.Route.Approach.ExitNode);
        Assert.Same(n1, thing.Route.Departure.EntryNode);
        Assert.Equal(LocalPathKind.DepartureThenGlobalThenApproach, thing.JudgeLocalPath());
    }

    [Fact]
    public void Process_RespectsBudgetAndLeavesRemainingQueued()
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

        var pathfinder = new Pathfinder(net);
        var first = new Thing();
        var second = new Thing();
        Assert.True(TripStarter.Request(first, pathfinder, houseA, houseB));
        Assert.True(TripStarter.Request(second, pathfinder, houseA, houseB));

        Assert.Equal(1, pathfinder.Process(1));
        Assert.NotNull(first.Route);
        Assert.Null(second.Route);
        Assert.Equal(1, pathfinder.PendingCount);
        Assert.Equal(LocalPathKind.SameSegment, first.JudgeLocalPath());
    }

    [Fact]
    public void Request_NodeIds_ReturnsWeightedShortestPath()
    {
        var net = new RoadNetwork();
        var a = new RoadNode(new World.WorldPosition { x = 0, y = 0, z = 0 }, 1);
        var b = new RoadNode(new World.WorldPosition { x = 1, y = 0, z = 0 }, 2);
        var c = new RoadNode(new World.WorldPosition { x = 2, y = 0, z = 0 }, 3);
        net.AddSegment(a, b, 100);
        net.AddSegment(a, c, 1);
        net.AddSegment(c, b, 1);

        var path = new Pathfinder(net).Request(1, 2);

        Assert.True(path.Found);
        Assert.Equal(new long[] { 1, 3, 2 }, path.NodeIds);
        Assert.Equal(2.0, path.TotalCost);
    }
}
