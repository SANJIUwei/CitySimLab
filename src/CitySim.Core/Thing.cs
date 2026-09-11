using System.Collections.Generic;

public enum LocalPathKind
{
    SameSegment,
    DepartureThenGlobalThenApproach,
    Unreachable
}

// 车、人、以后的载具共用。出行由 TripStarter 发起，这里负责排队、收路线、沿步骤走。
public class Thing
{
    public TripCommand? Command { get; private set; }
    public Trip? Route { get; private set; }
    public bool WaitingForRoute { get; private set; }

    // 逻辑位置。收到路线后按步骤走，不是每帧搜路。
    public World.WorldPosition Position { get; private set; }
    public int StepIndex { get; private set; }
    public bool Arrived { get; private set; }
    readonly List<LocalStep> _itinerary = new List<LocalStep>();
    public int StepCount => _itinerary.Count;

    public void AcceptCommand(TripCommand command)
    {
        Command = command;
        Route = null;
        WaitingForRoute = false;
        ClearTravel();
    }

    public void RequestRoute(Pathfinder pathfinder, ComputePriority priority = ComputePriority.High)
    {
        if (Command == null)
            return;
        WaitingForRoute = true;
        Route = null;
        ClearTravel();
        pathfinder.Submit(this, Command, priority);
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

    // 把出发局部、全局 Node、到达局部拼成一条只能往前走的步骤表。相邻重复点丢掉。
    public void PrepareTravel(RoadNetwork network)
    {
        _itinerary.Clear();
        StepIndex = 0;
        Arrived = false;
        if (Route == null || !Route.Reachable)
        {
            Arrived = true;
            return;
        }

        AppendUnique(Route.Departure.Steps());
        if (!Route.SameSegment)
        {
            for (int i = 0; i < Route.Global.NodeIds.Count; i++)
            {
                if (!network.TryGetNode(Route.Global.NodeIds[i], out var node))
                    continue;
                AppendUnique(new LocalStep(LocalStepKind.Node, node.Position));
            }
        }
        AppendUnique(Route.Approach.Steps());

        if (_itinerary.Count == 0)
        {
            Arrived = true;
            return;
        }
        Position = _itinerary[0].Position;
        Arrived = _itinerary.Count == 1;
    }

    // 走到下一个逻辑点。已经到终点再调返回 false。
    public bool Advance()
    {
        if (Arrived || _itinerary.Count == 0)
            return false;
        if (StepIndex + 1 >= _itinerary.Count)
        {
            Arrived = true;
            return false;
        }
        StepIndex++;
        Position = _itinerary[StepIndex].Position;
        Arrived = StepIndex == _itinerary.Count - 1;
        return true;
    }

    void AppendUnique(List<LocalStep> steps)
    {
        for (int i = 0; i < steps.Count; i++)
            AppendUnique(steps[i]);
    }

    void AppendUnique(LocalStep step)
    {
        if (_itinerary.Count > 0 && SamePlace(_itinerary[_itinerary.Count - 1].Position, step.Position))
            return;
        _itinerary.Add(step);
    }

    static bool SamePlace(World.WorldPosition a, World.WorldPosition b)
    {
        return a.x == b.x && a.y == b.y && a.z == b.z;
    }

    void ClearTravel()
    {
        _itinerary.Clear();
        StepIndex = 0;
        Arrived = false;
    }
}
