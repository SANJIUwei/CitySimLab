using System;
using System.Collections.Generic;
using System.Linq;

namespace CitySim.Core;

public class Building
{
    const VehicleKind MotorVehicles = VehicleKind.Car | VehicleKind.Bus | VehicleKind.Truck;

    RoadNetwork? _accessNetwork;
    WorldPosition _entrance;
    WorldPosition? _vehicleAccessPoint;
    int _accessLevel;
    Func<RoadSegment, WorldPosition, WorldPosition, bool>? _accessConnectionFilter;

    public WorldPosition Position { get; set; }

    public WorldPosition Entrance
    {
        get => _entrance;
        set
        {
            if (_entrance.Equals(value)) return;
            _entrance = value;
            if (_vehicleAccessPoint == null) InvalidateAttachment();
        }
    }

    // 与楼门分开；旧地图未提供车行开口时仍以楼门为锚点。
    public WorldPosition? VehicleAccessPoint
    {
        get => _vehicleAccessPoint;
        set
        {
            if (Nullable.Equals(_vehicleAccessPoint, value)) return;
            _vehicleAccessPoint = value;
            InvalidateAttachment();
        }
    }

    public int AccessLevel
    {
        get => _accessLevel;
        set
        {
            if (_accessLevel == value) return;
            _accessLevel = value;
            InvalidateAttachment();
        }
    }

    /// <summary>Optional map-supplied reachability check (road, opening point, road point).
    /// False means a real barrier or missing connection; null means no geometry check was supplied.
    /// The supplied predicate should be read-only, deterministic and safe for concurrent route queries.</summary>
    public Func<RoadSegment, WorldPosition, WorldPosition, bool>? AccessConnectionFilter
    {
        get => _accessConnectionFilter;
        set
        {
            if (_accessConnectionFilter == value) return;
            _accessConnectionFilter = value;
            InvalidateAttachment();
        }
    }

    public RoadAttachment? Attachment { get; private set; }

    public Building(WorldPosition position, WorldPosition entrance)
    {
        Position = position;
        _entrance = entrance;
    }

    public Building(WorldPosition position, WorldPosition entrance, RoadNetwork network)
        : this(position, entrance) => AttachNearest(network);

    /// <summary>Ranks legal candidates by regional access priority and then distance. Rejections follow.</summary>
    public IReadOnlyList<RoadAccessCandidate> PreviewAccess(RoadNetwork network,
        float maxDistance = float.PositiveInfinity, TravelOptions? travel = null)
    {
        ValidatePreview(maxDistance, travel);
        var opening = VehicleAccessPoint ?? Entrance;
        var candidates = network.Segments.Select(road =>
            EvaluateAccess(road, road.ClosestT(opening), opening, maxDistance, travel))
            .OrderBy(c => !c.IsEligible)
            .ThenBy(c => c.IsEligible ? c.Segment.Profile.AccessPriority : 0)
            .ThenBy(c => c.Distance)
            .ToArray();
        return Array.AsReadOnly(candidates);
    }

    public RoadAccessCandidate PreviewAccessAt(RoadNetwork network, RoadSegment segment, float t,
        float maxDistance = float.PositiveInfinity, TravelOptions? travel = null)
    {
        ValidatePreview(maxDistance, travel);
        if (!network.ContainsSegment(segment))
            return new(segment, t, default, double.PositiveInfinity, TravelDirection.None, RoadAccessFailure.RoadNotInNetwork);
        return EvaluateAccess(segment, t, VehicleAccessPoint ?? Entrance, maxDistance, travel);
    }

