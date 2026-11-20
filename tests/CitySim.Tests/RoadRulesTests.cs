namespace CitySim.Tests;

public sealed class RoadRulesTests
{
    [Fact]
    public void IncomingOnlyNodeIsAValidDestinationButNotAReverseRoute()
    {
        var net = new RoadNetwork();
        var a = Node(1, 0, 0);
        var b = Node(2, 10, 0);
        var s = net.AddSegment(a, b, 10);
        s.AddLane(LaneDirection.Forward);

        var route = net.FindPath(a.ID, b.ID);
        Assert.True(route.Found);
        Assert.Equal(new[] { a.ID, b.ID }, route.NodeIds);
        Assert.Equal(net.FindPath(a.ID, b.ID, PathAlgorithm.Dijkstra).TotalCost, route.TotalCost, 6);
        Assert.False(net.FindPath(b.ID, a.ID).Found);

        var nearA = At(s, .25f);
        var nearB = At(s, .75f);
        Assert.True(net.PlanTrip(nearA, nearB).Reachable);
        Assert.False(net.PlanTrip(nearB, nearA).Reachable);
    }

    [Fact]
    public void RightInRightOutLimitsTheOpeningNotTheRoadAndCanDetour()
    {
        var net = new RoadNetwork();
        var west = Node(1, 0, 0);
        var east = Node(2, 100, 0);
        var north = Node(3, 50, 50);
        var street = net.AddSegment(west, east, 100);
        var forward = street.AddLane(LaneDirection.Forward);
        var reverse = street.AddLane(LaneDirection.Reverse);
        net.SetTurn(forward, reverse, allowed: false);
        net.SetTurn(reverse, forward, allowed: false);
        var driveway = new Building(Pos(50, -5), Pos(50, -3));
        driveway.AttachTo(street, .5f, enterFrom: TravelDirection.Forward, exitToward: TravelDirection.Forward);
        var entrance = RouteEnd.FromBuilding(driveway);
        var before = At(street, .25f);
        var after = At(street, .75f);

        Assert.True(net.FindPath(east.ID, west.ID).Found);
        Assert.True(net.PlanTrip(before, entrance).Reachable);
        Assert.True(net.PlanTrip(entrance, after).Reachable);
        Assert.False(net.PlanTrip(after, entrance).Reachable);
        Assert.False(net.PlanTrip(entrance, before).Reachable);

        var leg1 = net.AddSegment(east, north, 100).AddLane(LaneDirection.Forward);
        var leg2 = net.AddSegment(north, west, 100).AddLane(LaneDirection.Forward);
        Assert.True(net.FindPath(west.ID, east.ID).Found);
        var detour = net.PlanTrip(after, entrance);
        Assert.True(detour.Reachable);
        Assert.False(detour.SameSegment);
        Assert.Contains(leg1, detour.Global.Lanes);
        Assert.Contains(leg2, detour.Global.Lanes);
        Assert.True(detour.TotalCost > 200);
        Assert.True(net.PlanTrip(entrance, before).Reachable);
        Assert.Same(forward, Assert.Single(net.FindPath(west.ID, east.ID).Lanes));
    }

    [Fact]
    public void AForbiddenTurnAppliesOnlyToItsIncomingApproach()
    {
        var net = new RoadNetwork();
        var center = Node(1, 0, 0);
        var west = Node(2, -10, 0);
        var east = Node(3, 10, 0);
        var north = Node(4, 0, 10);
        var westIn = net.AddSegment(west, center, 10).AddLane(LaneDirection.Forward);
        net.AddSegment(east, center, 10).AddLane(LaneDirection.Forward);
        var northOut = net.AddSegment(center, north, 10).AddLane(LaneDirection.Forward);
        net.SetTurn(westIn, northOut, allowed: false);

        Assert.False(net.FindPath(west.ID, north.ID).Found);
        Assert.True(net.FindPath(east.ID, north.ID).Found);
        Assert.True(net.FindPath(west.ID, center.ID).Found);
    }

    [Fact]
    public void SignalDelayAddsCostWithoutClosingTheTurn()
    {
        var net = new RoadNetwork();
        var a = Node(1, 0, 0);
        var b = Node(2, 10, 0);
        var c = Node(3, 20, 0);
        var incoming = net.AddSegment(a, b, 10).AddLane(LaneDirection.Forward);
        var outgoing = net.AddSegment(b, c, 10).AddLane(LaneDirection.Forward);

        var baseCost = net.FindPath(a.ID, c.ID).TotalCost;
        net.SetTurn(incoming, outgoing, waitingCost: 5);
        var delayed = net.FindPath(a.ID, c.ID);
        Assert.True(delayed.Found);
        Assert.Equal(baseCost + 5, delayed.TotalCost, 6);

        net.SetTurn(incoming, outgoing, allowed: false);
        Assert.False(net.FindPath(a.ID, c.ID).Found);
    }

