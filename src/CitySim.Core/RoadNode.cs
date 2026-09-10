// 只表示路口或路的端点。谁连谁放在路段上，避免节点和边各记一份拓扑、改的时候对不上。
public class RoadNode
{
    public World.WorldPosition Position { get; set; }

    // 由创建者保证唯一；节点不加一，否则还要挂一个全局计数器，现在没有路网容器。
    public long ID { get; set; }

    public RoadNode(World.WorldPosition position, long id)
    {
        Position = position;
        ID = id;
    }
}
