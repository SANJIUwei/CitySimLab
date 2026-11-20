using CitySim.Core;

namespace CitySim.Tests;

public sealed class TimedRoadTravelerTests
{
    [Fact]
    public void DriverStopsAtRedThenMovesOnGreenAndUsesPostedLimit()
    {
        var network = new RoadNetwork();
        var a = Node(1, 0);
        var b = Node(2, 20);
        var c = Node(3, 40);
        var inRoad = network.AddSegment(a, b, 20, new RoadProfile(PostedSpeedKph: 36));
        var outRoad = network.AddSegment(b, c, 20);
        var enter = inRoad.AddLane(LaneDirection.Forward);
        var exit = outRoad.AddLane(LaneDirection.Forward);
        network.SetSignalPlan(b, new SignalPlan(new[] { new SignalPhase(1, 2, 1, new SignalMovement(enter, exit)) }));

        var trip = network.PlanTrip(At(inRoad, 0), At(outRoad, 1));
        Assert.True(trip.Reachable);
        var cursor = new TimedRoadTraveler(network, trip, 0, metersPerUnit: 1, roadSpeedKph: 72, accessSpeedKph: 5);
        cursor.Tick(1);
        Assert.Equal(10, cursor.Position.x, 4);
        cursor.Tick(2);
        Assert.Equal(20, cursor.Position.x, 4);
        Assert.Equal(TimedTravelState.WaitingForGreen, cursor.State);
        cursor.Tick(1.5);
        Assert.Equal(30, cursor.Position.x, 4);
        cursor.Tick(.5);
        Assert.Equal(40, cursor.Position.x, 4);
        Assert.Equal(TimedTravelState.Arrived, cursor.State);
    }

    [Fact]
    public void RedPhaseDoesNotDeleteMovementButAnOmittedMovementIsNotLegal()
    {
        var network = new RoadNetwork();
        var a = Node(1, 0);
        var b = Node(2, 20);
        var c = Node(3, 40);
        var d = Node(4, 60);
        var incoming = network.AddSegment(a, b, 20).AddLane(LaneDirection.Forward);
        var green = network.AddSegment(b, c, 20).AddLane(LaneDirection.Forward);
        var unserved = network.AddSegment(b, d, 20).AddLane(LaneDirection.Forward);
        network.SetSignalPlan(b, new SignalPlan(new[] { new SignalPhase(2, 1, 1, new SignalMovement(incoming, green)) }));
        Assert.Equal(1.5, network.WaitUntilTurnPermitted(incoming, green, 2.5));
        Assert.True(network.FindPath(a.ID, c.ID).Found);
        Assert.False(network.FindPath(a.ID, d.ID).Found);
        Assert.Null(network.WaitUntilTurnPermitted(incoming, unserved, 2.5));
    }

    [Fact]
    public void VehicleDoesNotEnterLaneWhenTimeWindowClosesDuringTravel()
    {
        var network = new RoadNetwork();
        var a = Node(1, 0);
        var b = Node(2, 20);
        var c = Node(3, 40);
        var inRoad = network.AddSegment(a, b, 20);
        var outRoad = network.AddSegment(b, c, 20);
        inRoad.AddLane(LaneDirection.Forward);
        outRoad.AddLane(LaneDirection.Forward, VehicleKind.Car, new TravelWindow(0, 1));
        var trip = network.PlanTrip(At(inRoad, 0), At(outRoad, 1));
        var cursor = new TimedRoadTraveler(network, trip, 59, 1, 36, 5);
        cursor.Tick(5);
        Assert.Equal(20, cursor.Position.x, 4);
        Assert.Equal(TimedTravelState.NeedsReplan, cursor.State);
    }

    [Fact]
    public void UnsignalizedConflictHoldsVehicleWithoutDeletingItsRoute()
    {
        var network = new RoadNetwork();
        var a = Node(1, 0);
        var b = Node(2, 20);
        var c = Node(3, 40);
        var inRoad = network.AddSegment(a, b, 20);
        var outRoad = network.AddSegment(b, c, 20);
        var trip = network.PlanTrip(At(inRoad, 0), At(outRoad, 1));
        var cursor = new TimedRoadTraveler(network, trip, 0, 1, 36, 5,
            unsignalizedDecision: (_, time) => time < 3 ? RightOfWayDecision.YieldToRight : RightOfWayDecision.Proceed);
        cursor.Tick(2.5);
        Assert.Equal(20, cursor.Position.x, 4);
        Assert.Equal(TimedTravelState.WaitingForRightOfWay, cursor.State);
        Assert.Equal(RightOfWayDecision.YieldToRight, cursor.HoldReason);
        cursor.Tick(.5);
        cursor.Tick(2);
        Assert.Equal(TimedTravelState.Arrived, cursor.State);
        Assert.Equal(40, cursor.Position.x, 4);
        Assert.Null(cursor.HoldReason);
    }

    [Fact]
    public void CannotStartATripPlannedBeforeAnEdit()
    {
        var network = new RoadNetwork();
        var road = network.AddSegment(Node(1, 0), Node(2, 20), 20);
        var trip = network.PlanTrip(At(road, .1f), At(road, .9f));
        network.RemoveSegment(road);
        Assert.Throws<ArgumentException>(() => new TimedRoadTraveler(network, trip, 0, 1, 36, 5));
        var legacyTraveler = new Thing();
        legacyTraveler.ReceiveRoute(trip);
        legacyTraveler.PrepareTravel(network);
        Assert.True(legacyTraveler.NeedsReplan);
    }

    static RoadNode Node(long id, float x) => new(new WorldPosition { x = x }, id);
    static RouteEnd At(RoadSegment segment, float t) => RouteEnd.OnRoad(new RoadAttachment(segment, t));
}
