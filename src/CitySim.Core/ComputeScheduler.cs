using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

public enum ComputeLane
{
    Cpu,
    Gpu
}

public enum ComputePriority
{
    Low = 0,
    Normal = 1,
    High = 2
}

public interface IComputeJob
{
    string Category { get; }
    ComputeLane Lane { get; }
    ComputePriority Priority { get; }
    int PriorityScore { get; }
    int CostHint { get; }

    void Execute();
    void Apply();
}

public abstract class ComputeJob : IComputeJob
{
    public string Category { get; }
    public ComputeLane Lane { get; }
    public ComputePriority Priority { get; }
    public int PriorityScore { get; }
    public int CostHint { get; }

    protected ComputeJob(
        string category,
        ComputeLane lane = ComputeLane.Cpu,
        ComputePriority priority = ComputePriority.Normal,
        int costHint = 1)
        : this(category, ScoreOf(priority), lane, costHint)
    {
    }

    protected ComputeJob(string category, int priorityScore, ComputeLane lane = ComputeLane.Cpu, int costHint = 1)
    {
        Category = category;
        Lane = lane;
        PriorityScore = Math.Min(100, Math.Max(0, priorityScore));
        CostHint = Math.Max(1, costHint);
        Priority = PriorityScore >= 70 ? ComputePriority.High
            : PriorityScore <= 30 ? ComputePriority.Low
            : ComputePriority.Normal;
    }

    public abstract void Execute();
    public virtual void Apply() { }

    public static int ScoreOf(ComputePriority priority)
    {
        switch (priority)
        {
            case ComputePriority.High:
                return 80;
            case ComputePriority.Low:
                return 20;
            default:
                return 50;
        }
    }
}

// 被调度器管理的业务中心：自己收请求、自己排队，调度器只问它要不要交出计算作业。
public interface IComputeCenter
{
    string Category { get; }
    int Inbox { get; }
    int Dispatch(int maxJobs);
}

public interface IComputeBackend
{
    string Name { get; }
    bool Available { get; }
    void ExecuteBatch(IReadOnlyList<IComputeJob> jobs);
}

public sealed class CpuParallelBackend : IComputeBackend
{
    public string Name => "cpu-parallel";
    public bool Available => true;
    public int Degree { get; }

    public CpuParallelBackend(int degree = 0)
    {
        Degree = degree > 0 ? degree : Math.Max(1, Environment.ProcessorCount);
    }

    public void ExecuteBatch(IReadOnlyList<IComputeJob> jobs)
    {
        if (jobs.Count == 0)
            return;
        if (jobs.Count == 1 || Degree == 1)
        {
            for (int i = 0; i < jobs.Count; i++)
                jobs[i].Execute();
            return;
        }

        var options = new ParallelOptions { MaxDegreeOfParallelism = Degree };
        Parallel.For(0, jobs.Count, options, i => jobs[i].Execute());
    }
}

public sealed class GpuDeferredBackend : IComputeBackend
{
    public string Name => "gpu-deferred";
    public bool Available => false;

    public void ExecuteBatch(IReadOnlyList<IComputeJob> jobs)
    {
        throw new NotSupportedException(
            "GPU 尚未接入。接入后与 CPU 同一合同：排队提交、算完回传、主线程只 Apply。");
    }
}

public sealed class ComputeScheduler : IDisposable
{
    const double AgeBoostPerSecond = 0.4;

    readonly IComputeBackend _cpu;
    readonly IComputeBackend _gpu;
    readonly PriorityBucket[] _buckets = new PriorityBucket[11];
    readonly Queue<IComputeJob> _gpuWaiting = new Queue<IComputeJob>();
    readonly List<IComputeCenter> _centers = new List<IComputeCenter>();
    readonly object _gate = new object();
    readonly ComputeHost? _host;

    public IComputeBackend CpuBackend => _cpu;
    public IComputeBackend GpuBackend => _gpu;
    public bool DedicatedCore { get; }

