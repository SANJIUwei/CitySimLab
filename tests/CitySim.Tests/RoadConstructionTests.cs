namespace CitySim.Tests;

public sealed class RoadConstructionTests
{
    private static readonly RoadConstructionLane[] Forward = { new(LaneDirection.Forward) };

    [Fact]
    public void AtGradeExpresswayCrossingIsRejectedWithoutChangingNetwork()
    {
        var net = new RoadNetwork();
        net.AddSegment(Node(1, -10, 0), Node(2, 10, 0), profile: new RoadProfile(UrbanRoadClass.Expressway));
        long version = net.TopologyVersion;
        var proposed = new RoadConstructionPlan(Node(3, 0, -10), Node(4, 0, 10), new(), 1, Forward);
        Assert.False(net.PreviewConstruction(proposed).CanBuild);
        Assert.False(net.TryConstruct(proposed, out var road, out var preview));
        Assert.Null(road);
        Assert.Equal(RoadConstructionIssueKind.ControlledAccessRequiresGradeSeparation, Assert.Single(preview.Issues).Kind);
        var crossing = Assert.Single(preview.Crossings);
        Assert.Equal(RoadConstructionCrossingKind.ControlledAtGradeCrossing, crossing.Kind);
        Assert.Null(crossing.JunctionNode);
        Assert.Equal(0, crossing.Position.x);
        Assert.Equal(0, crossing.Position.y);
        Assert.Equal(version, net.TopologyVersion);
        Assert.Equal(2, net.Nodes.Count);
        Assert.Single(net.Segments);
    }

    [Fact]
    public void SeparatedCrossingDoesNotCreateTurnAndRetainsExplicitLaneDirections()
    {
        var net = new RoadNetwork();
        net.AddSegment(Node(1, -10, 0), Node(2, 10, 0), profile: new RoadProfile(UrbanRoadClass.Expressway));
        var plan = new RoadConstructionPlan(Node(3, 0, -10, 5), Node(4, 0, 10, 5),
            new RoadProfile(Structure: RoadStructure.Bridge, Level: 1), 2, Forward);
        Assert.True(net.TryConstruct(plan, out var road, out var preview));
        Assert.Equal(LaneDirection.Forward, Assert.Single(Assert.IsType<RoadSegment>(road).Lanes).Direction);
        var crossing = Assert.Single(preview.Crossings);
        Assert.Equal(RoadConstructionCrossingKind.GradeSeparated, crossing.Kind);
        Assert.Null(crossing.JunctionNode);
        Assert.All(Enum.GetValues<PathAlgorithm>(), algo =>
        {
            Assert.False(net.FindPath(1, 4, algo).Found);
            Assert.True(net.FindPath(3, 4, algo).Found);
            Assert.False(net.FindPath(4, 3, algo).Found);
        });
        Assert.Equal(4, net.Nodes.Count);
    }

    [Fact]
    public void SameLevelCrossingRequiresSharedNodeForJunction()
    {
        var net = new RoadNetwork();
        var junction = Node(2, 0, 0);
        net.AddSegment(Node(1, -10, 0), junction);
        var other = Node(4, 0, 10);
        var plan = new RoadConstructionPlan(Node(3, 0, 0), other, new(), 1, Forward);
        Assert.Equal(RoadConstructionIssueKind.JunctionRequiresSharedNode,
            Assert.Single(net.PreviewConstruction(plan).Issues).Kind);
        Assert.True(net.TryConstruct(plan with { Start = junction }, out _, out var preview));
        Assert.Same(junction, Assert.Single(preview.Crossings).JunctionNode);
        Assert.True(net.FindPath(1, 4).Found);
    }

