using System;
using System.Collections.Generic;

namespace CitySim.Core;

public enum TimedTravelState { Moving, WaitingForGreen, WaitingForRightOfWay, Arrived, NeedsReplan }

/// <summary>Drive a prepared trip on a clock; map scale is explicit, not a design assumption.</summary>
public sealed class TimedRoadTraveler
{
    readonly RoadNetwork _network;
    readonly long _networkVersion;
    readonly List<Leg> _legs = new();
    readonly TravelOptions _travel;
    readonly double _metersPerUnit;
    readonly double _roadSpeedKph;
    readonly double _accessSpeedKph;
    readonly Func<SignalMovement, double, RightOfWayDecision>? _rightOfWay;
    readonly Func<SignalMovement, double, SignalConflictSnapshot>? _signalConflicts;
    int _index;
    double _covered;
    bool _enteredLeg;

    readonly record struct Leg(WorldPosition Start, WorldPosition End, RoadLane? Lane, RoadLane? Incoming);

    public WorldPosition Position { get; private set; }
    public double ClockSeconds { get; private set; }
    public TimedTravelState State { get; private set; }
    public RightOfWayDecision? HoldReason { get; private set; }

    internal Func<RoadLane, RoadLane?, WorldPosition, TrafficWaitReason>? TrafficEnter { get; init; }
    internal Func<RoadLane, WorldPosition, double, double>? TrafficAdvance { get; init; }

    internal double TrafficDistanceTravelledUnits { get; private set; }

    internal RoadLane? TrafficLane => _index < _legs.Count
        ? (_enteredLeg ? _legs[_index].Lane : _legs[_index].Incoming)
        : null;

    internal SignalMovement? TrafficUpcomingMovement
    {
        get
        {
            if (_index >= _legs.Count) return null;
            int next = _enteredLeg ? _index + 1 : _index;
            if (next < _legs.Count && _legs[next] is { Incoming: { } incoming, Lane: { } outgoing })
                return new SignalMovement(incoming, outgoing);
            return null;
        }
    }

    public TimedRoadTraveler(RoadNetwork network, Trip trip, double clockSeconds,
        double metersPerUnit, double roadSpeedKph, double accessSpeedKph, TravelOptions? travel = null,
        Func<SignalMovement, double, RightOfWayDecision>? unsignalizedDecision = null,
        Func<SignalMovement, double, SignalConflictSnapshot>? signalConflictSnapshot = null)
    {
        if (!trip.Reachable || trip.PlannedNetwork != null && (trip.PlannedNetwork != network ||
            trip.PlannedTopology != network.TopologyVersion))
            throw new ArgumentException("A trip must be reachable and match the active network revision", nameof(trip));
        if (!double.IsFinite(clockSeconds) || clockSeconds < 0 ||
            !Positive(metersPerUnit) || !Positive(roadSpeedKph) || !Positive(accessSpeedKph))
            throw new ArgumentOutOfRangeException(nameof(clockSeconds));
        _network = network;
        _networkVersion = network.TopologyVersion;
        _travel = travel ?? trip.PlannedTravel ?? TravelOptions.Default;
        _travel.Validate();
        _metersPerUnit = metersPerUnit;
        _roadSpeedKph = roadSpeedKph;
        _accessSpeedKph = accessSpeedKph;
        _rightOfWay = unsignalizedDecision;
        _signalConflicts = signalConflictSnapshot;
        ClockSeconds = clockSeconds;
        Position = trip.From.Access;

        void Add(WorldPosition a, WorldPosition b, RoadLane? lane = null, RoadLane? incoming = null)
        {
            // Keep zero-length road legs: entering a lane or turn can still require permission.
            if (lane != null || WorldPosition.Distance(a, b) > 0)
                _legs.Add(new Leg(a, b, lane, incoming));
        }

        Add(trip.From.Access, trip.FromOnRoad);
        if (trip.SameSegment)
            Add(trip.FromOnRoad, trip.ToOnRoad, trip.DepartureLane);
        else
        {
            if (trip.DepartureLane == null || trip.ArrivalLane == null ||
                trip.Departure.EntryNode == null || trip.Approach.ExitNode == null)
                throw new ArgumentException("Trip must include its chosen road lanes", nameof(trip));
            var incoming = trip.DepartureLane;
            var current = trip.Departure.EntryNode.Position;
            Add(trip.FromOnRoad, current, incoming);
            foreach (var lane in trip.Global.Lanes)
            {
                var next = (lane.Direction == LaneDirection.Forward
                    ? lane.Segment.EndNode : lane.Segment.StartNode).Position;
                Add(current, next, lane, incoming);
                current = next;
                incoming = lane;
            }
            Add(current, trip.ToOnRoad, trip.ArrivalLane, incoming);
        }
        Add(trip.ToOnRoad, trip.To.Access);
        State = _legs.Count == 0 ? TimedTravelState.Arrived : TimedTravelState.Moving;
    }

