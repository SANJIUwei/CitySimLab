using System;
using System.Collections.Generic;

public static class ComputeCategories
{
    public const string Path = "path";
}

// 寻路计算只是一种作业，不属于建筑或载具。
// PathCenter 只负责收请求、组织排队；真正算不算、何时算，由 Scheduler 管。
public class PathCenter : IComputeCenter
{
    readonly RoadNetwork _network;
    readonly ComputeScheduler _scheduler;
    readonly Queue<IComputeJob> _inbox = new Queue<IComputeJob>();

    public string Category => ComputeCategories.Path;
    public int Inbox { get { lock (_inbox) return _inbox.Count; } }
    public int PendingCount => Inbox + _scheduler.PendingIn(Category);
    public ComputeScheduler Scheduler => _scheduler;

    public PathCenter(RoadNetwork network, ComputeScheduler? scheduler = null)
    {
        _network = network;
        _scheduler = scheduler ?? new ComputeScheduler();
        _scheduler.Manage(this);
    }

    public void Enqueue(Thing thing, TripCommand command, ComputePriority priority = ComputePriority.High)
    {
        lock (_inbox)
            _inbox.Enqueue(new PathSearchJob(_network, thing, command, priority));
    }

    public void Enqueue(long startNodeId, long endNodeId, Action<GlobalPath> onDone, ComputePriority priority = ComputePriority.Normal)
    {
        lock (_inbox)
            _inbox.Enqueue(new NodeSearchJob(_network, startNodeId, endNodeId, onDone, priority));
    }

    public int Dispatch(int maxJobs)
    {
        if (maxJobs <= 0)
            return 0;
        int n = 0;
        lock (_inbox)
        {
            while (n < maxJobs && _inbox.Count > 0)
            {
                _scheduler.Submit(_inbox.Dequeue());
                n++;
            }
        }
        return n;
    }

    public int Process(int budget)
    {
        return _scheduler.Tick(Category, budget);
    }
}

// 旧名。寻路中心才是排队入口。
public class Pathfinder : PathCenter
{
    public Pathfinder(RoadNetwork network, ComputeScheduler? scheduler = null)
        : base(network, scheduler)
    {
    }

    public void Submit(Thing thing, TripCommand command, ComputePriority priority = ComputePriority.High)
    {
        Enqueue(thing, command, priority);
    }

    public GlobalPath Request(long startNodeId, long endNodeId)
    {
        GlobalPath result = GlobalPath.None;
        Enqueue(startNodeId, endNodeId, path => result = path);
        Process(1);
        return result;
    }
}

sealed class PathSearchJob : ComputeJob
{
    readonly RoadNetwork _network;
    readonly Thing _thing;
    readonly TripCommand _command;
    Trip? _trip;

    public PathSearchJob(RoadNetwork network, Thing thing, TripCommand command, ComputePriority priority)
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

sealed class NodeSearchJob : ComputeJob
{
    readonly RoadNetwork _network;
    readonly long _start;
    readonly long _end;
    readonly Action<GlobalPath> _onDone;
    GlobalPath _result = GlobalPath.None;

    public NodeSearchJob(RoadNetwork network, long start, long end, Action<GlobalPath> onDone, ComputePriority priority)
        : base(ComputeCategories.Path, ComputeLane.Cpu, priority)
    {
        _network = network;
        _start = start;
        _end = end;
        _onDone = onDone;
    }

    public override void Execute()
    {
        _result = _network.FindPath(_start, _end);
    }

    public override void Apply() => _onDone(_result);
}
