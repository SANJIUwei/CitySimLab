using System;
using System.Collections.Generic;
using System.Linq;

namespace CitySim.Core;

public partial class RoadNetwork
{
    readonly Dictionary<long, SignalPlan> _signals = new();

    public void SetSignalPlan(RoadNode junction, SignalPlan plan)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (!_nodesById.TryGetValue(junction.ID, out var registered) || registered != junction)
            throw new ArgumentException("Use a registered junction", nameof(junction));

        foreach (var movement in plan.Movements)
        {
            var incoming = movement.Incoming;
            var outgoing = movement.Outgoing;
            if (incoming.ToId != junction.ID || !_baseCosts.ContainsKey(incoming.Segment) ||
                !_baseCosts.ContainsKey(outgoing.Segment) || !incoming.Segment.RoutingLanes.Contains(incoming) ||
                !outgoing.Segment.RoutingLanes.Contains(outgoing) || !HasPhysicalMovement(incoming, outgoing))
                throw new ArgumentException("A signal can only govern physical movements at its own junction", nameof(plan));
        }
        _signals[junction.ID] = plan;
        EnableRoadRules();
    }

    public bool HasSignalPlan(long junctionId) => _signals.ContainsKey(junctionId);

    public bool RemoveSignalPlan(RoadNode junction)
    {
        if (!_nodesById.TryGetValue(junction.ID, out var registered) || registered != junction ||
            !_signals.Remove(junction.ID))
            return false;
        EnableRoadRules();
        return true;
    }

    // null 禁行；0 可以通行；正值是时间到下一次绿灯，不是路线断开。
    public double? WaitUntilTurnPermitted(RoadLane incoming, RoadLane outgoing, double clockSeconds,
        SignalConflictSnapshot conflictSnapshot = SignalConflictSnapshot.Unknown)
    {
        if (!double.IsFinite(clockSeconds) || clockSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(clockSeconds));
        if (!ContainsSegment(incoming.Segment) || !ContainsSegment(outgoing.Segment) ||
            !incoming.Segment.RoutingLanes.Contains(incoming) || !outgoing.Segment.RoutingLanes.Contains(outgoing) ||
            !TryGetMovement(incoming, outgoing, out _))
            return null;
        return _signals.TryGetValue(incoming.ToId, out var plan)
            ? plan.WaitUntilPermitted(new SignalMovement(incoming, outgoing), clockSeconds, conflictSnapshot)
            : 0;
    }

    bool HasGreenPhase(RoadLane incoming, RoadLane outgoing) =>
        !_signals.TryGetValue(incoming.ToId, out var plan) ||
        plan.Movements.Contains(new SignalMovement(incoming, outgoing));
}
