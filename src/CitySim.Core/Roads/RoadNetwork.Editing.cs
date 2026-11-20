using System;
using System.Collections.Generic;
using System.Linq;

namespace CitySim.Core;

public enum RoadEditFailure
{
    None, MissingRoad, InvalidJunction, ConflictingNodeId, NotOnRoadInterior,
    IncompatibleJunction, BuildingAccessWouldBeLost, SignalConfigurationRequiresRemap
}

public sealed class RoadSplitResult
{
    public RoadSegment Original { get; }
    public RoadSegment First { get; }
    public RoadSegment Second { get; }
    public RoadNode Junction => First.EndNode;
    public float SplitT { get; }

    internal RoadSplitResult(RoadSegment original, RoadSegment first, RoadSegment second, float splitT)
        => (Original, First, Second, SplitT) = (original, first, second, splitT);

    public RoadAttachment RemapAttachment(RoadAttachment attachment)
    {
        if (!ReferenceEquals(attachment.Segment, Original)) return attachment;
        var first = attachment.T <= SplitT;
        var t = first ? attachment.T / SplitT : (attachment.T - SplitT) / (1 - SplitT);
        return new RoadAttachment(first ? First : Second, Math.Clamp(t, 0, 1),
            attachment.EnterFrom, attachment.ExitToward);
    }
}

public partial class RoadNetwork
{
    public bool TrySplitSegment(RoadSegment road, RoadNode junction, out RoadSplitResult? split,
        out RoadEditFailure failure, IReadOnlyList<Building>? buildings = null)
    {
        var draft = PrepareRoadSplit(road, junction, buildings, out failure);
        split = draft == null ? null : ApplyRoadSplit(draft, buildings);
        return split != null;
    }

    public RoadConstructionPreview PreviewConstructionAtJunction(RoadSegment existing, RoadNode junction,
        RoadConstructionPlan branch, IReadOnlyList<Building>? buildings = null)
    {
        return PreviewConstructionAtJunction(existing, junction, branch, buildings, out _);
    }

    public RoadConstructionPreview PreviewConstructionAtJunction(RoadSegment existing, RoadNode junction,
        RoadConstructionPlan branch, IReadOnlyList<Building>? buildings, out RoadEditFailure failure)
    {
        var draft = PrepareRoadSplit(existing, junction, buildings, out failure);
        return draft == null ? InvalidRoadEditPreview() : PreviewBranch(draft, branch);
    }

    public bool TryConstructAtJunction(RoadSegment existing, RoadNode junction, RoadConstructionPlan branch,
        out RoadSplitResult? split, out RoadSegment? segment, out RoadConstructionPreview preview,
        IReadOnlyList<Building>? buildings = null)
    {
        if (branch == null) throw new ArgumentNullException(nameof(branch));
        if (branch.Lanes != null) branch = branch with { Lanes = branch.Lanes.ToArray() };

        var draft = PrepareRoadSplit(existing, junction, buildings, out _);
        preview = draft == null ? InvalidRoadEditPreview() : PreviewBranch(draft, branch);
        if (draft == null || !preview.CanBuild)
        {
            split = null;
            segment = null;
            return false;
        }
        split = ApplyRoadSplit(draft, buildings);
        segment = AddSegment(branch.Start, branch.End, branch.Cost, branch.Profile);
        foreach (var lane in branch.Lanes!)
            segment.AddLane(lane.Direction, lane.AllowedVehicles, lane.ActiveWindow, lane.PostedSpeedKph);
        return true;
    }

    public RoadConstructionPreview PreviewConstructionAcrossJunctions(RoadSegment firstRoad, RoadSegment secondRoad,
        RoadConstructionPlan branch, IReadOnlyList<Building>? buildings = null)
    {
        return PrepareDoubleTap(firstRoad, secondRoad, branch, buildings, out _, out _);
    }

    public bool TryConstructAcrossJunctions(RoadSegment firstRoad, RoadSegment secondRoad,
        RoadConstructionPlan branch, out RoadSplitResult? firstSplit, out RoadSplitResult? secondSplit,
        out RoadSegment? segment, out RoadConstructionPreview preview, IReadOnlyList<Building>? buildings = null)
    {
        if (branch == null) throw new ArgumentNullException(nameof(branch));
        if (branch.Lanes != null) branch = branch with { Lanes = branch.Lanes.ToArray() };
        preview = PrepareDoubleTap(firstRoad, secondRoad, branch, buildings, out var first, out var second);
        if (!preview.CanBuild || first == null || second == null)
        {
            firstSplit = null;
            secondSplit = null;
            segment = null;
            return false;
        }
        firstSplit = ApplyRoadSplit(first, buildings);
        secondSplit = ApplyRoadSplit(second, buildings);
        segment = AddSegment(branch.Start, branch.End, branch.Cost, branch.Profile);
        foreach (var lane in branch.Lanes!)
            segment.AddLane(lane.Direction, lane.AllowedVehicles, lane.ActiveWindow, lane.PostedSpeedKph);
        return true;
    }

