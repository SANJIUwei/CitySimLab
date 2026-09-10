// 相对路段两端的行驶方向，不是路口左转/右转，也不是东南西北。
public enum LaneDirection
{
    Forward,
    Reverse
}

// 同一段路上的一条单向通道。不做成 Node，否则车道数会把寻路图撑大。
public class RoadLane
{
    public RoadSegment Segment { get; }

    public LaneDirection Direction { get; }

    public RoadLane(RoadSegment segment, LaneDirection direction)
    {
        Segment = segment;
        Direction = direction;
    }

    // TODO(以后): 转向和标志发生在路口（Node），看本车道接到下一段的哪条车道，不在这里写转向角。
}
