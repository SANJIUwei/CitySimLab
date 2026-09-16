using CitySim.Core;

Console.WriteLine($"{ProjectAnchor.Name} environment is ready.");

var a = new WorldPosition { x = 0f, y = 0f, z = 0f };
var b = new WorldPosition { x = 3f, y = 4f, z = 0f };
Console.WriteLine($"Distance={WorldPosition.Distance(a, b)}");

var start = new RoadNode(a, 1);
var end = new RoadNode(b, 2);
var segment = new RoadSegment(start, end);
segment.AddLane(LaneDirection.Forward);
segment.AddLane(LaneDirection.Reverse);

Console.WriteLine($"Segment {start.ID}->{end.ID} lanes={segment.Lanes.Count}");
Console.WriteLine($"Lane0={segment.Lanes[0].Direction} Lane1={segment.Lanes[1].Direction}");

var net = new RoadNetwork();
var n1 = new RoadNode(a, 1);
var n2 = new RoadNode(b, 2);
net.AddSegment(n1, n2, cost: 5);
var houseA = new Building(a, a, net);
var houseB = new Building(b, b, net);
var scheduler = new ComputeScheduler();
var pathCenter = new PathCenter(net, scheduler);
var thing = new Thing();
TripStarter.Request(thing, pathCenter, houseA, houseB);
pathCenter.Process(1);
var trip = thing.Route!;
Console.WriteLine($"Waiting={thing.WaitingForRoute} Kind={thing.JudgeLocalPath()} SameSegment={trip.SameSegment} LocalCost={trip.Global.TotalCost} Nodes={trip.Global.NodeIds.Count}");
Console.WriteLine($"Departure steps={trip.Departure.Steps().Count} Approach steps={trip.Approach.Steps().Count}");
