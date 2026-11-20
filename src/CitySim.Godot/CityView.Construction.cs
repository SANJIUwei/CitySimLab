using System;
using System.Linq;
using CitySim.Core;
using global::Godot;

namespace CitySim.Godot;

public partial class CityView
{
    private enum BuildTemplate { TwoWayStreet, OneWayStreet, Expressway, ElevatedExpressway }

    private bool _constructionMode;
    private Button _constructionButton = null!;
    private Button _templateButton = null!;
    private BuildTemplate _template;
    private RoadNode? _constructionStart;
    private RoadNode? _constructionHover;
    private RoadConstructionPreview? _constructionPreview;
    private string _constructionMessage = "";

    private void InstallConstructionControls()
    {
        _constructionButton = AddButton("铺路预览［B］", 485, 49, ToggleConstruction);
        _constructionButton.Size = new Vector2(144, 35);
        _templateButton = AddButton("", 651, 49, CycleTemplate);
        _templateButton.Size = new Vector2(215, 35);
        _templateButton.Visible = false;
    }

    private void ToggleConstruction()
    {
        _constructionMode = !_constructionMode;
        _constructionButton.Text = _constructionMode ? "退出铺路［B］" : "铺路预览［B］";
        _templateButton.Visible = _constructionMode;
        _templateButton.Text = TemplateTitle() + "［T 切换］";
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
        _template = (BuildTemplate)(((int)_template + 1) % 4);
        _templateButton.Text = TemplateTitle() + "［T 切换］";
        RecheckConstructionPreview();
    }

    private string TemplateTitle() => _template switch
    {
        BuildTemplate.OneWayStreet => "单向支路",
        BuildTemplate.Expressway => "地面快速路",
        BuildTemplate.ElevatedExpressway => "高架快速路",
        _ => "双向支路"
    };

    private RoadConstructionPlan MakeConstructionPlan(RoadNode start, RoadNode end)
    {
        bool controlled = _template is BuildTemplate.Expressway or BuildTemplate.ElevatedExpressway;
        bool elevated = _template == BuildTemplate.ElevatedExpressway;
        if (elevated)
        {
            start = ElevatedEndpoint(start);
            end = ElevatedEndpoint(end);
        }
        var profile = new RoadProfile(
            UrbanClass: controlled ? UrbanRoadClass.Expressway : UrbanRoadClass.Local,
            Role: controlled ? RoadRole.Mainline : RoadRole.Ordinary,
            Structure: elevated ? RoadStructure.Elevated : RoadStructure.AtGrade,
            Level: elevated ? 1 : 0, HasMedian: controlled);
        var modes = controlled ? VehicleKind.Car | VehicleKind.Bus | VehicleKind.Truck : VehicleKind.All;
        var lanes = _template == BuildTemplate.OneWayStreet
            ? new[] { new RoadConstructionLane(LaneDirection.Forward, modes) }
            : new[] { new RoadConstructionLane(LaneDirection.Forward, modes), new RoadConstructionLane(LaneDirection.Reverse, modes) };
        // Only a logical template: it does not prescribe an engineering cross-section or lane width.
        return new RoadConstructionPlan(start, end, profile, WorldPosition.Distance(start.Position, end.Position), lanes);
    }

    // A crossing deck has distinct node identities even when its plan-view positions coincide.
    private RoadNode ElevatedEndpoint(RoadNode ground)
    {
        long id = 1_000_000 + ground.ID;
        if (_demo.Network.TryGetNode(id, out var existing)) return existing;
        var position = ground.Position;
        position.z += 1; // schematic deck level, not physical clearance
        return new RoadNode(position, id);
    }

    private RoadNode? ConstructionNodeNear(Vector2 pointer)
    {
        RoadNode? hit = null;
        float closest = 18f * 18f;
        foreach (var node in _demo.Network.Nodes)
        {
            if (node.ID >= 1_000_000) continue; // pick by the ground-level diagram
            float dist = Map(node.Position).DistanceSquaredTo(pointer);
            if (dist > closest)
                continue;
            closest = dist;
            hit = node;
        }
        return hit;
    }

    private void RecheckConstructionPreview()
    {
        _constructionPreview = _constructionStart is { } a && _constructionHover is { } b && a != b
            ? _demo.Network.PreviewConstruction(MakeConstructionPlan(a, b)) : null;
        QueueRedraw();
    }

