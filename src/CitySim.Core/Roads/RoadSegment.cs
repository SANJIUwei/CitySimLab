using System;
using System.Collections.Generic;

namespace CitySim.Core;

// 图上的一条边：永远两个端点。多车道、多方向是边上的 Lane，不要为此再拆出更多 Node。
public class RoadSegment
{
    // Forward 的起点、Reverse 的终点。不是“车流一定从这里出发”。
    public RoadNode StartNode { get; }

    public RoadNode EndNode { get; }

    public List<RoadLane> Lanes { get; } = new List<RoadLane>();

    // 沿路几何长度。局部 t→t、进出段用这个，不要跟寻路权重混用。
    public double Length { get; set; }

    // 全局寻路用的边权。限速、禁行、拥堵以后加进这个数，不改变 Length。
    public double Cost { get; set; }

    public RoadSegment(RoadNode startNode, RoadNode endNode, double cost = 1.0)
    {
        StartNode = startNode;
        EndNode = endNode;
        Cost = cost;
        Length = WorldPosition.Distance(startNode.Position, endNode.Position);
        if (Length == 0)
            Length = cost;
    }

    // 局部走段时按 t 插值。不是寻路搜索，只是把缓存的比例变成坐标。
    public WorldPosition PointAt(float t)
    {
        float u = 1f - t;
        return new WorldPosition
        {
            x = StartNode.Position.x * u + EndNode.Position.x * t,
            y = StartNode.Position.y * u + EndNode.Position.y * t,
            z = StartNode.Position.z * u + EndNode.Position.z * t
        };
    }

    // 同一段上两个接入点之间的沿路距离。用 Length，不用 Cost。
    public double LengthBetween(float tA, float tB)
    {
        return Math.Abs(tA - tB) * Length;
    }

    // 点到这段直线的最近点比例，夹在 [0,1]。零长度段固定 0。不切段。
    public float ClosestT(WorldPosition point)
    {
        var a = StartNode.Position;
        var b = EndNode.Position;
        double abx = b.x - a.x;
        double aby = b.y - a.y;
        double abz = b.z - a.z;
        double ab2 = abx * abx + aby * aby + abz * abz;
        if (ab2 == 0)
            return 0f;
        double t = ((point.x - a.x) * abx + (point.y - a.y) * aby + (point.z - a.z) * abz) / ab2;
        if (t < 0) return 0f;
        if (t > 1) return 1f;
        return (float)t;
    }

    public double DistanceTo(WorldPosition point)
    {
        return WorldPosition.Distance(point, PointAt(ClosestT(point)));
    }

    // 从段上创建车道，保证 Lanes 和 lane.Segment 同时写上；不要在外面 new RoadLane 再漏加进列表。
    public RoadLane AddLane(LaneDirection direction)
    {
        var lane = new RoadLane(this, direction);
        Lanes.Add(lane);
        return lane;
    }

    // TODO(以后): 长度/弯直。现在用两端坐标就能量直线距离，弯路几何还没定。
}
