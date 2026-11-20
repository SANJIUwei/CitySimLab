using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Core;
using global::Godot;

namespace CitySim.Godot;

public partial class CityView
{
    private sealed record BuildAnchor(RoadNode Node, RoadSegment? Tap);
    private static readonly Rect2 BuildArea = new(58, 184, 876, 455);
    private bool _constructionMode;
    private Button _constructionButton = null!;
    private Button _templateButton = null!;
    private int _templateIndex;
    private int _structureOverride;
    private HighwayNetworkClass _highwayClass;
    private int _limitIndex;
    private static readonly double?[] PostedOptions = { null, 30, 40, 50, 60, 80, 100, 120 };
    private IReadOnlyList<RoadBuildTemplate> _templates = Array.Empty<RoadBuildTemplate>();
    private BuildAnchor? _constructionStart, _constructionHover;
    private RoadConstructionPreview? _constructionPreview;
    private string _constructionMessage = "";
    private int _nextRoadNode;
    private RoadBuildTemplate Template => _templates[_templateIndex];
    private int SelectedLevel => _structureOverride == 1 ? 1 : _structureOverride == 2 ? -1 : Template.Profile.Level;

    private void InstallConstructionControls()
    {
        _templates = RoadTemplateCatalog.MainlandChinaModern(new RoadDesignContext(.14));
        if (_templates.Count == 0) throw new InvalidOperationException("A road catalogue is required");
        _nextRoadNode = 101;
        _constructionButton = AddButton("铺路［B］", 475, 49, ToggleConstruction);
        _constructionButton.Size = new Vector2(120, 35);
        _templateButton = AddButton("", 604, 49, CycleTemplate);
        _templateButton.Size = new Vector2(221, 35);
        _templateButton.Visible = false;
        _templateButton.Text = Template.Name + "［T］";
    }

    private void ToggleConstruction()
    {
        if (!_constructionMode && _accessMode) ToggleAccess();
        _constructionMode = !_constructionMode;
        _constructionButton.Text = _constructionMode ? "退出铺路［B］" : "铺路［B］";
        _templateButton.Visible = _constructionMode;
        _templateButton.Text = Template.Name + "［T］";
        _constructionStart = null;
        _constructionHover = null;
        _constructionPreview = null;
        _constructionMessage = "";
        if (_constructionMode)
        {
            _playing = false;
            SyncButtons();
        }
        QueueRedraw();
    }

    private void CycleTemplate()
    {
        _templateIndex = (_templateIndex + 1) % _templates.Count;
        _structureOverride = 0;
        _highwayClass = HighwayNetworkClass.Unspecified;
        _limitIndex = 0;
        _templateButton.Text = Template.Name + "［T］";
        _constructionStart = null;
        _constructionHover = null;
        _constructionMessage = "模板已切换，请重新选择起点。";
        RecheckConstructionPreview();
    }

    private void CycleStructure()
    {
        if (Template.Profile.Role == RoadRole.Ramp) return;
        _structureOverride = (_structureOverride + 1) % 3;
        _constructionStart = null;
        _constructionHover = null;
        _constructionMessage = "层级已切换，请重新选择起点。";
        RecheckConstructionPreview();
    }

    private void CycleHighwayClass()
    {
        if (Template.Profile.HighwayGrade == HighwayTechnicalGrade.Unspecified) return;
        _highwayClass = (HighwayNetworkClass)(((int)_highwayClass + 1) % 6);
        RecheckConstructionPreview();
    }

    private void CyclePostedLimit()
    {
        _limitIndex = (_limitIndex + 1) % PostedOptions.Length;
        RecheckConstructionPreview();
    }

    private RoadConstructionPlan MakeConstructionPlan(BuildAnchor start, BuildAnchor end)
    {
        var plan = Template.MakePlan(start.Node, end.Node, level: SelectedLevel, postedSpeedKph: PostedOptions[_limitIndex]);
        return plan with
        {
            Profile = plan.Profile with
            {
                HighwayNetwork = _highwayClass,
                Structure = _structureOverride switch
                {
                    1 => RoadStructure.Bridge, 2 => RoadStructure.Tunnel, _ => plan.Profile.Structure
                }
            }
        };
    }

