using System;
using System.Collections.Generic;

namespace CitySim.Core;

public partial class RoadNetwork
{
    readonly struct LegalStart
    {
        public readonly long NodeId;
        public readonly RoadLane? IncomingLane;
        public readonly double Cost;

        public LegalStart(long nodeId, RoadLane? incomingLane, double cost)
            => (NodeId, IncomingLane, Cost) = (nodeId, incomingLane, cost);
    }

    readonly struct LegalGoal
    {
        public readonly long NodeId;
        public readonly RoadLane? OutgoingLane;
        public readonly double Cost;

        public LegalGoal(long nodeId, RoadLane? outgoingLane, double cost)
            => (NodeId, OutgoingLane, Cost) = (nodeId, outgoingLane, cost);
    }

    sealed class LegalPath
    {
        public readonly IReadOnlyList<long> NodeIds;
        public readonly IReadOnlyList<RoadLane> Lanes;
        public readonly double TotalCost;
        public readonly int StartIndex;
        public readonly int GoalIndex;
        public readonly IReadOnlyList<PathSearchStep> SearchSteps;

        public LegalPath(IReadOnlyList<long> nodeIds, IReadOnlyList<RoadLane> lanes, double totalCost,
            int startIndex, int goalIndex, IReadOnlyList<PathSearchStep> searchSteps)
            => (NodeIds, Lanes, TotalCost, StartIndex, GoalIndex, SearchSteps) =
               (nodeIds, lanes, totalCost, startIndex, goalIndex, searchSteps);
    }

    readonly struct LegalLabel
    {
        public readonly long Node;
        public readonly RoadLane? Incoming;
        public readonly double Cost;
        public readonly int Parent;
        public readonly int StartIndex;

        public LegalLabel(long node, RoadLane? incoming, double cost, int parent, int startIndex)
            => (Node, Incoming, Cost, Parent, StartIndex) = (node, incoming, cost, parent, startIndex);
    }

    // 虚拟起终点把建筑半段接入成本纳入同一次搜索。状态保留来向车道，
    // 因为同一路口从不同车道进来，可以合法驶出的方向不一定相同。
    // heuristic 必须是到任一终点候选（含接入成本）的下界。
    LegalPath? FindLegalPath(IReadOnlyList<LegalStart> starts, IReadOnlyList<LegalGoal> goals,
        TravelOptions options, bool captureSearch = false, Func<long, double>? heuristic = null)
        => FindLegalPath(starts, goals, options, out _, captureSearch, heuristic);

