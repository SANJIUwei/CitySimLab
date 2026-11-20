using CitySim.Core;

namespace CitySim.Tests;

public sealed class TrafficSimulationTests
{
    [Fact]
    public void FasterCarQueuesAndCannotPassInSameLane()
    {
        var network = new RoadNetwork();
        var lane = network.AddSegment(Node(1, 0, 0), Node(2, 100, 0), 100);
        lane.AddLane(LaneDirection.Forward);
        var trip = network.PlanTrip(On(lane, 0), On(lane, 1));
        var sim = Sim(network);
        var slow = sim.AddVehicle(trip, 18, 10, 4);
        var fast = sim.AddVehicle(trip, 72, 10, 4);
        sim.Tick(.25);
        Assert.Equal(TrafficWaitReason.StartingLaneOccupied, fast.HoldReason);
        bool followed = false;
        for (int i = 0; i < 75; i++)
        {
            sim.Tick(.25);
            followed |= fast.HoldReason == TrafficWaitReason.FollowingVehicle;
            if (slow.CurrentLane != null && fast.CurrentLane != null)
                Assert.True(slow.Position.x - fast.Position.x >= 5.99f);
        }
        Assert.True(followed);
        sim.Tick(10);
        Assert.True(slow.Arrived);
        Assert.True(fast.Arrived);
    }

    [Fact]
    public void ConflictingMovementsDoNotOccupyJunctionAtOnce()
    {
        var (network, junction, w, e, s, n) = Cross();
        var sim = Sim(network);
        var west = sim.AddVehicle(network.PlanTrip(On(w, 0), On(e, 1)), 36, 10);
        var south = sim.AddVehicle(network.PlanTrip(On(s, 0), On(n, 1)), 36, 10);
        sim.Tick(2.25);
        Assert.Same(e, west.CurrentLane!.Segment);
        Assert.Same(s, south.CurrentLane!.Segment);
        Assert.Equal(TrafficWaitReason.JunctionOccupied, south.HoldReason);
        Assert.Equal(junction.Position.y, south.Position.y, 3);
        sim.Tick(8);
        Assert.True(west.Arrived);
        Assert.True(south.Arrived);
    }

    [Fact]
    public void QueueAtBlockedExitDoesNotEnterJunctionUntilClear()
    {
        var (network, _, w, e, _, _) = Cross();
        var sim = Sim(network);
        var blocker = sim.AddVehicle(network.PlanTrip(On(e, .05f), On(e, 1)), .36, 5);
        var follower = sim.AddVehicle(network.PlanTrip(On(w, 0), On(e, 1)), 36, 10);
        sim.Tick(2.5);
        Assert.Equal(TrafficWaitReason.ExitLaneOccupied, follower.HoldReason);
        Assert.Same(w, follower.CurrentLane!.Segment);
        Assert.Equal(0, follower.Position.x, 3);
        Assert.True(sim.RemoveVehicle(blocker));
        sim.Tick(.5);
        Assert.Same(e, follower.CurrentLane!.Segment);
        Assert.True(follower.Position.x > 0);
    }

    [Fact]
    public void UncontrolledJunctionStopsAndYieldsToApproachOnRight()
    {
        var (network, junction, w, e, s, n) = Cross();
        var sim = Sim(network);
        sim.SetUnsignalizedPolicy(junction, new TrafficJunctionPolicy());
        var west = sim.AddVehicle(network.PlanTrip(On(w, 0), On(e, 1)), 36, 10);
        var south = sim.AddVehicle(network.PlanTrip(On(s, 0), On(n, 1)), 36, 10);
        sim.Tick(2);
        Assert.Equal(RightOfWayDecision.StopAndLook, west.YieldReason);
        Assert.Equal(RightOfWayDecision.StopAndLook, south.YieldReason);
        sim.Tick(.25);
        Assert.Equal(RightOfWayDecision.YieldToRight, west.YieldReason);
        Assert.Same(n, south.CurrentLane!.Segment);
        sim.Tick(8);
        Assert.True(west.Arrived);
        Assert.True(south.Arrived);
    }

    [Fact]
    public void UnreachableNotSpawnedAndCanReplanFromOriginalPoint()
    {
        var net = new RoadNetwork();
        var a = net.AddSegment(Node(1, 0, 0), Node(2, 20, 0), 20);
        var b = net.AddSegment(Node(3, 30, 0), Node(4, 50, 0), 20);
        var from = On(a, 0);
        var to = On(b, 1);
        var sim = Sim(net);
        var car = sim.AddVehicle(net.PlanTrip(from, to), 36, 10);
        sim.Tick(1);
        Assert.True(car.Unreachable);
        Assert.Equal(TrafficWaitReason.Unreachable, car.HoldReason);
        Assert.False(sim.TryReplan(car, net.PlanTrip(On(b, 0), to)));
        net.AddSegment(a.EndNode, b.StartNode, 10);
        Assert.True(sim.TryReplan(car, net.PlanTrip(from, to)));
        sim.Tick(7);
        Assert.True(car.Arrived);
    }

