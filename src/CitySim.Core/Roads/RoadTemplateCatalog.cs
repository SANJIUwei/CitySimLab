using System;
using System.Collections.Generic;

namespace CitySim.Core;

/// <summary>调用者显式选择地图比例；济南是可选地方参考，不是全国默认。</summary>
public sealed record RoadDesignContext(double MetersPerUnit, string? Locality = null, DateTime? ReferenceDate = null)
{
    internal void Validate()
    {
        if (!double.IsFinite(MetersPerUnit) || MetersPerUnit <= 0)
            throw new ArgumentOutOfRangeException(nameof(MetersPerUnit), "Map scale must be finite and positive.");
        if (Locality is not (null or "济南" or "Jinan"))
            throw new ArgumentException("No verified local profile is available for this locality.", nameof(Locality));
        if (ReferenceDate is DateTime date &&
            date.Date < (Locality == null ? new DateTime(2022, 1, 1) : new DateTime(2026, 4, 15)))
            throw new ArgumentException("The selected modern standards were not yet in force at that date.", nameof(ReferenceDate));
    }

    public double MetersToUnits(double meters)
    {
        if (!double.IsFinite(MetersPerUnit) || MetersPerUnit <= 0)
            throw new ArgumentOutOfRangeException(nameof(MetersPerUnit));
        if (!double.IsFinite(meters) || meters < 0)
            throw new ArgumentOutOfRangeException(nameof(meters));
        double units = meters / MetersPerUnit;
        if (!double.IsFinite(units))
            throw new ArgumentOutOfRangeException(nameof(meters));
        return units;
    }
}

/// <summary>工程参考值，非道路运营限速。null 表示必须另按适用道路、地形或规范审定。</summary>
public sealed record RoadEngineeringDesign(
    string Reference, string Scope,
    double? MinMotorLaneWidthMeters = null, double? MinSidewalkWidthMeters = null,
    double? MinBikePathWidthMeters = null, double? MinMotorClearanceMeters = null,
    double? MinStoppingSightDistanceMeters = null, double? MaxLongitudinalGradePercent = null,
    bool PhysicallySeparateBicycles = false, double? NominalRedLineWidthMeters = null)
{
    /// <summary>直线纵坡预检。未知工程值不声称通过施工审查。</summary>
    public bool? CheckStraightGrade(WorldPosition from, WorldPosition to)
    {
        if (MaxLongitudinalGradePercent == null)
            return null;
        double dx = (double)to.x - from.x;
        double dy = (double)to.y - from.y;
        double dz = Math.Abs((double)to.z - from.z);
        double run = Math.Sqrt(dx * dx + dy * dy);
        return double.IsFinite(run) && run > 0 && double.IsFinite(dz) &&
            dz * 100 <= run * MaxLongitudinalGradePercent.Value;
    }
}

public sealed class RoadBuildTemplate
{
    public string Id { get; }
    public string Name { get; }
    public RoadProfile Profile { get; }
    public IReadOnlyList<RoadConstructionLane> Lanes { get; }
    public RoadEngineeringDesign Design { get; }
    public RoadDesignContext Context { get; }

    internal RoadBuildTemplate(string id, string name, RoadProfile profile,
        RoadConstructionLane[] lanes, RoadEngineeringDesign design, RoadDesignContext context)
    {
        Id = id;
        Name = name;
        Profile = profile with { Engineering = design, EngineeringMetersPerUnit = context.MetersPerUnit };
        Lanes = Array.AsReadOnly(lanes);
        Design = design;
        Context = context;
    }

    /// <summary>明确传入运营限速或经审定的开口退距；不从工程设计速度推出任一值。</summary>
    public RoadConstructionPlan MakePlan(RoadNode start, RoadNode end, int? level = null,
        double? postedSpeedKph = null, double? cost = null, double? approvedAccessSetbackMeters = null)
    {
        if (postedSpeedKph is double speed && (!double.IsFinite(speed) || speed <= 0))
            throw new ArgumentOutOfRangeException(nameof(postedSpeedKph));
        var profile = Profile with
        {
            Level = level ?? Profile.Level,
            PostedSpeedKph = postedSpeedKph,
            MinimumAccessSetback = approvedAccessSetbackMeters is double setback
                ? Context.MetersToUnits(setback) : Profile.MinimumAccessSetback
        };
        return new RoadConstructionPlan(start, end, profile,
            cost ?? WorldPosition.Distance(start.Position, end.Position), Lanes);
    }
}

