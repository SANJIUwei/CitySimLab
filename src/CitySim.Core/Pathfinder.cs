using System.Collections.Generic;

// 所有需要寻路的物体只调这里。建筑不求路；事物提交请求、等回传。
public class Pathfinder
{
    readonly RoadNetwork _network;
    readonly Queue<PathRequest> _queue = new Queue<PathRequest>();

    public int PendingCount => _queue.Count;

    public Pathfinder(RoadNetwork network)
    {
        _network = network;
    }

    // 事物把建筑给的 A/B 指令交进来排队。不立刻算。
    public void Submit(Thing thing, TripCommand command)
    {
        _queue.Enqueue(new PathRequest(thing, command));
    }

    // 每帧处理定额条。算完把路线发回给事物。
    public int Process(int budget)
    {
        int processed = 0;
        while (processed < budget && _queue.Count > 0)
        {
            var request = _queue.Dequeue();
            var trip = _network.PlanTrip(request.Command.Origin, request.Command.Destination);
            request.Thing.ReceiveRoute(trip);
            processed++;
        }
        return processed;
    }

    // 已经在图上的物体：只要 Node 序列。仍同步，给测试和以后路上的车用。
    public GlobalPath Request(long startNodeId, long endNodeId)
    {
        return _network.FindPath(startNodeId, endNodeId);
    }

    sealed class PathRequest
    {
        public Thing Thing { get; }
        public TripCommand Command { get; }

        public PathRequest(Thing thing, TripCommand command)
        {
            Thing = thing;
            Command = command;
        }
    }
}