    RoadConstructionPreview PrepareDoubleTap(RoadSegment first, RoadSegment second,
        RoadConstructionPlan branch, IReadOnlyList<Building>? buildings,
        out RoadSplitResult? firstSplit, out RoadSplitResult? secondSplit)
    {
        if (branch == null) throw new ArgumentNullException(nameof(branch));
        firstSplit = null;
        secondSplit = null;
        if (first == second || branch.Start == null || branch.End == null || branch.Start.ID == branch.End.ID)
            return InvalidRoadEditPreview();

        firstSplit = PrepareRoadSplit(first, branch.Start, buildings, out _);
        secondSplit = PrepareRoadSplit(second, branch.End, buildings, out _);
        if (firstSplit == null || secondSplit == null)
            return InvalidRoadEditPreview();
        return PreviewBranch(firstSplit, branch, secondSplit);
    }

    static RoadConstructionPreview InvalidRoadEditPreview() =>
        new(new List<RoadConstructionIssue> { new(RoadConstructionIssueKind.InvalidEndpoint) },
            new List<RoadConstructionCrossing>());

    RoadConstructionPreview PreviewBranch(RoadSplitResult split, RoadConstructionPlan branch, RoadSplitResult? second = null)
    {
        if (branch == null) throw new ArgumentNullException(nameof(branch));
        if (ReferenceEquals(branch.Start, split.Junction) == ReferenceEquals(branch.End, split.Junction))
            return InvalidRoadEditPreview();

        // 隔离几何预演：不移除真实路段，也不触发任何真实路网的订阅。
        var shadow = new RoadNetwork();
        foreach (var entry in _nodesById) shadow._nodesById.Add(entry.Key, entry.Value);
        shadow._nodesById[split.Junction.ID] = split.Junction;
        foreach (var road in Segments)
            if (road != split.Original && road != second?.Original) shadow.Segments.Add(road);
        shadow.Segments.Add(split.First);
        shadow.Segments.Add(split.Second);
        if (second != null)
        {
            shadow._nodesById[second.Junction.ID] = second.Junction;
            shadow.Segments.Add(second.First);
            shadow.Segments.Add(second.Second);
        }
        var preview = shadow.PreviewConstruction(branch);
        if (!preview.CanBuild)
            return preview;
        bool Connects(RoadSplitResult candidate) =>
            preview.Crossings.Any(x => x.JunctionNode == candidate.Junction &&
                (x.ExistingRoad == candidate.First || x.ExistingRoad == candidate.Second));
        if (Connects(split) && (second == null || Connects(second)))
            return preview;
        // 这项操作明确要求接入端点，不能把分层相交误报为修通。
        return new RoadConstructionPreview(
            new List<RoadConstructionIssue> { new(RoadConstructionIssueKind.JunctionRequiresSharedNode) },
            preview.Crossings.ToList());
    }

    RoadSplitResult? PrepareRoadSplit(RoadSegment road, RoadNode junction, IReadOnlyList<Building>? buildings,
        out RoadEditFailure failure)
    {
        failure = RoadEditFailure.MissingRoad;
        if (road == null || !_baseCosts.TryGetValue(road, out var cost)) return null;
        failure = RoadEditFailure.InvalidJunction;
        if (!ValidConstructionEndpoint(junction)) return null;
        failure = RoadEditFailure.ConflictingNodeId;
        if (!UsesRegisteredEndpoint(junction)) return null;
        failure = RoadEditFailure.NotOnRoadInterior;
        var t = road.ClosestT(junction.Position);
        var geometricLength = WorldPosition.Distance(road.StartNode.Position, road.EndNode.Position);
        if (!float.IsFinite(t) || t <= 0 || t >= 1 || !double.IsFinite(geometricLength) || geometricLength == 0 ||
            !double.IsFinite(road.Length) || road.Length <= 0 ||
            WorldPosition.Distance(road.PointAt(t), junction.Position) > Math.Max(1e-7, geometricLength * 1e-6))
            return null;

        // 已有节点可以接入，但不得借分段悄悄连接不同层或绕过受控主线的匝道要求。
        if (_roadsAt.TryGetValue(junction.ID, out var incident))
        {
            failure = RoadEditFailure.IncompatibleJunction;
            foreach (var other in incident)
            {
                bool ramp = other.Profile.Role == RoadRole.Ramp || road.Profile.Role == RoadRole.Ramp;
                if (other.Profile.Level != road.Profile.Level && !ramp ||
                    IsConstructionControlledMainline(other.Profile) != IsConstructionControlledMainline(road.Profile) && !ramp)
                    return null;
            }
        }

        failure = RoadEditFailure.SignalConfigurationRequiresRemap;
        if (HasSignalPlan(junction.ID)) return null;

        var first = new RoadSegment(road.StartNode, junction, cost * t, road.Profile);
        var second = new RoadSegment(junction, road.EndNode, cost - first.Cost, road.Profile);
        var laneMap = CopySplitDimensionsAndLanes(road, first, second, t);
        try
        {
            foreach (var signal in SignalsForSplit(road))
                _ = RemapSplitSignal(signal.Value, road, laneMap);
        }
        catch (ArgumentException) { return null; }

        var split = new RoadSplitResult(road, first, second, t);
        failure = RoadEditFailure.BuildingAccessWouldBeLost;
        if (buildings != null)
        {
            foreach (var building in buildings)
            {
                if (building?.Attachment is not { } old || old.Segment != road) continue;
                var a = split.RemapAttachment(old);
                var shadow = new Building(building.Position, building.Entrance)
                {
                    VehicleAccessPoint = building.VehicleAccessPoint,
                    AccessLevel = building.AccessLevel,
                    AccessConnectionFilter = building.AccessConnectionFilter
                };
                try
                {
                    shadow.AttachTo(a.Segment, a.T, a.EnterFrom, a.ExitToward);
                    if (shadow.Attachment!.EnterFrom != a.EnterFrom || shadow.Attachment.ExitToward != a.ExitToward)
                        return null;
                }
                catch (InvalidOperationException) { return null; }
            }
        }
        failure = RoadEditFailure.None;
        return split;
    }

