using System;
using System.Collections.Generic;

namespace CitySim.Core;

public sealed class ComputeScheduler
{
    const double AgeBoostPerSecond = 0.4;

    readonly IComputeBackend _backend;
    readonly PriorityBucket[] _buckets = new PriorityBucket[11];
    readonly List<IComputeCenter> _centers = new List<IComputeCenter>();
    readonly object _gate = new object();

    public IComputeBackend Backend => _backend;

    public ComputeScheduler(IComputeBackend? backend = null)
    {
        _backend = backend ?? new CpuParallelBackend();
        for (int i = 0; i < _buckets.Length; i++)
            _buckets[i] = new PriorityBucket();
    }

    public int Pending
    {
        get
        {
            lock (_gate)
            {
                int n = 0;
                for (int i = 0; i < _buckets.Length; i++)
                    n += _buckets[i].Count;
                return n;
            }
        }
    }

    public int PendingIn(string category)
    {
        lock (_gate)
        {
            int n = 0;
            for (int i = 0; i < _buckets.Length; i++)
                n += _buckets[i].CountIn(category);
            return n;
        }
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
            _buckets[BucketOf(job.PriorityScore)].Enqueue(job);
    }

    public int Tick(int maxJobs) => Pump(maxJobs, null);

    public int Tick(string category, int maxJobs) => Pump(maxJobs, category);

    public void RunBatch(IReadOnlyList<IComputeJob> jobs)
    {
        if (jobs.Count == 0)
            return;
        _backend.ExecuteBatch(jobs);
        for (int i = 0; i < jobs.Count; i++)
            jobs[i].Apply();
    }

    int Pump(int maxJobs, string? category)
    {
        if (maxJobs <= 0)
            return 0;
        DispatchCenters(maxJobs, category);
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
        _backend.ExecuteBatch(batch);
        for (int i = 0; i < batch.Count; i++)
            batch[i].Apply();
        return batch.Count;
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

    static int BucketOf(int score) => Math.Min(10, Math.Max(0, score / 10));

    static double Weight(int score)
    {
        return 0.2 + (score / 100.0) * 3.8;
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
}
