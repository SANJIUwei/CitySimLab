using System;

// 路上的一端：挂接点 + 路外进出点。路外可以是门口；已经在路上时和挂接点重合。
public sealed class RouteEnd
{
    public RoadAttachment Attachment { get; }
    public World.WorldPosition Access { get; }

    public RouteEnd(RoadAttachment attachment, World.WorldPosition access)
    {
        Attachment = attachment;
        Access = access;
    }

    public static RouteEnd FromBuilding(Building building)
    {
        if (building.Attachment == null)
            throw new InvalidOperationException("Building is not attached to a segment");
        return new RouteEnd(building.Attachment, building.Entrance);
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
        Pathfinder pathfinder,
        RouteEnd from,
        RouteEnd to,
        ComputePriority priority = ComputePriority.High)
    {
        thing.AcceptCommand(new TripCommand(from, to));
        thing.RequestRoute(pathfinder, priority);
        return true;
    }

    public static bool Request(
        Thing thing,
        Pathfinder pathfinder,
        Building from,
        Building to,
        ComputePriority priority = ComputePriority.High)
    {
        if (from.Attachment == null || to.Attachment == null)
            return false;
        return Request(thing, pathfinder, RouteEnd.FromBuilding(from), RouteEnd.FromBuilding(to), priority);
    }
}

public sealed class TripCommand
{
    public RouteEnd Origin { get; }
    public RouteEnd Destination { get; }

    public TripCommand(RouteEnd origin, RouteEnd destination)
    {
        Origin = origin;
        Destination = destination;
    }
}
