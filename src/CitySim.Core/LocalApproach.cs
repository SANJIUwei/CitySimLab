using System.Collections.Generic;

// 最后一段：离开全局最后一个 Node 之后不再搜图。
// 固定两步：沿目标路段走到缓存的 t，再从挂接点进门。
public enum LocalStepKind
{
    ExitNode,
    OnRoad,
    Entrance
}

public readonly struct LocalStep
{
    public LocalStepKind Kind { get; }
    public World.WorldPosition Position { get; }

    public LocalStep(LocalStepKind kind, World.WorldPosition position)
    {
        Kind = kind;
        Position = position;
    }
}

public sealed class LocalApproach
{
    public RoadNode? ExitNode { get; }
    public RoadAttachment Attachment { get; }
    public World.WorldPosition Entrance { get; }

    public World.WorldPosition OnRoad => Attachment.Segment.PointAt(Attachment.T);

    public LocalApproach(RoadNode? exitNode, RoadAttachment attachment, World.WorldPosition entrance)
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
            var world = new World();
            return world.Distance(OnRoad, Entrance);
        }
    }

    public List<LocalStep> Steps()
    {
        var steps = new List<LocalStep>(3);
        if (ExitNode != null)
            steps.Add(new LocalStep(LocalStepKind.ExitNode, ExitNode.Position));
        steps.Add(new LocalStep(LocalStepKind.OnRoad, OnRoad));
        steps.Add(new LocalStep(LocalStepKind.Entrance, Entrance));
        return steps;
    }
}
