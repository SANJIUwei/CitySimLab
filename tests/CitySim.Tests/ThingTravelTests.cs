namespace CitySim.Tests;

public sealed class ThingTravelTests
{
    [Fact]
    public void Advance_SameSegment_EndsAtDestinationEntrance()
    {
        var net = new RoadNetwork();
        var n1 = new RoadNode(new World.WorldPosition { x = 0, y = 0, z = 0 }, 1);
        var n2 = new RoadNode(new World.WorldPosition { x = 10, y = 0, z = 0 }, 2);
        var road = net.AddSegment(n1, n2, 10);
        var a = new Building(new World.WorldPosition { x = 2, y = 1, z = 0 }, new World.WorldPosition { x = 2, y = 0, z = 0 });
        var b = new Building(new World.WorldPosition { x = 8, y = 1, z = 0 }, new World.WorldPosition { x = 8, y = 0, z = 0 });
        a.AttachTo(road, 0.2f);
        b.AttachTo(road, 0.8f);

        var thing = Travel(net, a, b);

        Assert.Equal(a.Entrance.x, thing.Position.x);
        while (thing.Advance()) { }

        Assert.True(thing.Arrived);
        Assert.Equal(b.Entrance.x, thing.Position.x);
        Assert.Equal(b.Entrance.y, thing.Position.y);
    }

    [Fact]
    public void Advance_CrossSegment_VisitsGlobalNodesThenDoor()
    {
        var net = new RoadNetwork();
        var n1 = new RoadNode(new World.WorldPosition { x = 0, y = 0, z = 0 }, 1);
        var n2 = new RoadNode(new World.WorldPosition { x = 10, y = 0, z = 0 }, 2);
        var n3 = new RoadNode(new World.WorldPosition { x = 20, y = 0, z = 0 }, 3);
        var s12 = net.AddSegment(n1, n2, 10);
        var s23 = net.AddSegment(n2, n3, 10);
        var a = new Building(new World.WorldPosition { x = 1, y = 2, z = 0 }, new World.WorldPosition { x = 1, y = 0, z = 0 });
        var b = new Building(new World.WorldPosition { x = 19, y = 2, z = 0 }, new World.WorldPosition { x = 19, y = 0, z = 0 });
        a.AttachTo(s12, 0.1f);
        b.AttachTo(s23, 0.9f);

        var thing = Travel(net, a, b);
        var seenX = new List<float> { thing.Position.x };
        while (thing.Advance())
            seenX.Add(thing.Position.x);

        Assert.True(thing.Arrived);
        Assert.Contains(0f, seenX);
        Assert.Contains(10f, seenX);
        Assert.Contains(20f, seenX);
        Assert.Equal(b.Entrance.x, thing.Position.x);
    }

    static Thing Travel(RoadNetwork net, Building from, Building to)
    {
        var pathfinder = new Pathfinder(net);
        var thing = new Thing();
        Assert.True(TripStarter.Request(thing, pathfinder, from, to));
        pathfinder.Process(1);
        return thing;
    }
}
