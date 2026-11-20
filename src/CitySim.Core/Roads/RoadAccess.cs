namespace CitySim.Core;

/// <summary>First applicable rejection reason for a specific proposed site-to-road connection.</summary>
public enum RoadAccessFailure
{
    None,
    RoadNotInNetwork,
    RoadProhibitsAccess,
    WrongLevel,
    IntersectionSetback,
    NoMotorVehicleLane,
    MedianOrDirection,
    TooFar,
    BlockedConnection,
    InvalidPosition,
    InvalidDirection
}

public sealed record RoadAccessCandidate(
    RoadSegment Segment,
    float T,
    WorldPosition RoadPoint,
    double Distance,
    TravelDirection AllowedDirections,
    RoadAccessFailure Failure)
{
    public bool IsEligible => Failure == RoadAccessFailure.None;
}

public partial class RoadNetwork
{
    // Access edits can invalidate a route even when the lane graph itself is unchanged.
    internal void NotifyBuildingAccessChanged() => TopologyVersion++;
}
