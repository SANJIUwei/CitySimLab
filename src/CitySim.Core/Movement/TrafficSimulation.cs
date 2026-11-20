using System;
using System.Collections.Generic;

namespace CitySim.Core;

/// <summary>Numerical simulation inputs, not engineering standards or roadway geometry.</summary>
public sealed record TrafficParameters(double StepSeconds, double MinimumGapMeters, double JunctionClearanceMeters);

public enum TrafficWaitReason
{
    None, StartingLaneOccupied, FollowingVehicle, JunctionOccupied, ExitLaneOccupied,
    RedSignal, Yielding, Unreachable, NeedsReplan
}

public sealed class TrafficVehicle
{
    internal TrafficVehicle(long id, Trip trip, double speed, double accessSpeed, double length, TravelOptions options)
        => (Id, Trip, RoadSpeedKph, AccessSpeedKph, LengthMeters, Travel) =
            (id, trip, speed, accessSpeed, length, options);

    public long Id { get; }
    public Trip Trip { get; internal set; }
    public double RoadSpeedKph { get; }
    public double AccessSpeedKph { get; }
    public double LengthMeters { get; }
    public TravelOptions Travel { get; }
    internal TimedRoadTraveler? Traveler { get; set; }
    internal RoadLane? OccupiedLaneAtReplan { get; set; }
    internal TrafficWaitReason TrafficHold { get; set; }

    public WorldPosition Position => Traveler?.Position ?? Trip.From.Access;
    public RoadLane? CurrentLane => Traveler?.TrafficLane ??
        (Traveler?.TrafficDistanceTravelledUnits == 0 && !Arrived ? OccupiedLaneAtReplan : null);
    public bool Arrived => Traveler?.State == TimedTravelState.Arrived;
    public bool NeedsReplan => Traveler?.State == TimedTravelState.NeedsReplan;
    public bool Unreachable => Traveler == null;
    public RightOfWayDecision? YieldReason => Traveler?.HoldReason;

    public TrafficWaitReason HoldReason => Unreachable ? TrafficWaitReason.Unreachable
        : NeedsReplan ? TrafficWaitReason.NeedsReplan
        : Traveler?.State switch
        {
            TimedTravelState.WaitingForGreen => TrafficWaitReason.RedSignal,
            TimedTravelState.WaitingForRightOfWay => TrafficWaitReason.Yielding,
            _ => TrafficHold
        };
}

/// <summary>
/// Per-junction priority inputs. Current snapshots only include explicitly reported conflicting
/// movements; use supplied sign and roundabout policy when those features are present.
/// </summary>
public sealed record TrafficJunctionPolicy(JunctionControl Control = JunctionControl.Uncontrolled,
    Func<SignalMovement, SignalMovement, SignedPriority>? SignedPriorityBetween = null,
    Func<SignalMovement, bool>? IsEnteringRoundabout = null,
    Func<SignalMovement, bool>? IsCirculating = null);

public sealed class TrafficSimulation
{
    readonly RoadNetwork _network;
    readonly double _metersPerUnit;
    readonly TrafficParameters _parameters;
    readonly List<TrafficVehicle> _vehicles = new();
    readonly Dictionary<RoadLane, List<LaneOccupancy>> _laneSweeps = new();
    readonly List<Approach> _approaches = new();
    readonly List<Reservation> _reservations = new();
    readonly Dictionary<long, TrafficJunctionPolicy> _junctionPolicies = new();
    readonly HashSet<(long, SignalMovement)> _observedStops = new();
    double _pending;
    long _lastId;

    // Explicit null means no geometry/conflict map: reserve the entire intersection.
    readonly Func<SignalMovement, SignalMovement, bool>? _junctionConflicts;
    readonly Func<SignalMovement, double, bool?>? _pedestrianConflict;

    sealed class LaneOccupancy
    {
        public readonly TrafficVehicle Vehicle;
        public double Start;
        public double End;
        public LaneOccupancy(TrafficVehicle vehicle, double station) => (Vehicle, Start, End) = (vehicle, station, station);
    }

    readonly record struct Approach(TrafficVehicle Vehicle, SignalMovement Movement, double DistanceMeters);
    readonly record struct Reservation(TrafficVehicle Vehicle, SignalMovement Movement, double EntryDistance);

