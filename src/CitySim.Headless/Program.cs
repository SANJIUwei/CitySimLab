using CitySim.Core;

Console.WriteLine($"{ProjectAnchor.Name} environment is ready.");

var world = new World();
var a = new World.WorldPosition { x = 0f, y = 0f, z = 0f };
var b = new World.WorldPosition { x = 3f, y = 4f, z = 0f };
Console.WriteLine($"Distance={world.Distance(a, b)}");

// 一条双向路的最小样本：两个节点、一段路、正反向各一条车道，用来肉眼核对当前模型。
var start = new RoadNode(a, 1);
var end = new RoadNode(b, 2);
var segment = new RoadSegment(start, end);
segment.AddLane(LaneDirection.Forward);
segment.AddLane(LaneDirection.Reverse);

Console.WriteLine($"Segment {start.ID}->{end.ID} lanes={segment.Lanes.Count}");
Console.WriteLine($"Lane0={segment.Lanes[0].Direction} Lane1={segment.Lanes[1].Direction}");