    LegalPath? FindLegalPath(IReadOnlyList<LegalStart> starts, IReadOnlyList<LegalGoal> goals,
        TravelOptions options, out RouteFailure failure, bool captureSearch = false, Func<long, double>? heuristic = null)
    {
        failure = RouteFailure.NoLegalRoute;
        if (starts.Count == 0)
        {
            failure = RouteFailure.DepartureNotAllowed;
            return null;
        }
        if (goals.Count == 0)
        {
            failure = RouteFailure.ArrivalNotAllowed;
            return null;
        }

        var goalsAt = new Dictionary<long, List<int>>();
        for (int i = 0; i < goals.Count; i++)
        {
            var goal = goals[i];
            if (!_nodesById.ContainsKey(goal.NodeId) || !IsLegalSearchCost(goal.Cost) ||
                goal.OutgoingLane is { } outLane && (outLane.FromId != goal.NodeId || !outLane.CanTravel(options)))
                continue;
            if (!goalsAt.TryGetValue(goal.NodeId, out var atNode))
                goalsAt[goal.NodeId] = atNode = new List<int>();
            atNode.Add(i);
        }
        if (goalsAt.Count == 0)
        {
            failure = RouteFailure.ArrivalNotAllowed;
            return null;
        }

        var heap = new PathScratch();
        var labelSlots = new Dictionary<(long node, RoadLane? incoming), int>();
        var labels = new List<LegalLabel>();
        var trace = captureSearch ? new List<PathSearchStep>() : null;

        bool Relax(long node, RoadLane? incoming, double cost, int parent, int startIndex)
        {
            if (!IsLegalSearchCost(cost))
                return false;
            if (labelSlots.TryGetValue((node, incoming), out int index))
            {
                if (cost >= labels[index].Cost)
                    return false;
                labels[index] = new LegalLabel(node, incoming, cost, parent, startIndex);
            }
            else
            {
                index = labels.Count;
                labelSlots.Add((node, incoming), index);
                labels.Add(new LegalLabel(node, incoming, cost, parent, startIndex));
            }
            HeapPush(heap, cost, index, cost + (heuristic?.Invoke(node) ?? 0));
            return true;
        }

        for (int i = 0; i < starts.Count; i++)
        {
            var start = starts[i];
            if (!_nodesById.ContainsKey(start.NodeId) ||
                start.IncomingLane is { } inLane && (inLane.ToId != start.NodeId || !inLane.CanTravel(options)))
                continue;
            Relax(start.NodeId, start.IncomingLane, start.Cost, -1, i);
        }

        if (heap.Heap.Count == 0)
        {
            failure = RouteFailure.DepartureNotAllowed;
            return null;
        }

        bool blockedNearGoalByTurn = false;
        bool blockedNearGoalByCategoryOrTime = false;
        bool blockedNearGoalByDirection = false;
        int bestLabel = -1;
        int bestGoal = -1;
        double bestCost = double.PositiveInfinity;
        while (heap.Heap.Count > 0)
        {
            var (cost, slot, priority) = HeapPop(heap);
            if (priority >= bestCost)
                break;
            int index = (int)slot;
            var label = labels[index];
            if (cost != label.Cost)
                continue;

            if (goalsAt.TryGetValue(label.Node, out var localGoals))
            {
                foreach (int goalIndex in localGoals)
                {
                    var goal = goals[goalIndex];
                    double wait = 0;
                    if (label.Incoming != null && goal.OutgoingLane != null &&
                        !TryGetMovement(label.Incoming, goal.OutgoingLane, out wait))
                    {
                        blockedNearGoalByTurn = true;
                        continue;
                    }

                    double total = cost + goal.Cost + wait;
                    if (total < bestCost)
                    {
                        bestCost = total;
                        bestLabel = index;
                        bestGoal = goalIndex;
                    }
                }
            }

            List<PathRelaxation>? improvements = captureSearch ? new List<PathRelaxation>() : null;
            // 若这个候选已达到下界，则不用再展开它的出边。
            if (priority < bestCost && _roadsAt.TryGetValue(label.Node, out var roads))
            {
                foreach (var segment in roads)
                {
                    long opposite = segment.StartNode.ID == label.Node ? segment.EndNode.ID : segment.StartNode.ID;
                    bool leadsToGoal = goalsAt.ContainsKey(opposite);
                    bool hasOutgoing = false;
                    foreach (var lane in segment.RoutingLanes)
                    {
                        if (lane.FromId != label.Node)
                            continue;
                        hasOutgoing = true;
                        if (!lane.CanTravel(options))
                        {
                            blockedNearGoalByCategoryOrTime |= leadsToGoal;
                            continue;
                        }
                        double wait = 0;
                        if (label.Incoming != null && !TryGetMovement(label.Incoming, lane, out wait))
                        {
                            blockedNearGoalByTurn |= leadsToGoal;
                            continue;
                        }
                        double nextCost = cost + _baseCosts[segment] + wait;
                        if (Relax(lane.ToId, lane, nextCost, index, label.StartIndex))
                            improvements?.Add(new PathRelaxation(lane.ToId, nextCost));
                    }
                    blockedNearGoalByDirection |= leadsToGoal && !hasOutgoing;
                }
            }
            if (trace != null)
                trace.Add(new PathSearchStep(label.Node, cost, priority, improvements!.AsReadOnly()));
        }

        if (bestLabel < 0)
        {
            // 仅在终点附近观察到单种阻塞时给出具体提示，避免误报无关支路。
            int blockers = (blockedNearGoalByTurn ? 1 : 0) + (blockedNearGoalByCategoryOrTime ? 1 : 0) +
                (blockedNearGoalByDirection ? 1 : 0);
            if (blockers == 1)
                failure = blockedNearGoalByTurn ? RouteFailure.TurnNotAllowed :
                    blockedNearGoalByDirection ? RouteFailure.DirectionNotAllowed : RouteFailure.VehicleOrTimeRestricted;
            return null;
        }

        failure = RouteFailure.None;
        var nodeIds = new List<long>();
        var lanes = new List<RoadLane>();
        int current = bestLabel;
        while (true)
        {
            var label = labels[current];
            nodeIds.Add(label.Node);
            if (label.Parent < 0)
                break;
            lanes.Add(label.Incoming!);
            current = label.Parent;
        }
        nodeIds.Reverse();
        lanes.Reverse();
        return new LegalPath(nodeIds.AsReadOnly(), lanes.AsReadOnly(), bestCost, labels[bestLabel].StartIndex,
            bestGoal, trace == null ? Array.Empty<PathSearchStep>() : trace.AsReadOnly());
    }

    static bool IsLegalSearchCost(double cost)
        => cost >= 0 && !double.IsPositiveInfinity(cost);
}