    [Theory]
    [InlineData(false, TrafficWaitReason.None)]
    [InlineData(true, TrafficWaitReason.RedSignal)]
    [InlineData(null, TrafficWaitReason.RedSignal)]
    public void PermissiveCircularRedUsesLivePedestrianConflicts(bool? pedestrian, TrafficWaitReason expected)
    {
        var (network, junction, w, e, s, n) = Cross();
        // y points north: westbound approach from the south turns right toward the east.
        var permitted = new SignalMovement(s.Lanes[0], e.Lanes[0]);
        var other = new SignalMovement(w.Lanes[0], n.Lanes[0]);
        network.SetSignalPlan(junction, new SignalPlan(new[]
        {
            new SignalPhase(10, 1, 1, other), new SignalPhase(10, 1, 1, permitted)
        }, turnControls: new[] { new SignalTurnControl(permitted, SignalRedRule.CircularRightTurnMayYield) }));
        var sim = new TrafficSimulation(network, 1, new TrafficParameters(.25, 2, 4),
            pedestrianConflict: (_, _) => pedestrian);
        var car = sim.AddVehicle(network.PlanTrip(On(s, 0), On(e, 1)), 36, 10);
        sim.Tick(2.5);
        Assert.Equal(expected, car.HoldReason);
        Assert.Same(expected == TrafficWaitReason.None ? e : s, car.CurrentLane!.Segment);
    }

    [Fact]
    public void ReleasedConflictingVehicleWinsOverPermissiveRightOnRed()
    {
        var (network, junction, w, e, s, n) = Cross();
        var right = new SignalMovement(s.Lanes[0], e.Lanes[0]);
        var released = new SignalMovement(w.Lanes[0], n.Lanes[0]);
        network.SetSignalPlan(junction, new SignalPlan(new[]
        {
            new SignalPhase(10, 1, 1, released), new SignalPhase(10, 1, 1, right)
        }, new[] { new SignalConflict(right, released) },
            turnControls: new[] { new SignalTurnControl(right, SignalRedRule.CircularRightTurnMayYield) }));
        var sim = new TrafficSimulation(network, 1, new TrafficParameters(.25, 2, 4),
            pedestrianConflict: (_, _) => false);
        var onRed = sim.AddVehicle(network.PlanTrip(On(s, 0), On(e, 1)), 36, 10);
        var onGreen = sim.AddVehicle(network.PlanTrip(On(w, 0), On(n, 1)), 36, 10);
        sim.Tick(2.25);
        Assert.Same(n, onGreen.CurrentLane!.Segment);
        Assert.Equal(TrafficWaitReason.RedSignal, onRed.HoldReason);
        Assert.Same(s, onRed.CurrentLane!.Segment);
        sim.Tick(1);
        Assert.Same(e, onRed.CurrentLane!.Segment);
    }

    [Fact]
    public void ReplanAfterAnEditDoesNotReleaseAnOccupiedJunctionPrematurely()
    {
        var (network, _, w, e, s, n) = Cross();
        var sim = Sim(network);
        var west = sim.AddVehicle(network.PlanTrip(On(w, 0), On(e, 1)), 36, 10);
        var south = sim.AddVehicle(network.PlanTrip(On(s, 0), On(n, 1)), 36, 10);
        sim.Tick(2.25);
        Assert.Equal(2.5, west.Position.x, 3);
        network.AddSegment(Node(6, 70, 0), Node(7, 80, 0), 10);
        sim.Tick(.25);
        Assert.True(west.NeedsReplan);
        Assert.True(south.NeedsReplan);
        Assert.True(sim.TryReplan(west, network.PlanTrip(On(e, e.ClosestT(west.Position)), On(e, 1))));
        Assert.True(sim.TryReplan(south, network.PlanTrip(On(s, s.ClosestT(south.Position)), On(n, 1))));
        sim.Tick(.25);
        Assert.Equal(TrafficWaitReason.JunctionOccupied, south.HoldReason);
        sim.Tick(1);
        Assert.Same(n, south.CurrentLane!.Segment);
    }

    static TrafficSimulation Sim(RoadNetwork net) => new(net, 1, new TrafficParameters(.25, 2, 4));

    static (RoadNetwork, RoadNode, RoadSegment, RoadSegment, RoadSegment, RoadSegment) Cross()
    {
        var net = new RoadNetwork();
        var j = Node(1, 0, 0);
        var w = net.AddSegment(Node(2, -20, 0), j, 20);
        var e = net.AddSegment(j, Node(3, 30, 0), 30);
        var s = net.AddSegment(Node(4, 0, -20), j, 20);
        var n = net.AddSegment(j, Node(5, 0, 30), 30);
        foreach (var leg in new[] { w, e, s, n }) leg.AddLane(LaneDirection.Forward);
        return (net, j, w, e, s, n);
    }

    static RoadNode Node(long id, float x, float y) => new(new WorldPosition { x = x, y = y }, id);
    static RouteEnd On(RoadSegment segment, float t) => RouteEnd.OnRoad(new RoadAttachment(segment, t));
}