    [Fact]
    public void RampRequiresActualSharedNodeAndCanConnectSeparatedRoads()
    {
        var net = new RoadNetwork();
        var mainAt = Node(2, 0, 0, 5);
        var localAt = Node(3, 2, 7);
        net.AddSegment(Node(1, -10, 0, 5), mainAt, profile:
            new RoadProfile(UrbanRoadClass.Expressway, Structure: RoadStructure.Elevated, Level: 1));
        net.AddSegment(localAt, Node(4, 10, 7));
        var profile = new RoadProfile(Role: RoadRole.Ramp);
        var fake = new RoadConstructionPlan(Node(5, 0, 0, 5), Node(6, 2, 7), profile, 1, Forward);
        Assert.Contains(net.PreviewConstruction(fake).Issues, x => x.Kind == RoadConstructionIssueKind.RampRequiresJunction);
        Assert.True(net.TryConstruct(fake with { Start = mainAt, End = localAt }, out _, out var preview));
        Assert.Equal(2, preview.Crossings.Count);
        Assert.All(preview.Crossings, x => Assert.Equal(RoadConstructionCrossingKind.Junction, x.Kind));
        Assert.True(net.FindPath(1, 4).Found);
        Assert.False(net.FindPath(4, 1).Found);
    }

    [Fact]
    public void PreviewIsNotAuthorizationAfterNetworkChanges()
    {
        var net = new RoadNetwork();
        var first = new RoadConstructionPlan(Node(1, -10, 0), Node(2, 10, 0), new(), 1, Forward);
        var second = new RoadConstructionPlan(Node(3, 0, -10), Node(4, 0, 10), new(), 1, Forward);
        Assert.True(net.PreviewConstruction(second).CanBuild);
        Assert.True(net.TryConstruct(first, out _, out _));
        Assert.False(net.TryConstruct(second, out _, out var current));
        Assert.Equal(RoadConstructionIssueKind.JunctionRequiresSharedNode, Assert.Single(current.Issues).Kind);
    }

    [Fact]
    public void SameLevelCollinearOverlapIsRejectedEvenWhenAnEndpointIsShared()
    {
        var net = new RoadNetwork();
        var a = Node(1, 0, 0);
        net.AddSegment(a, Node(2, 10, 0));
        var plan = new RoadConstructionPlan(a, Node(3, 15, 0), new(), 1, Forward);
        Assert.False(net.TryConstruct(plan, out _, out var invalid));
        Assert.Equal(RoadConstructionIssueKind.OverlappingRoads, Assert.Single(invalid.Issues).Kind);
        Assert.Equal(RoadConstructionCrossingKind.Overlap, Assert.Single(invalid.Crossings).Kind);
        Assert.Single(net.Segments);
    }

    [Fact]
    public void RejectsInvalidEndpointAndLaneDataWithoutPartialChanges()
    {
        var net = new RoadNetwork();
        net.AddNode(Node(1, 0, 0));
        var conflict = new RoadConstructionPlan(Node(1, 0, 0), Node(2, 0, 0, 1), new(), 1, Forward);
        var result = net.PreviewConstruction(conflict);
        Assert.Contains(result.Issues, x => x.Kind == RoadConstructionIssueKind.ConflictingNodeId);
        Assert.Contains(result.Issues, x => x.Kind == RoadConstructionIssueKind.ZeroLength);
        var malformed = new RoadConstructionPlan(Node(2, 1, 1), Node(3, 2, 2), new(), 1,
            new[] { new RoadConstructionLane(LaneDirection.Forward, PostedSpeedKph: double.NaN) });
        Assert.False(net.TryConstruct(malformed, out _, out var malformedPreview));
        Assert.Equal(0, Assert.Single(malformedPreview.Issues).LaneIndex);
        Assert.Empty(net.Segments);
        Assert.Single(net.Nodes);
    }

    [Fact]
    public void ExpresswayCanContinueButCannotCreateAnAtGradeMainlineJunction()
    {
        var net = new RoadNetwork();
        var junction = Node(2, 0, 0);
        var profile = new RoadProfile(UrbanRoadClass.Expressway);
        net.AddSegment(Node(1, -10, 0), junction, profile: profile);
        var extension = new RoadConstructionPlan(junction, Node(3, 10, 0), profile, 10, Forward);
        Assert.True(net.TryConstruct(extension, out _, out _));
        var branching = extension with { End = Node(4, 0, 10) };
        Assert.False(net.TryConstruct(branching, out _, out var preview));
        Assert.All(preview.Issues, issue =>
            Assert.Equal(RoadConstructionIssueKind.ControlledAccessRequiresGradeSeparation, issue.Kind));
        Assert.Equal(2, net.Segments.Count);
    }

    private static RoadNode Node(long id, float x, float y, float z = 0) =>
        new(new WorldPosition { x = x, y = y, z = z }, id);
}
