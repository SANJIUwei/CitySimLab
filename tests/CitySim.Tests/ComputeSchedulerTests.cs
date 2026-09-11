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
    public void GpuJobs_WaitInsteadOfBlockingCpuLane()
    {
        var scheduler = new ComputeScheduler();
        var cpuDone = new List<int>();
        scheduler.Submit(new FlagJob("sim", cpuDone, 1));
        scheduler.Submit(new GpuNopJob());

        int n = scheduler.Tick(8);

        Assert.Equal(1, n);
        Assert.Single(cpuDone);
        Assert.Equal(1, scheduler.GpuWaiting);
        Assert.False(scheduler.GpuBackend.Available);
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
            : base(category, ComputeLane.Cpu, priority)
        {
            _done = done;
            _id = id;
        }

        public override void Execute() { }

        public override void Apply() => _done.Add(_id);
    }

    sealed class GpuNopJob : ComputeJob
    {
        public GpuNopJob() : base("gpu-wait", ComputeLane.Gpu) { }
        public override void Execute() { }
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
