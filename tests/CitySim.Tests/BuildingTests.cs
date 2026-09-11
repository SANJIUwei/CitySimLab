namespace CitySim.Tests;

public sealed class BuildingTests
{
    [Fact]
    public void AttachTo_StoresSegmentAndT()
    {
        var start = new RoadNode(new World.WorldPosition { x = 0f, y = 0f, z = 0f }, 1);
        var end = new RoadNode(new World.WorldPosition { x = 10f, y = 0f, z = 0f }, 2);
        var segment = new RoadSegment(start, end);
        var building = new Building(
            new World.WorldPosition { x = 5f, y = 2f, z = 0f },
            new World.WorldPosition { x = 5f, y = 0.5f, z = 0f });

        building.AttachTo(segment, 0.5f);

        Assert.NotNull(building.Attachment);
        Assert.Same(segment, building.Attachment.Segment);
        Assert.Equal(0.5f, building.Attachment.T);
    }

    [Fact]
    public void AttachNearest_ProjectsEntranceOntoClosestSegmentAndStoresT()
    {
        var net = new RoadNetwork();
        var n1 = new RoadNode(new World.WorldPosition { x = 0, y = 0, z = 0 }, 1);
        var n2 = new RoadNode(new World.WorldPosition { x = 10, y = 0, z = 0 }, 2);
        var n3 = new RoadNode(new World.WorldPosition { x = 0, y = 10, z = 0 }, 3);
        var road = net.AddSegment(n1, n2);
        net.AddSegment(n1, n3);

        var building = new Building(
            new World.WorldPosition { x = 4, y = 3, z = 0 },
            new World.WorldPosition { x = 4, y = 2, z = 0 },
            net);

        Assert.NotNull(building.Attachment);
        Assert.Same(road, building.Attachment.Segment);
        Assert.Equal(0.4f, building.Attachment.T, 3);
    }

    [Fact]
    public void AttachNearest_ClampsPastTheEndOfTheSegment()
    {
        var net = new RoadNetwork();
        var n1 = new RoadNode(new World.WorldPosition { x = 0, y = 0, z = 0 }, 1);
        var n2 = new RoadNode(new World.WorldPosition { x = 10, y = 0, z = 0 }, 2);
        net.AddSegment(n1, n2);
        var building = new Building(
            new World.WorldPosition { x = 20, y = 3, z = 0 },
            new World.WorldPosition { x = 20, y = 1, z = 0 });

        Assert.True(building.AttachNearest(net));
        Assert.Equal(1f, building.Attachment!.T, 3);
    }

    [Fact]
    public void TripStarter_RejectsUnattachedBuildings()
    {
        var net = new RoadNetwork();
        var n1 = new RoadNode(new World.WorldPosition { x = 0, y = 0, z = 0 }, 1);
        var n2 = new RoadNode(new World.WorldPosition { x = 10, y = 0, z = 0 }, 2);
        net.AddSegment(n1, n2, 1);
        var origin = new Building(
            new World.WorldPosition { x = 0, y = 0, z = 0 },
            new World.WorldPosition { x = 0, y = 0, z = 0 });
        var destination = new Building(
            new World.WorldPosition { x = 10, y = 0, z = 0 },
            new World.WorldPosition { x = 10, y = 0, z = 0 });

        var pathfinder = new Pathfinder(net);
        var thing = new Thing();

        Assert.False(TripStarter.Request(thing, pathfinder, origin, destination));
        Assert.Equal(0, pathfinder.PendingCount);
        Assert.Null(thing.Command);
    }
}
