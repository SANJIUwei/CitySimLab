namespace CitySim.Tests;

public sealed class PathfinderTests
{
    [Fact]
    public void Request_NodeIds_StillGoesThroughCenterQueue()
    {
        var net = new RoadNetwork();
        var a = new RoadNode(new WorldPosition { x = 0, y = 0, z = 0 }, 1);
        var b = new RoadNode(new WorldPosition { x = 1, y = 0, z = 0 }, 2);
        var c = new RoadNode(new WorldPosition { x = 2, y = 0, z = 0 }, 3);
        net.AddSegment(a, b, 100);
        net.AddSegment(a, c, 1);
        net.AddSegment(c, b, 1);

        var path = new Pathfinder(net).Request(1, 2);

        Assert.True(path.Found);
        Assert.Equal(new long[] { 1, 3, 2 }, path.NodeIds);
        Assert.Equal(2.0, path.TotalCost);
    }
}
