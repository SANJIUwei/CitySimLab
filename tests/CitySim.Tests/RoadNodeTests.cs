namespace CitySim.Tests;

public sealed class RoadNodeTests
{
    [Fact]
    public void Constructor_StoresPositionAndId()
    {
        var position = new World.WorldPosition { x = 3f, y = 4f, z = 0f };

        var node = new RoadNode(position, 7);

        Assert.Equal(3f, node.Position.x);
        Assert.Equal(4f, node.Position.y);
        Assert.Equal(0f, node.Position.z);
        Assert.Equal(7, node.ID);
    }
}
