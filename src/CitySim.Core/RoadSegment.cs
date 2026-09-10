using System.Collections.Generic;

// 图上的一条边：永远两个端点。多车道、多方向是边上的 Lane，不要为此再拆出更多 Node。
public class RoadSegment
{
    // Forward 的起点、Reverse 的终点。不是“车流一定从这里出发”。
    public RoadNode StartNode { get; }

    public RoadNode EndNode { get; }

    public List<RoadLane> Lanes { get; } = new List<RoadLane>();

    // 全局寻路用的边权。限速、禁行、排队以后都加进这个数；现在先当长度。
    public double Cost { get; set; }

    public RoadSegment(RoadNode startNode, RoadNode endNode, double cost = 1.0)
    {
        StartNode = startNode;
        EndNode = endNode;
        Cost = cost;
    }

    // 局部走段时按 t 插值。不是寻路搜索，只是把缓存的比例变成坐标。
    public World.WorldPosition PointAt(float t)
    {
        float u = 1f - t;
        return new World.WorldPosition
        {
            x = StartNode.Position.x * u + EndNode.Position.x * t,
            y = StartNode.Position.y * u + EndNode.Position.y * t,
            z = StartNode.Position.z * u + EndNode.Position.z * t
        };
    }

    // 从段上创建车道，保证 Lanes 和 lane.Segment 同时写上；不要在外面 new RoadLane 再漏加进列表。
    public RoadLane AddLane(LaneDirection direction)
    {
        var lane = new RoadLane(this, direction);
        Lanes.Add(lane);
        return lane;
    }

    // TODO(以后): 长度/弯直。现在用两端坐标就能量直线距离，弯路几何还没定。
    // TODO(以后): 建筑接入。接入挂在段上而不是切段，避免每个建筑变成图节点。
}