    public ComputeScheduler(
        IComputeBackend? cpu = null,
        IComputeBackend? gpu = null,
        bool dedicatedCore = false)
    {
        _cpu = cpu ?? new CpuParallelBackend();
        _gpu = gpu ?? new GpuDeferredBackend();
        DedicatedCore = dedicatedCore;
        for (int i = 0; i < _buckets.Length; i++)
            _buckets[i] = new PriorityBucket();
        if (dedicatedCore)
            _host = new ComputeHost(this);
    }

    public int Pending
    {
        get
        {
            lock (_gate)
            {
                int n = _gpuWaiting.Count + (_host?.InFlight ?? 0) + (_host?.DoneCount ?? 0);
                for (int i = 0; i < _buckets.Length; i++)
                    n += _buckets[i].Count;
                return n;
            }
        }
    }

    public int PendingIn(string category)
    {
        int n = 0;
        lock (_gate)
        {
            for (int i = 0; i < _buckets.Length; i++)
                n += _buckets[i].CountIn(category);
        }
        n += _host?.CountCategory(category) ?? 0;
        return n;
    }

    public int GpuWaiting
    {
        get { lock (_gate) return _gpuWaiting.Count; }
    }

    public void Manage(IComputeCenter center)
    {
        lock (_gate)
        {
            if (!_centers.Contains(center))
                _centers.Add(center);
        }
    }

    public void Submit(IComputeJob job)
    {
        lock (_gate)
        {
            if (job.Lane == ComputeLane.Gpu)
                _gpuWaiting.Enqueue(job);
            else
                _buckets[BucketOf(job.PriorityScore)].Enqueue(job);
        }
        _host?.Wake();
    }

    public int Tick(int maxJobs) => Pump(maxJobs, null);

    public int Tick(string category, int maxJobs) => Pump(maxJobs, category);

    public void RunBatch(IReadOnlyList<IComputeJob> jobs)
    {
        if (jobs.Count == 0)
            return;
        _cpu.ExecuteBatch(jobs);
        for (int i = 0; i < jobs.Count; i++)
            jobs[i].Apply();
    }

    public void Dispose()
    {
        _host?.Dispose();
    }

    int Pump(int maxJobs, string? category)
    {
        if (maxJobs <= 0)
            return 0;
        DispatchCenters(maxJobs, category);
        TryFlushGpu();
        if (_host != null)
            return PumpDedicated(maxJobs, category);
        return PumpInline(maxJobs, category);
    }

    void DispatchCenters(int maxJobs, string? category)
    {
        IComputeCenter[] snapshot;
        lock (_gate)
            snapshot = _centers.ToArray();
        int remaining = maxJobs;
        for (int i = 0; i < snapshot.Length && remaining > 0; i++)
        {
            var center = snapshot[i];
            if (category != null && center.Category != category)
                continue;
            remaining -= center.Dispatch(remaining);
        }
    }

    int PumpInline(int maxJobs, string? category)
    {
        var batch = new List<IComputeJob>(maxJobs);
        lock (_gate)
        {
            while (batch.Count < maxJobs)
            {
                var job = PickLocked(category);
                if (job == null)
                    break;
                batch.Add(job);
            }
        }
        if (batch.Count == 0)
            return 0;
        _cpu.ExecuteBatch(batch);
        for (int i = 0; i < batch.Count; i++)
            batch[i].Apply();
        return batch.Count;
    }

    int PumpDedicated(int maxJobs, string? category)
    {
        _host!.Wake();
        int applied = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (applied < maxJobs && sw.ElapsedMilliseconds < 30_000)
        {
            var skipped = new List<IComputeJob>();
            while (applied < maxJobs && _host.TryDequeueDone(out var job))
            {
                if (category != null && job.Category != category)
                {
                    skipped.Add(job);
                    continue;
                }
                job.Apply();
                applied++;
            }
            for (int i = 0; i < skipped.Count; i++)
                _host.RequeueDone(skipped[i]);
            if (applied >= maxJobs)
                break;
            bool more;
            lock (_gate)
                more = HasWaitingLocked(category) || _host.InFlight > 0 || _host.DoneCount > 0;
            if (!more)
                break;
            _host.WaitProgress(1);
        }
        return applied;
    }

