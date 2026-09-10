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

        Assert.Same(segment, building.Attachment.Segment);
        Assert.Equal(0.5f, building.Attachment.T);
    }
}