    public bool AttachNearest(RoadNetwork network, float maxDistance = float.PositiveInfinity)
    {
        // Initial world load needs only the best candidate; avoid sorting the full list.
        ValidatePreview(maxDistance, null);
        var opening = VehicleAccessPoint ?? Entrance;
        RoadAccessCandidate? best = null;
        foreach (var road in network.Segments)
        {
            var candidate = EvaluateAccess(road, road.ClosestT(opening), opening, maxDistance, null);
            if (!candidate.IsEligible) continue;
            if (best == null || road.Profile.AccessPriority < best.Segment.Profile.AccessPriority ||
                road.Profile.AccessPriority == best.Segment.Profile.AccessPriority && candidate.Distance < best.Distance)
                best = candidate;
        }

        SetAttachment(best == null ? null : new RoadAttachment(best.Segment, best.T,
            best.AllowedDirections, best.AllowedDirections), network);
        return best != null;
    }

    public bool TryAttachTo(RoadNetwork network, RoadSegment segment, float t, out RoadAccessFailure failure,
        TravelDirection enterFrom = TravelDirection.Both, TravelDirection exitToward = TravelDirection.Both,
        float maxDistance = float.PositiveInfinity, TravelOptions? travel = null)
    {
        var candidate = PreviewAccessAt(network, segment, t, maxDistance, travel);
        failure = candidate.Failure;
        if (!candidate.IsEligible) return false;
        var legal = candidate.AllowedDirections;
        if (!MatchesDirection(enterFrom, legal) || !MatchesDirection(exitToward, legal) ||
            ((enterFrom | exitToward) & legal) == TravelDirection.None)
        {
            failure = RoadAccessFailure.InvalidDirection;
            return false;
        }
        SetAttachment(new RoadAttachment(segment, t, enterFrom & legal, exitToward & legal), network);
        return true;
    }

    public bool DetachAccess(RoadNetwork network)
    {
        if (Attachment == null) return false;
        // Legacy loading AttachTo had no network argument; accept the owning network or known active network.
        if (_accessNetwork != network && !network.ContainsSegment(Attachment.Segment)) return false;
        SetAttachment(null, network);
        return true;
    }

    public bool HasActiveAttachment(RoadNetwork network) => Attachment is { } attachment &&
        network.ContainsSegment(attachment.Segment) &&
        ((attachment.EnterFrom | attachment.ExitToward) & EvaluateAccess(attachment.Segment,
            attachment.T, VehicleAccessPoint ?? Entrance, float.PositiveInfinity, null).AllowedDirections)
        != TravelDirection.None;

    /// <summary>Low-level load/migration helper. Use TryAttachTo for registered player edits.</summary>
    public void AttachTo(RoadSegment segment, float t,
        TravelDirection enterFrom = TravelDirection.Both, TravelDirection exitToward = TravelDirection.Both)
    {
        var requested = new RoadAttachment(segment, t, enterFrom, exitToward);
        var candidate = EvaluateAccess(segment, requested.T, VehicleAccessPoint ?? Entrance,
            float.PositiveInfinity, null);
        enterFrom &= candidate.AllowedDirections;
        exitToward &= candidate.AllowedDirections;
        if ((enterFrom | exitToward) == TravelDirection.None)
            throw new InvalidOperationException("No legal motor vehicle opening at this location");
        SetAttachment(new RoadAttachment(segment, t, enterFrom, exitToward), _accessNetwork);
    }

    // Topology editor prevalidates the exact replacement before mutating the network.
    internal void ApplyRemappedAttachment(RoadAttachment attachment) => SetAttachment(attachment, _accessNetwork);

    void InvalidateAttachment()
    {
        if (Attachment != null) SetAttachment(null, _accessNetwork);
    }

    void SetAttachment(RoadAttachment? next, RoadNetwork? network)
    {
        var old = Attachment;
        var oldNetwork = _accessNetwork;
        if (ReferenceEquals(oldNetwork, network) &&
            (ReferenceEquals(old, next) ||
            old != null && next != null && ReferenceEquals(old.Segment, next.Segment) && old.T == next.T &&
            old.EnterFrom == next.EnterFrom && old.ExitToward == next.ExitToward))
            return;
        Attachment = next;
        _accessNetwork = network;

        // Initial placement cannot invalidate a previously planned route for the building.
        if (old == null) return;
        oldNetwork?.NotifyBuildingAccessChanged();
        if (network != null && !ReferenceEquals(network, oldNetwork)) network.NotifyBuildingAccessChanged();
    }

