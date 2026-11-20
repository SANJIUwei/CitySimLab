using System;
using System.Collections.Generic;
using System.Threading;

namespace CitySim.Core;

public partial class RoadNetwork
{
    public const int MaxLandmarks = 4;
    readonly object _landmarkGate = new object();
    LandmarkIndex? _landmarkIndex;

    // 可在地图载入或批量铺路后主动调用，避免第一笔 ALT 请求同步建表。
    // 与既有寻路约定相同：修改路网应在计算批次之外进行。
    public int PrepareLandmarks() => GetLandmarks().Count;

    LandmarkIndex GetLandmarks()
    {
        var index = Volatile.Read(ref _landmarkIndex);
        if (index != null)
            return index;
        lock (_landmarkGate)
        {
            index = _landmarkIndex;
            if (index != null)
                return index;
            index = BuildLandmarks();
            Volatile.Write(ref _landmarkIndex, index);
            return index;
        }
    }

    LandmarkIndex BuildLandmarks()
    {
        var ids = new List<long>(_nodesById.Count);
        // 孤立节点不会经过全局边，不消耗有限的地标名额。
        foreach (var id in _nodesById.Keys)
        {
            if (_adj[id].Count > 0)
                ids.Add(id);
        }
        ids.Sort();
        var slots = new Dictionary<long, int>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
            slots.Add(ids[i], i);

        var landmarks = PickLandmarks(ids);
        var distances = new double[landmarks.Count][];
        var heap = new PathScratch();
        for (int i = 0; i < landmarks.Count; i++)
        {
            var row = new double[ids.Count];
            Array.Fill(row, double.PositiveInfinity);
            var start = landmarks[i];
            row[slots[start]] = 0;
            HeapPush(heap, 0, start, 0);
            while (heap.Heap.Count > 0)
            {
                var (du, u, _) = HeapPop(heap);
                if (du != row[slots[u]])
                    continue;
                foreach (var (v, cost) in _adj[u])
                {
                    int slot = slots[v];
                    double alt = du + cost;
                    if (alt >= row[slot])
                        continue;
                    row[slot] = alt;
                    HeapPush(heap, alt, v, alt);
                }
            }
            distances[i] = row;
        }
        return new LandmarkIndex(slots, distances);
    }

    List<long> PickLandmarks(List<long> ids)
    {
        var picked = new List<long>(MaxLandmarks);
        if (ids.Count == 0)
            return picked;

        var lo = new[] { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity };
        var hi = new[] { double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity };
        foreach (var id in ids)
        {
            var p = _nodesById[id].Position;
            for (int axis = 0; axis < 3; axis++)
            {
                double value = Coordinate(p, axis);
                lo[axis] = Math.Min(lo[axis], value);
                hi[axis] = Math.Max(hi[axis], value);
            }
        }

        // 只用几何坐标选择两条跨度最大的轴，不用坐标估计通行代价。
        var axes = new[] { 0, 1, 2 };
        Array.Sort(axes, (a, b) =>
        {
            int order = (hi[b] - lo[b]).CompareTo(hi[a] - lo[a]);
            return order == 0 ? a.CompareTo(b) : order;
        });
        int x = axes[0], y = axes[1];
        for (int corner = 0; corner < MaxLandmarks && picked.Count < ids.Count; corner++)
        {
            double targetX = (corner & 1) == 0 ? lo[x] : hi[x];
            double targetY = (corner & 2) == 0 ? lo[y] : hi[y];
            double best = double.PositiveInfinity;
            long? bestId = null;
            foreach (var id in ids)
            {
                if (picked.Contains(id))
                    continue;
                var p = _nodesById[id].Position;
                double dx = Coordinate(p, x) - targetX;
                double dy = Coordinate(p, y) - targetY;
                double score = dx * dx + dy * dy;
                if (bestId != null && score >= best)
                    continue;
                best = score;
                bestId = id;
            }
            if (bestId.HasValue)
                picked.Add(bestId.Value);
        }
        return picked;
    }

    static double Coordinate(WorldPosition p, int axis)
    {
        double value = axis == 0 ? p.x : axis == 1 ? p.y : p.z;
        return double.IsNaN(value) || double.IsInfinity(value) ? 0 : value;
    }

    sealed class LandmarkIndex
    {
        readonly Dictionary<long, int> _slots;
        readonly double[][] _distances;
        readonly double _roundoffScale;
        public int Count => _distances.Length;

        public LandmarkIndex(Dictionary<long, int> slots, double[][] distances)
        {
            _slots = slots;
            _distances = distances;
            // 长路径的累加及两距离相减会引入舍入误差，略微下调仍保持保守。
            _roundoffScale = 4.440892098500626e-16 * Math.Max(1, slots.Count);
        }

        public int Slot(long id) => _slots.TryGetValue(id, out int slot) ? slot : -1;

        // 适用于当前双向邻接表。单向图需要分别计算去/回方向，不能照搬绝对差值。
        public double Estimate(long node, int goal)
        {
            int from = Slot(node);
            if (from < 0 || goal < 0)
                return 0;
            double best = 0;
            foreach (var row in _distances)
            {
                double a = row[from], b = row[goal];
                if (double.IsInfinity(a) || double.IsInfinity(b))
                    continue;
                double lowerBound = Math.Abs(a - b) - Math.Max(a, b) * _roundoffScale;
                if (lowerBound > best)
                    best = lowerBound;
            }
            return best;
        }
    }
}