    private BuildAnchor? ConstructionAnchorNear(Vector2 pointer, int candidateId)
    {
        if (!BuildArea.HasPoint(pointer)) return null;
        int level = SelectedLevel;
        bool ramp = Template.Profile.Role == RoadRole.Ramp;

        RoadNode? node = null;
        float closest = 13 * 13;
        foreach (var n in _demo.Network.Nodes)
        {
            var touching = _demo.Network.Segments.Where(s => s.StartNode == n || s.EndNode == n);
            if (!ramp && !touching.Any(s => s.Profile.Level == level)) continue;
            float d = Map(n.Position).DistanceSquaredTo(pointer);
            if (d > closest) continue;
            closest = d;
            node = n;
        }
        if (node != null) return new BuildAnchor(node, null);

        var world = new WorldPosition { x = (pointer.X - 46) / .9f, y = (pointer.Y - 127) / .9f,
            z = level * 45f }; // separated graphical deck; not a clearance specification
        RoadSegment? tap = null;
        WorldPosition snapped = world;
        closest = 11 * 11;
        foreach (var road in _demo.Network.Segments)
        {
            if (road.Profile.Level != level && !ramp) continue;
            var flat = world;
            flat.z = road.StartNode.Position.z;
            float t = road.ClosestT(flat);
            if (t <= .001f || t >= .999f) continue;
            var candidate = road.PointAt(t);
            float d = Map(candidate).DistanceSquaredTo(pointer);
            if (d > closest) continue;
            closest = d;
            tap = road;
            snapped = candidate;
        }
        return new BuildAnchor(new RoadNode(snapped, candidateId), tap);
    }

    private RoadConstructionPreview? PreviewBuild(BuildAnchor a, BuildAnchor b)
    {
        var plan = MakeConstructionPlan(a, b);
        if (a.Tap != null && b.Tap != null)
            return PreviewBothTaps(a, b, plan);
        var tap = a.Tap != null ? a : b.Tap != null ? b : null;
        return tap?.Tap is { } road
            ? _demo.Network.PreviewConstructionAtJunction(road, tap.Node, plan, _demo.AllBuildings)
            : _demo.Network.PreviewConstruction(plan);
    }

    private RoadConstructionPreview? PreviewBothTaps(BuildAnchor a, BuildAnchor b, RoadConstructionPlan plan)
        => _demo.Network.PreviewConstructionAcrossJunctions(a.Tap!, b.Tap!, plan, _demo.AllBuildings);

    private bool ApplyBuild(BuildAnchor a, BuildAnchor b, out RoadConstructionPreview? preview)
    {
        var plan = MakeConstructionPlan(a, b);
        if (a.Tap != null && b.Tap != null)
        {
            var connected = _demo.Network.TryConstructAcrossJunctions(a.Tap, b.Tap, plan,
                out _, out _, out _, out var multi, _demo.AllBuildings);
            preview = multi;
            return connected;
        }
        var tap = a.Tap != null ? a : b.Tap != null ? b : null;
        if (tap?.Tap is { } road)
        {
            var applied = _demo.Network.TryConstructAtJunction(road, tap.Node, plan, out _, out _, out var detail, _demo.AllBuildings);
            preview = detail;
            return applied;
        }
        var success = _demo.Network.TryConstruct(plan, out _, out var basic);
        preview = basic;
        return success;
    }

    private void RecheckConstructionPreview()
    {
        _constructionPreview = _constructionStart is { } a && _constructionHover is { } b && a.Node != b.Node
            ? PreviewBuild(a, b) : null;
        QueueRedraw();
    }

    private bool PickConstructionAnchor(BuildAnchor anchor)
    {
        if (_constructionStart == null)
        {
            _constructionStart = anchor;
            if (!_demo.Network.TryGetNode(anchor.Node.ID, out _))
                _nextRoadNode++;
            _constructionHover = null;
            _constructionMessage = "选择终点。路段中点会预览新路口。";
            RecheckConstructionPreview();
            return false;
        }
        if (_constructionStart.Node == anchor.Node) return false;
        if (!ApplyBuild(_constructionStart, anchor, out var preview))
        {
            _constructionHover = anchor;
            _constructionPreview = preview;
            _constructionMessage = preview != null && preview.Issues.Count > 0
                ? ConstructionReason(preview.Issues[0].Kind) : "此两点需要两个路段同时拆分，未修改地图。";
            QueueRedraw();
            return false;
        }
        if (anchor.Node.ID == _nextRoadNode) _nextRoadNode++;
        _constructionStart = null;
        _constructionHover = null;
        _constructionPreview = null;
        _constructionMessage = "路段已铺设，建筑接入和出行已重新核对。";
        RebuildMapAfterEdit();
        return true;
    }

