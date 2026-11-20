namespace CitySim.Tests;

public sealed class QueuedAccessChangesTests
{
    [Fact]
    public void AQueuedTripCannotUseAnAccessPointClosedBeforeTheJobRuns()
    {
        var net = new RoadNetwork();
        var a = new RoadNode(new WorldPosition { x = 0 }, 1);
        var b = new RoadNode(new WorldPosition { x = 10 }, 2);
        var road = net.AddSegment(a, b, 10);
        var from = new Building(new WorldPosition { x = 2, y = 1 }, new WorldPosition { x = 2, y = 1 }, net);
        var to = new Building(new WorldPosition { x = 8, y = 1 }, new WorldPosition { x = 8, y = 1 }, net);
        var subject = new Thing();
        var center = new PathCenter(net);
        Assert.True(TripStarter.Request(subject, center, from, to));
        from.DetachAccess(net);
        Assert.Equal(1, center.Process(1));
        Assert.Equal(RouteFailure.NoBuildingAccess, subject.Route!.Failure);
        Assert.False(subject.Advance());
    }
}
