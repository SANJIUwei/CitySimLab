namespace CitySim.Tests;

public sealed class PathCenterTests
{
    [Fact]
    public void Enqueue_StaysInInboxUntilSchedulerTick()
    {
        var net = TwoNodeNetwork(out _, out _, out var segment);
        var houseA = House(1, 0, segment, 0.1f);
        var houseB = House(9, 0, segment, 0.9f);
        var scheduler = new ComputeScheduler();
        var center = new PathCenter(net, scheduler);
        var thing = new Thing();

        Assert.True(TripStarter.Request(thing, center, houseA, houseB));
        Assert.Equal(1, center.Inbox);
        Assert.Equal(1, center.PendingCount);
        Assert.True(thing.WaitingForRoute);
        Assert.Null(thing.Route);

        Assert.Equal(1, scheduler.Tick(ComputeCategories.Path, 1));

        Assert.Equal(0, center.Inbox);
        Assert.Equal(0, center.PendingCount);
        Assert.False(thing.WaitingForRoute);
        Assert.NotNull(thing.Route);
        Assert.Equal(LocalPathKind.SameSegment, thing.JudgeLocalPath());
    }

    [Fact]
    public void Process_RespectsBudgetAndLeavesRemainingInInbox()
    {
        var net = TwoNodeNetwork(out _, out _, out var segment);
        var houseA = House(1, 0, segment, 0.1f);
        var houseB = House(9, 0, segment, 0.9f);
        var center = new PathCenter(net);
        var first = new Thing();
        var second = new Thing();
        Assert.True(TripStarter.Request(first, center, houseA, houseB));
        Assert.True(TripStarter.Request(second, center, houseA, houseB));
        Assert.Equal(2, center.Inbox);

        Assert.Equal(1, center.Process(1));
        Assert.NotNull(first.Route);
        Assert.Null(second.Route);
        Assert.Equal(1, center.PendingCount);
    }

    [Fact]
    public void NodeSearch_GoesThroughCenterQueue()
    {
        var net = new RoadNetwork();
        var a = new RoadNode(new WorldPosition { x = 0, y = 0, z = 0 }, 1);
        var b = new RoadNode(new WorldPosition { x = 1, y = 0, z = 0 }, 2);
        var c = new RoadNode(new WorldPosition { x = 2, y = 0, z = 0 }, 3);
        net.AddSegment(a, b, 100);
        net.AddSegment(a, c, 1);
        net.AddSegment(c, b, 1);

        GlobalPath? path = null;
        var center = new PathCenter(net);
        center.Enqueue(1, 2, p => path = p);
        Assert.Equal(1, center.Inbox);

        center.Process(1);

        Assert.NotNull(path);
        Assert.True(path!.Found);
        Assert.Equal(new long[] { 1, 3, 2 }, path.NodeIds);
        Assert.Equal(2.0, path.TotalCost);
        Assert.Equal(0, center.Inbox);
    }

    [Fact]
    public void PathfinderAlias_StillForwardsToCenter()
    {
        var net = TwoNodeNetwork(out _, out _, out var segment);
        var houseA = House(1, 0, segment, 0.1f);
        var houseB = House(9, 0, segment, 0.9f);
        var alias = new Pathfinder(net);
        var thing = new Thing();
        Assert.True(TripStarter.Request(thing, alias, houseA, houseB));
        alias.Process(1);
        Assert.NotNull(thing.Route);
    }

    static RoadNetwork TwoNodeNetwork(out RoadNode n1, out RoadNode n2, out RoadSegment segment)
    {
        var net = new RoadNetwork();
        n1 = new RoadNode(new WorldPosition { x = 0, y = 0, z = 0 }, 1);
        n2 = new RoadNode(new WorldPosition { x = 10, y = 0, z = 0 }, 2);
        segment = net.AddSegment(n1, n2, 10);
        return net;
    }

    static Building House(float x, float y, RoadSegment segment, float t)
    {
        var building = new Building(
            new WorldPosition { x = x, y = y + 2, z = 0 },
            new WorldPosition { x = x, y = y, z = 0 });
        building.AttachTo(segment, t);
        return building;
    }
}
