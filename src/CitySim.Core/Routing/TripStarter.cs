using System;

namespace CitySim.Core;

// 路上的一端：挂接点 + 路外进出点。路外可以是门口；已经在路上时和挂接点重合。
public sealed class RouteEnd
{
    public RoadAttachment Attachment { get; }
    public WorldPosition Access { get; }
    public TravelDirection EnterFrom { get; }
    public TravelDirection ExitToward { get; }
    public bool IsBuildingAccess { get; }
    internal Building? SourceBuilding { get; private init; }

    public RouteEnd(RoadAttachment attachment, WorldPosition access,
        TravelDirection enterFrom = TravelDirection.Both, TravelDirection exitToward = TravelDirection.Both,
        bool isBuildingAccess = false)
    {
        Attachment = attachment;
        Access = access;
        EnterFrom = enterFrom;
        ExitToward = exitToward;
        IsBuildingAccess = isBuildingAccess;
    }

    public static RouteEnd FromBuilding(Building building)
    {
        if (building.Attachment == null)
            throw new InvalidOperationException("Building is not attached to a segment");
        return new RouteEnd(building.Attachment, building.VehicleAccessPoint ?? building.Entrance,
            building.Attachment.EnterFrom, building.Attachment.ExitToward, isBuildingAccess: true)
            { SourceBuilding = building };
    }

    public static RouteEnd OnRoad(RoadAttachment attachment)
    {
        var onRoad = attachment.Segment.PointAt(attachment.T);
        return new RouteEnd(attachment, onRoad);
    }
}

public static class TripStarter
{
    public static TripCommand Command(RouteEnd from, RouteEnd to)
    {
        return new TripCommand(from, to);
    }

    public static bool Request(
        Thing thing,
        PathCenter pathCenter,
        RouteEnd from,
        RouteEnd to,
        ComputePriority priority = ComputePriority.High, TravelOptions? travel = null)
    {
        thing.AcceptCommand(new TripCommand(from, to, travel));
        thing.RequestRoute(pathCenter, priority);
        return true;
    }

    public static bool Request(
        Thing thing,
        PathCenter pathCenter,
        Building from,
        Building to,
        ComputePriority priority = ComputePriority.High, TravelOptions? travel = null)
    {
        if (!pathCenter.HasActiveAccess(from) || !pathCenter.HasActiveAccess(to))
            return false;
        return Request(thing, pathCenter, RouteEnd.FromBuilding(from), RouteEnd.FromBuilding(to), priority, travel);
    }
}

public sealed class TripCommand
{
    public RouteEnd Origin { get; }
    public RouteEnd Destination { get; }
    public TravelOptions? Travel { get; }

    public TripCommand(RouteEnd origin, RouteEnd destination, TravelOptions? travel = null)
    {
        Origin = origin;
        Destination = destination;
        Travel = travel;
    }
}