    internal IComputeJob? TryTakeWork()
    {
        lock (_gate)
            return PickLocked(null);
    }

    IComputeJob? PickLocked(string? category)
    {
        int best = -1;
        double bestVr = double.MaxValue;
        long now = DateTime.UtcNow.Ticks;
        for (int i = 0; i < _buckets.Length; i++)
        {
            if (!_buckets[i].HasWork(category))
                continue;
            double weight = Weight(i * 10);
            double waitSec = (now - _buckets[i].OldestTicks) / (double)TimeSpan.TicksPerSecond;
            double effectiveVr = _buckets[i].VRuntime / weight - waitSec * AgeBoostPerSecond;
            if (best >= 0 && effectiveVr >= bestVr - 1e-9)
                continue;
            best = i;
            bestVr = effectiveVr;
        }
        if (best < 0)
            return null;
        if (!_buckets[best].TryDequeue(category, out var job) || job == null)
            return null;
        _buckets[best].VRuntime += job.CostHint / Weight(job.PriorityScore);
        return job;
    }

    bool HasWaitingLocked(string? category)
    {
        for (int i = 0; i < _buckets.Length; i++)
        {
            if (_buckets[i].HasWork(category))
                return true;
        }
        return false;
    }

    static int BucketOf(int score) => Math.Min(10, Math.Max(0, score / 10));

    static double Weight(int score)
    {
        return 0.2 + (score / 100.0) * 3.8;
    }

    void TryFlushGpu()
    {
        List<IComputeJob>? gpuBatch = null;
        lock (_gate)
        {
            if (!_gpu.Available || _gpuWaiting.Count == 0)
                return;
            gpuBatch = new List<IComputeJob>(_gpuWaiting.Count);
            while (_gpuWaiting.Count > 0)
                gpuBatch.Add(_gpuWaiting.Dequeue());
        }
        _gpu.ExecuteBatch(gpuBatch!);
        for (int i = 0; i < gpuBatch!.Count; i++)
            gpuBatch[i].Apply();
    }

    sealed class PriorityBucket
    {
        public double VRuntime;
        readonly Dictionary<string, Queue<IComputeJob>> _queues = new Dictionary<string, Queue<IComputeJob>>();
        readonly List<string> _order = new List<string>();
        readonly Dictionary<string, long> _oldest = new Dictionary<string, long>();
        int _rr;

