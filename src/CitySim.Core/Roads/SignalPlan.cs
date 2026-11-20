using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CitySim.Core;

// A movement is a specific lane-to-lane turn, not just an approach or a road.
public readonly record struct SignalMovement(RoadLane Incoming, RoadLane Outgoing);

public readonly record struct SignalConflict(SignalMovement First, SignalMovement Second);

// Explicit per-movement control, never inferred from road class or geometry.
// Source: https://www.yancheng.gov.cn/art/2025/11/26/art_34214_4383922.html
public enum SignalRedRule
{
    Protected,
    CircularRightTurnMayYield,
    CircularRightTurnProhibited,
    GoverningArrow
}

public readonly record struct SignalTurnControl(SignalMovement Movement, SignalRedRule RedRule);

// Clear is an affirmative live snapshot covering priority vehicle, bicycle
// and pedestrian movements, including any traffic already clearing the turn.
public enum SignalConflictSnapshot { Unknown, Clear, ConflictPresent }

public sealed class SignalPhase
{
    public double GreenSeconds { get; }
    public double YellowSeconds { get; }
    public double AllRedSeconds { get; }
    public IReadOnlyList<SignalMovement> GreenMovements { get; }

    public SignalPhase(double greenSeconds, double yellowSeconds, double allRedSeconds,
        params SignalMovement[] greenMovements)
    {
        if (!double.IsFinite(greenSeconds) || greenSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(greenSeconds), "Use a positive finite green time.");
        if (!double.IsFinite(yellowSeconds) || yellowSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(yellowSeconds), "Use a finite nonnegative yellow time.");
        if (!double.IsFinite(allRedSeconds) || allRedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(allRedSeconds), "Use a finite nonnegative all-red time.");
        if (!double.IsFinite(greenSeconds + yellowSeconds + allRedSeconds))
            throw new ArgumentOutOfRangeException(nameof(greenSeconds), "The total phase duration must be finite.");
        if (greenMovements == null)
            throw new ArgumentNullException(nameof(greenMovements));
        if (greenMovements.Length == 0)
            throw new ArgumentException("A green phase must serve at least one movement.", nameof(greenMovements));

        var movements = new HashSet<SignalMovement>();
        foreach (var movement in greenMovements)
        {
            ValidateMovement(movement, nameof(greenMovements));
            if (!movements.Add(movement))
                throw new ArgumentException("A movement cannot appear twice in the same phase.", nameof(greenMovements));
        }

        GreenSeconds = greenSeconds;
        YellowSeconds = yellowSeconds;
        AllRedSeconds = allRedSeconds;
        GreenMovements = Array.AsReadOnly((SignalMovement[])greenMovements.Clone());
    }

    internal static void ValidateMovement(SignalMovement movement, string parameter)
    {
        if (movement.Incoming == null || movement.Outgoing == null ||
            movement.Incoming.ToId != movement.Outgoing.FromId)
            throw new ArgumentException("A signal movement must connect an incoming lane to an outgoing lane at a junction.", parameter);
    }
}

// A fixed cycle. No green authorizations start during yellow or all-red.
// Conflict pairs are supplied by the junction designer; geometry alone does not infer them.
public sealed class SignalPlan
{
    readonly Dictionary<SignalMovement, List<(double Start, double End)>> _greens =
        new Dictionary<SignalMovement, List<(double Start, double End)>>();

    readonly List<SignalConflict> _conflicts = new();
    readonly Dictionary<SignalMovement, SignalRedRule> _redRules = new();
    readonly List<(double Start, double End)> _greenStages = new();

    public IReadOnlyList<SignalPhase> Phases { get; }
    public IReadOnlyCollection<SignalMovement> Movements { get; }
    public double CycleSeconds { get; }
    public double OffsetSeconds { get; }
    public long JunctionId { get; }

