using System.Collections.Generic;
using System.Threading;

namespace CitySim.Tests;

public sealed class ComputeSchedulerTests
{
    [Fact]
    public void Tick_SharesBudgetAcrossCategories()
    {
        var scheduler = new ComputeScheduler(new CpuParallelBackend(degree: 1));
        var pathDone = new List<int>();
        var econDone = new List<int>();
        scheduler.Submit(new FlagJob("path", pathDone, 1));
        scheduler.Submit(new FlagJob("path", pathDone, 2));
        scheduler.Submit(new FlagJob("economy", econDone, 1));
        scheduler.Submit(new FlagJob("economy", econDone, 2));

        int n = scheduler.Tick(2);

        Assert.Equal(2, n);
        Assert.Single(pathDone);
        Assert.Single(econDone);
        Assert.Equal(2, scheduler.Pending);
    }

    [Fact]
    public void DefaultBackend_IsCpuParallel()
    {
        var scheduler = new ComputeScheduler();
        Assert.Equal("cpu-parallel", scheduler.Backend.Name);
        Assert.True(scheduler.Backend.Available);
    }

    [Fact]
    public void GpuDeferredBackend_StaysOptionalAndUnavailable()
    {
        var gpu = new GpuDeferredBackend();
        Assert.False(gpu.Available);
        Assert.Throws<NotSupportedException>(() => gpu.ExecuteBatch(Array.Empty<IComputeJob>()));
    }

    [Fact]
    public void Apply_RunsOnCallerAfterExecute()
    {
        int executeOn = 0;
        int applyOn = 0;
        var scheduler = new ComputeScheduler(new CpuParallelBackend(degree: 1));
        scheduler.Submit(new ThreadProbeJob(
            () => executeOn = Thread.CurrentThread.ManagedThreadId,
            () => applyOn = Thread.CurrentThread.ManagedThreadId));

        int caller = Thread.CurrentThread.ManagedThreadId;
        scheduler.Tick(1);

        Assert.Equal(caller, applyOn);
        Assert.NotEqual(0, executeOn);
    }

    [Fact]
    public void Tick_HighGetsMoreThanLow_ButLowStillRuns()
    {
        var scheduler = new ComputeScheduler(new CpuParallelBackend(degree: 1));
        var high = new List<int>();
        var low = new List<int>();
        for (int i = 0; i < 20; i++)
        {
            scheduler.Submit(new FlagJob("sim", high, i, ComputePriority.High));
            scheduler.Submit(new FlagJob("sim", low, i, ComputePriority.Low));
        }

        scheduler.Tick(10);

        Assert.True(high.Count > low.Count);
        Assert.True(low.Count > 0);
        Assert.Equal(10, high.Count + low.Count);
    }

    [Fact]
    public void Tick_AsksManagedPathCenterToDispatchInbox()
    {
        var net = new RoadNetwork();
        var a = new RoadNode(new World.WorldPosition { x = 0, y = 0, z = 0 }, 1);
        var b = new RoadNode(new World.WorldPosition { x = 10, y = 0, z = 0 }, 2);
        net.AddSegment(a, b, 1);
        var scheduler = new ComputeScheduler();
        var center = new PathCenter(net, scheduler);
        GlobalPath? path = null;
        center.Enqueue(1, 2, p => path = p);

        Assert.Equal(1, center.Inbox);
        Assert.Equal(1, scheduler.Tick(ComputeCategories.Path, 1));
        Assert.Equal(0, center.Inbox);
        Assert.True(path!.Found);
        Assert.Equal(new long[] { 1, 2 }, path.NodeIds);
    }

    [Fact]
    public void DedicatedCore_StillAppliesOnCaller()
    {
        using var scheduler = new ComputeScheduler(dedicatedCore: true);
        var done = new List<int>();
        scheduler.Submit(new FlagJob("sim", done, 7));

        int n = scheduler.Tick(1);

        Assert.Equal(1, n);
        Assert.Equal(7, Assert.Single(done));
        Assert.True(scheduler.DedicatedCore);
    }

    [Fact]
    public void Tick_LowIsNotStarvedByEndlessHigh()
    {
        var scheduler = new ComputeScheduler(new CpuParallelBackend(degree: 1));
        var high = new List<int>();
        var low = new List<int>();
        for (int i = 0; i < 50; i++)
            scheduler.Submit(new FlagJob("sim", high, i, ComputePriority.High));
        for (int i = 0; i < 4; i++)
            scheduler.Submit(new FlagJob("sim", low, i, ComputePriority.Low));

        for (int t = 0; t < 8; t++)
            scheduler.Tick(5);

        Assert.Equal(4, low.Count);
    }

    sealed class FlagJob : ComputeJob
    {
        readonly List<int> _done;
        readonly int _id;

        public FlagJob(string category, List<int> done, int id, ComputePriority priority = ComputePriority.Normal)
            : base(category, priority)
        {
            _done = done;
            _id = id;
        }

        public override void Execute() { }

        public override void Apply() => _done.Add(_id);
    }

    sealed class ThreadProbeJob : ComputeJob
    {
        readonly Action _execute;
        readonly Action _apply;

        public ThreadProbeJob(Action execute, Action apply) : base("probe")
        {
            _execute = execute;
            _apply = apply;
        }

        public override void Execute() => _execute();
        public override void Apply() => _apply();
    }
}