    private void RebuildMapAfterEdit()
    {
        foreach (var junction in _demo.Network.Nodes)
            _nodePositions[junction.ID] = Map(junction.Position);
        _demo.RecheckBuildingAccess();
        RestartCurrent();
        _playing = false;
        SyncButtons();
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } button && _accessMode)
        {
            if (PickAccessBuilding(ToLocal(button.Position))) GetViewport().SetInputAsHandled();
            return;
        }
        if (!_constructionMode) return;
        if (@event is InputEventMouseMotion move)
        {
            _constructionHover = ConstructionAnchorNear(ToLocal(move.Position), _nextRoadNode);
            RecheckConstructionPreview();
        }
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click &&
            ConstructionAnchorNear(ToLocal(click.Position), _nextRoadNode) is { } node)
        {
            PickConstructionAnchor(node);
            GetViewport().SetInputAsHandled();
        }
    }

    private void DrawConstruction()
    {
        if (!_constructionMode) return;
        if (_constructionStart is { } start)
            DrawArc(Map(start.Node.Position), 16, 0, Mathf.Tau, 28, Gold, 2.5f, true);
        if (_constructionHover is { } hover)
            DrawArc(Map(hover.Node.Position), hover.Tap == null ? 11 : 15, 0, Mathf.Tau, 28, Blue, 2, true);
        if (_constructionStart is { } a && _constructionHover is { } b)
        {
            var color = _constructionPreview?.CanBuild == true ? Teal : Coral;
            var p = Map(a.Node.Position);
            var q = Map(b.Node.Position);
            Dashed(p, q, color, 5, 8, 5);
            var delta = (q - p).Normalized();
            var side = new Vector2(-delta.Y, delta.X);
            if (Template.Profile.HasMedian)
                DrawLine(p, q, Back, 1, true);
            var lanes = Template.Lanes;
            for (int i = 0; i < lanes.Count; i++)
            {
                var direction = lanes[i].Direction == LaneDirection.Forward ? delta : -delta;
                var tip = p.Lerp(q, (i + 1f) / (lanes.Count + 1f));
                DrawLine(tip - direction * 8 + side * 4, tip, color, 1.5f, true);
                DrawLine(tip - direction * 8 - side * 4, tip, color, 1.5f, true);
            }
            if (_constructionPreview is { } result)
                foreach (var crossing in result.Crossings)
                {
                    bool separate = crossing.Kind == RoadConstructionCrossingKind.GradeSeparated;
                    DrawCircle(Map(crossing.Position), separate ? 6 : 8, separate ? Blue : color);
                }
        }

        var box = new Rect2(57, 619, 892, 81);
        DrawRect(box, Panel);
        DrawRect(box, Border, false, 1);
        string status = _constructionPreview is { } preview
            ? preview.CanBuild
                ? preview.Crossings.Any(c => c.Kind == RoadConstructionCrossingKind.GradeSeparated)
                    ? "可铺设：不同高程的交叉不互通。"
                    : _constructionStart?.Tap != null || _constructionHover?.Tap != null
                        ? "可铺设：将在道路中间建立实际路口。" : "可铺设：已检查道路连接。"
                : ConstructionReason(preview.Issues[0].Kind)
            : _constructionMessage.Length > 0 ? _constructionMessage : "点空地、现有路口或路段中点。T 切换模板。";
        string design = Template.Profile.DesignSpeedKph is { } kmh ? $" · 设计速度 {kmh:0} km/h（不是限速）" : "";
        int motor = Template.Lanes.Count(l => (l.AllowedVehicles & (VehicleKind.Car | VehicleKind.Truck | VehicleKind.Bus)) != 0);
        Text(71, 641, $"铺路 / {Template.Name} · {motor} 条机动车道 · 层级 {SelectedLevel}{design}", 13, Ink);
        string network = _highwayClass switch
        {
            HighwayNetworkClass.National => "国道", HighwayNetworkClass.Provincial => "省道",
            HighwayNetworkClass.County => "县道", HighwayNetworkClass.Township => "乡道",
            HighwayNetworkClass.Village => "村道", _ => "未设路线归属"
        };
        string posted = PostedOptions[_limitIndex] is { } value ? $"{value:0} km/h" : "未设标志限速";
        Text(71, 663, $"L 结构/层级 · V 标志限速 {posted} · N 公路归属 {network}", 12, Muted);
        Text(71, 687, status, 13, _constructionPreview?.CanBuild == false ? Coral : Teal);
    }

    private static string ConstructionReason(RoadConstructionIssueKind issue) => issue switch
    {
        RoadConstructionIssueKind.ControlledAccessRequiresGradeSeparation => "不可铺设：快速主线不能平交，需实际的立体连接。",
        RoadConstructionIssueKind.JunctionRequiresSharedNode => "不可铺设：平交需要在交点接入真实路口。",
        RoadConstructionIssueKind.OverlappingRoads => "不可铺设：与同层路段重叠。",
        RoadConstructionIssueKind.RampRequiresJunction => "不可铺设：匝道必须接到实际连接点。",
        RoadConstructionIssueKind.ExcessiveGrade => "不可铺设：纵坡超过选定模板的工程界限。",
        RoadConstructionIssueKind.InsufficientGeometricClearance => "不可铺设：道路的高差低于选定规范的最低净空。",
        _ => "不可铺设：端点、拓扑、道路模板或车道配置无效。"
    };

    private void SelectTemplate(Func<RoadBuildTemplate, bool> match)
    {
        for (int i = 0; i < _templates.Count; i++)
        {
            if (!match(_templates[i])) continue;
            _templateIndex = i;
            return;
        }
        throw new InvalidOperationException("The requested test template is not loaded");
    }

    private bool RunConstructionSmoke()
    {
        if (!_demo.Network.TryGetNode(2, out var a) || !_demo.Network.TryGetNode(7, out var b))
            return false;
        int before = _demo.Network.Segments.Count;
        ToggleConstruction();
        var start = new BuildAnchor(a, null);
        var end = new BuildAnchor(b, null);
        SelectTemplate(x => x.Profile.UrbanClass == UrbanRoadClass.Expressway && x.Profile.Level == 0);
        bool blocked = PreviewBuild(start, end)?.CanBuild == false;
        SelectTemplate(x => x.Profile.UrbanClass == UrbanRoadClass.Local && x.Lanes.Any(l => l.Direction == LaneDirection.Reverse));
        PickConstructionAnchor(start);
        _constructionHover = end;
        RecheckConstructionPreview();
        bool preview = _constructionPreview?.CanBuild == true && _demo.Network.Segments.Count == before;
        bool placed = PickConstructionAnchor(end) && _demo.Network.Segments.Count == before + 1;
        bool duplicatesRejected = PreviewBuild(start, end)?.CanBuild == false;
        var connector = _demo.Network.Segments.Single(road => road.StartNode == a && road.EndNode == b);
        var tapPoint = connector.PointAt(.5f);
        var tap = new BuildAnchor(new RoadNode(tapPoint, _nextRoadNode++), connector);
        var freePoint = tapPoint;
        freePoint.x -= 30;
        freePoint.y += 15;
        var free = new BuildAnchor(new RoadNode(freePoint, _nextRoadNode++), null);
        bool tapPreview = PreviewBuild(tap, free)?.CanBuild == true &&
            _demo.Network.ContainsSegment(connector) && !_demo.Network.TryGetNode(tap.Node.ID, out _);
        bool tapPlaced = ApplyBuild(tap, free, out _) && !_demo.Network.ContainsSegment(connector) &&
            _demo.Network.FindPath(a.ID, free.Node.ID).Found;
        RebuildMapAfterEdit();
        ToggleConstruction();
        return blocked && preview && placed && duplicatesRejected && tapPreview && tapPlaced;
    }
}
