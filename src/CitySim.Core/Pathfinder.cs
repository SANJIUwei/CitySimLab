using System.Collections.Generic;

public static class ComputeCategories
{
    public const string Path = "path";
}

// 寻路只向调度器交 Category=path 的作业，自己不抢核。
public class Pathfinder
{
    readonly RoadNetwork _network;
    readonly ComputeScheduler _scheduler;

    public int PendingCount => _scheduler.PendingIn(ComputeCategories.Path);
    public ComputeScheduler Scheduler => _scheduler;

    public Pathfinder(RoadNetwork network, ComputeScheduler? scheduler = null)
    {
        _network = network;
        _scheduler = scheduler ?? new ComputeScheduler(dedicatedCore: false);
    }

    public void Submit(Thing thing, TripCommand command, ComputePriority priority = ComputePriority.High)
    {
        _scheduler.Submit(new PathJob(_network, thing, command, priority));
    }

    public int Process(int budget)
    {
        return _scheduler.Tick(ComputeCategories.Path, budget);
    }

    public GlobalPath Request(long startNodeId, long endNodeId)
    {
        return _network.FindPath(startNodeId, endNodeId);
    }

    sealed class PathJob : ComputeJob
    {
        readonly RoadNetwork _network;
        readonly Thing _thing;
        readonly TripCommand _command;
        Trip? _trip;

        public PathJob(RoadNetwork network, Thing thing, TripCommand command, ComputePriority priority)
            : base(ComputeCategories.Path, ComputeLane.Cpu, priority)
        {
            _network = network;
            _thing = thing;
            _command = command;
        }

        public override void Execute()
        {
            _trip = _network.PlanTrip(_command.Origin, _command.Destination);
        }

        public override void Apply()
        {
            if (_trip == null)
                return;
            _thing.ReceiveRoute(_trip);
            _thing.PrepareTravel(_network);
        }
    }
}
