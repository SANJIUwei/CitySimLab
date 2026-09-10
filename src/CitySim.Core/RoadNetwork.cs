using System;
using System.Collections.Generic;

// 全局图只挂 Node 和 Segment。载具排队求路以后再做，现在每次调用都是同步算完。
public class RoadNetwork
{
    public List<RoadNode> Nodes { get; } = new List<RoadNode>();
    public List<RoadSegment> Segments { get; } = new List<RoadSegment>();

    readonly Dictionary<long, RoadNode> _nodesById = new Dictionary<long, RoadNode>();
    readonly Dictionary<long, List<(long to, double cost)>> _adj = new Dictionary<long, List<(long to, double cost)>>();

    public void AddNode(RoadNode node)
    {
        if (_nodesById.ContainsKey(node.ID))
            throw new InvalidOperationException("Node ID already exists");
        Nodes.Add(node);
        _nodesById[node.ID] = node;
        if (!_adj.ContainsKey(node.ID))
            _adj[node.ID] = new List<(long, double)>();
    }

    public RoadSegment AddSegment(RoadNode start, RoadNode end, double cost = 1.0)
    {
        if (!_nodesById.ContainsKey(start.ID)) AddNode(start);
        if (!_nodesById.ContainsKey(end.ID)) AddNode(end);
        var segment = new RoadSegment(start, end, cost);
        Segments.Add(segment);
        // 全局图先当双向；单行以后用车道方向裁边，不在这里写死单向。
        _adj[start.ID].Add((end.ID, cost));
        _adj[end.ID].Add((start.ID, cost));
        return segment;
    }

    // 入口 Node 只看缓存的 t：靠近 0 走 Start，靠近 1 走 End。不用直线距离，避免门口歪了就上错端。
    public RoadNode FindEntryNode(Building building)
    {
        if (building.Attachment == null)
            throw new InvalidOperationException("Building is not attached to a segment");
        var attachment = building.Attachment;
        return attachment.T <= 0.5f ? attachment.Segment.StartNode : attachment.Segment.EndNode;
    }

    public GlobalPath FindPath(long startId, long endId)
    {
        var dist = new Dictionary<long, double>();
        var prev = new Dictionary<long, long>();
        var open = new List<long>();
        foreach (var id in _nodesById.Keys)
            dist[id] = double.PositiveInfinity;
        if (!dist.ContainsKey(startId) || !dist.ContainsKey(endId))
            return GlobalPath.None;
        dist[startId] = 0;
        open.Add(startId);

        while (open.Count > 0)
        {
            int bestIndex = 0;
            for (int i = 1; i < open.Count; i++)
            {
                if (dist[open[i]] < dist[open[bestIndex]])
                    bestIndex = i;
            }
            long u = open[bestIndex];
            open.RemoveAt(bestIndex);
            if (u == endId)
                break;
            if (!_adj.TryGetValue(u, out var edges))
                continue;
            for (int i = 0; i < edges.Count; i++)
            {
                var (v, cost) = edges[i];
                double alt = dist[u] + cost;
                if (alt < dist[v])
                {
                    dist[v] = alt;
                    prev[v] = u;
                    if (!open.Contains(v))
                        open.Add(v);
                }
            }
        }

        if (double.IsPositiveInfinity(dist[endId]))
            return GlobalPath.None;

        var ids = new List<long>();
        for (long at = endId; ; at = prev[at])
        {
            ids.Add(at);
            if (at == startId) break;
            if (!prev.ContainsKey(at))
                return GlobalPath.None;
        }
        ids.Reverse();
        return new GlobalPath(ids, dist[endId]);
    }

    // 建筑A到建筑B：先定两端入口 Node，再跑全局带权最短路。局部进出段不在这里算。
    public GlobalPath FindPath(Building from, Building to)
    {
        var a = FindEntryNode(from);
        var b = FindEntryNode(to);
        return FindPath(a.ID, b.ID);
    }

    // 同一段：只走局部 t→t，不进全局图。跨段：出门→t→入口 Node，全局最短路，出口 Node→t→进门。
    public Trip PlanTrip(Building from, Building to)
    {
        if (from.Attachment == null || to.Attachment == null)
            throw new InvalidOperationException("Building is not attached to a segment");

        var fromOnRoad = from.Attachment.Segment.PointAt(from.Attachment.T);
        var toOnRoad = to.Attachment.Segment.PointAt(to.Attachment.T);

        if (ReferenceEquals(from.Attachment.Segment, to.Attachment.Segment))
        {
            double localCost = from.Attachment.Segment.LengthBetween(from.Attachment.T, to.Attachment.T);
            var departure = new LocalDeparture(from.Entrance, from.Attachment, null);
            var approach = new LocalApproach(null, to.Attachment, to.Entrance);
            return new Trip(from, to, sameSegment: true, GlobalPath.LocalOnly(localCost), fromOnRoad, toOnRoad, departure, approach);
        }

        var global = FindPath(from, to);
        RoadNode? entryNode = global.Found ? FindEntryNode(from) : null;
        RoadNode? exitNode = global.Found ? FindEntryNode(to) : null;
        var startLocal = new LocalDeparture(from.Entrance, from.Attachment, entryNode);
        var endLocal = new LocalApproach(exitNode, to.Attachment, to.Entrance);
        return new Trip(from, to, sameSegment: false, global, fromOnRoad, toOnRoad, startLocal, endLocal);
    }
}

public sealed class GlobalPath
{
    public static readonly GlobalPath None = new GlobalPath(new List<long>(), double.PositiveInfinity);

    public static GlobalPath LocalOnly(double cost) => new GlobalPath(new List<long>(), cost);

    public List<long> NodeIds { get; }
    public double TotalCost { get; }
    public bool Found => !double.IsPositiveInfinity(TotalCost);

    public GlobalPath(List<long> nodeIds, double totalCost)
    {
        NodeIds = nodeIds;
        TotalCost = totalCost;
    }
}

// 一次出行：路上的两个接入点 + 可选的全局 Node 序列。载具还没写，这里只给出该走哪一段。
public sealed class Trip
{
    public Building From { get; }
    public Building To { get; }
    public bool SameSegment { get; }
    public GlobalPath Global { get; }
    public World.WorldPosition FromOnRoad { get; }
    public World.WorldPosition ToOnRoad { get; }
    public LocalDeparture Departure { get; }
    public LocalApproach Approach { get; }

    public bool Reachable => SameSegment || Global.Found;

    public Trip(
        Building from,
        Building to,
        bool sameSegment,
        GlobalPath global,
        World.WorldPosition fromOnRoad,
        World.WorldPosition toOnRoad,
        LocalDeparture departure,
        LocalApproach approach)
    {
        From = from;
        To = to;
        SameSegment = sameSegment;
        Global = global;
        FromOnRoad = fromOnRoad;
        ToOnRoad = toOnRoad;
        Departure = departure;
        Approach = approach;
    }
}
