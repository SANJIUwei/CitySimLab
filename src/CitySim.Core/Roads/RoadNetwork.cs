using System;
using System.Collections.Generic;

namespace CitySim.Core;

// 全局图只挂 Node 和 Segment。排队在 PathCenter，计算作业交给 Scheduler。
public partial class RoadNetwork
{
    public List<RoadNode> Nodes { get; } = new List<RoadNode>();
    public List<RoadSegment> Segments { get; } = new List<RoadSegment>();

    readonly Dictionary<long, RoadNode> _nodesById = new Dictionary<long, RoadNode>();
    readonly Dictionary<long, List<(long to, double cost)>> _adj = new Dictionary<long, List<(long to, double cost)>>();

    readonly Dictionary<long, List<RoadSegment>> _roadsAt = new();
    readonly Dictionary<RoadSegment, double> _baseCosts = new();
    readonly Dictionary<(RoadLane Incoming, RoadLane Outgoing), (bool Allowed, double Wait)> _turns = new();
    readonly HashSet<long> _explicitTurns = new();
    bool _usesRoadRules;
    public long TopologyVersion { get; private set; }

    [ThreadStatic] static PathScratch? t_scratch;

    public void AddNode(RoadNode node)
    {
        if (node.ID < 0 || node.ID > 4_000_000)
            throw new InvalidOperationException("Node ID must be a compact non-negative integer");
        if (_nodesById.ContainsKey(node.ID))
            throw new InvalidOperationException("Node ID already exists");
        Nodes.Add(node);
        _roadsAt.Add(node.ID, new());
        _nodesById[node.ID] = node;
        if (!_adj.ContainsKey(node.ID))
            _adj[node.ID] = new List<(long, double)>();
    }

    public bool TryGetNode(long id, out RoadNode node) => _nodesById.TryGetValue(id, out node);

    public RoadSegment AddSegment(RoadNode start, RoadNode end, double cost = 1.0, RoadProfile? profile = null)
    {
        if (double.IsNaN(cost) || double.IsInfinity(cost) || cost < 0)
            throw new ArgumentOutOfRangeException(nameof(cost), "Routing cost must be finite and non-negative");
        if (!_nodesById.ContainsKey(start.ID)) AddNode(start);
        if (!_nodesById.ContainsKey(end.ID)) AddNode(end);
        if (!ReferenceEquals(_nodesById[start.ID], start) || !ReferenceEquals(_nodesById[end.ID], end))
            throw new ArgumentException("Use the registered road node instance");
        var segment = new RoadSegment(start, end, cost, profile);
        _baseCosts.Add(segment, cost);
        _roadsAt[start.ID].Add(segment);
        if (start.ID != end.ID)
            _roadsAt[end.ID].Add(segment);
        segment.LanesChanged += EnableRoadRules;
        if (profile != null)
            EnableRoadRules();
        Segments.Add(segment);
        TopologyVersion++;
        // 双向放松图只为 ALT 提供下界，不能作为已配置道路的合法性依据。
        _adj[start.ID].Add((end.ID, cost));
        _adj[end.ID].Add((start.ID, cost));
        lock (_landmarkGate)
            System.Threading.Volatile.Write(ref _landmarkIndex, null);
        return segment;
    }

    public RoadNode FindEntryNode(RoadAttachment attachment)
    {
        return attachment.T <= 0.5f ? attachment.Segment.StartNode : attachment.Segment.EndNode;
    }

    public RoadNode FindEntryNode(Building building)
    {
        if (building.Attachment == null)
            throw new InvalidOperationException("Building is not attached to a segment");
        return FindEntryNode(building.Attachment);
    }

