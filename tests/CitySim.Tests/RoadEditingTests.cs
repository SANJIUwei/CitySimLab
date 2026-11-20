namespace CitySim.Tests;

public sealed class RoadEditingTests
{
    static readonly RoadConstructionLane[] TwoWay =
        { new(LaneDirection.Forward), new(LaneDirection.Reverse) };

    [Fact]
    public void SplittingPreservesDirectionCostsProfileAndBuildingAttachment()
    {
        var net = new RoadNetwork();
        var road = net.AddSegment(Node(1, 0, 0), Node(2, 100, 0), 30, new RoadProfile(PostedSpeedKph: 45));
        var window = new TravelWindow(100, 800);
        road.AddLane(LaneDirection.Forward, VehicleKind.Car, window, 31);
        var place = new Building(At(65, -6), At(65, -4));
        place.AttachTo(road, .65f, TravelDirection.Forward, TravelDirection.Forward);
        Assert.True(net.TrySplitSegment(road, Node(3, 25, 0), out var split, out var failure, new[] { place }));
        var result = Assert.IsType<RoadSplitResult>(split);
        Assert.Equal(RoadEditFailure.None, failure);
        Assert.False(net.ContainsSegment(road));
        Assert.Same(road.Profile, result.First.Profile);
        Assert.Equal(7.5, result.First.Cost, 6);
        Assert.Equal(22.5, result.Second.Cost, 6);
        foreach (var part in new[] { result.First, result.Second })
        {
            var lane = Assert.Single(part.Lanes);
            Assert.Equal(LaneDirection.Forward, lane.Direction);
            Assert.Equal(VehicleKind.Car, lane.AllowedVehicles);
            Assert.Equal(window, lane.ActiveWindow);
            Assert.Equal(31, lane.PostedSpeedKph);
        }
        Assert.Same(result.Second, place.Attachment!.Segment);
        Assert.Equal(.5333333f, place.Attachment.T, 5);
        Assert.Equal(TravelDirection.Forward, place.Attachment.EnterFrom);
        var options = new TravelOptions(VehicleKind.Car, 500);
        foreach (var algorithm in Enum.GetValues<PathAlgorithm>())
        {
            Assert.Equal(30, net.FindPath(1, 2, algorithm, travel: options).TotalCost, 6);
            Assert.False(net.FindPath(2, 1, algorithm, travel: options).Found);
        }
    }

    [Fact]
    public void SplitRemapsEndpointTurnAndExistingSignal()
    {
        var net = new RoadNetwork();
        var junction = Node(2, 100, 0);
        var road = net.AddSegment(Node(1, 0, 0), junction, 30);
        var incoming = road.AddLane(LaneDirection.Forward);
        var outgoing = net.AddSegment(junction, Node(3, 100, 20), 10).AddLane(LaneDirection.Forward);
        net.SetTurn(incoming, outgoing, waitingCost: 3);
        net.SetSignalPlan(junction, new SignalPlan(new[] { new SignalPhase(5, 1, 1, new SignalMovement(incoming, outgoing)) }));
        Assert.True(net.TrySplitSegment(road, Node(4, 50, 0), out var split, out _));
        var newIncoming = Assert.Single(split!.Second.Lanes);
        Assert.Equal(0, net.WaitUntilTurnPermitted(newIncoming, outgoing, 1));
        Assert.Equal(1, net.WaitUntilTurnPermitted(newIncoming, outgoing, 6));
        Assert.Null(net.WaitUntilTurnPermitted(incoming, outgoing, 1));
        Assert.Equal(43, net.FindPath(1, 3).TotalCost, 7);
    }

    [Fact]
    public void RefusesSplitThatWouldLoseExistingBuildingAccess()
    {
        var net = new RoadNetwork();
        var road = net.AddSegment(Node(1, 0, 0), Node(2, 100, 0), profile: new RoadProfile(MinimumAccessSetback: 10));
        var place = new Building(At(50, -2), At(50, -2));
        place.AttachTo(road, .5f);
        long version = net.TopologyVersion;
        Assert.False(net.TrySplitSegment(road, Node(3, 50, 0), out _, out var failure, new[] { place }));
        Assert.Equal(RoadEditFailure.BuildingAccessWouldBeLost, failure);
        Assert.Equal(version, net.TopologyVersion);
        Assert.Same(road, place.Attachment!.Segment);
        Assert.Single(net.Segments);
    }

    [Fact]
    public void TeeBuildsSharedJunctionButWrongLayerLeavesMapUnchanged()
    {
        var net = new RoadNetwork();
        var road = net.AddSegment(Node(1, 0, 0), Node(2, 100, 0), 4);
        var split = Node(3, 50, 0);
        var end = Node(4, 50, 20);
        var plan = new RoadConstructionPlan(split, end, new RoadProfile(Level: 1), 2, TwoWay);
        long version = net.TopologyVersion;
        Assert.False(net.PreviewConstructionAtJunction(road, split, plan).CanBuild);
        Assert.False(net.TryConstructAtJunction(road, split, plan, out _, out _, out _));
        Assert.Single(net.Segments);
        Assert.Equal(version, net.TopologyVersion);

        plan = plan with { Profile = new RoadProfile() };
        Assert.True(net.TryConstructAtJunction(road, split, plan, out var result, out var branch, out _));
        Assert.Same(split, result!.Junction);
        Assert.Same(split, branch!.StartNode);
        Assert.Equal(new long[] { 1, 3, 4 }, net.FindPath(1, 4).NodeIds);
    }

    [Fact]
    public void DoubleTapJoinsTwoRoadsOnlyAfterBothSplitsPass()
    {
        var net = new RoadNetwork();
        var first = net.AddSegment(Node(1, 0, 0), Node(2, 100, 0), 4);
        var second = net.AddSegment(Node(3, 0, 100), Node(4, 100, 100), 4);
        var start = Node(5, 25, 0);
        var end = Node(6, 25, 100);
        var plan = new RoadConstructionPlan(start, end, new RoadProfile(), 4, TwoWay);
        Assert.True(net.PreviewConstructionAcrossJunctions(first, second, plan).CanBuild);
        Assert.Equal(2, net.Segments.Count);
        var badEnd = plan with { End = Node(6, 25, 90) };
        Assert.False(net.TryConstructAcrossJunctions(first, second, badEnd, out _, out _, out _, out _));
        Assert.True(net.ContainsSegment(first));
        Assert.True(net.ContainsSegment(second));
        Assert.Equal(4, net.Nodes.Count);

        Assert.True(net.TryConstructAcrossJunctions(first, second, plan, out var a, out var b, out var link, out _));
        Assert.Same(a!.Junction, link!.StartNode);
        Assert.Same(b!.Junction, link.EndNode);
        Assert.Equal(5, net.Segments.Count);
        Assert.Equal(new long[] { 1, 5, 6, 4 }, net.FindPath(1, 4).NodeIds);
    }

    static WorldPosition At(float x, float y) => new() { x = x, y = y };
    static RoadNode Node(long id, float x, float y) => new(At(x,y), id);
}
