namespace CitySim.Tests;

public sealed class SignalControlTests
{
    [Fact]
    public void ExplicitCircularRightTurnNeedsAnAffirmativeConflictSnapshot()
    {
        var (net, right) = Junction(SignalRedRule.CircularRightTurnMayYield);

        Assert.Equal(7d, net.WaitUntilTurnPermitted(right.Incoming, right.Outgoing, 5));
        Assert.Equal(7d, net.WaitUntilTurnPermitted(right.Incoming, right.Outgoing, 5, SignalConflictSnapshot.ConflictPresent));
        Assert.Equal(0d, net.WaitUntilTurnPermitted(right.Incoming, right.Outgoing, 5, SignalConflictSnapshot.Clear));
        Assert.Equal(0d, net.WaitUntilTurnPermitted(right.Incoming, right.Outgoing, 12));
    }

    [Theory]
    [InlineData(SignalRedRule.GoverningArrow)]
    [InlineData(SignalRedRule.CircularRightTurnProhibited)]
    [InlineData(SignalRedRule.Protected)]
    public void ArrowSignAndDefaultProtectedControlsCannotBeBypassed(SignalRedRule rule)
    {
        var (net, right) = Junction(rule);
        Assert.Equal(7d, net.WaitUntilTurnPermitted(right.Incoming, right.Outgoing, 5, SignalConflictSnapshot.Clear));
        Assert.Equal(0d, net.WaitUntilTurnPermitted(right.Incoming, right.Outgoing, 12, SignalConflictSnapshot.Clear));
    }

    [Fact]
    public void ClearSnapshotCannotBypassClearanceOrPermanentBan()
    {
        var (net, right) = Junction(SignalRedRule.CircularRightTurnMayYield);
        Assert.Equal(3d, net.WaitUntilTurnPermitted(right.Incoming, right.Outgoing, 9, SignalConflictSnapshot.Clear));
        Assert.Equal(1d, net.WaitUntilTurnPermitted(right.Incoming, right.Outgoing, 11, SignalConflictSnapshot.Clear));
        net.SetTurn(right.Incoming, right.Outgoing, allowed: false);
        Assert.Null(net.WaitUntilTurnPermitted(right.Incoming, right.Outgoing, 5, SignalConflictSnapshot.Clear));
        Assert.Null(net.WaitUntilTurnPermitted(right.Incoming, right.Outgoing, 12, SignalConflictSnapshot.Clear));
    }

    private static (RoadNetwork, SignalMovement) Junction(SignalRedRule rule)
    {
        var net = new RoadNetwork();
        var center = Node(1, 0, 0);
        var east = net.AddSegment(Node(2, 10, 0), center).AddLane(LaneDirection.Forward);
        var north = net.AddSegment(center, Node(3, 0, 10)).AddLane(LaneDirection.Forward);
        var south = net.AddSegment(Node(4, 0, -10), center).AddLane(LaneDirection.Forward);
        var right = new SignalMovement(east, north);
        var through = new SignalMovement(south, north);
        var plan = new SignalPlan(new[]
        {
            new SignalPhase(9, 2, 1, through),
            new SignalPhase(7, 2, 1, right)
        }, new[] { new SignalConflict(right, through) }, turnControls: rule == SignalRedRule.Protected
            ? null : new[] { new SignalTurnControl(right, rule) });
        net.SetSignalPlan(center, plan);
        return (net, right);
    }

    private static RoadNode Node(long id, float x, float y) => new(new WorldPosition { x = x, y = y }, id);
}
