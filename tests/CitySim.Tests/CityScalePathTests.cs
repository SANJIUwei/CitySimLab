using System.Diagnostics;
using Xunit.Abstractions;

namespace CitySim.Tests;

public sealed class CityScalePathTests
{
    readonly ITestOutputHelper _output;

    public CityScalePathTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // 百万人口对应的是市民上限，不是路口数。路口按约 18km 城、100m 街区估成 160x160。
    // 2 万次寻路对齐原版路上载具量级（16384），以后再加。
    [Fact]
    public void FindPath_MillionCityGraph_TwentyThousandQueries()
    {
        const int n = 160;
        const int sequential = 2_000;
        const int parallel = 20_000;
        var net = BuildGrid(n);
        int nodeCount = n * n;
        var scheduler = new ComputeScheduler(new CpuParallelBackend());

        var seq = RunSequential(net, nodeCount, sequential, seed: 2026);
        var par = RunParallel(net, scheduler, nodeCount, parallel, seed: 2026);

        _output.WriteLine("skylines vanilla: ~1,048,576 cims; vehicles 16,384 (More Vehicles 65,536)");
        _output.WriteLine($"grid={n}x{n} nodes={net.Nodes.Count} segments={net.Segments.Count} workers={Environment.ProcessorCount}");
        _output.WriteLine($"sequential queries={sequential} found={seq.Found} totalMs={seq.TotalMs:F0} avgMs={seq.AvgMs:F3}");
        _output.WriteLine($"parallel queries={parallel} found={par.Found} totalMs={par.TotalMs:F0} avgMs={par.AvgMs:F3} maxHops={par.MaxHops}");

        Assert.Equal(sequential, seq.Found);
        Assert.Equal(parallel, par.Found);
    }

    static BatchResult RunSequential(RoadNetwork net, int nodeCount, int queries, int seed)
    {
        var rng = new Random(seed);
        int found = 0;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < queries; i++)
        {
            if (net.FindPath(rng.Next(1, nodeCount + 1), rng.Next(1, nodeCount + 1)).Found)
                found++;
        }
        sw.Stop();
        return new BatchResult(found, sw.Elapsed.TotalMilliseconds, 0);
    }

    static BatchResult RunParallel(RoadNetwork net, ComputeScheduler scheduler, int nodeCount, int queries, int seed)
    {
        var rng = new Random(seed);
        var jobs = new FindPathJob[queries];
        for (int i = 0; i < queries; i++)
            jobs[i] = new FindPathJob(net, rng.Next(1, nodeCount + 1), rng.Next(1, nodeCount + 1));

        var sw = Stopwatch.StartNew();
        scheduler.RunBatch(jobs);
        sw.Stop();

        int found = 0;
        int maxHops = 0;
        for (int i = 0; i < jobs.Length; i++)
        {
            if (!jobs[i].Result.Found)
                continue;
            found++;
            if (jobs[i].Result.NodeIds.Count > maxHops)
                maxHops = jobs[i].Result.NodeIds.Count;
        }
        return new BatchResult(found, sw.Elapsed.TotalMilliseconds, maxHops);
    }

    readonly struct BatchResult
    {
        public int Found { get; }
        public double TotalMs { get; }
        public int MaxHops { get; }
        public double AvgMs => Found == 0 ? 0 : TotalMs / Found;
        public BatchResult(int found, double totalMs, int maxHops)
        {
            Found = found;
            TotalMs = totalMs;
            MaxHops = maxHops;
        }
    }

    sealed class FindPathJob : ComputeJob
    {
        readonly RoadNetwork _net;
        readonly long _from;
        readonly long _to;
        public GlobalPath Result { get; private set; } = GlobalPath.None;

        public FindPathJob(RoadNetwork net, long from, long to)
            : base("bench-path")
        {
            _net = net;
            _from = from;
            _to = to;
        }

        public override void Execute() => Result = _net.FindPath(_from, _to);
    }

    static RoadNetwork BuildGrid(int n)
    {
        var net = new RoadNetwork();
        var nodes = new RoadNode[n, n];
        long id = 1;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                nodes[x, y] = new RoadNode(new World.WorldPosition { x = x, y = y, z = 0 }, id++);
                net.AddNode(nodes[x, y]);
            }
        }
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                if (x + 1 < n) net.AddSegment(nodes[x, y], nodes[x + 1, y], 1);
                if (y + 1 < n) net.AddSegment(nodes[x, y], nodes[x, y + 1], 1);
            }
        }
        return net;
    }
}
