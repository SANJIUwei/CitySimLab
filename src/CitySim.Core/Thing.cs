using System.Collections.Generic;

public enum LocalPathKind
{
    SameSegment,
    DepartureThenGlobalThenApproach,
    Unreachable
}

// 车、人、以后的载具共用。建筑只给 A/B，这里负责求路、收路线、判定局部怎么走。
public class Thing
{
    public TripCommand? Command { get; private set; }
    public Trip? Route { get; private set; }
    public bool WaitingForRoute { get; private set; }

    public void AcceptCommand(TripCommand command)
    {
        Command = command;
        Route = null;
        WaitingForRoute = false;
    }

    public void RequestRoute(Pathfinder pathfinder)
    {
        if (Command == null)
            return;
        WaitingForRoute = true;
        Route = null;
        pathfinder.Submit(this, Command);
    }

    public void ReceiveRoute(Trip trip)
    {
        Route = trip;
        WaitingForRoute = false;
    }

    // 收到路线后的局部判定：同一段不进全局图；跨段先走出发局部，再走全局，再走最后一段。
    public LocalPathKind JudgeLocalPath()
    {
        if (Route == null || !Route.Reachable)
            return LocalPathKind.Unreachable;
        if (Route.SameSegment)
            return LocalPathKind.SameSegment;
        return LocalPathKind.DepartureThenGlobalThenApproach;
    }

    public List<LocalStep> DepartureSteps()
    {
        if (Route == null)
            return new List<LocalStep>();
        return Route.Departure.Steps();
    }

    public List<LocalStep> ApproachSteps()
    {
        if (Route == null)
            return new List<LocalStep>();
        return Route.Approach.Steps();
    }
}
