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

    // 接入时写死 segment+t，之后寻路只读这两个字段。t 由策划/放置逻辑给定，这里不算最近点。
    public void AttachTo(RoadSegment segment, float t)
    {
        Attachment = new RoadAttachment(segment, t);
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