/// <summary>可选择的代表性断面。不是各道路等级唯一允许的工程方案。</summary>
public static class RoadTemplateCatalog
{
    const VehicleKind Motor = VehicleKind.Car | VehicleKind.Bus | VehicleKind.Truck;
    const string NationalUrban = "GB 55011-2021 §3.1.2/3.1.4/3.2.3/3.4";
    const string JinanUrban = NationalUrban + "；济自然规划发〔2026〕26号 第106、108、114条";
    const string NationalHighway = "JTG B01-2014 §3.1.2/3.5.1/4.0.2–4.0.4/4.0.15/4.0.20";

    public static IReadOnlyList<RoadBuildTemplate> MainlandChinaModern(RoadDesignContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        context.Validate();
        bool jinan = context.Locality != null;
        string scope = jinan ? "中国大陆现代城市道路；济南市辖区新改建道路常规参考"
            : "中国大陆现代城市道路；不代选地方断面或地块开口审批";

        RoadBuildTemplate Urban(string id, string name, UrbanRoadClass roadClass, int speed,
            int lanesPerMotorDirection, bool oneWay = false, RoadRole role = RoadRole.Ordinary,
            RoadStructure structure = RoadStructure.AtGrade, int level = 0)
        {
            bool isMainline = role == RoadRole.Mainline;
            var sidewalk = isMainline ? (double?)null : jinan
                ? role == RoadRole.Frontage ? 3
                    : roadClass switch { UrbanRoadClass.Arterial => 3, UrbanRoadClass.Collector => 2.5, _ => 2 }
                : 1.5;
            var bicycle = isMainline ? (double?)null : jinan && role == RoadRole.Frontage ? 3.5 :
                jinan && roadClass is UrbanRoadClass.Arterial or UrbanRoadClass.Collector ? 3.5 : 1.5;
            var redLine = jinan && role != RoadRole.Frontage ? roadClass switch
            {
                UrbanRoadClass.Arterial => 50d,
                UrbanRoadClass.Collector => 32d,
                UrbanRoadClass.Local => oneWay ? 15d : 20d,
                _ => (double?)null
            } : null;

            int priority = roadClass switch
            {
                UrbanRoadClass.Local => 0,
                UrbanRoadClass.Collector => 1,
                _ => 2
            };
            var profile = new RoadProfile(roadClass, role,
                isMainline ? BuildingAccessPolicy.Prohibited : BuildingAccessPolicy.Unspecified,
                Structure: structure, Level: level, DesignSpeedKph: speed, AccessPriority: priority,
                HasMedian: isMainline || roadClass == UrbanRoadClass.Arterial);
            var design = new RoadEngineeringDesign(jinan ? JinanUrban : NationalUrban,
                jinan && roadClass == UrbanRoadClass.Local ? scope + "（Ⅱ类支路断面参考）" : scope,
                MinMotorLaneWidthMeters: jinan ? 3.25 : null, MinSidewalkWidthMeters: sidewalk,
                MinBikePathWidthMeters: bicycle, MinMotorClearanceMeters: 4.5,
                MinStoppingSightDistanceMeters: UrbanStoppingSightDistance(speed),
                PhysicallySeparateBicycles: !isMainline && speed > 40, NominalRedLineWidthMeters: redLine);
            return new RoadBuildTemplate(id, name, profile, MakeLanes(lanesPerMotorDirection, oneWay, !isMainline),
                design, context);
        }

        RoadBuildTemplate Highway(string id, string name, HighwayTechnicalGrade grade, int speed,
            int lanesPerDirection, double width, double sight, double gradeMax)
        {
            bool divided = grade is HighwayTechnicalGrade.Motorway or HighwayTechnicalGrade.First;
            var profile = new RoadProfile(Role: grade == HighwayTechnicalGrade.Motorway ? RoadRole.Mainline : RoadRole.Ordinary,
                BuildingAccess: BuildingAccessPolicy.Prohibited, HighwayGrade: grade, DesignSpeedKph: speed, HasMedian: divided);
            var design = new RoadEngineeringDesign(NationalHighway, "中国大陆公路常规选型；沿线开口须另行审定",
                MinMotorLaneWidthMeters: width, MinStoppingSightDistanceMeters: sight, MaxLongitudinalGradePercent: gradeMax);
            return new RoadBuildTemplate(id, name, profile, MakeLanes(lanesPerDirection, false, false), design, context);
        }

        var ramp = new RoadBuildTemplate("urban.ramp", "连接匝道（单向）",
            new RoadProfile(Role: RoadRole.Ramp, BuildingAccess: BuildingAccessPolicy.Prohibited),
            MakeLanes(1, true, false),
            new RoadEngineeringDesign("GB 55011-2021 §4.0.7", "互通匝道；速度、断面、净空按该互通专项设计"),
            context);
        return Array.AsReadOnly(new[]
        {
            Urban("urban.local.two-way", "双向支路", UrbanRoadClass.Local, 30, 1),
            Urban("urban.local.one-way", "单向支路", UrbanRoadClass.Local, 30, 1, oneWay: true),
            Urban("urban.collector", "次干路", UrbanRoadClass.Collector, 40, 2),
            Urban("urban.arterial", "主干路", UrbanRoadClass.Arterial, 50, 3),
            Urban("urban.expressway", "地面快速路", UrbanRoadClass.Expressway, 80, 2, role: RoadRole.Mainline),
            Urban("urban.expressway.elevated", "高架快速路", UrbanRoadClass.Expressway, 80, 2,
                role: RoadRole.Mainline, structure: RoadStructure.Elevated, level: 1),
            Urban("urban.frontage", "快速路辅路", UrbanRoadClass.Collector, 40, 1, role: RoadRole.Frontage),
            ramp,
            Highway("highway.motorway", "高速公路", HighwayTechnicalGrade.Motorway, 100, 2, 3.75, 160, 4),
            Highway("highway.first", "一级公路", HighwayTechnicalGrade.First, 100, 2, 3.75, 160, 4),
            Highway("highway.second", "二级公路", HighwayTechnicalGrade.Second, 80, 1, 3.75, 110, 5),
            Highway("highway.third", "三级公路", HighwayTechnicalGrade.Third, 40, 1, 3.50, 40, 7),
            Highway("highway.fourth", "四级公路", HighwayTechnicalGrade.Fourth, 30, 1, 3.25, 30, 8)
        });
    }

