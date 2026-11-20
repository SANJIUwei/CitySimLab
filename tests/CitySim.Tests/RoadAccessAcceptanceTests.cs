namespace CitySim.Tests;

public sealed class RoadAccessAcceptanceTests
{
    [Fact]
    public void PreviewRanksLegalCandidatesAndExplainsRejectedNearbyRoads()
    {
        var net = new RoadNetwork();
        var factory = new Building(At(10, 0), At(11, 0)) { VehicleAccessPoint = At(10, 1) };
        var mainline = Line(net, 1, 0, new(UrbanClass: UrbanRoadClass.Expressway));
        var deck = Line(net, 3, 2, new(Level: 1, Structure: RoadStructure.Elevated));
        var barrier = Line(net, 5, 3, new());
        var arterial = Line(net, 7, 4, new(AccessPriority: 2));
        var branch = Line(net, 9, 5, new(UrbanClass: UrbanRoadClass.Local, AccessPriority: 1));
        var setback = Line(net, 11, 6, new(MinimumAccessSetback: 11));
        var walking = Line(net, 13, 7, new());
        walking.AddLane(LaneDirection.Forward, VehicleKind.Pedestrian);
        var remote = Line(net, 15, 20, new());
        factory.AccessConnectionFilter = (road, _, _) => road != barrier;

        var preview = factory.PreviewAccess(net, maxDistance: 9);
        Assert.Null(factory.Attachment);
        Assert.Equal(new[] { branch, arterial }, preview.Where(x => x.IsEligible).Select(x => x.Segment));
        var reasons = preview.ToDictionary(x => x.Segment, x => x.Failure);
        Assert.Equal(RoadAccessFailure.RoadProhibitsAccess, reasons[mainline]);
        Assert.Equal(RoadAccessFailure.WrongLevel, reasons[deck]);
        Assert.Equal(RoadAccessFailure.BlockedConnection, reasons[barrier]);
        Assert.Equal(RoadAccessFailure.IntersectionSetback, reasons[setback]);
        Assert.Equal(RoadAccessFailure.NoMotorVehicleLane, reasons[walking]);
        Assert.Equal(RoadAccessFailure.TooFar, reasons[remote]);
        Assert.True(factory.AttachNearest(net, maxDistance: 9));
        Assert.Same(branch, factory.Attachment!.Segment);
        Assert.Equal(.5f, factory.Attachment.T);
        Assert.False(factory.TryAttachTo(net, barrier, .5f, out var error));
        Assert.Equal(RoadAccessFailure.BlockedConnection, error);
        Assert.Same(branch, factory.Attachment!.Segment);
    }

    [Fact]
    public void ManualOpeningCannotCrossMedianOrReferenceForeignRoad()
    {
        var net = new RoadNetwork();
        var other = new RoadNetwork();
        var road = Line(net, 1, 0, new(HasMedian: true));
        var foreign = Line(other, 1, 0, new());
        var building = new Building(At(10, -1), At(10, -2));
        var center = new Building(At(10, 0), At(10, 0));
        Assert.Equal(RoadAccessFailure.MedianOrDirection, center.PreviewAccessAt(net, road, .5f).Failure);
        Assert.False(building.TryAttachTo(net, foreign, .5f, out var reason));
        Assert.Equal(RoadAccessFailure.RoadNotInNetwork, reason);
        Assert.False(building.TryAttachTo(net, road, .5f, out reason, TravelDirection.Reverse));
        Assert.Equal(RoadAccessFailure.InvalidDirection, reason);
        Assert.Null(building.Attachment);
        Assert.True(building.TryAttachTo(net, road, .5f, out reason,
            enterFrom: TravelDirection.Forward, exitToward: TravelDirection.None));
        Assert.Equal(RoadAccessFailure.None, reason);
        Assert.Equal(TravelDirection.Forward, building.Attachment!.EnterFrom);
        Assert.Equal(TravelDirection.None, building.Attachment.ExitToward);
    }

    [Fact]
    public void PreviewCanEvaluateTheRequestedVehicleAndAccessWindow()
    {
        var net = new RoadNetwork();
        var road = Line(net, 1, 0, new());
        road.AddLane(LaneDirection.Forward, VehicleKind.Truck, new TravelWindow(360, 600));
        var building = new Building(At(10, 1), At(10, 2));
        Assert.False(building.PreviewAccessAt(net, road, .5f, travel: new(VehicleKind.Car, 480)).IsEligible);
        Assert.False(building.PreviewAccessAt(net, road, .5f, travel: new(VehicleKind.Truck, 600)).IsEligible);
        var allowed = building.PreviewAccessAt(net, road, .5f, travel: new(VehicleKind.Truck, 480));
        Assert.Equal(TravelDirection.Forward, allowed.AllowedDirections);
    }