    /// <summary>Shared simulated clock; fractional elapsed time below one step is buffered.</summary>
    public double ClockSeconds { get; private set; }
    public IReadOnlyList<TrafficVehicle> Vehicles { get; }

    public TrafficSimulation(RoadNetwork network, double metersPerUnit, TrafficParameters parameters,
        double clockSeconds = 0, Func<SignalMovement, SignalMovement, bool>? junctionConflicts = null,
        Func<SignalMovement, double, bool?>? pedestrianConflict = null)
    {
        _network = network ?? throw new ArgumentNullException(nameof(network));
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        if (!Positive(metersPerUnit) || !Positive(parameters.StepSeconds) ||
            !Nonnegative(parameters.MinimumGapMeters) || !Positive(parameters.JunctionClearanceMeters) ||
            !Nonnegative(clockSeconds) || clockSeconds + parameters.StepSeconds <= clockSeconds)
            throw new ArgumentOutOfRangeException(nameof(parameters), "Supply finite positive units and simulation timing.");
        _metersPerUnit = metersPerUnit;
        _junctionConflicts = junctionConflicts;
        _pedestrianConflict = pedestrianConflict;
        ClockSeconds = clockSeconds;
        Vehicles = _vehicles.AsReadOnly();
    }

    public TrafficVehicle AddVehicle(Trip trip, double roadSpeedKph, double accessSpeedKph,
        double lengthMeters = 4, TravelOptions? travel = null)
    {
        if (trip == null) throw new ArgumentNullException(nameof(trip));
        if (!Positive(roadSpeedKph) || !Positive(accessSpeedKph) || !Positive(lengthMeters))
            throw new ArgumentOutOfRangeException(nameof(roadSpeedKph), "Specify positive finite vehicle dimensions and speeds.");
        var options = travel ?? trip.PlannedTravel ?? TravelOptions.Default;
        options.Validate();
        if (options.Vehicle is not (VehicleKind.Car or VehicleKind.Bus or VehicleKind.Truck))
            throw new ArgumentException("Use this simulation for motor vehicles; report other road users as conflicts.", nameof(travel));
        var v = new TrafficVehicle(++_lastId, trip, roadSpeedKph, accessSpeedKph, lengthMeters, options);
        v.Traveler = CreateDriver(v, trip);
        _vehicles.Add(v);
        return v;
    }

    public bool TryReplan(TrafficVehicle vehicle, Trip replacement)
    {
        if (!_vehicles.Contains(vehicle) || replacement == null || !replacement.Reachable ||
            vehicle.Arrived || WorldPosition.Distance(vehicle.Position, replacement.From.Access) > 1e-4 ||
            replacement.PlannedNetwork != null && (replacement.PlannedNetwork != _network ||
                replacement.PlannedTopology != _network.TopologyVersion) ||
            replacement.PlannedTravel is { } planned && planned.Vehicle != vehicle.Travel.Vehicle ||
            vehicle.CurrentLane is { } current && !PreservesTravelDirection(current, replacement.DepartureLane))
            return false;
        var driver = CreateDriver(vehicle, replacement);
        var lastLane = vehicle.CurrentLane;
        vehicle.OccupiedLaneAtReplan = lastLane != null && _network.ContainsSegment(lastLane.Segment) ? lastLane : null;
        // Keep a vehicle's unfinished physical junction reservation during a replan.
        // Rebase progress because the replacement cursor starts its own odometer at zero.
        _reservations.RemoveAll(r => r.Vehicle == vehicle && Cleared(r));
        for (int i = 0; i < _reservations.Count; i++)
        {
            var held = _reservations[i];
            if (held.Vehicle == vehicle && vehicle.Traveler is { } previous)
                _reservations[i] = held with
                {
                    EntryDistance = held.EntryDistance - previous.TrafficDistanceTravelledUnits
                };
        }
        vehicle.Trip = replacement;
        vehicle.Traveler = driver;
        vehicle.TrafficHold = TrafficWaitReason.None;
        _observedStops.RemoveWhere(x => x.Item1 == vehicle.Id);
        return true;
    }

