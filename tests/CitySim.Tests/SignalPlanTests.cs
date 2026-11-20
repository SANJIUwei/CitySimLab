namespace CitySim.Tests;

public sealed class SignalPlanTests
{
    [Fact]
    public void YellowAndAllRedWaitForTheNextGreenRatherThanClosingTheMovement()
    {
        var (westToEast, southToNorth, _) = Movements();
        var plan = new SignalPlan(new[]
        {
            new SignalPhase(10, 2, 1, westToEast),
            new SignalPhase(8, 2, 1, southToNorth)
        }, new[] { new SignalConflict(westToEast, southToNorth) });

        Assert.Equal(24d, plan.CycleSeconds);
        Assert.Equal(0d, plan.WaitUntilGreen(westToEast, 0));
        Assert.Equal(0d, plan.WaitUntilGreen(westToEast, 9.5));
        Assert.Equal(14d, plan.WaitUntilGreen(westToEast, 10)); // yellow starts
        Assert.Equal(12d, plan.WaitUntilGreen(westToEast, 12)); // all-red starts
        Assert.Equal(1d, plan.WaitUntilGreen(southToNorth, 12));
        Assert.Equal(0d, plan.WaitUntilGreen(southToNorth, 13));
        Assert.Equal(0d, plan.WaitUntilGreen(westToEast, 24));
    }

    [Fact]
    public void OffsetAndMultipleGreensUseTheNearestAllowedInterval()
    {
        var (westToEast, southToNorth, westToNorth) = Movements();
        var plan = new SignalPlan(new[]
        {
            new SignalPhase(4, 1, 1, westToEast),
            new SignalPhase(6, 1, 1, southToNorth),
            new SignalPhase(4, 1, 1, westToEast)
        }, offsetSeconds: 3);

        Assert.Equal(1d, plan.WaitUntilGreen(westToEast, 2));
        Assert.Equal(0d, plan.WaitUntilGreen(westToEast, 3));
        Assert.Equal(10d, plan.WaitUntilGreen(westToEast, 7));
        Assert.Equal(0d, plan.WaitUntilGreen(westToEast, 17));
        Assert.Equal(0d, plan.WaitUntilGreen(westToEast, 23));
        Assert.Null(plan.WaitUntilGreen(westToNorth, 23));
    }

    [Fact]
    public void ConflictPairsAreSymmetricAndApplyOnlyToSimultaneousGreen()
    {
        var (westToEast, southToNorth, westToNorth) = Movements();
        var pair = new SignalConflict(westToEast, southToNorth);
        Assert.Throws<ArgumentException>(() => new SignalPlan(new[]
        {
            new SignalPhase(10, 2, 1, southToNorth, westToEast)
        }, new[] { pair }));

        var phases = new[]
        {
            new SignalPhase(10, 2, 1, westToEast, westToNorth),
            new SignalPhase(8, 2, 1, southToNorth)
        };
        var plan = new SignalPlan(phases, new[] { pair });
        Assert.Equal(0d, plan.WaitUntilGreen(westToNorth, 0));
        Assert.Equal(13d, plan.WaitUntilGreen(southToNorth, 0));
    }

    [Fact]
    public void RejectsInvalidTimingAndMovesThatDoNotMeetAtAJunction()
    {
        var (westToEast, southToNorth, _) = Movements();
        Assert.Throws<ArgumentOutOfRangeException>(() => new SignalPhase(0, 1, 1, westToEast));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SignalPhase(1, -1, 1, westToEast));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SignalPhase(1, 1, double.NaN, westToEast));
        Assert.Throws<ArgumentException>(() => new SignalPhase(1, 1, 1,
            new SignalMovement(westToEast.Outgoing, southToNorth.Outgoing)));
        Assert.Throws<ArgumentException>(() => new SignalPlan(Array.Empty<SignalPhase>()));
        var plan = new SignalPlan(new[] { new SignalPhase(1, 0, 0, westToEast) });
        Assert.Throws<ArgumentOutOfRangeException>(() => plan.WaitUntilGreen(westToEast, double.PositiveInfinity));
    }

    private static (SignalMovement, SignalMovement, SignalMovement) Movements()
    {
        var net = new RoadNetwork();
        var junction = Node(1, 0, 0);
        var west = net.AddSegment(Node(2, -10, 0), junction).AddLane(LaneDirection.Forward);
        var south = net.AddSegment(Node(3, 0, -10), junction).AddLane(LaneDirection.Forward);
        var east = net.AddSegment(junction, Node(4, 10, 0)).AddLane(LaneDirection.Forward);
        var north = net.AddSegment(junction, Node(5, 0, 10)).AddLane(LaneDirection.Forward);
        return (new SignalMovement(west, east), new SignalMovement(south, north), new SignalMovement(west, north));
    }

    private static RoadNode Node(long id, float x, float y) => new(new WorldPosition { x = x, y = y }, id);
}