    public GlobalPath FindPath(long startId, long endId, PathAlgorithm algorithm = PathAlgorithm.Alt,
        bool captureSearch = false, TravelOptions? travel = null)
    {
        var options = travel ?? TravelOptions.Default;
        options.Validate();
        if (!_nodesById.ContainsKey(startId) || !_nodesById.ContainsKey(endId))
            return GlobalPath.Failed(RouteFailure.MissingNode);
        if (_usesRoadRules && startId != endId)
            return FindConfiguredPath(startId, endId, algorithm, captureSearch, options);
        if (startId == endId)
            return new GlobalPath(new List<long> { startId }, 0, searchSteps: captureSearch
                ? Array.AsReadOnly(new[] { new PathSearchStep(startId, 0, 0, Array.Empty<PathRelaxation>()) })
                : null);

        var landmarks = algorithm == PathAlgorithm.Alt && _adj[startId].Count != 0 && _adj[endId].Count != 0
            ? GetLandmarks() : null;
        int goal = landmarks?.Slot(endId) ?? -1;
        var actualAlgorithm = landmarks == null ? PathAlgorithm.Dijkstra : PathAlgorithm.Alt;
        var steps = captureSearch ? new List<PathSearchStep>() : null;

        var scratch = GetScratch();
        scratch.Ensure((int)Math.Max(startId, endId));
        scratch.EnsureCapacity(Nodes.Count);
        scratch.Stamp++;
        if (scratch.Stamp == int.MaxValue)
        {
            Array.Clear(scratch.Seen, 0, scratch.Seen.Length);
            scratch.Stamp = 1;
        }

        int startIndex = (int)startId;
        int endIndex = (int)endId;
        scratch.Seen[startIndex] = scratch.Stamp;
        scratch.Dist[startIndex] = 0;
        scratch.Heap.Clear();
        HeapPush(scratch, 0, startId, landmarks?.Estimate(startId, goal) ?? 0);

        while (scratch.Heap.Count > 0)
        {
            var (du, u, priority) = HeapPop(scratch);
            if (scratch.Seen[(int)u] == scratch.Stamp && du != scratch.Dist[(int)u])
                continue;
            if (u == endId)
            {
                steps?.Add(new PathSearchStep(u, du, priority, Array.Empty<PathRelaxation>()));
                break;
            }

            var edges = _adj[u];
            var improvements = captureSearch ? new List<PathRelaxation>() : null;
            for (int i = 0; i < edges.Count; i++)
            {
                var (v, cost) = edges[i];
                int vi = (int)v;
                scratch.Ensure(vi);
                double alt = du + cost;
                if (double.IsPositiveInfinity(alt))
                    continue;
                if (scratch.Seen[vi] != scratch.Stamp || alt < scratch.Dist[vi])
                {
                    scratch.Seen[vi] = scratch.Stamp;
                    scratch.Dist[vi] = alt;
                    scratch.Prev[vi] = u;
                    HeapPush(scratch, alt, v, alt + (landmarks?.Estimate(v, goal) ?? 0));
                    improvements?.Add(new PathRelaxation(v, alt));
                }
            }
            if (steps != null)
                steps.Add(new PathSearchStep(u, du, priority, improvements!.AsReadOnly()));
        }

        if (scratch.Seen[endIndex] != scratch.Stamp)
            return new GlobalPath(new List<long>(), double.PositiveInfinity, actualAlgorithm, steps?.AsReadOnly());

        var ids = new List<long>();
        for (long at = endId; ; at = scratch.Prev[(int)at])
        {
            ids.Add(at);
            if (at == startId) break;
        }
        ids.Reverse();
        return new GlobalPath(ids, scratch.Dist[endIndex], actualAlgorithm, steps?.AsReadOnly());
    }

    static PathScratch GetScratch()
    {
        var scratch = t_scratch;
        if (scratch == null)
        {
            scratch = new PathScratch();
            t_scratch = scratch;
        }
        return scratch;
    }