    [Fact]
    public void TruckTripUsesOnlyLegalOpeningAndReplansOrFailsAfterAccessEdit()
    {
        var net = new RoadNetwork();
        var localA = Node(1, 0, 0);
        var localB = Node(2, 10, 0);
        var localC = Node(3, 20, 0);
        var localD = Node(4, 30, 0);
        var factoryRoad = net.AddSegment(localA, localB, 10, new(UrbanClass: UrbanRoadClass.Local));
        var collector = net.AddSegment(localB, localC, 10, new(UrbanClass: UrbanRoadClass.Collector));
        var warehouseRoad = net.AddSegment(localC, localD, 10, new(UrbanClass: UrbanRoadClass.Local));
        var mainline = net.AddSegment(Node(5, 0, -1.25f), Node(6, 30, -1.25f), 1,
            new(UrbanClass: UrbanRoadClass.Expressway, Role: RoadRole.Mainline));
        var detourA = Node(7, 0, 5);
        var detourB = Node(8, 20, 5);
        var detourStart = net.AddSegment(localA, detourA, 5);
        var detourMid = net.AddSegment(detourA, detourB, 20);
        var detourEnd = net.AddSegment(detourB, localC, 5);

        var factory = new Building(At(2, -3), At(2, -2)) { VehicleAccessPoint = At(2, -1) };
        var warehouse = new Building(At(28, -3), At(28, -2)) { VehicleAccessPoint = At(28, -1) };
        Assert.True(factory.AttachNearest(net, maxDistance: 3));
        Assert.Same(factoryRoad, factory.Attachment!.Segment);
        Assert.True(warehouse.TryAttachTo(net, warehouseRoad, .8f, out _));
        var truck = new TravelOptions(VehicleKind.Truck, 8 * 60);
        Assert.True(net.TryPlanTrip(factory, warehouse, out var direct, out _, travel: truck));
        Assert.Equal(26, direct!.TotalCost, 5);
        Assert.Same(collector, Assert.Single(direct.Global.Lanes).Segment);
        Assert.DoesNotContain(direct.Global.Lanes, l => l.Segment == mainline);
        var traveler = new TimedRoadTraveler(net, direct, 8 * 60 * 60, 1, 30, 12);

        Assert.True(factory.TryAttachTo(net, factoryRoad, .2f, out _, exitToward: TravelDirection.Reverse));
        traveler.Tick(.25);
        Assert.Equal(TimedTravelState.NeedsReplan, traveler.State);
        Assert.True(net.TryPlanTrip(factory, warehouse, out var reroute, out _, travel: truck));
        Assert.Equal(new[] { detourStart, detourMid, detourEnd }, reroute!.Global.Lanes.Select(l => l.Segment));
        Assert.Equal(40, reroute.TotalCost, 5);

        Assert.True(factory.DetachAccess(net));
        Assert.False(net.TryPlanTrip(factory, warehouse, out _, out var failure, travel: truck));
        Assert.Equal(RouteFailure.NoBuildingAccess, failure);
        Assert.False(factory.DetachAccess(net));
    }

    [Fact]
    public void ChangingOnlyThePedestrianDoorPreservesTheExplicitVehicleOpening()
    {
        var net = new RoadNetwork();
        var road = Line(net, 1, 0, new());
        var building = new Building(At(10, 1), At(10, 2)) { VehicleAccessPoint = At(10, 1) };
        Assert.True(building.TryAttachTo(net, road, .5f, out _));
        long currentVersion = net.TopologyVersion;
        building.Entrance = At(12, 2);
        Assert.True(building.HasActiveAttachment(net));
        Assert.Equal(currentVersion, net.TopologyVersion);
        building.VehicleAccessPoint = At(12, 1);
        Assert.Null(building.Attachment);
        Assert.True(net.TopologyVersion > currentVersion);
    }

    static RoadSegment Line(RoadNetwork net, int first, float y, RoadProfile profile) =>
        net.AddSegment(Node(first, 0, y), Node(first + 1, 20, y), 20, profile);

    static RoadNode Node(long id, float x, float y) => new(At(x, y), id);
    static WorldPosition At(float x, float y) => new() { x = x, y = y };
}
