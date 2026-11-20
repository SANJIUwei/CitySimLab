using System;
using System.Collections.Generic;

namespace CitySim.Core;

public sealed record RoadConstructionLane(LaneDirection Direction, VehicleKind AllowedVehicles = VehicleKind.All,
    TravelWindow? ActiveWindow = null, double? PostedSpeedKph = null);

public sealed record RoadConstructionPlan(RoadNode Start, RoadNode End, RoadProfile Profile, double Cost,
    IReadOnlyList<RoadConstructionLane> Lanes);

public enum RoadConstructionIssueKind
{
    InvalidEndpoint, ConflictingNodeId, ZeroLength, InvalidCost, InvalidProfile, MissingLanes, InvalidLane,
    JunctionRequiresSharedNode, ControlledAccessRequiresGradeSeparation, OverlappingRoads, RampRequiresJunction,
    ExcessiveGrade, InsufficientGeometricClearance
}

public enum RoadConstructionCrossingKind
{
    Junction, GradeSeparated, CrossingNeedsJunction, ControlledAtGradeCrossing, Overlap
}

public sealed record RoadConstructionIssue(RoadConstructionIssueKind Kind, RoadSegment? ExistingRoad = null, int? LaneIndex = null);

public sealed record RoadConstructionCrossing(RoadSegment ExistingRoad, WorldPosition Position,
    RoadConstructionCrossingKind Kind, RoadNode? JunctionNode = null);

public sealed class RoadConstructionPreview
{
    public bool CanBuild => Issues.Count == 0;
    public IReadOnlyList<RoadConstructionIssue> Issues { get; }
    public IReadOnlyList<RoadConstructionCrossing> Crossings { get; }

    internal RoadConstructionPreview(List<RoadConstructionIssue> issues, List<RoadConstructionCrossing> crossings)
        => (Issues, Crossings) = (issues.AsReadOnly(), crossings.AsReadOnly());
}

public partial class RoadNetwork
{
    /// <summary>检查候选路段，不自动切路、建路口或修改路网。</summary>
    public RoadConstructionPreview PreviewConstruction(RoadConstructionPlan plan)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        var issues = new List<RoadConstructionIssue>();
        var crossings = new List<RoadConstructionCrossing>();
        var a = plan.Start;
        var b = plan.End;
        var p = plan.Profile;
        var hasEndpoints = ValidConstructionEndpoint(a) && ValidConstructionEndpoint(b);
        if (!hasEndpoints)
            issues.Add(new(RoadConstructionIssueKind.InvalidEndpoint));
        else if (a.ID == b.ID && !ReferenceEquals(a, b) ||
            !UsesRegisteredEndpoint(a) || !UsesRegisteredEndpoint(b))
            issues.Add(new(RoadConstructionIssueKind.ConflictingNodeId));

        bool hasLength = hasEndpoints && (a.Position.x != b.Position.x || a.Position.y != b.Position.y);
        if (hasEndpoints && !hasLength)
            issues.Add(new(RoadConstructionIssueKind.ZeroLength));

        if (!double.IsFinite(plan.Cost) || plan.Cost < 0)
            issues.Add(new(RoadConstructionIssueKind.InvalidCost));
        bool hasProfile = p != null && ValidConstructionProfile(p);
        if (!hasProfile)
            issues.Add(new(RoadConstructionIssueKind.InvalidProfile));

        if (plan.Lanes == null || plan.Lanes.Count == 0)
            issues.Add(new(RoadConstructionIssueKind.MissingLanes));
        else
            for (int i = 0; i < plan.Lanes.Count; i++)
            {
                var lane = plan.Lanes[i];
                if (lane == null || lane.Direction is not (LaneDirection.Forward or LaneDirection.Reverse) ||
                    lane.AllowedVehicles == VehicleKind.None || (lane.AllowedVehicles & ~VehicleKind.All) != 0 ||
                    hasProfile && (lane.AllowedVehicles & p!.AllowedTravelModes) == 0 ||
                    lane.ActiveWindow is TravelWindow window && window.StartMinute == window.EndMinute ||
                    !ValidConstructionSpeed(lane.PostedSpeedKph))
                    issues.Add(new(RoadConstructionIssueKind.InvalidLane, LaneIndex: i));
            }

        if (!hasLength || !hasProfile)
            return new RoadConstructionPreview(issues, crossings);
        if (p!.Engineering?.CheckStraightGrade(a.Position, b.Position) == false)
            issues.Add(new(RoadConstructionIssueKind.ExcessiveGrade));