        public int Count
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _order.Count; i++)
                    n += _queues[_order[i]].Count;
                return n;
            }
        }

        public long OldestTicks
        {
            get
            {
                long oldest = DateTime.UtcNow.Ticks;
                foreach (var kv in _oldest)
                {
                    if (kv.Value < oldest)
                        oldest = kv.Value;
                }
                return oldest;
            }
        }

        public int CountIn(string category)
        {
            return _queues.TryGetValue(category, out var q) ? q.Count : 0;
        }

        public bool HasWork(string? category)
        {
            return category == null ? Count > 0 : CountIn(category) > 0;
        }

        public void Enqueue(IComputeJob job)
        {
            if (!_queues.TryGetValue(job.Category, out var q))
            {
                q = new Queue<IComputeJob>();
                _queues[job.Category] = q;
                _order.Add(job.Category);
            }
            if (q.Count == 0)
                _oldest[job.Category] = DateTime.UtcNow.Ticks;
            q.Enqueue(job);
        }

        public bool TryDequeue(string? category, out IComputeJob? job)
        {
            job = null;
            if (category != null)
            {
                if (!_queues.TryGetValue(category, out var q) || q.Count == 0)
                    return false;
                job = q.Dequeue();
                if (q.Count == 0)
                    _oldest.Remove(category);
                return true;
            }
            if (_order.Count == 0)
                return false;
            for (int n = 0; n < _order.Count; n++)
            {
                var key = _order[_rr % _order.Count];
                _rr++;
                var q = _queues[key];
                if (q.Count == 0)
                    continue;
                job = q.Dequeue();
                if (q.Count == 0)
                    _oldest.Remove(key);
                return true;
            }
            return false;
        }
    }

    sealed class ComputeHost : IDisposable
    {
        readonly ComputeScheduler _owner;
        readonly BlockingCollection<IComputeJob> _work = new BlockingCollection<IComputeJob>();
        readonly ConcurrentQueue<IComputeJob> _done = new ConcurrentQueue<IComputeJob>();
        readonly AutoResetEvent _wake = new AutoResetEvent(false);
        readonly AutoResetEvent _progress = new AutoResetEvent(false);
        readonly Thread _schedulerThread;
        readonly Thread[] _workers;
        readonly int _schedulerCore;
        volatile bool _running = true;
        int _idle;
        int _inflight;

        public int InFlight => Volatile.Read(ref _inflight);
        public int DoneCount => _done.Count;

        public ComputeHost(ComputeScheduler owner)
        {
            _owner = owner;
            int cpus = Math.Max(1, Environment.ProcessorCount);
            _schedulerCore = cpus - 1;
            int workerCount = Math.Max(1, cpus - 1);
            _idle = workerCount;
            _workers = new Thread[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                int core = i % Math.Max(1, _schedulerCore);
                _workers[i] = new Thread(() => WorkerLoop(core))
                {
                    IsBackground = true,
                    Name = "citysim-compute-worker-" + i
                };
                _workers[i].Start();
            }
            _schedulerThread = new Thread(SchedulerLoop)
            {
                IsBackground = true,
                Name = "citysim-compute-scheduler",
                Priority = ThreadPriority.AboveNormal
            };
            _schedulerThread.Start();
        }

        public void Wake() => _wake.Set();

        public void WaitProgress(int ms) => _progress.WaitOne(ms);

        public bool TryDequeueDone(out IComputeJob job) => _done.TryDequeue(out job!);

        public void RequeueDone(IComputeJob job) => _done.Enqueue(job);

        public int CountCategory(string category)
        {
            int n = 0;
            foreach (var job in _done)
            {
                if (job.Category == category)
                    n++;
            }
            return n;
        }

        void SchedulerLoop()
        {
            TryPin(_schedulerCore);
            while (_running)
            {
                _wake.WaitOne(8);
                while (Volatile.Read(ref _idle) > 0 && _running)
                {
                    var job = _owner.TryTakeWork();
                    if (job == null)
                        break;
                    Interlocked.Decrement(ref _idle);
                    Interlocked.Increment(ref _inflight);
                    if (!_work.IsAddingCompleted)
                        _work.Add(job);
                }
            }
        }

        void WorkerLoop(int core)
        {
            TryPin(core);
            try
            {
                foreach (var job in _work.GetConsumingEnumerable())
                {
                    job.Execute();
                    _done.Enqueue(job);
                    Interlocked.Decrement(ref _inflight);
                    Interlocked.Increment(ref _idle);
                    _progress.Set();
                    _wake.Set();
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        public void Dispose()
        {
            _running = false;
            _wake.Set();
            _work.CompleteAdding();
            _schedulerThread.Join(500);
            for (int i = 0; i < _workers.Length; i++)
                _workers[i].Join(500);
            _wake.Dispose();
            _progress.Dispose();
            _work.Dispose();
        }

        static void TryPin(int core)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;
            try
            {
                nint mask = (nint)(1L << core);
                SetThreadAffinityMask(GetCurrentThread(), mask);
            }
            catch
            {
            }
        }

        [DllImport("kernel32.dll")]
        static extern nint GetCurrentThread();

        [DllImport("kernel32.dll")]
        static extern nint SetThreadAffinityMask(nint hThread, nint dwThreadMask);
    }
}
