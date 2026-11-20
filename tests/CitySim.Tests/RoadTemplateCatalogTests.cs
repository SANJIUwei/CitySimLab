namespace CitySim.Tests;

public sealed class RoadTemplateCatalogTests
{
    [Fact]
    public void ProvidesStableUrbanAndHighwayTemplates()
    {
        var catalogue = RoadTemplateCatalog.MainlandChinaModern(new(0.14));
        Assert.Equal(new[]
        {
            "urban.local.two-way", "urban.local.one-way", "urban.collector", "urban.arterial",
            "urban.expressway", "urban.expressway.elevated", "urban.frontage", "urban.ramp",
            "highway.motorway", "highway.first", "highway.second", "highway.third", "highway.fourth"
        }, catalogue.Select(x => x.Id));
        Assert.All(catalogue, template => Assert.Null(template.Profile.PostedSpeedKph));
        Assert.All(catalogue, template => Assert.Same(template.Design, template.Profile.Engineering));
    }

    [Fact]
    public void SelectionRequiresExplicitScaleAndSupportedPlaceAndEra()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RoadTemplateCatalog.MainlandChinaModern(new(double.NaN)));
        Assert.Throws<ArgumentOutOfRangeException>(() => RoadTemplateCatalog.MainlandChinaModern(new(0)));
        Assert.Throws<ArgumentException>(() => RoadTemplateCatalog.MainlandChinaModern(new(1, "上海")));
        Assert.Throws<ArgumentException>(() => RoadTemplateCatalog.MainlandChinaModern(new(1, null, new(2021, 12, 31))));
        Assert.Throws<ArgumentException>(() => RoadTemplateCatalog.MainlandChinaModern(new(1, "济南", new(2026, 4, 14))));
        Assert.NotEmpty(RoadTemplateCatalog.MainlandChinaModern(new(1, "济南", new(2026, 4, 15))));
    }

    [Fact]
    public void OnlySelectedJinanContextAddsLocalMinimumsAndRedLines()
    {
        var national = Template("urban.arterial");
        var local = Template("urban.arterial", "济南");
        Assert.Null(national.Design.MinMotorLaneWidthMeters);
        Assert.Null(national.Design.NominalRedLineWidthMeters);
        Assert.Equal(1.5, national.Design.MinSidewalkWidthMeters);
        Assert.Equal(50, national.Profile.DesignSpeedKph);
        Assert.Equal(60, national.Design.MinStoppingSightDistanceMeters);
        Assert.True(national.Design.PhysicallySeparateBicycles);
        Assert.Equal(3.25, local.Design.MinMotorLaneWidthMeters);
        Assert.Equal(50, local.Design.NominalRedLineWidthMeters);
        Assert.Equal(3, local.Design.MinSidewalkWidthMeters);
        Assert.Equal(3, Template("urban.frontage", "济南").Design.MinSidewalkWidthMeters);
        Assert.Equal(3.5, local.Design.MinBikePathWidthMeters);
        Assert.Equal(1.5, Template("urban.local.two-way", "济南").Design.MinBikePathWidthMeters);
        Assert.Equal(110, Template("urban.expressway").Design.MinStoppingSightDistanceMeters);
        Assert.Null(Template("urban.expressway").Design.MaxLongitudinalGradePercent);
    }

    [Fact]
    public void OneWayMotorAndBicycleDoesNotRestrictPedestrianDirection()
    {
        var oneWay = Template("urban.local.one-way");
        var reverse = Assert.Single(oneWay.Lanes, x => x.Direction == LaneDirection.Reverse);
        Assert.Equal(VehicleKind.Pedestrian, reverse.AllowedVehicles);
        var express = Template("urban.expressway.elevated");
        Assert.True(express.Profile.HasMedian);
        Assert.Equal(RoadStructure.Elevated, express.Profile.Structure);
        Assert.Equal(1, express.Profile.Level);
        Assert.All(express.Lanes, x => Assert.False((x.AllowedVehicles & (VehicleKind.Pedestrian | VehicleKind.Bicycle)) != 0));
        Assert.False(express.Profile.AllowsBuildingAccess);
    }

    [Fact]
    public void SetbackUsesMapScaleAndDesignSpeedNeverSetsPostedSpeed()
    {
        var frontage = Template("urban.frontage");
        var a = new RoadNode(P(0, 0, 0), 1);
        var b = new RoadNode(P(10, 0, 0), 2);
        var plan = frontage.MakePlan(a, b, postedSpeedKph: 25, approvedAccessSetbackMeters: 20);
        Assert.Equal(40, plan.Profile.DesignSpeedKph);
        Assert.Equal(25, plan.Profile.PostedSpeedKph);
        Assert.Equal(40, plan.Profile.MinimumAccessSetback);
        Assert.Equal(10, plan.Cost);
        Assert.Null(frontage.MakePlan(a, b).Profile.PostedSpeedKph);
        Assert.Null(Template("urban.ramp").Profile.DesignSpeedKph);
        Assert.False(frontage.Profile.AllowsBuildingAccess);
    }

    [Fact]
    public void HighwayBaselineChecksStraightGradesUsingHorizontalRun()
    {
        var motorway = Template("highway.motorway");
        Assert.Equal(3.75, motorway.Design.MinMotorLaneWidthMeters);
        Assert.Equal(4, motorway.Design.MaxLongitudinalGradePercent);
        Assert.Equal(160, motorway.Design.MinStoppingSightDistanceMeters);
        Assert.True(motorway.Design.CheckStraightGrade(P(0, 0, 0), P(100, 0, 4)));
        Assert.False(motorway.Design.CheckStraightGrade(P(0, 0, 0), P(100, 0, 5)));
        Assert.False(motorway.Design.CheckStraightGrade(P(0, 0, 0), P(0, 0, 5)));
        Assert.Null(Template("urban.arterial").Design.CheckStraightGrade(P(0, 0, 0), P(100, 0, 5)));
    }

    static RoadBuildTemplate Template(string id, string? locality = null) =>
        RoadTemplateCatalog.MainlandChinaModern(new(0.5, locality)).Single(x => x.Id == id);
    static WorldPosition P(int x, int y, int z) => new() { x = x, y = y, z = z };
}
