// 建筑本体在世界坐标里；车上最后一段不现场搜路，只读下面缓存的段和 t。
public class Building
{
    public World.WorldPosition Position { get; set; }

    // 进出点，和 Position 分开：人进的是门，不是房子中心。
    public World.WorldPosition Entrance { get; set; }

    // 未接入前为 null；热路径上先看有没有缓存，没有就根本不该走最后一百米。
    public RoadAttachment? Attachment { get; private set; }

    public Building(World.WorldPosition position, World.WorldPosition entrance)
    {
        Position = position;
        Entrance = entrance;
    }

    // 生成时调用：对入口做最近点投影，结果存进 Attachment，之后寻路只读缓存。
    public Building(World.WorldPosition position, World.WorldPosition entrance, RoadNetwork network)
        : this(position, entrance)
    {
        AttachNearest(network);
    }

    public bool AttachNearest(RoadNetwork network)
    {
        RoadSegment? best = null;
        float bestT = 0f;
        double bestDist = double.PositiveInfinity;
        for (int i = 0; i < network.Segments.Count; i++)
        {
            var segment = network.Segments[i];
            float t = segment.ClosestT(Entrance);
            double d = segment.DistanceTo(Entrance);
            if (d < bestDist)
            {
                bestDist = d;
                best = segment;
                bestT = t;
            }
        }

        if (best == null)
            return false;

        Attachment = new RoadAttachment(best, bestT);
        return true;
    }

    // 测试或手工覆盖用。正式生成走 AttachNearest。
    public void AttachTo(RoadSegment segment, float t)
    {
        Attachment = new RoadAttachment(segment, t);
    }

    // 建筑只发 A/B，然后不管。事物拿着这条指令去排队求路。
    public TripCommand IssueTo(Building destination)
    {
        return new TripCommand(this, destination);
    }
}

// 出行指令：起点建筑是 A，终点建筑是 B。不是路线，也不持有寻路结果。
public sealed class TripCommand
{
    public Building Origin { get; }
    public Building Destination { get; }

    public TripCommand(Building origin, Building destination)
    {
        Origin = origin;
        Destination = destination;
    }
}

// 建筑在这段路上的位置：t=0 是 StartNode，t=1 是 EndNode。最后一百米按这个比例走，不再问图。
public class RoadAttachment
{
    public RoadSegment Segment { get; }

    public float T { get; }

    public RoadAttachment(RoadSegment segment, float t)
    {
        Segment = segment;
        T = t;
    }
}