    static int UrbanStoppingSightDistance(int designSpeed) => designSpeed switch
    {
        100 => 160, 80 => 110, 60 => 70, 50 => 60, 40 => 40, 30 => 30, 20 => 20,
        _ => throw new ArgumentOutOfRangeException(nameof(designSpeed))
    };

    static RoadConstructionLane[] MakeLanes(int motorPerDirection, bool oneWay, bool hasSlowNetwork)
    {
        var lanes = new List<RoadConstructionLane>();
        for (int i = 0; i < motorPerDirection; i++)
            lanes.Add(new RoadConstructionLane(LaneDirection.Forward, Motor));
        if (!oneWay)
            for (int i = 0; i < motorPerDirection; i++)
                lanes.Add(new RoadConstructionLane(LaneDirection.Reverse, Motor));
        if (hasSlowNetwork)
        {
            lanes.Add(new RoadConstructionLane(LaneDirection.Forward, VehicleKind.Bicycle));
            if (!oneWay)
                lanes.Add(new RoadConstructionLane(LaneDirection.Reverse, VehicleKind.Bicycle));
            lanes.Add(new RoadConstructionLane(LaneDirection.Forward, VehicleKind.Pedestrian));
            lanes.Add(new RoadConstructionLane(LaneDirection.Reverse, VehicleKind.Pedestrian));
        }
        return lanes.ToArray();
    }
}
