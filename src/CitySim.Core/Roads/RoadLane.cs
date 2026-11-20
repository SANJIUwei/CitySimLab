using System;

namespace CitySim.Core;

public enum LaneDirection { Forward, Reverse }

public class RoadLane
{
    public RoadSegment Segment { get; }
    public LaneDirection Direction { get; }
    public VehicleKind AllowedVehicles { get; }
    public TravelWindow? ActiveWindow { get; }
    public double? PostedSpeedKph { get; }
    public double? EffectivePostedSpeedKph => PostedSpeedKph ?? Segment.Profile.PostedSpeedKph;
    public long FromId => (Direction == LaneDirection.Forward ? Segment.StartNode : Segment.EndNode).ID;
    public long ToId => (Direction == LaneDirection.Forward ? Segment.EndNode : Segment.StartNode).ID;
    internal TravelDirection DirectionFlag => Direction == LaneDirection.Forward
        ? TravelDirection.Forward : TravelDirection.Reverse;

    public RoadLane(RoadSegment segment, LaneDirection direction, VehicleKind allowedVehicles = VehicleKind.All,
        TravelWindow? activeWindow = null, double? postedSpeedKph = null)
    {
        if (direction is not (LaneDirection.Forward or LaneDirection.Reverse))
            throw new ArgumentOutOfRangeException(nameof(direction));
        if (allowedVehicles == VehicleKind.None || (allowedVehicles & ~VehicleKind.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(allowedVehicles));
        if (postedSpeedKph is double speed && (!double.IsFinite(speed) || speed <= 0))
            throw new ArgumentOutOfRangeException(nameof(postedSpeedKph));
        Segment = segment;
        Direction = direction;
        AllowedVehicles = allowedVehicles;
        ActiveWindow = activeWindow;
        PostedSpeedKph = postedSpeedKph;
    }

    public bool CanTravel(TravelOptions travel) => (AllowedVehicles & travel.Vehicle) != 0 &&
        (Segment.Profile.AllowedTravelModes & travel.Vehicle) != 0 &&
        (ActiveWindow == null || ActiveWindow.Value.Includes(travel.MinuteOfDay));
}
