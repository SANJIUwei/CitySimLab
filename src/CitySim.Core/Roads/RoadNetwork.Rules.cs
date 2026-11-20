using System;
using System.Collections.Generic;
using System.Linq;

namespace CitySim.Core;

public partial class RoadNetwork
{
    void EnableRoadRules()
    {
        _usesRoadRules = true;
        TopologyVersion++;
    }

    public bool ContainsSegment(RoadSegment segment) => _baseCosts.ContainsKey(segment);

    public bool RemoveSegment(RoadSegment segment)
    {
        if (!_baseCosts.TryGetValue(segment, out var cost))
            return false;
        _baseCosts.Remove(segment);
        TopologyVersion++;
        segment.LanesChanged -= EnableRoadRules;
        Segments.Remove(segment);
        long a = segment.StartNode.ID, b = segment.EndNode.ID;
        _roadsAt[a].Remove(segment);
        if (a != b) _roadsAt[b].Remove(segment);
        _adj[a].Remove((b, cost));
        _adj[b].Remove((a, cost));
        foreach (var key in _turns.Keys.Where(x => x.Incoming.Segment == segment || x.Outgoing.Segment == segment).ToArray())
            _turns.Remove(key);
        lock (_landmarkGate)
            System.Threading.Volatile.Write(ref _landmarkIndex, null);
        return true;
    }

    public void RequireExplicitTurns(RoadNode node)
    {
        if (!_nodesById.TryGetValue(node.ID, out var actual) || actual != node)
            throw new ArgumentException("The junction must be in this network", nameof(node));
        _explicitTurns.Add(node.ID);
        EnableRoadRules();
    }

    public void SetTurn(RoadLane incoming, RoadLane outgoing, bool allowed = true, double waitingCost = 0)
    {
        if (!_baseCosts.ContainsKey(incoming.Segment) || !_baseCosts.ContainsKey(outgoing.Segment) ||
            !incoming.Segment.RoutingLanes.Contains(incoming) || !outgoing.Segment.RoutingLanes.Contains(outgoing) ||
            !HasPhysicalMovement(incoming, outgoing))
            throw new ArgumentException("Turn lanes must share a connected junction");
        if (!double.IsFinite(waitingCost) || waitingCost < 0)
            throw new ArgumentOutOfRangeException(nameof(waitingCost));
        _turns[(incoming, outgoing)] = (allowed, waitingCost);
        EnableRoadRules();
    }

    bool TryGetMovement(RoadLane incoming, RoadLane outgoing, out double wait)
    {
        wait = 0;
        if (!HasPhysicalMovement(incoming, outgoing) || !HasGreenPhase(incoming, outgoing))
            return false;
        if (_turns.TryGetValue((incoming, outgoing), out var setting))
        {
            wait = setting.Wait;
            return setting.Allowed;
        }
        return !_explicitTurns.Contains(incoming.ToId) && incoming.FromId != outgoing.ToId;
    }

    static bool HasPhysicalMovement(RoadLane incoming, RoadLane outgoing)
    {
        if (incoming.ToId != outgoing.FromId || !incoming.Segment.CanPhysicallyConnect(outgoing.Segment))
            return false;
        // 受控主线需要真实的匝道/接续路段，单独设置转向不能凭空造连接。
        bool mainA = ControlledMainline(incoming.Segment);
        bool mainB = ControlledMainline(outgoing.Segment);
        return mainA == mainB || incoming.Segment.Profile.Role == RoadRole.Ramp ||
            outgoing.Segment.Profile.Role == RoadRole.Ramp;
    }

    static bool ControlledMainline(RoadSegment s) => s.Profile.Role != RoadRole.Frontage &&
        (s.Profile.UrbanClass == UrbanRoadClass.Expressway || s.Profile.HighwayGrade == HighwayTechnicalGrade.Motorway);

    Func<long, double>? LegalHeuristic(PathAlgorithm algorithm, IReadOnlyList<LegalGoal> goals)
    {
        if (algorithm != PathAlgorithm.Alt || goals.Count == 0)
            return null;
        var index = GetLandmarks();
        var slots = goals.Select(goal => (slot: index.Slot(goal.NodeId), goal.Cost)).ToArray();
        // 预计算仍用无向松弛图。它只会低估合法路线；方向、车型、禁转会删除走法，
        // 等待只增加代价，因此不会把真实更优的路径剪掉。
        return node =>
        {
            double min = double.PositiveInfinity;
            foreach (var goal in slots)
                min = Math.Min(min, index.Estimate(node, goal.slot) + goal.Cost);
            return min;
        };
    }