    static bool Positive(double value) => double.IsFinite(value) && value > 0;

    public void Tick(double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0 || !double.IsFinite(ClockSeconds + deltaSeconds))
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        double horizon = ClockSeconds + deltaSeconds;
        while (State != TimedTravelState.Arrived && State != TimedTravelState.NeedsReplan)
        {
            if (_network.TopologyVersion != _networkVersion)
            {
                State = TimedTravelState.NeedsReplan;
                break;
            }

            var leg = _legs[_index];
            if (!_enteredLeg)
            {
                if (leg.Lane is { } lane)
                {
                    int minute = (int)(ClockSeconds % 86400 / 60);
                    if (!_network.ContainsSegment(lane.Segment) || !lane.CanTravel(new TravelOptions(_travel.Vehicle, minute)))
                    {
                        State = TimedTravelState.NeedsReplan;
                        break;
                    }
                    if (leg.Incoming is { } incoming)
                    {
                        var movement = new SignalMovement(incoming, lane);
                        var conflicts = _signalConflicts?.Invoke(movement, ClockSeconds) ?? SignalConflictSnapshot.Unknown;
                        double? wait = _network.WaitUntilTurnPermitted(incoming, lane, ClockSeconds, conflicts);
                        if (wait == null)
                        {
                            State = TimedTravelState.NeedsReplan;
                            break;
                        }
                        if (!_network.HasSignalPlan(incoming.ToId) && _rightOfWay != null)
                        {
                            var decision = _rightOfWay(movement, ClockSeconds);
                            HoldReason = decision == RightOfWayDecision.Proceed ? null : decision;
                            if (HoldReason != null)
                            {
                                State = TimedTravelState.WaitingForRightOfWay;
                                break;
                            }
                        }
                        if (wait.Value > 0)
                        {
                            State = TimedTravelState.WaitingForGreen;
                            if (ClockSeconds + wait.Value > horizon)
                                break;
                            ClockSeconds += wait.Value;
                            // A restriction can change during the wait; check entry again at actual green.
                            int greenMinute = (int)(ClockSeconds % 86400 / 60);
                            if (!lane.CanTravel(new TravelOptions(_travel.Vehicle, greenMinute)))
                            {
                                State = TimedTravelState.NeedsReplan;
                                break;
                            }
                            var updated = _signalConflicts?.Invoke(movement, ClockSeconds) ?? SignalConflictSnapshot.Unknown;
                            if (_network.WaitUntilTurnPermitted(incoming, lane, ClockSeconds, updated) != 0)
                                break;
                        }
                    }
                    State = TimedTravelState.Moving;
                    if (TrafficEnter != null && TrafficEnter(lane, leg.Incoming, Position) != TrafficWaitReason.None)
                        break;
                }
                _enteredLeg = true;
            }

            HoldReason = null;
            State = TimedTravelState.Moving;
            double distance = WorldPosition.Distance(leg.Start, leg.End);
            double speed = (leg.Lane == null ? _accessSpeedKph
                : Math.Min(_roadSpeedKph, leg.Lane.EffectivePostedSpeedKph ?? _roadSpeedKph)) / 3.6 / _metersPerUnit;
            if (!double.IsFinite(distance) || !Positive(speed))
            {
                State = TimedTravelState.NeedsReplan;
                break;
            }
            double remaining = Math.Max(0, distance - _covered);
            double remainingTime = remaining / speed;
            double desired = Math.Min(remaining, Math.Max(0, (horizon - ClockSeconds) * speed));
            double permitted = leg.Lane != null && TrafficAdvance != null
                ? Math.Clamp(TrafficAdvance(leg.Lane, Position, desired), 0, desired)
                : desired;
            if (permitted < desired || remainingTime > horizon - ClockSeconds)
            {
                _covered += permitted;
                TrafficDistanceTravelledUnits += permitted;
                if (distance > 0) Position = Interpolate(leg.Start, leg.End, _covered / distance);
                break;
            }
            ClockSeconds += remainingTime;
            TrafficDistanceTravelledUnits += remaining;
            Position = leg.End;
            _index++;
            _covered = 0;
            _enteredLeg = false;
            if (_index == _legs.Count)
                State = TimedTravelState.Arrived;
        }
        ClockSeconds = horizon;
    }

    static WorldPosition Interpolate(WorldPosition a, WorldPosition b, double fraction) => new()
    {
        x = (float)(a.x + (b.x - a.x) * fraction),
        y = (float)(a.y + (b.y - a.y) * fraction),
        z = (float)(a.z + (b.z - a.z) * fraction)
    };
}