    static void ValidatePreview(float maxDistance, TravelOptions? travel)
    {
        if (float.IsNaN(maxDistance) || maxDistance < 0)
            throw new ArgumentOutOfRangeException(nameof(maxDistance));
        travel?.Validate();
    }

    static bool MatchesDirection(TravelDirection requested, TravelDirection legal) =>
        requested is TravelDirection.None or TravelDirection.Both ||
        (requested is TravelDirection.Forward or TravelDirection.Reverse && (requested & legal) != TravelDirection.None);

    static bool IsFinite(WorldPosition point) => float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);

    RoadAccessCandidate EvaluateAccess(RoadSegment segment, float t, WorldPosition opening,
        float maxDistance, TravelOptions? travel)
    {
        if (!float.IsFinite(t) || t < 0 || t > 1 || !IsFinite(opening))
            return new(segment, t, default, double.PositiveInfinity, TravelDirection.None, RoadAccessFailure.InvalidPosition);
        var point = segment.PointAt(t);
        if (!IsFinite(point))
            return new(segment, t, default, double.PositiveInfinity, TravelDirection.None, RoadAccessFailure.InvalidPosition);
        double distance = WorldPosition.Distance(opening, point);

        RoadAccessCandidate Deny(RoadAccessFailure reason) => new(segment, t, point, distance, TravelDirection.None, reason);

        var profile = segment.Profile;
        if (!profile.AllowsBuildingAccess) return Deny(RoadAccessFailure.RoadProhibitsAccess);
        if (profile.Level != AccessLevel) return Deny(RoadAccessFailure.WrongLevel);
        if (segment.Length * Math.Min(t, 1 - t) < profile.MinimumAccessSetback)
            return Deny(RoadAccessFailure.IntersectionSetback);
        if (distance > maxDistance) return Deny(RoadAccessFailure.TooFar);
        if (AccessConnectionFilter != null && !AccessConnectionFilter(segment, opening, point))
            return Deny(RoadAccessFailure.BlockedConnection);

        TravelDirection available = TravelDirection.None;
        foreach (var lane in segment.RoutingLanes)
            if ((lane.AllowedVehicles & profile.AllowedTravelModes & MotorVehicles) != 0 &&
                (travel == null || (travel.Value.Vehicle & MotorVehicles) != 0 && lane.CanTravel(travel.Value)))
                available |= lane.DirectionFlag;
        if (available == TravelDirection.None) return Deny(RoadAccessFailure.NoMotorVehicleLane);
        if (profile.HasMedian)
        {
            // 模拟平面 x/y，z 为高程。中央分隔带存在时只能接开口同侧。
            var a = segment.StartNode.Position;
            var b = segment.EndNode.Position;
            double side = ((double)b.x - a.x) * (opening.y - a.y) - ((double)b.y - a.y) * (opening.x - a.x);
            available &= side < 0 ? TravelDirection.Forward : side > 0 ? TravelDirection.Reverse : TravelDirection.None;
            if (available == TravelDirection.None) return Deny(RoadAccessFailure.MedianOrDirection);
        }
        return new(segment, t, point, distance, available, RoadAccessFailure.None);
    }
}

public class RoadAttachment
{
    public RoadSegment Segment { get; }
    public float T { get; }
    public TravelDirection EnterFrom { get; }
    public TravelDirection ExitToward { get; }

    public RoadAttachment(RoadSegment segment, float t,
        TravelDirection enterFrom = TravelDirection.Both, TravelDirection exitToward = TravelDirection.Both)
    {
        if (!float.IsFinite(t) || t < 0 || t > 1) throw new ArgumentOutOfRangeException(nameof(t));
        if (((enterFrom | exitToward) & ~TravelDirection.Both) != 0)
            throw new ArgumentOutOfRangeException(nameof(enterFrom));
        Segment = segment;
        T = t;
        EnterFrom = enterFrom;
        ExitToward = exitToward;
    }
}