    bool PreservesTravelDirection(RoadLane current, RoadLane? replacement)
    {
        if (replacement == null) return false;
        if (current.Segment == replacement.Segment)
            return current.Direction == replacement.Direction;
        if (_network.ContainsSegment(current.Segment))
            return false; // choose the current lane first, then take a legally planned turn
        var a = Heading(current);
        var b = Heading(replacement);
        double dot = a.x * b.x + a.y * b.y;
        double cross = a.x * b.y - a.y * b.x;
        // When split or replaced, a same-point aligned continuation preserves heading.
        return dot > 0 && Math.Abs(cross) <= dot * 1e-5;
    }

    public bool RemoveVehicle(TrafficVehicle vehicle)
    {
        if (!_vehicles.Remove(vehicle)) return false;
        _reservations.RemoveAll(x => x.Vehicle == vehicle);
        _observedStops.RemoveWhere(x => x.Item1 == vehicle.Id);
        return true;
    }

    public void SetUnsignalizedPolicy(RoadNode junction, TrafficJunctionPolicy policy)
    {
        if (!_network.TryGetNode(junction.ID, out var actual) || actual != junction)
            throw new ArgumentException("Use a registered junction", nameof(junction));
        _junctionPolicies[junction.ID] = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    TimedRoadTraveler? CreateDriver(TrafficVehicle vehicle, Trip trip)
    {
        if (!trip.Reachable) return null;
        return new TimedRoadTraveler(_network, trip, ClockSeconds, _metersPerUnit,
            vehicle.RoadSpeedKph, vehicle.AccessSpeedKph, vehicle.Travel,
            (move, time) => EvaluateRightOfWay(vehicle, move, time),
            (move, time) => SignalSnapshot(vehicle, move, time))
        {
            TrafficEnter = (lane, incoming, position) => Enter(vehicle, lane, incoming, position),
            TrafficAdvance = (lane, position, desired) => CapAdvance(vehicle, lane, position, desired)
        };
    }

    public void Tick(double seconds)
    {
        if (!Nonnegative(seconds) || !double.IsFinite(_pending + seconds) ||
            !double.IsFinite(ClockSeconds + _pending + seconds))
            throw new ArgumentOutOfRangeException(nameof(seconds));
        _pending += seconds;
        double epsilon = Math.Min(1e-10, _parameters.StepSeconds * 1e-9);
        while (_pending + epsilon >= _parameters.StepSeconds)
        {
            Step();
            _pending = Math.Max(0, _pending - _parameters.StepSeconds);
        }
    }

    void Step()
    {
        _laneSweeps.Clear();
        _approaches.Clear();
        _reservations.RemoveAll(x => Cleared(x));
        foreach (var v in _vehicles)
        {
            if (v.CurrentLane is { } lane && !v.Arrived)
                TrackLane(v, lane, Station(lane, v.Position));
            if (v.Traveler?.TrafficUpcomingMovement is { } movement &&
                v.CurrentLane == movement.Incoming && !v.Arrived)
                _approaches.Add(new(v, movement, DistanceToLineMeters(movement.Incoming, v.Position)));
        }

        // Stable order for equal-step events. Occupancy holds last through this full step,
        // so the order never authorizes two conflicting entries into the same space.
        foreach (var v in _vehicles)
        {
            v.TrafficHold = TrafficWaitReason.None;
            v.Traveler?.Tick(_parameters.StepSeconds);
        }
        ClockSeconds += _parameters.StepSeconds;
    }

    bool Cleared(Reservation r)
    {
        if (r.Vehicle.Arrived || r.Vehicle.Traveler == null)
            return true;
        double distance = (r.Vehicle.Traveler.TrafficDistanceTravelledUnits - r.EntryDistance) * _metersPerUnit;
        return distance + 1e-6 >= _parameters.JunctionClearanceMeters + r.Vehicle.LengthMeters / 2;
    }

    TrafficWaitReason Enter(TrafficVehicle v, RoadLane lane, RoadLane? incoming, WorldPosition point)
    {
        double station = Station(lane, point);
        foreach (var other in LaneOccupants(lane, v))
        {
            // A vehicle already advanced in this step may have crossed the merge point.
            // Check its swept station range, not only where it ended.
            double gap = Math.Max(0, Math.Max(other.Start - station, station - other.End));
            if (gap * _metersPerUnit < RequiredSeparation(v, other.Vehicle) - 1e-6)
                return v.TrafficHold = incoming == null ? TrafficWaitReason.StartingLaneOccupied : TrafficWaitReason.ExitLaneOccupied;
        }

        if (incoming is { } approach)
        {
            var movement = new SignalMovement(approach, lane);
            foreach (var other in _reservations)
                if (other.Vehicle != v && Conflicts(movement, other.Movement))
                    return v.TrafficHold = TrafficWaitReason.JunctionOccupied;
            _reservations.Add(new(v, movement, v.Traveler!.TrafficDistanceTravelledUnits));
            _observedStops.Remove((v.Id, movement));
        }
        TrackLane(v, lane, station);
        return TrafficWaitReason.None;
    }

    double CapAdvance(TrafficVehicle v, RoadLane lane, WorldPosition point, double desiredUnits)
    {
        if (desiredUnits <= 0) return 0;
        double station = Station(lane, point);
        double allowed = desiredUnits;
        foreach (var other in LaneOccupants(lane, v))
        {
            double difference = other.Start - station;
            if (difference < 0) continue;
            allowed = Math.Min(allowed, Math.Max(0, difference - RequiredSeparation(v, other.Vehicle) / _metersPerUnit));
        }
        if (allowed + 1e-7 < desiredUnits)
            v.TrafficHold = TrafficWaitReason.FollowingVehicle;
        var swept = TrackLane(v, lane, station);
        swept.End = Math.Max(swept.End, station + allowed);
        return allowed;
    }

    LaneOccupancy TrackLane(TrafficVehicle vehicle, RoadLane lane, double station)
    {
        if (!_laneSweeps.TryGetValue(lane, out var list))
            _laneSweeps.Add(lane, list = new());
        foreach (var current in list)
            if (current.Vehicle == vehicle)
            {
                current.Start = Math.Min(current.Start, station);
                current.End = Math.Max(current.End, station);
                return current;
            }
        var entry = new LaneOccupancy(vehicle, station);
        list.Add(entry);
        return entry;
    }

    IEnumerable<LaneOccupancy> LaneOccupants(RoadLane lane, TrafficVehicle except)
    {
        if (_laneSweeps.TryGetValue(lane, out var list))
            foreach (var other in list)
                if (other.Vehicle != except)
                    yield return other;
    }

    double RequiredSeparation(TrafficVehicle a, TrafficVehicle b) =>
        (a.LengthMeters + b.LengthMeters) / 2 + _parameters.MinimumGapMeters;

    bool Conflicts(SignalMovement a, SignalMovement b) => a.Incoming.ToId == b.Incoming.ToId &&
        (a.Incoming == b.Incoming || a.Outgoing == b.Outgoing || _junctionConflicts == null ||
         _junctionConflicts(a, b) || _junctionConflicts(b, a));

    RightOfWayDecision EvaluateRightOfWay(TrafficVehicle v, SignalMovement movement, double time)
    {
        if (!_junctionPolicies.TryGetValue(movement.Incoming.ToId, out var rule))
            return RightOfWayDecision.Proceed;

        bool exitClear = true;
        double startStation = 0;
        foreach (var occupied in LaneOccupants(movement.Outgoing, v))
            if ((occupied.Start - startStation) * _metersPerUnit < RequiredSeparation(v, occupied.Vehicle))
            {
                exitClear = false;
                break;
            }
        var key = (v.Id, movement);
        var conflicts = new List<ConflictingMotorVehicle>();
        foreach (var other in _approaches)
        {
            if (other.Vehicle == v || other.Movement.Incoming == movement.Incoming ||
                !Conflicts(movement, other.Movement))
                continue;
            double eta = other.DistanceMeters / LaneSpeedMetersPerSecond(other.Vehicle, other.Movement.Incoming);
            if (eta > Math.Max(0, time - ClockSeconds) + _parameters.StepSeconds + 1e-6)
                continue;
            conflicts.Add(new ConflictingMotorVehicle(Classify(other.Movement), Relative(movement, other.Movement),
                rule.SignedPriorityBetween?.Invoke(movement, other.Movement) ?? SignedPriority.Unspecified,
                rule.IsCirculating?.Invoke(other.Movement) == true,
                rule.IsEnteringRoundabout?.Invoke(other.Movement) == true));
        }
        var decision = UnsignalizedRightOfWay.Evaluate(new UnsignalizedContext(rule.Control, Classify(movement),
            _observedStops.Contains(key), rule.IsEnteringRoundabout?.Invoke(movement) == true, exitClear,
            rule.IsCirculating?.Invoke(movement) == true), conflicts);
        if (decision == RightOfWayDecision.StopAndLook)
            _observedStops.Add(key);
        return decision;
    }

    SignalConflictSnapshot SignalSnapshot(TrafficVehicle v, SignalMovement movement, double time)
    {
        // A green conflict may have been released between steps. Without a complete
        // pedestrian observer the absence of cars does not prove permission on a circular red.
        foreach (var r in _reservations)
            if (r.Vehicle != v && Conflicts(movement, r.Movement))
                return SignalConflictSnapshot.ConflictPresent;
        foreach (var a in _approaches)
        {
            if (a.Vehicle == v || a.Movement.Incoming == movement.Incoming || !Conflicts(movement, a.Movement))
                continue;

            // Look from the shared-step snapshot at the earliest this other vehicle
            // could reach the junction while this vehicle is still clearing it.
            double subjectSpeed = LaneSpeedMetersPerSecond(v, movement.Outgoing);
            double otherSpeed = LaneSpeedMetersPerSecond(a.Vehicle, a.Movement.Incoming);
            double clearanceTime = (_parameters.JunctionClearanceMeters + v.LengthMeters / 2) / subjectSpeed;
            double eta = a.DistanceMeters / otherSpeed;
            if (eta <= _parameters.StepSeconds + clearanceTime &&
                _network.WaitUntilTurnPermitted(a.Movement.Incoming, a.Movement.Outgoing, time + eta) == 0)
                return SignalConflictSnapshot.ConflictPresent;
        }
        var pedestrian = _pedestrianConflict?.Invoke(movement, time);
        return pedestrian == true ? SignalConflictSnapshot.ConflictPresent :
            pedestrian == false ? SignalConflictSnapshot.Clear : SignalConflictSnapshot.Unknown;
    }

    static double LaneSpeedMetersPerSecond(TrafficVehicle vehicle, RoadLane lane) =>
        Math.Min(vehicle.RoadSpeedKph, lane.EffectivePostedSpeedKph ?? vehicle.RoadSpeedKph) / 3.6;

    static (double x, double y) Heading(RoadLane lane)
    {
        var a = lane.Segment.StartNode.Position;
        var b = lane.Segment.EndNode.Position;
        int sign = lane.Direction == LaneDirection.Forward ? 1 : -1;
        return ((b.x - (double)a.x) * sign, (b.y - (double)a.y) * sign);
    }

    static MotorVehicleMovement Classify(SignalMovement movement)
    {
        var a = Heading(movement.Incoming);
        var b = Heading(movement.Outgoing);
        var cross = a.x * b.y - a.y * b.x;
        var dot = a.x * b.x + a.y * b.y;
        if (Math.Abs(cross) <= Math.Abs(dot) * .25)
            return dot > 0 ? MotorVehicleMovement.Straight : MotorVehicleMovement.Other;
        return cross > 0 ? MotorVehicleMovement.Left : MotorVehicleMovement.Right;
    }

    static RelativeApproach Relative(SignalMovement subject, SignalMovement other)
    {
        var a = Heading(subject.Incoming);
        var b = Heading(other.Incoming);
        var cross = a.x * b.y - a.y * b.x;
        var dot = a.x * b.x + a.y * b.y;
        if (Math.Abs(cross) <= Math.Abs(dot) * .25)
            return dot < 0 ? RelativeApproach.Opposite : RelativeApproach.Unknown;
        // The other approach is on the opposite side from its velocity vector.
        return cross > 0 ? RelativeApproach.Right : RelativeApproach.Left;
    }

    double DistanceToLineMeters(RoadLane lane, WorldPosition position) =>
        Math.Max(0, WorldPosition.Distance(lane.Segment.StartNode.Position, lane.Segment.EndNode.Position) -
            Station(lane, position)) * _metersPerUnit;

    static double Station(RoadLane lane, WorldPosition position)
    {
        var a = lane.Segment.StartNode.Position;
        var b = lane.Segment.EndNode.Position;
        double t = lane.Segment.ClosestT(position);
        return (lane.Direction == LaneDirection.Forward ? t : 1 - t) * WorldPosition.Distance(a, b);
    }

    static bool Nonnegative(double value) => double.IsFinite(value) && value >= 0;
    static bool Positive(double value) => Nonnegative(value) && value > 0;
}
