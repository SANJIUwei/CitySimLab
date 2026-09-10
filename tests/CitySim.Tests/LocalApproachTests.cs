namespace CitySim.Tests;

public sealed class LocalApproachTests
{
    [Fact]
    public void CrossSegment_LeavesExitNodeThenWalksToTThenEntersDoor()
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
            new World.WorldPosition { x = 19, y = 0.5f, z = 0 });
        houseA.AttachTo(s12, 0.1f);
        houseB.AttachTo(s23, 0.9f);

        var trip = net.PlanTrip(houseA, houseB);
        var steps = trip.Approach.Steps();

        Assert.False(trip.SameSegment);
        Assert.Same(n3, trip.Approach.ExitNode);
        Assert.Equal(3, steps.Count);
        Assert.Equal(LocalStepKind.ExitNode, steps[0].Kind);
        Assert.Equal(20f, steps[0].Position.x, 3);
        Assert.Equal(LocalStepKind.OnRoad, steps[1].Kind);
        Assert.Equal(19f, steps[1].Position.x, 3);
        Assert.Equal(LocalStepKind.Entrance, steps[2].Kind);
        Assert.Equal(19f, steps[2].Position.x, 3);
        Assert.Equal(0.5f, steps[2].Position.y, 3);
        Assert.Equal(1.0, trip.Approach.AlongSegmentCost, 5);
        Assert.Equal(0.5, trip.Approach.OffRoadDistance, 5);
    }

    [Fact]
    public void SameSegment_SkipsExitNodeAndOnlyWalksOnRoadThenDoor()
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
            new World.WorldPosition { x = 9, y = 1, z = 0 });
        houseA.AttachTo(segment, 0.1f);
        houseB.AttachTo(segment, 0.9f);

        var trip = net.PlanTrip(houseA, houseB);
        var steps = trip.Approach.Steps();

        Assert.True(trip.SameSegment);
        Assert.Null(trip.Approach.ExitNode);
        Assert.Equal(0.0, trip.Approach.AlongSegmentCost);
        Assert.Equal(2, steps.Count);
        Assert.Equal(LocalStepKind.OnRoad, steps[0].Kind);
        Assert.Equal(9f, steps[0].Position.x, 3);
        Assert.Equal(LocalStepKind.Entrance, steps[1].Kind);
        Assert.Equal(9f, steps[1].Position.x, 3);
        Assert.Equal(1f, steps[1].Position.y, 3);
        Assert.Equal(1.0, trip.Approach.OffRoadDistance, 5);
    }
}