    RoadSplitResult ApplyRoadSplit(RoadSplitResult draft, IReadOnlyList<Building>? buildings)
    {
        var original = draft.Original;
        var turns = _turns.Where(x =>
            (x.Key.Incoming.Segment == original || x.Key.Outgoing.Segment == original) &&
            x.Key.Incoming.Segment.RoutingLanes.Contains(x.Key.Incoming) &&
            x.Key.Outgoing.Segment.RoutingLanes.Contains(x.Key.Outgoing)).ToArray();
        var signals = SignalsForSplit(original);

        var first = AddSegment(draft.First.StartNode, draft.Junction, draft.First.Cost, original.Profile);
        var second = AddSegment(draft.Junction, draft.Second.EndNode, draft.Second.Cost, original.Profile);
        var laneMap = CopySplitDimensionsAndLanes(original, first, second, draft.SplitT);
        var split = new RoadSplitResult(original, first, second, draft.SplitT);

        RemoveSegment(original);
        foreach (var turn in turns)
            SetTurn(RemapSplitLane(turn.Key.Incoming, true, original, laneMap),
                RemapSplitLane(turn.Key.Outgoing, false, original, laneMap),
                turn.Value.Allowed, turn.Value.Wait);
        foreach (var signal in signals)
            SetSignalPlan(_nodesById[signal.Key], RemapSplitSignal(signal.Value, original, laneMap));

        if (buildings != null)
        {
            foreach (var building in buildings)
            {
                if (building?.Attachment is not { } old || old.Segment != original) continue;
                var a = split.RemapAttachment(old);
                building.ApplyRemappedAttachment(a);
            }
        }
        return split;
    }

    KeyValuePair<long, SignalPlan>[] SignalsForSplit(RoadSegment original) =>
        _signals.Where(x => x.Key == original.StartNode.ID || x.Key == original.EndNode.ID).ToArray();

    static RoadLane RemapSplitLane(RoadLane lane, bool incoming, RoadSegment original,
        Dictionary<RoadLane, (RoadLane First, RoadLane Second)> map)
    {
        if (lane.Segment != original) return lane;
        if (!map.TryGetValue(lane, out var pair))
            throw new ArgumentException("An existing signal references an inactive lane");
        bool useFirst = (lane.Direction == LaneDirection.Forward) != incoming;
        return useFirst ? pair.First : pair.Second;
    }

    SignalPlan RemapSplitSignal(SignalPlan plan, RoadSegment original,
        Dictionary<RoadLane, (RoadLane First, RoadLane Second)> map)
    {
        RoadLane Remap(RoadLane lane, bool incoming)
        {
            if (lane.Segment != original && (!_baseCosts.ContainsKey(lane.Segment) ||
                !lane.Segment.RoutingLanes.Contains(lane)))
                throw new ArgumentException("An existing signal references an inactive lane");
            return RemapSplitLane(lane, incoming, original, map);
        }
        return plan.RemapMovements(m => new SignalMovement(Remap(m.Incoming, true), Remap(m.Outgoing, false)));
    }

    static Dictionary<RoadLane, (RoadLane First, RoadLane Second)> CopySplitDimensionsAndLanes(
        RoadSegment original, RoadSegment first, RoadSegment second, float t)
    {
        first.Length = original.Length * t;
        second.Length = original.Length - first.Length;
        var lanes = new Dictionary<RoadLane, (RoadLane, RoadLane)>();
        foreach (var lane in original.RoutingLanes)
        {
            RoadLane Create(RoadSegment half) => half.AddLane(lane.Direction, lane.AllowedVehicles,
                lane.ActiveWindow, lane.PostedSpeedKph);
            lanes.Add(lane, (Create(first), Create(second)));
        }
        if (lanes.Count == 0)
        {
            // 路段显式关闭时不得因分段而恢复默认双向车道。
            first.RemoveLane(first.AddLane(LaneDirection.Forward));
            second.RemoveLane(second.AddLane(LaneDirection.Forward));
        }
        return lanes;
    }
}