        bool rampHasJunction = false;
        foreach (var existing in Segments)
        {
            if (!IntersectConstructionXY(a.Position, b.Position, existing.StartNode.Position,
                existing.EndNode.Position, out var point, out bool overlap))
                continue;

            var existingProfile = existing.Profile;
            bool hasRamp = p!.Role == RoadRole.Ramp || existingProfile.Role == RoadRole.Ramp;
            var shared = SharedConstructionEndpoint(a, b, existing);
            bool gradeSeparated = p.Level != existingProfile.Level;
            bool proposedMainline = IsConstructionControlledMainline(p);
            bool existingMainline = IsConstructionControlledMainline(existingProfile);
            bool controlledMismatch = proposedMainline != existingMainline;
            bool sharedMainlineBranches = proposedMainline && existingMainline && shared != null &&
                MoreThanOneMainlineAt(shared, p.Level);
            bool uncontrolledMainlineConflict = controlledMismatch || proposedMainline && existingMainline &&
                (shared == null || sharedMainlineBranches);

            RoadConstructionCrossingKind kind;
            RoadConstructionIssueKind? issue = null;
            if (gradeSeparated && (shared == null || !hasRamp || overlap))
            {
                kind = RoadConstructionCrossingKind.GradeSeparated;
                if (!HasMinimumGeometricClearance(p, existing, point))
                    issue = RoadConstructionIssueKind.InsufficientGeometricClearance;
            }
            else if (overlap)
            {
                kind = RoadConstructionCrossingKind.Overlap;
                issue = RoadConstructionIssueKind.OverlappingRoads;
            }
            else if (uncontrolledMainlineConflict && (!hasRamp || shared == null) && !gradeSeparated)
            {
                kind = RoadConstructionCrossingKind.ControlledAtGradeCrossing;
                issue = RoadConstructionIssueKind.ControlledAccessRequiresGradeSeparation;
            }
            else if (shared == null)
            {
                kind = RoadConstructionCrossingKind.CrossingNeedsJunction;
                issue = RoadConstructionIssueKind.JunctionRequiresSharedNode;
            }
            else
            {
                kind = RoadConstructionCrossingKind.Junction;
                rampHasJunction = true;
                point = shared.Position;
            }

            crossings.Add(new(existing, point, kind, kind == RoadConstructionCrossingKind.Junction ? shared : null));
            if (issue.HasValue)
                issues.Add(new(issue.Value, existing));
        }
        if (p!.Role == RoadRole.Ramp && !rampHasJunction)
            issues.Add(new(RoadConstructionIssueKind.RampRequiresJunction));
        return new RoadConstructionPreview(issues, crossings);
    }

    /// <summary>应用前重验。与其他路网编辑一样，应在寻路批次之外调用。</summary>
    public bool TryConstruct(RoadConstructionPlan plan, out RoadSegment? segment, out RoadConstructionPreview preview)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        // 固定车道列表供整次验证和创建使用，不能在验证与 AddLane 之间改变设计。
        if (plan.Lanes != null)
        {
            var lanes = new RoadConstructionLane[plan.Lanes.Count];
            for (int i = 0; i < lanes.Length; i++) lanes[i] = plan.Lanes[i];
            plan = plan with { Lanes = lanes };
        }
        preview = PreviewConstruction(plan);
        if (!preview.CanBuild)
        {
            segment = null;
            return false;
        }

        segment = AddSegment(plan.Start, plan.End, plan.Cost, plan.Profile);
        foreach (var lane in plan.Lanes!)
            segment.AddLane(lane.Direction, lane.AllowedVehicles, lane.ActiveWindow, lane.PostedSpeedKph);
        return true;
    }

    static bool ValidConstructionEndpoint(RoadNode? node) => node != null && node.ID >= 0 && node.ID <= 4_000_000 &&
        float.IsFinite(node.Position.x) && float.IsFinite(node.Position.y) && float.IsFinite(node.Position.z);

    bool UsesRegisteredEndpoint(RoadNode node) =>
        !_nodesById.TryGetValue(node.ID, out var registered) || ReferenceEquals(node, registered);

    static bool ValidConstructionProfile(RoadProfile profile) =>
        Enum.IsDefined(typeof(UrbanRoadClass), profile.UrbanClass) &&
        Enum.IsDefined(typeof(HighwayTechnicalGrade), profile.HighwayGrade) &&
        Enum.IsDefined(typeof(HighwayNetworkClass), profile.HighwayNetwork) &&
        Enum.IsDefined(typeof(RoadRole), profile.Role) &&
        Enum.IsDefined(typeof(RoadStructure), profile.Structure) &&
        Enum.IsDefined(typeof(BuildingAccessPolicy), profile.BuildingAccess) &&
        double.IsFinite(profile.MinimumAccessSetback) && profile.MinimumAccessSetback >= 0 &&
        ValidConstructionSpeed(profile.DesignSpeedKph) && ValidConstructionSpeed(profile.PostedSpeedKph) &&
        ValidConstructionSpeed(profile.EngineeringMetersPerUnit);

    static bool ValidConstructionSpeed(double? speed) => speed == null || double.IsFinite(speed.Value) && speed.Value > 0;

    // Centreline separation is a necessary bound, not engineering-certified net clearance.
    // Do not invent a clearance rule when the lower road has no reference.
    static bool HasMinimumGeometricClearance(RoadProfile proposed, RoadSegment existing, WorldPosition crossing)
    {
        var prior = existing.Profile;
        var lower = proposed.Level < prior.Level ? proposed : prior;
        if (lower.Engineering?.MinMotorClearanceMeters is not double metres) return true;
        var scale = lower.EngineeringMetersPerUnit;
        if (scale is not double metersPerUnit) return true;

        var start = existing.StartNode.Position;
        var end = existing.EndNode.Position;
        double dx = (double)end.x - start.x, dy = (double)end.y - start.y;
        double run2 = dx * dx + dy * dy;
        if (run2 == 0) return false;
        double t = Math.Clamp(((crossing.x - start.x) * dx + (crossing.y - start.y) * dy) / run2, 0, 1);
        double existingZ = start.z + t * ((double)end.z - start.z);
        double verticalGap = proposed.Level < prior.Level ? existingZ - crossing.z : crossing.z - existingZ;
        return verticalGap * metersPerUnit >= metres;
    }

    bool MoreThanOneMainlineAt(RoadNode node, int level)
    {
        if (!_roadsAt.TryGetValue(node.ID, out var connected)) return false;
        int count = 0;
        foreach (var road in connected)
            if (road.Profile.Level == level && IsConstructionControlledMainline(road.Profile) && ++count >= 2)
                return true;
        return false;
    }

    static bool IsConstructionControlledMainline(RoadProfile profile) => profile.Role != RoadRole.Frontage &&
        (profile.UrbanClass == UrbanRoadClass.Expressway || profile.HighwayGrade == HighwayTechnicalGrade.Motorway);

    RoadNode? SharedConstructionEndpoint(RoadNode a, RoadNode b, RoadSegment road)
    {
        if (_nodesById.TryGetValue(a.ID, out var candidate) && ReferenceEquals(a, candidate) &&
            (ReferenceEquals(a, road.StartNode) || ReferenceEquals(a, road.EndNode)))
            return a;
        if (_nodesById.TryGetValue(b.ID, out candidate) && ReferenceEquals(b, candidate) &&
            (ReferenceEquals(b, road.StartNode) || ReferenceEquals(b, road.EndNode)))
            return b;
        return null;
    }

    static bool IntersectConstructionXY(WorldPosition a, WorldPosition b, WorldPosition c, WorldPosition d,
        out WorldPosition point, out bool overlap)
    {
        // 数值容差仅用于相交计算，不是道路净距或工程尺寸。
        const double epsilon = 1e-9;
        double ax = a.x, ay = a.y;
        double rx = (double)b.x - a.x, ry = (double)b.y - a.y;
        double sx = (double)d.x - c.x, sy = (double)d.y - c.y;
        double qx = (double)c.x - a.x, qy = (double)c.y - a.y;
        double rr = rx * rx + ry * ry;
        double ss = sx * sx + sy * sy;
        double det = rx * sy - ry * sx;
        point = default;
        overlap = false;
        double t;
        if (Math.Abs(det) <= epsilon * Math.Sqrt(rr * ss))
        {
            if (Math.Abs(qx * ry - qy * rx) > epsilon * Math.Sqrt(rr * (qx * qx + qy * qy)))
                return false;
            var t0 = (qx * rx + qy * ry) / rr;
            var t1 = t0 + (sx * rx + sy * ry) / rr;
            var lo = Math.Max(0, Math.Min(t0, t1));
            var hi = Math.Min(1, Math.Max(t0, t1));
            if (hi < lo - epsilon)
                return false;
            overlap = hi > lo + epsilon;
            t = (lo + hi) / 2;
        }
        else
        {
            t = (qx * sy - qy * sx) / det;
            double u = (qx * ry - qy * rx) / det;
            if (t < -epsilon || t > 1 + epsilon || u < -epsilon || u > 1 + epsilon)
                return false;
        }
        t = Math.Min(1, Math.Max(0, t));
        point = new WorldPosition
        {
            x = (float)(ax + t * rx),
            y = (float)(ay + t * ry),
            z = (float)(a.z + t * ((double)b.z - a.z))
        };
        return true;
    }
}