    [Fact]
    public void BusLaneAppliesToBusesOnlyInsideItsWindow()
    {
        var net = new RoadNetwork();
        var a = Node(1, 0, 0);
        var b = Node(2, 10, 0);
        var bus = net.AddSegment(a, b, 1).AddLane(LaneDirection.Forward, VehicleKind.Bus,
            new TravelWindow(480, 540));
        var general = net.AddSegment(a, b, 20).AddLane(LaneDirection.Forward);
        var inWindow = net.FindPath(a.ID, b.ID, travel: new TravelOptions(VehicleKind.Bus, 500));
        var outOfWindow = net.FindPath(a.ID, b.ID, travel: new TravelOptions(VehicleKind.Bus, 540));
        Assert.Same(bus, Assert.Single(inWindow.Lanes));
        Assert.Same(general, Assert.Single(outOfWindow.Lanes));
        Assert.Same(general, Assert.Single(net.FindPath(a.ID, b.ID, travel: new TravelOptions(VehicleKind.Car, 500)).Lanes));
        Assert.True(inWindow.TotalCost < outOfWindow.TotalCost);
    }

    [Fact]
    public void RoadsCrossingAtDifferentElevationsAreNotAutomaticallyConnected()
    {
        var net = new RoadNetwork();
        var west = Node(1, -10, 0, 5);
        var east = Node(2, 10, 0, 5);
        var south = Node(3, 0, -10);
        var north = Node(4, 0, 10);
        net.AddSegment(west, east, 20, profile: new RoadProfile(
            UrbanClass: UrbanRoadClass.Expressway, Role: RoadRole.Mainline, Structure: RoadStructure.Elevated, Level: 1));
        net.AddSegment(south, north, 20);
        Assert.True(net.FindPath(west.ID, east.ID).Found);
        Assert.True(net.FindPath(south.ID, north.ID).Found);
        Assert.False(net.FindPath(west.ID, north.ID).Found);
        Assert.False(net.FindPath(south.ID, east.ID).Found);
    }

    [Fact]
    public void NearestLegalOpeningSkipsExpresswayMainlineAndIsRecheckedWhenRoadRemoved()
    {
        var net = new RoadNetwork();
        net.AddSegment(Node(1, 0, 0), Node(2, 100, 0), cost: 100,
            profile: new RoadProfile(UrbanClass: UrbanRoadClass.Expressway, Role: RoadRole.Mainline));
        var street = net.AddSegment(Node(3, 0, 10), Node(4, 100, 10), cost: 100,
            profile: new RoadProfile(UrbanClass: UrbanRoadClass.Local));
        var factory = new Building(Pos(50, 2), Pos(50, 1));
        Assert.False(factory.AttachNearest(net, 2));
        Assert.True(factory.AttachNearest(net, 20));
        Assert.Same(street, factory.Attachment?.Segment);
        var supplier = new Building(Pos(20, 11), Pos(20, 11));
        supplier.AttachTo(street, .2f);
        Assert.True(net.PlanTrip(supplier, factory).Reachable);

        net.RemoveSegment(street);
        var failed = net.PlanTrip(supplier, factory);
        Assert.False(failed.Reachable);
        Assert.Equal(RouteFailure.NoBuildingAccess, failed.Failure);
        Assert.False(factory.AttachNearest(net, 20));
        Assert.Null(factory.Attachment);
    }

    [Fact]
    public void ConfiguredAccessPriorityWinsOverNearestOtherwiseLegalRoad()
    {
        var net = new RoadNetwork();
        var arterial = net.AddSegment(Node(1, 0, 0), Node(2, 100, 0), cost: 100,
            profile: new RoadProfile(UrbanClass: UrbanRoadClass.Arterial, AccessPriority: 10));
        var sideStreet = net.AddSegment(Node(3, 0, 10), Node(4, 100, 10), cost: 100,
            profile: new RoadProfile(UrbanClass: UrbanRoadClass.Local, AccessPriority: 0));
        var building = new Building(Pos(50, 2), Pos(50, 1));

        Assert.True(building.AttachNearest(net, 2));
        Assert.Same(arterial, building.Attachment?.Segment);
        Assert.True(building.AttachNearest(net, 20));
        Assert.Same(sideStreet, building.Attachment?.Segment);
    }

