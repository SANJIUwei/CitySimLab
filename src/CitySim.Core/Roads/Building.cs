using System;

namespace CitySim.Core;

public class Building
{
    public WorldPosition Position { get; set; }
    public WorldPosition Entrance { get; set; }
    // 与楼门分开；旧地图未提供车行开口时仍以楼门为锚点。
    public WorldPosition? VehicleAccessPoint { get; set; }
    public int AccessLevel { get; set; }
    public RoadAttachment? Attachment { get; private set; }

    public Building(WorldPosition position, WorldPosition entrance)
    {
        Position = position;
        Entrance = entrance;
    }

    public Building(WorldPosition position, WorldPosition entrance, RoadNetwork network)
        : this(position, entrance) => AttachNearest(network);

    public bool AttachNearest(RoadNetwork network, float maxDistance = float.PositiveInfinity)
    {
        if (float.IsNaN(maxDistance) || maxDistance < 0)
            throw new ArgumentOutOfRangeException(nameof(maxDistance));
        RoadAttachment? best = null;
        var opening = VehicleAccessPoint ?? Entrance;
        double bestDist = double.PositiveInfinity;
        foreach (var segment in network.Segments)
        {
            float t = segment.ClosestT(opening);
            var directions = OpeningDirections(segment, t);
            if (directions == TravelDirection.None)
                continue;
            double d = WorldPosition.Distance(opening, segment.PointAt(t));
            if (d > maxDistance)
                continue;
            if (best == null || segment.Profile.AccessPriority < best.Segment.Profile.AccessPriority ||
                segment.Profile.AccessPriority == best.Segment.Profile.AccessPriority && d < bestDist)
            {
                bestDist = d;
                best = new RoadAttachment(segment, t, directions, directions);
            }
        }
        Attachment = best;
        return best != null;
    }

    public bool HasActiveAttachment(RoadNetwork network) => Attachment != null &&
        network.ContainsSegment(Attachment.Segment) &&
        ((Attachment.EnterFrom | Attachment.ExitToward) & OpeningDirections(Attachment.Segment, Attachment.T))
        != TravelDirection.None;

    public void AttachTo(RoadSegment segment, float t,
        TravelDirection enterFrom = TravelDirection.Both, TravelDirection exitToward = TravelDirection.Both)
    {
        // 正式开口也要经过物理与地区配置校验，手工选择不绕过。
        var requested = new RoadAttachment(segment, t, enterFrom, exitToward);
        var legal = OpeningDirections(segment, requested.T);
        enterFrom &= legal;
        exitToward &= legal;
        if ((enterFrom | exitToward) == TravelDirection.None)
            throw new InvalidOperationException("No legal motor vehicle opening at this location");
        Attachment = new RoadAttachment(segment, t, enterFrom, exitToward);
    }

    TravelDirection OpeningDirections(RoadSegment segment, float t)
    {
        var p = segment.Profile;
        if (!p.AllowsBuildingAccess || p.Level != AccessLevel ||
            segment.Length * Math.Min(t, 1 - t) < p.MinimumAccessSetback)
            return TravelDirection.None;
        TravelDirection available = TravelDirection.None;
        foreach (var lane in segment.RoutingLanes)
            if ((lane.AllowedVehicles & (VehicleKind.Car | VehicleKind.Bus | VehicleKind.Truck)) != 0)
                available |= lane.DirectionFlag;
        if (!p.HasMedian)
            return available;
        // 模拟平面 x/y，z 为高程。中央分隔带存在时只能接开口同侧。
        var a = segment.StartNode.Position;
        var b = segment.EndNode.Position;
        var opening = VehicleAccessPoint ?? Entrance;
        double side = ((double)b.x - a.x) * (opening.y - a.y) - ((double)b.y - a.y) * (opening.x - a.x);
        return available & (side < 0 ? TravelDirection.Forward : side > 0 ? TravelDirection.Reverse : TravelDirection.None);
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