    public SignalPlan(IEnumerable<SignalPhase> phases, IEnumerable<SignalConflict>? conflicts = null,
        double offsetSeconds = 0, IEnumerable<SignalTurnControl>? turnControls = null)
    {
        if (phases == null)
            throw new ArgumentNullException(nameof(phases));
        if (!double.IsFinite(offsetSeconds))
            throw new ArgumentOutOfRangeException(nameof(offsetSeconds));

        var phaseList = new List<SignalPhase>(phases);
        if (phaseList.Count == 0 || phaseList[0] == null)
            throw new ArgumentException("The plan must contain at least one valid phase.", nameof(phases));
        JunctionId = phaseList[0].GreenMovements[0].Incoming.ToId;

        var conflictPairs = new HashSet<(SignalMovement, SignalMovement)>();
        if (conflicts != null)
        {
            foreach (var conflict in conflicts)
            {
                SignalPhase.ValidateMovement(conflict.First, nameof(conflicts));
                SignalPhase.ValidateMovement(conflict.Second, nameof(conflicts));
                if (conflict.First == conflict.Second || conflict.First.Incoming.ToId != JunctionId ||
                    conflict.Second.Incoming.ToId != JunctionId)
                    throw new ArgumentException("A conflict must involve two distinct movements at this junction.", nameof(conflicts));
                if (conflictPairs.Add((conflict.First, conflict.Second)))
                    _conflicts.Add(conflict);
                conflictPairs.Add((conflict.Second, conflict.First));
            }
        }

        double start = 0;
        foreach (var phase in phaseList)
        {
            if (phase == null)
                throw new ArgumentException("A phase cannot be null.", nameof(phases));
            double greenEnd = start + phase.GreenSeconds;
            double yellowEnd = greenEnd + phase.YellowSeconds;
            double nextStart = yellowEnd + phase.AllRedSeconds;
            if (!double.IsFinite(nextStart) || greenEnd <= start ||
                phase.YellowSeconds > 0 && yellowEnd <= greenEnd ||
                phase.AllRedSeconds > 0 && nextStart <= yellowEnd)
                throw new ArgumentException("Each interval must fit in a finite cycle without losing its duration.", nameof(phases));

            _greenStages.Add((start, greenEnd));
            for (int i = 0; i < phase.GreenMovements.Count; i++)
            {
                var movement = phase.GreenMovements[i];
                if (movement.Incoming.ToId != JunctionId)
                    throw new ArgumentException("A signal plan must serve just one junction.", nameof(phases));
                for (int j = 0; j < i; j++)
                    if (conflictPairs.Contains((movement, phase.GreenMovements[j])))
                        throw new ArgumentException("A phase cannot give green to a configured conflict pair.", nameof(phases));

                if (!_greens.TryGetValue(movement, out var intervals))
                    _greens[movement] = intervals = new List<(double, double)>();
                intervals.Add((start, greenEnd));
            }
            start = nextStart;
        }

        if (turnControls != null)
        {
            foreach (var control in turnControls)
            {
                if (!_greens.ContainsKey(control.Movement))
                    throw new ArgumentException("A movement with signal control must have a scheduled green.", nameof(turnControls));
                if (!Enum.IsDefined(typeof(SignalRedRule), control.RedRule) ||
                    !_redRules.TryAdd(control.Movement, control.RedRule))
                    throw new ArgumentException("A movement must have a single valid signal rule.", nameof(turnControls));
            }
        }

        CycleSeconds = start;
        OffsetSeconds = offsetSeconds;
        Phases = new ReadOnlyCollection<SignalPhase>(phaseList);
        Movements = new ReadOnlyCollection<SignalMovement>(new List<SignalMovement>(_greens.Keys));
    }

    // A positive offset delays the start of phase 1. A query during clearance
    // returns time to the next green, possibly in the next cycle.
    // Null means no phase in this plan grants the movement a green.
    public double? WaitUntilGreen(SignalMovement movement, double clockSeconds)
    {
        if (!double.IsFinite(clockSeconds))
            throw new ArgumentOutOfRangeException(nameof(clockSeconds));
        if (!_greens.TryGetValue(movement, out var intervals))
            return null;

        double time = CycleTime(clockSeconds);
        foreach (var interval in intervals)
        {
            if (time < interval.Start)
                return interval.Start - time;
            if (time < interval.End)
                return 0d;
        }
        return (CycleSeconds - time) + intervals[0].Start;
    }

    // A snapshot can permit entry only now. A red or blocked vehicle otherwise
    // plans for protected green; the caller can recheck when traffic changes.
    public double? WaitUntilPermitted(SignalMovement movement, double clockSeconds,
        SignalConflictSnapshot conflictSnapshot = SignalConflictSnapshot.Unknown)
    {
        double? wait = WaitUntilGreen(movement, clockSeconds);
        if (!(wait > 0) || conflictSnapshot != SignalConflictSnapshot.Clear ||
            !_redRules.TryGetValue(movement, out var rule) ||
            rule != SignalRedRule.CircularRightTurnMayYield)
            return wait;

        double time = CycleTime(clockSeconds);
        foreach (var stage in _greenStages)
            if (time >= stage.Start && time < stage.End)
                return 0d; // the configured circular indication is red at this instant

        return wait; // another phase's yellow/all-red is not a permissive circular red
    }

    internal SignalPlan RemapMovements(Func<SignalMovement, SignalMovement> remap)
    {
        if (remap == null) throw new ArgumentNullException(nameof(remap));
        var mapped = new Dictionary<SignalMovement, SignalMovement>();
        SignalMovement Map(SignalMovement source)
        {
            if (!mapped.TryGetValue(source, out var target))
                mapped[source] = target = remap(source);
            return target;
        }

        var phases = new List<SignalPhase>(Phases.Count);
        foreach (var phase in Phases)
        {
            var movements = new SignalMovement[phase.GreenMovements.Count];
            for (int i = 0; i < movements.Length; i++)
                movements[i] = Map(phase.GreenMovements[i]);
            phases.Add(new SignalPhase(phase.GreenSeconds, phase.YellowSeconds, phase.AllRedSeconds, movements));
        }

        var conflicts = new List<SignalConflict>(_conflicts.Count);
        foreach (var conflict in _conflicts)
            conflicts.Add(new SignalConflict(Map(conflict.First), Map(conflict.Second)));

        var controls = new List<SignalTurnControl>(_redRules.Count);
        foreach (var rule in _redRules)
            controls.Add(new SignalTurnControl(Map(rule.Key), rule.Value));
        return new SignalPlan(phases, conflicts, OffsetSeconds, controls);
    }

    double CycleTime(double clockSeconds)
    {
        // Normalize independently to avoid overflow if two finite clocks have opposite signs.
        double cycleClock = PositiveModulo(clockSeconds, CycleSeconds);
        double cycleOffset = PositiveModulo(OffsetSeconds, CycleSeconds);
        return PositiveModulo(cycleClock - cycleOffset, CycleSeconds);
    }

    static double PositiveModulo(double value, double modulus)
    {
        double remainder = value % modulus;
        if (remainder < 0)
            remainder += modulus;
        return remainder >= modulus ? 0 : remainder;
    }
}