    [Fact]
    public void ARealRampIsRequiredToTransferFromOrdinaryRoadToExpressway()
    {
        var net = new RoadNetwork();
        var a = Node(1, 0, 0);
        var b = Node(2, 20, 0);
        var c = Node(3, 40, 0);
        var d = Node(4, 35, 5, 4);
        var incoming = net.AddSegment(a, b, 20).AddLane(LaneDirection.Forward);
        var mainline = net.AddSegment(b, c, 20, new RoadProfile(UrbanClass: UrbanRoadClass.Expressway))
            .AddLane(LaneDirection.Forward);
        Assert.Throws<ArgumentException>(() => net.SetTurn(incoming, mainline));
        Assert.False(net.FindPath(a.ID, c.ID).Found);
        var ramp = net.AddSegment(b, d, 15, new RoadProfile(Role: RoadRole.Ramp)).AddLane(LaneDirection.Forward);
        var continuation = net.AddSegment(d, c, 15, new RoadProfile(UrbanClass: UrbanRoadClass.Expressway)).AddLane(LaneDirection.Forward);
        Assert.Equal(new[] { incoming, ramp, continuation }, net.FindPath(a.ID, c.ID).Lanes);
    }

    [Fact]
    public void LaneRemovalStopsExistingItineraryAndDoesNotReopenDefaultDirections()
    {
        var net = new RoadNetwork();
        var a = Node(1, 0, 0);
        var b = Node(2, 10, 0);
        var segment = net.AddSegment(a, b, 10);
        var lane = segment.AddLane(LaneDirection.Forward, VehicleKind.Truck);
        var traveler = new Thing();
        var center = new PathCenter(net);
        Assert.True(TripStarter.Request(traveler, center, At(segment, .25f), At(segment, .75f),
            travel: new TravelOptions(VehicleKind.Truck)));
        center.Process(1);
        Assert.True(traveler.Route!.Reachable);
        Assert.True(segment.RemoveLane(lane));
        Assert.True(traveler.NeedsReplan);
        Assert.False(traveler.Advance());
        Assert.False(net.FindPath(a.ID, b.ID, travel: new TravelOptions(VehicleKind.Truck)).Found);
        Assert.False(net.FindPath(b.ID, a.ID).Found);
    }

    [Fact]
    public void MedianAttachmentUsesOpeningSideRatherThanCarriagewayCenter()
    {
        var net = new RoadNetwork();
        var segment = net.AddSegment(Node(1, 0, 0), Node(2, 100, 0), 100, new RoadProfile(HasMedian: true));
        var a = new Building(Pos(50, -10), Pos(50, -6)) { VehicleAccessPoint = Pos(50, -3) };
        var b = new Building(Pos(60, 10), Pos(60, 6));
        Assert.True(a.AttachNearest(net));
        Assert.True(b.AttachNearest(net));
        Assert.Equal(TravelDirection.Forward, a.Attachment!.EnterFrom);
        Assert.Equal(TravelDirection.Reverse, b.Attachment!.ExitToward);
        Assert.False(net.PlanTrip(a, b).Reachable);
        Assert.Equal(-3, RouteEnd.FromBuilding(a).Access.y);
    }

    [Fact]
    public void LandmarkHeuristicMatchesDijkstraAcrossMixedLaneGraph()
    {
        var net = new RoadNetwork();
        var nodes = Enumerable.Range(1, 12).Select(i => Node(i, i % 4, i / 4)).ToArray();
        var random = new Random(15);
        for (int i = 0; i < nodes.Length; i++)
        for (int j = i + 1; j < nodes.Length; j++)
        {
            if (random.Next(4) >= 2) continue;
            var edge = net.AddSegment(nodes[i], nodes[j], random.Next(1, 12));
            int directions = random.Next(1, 4);
            if ((directions & 1) != 0) edge.AddLane(LaneDirection.Forward);
            if ((directions & 2) != 0) edge.AddLane(LaneDirection.Reverse);
        }
        foreach (var from in nodes)
        foreach (var to in nodes)
        {
            var dijkstra = net.FindPath(from.ID, to.ID, PathAlgorithm.Dijkstra);
            var alt = net.FindPath(from.ID, to.ID);
            Assert.Equal(dijkstra.Found, alt.Found);
            Assert.Equal(dijkstra.TotalCost, alt.TotalCost);
        }
    }

    private static RoadAttachment Attachment(RoadSegment segment, float t) => new(segment, t);

    private static RouteEnd At(RoadSegment segment, float t) => RouteEnd.OnRoad(Attachment(segment, t));

    private static RoadNode Node(long id, float x, float y, float z = 0) =>
        new(Pos(x, y, z), id);

    private static WorldPosition Pos(float x, float y, float z = 0) => new() { x = x, y = y, z = z };
}
