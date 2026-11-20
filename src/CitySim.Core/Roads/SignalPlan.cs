using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CitySim.Core;

// A movement is a specific lane-to-lane turn, not just an approach or a road.
public readonly record struct SignalMovement(RoadLane Incoming, RoadLane Outgoing);

public readonly record struct SignalConflict(SignalMovement First, SignalMovement Second);

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

    public IReadOnlyList<SignalPhase> Phases { get; }
    public IReadOnlyCollection<SignalMovement> Movements { get; }
    public double CycleSeconds { get; }
    public double OffsetSeconds { get; }
    public long JunctionId { get; }

    public SignalPlan(IEnumerable<SignalPhase> phases, IEnumerable<SignalConflict>? conflicts = null,
        double offsetSeconds = 0)
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
                conflictPairs.Add((conflict.First, conflict.Second));
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

        // Normalize independently to avoid overflow if two finite clocks have opposite signs.
        double cycleClock = PositiveModulo(clockSeconds, CycleSeconds);
        double cycleOffset = PositiveModulo(OffsetSeconds, CycleSeconds);
        double time = PositiveModulo(cycleClock - cycleOffset, CycleSeconds);
        foreach (var interval in intervals)
        {
            if (time < interval.Start)
                return interval.Start - time;
            if (time < interval.End)
                return 0d;
        }
        return (CycleSeconds - time) + intervals[0].Start;
    }

    static double PositiveModulo(double value, double modulus)
    {
        double remainder = value % modulus;
        if (remainder < 0)
            remainder += modulus;
        return remainder >= modulus ? 0 : remainder;
    }
}