    GlobalPath FindConfiguredPath(long start, long goal, PathAlgorithm algorithm, bool captureSearch, TravelOptions options)
    {
        var starts = new[] { new LegalStart(start, null, 0) };
        var goals = new[] { new LegalGoal(goal, null, 0) };
        var heuristic = LegalHeuristic(algorithm, goals);
        var actual = heuristic == null ? PathAlgorithm.Dijkstra : PathAlgorithm.Alt;
        var path = FindLegalPath(starts, goals, options, out var failure, captureSearch, heuristic);
        return path == null ? GlobalPath.Failed(failure, actual) :
            new GlobalPath(new List<long>(path.NodeIds), path.TotalCost, actual, path.SearchSteps, path.Lanes);
    }

    Trip PlanConfiguredTrip(RouteEnd from, RouteEnd to, PathAlgorithm algorithm, bool captureSearch, TravelOptions options)
    {
        var a = from.Attachment;
        var b = to.Attachment;
        var aPosition = a.Segment.PointAt(a.T);
        var bPosition = b.Segment.PointAt(b.T);

        Trip Make(GlobalPath global, bool local, RoadNode? entry = null, RoadNode? exit = null,
            double? cost = null, RoadLane? departureLane = null, RoadLane? arrivalLane = null) =>
            new(from, to, local, global, aPosition, bPosition,
                new LocalDeparture(from.Access, a, entry), new LocalApproach(exit, b, to.Access),
                cost, departureLane, arrivalLane)
                { PlannedNetwork = this, PlannedTopology = TopologyVersion, PlannedTravel = options };

        if (!_baseCosts.TryGetValue(a.Segment, out var aCost) || !_baseCosts.TryGetValue(b.Segment, out var bCost) ||
            from.IsBuildingAccess && !a.Segment.Profile.AllowsBuildingAccess ||
            to.IsBuildingAccess && !b.Segment.Profile.AllowsBuildingAccess)
            return Make(GlobalPath.Failed(RouteFailure.NoBuildingAccess), false);

        var startLanes = a.Segment.RoutingLanes.Where(l => l.CanTravel(options) && (from.ExitToward & l.DirectionFlag) != 0).ToArray();
        var endLanes = b.Segment.RoutingLanes.Where(l => l.CanTravel(options) && (to.EnterFrom & l.DirectionFlag) != 0).ToArray();
        if (startLanes.Length == 0)
            return Make(GlobalPath.Failed(RouteFailure.DepartureNotAllowed), false);
        if (endLanes.Length == 0)
            return Make(GlobalPath.Failed(RouteFailure.ArrivalNotAllowed), false);

        RoadLane? direct = null;
        double directCost = double.PositiveInfinity;
        if (a.Segment == b.Segment)
        {
            direct = startLanes.FirstOrDefault(l => (a.T == b.T ||
                (b.T > a.T ? l.Direction == LaneDirection.Forward : l.Direction == LaneDirection.Reverse))
                && endLanes.Contains(l));
            if (direct != null)
                directCost = Math.Abs(b.T - a.T) * aCost;
        }

        var starts = startLanes.Select(l => new LegalStart(l.ToId, l,
            aCost * (l.Direction == LaneDirection.Forward ? 1 - a.T : a.T))).ToArray();
        var goals = endLanes.Select(l => new LegalGoal(l.FromId, l,
            bCost * (l.Direction == LaneDirection.Forward ? b.T : 1 - b.T))).ToArray();
        var heuristic = LegalHeuristic(algorithm, goals);
        var actual = heuristic == null ? PathAlgorithm.Dijkstra : PathAlgorithm.Alt;
        var failure = RouteFailure.NoLegalRoute;
        var path = directCost == 0 ? null :
            FindLegalPath(starts, goals, options, out failure, captureSearch, heuristic);
        if (direct != null && (path == null || directCost <= path.TotalCost))
            return Make(new GlobalPath(new(), directCost, lanes: Array.AsReadOnly(new[] { direct })),
                true, cost: directCost, departureLane: direct, arrivalLane: direct);
        if (path == null)
            return Make(GlobalPath.Failed(failure, actual), false);
        var start = starts[path.StartIndex];
        var goal = goals[path.GoalIndex];
        double globalCost = Math.Max(0, path.TotalCost - start.Cost - goal.Cost);
        return Make(new GlobalPath(new List<long>(path.NodeIds), globalCost, actual, path.SearchSteps, path.Lanes), false,
            _nodesById[start.NodeId], _nodesById[goal.NodeId], path.TotalCost, start.IncomingLane, goal.OutgoingLane);
    }
}
