using System;

namespace CitySim.Core;

public enum UrbanRoadClass { Unspecified, Expressway, Arterial, Collector, Local }
public enum HighwayTechnicalGrade { Unspecified, Motorway, First, Second, Third, Fourth }
public enum HighwayNetworkClass { Unspecified, National, Provincial, County, Township, Village }
public enum RoadRole { Ordinary, Mainline, Frontage, Ramp }
public enum RoadStructure { AtGrade, Bridge, Elevated, Tunnel }
public enum BuildingAccessPolicy { Unspecified, Allowed, Prohibited }

public sealed record RoadProfile(
    UrbanRoadClass UrbanClass = UrbanRoadClass.Unspecified,
    RoadRole Role = RoadRole.Ordinary,
    BuildingAccessPolicy BuildingAccess = BuildingAccessPolicy.Unspecified,
    HighwayTechnicalGrade HighwayGrade = HighwayTechnicalGrade.Unspecified,
    HighwayNetworkClass HighwayNetwork = HighwayNetworkClass.Unspecified,
    RoadStructure Structure = RoadStructure.AtGrade,
    int Level = 0,
    double? DesignSpeedKph = null,
    double? PostedSpeedKph = null,
    double MinimumAccessSetback = 0,
    int AccessPriority = 0,
    bool HasMedian = false)
{
    public VehicleKind AllowedTravelModes =>
        Role != RoadRole.Frontage && (UrbanClass == UrbanRoadClass.Expressway ||
            HighwayGrade == HighwayTechnicalGrade.Motorway || Role == RoadRole.Ramp)
            ? VehicleKind.Car | VehicleKind.Bus | VehicleKind.Truck : VehicleKind.All;

    // 受控主线和匝道不能凭距离接地块。辅路必须按适用地区明确配置。
    public bool AllowsBuildingAccess =>
        Role != RoadRole.Ramp &&
        !(Role != RoadRole.Frontage &&
          (UrbanClass == UrbanRoadClass.Expressway || HighwayGrade == HighwayTechnicalGrade.Motorway)) &&
        (BuildingAccess == BuildingAccessPolicy.Allowed ||
            (BuildingAccess == BuildingAccessPolicy.Unspecified && Role != RoadRole.Frontage));
}

[Flags]
public enum VehicleKind { None = 0, Car = 1, Bus = 2, Truck = 4, Bicycle = 8, Pedestrian = 16, All = 31 }

[Flags]
public enum TravelDirection { None = 0, Forward = 1, Reverse = 2, Both = 3 }

public readonly record struct TravelWindow
{
    public int StartMinute { get; }
    public int EndMinute { get; }

    public TravelWindow(int startMinute, int endMinute)
    {
        if (startMinute < 0 || startMinute >= 1440 || endMinute < 0 || endMinute > 1440 || startMinute == endMinute)
            throw new ArgumentOutOfRangeException(nameof(startMinute), "Use a nonempty daily interval");
        StartMinute = startMinute;
        EndMinute = endMinute;
    }

    public bool Includes(int minute) => StartMinute < EndMinute
        ? minute >= StartMinute && minute < EndMinute
        : StartMinute > EndMinute && (minute >= StartMinute || minute < EndMinute);
}

public readonly record struct TravelOptions(VehicleKind Vehicle, int MinuteOfDay = 0)
{
    public static TravelOptions Default => new(VehicleKind.Car);

    internal void Validate()
    {
        var bits = (int)Vehicle;
        if (bits <= 0 || (Vehicle & ~VehicleKind.All) != 0 || (bits & (bits - 1)) != 0 ||
            MinuteOfDay < 0 || MinuteOfDay >= 1440)
            throw new ArgumentOutOfRangeException(nameof(TravelOptions), "Specify one travel mode and a minute within the day");
    }
}

public enum RouteFailure
{
    None,
    MissingNode,
    NoBuildingAccess,
    DepartureNotAllowed,
    ArrivalNotAllowed,
    NoLegalRoute,
    DirectionNotAllowed,
    VehicleOrTimeRestricted,
    TurnNotAllowed
}
