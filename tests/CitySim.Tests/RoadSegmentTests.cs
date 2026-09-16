namespace CitySim.Tests;

public sealed class RoadSegmentTests
{
    [Fact]
    public void TwoWaySegment_HasTwoNodesAndTwoLanes()
    {
        var start = new RoadNode(new WorldPosition { x = 0f, y = 0f, z = 0f }, 1);
        var end = new RoadNode(new WorldPosition { x = 3f, y = 4f, z = 0f }, 2);
        var segment = new RoadSegment(start, end);

        var forward = segment.AddLane(LaneDirection.Forward);
        var reverse = segment.AddLane(LaneDirection.Reverse);

        Assert.Same(start, segment.StartNode);
        Assert.Same(end, segment.EndNode);
        Assert.Equal(2, segment.Lanes.Count);
        Assert.Same(segment, forward.Segment);
        Assert.Same(segment, reverse.Segment);
        Assert.Equal(LaneDirection.Forward, forward.Direction);
        Assert.Equal(LaneDirection.Reverse, reverse.Direction);
    }
}