    static void HeapPush(PathScratch scratch, double dist, long id, double priority)
    {
        scratch.Heap.Add((dist, id, priority));
        int i = scratch.Heap.Count - 1;
        while (i > 0)
        {
            int p = (i - 1) / 2;
            if (HasPriority(scratch.Heap[p], scratch.Heap[i]))
                break;
            (scratch.Heap[p], scratch.Heap[i]) = (scratch.Heap[i], scratch.Heap[p]);
            i = p;
        }
    }

    // f 相同时优先更接近终点的候选点（更大的 g）；两者都相等时保留既有堆语义。
    static bool HasPriority((double dist, long id, double priority) a, (double dist, long id, double priority) b)
        => a.priority < b.priority || (a.priority == b.priority && a.dist >= b.dist);

    static (double dist, long id, double priority) HeapPop(PathScratch scratch)
    {
        var root = scratch.Heap[0];
        int last = scratch.Heap.Count - 1;
        scratch.Heap[0] = scratch.Heap[last];
        scratch.Heap.RemoveAt(last);
        int i = 0;
        while (true)
        {
            int l = i * 2 + 1;
            int r = l + 1;
            if (l >= scratch.Heap.Count)
                break;
            int s = r < scratch.Heap.Count && !HasPriority(scratch.Heap[l], scratch.Heap[r]) ? r : l;
            if (HasPriority(scratch.Heap[i], scratch.Heap[s]))
                break;
            (scratch.Heap[i], scratch.Heap[s]) = (scratch.Heap[s], scratch.Heap[i]);
            i = s;
        }
        return root;
    }

    sealed class PathScratch
    {
        public int Stamp;
        public int[] Seen = Array.Empty<int>();
        public double[] Dist = Array.Empty<double>();
        public long[] Prev = Array.Empty<long>();
        public List<(double dist, long id, double priority)> Heap = new List<(double, long, double)>();

        public void Ensure(int id)
        {
            int n = id + 1;
            if (Seen.Length >= n)
                return;
            int size = Math.Max(n, Math.Max(64, Seen.Length * 2));
            Array.Resize(ref Seen, size);
            Array.Resize(ref Dist, size);
            Array.Resize(ref Prev, size);
        }

        public void EnsureCapacity(int nodeCount)
        {
            if (nodeCount <= 0)
                return;
            Ensure(nodeCount);
        }
    }

    public GlobalPath FindPath(RouteEnd from, RouteEnd to)
        => PlanTrip(from, to).Global;

    public GlobalPath FindPath(Building from, Building to)
        => TryPlanTrip(from, to, out var trip, out var failure) ? trip!.Global :
            trip?.Global ?? GlobalPath.Failed(failure);

    // 接入点参加合法路径搜索，不能为了距离近自行选择一个路口或逆走同一段。
    public Trip PlanTrip(RouteEnd from, RouteEnd to, PathAlgorithm algorithm = PathAlgorithm.Alt,
        bool captureSearch = false, TravelOptions? travel = null)
    {
        var options = travel ?? TravelOptions.Default;
        options.Validate();
        return PlanConfiguredTrip(from, to, algorithm, captureSearch, options);
    }

    public Trip PlanTrip(Building from, Building to, PathAlgorithm algorithm = PathAlgorithm.Alt,
        bool captureSearch = false, TravelOptions? travel = null)
        => PlanTrip(RouteEnd.FromBuilding(from), RouteEnd.FromBuilding(to), algorithm, captureSearch, travel);

    public bool TryPlanTrip(Building from, Building to, out Trip? trip, out RouteFailure failure,
        PathAlgorithm algorithm = PathAlgorithm.Alt, bool captureSearch = false, TravelOptions? travel = null)
    {
        trip = null;
        if (!from.HasActiveAttachment(this) || !to.HasActiveAttachment(this))
        {
            failure = RouteFailure.NoBuildingAccess;
            return false;
        }
        trip = PlanTrip(from, to, algorithm, captureSearch, travel);
        failure = trip.Failure;
        return trip.Reachable;
    }
}
