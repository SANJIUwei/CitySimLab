namespace CitySim.Tests;

public sealed class RoadEngineeringGeometryTests
{
    private static readonly RoadConstructionLane[] Forward = { new(LaneDirection.Forward, VehicleKind.Car) };

    [Fact]
    public void SelectedTechnicalTemplateRejectsExcessGradeBeforeEditing()
    {
        var net = new RoadNetwork();
        var road = RoadTemplateCatalog.MainlandChinaModern(new RoadDesignContext(1))
            .Single(template => template.Id == "highway.motorway");
        var a = Node(1, 0, 0, 0);
        var b = Node(2, 100, 0, 6);
        Assert.Contains(net.PreviewConstruction(road.MakePlan(a,b)).Issues, x => x.Kind == RoadConstructionIssueKind.ExcessiveGrade);
        Assert.Empty(net.Segments);
    }

    [Fact]
    public void DeclaredLevelDoesNotBypassKnownMinimumGeometricClearance()
    {
        var net = new RoadNetwork();
        var lower = new RoadProfile(Engineering: new RoadEngineeringDesign("test profile", "test", MinMotorClearanceMeters: 4.5),
            EngineeringMetersPerUnit: .5);
        net.AddSegment(Node(1,-10,0,0),Node(2,10,0,0),20,lower);
        var bridge = new RoadProfile(Level: 1, Structure: RoadStructure.Bridge);
        var plan = new RoadConstructionPlan(Node(3,0,-10,8),Node(4,0,10,8),bridge,20,Forward);
        Assert.False(net.TryConstruct(plan, out _, out var insufficient));
        Assert.Contains(insufficient.Issues, x => x.Kind == RoadConstructionIssueKind.InsufficientGeometricClearance);
        var clear = plan with { Start = Node(3,0,-10,10), End = Node(4,0,10,10) };
        Assert.True(net.TryConstruct(clear, out _, out _));
        Assert.False(net.FindPath(1,4).Found);
    }

    private static RoadNode Node(int id, float x, float y, float z) =>
        new(new WorldPosition { x=x, y=y, z=z }, id);
}