    private bool PickConstructionNode(RoadNode node)
    {
        if (_constructionStart == null)
        {
            _constructionStart = node;
            _constructionHover = node;
            _constructionMessage = "选择终点，绿色可建、红色不可建。";
            RecheckConstructionPreview();
            return false;
        }
        if (_constructionStart == node)
            return false;

        var plan = MakeConstructionPlan(_constructionStart, node);
        if (!_demo.Network.TryConstruct(plan, out _, out var preview))
        {
            _constructionHover = node;
            _constructionPreview = preview;
            _constructionMessage = ConstructionReason(preview.Issues[0].Kind);
            QueueRedraw();
            return false;
        }

        _constructionStart = null;
        _constructionHover = null;
        _constructionPreview = null;
        _constructionMessage = _demo.Network.HasSignalPlan(plan.Start.ID) || _demo.Network.HasSignalPlan(plan.End.ID)
            ? "路段已铺设；新转向须配置放行相位才能通行。"
            : "路段已铺设；原出行按新路网重新计算。";
        foreach (var junction in _demo.Network.Nodes)
            _nodePositions[junction.ID] = Map(junction.Position);
        RestartCurrent();
        _playing = false;
        SyncButtons();
        QueueRedraw();
        return true;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_constructionMode)
            return;
        if (@event is InputEventMouseMotion move)
        {
            var hover = ConstructionNodeNear(ToLocal(move.Position));
            if (_constructionHover != hover)
            {
                _constructionHover = hover;
                _constructionMessage = "";
                RecheckConstructionPreview();
            }
        }
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click &&
            ConstructionNodeNear(ToLocal(click.Position)) is { } node)
        {
            PickConstructionNode(node);
            GetViewport().SetInputAsHandled();
        }
    }

    private void DrawConstruction()
    {
        if (!_constructionMode)
            return;

        if (_constructionStart is { } start)
            DrawArc(Map(start.Position), 17, 0, Mathf.Tau, 28, Gold, 2.5f, true);
        if (_constructionHover is { } hover)
            DrawArc(Map(hover.Position), 13, 0, Mathf.Tau, 28, Blue, 2, true);
        if (_constructionPreview is { } result && _constructionStart is { } a && _constructionHover is { } b)
        {
            var color = result.CanBuild ? Teal : Coral;
            Dashed(Map(a.Position), Map(b.Position), color, 4, 8, 5);
            foreach (var crossing in result.Crossings)
            {
                bool separate = crossing.Kind == RoadConstructionCrossingKind.GradeSeparated;
                DrawCircle(Map(crossing.Position), separate ? 6 : 8, separate ? Blue : color);
            }
        }

        var box = new Rect2(57, 647, 670, 50);
        DrawRect(box, Panel);
        DrawRect(box, Border, false, 1);
        string status = _constructionPreview is { } preview
            ? preview.CanBuild
                ? preview.Crossings.Any(c => c.Kind == RoadConstructionCrossingKind.GradeSeparated)
                    ? "可铺设：不同层的道路跨越不互通。" : "可铺设：只在实际共用的端点连接。"
                : ConstructionReason(preview.Issues[0].Kind)
            : _constructionMessage.Length > 0 ? _constructionMessage : "点击两个现有路口，先选起点；T 切换模板。";
        Text(71, 669, "铺路  /  " + TemplateTitle(), 13, Ink);
        Text(71, 689, status, 13, _constructionPreview?.CanBuild == false ? Coral : Teal);
    }

    private static string ConstructionReason(RoadConstructionIssueKind issue) => issue switch
    {
        RoadConstructionIssueKind.ControlledAccessRequiresGradeSeparation => "不可铺设：快速路不能与普通路平交，请使用立体跨越。",
        RoadConstructionIssueKind.JunctionRequiresSharedNode => "不可铺设：平交处必须有真正的路口，不能仅让线条相交。",
        RoadConstructionIssueKind.OverlappingRoads => "不可铺设：与同层路段重叠。",
        RoadConstructionIssueKind.RampRequiresJunction => "不可铺设：匝道必须有实际连接点。",
        _ => "不可铺设：端点、道路模板或车道配置无效。"
    };

    private bool RunConstructionSmoke()
    {
        if (!_demo.Network.TryGetNode(2, out var a) || !_demo.Network.TryGetNode(7, out var b))
            return false;
        int before = _demo.Network.Segments.Count;
        ToggleConstruction();
        _template = BuildTemplate.Expressway;
        bool blocked = !_demo.Network.PreviewConstruction(MakeConstructionPlan(a, b)).CanBuild;
        _template = BuildTemplate.TwoWayStreet;
        PickConstructionNode(a);
        _constructionHover = b;
        RecheckConstructionPreview();
        bool preview = _constructionPreview?.CanBuild == true && _demo.Network.Segments.Count == before;
        bool placed = PickConstructionNode(b) && _demo.Network.Segments.Count == before + 1;
        bool duplicatesRejected = !_demo.Network.PreviewConstruction(MakeConstructionPlan(a, b)).CanBuild;
        ToggleConstruction();
        return blocked && preview && placed && duplicatesRejected;
    }
}
