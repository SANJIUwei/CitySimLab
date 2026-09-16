using System.Collections.Generic;

namespace CitySim.Core;

// 最后一段：离开全局最后一个 Node 之后不再搜图。
// 固定两步：沿目标路段走到缓存的 t，再从挂接点进门。
public enum LocalStepKind
{
    Entrance,
    OnRoad,
    Node
}

public readonly struct LocalStep
{
    public LocalStepKind Kind { get; }
    public WorldPosition Position { get; }

    public LocalStep(LocalStepKind kind, WorldPosition position)
    {
        Kind = kind;
        Position = position;
    }
}

public sealed class LocalApproach
{
    public RoadNode? ExitNode { get; }
    public RoadAttachment Attachment { get; }
    public WorldPosition Entrance { get; }

    public WorldPosition OnRoad => Attachment.Segment.PointAt(Attachment.T);

    public LocalApproach(RoadNode? exitNode, RoadAttachment attachment, WorldPosition entrance)
    {
        ExitNode = exitNode;
        Attachment = attachment;
        Entrance = entrance;
    }

    // 从出口 Node 沿段走到 t。同一段出行没有出口 Node，这段代价已经算在路上。
    public double AlongSegmentCost
    {
        get
        {
            if (ExitNode == null)
                return 0;
            float tFrom = ReferenceEquals(ExitNode, Attachment.Segment.StartNode) ? 0f : 1f;
            return Attachment.Segment.LengthBetween(tFrom, Attachment.T);
        }
    }

    public double OffRoadDistance
    {
        get
        {
            return WorldPosition.Distance(OnRoad, Entrance);
        }
    }

    public List<LocalStep> Steps()
    {
        var steps = new List<LocalStep>(3);
        if (ExitNode != null)
            steps.Add(new LocalStep(LocalStepKind.Node, ExitNode.Position));
        steps.Add(new LocalStep(LocalStepKind.OnRoad, OnRoad));
        steps.Add(new LocalStep(LocalStepKind.Entrance, Entrance));
        return steps;
    }
}

// 最先一段：出门后不再搜图。固定两步：挂接点 t，再到入口 Node。同一段没有入口 Node。
public sealed class LocalDeparture
{
    public WorldPosition Entrance { get; }
    public RoadAttachment Attachment { get; }
    public RoadNode? EntryNode { get; }

    public WorldPosition OnRoad => Attachment.Segment.PointAt(Attachment.T);

    public LocalDeparture(WorldPosition entrance, RoadAttachment attachment, RoadNode? entryNode)
    {
        Entrance = entrance;
        Attachment = attachment;
        EntryNode = entryNode;
    }

    public double AlongSegmentCost
    {
        get
        {
            if (EntryNode == null)
                return 0;
            float tTo = ReferenceEquals(EntryNode, Attachment.Segment.StartNode) ? 0f : 1f;
            return Attachment.Segment.LengthBetween(Attachment.T, tTo);
        }
    }

    public double OffRoadDistance
    {
        get
        {
            return WorldPosition.Distance(Entrance, OnRoad);
        }
    }

    public List<LocalStep> Steps()
    {
        var steps = new List<LocalStep>(3);
        steps.Add(new LocalStep(LocalStepKind.Entrance, Entrance));
        steps.Add(new LocalStep(LocalStepKind.OnRoad, OnRoad));
        if (EntryNode != null)
            steps.Add(new LocalStep(LocalStepKind.Node, EntryNode.Position));
        return steps;
    }
}
