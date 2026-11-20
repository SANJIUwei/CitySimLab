using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Core;
using global::Godot;

namespace CitySim.Godot;

/// <summary>Godot presentation only. The route positions are produced by CitySim.Core.</summary>
public partial class CityView : Node2D
{
    private static readonly Color Back = C("0a171a");
    private static readonly Color Ground = C("132629");
    private static readonly Color Panel = C("14262a");
    private static readonly Color Border = C("2a4144");
    private static readonly Color Ink = C("f1eee3");
    private static readonly Color Muted = C("9bb0ab");
    private static readonly Color Gold = C("fbd178");
    private static readonly Color Teal = C("77e1c1");
    private static readonly Color Coral = C("ff927f");
    private static readonly Color Blue = C("8acaff");
    private static readonly Font Font = new SystemFont
    {
        FontNames = new[] { "Noto Sans CJK SC", "Microsoft YaHei", "PingFang SC", "WenQuanYi Zen Hei" }
    };

    private const float SearchInterval = .55f;
    private readonly CityDemo _demo = new();
    private readonly List<Vector2> _route = new();
    private readonly List<float> _distances = new();
    private Button _play = null!;
    private Button _step = null!;
    private readonly Dictionary<long, Vector2> _nodePositions = new();
    private readonly Dictionary<long, double> _knownCosts = new();
    private readonly HashSet<long> _confirmed = new();
    private int _searchIndex;
    private float _phaseDelay;
    private bool Searching => _route.Count > 0 && _searchIndex < _demo.SearchSteps.Count;
    private bool Arrived => !Searching && _phaseDelay <= 0 &&
        (_traffic != null ? _traffic.Vehicles.All(vehicle => vehicle.Arrived) : _timed?.State == TimedTravelState.Arrived);
    private int _from;
    private int _to = 1;
    private bool _playing = true;
    private float _distance;
    private float _total;
    private TimedRoadTraveler? _timed;
    private TrafficSimulation? _traffic;
    private TrafficVehicle? _leadTruck;
    private bool IsFreight => _from == CityDemo.FactoryIndex || _to == CityDemo.FactoryIndex;
    private string? _capturePath;
    private int _frames;

    public override void _Ready()
    {
        foreach (var node in _demo.Network.Nodes)
            _nodePositions.Add(node.ID, Map(node.Position));
        _play = AddButton("暂停［空格］", 994, 557, Toggle);
        _step = AddButton("单步寻路［→］", 994, 601, StepOnce);
        AddButton("重新演示［R］", 994, 645, RestartCurrent);
        AddButton("交换起终点", 994, 689, Reverse);
        InstallConstructionControls();
        InstallAccessControls();
        RestartCurrent();
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--smoke")
                CallDeferred(nameof(RunSmoke));
            else if (arg.StartsWith("--capture=", StringComparison.Ordinal))
            {
                _capturePath = arg.Substring("--capture=".Length);
                while (Searching && _searchIndex < 4) AdvanceSearch();
                _playing = false;
                SyncButtons();
            }
        }
    }

    public override void _Process(double delta)
    {
        _frames++;
        if (_playing)
        {
            if (Searching || _phaseDelay > 0)
            {
                _phaseDelay = MathF.Max(0, _phaseDelay - (float)delta);
                if (Searching && _phaseDelay == 0) AdvanceSearch();
            }
            else if (_traffic != null && _leadTruck != null)
            {
                var before = Map(_leadTruck.Position);
                _traffic.Tick(delta);
                _distance = MathF.Min(_total, _distance + before.DistanceTo(Map(_leadTruck.Position)));
                if (Arrived) _distance = _total;
            }
            else if (_timed != null)
            {
                var before = Map(_timed.Position);
                _timed.Tick(delta);
                _distance = MathF.Min(_total, _distance + before.DistanceTo(Map(_timed.Position)));
                if (_timed.State == TimedTravelState.Arrived) _distance = _total;
            }
            if (Arrived || _timed?.State == TimedTravelState.NeedsReplan ||
                _traffic?.Vehicles.Any(vehicle => vehicle.NeedsReplan) == true) _playing = false;
            SyncButtons();
            QueueRedraw();
        }
        if (_capturePath != null && _frames >= 12)
        {
            string path = _capturePath;
            _capturePath = null;
            Capture(path);
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.B) ToggleConstruction();
        else if (key.Keycode == Key.A) ToggleAccess();
        else if (key.Keycode == Key.F) ToggleFreight();
        else if (_accessMode && key.Keycode == Key.Up) CycleAccess(-1);
        else if (_accessMode && key.Keycode == Key.Down) CycleAccess(1);
        else if (_accessMode && key.Keycode == Key.Enter) ApplyAccessSelection();
        else if (_accessMode && key.Keycode == Key.Delete) CloseAccessSelection();
        else if (_accessMode && key.Keycode == Key.Escape) ToggleAccess();
        else if (_constructionMode && key.Keycode == Key.T) CycleTemplate();
        else if (_constructionMode && key.Keycode == Key.L) CycleStructure();
        else if (_constructionMode && key.Keycode == Key.V) CyclePostedLimit();
        else if (_constructionMode && key.Keycode == Key.N) CycleHighwayClass();
        else if (_constructionMode && key.Keycode == Key.Escape) ToggleConstruction();
        else if (key.Keycode == Key.Space) Toggle();
        else if (key.Keycode == Key.R) RestartCurrent();
        else if (key.Keycode == Key.Right && Searching) StepOnce();
        else return;
        GetViewport().SetInputAsHandled();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, 1280, 800), Back);
        Text(42, 39, "CITYSIMLAB   /   城市模拟工作台", 15, Teal);
        Text(40, 85, "城市寻路演示", 42, Ink);
        Text(980, 53, "GODOT 预览  /  03", 17, Ink);
        Text(980, 78, "设计组 · 现代中国大陆", 10, Muted);
        DrawLine(new Vector2(40, 102), new Vector2(1240, 102), Border, 1);
        DrawMap();
        DrawSidebar();
        DrawLine(new Vector2(40, 754), new Vector2(1240, 754), Border, 1);
        Text(42, 778, "城市模拟核心   /   路网 → 寻路 → 移动", 14, Muted);
        Text(979, 778, "03   /   地标寻路", 14, Muted);
    }

    private void DrawMap()
    {
        var outer = new Rect2(40, 118, 914, 618);
        DrawRect(outer, Ground);
        for (int x = 58; x < 950; x += 24)
            for (int y = 137; y < 735; y += 24)
                DrawCircle(new Vector2(x, y), 1, C("284043"));
        DrawRect(outer, Border, false, 1);
        Text(60, 148, "01  /  路网地图", 15, Ink);
        Text(60, 171, Searching ? "路口数字是已走代价，不是直线距离" : "金线是最低代价路线，白点沿路线移动", 13, Muted);
        Text(796, 148, "↑ 北", 19, Ink);

        foreach (var block in _demo.Blocks)
        {
            Vector2 center = Map(block.Position);
            Vector2 size = new Vector2(block.Width, block.Height) * .9f;
            var rect = new Rect2(center - size / 2, size);
            DrawRect(rect, C("1b3633"));
            DrawRect(rect, C("35544b"), false, 1);
            for (int i = 0; i < 4; i++)
            {
                var p = rect.Position + new Vector2((i + .8f) * size.X / 4.4f, size.Y * (i % 2 == 0 ? .35f : .67f));
                DrawCircle(p, 7, C("31594d"));
                DrawCircle(p - new Vector2(1, 2), 4, C("426f5e"));
            }
        }

        foreach (var road in _demo.Network.Segments)
        {
            var a = Map(road.StartNode.Position);
            var b = Map(road.EndNode.Position);
            DrawLine(a, b, C("0b181b"), 24, true);
            DrawLine(a, b, road.Profile.Level > 0 ? C("557a87") :
                road.Profile.UrbanClass == UrbanRoadClass.Arterial ? C("4b6464") : C("384f51"), 18, true);
            if (road.Profile.HasMedian) DrawLine(a, b, Back, 2, true);
        }
        foreach (var road in _demo.Network.Segments)
        {
            var a = Map(road.StartNode.Position);
            var b = Map(road.EndNode.Position);
            bool forward = false, reverse = false;
            foreach (var lane in road.Lanes)
            {
                forward |= lane.Direction == LaneDirection.Forward;
                reverse |= lane.Direction == LaneDirection.Reverse;
            }
            if (forward == reverse)
            {
                Dashed(a, b, C("778983"), 1.2f, 6, 10);
                continue;
            }
            var direction = (forward ? b - a : a - b).Normalized();
            var side = new Vector2(-direction.Y, direction.X);
            for (int i = 1; i <= 3; i++)
            {
                var tip = a.Lerp(b, i / 4f) + direction * 5;
                DrawLine(tip - direction * 9 + side * 4, tip, Teal, 1.7f, true);
                DrawLine(tip - direction * 9 - side * 4, tip, Teal, 1.7f, true);
            }
        }
        foreach (var n in _demo.Network.Nodes)
        {
            Vector2 p = Map(n.Position);
            DrawCircle(p, 10, C("3b5153"));
            DrawCircle(p, 4, C("75918a"));
        }

        if (_demo.SignalJunction is { } signal)
        {
            var location = Map(signal.Position) + new Vector2(11, -11);
            DrawCircle(location, 5, _timed?.State == TimedTravelState.WaitingForGreen ? Coral : Gold);
            Text(location.X + 8, location.Y + 3, "信号", 10, Ink);
        }

        for (int i = 0; i < _demo.Buildings.Count; i++)
        {
            var b = _demo.Buildings[i];
            var building = b.Building;
            bool from = i == _from, to = i == _to;
            var color = from ? Teal : to ? Coral : C("648885");
            Vector2 center = Map(building.Position);
            var size = new Vector2(b.Width, b.Height) * .9f;
            DrawRect(new Rect2(center - size / 2 + new Vector2(4, 5), size), C("0c181b"));
            DrawRect(new Rect2(center - size / 2, size), from ? C("244c47") : to ? C("523b3b") : C("2b3e42"));
            DrawRect(new Rect2(center - size / 2, size), color, false, 1.4f);
            DrawLine(center - new Vector2(size.X * .36f, 0), center + new Vector2(size.X * .36f, 0), color.Darkened(.48f), 1);
            if (building.Attachment is { } attachment)
            {
                var door = Map(building.Entrance);
                var access = Map(attachment.Segment.PointAt(attachment.T));
                Dashed(door, access, color, 1.8f, 3, 4);
                DrawCircle(door, 3, color);
                DrawCircle(access, 3, color);
            }
            Text(center.X - size.X / 2, center.Y - size.Y / 2 - 8, b.Name, 11, color);
        }

        if (Searching) DrawSearchOverlay();
        else for (int i = 1; i < _route.Count; i++)
        {
            DrawLine(_route[i - 1], _route[i], C("272920"), 8, true);
            DrawLine(_route[i - 1], _route[i], Gold, 3, true);
        }
        if (_route.Count > 0)
        {
            Marker(_route[0], "A", Teal);
            Marker(_route[^1], "B", Coral);
            if (!Searching && _phaseDelay <= 0)
            {
                if (_traffic != null)
                {
                    foreach (var vehicle in _traffic.Vehicles)
                    {
                        var p = Map(vehicle.Position);
                        DrawCircle(p, 9, Back);
                        DrawCircle(p, 6, vehicle.HoldReason == TrafficWaitReason.None ? Ink : Coral);
                        Text(p.X + 10, p.Y + 3, $"货车 {vehicle.Id}", 10, Ink);
                    }
                }
                else
                {
                    var p = _timed == null ? PositionAt(_distance) : Map(_timed.Position);
                    DrawCircle(p, 13, C("24251d"));
                    DrawCircle(p, 8, Ink);
                    DrawCircle(p, 4, Gold);
                }
            }
        }
        if (Searching)
        {
            Text(60, 715, "●  本步确认", 14, Gold);
            Text(206, 715, "●  已确认", 14, Teal);
            Text(335, 715, "●  待确认", 14, Blue);
            Text(464, 715, "━  本步更新", 14, Blue);
        }
        else
        {
            Text(60, 715, "●  道路接入点", 14, Muted);
            Text(210, 715, "━  寻路结果", 14, Gold);
            Text(350, 715, "●  移动对象", 14, Ink);
            Text(485, 715, "›  单向道路", 14, Teal);
        }
        var scale = new Vector2(810, 709);
        DrawLine(scale, scale + new Vector2(90, 0), Muted, 1);
        DrawLine(scale, scale + new Vector2(0, -5), Muted, 1);
        DrawLine(scale + new Vector2(90, 0), scale + new Vector2(90, -5), Muted, 1);
        Text(810, 726, "100 地图单位", 10, Muted);
        DrawConstruction();
        DrawAccess();
    }

    private void DrawSearchOverlay()
    {
        var step = _demo.SearchSteps[_searchIndex];
        foreach (var update in step.Improvements)
            DrawLine(_nodePositions[step.NodeId], _nodePositions[update.NodeId], Blue, 2.5f, true);
        foreach (var (id, cost) in _knownCosts)
        {
            var p = _nodePositions[id];
            var color = id == step.NodeId ? Gold : _confirmed.Contains(id) ? Teal : Blue;
            DrawCircle(p, 10, Back);
            DrawCircle(p, 7, color);
            var label = $"#{id} · {cost:0.#}";
            float width = Font.GetStringSize(label, HorizontalAlignment.Left, -1, 13).X + 12;
            DrawRect(new Rect2(p.X + 12, p.Y - 29, width, 21), Back);
            Text(p.X + 18, p.Y - 14, label, 13, color);
        }
        if (_demo.Baseline is { NodeIds.Count: > 0 } baseline)
            DrawArc(_nodePositions[baseline.NodeIds[^1]], 14, 0, Mathf.Tau, 24, Coral, 1.5f, true);
    }

    private void DrawSidebar()
    {
        DrawRect(new Rect2(976, 118, 264, 618), Panel);
        DrawRect(new Rect2(976, 118, 264, 618), Border, false);
        Text(995, 150, "02  /  出行面板", 15, Teal);
        Text(994, 200, "A → B", 36, Ink);
        Text(995, 232, _demo.Buildings[_from].Name + "  →  " + _demo.Buildings[_to].Name, 13, Muted);
        Rule(252);
        Text(995, 279, !_playing && !Arrived ? "出行状态 · 已暂停" : "出行状态", 12, Muted);
        Text(995, 309, _route.Count == 0 ? "无可用路线" : Searching ? "① 逐步寻路" :
            _phaseDelay > 0 ? "② 路线已确定" : Arrived ? "已到达" :
            _timed?.State == TimedTravelState.WaitingForGreen ? "红灯等待" :
            _timed?.State == TimedTravelState.NeedsReplan ? "需重新规划" :
            IsFreight ? "货车正在运输" : "③ 沿路线行进", 23, Gold);
        float progress = Searching ? (_searchIndex + 1f) / _demo.SearchSteps.Count :
            _phaseDelay > 0 ? 1f : _total > 0 ? _distance / _total : 0f;
        DrawRect(new Rect2(996, 328, 224, 5), Border);
        if (progress > 0) DrawRect(new Rect2(996, 328, 224 * progress, 5), Gold);
        Rule(351);
        if (Searching)
        {
            var step = _demo.SearchSteps[_searchIndex];
            Metric(382, "搜索步数", $"{_searchIndex + 1} / {_demo.SearchSteps.Count}");
            Metric(414, "确认路口", $"#{step.NodeId}");
            Metric(446, "已走代价", $"{step.Cost:0.#}");
            Metric(478, "估计总代价", $"{step.Priority:0.#}");
        }
        else if (_demo.Baseline is { } baseline)
        {
            Metric(382, _demo.Traveler.Route?.Global.Algorithm == PathAlgorithm.Alt ? "ALT 搜索步数" : "本次搜索步数", _demo.SearchSteps.Count.ToString());
            Metric(414, "Dijkstra 搜索步数", baseline.SearchSteps.Count.ToString());
            Metric(446, "整趟道路代价", $"{_demo.Traveler.Route?.TotalCost:0.##}");
            if (_traffic != null)
                Metric(478, "等候车辆", _traffic.Vehicles.Count(v => !v.Arrived && v.HoldReason != TrafficWaitReason.None).ToString());
        }
        Rule(491);
        if (_route.Count == 0)
        {
            string reason = _demo.LastFailure switch
            {
                RouteFailure.NoBuildingAccess => "建筑没有可用的车行出入口",
                RouteFailure.DepartureNotAllowed => "没有可用的驶出方向",
                RouteFailure.ArrivalNotAllowed => "没有可用的驶入方向",
                RouteFailure.DirectionNotAllowed => "通行方向不允许",
                RouteFailure.VehicleOrTimeRestricted => "受车型或时段限制",
                RouteFailure.TurnNotAllowed => "必要的转向不允许",
                _ => "没有找到符合道路规则的路线"
            };
            Text(995, 516, reason, 13, Muted);
            return;
        }
        var trip = _demo.Traveler.Route;
        bool usedAlt = trip?.Global.Algorithm == PathAlgorithm.Alt;
        Text(995, 516, trip?.SameSegment == true ? "同段路线 · 无需全局寻路" :
            usedAlt ? "ALT · 地标引导的 A*" : "Dijkstra · 回退寻路", 15, Ink);
        if (trip?.SameSegment != true)
            Text(995, 537, usedAlt ? "优先考虑估计总代价最低的路口" : "优先考虑已走代价最低的路口", 12, Muted);
    }

    private void Metric(float y, string label, string value)
    {
        Text(995, y, label, 12, Muted);
        DrawString(Font, new Vector2(1100, y), value, HorizontalAlignment.Right, 120, 19, Ink);
    }

    private void Rule(float y) =>
        DrawLine(new Vector2(994, y), new Vector2(1221, y), Border, 1);

    private void Marker(Vector2 p, string label, Color color)
    {
        Vector2 chip = p + new Vector2(14, 12);
        DrawCircle(chip, 9, color);
        Text(chip.X - 4, chip.Y + 4, label, 11, Back);
    }

    private static Vector2 Map(WorldPosition p) => new(46 + p.x * .9f, 127 + p.y * .9f);
    private static Color C(string hex) => Color.FromHtml(hex);
    private void Text(float x, float y, string value, int size, Color color) =>
        DrawString(Font, new Vector2(x, y), value, HorizontalAlignment.Left, -1, size, color);

    private void Dashed(Vector2 a, Vector2 b, Color color, float width, float dash, float gap)
    {
        var d = b - a;
        float length = d.Length();
        if (length < .01f) return;
        var unit = d / length;
        for (float t = 0; t < length; t += dash + gap)
            DrawLine(a + unit * t, a + unit * MathF.Min(length, t + dash), color, width, true);
    }

    private Button AddButton(string label, int x, int y, Action pressed)
    {
        var b = new Button { Text = label, Position = new Vector2(x, y), Size = new Vector2(228, 39), FocusMode = Control.FocusModeEnum.None };
        b.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        b.AddThemeFontOverride("font", Font);
        b.AddThemeFontSizeOverride("font_size", 15);
        b.AddThemeColorOverride("font_color", Ink);
        b.AddThemeColorOverride("font_hover_color", Ink);
        b.AddThemeStyleboxOverride("normal", Box(C("203638"), C("375358")));
        b.AddThemeStyleboxOverride("hover", Box(C("2c4749"), Teal));
        b.AddThemeStyleboxOverride("pressed", Box(C("375658"), Teal));
        b.Pressed += pressed;
        AddChild(b);
        return b;
    }

    private static StyleBoxFlat Box(Color background, Color border) => new()
    {
        BgColor = background, BorderColor = border,
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
    };

    private void Reverse()
    {
        (_from, _to) = (_to, _from);
        RestartCurrent();
    }

    private void RestartCurrent()
    {
        bool hasRoute = _demo.Restart(_from, _to);
        _route.Clear();
        _distances.Clear();
        _timed = null;
        _traffic = null;
        _leadTruck = null;
        if (hasRoute && _demo.Traveler.Route is { } trip)
        {
            if (IsFreight)
            {
                _traffic = new TrafficSimulation(_demo.Network, .14, new TrafficParameters(.05, 2, 4));
                foreach (var junction in _demo.Network.Nodes)
                    if (!_demo.Network.HasSignalPlan(junction.ID) && _demo.Network.Segments
                        .Count(road => road.StartNode == junction || road.EndNode == junction) >= 3)
                        _traffic.SetUnsignalizedPolicy(junction, new TrafficJunctionPolicy());
                _leadTruck = _traffic.AddVehicle(trip, 34, 12, lengthMeters: 6);
                _traffic.AddVehicle(trip, 44, 12, lengthMeters: 6);
            }
            else
                _timed = new TimedRoadTraveler(_demo.Network, trip, 0, metersPerUnit: .14, roadSpeedKph: 50, accessSpeedKph: 12);
        }
        _distance = _total = 0;
        foreach (var point in hasRoute ? _demo.Route : Array.Empty<WorldPosition>())
        {
            var v = Map(point);
            if (_route.Count != 0)
                _total += _route[^1].DistanceTo(v);
            _route.Add(v);
            _distances.Add(_total);
        }
        _knownCosts.Clear();
        _confirmed.Clear();
        _searchIndex = 0;
        _phaseDelay = Searching ? SearchInterval : 1.4f;
        if (hasRoute && Searching) ApplySearchStep();
        _playing = hasRoute;
        SyncButtons();
        QueueRedraw();
    }

    private void Toggle()
    {
        if (Arrived)
        {
            RestartCurrent();
            return;
        }
        _playing = !_playing;
        SyncButtons();
        QueueRedraw();
    }

    private void AdvanceSearch()
    {
        _searchIndex++;
        _phaseDelay = Searching ? SearchInterval : 1.4f;
        if (Searching) ApplySearchStep();
    }

    private void ApplySearchStep()
    {
        var step = _demo.SearchSteps[_searchIndex];
        _knownCosts[step.NodeId] = step.Cost;
        _confirmed.Add(step.NodeId);
        foreach (var update in step.Improvements)
            _knownCosts[update.NodeId] = update.Cost;
    }

    private void StepOnce()
    {
        if (!Searching) return;
        _playing = false;
        AdvanceSearch();
        SyncButtons();
        QueueRedraw();
    }

    private void SyncButtons()
    {
        _step.Disabled = !Searching;
        _play.Text = Arrived ? "重播［空格］" : _playing ? "暂停［空格］" : "继续［空格］";
    }

    private Vector2 PositionAt(float distance)
    {
        for (int i = 1; i < _distances.Count; i++)
        {
            float start = _distances[i - 1], end = _distances[i];
            if (distance <= end && end > start)
                return _route[i - 1].Lerp(_route[i], (distance - start) / (end - start));
        }
        return _route[^1];
    }

    private async void Capture(string path)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = GetViewport().GetTexture().GetImage();
        var result = image.SavePng(path);
        GD.Print($"CitySimLab preview capture: {result}");
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }

    private bool RunFreightSmoke()
    {
        var saved = (_from, _to);
        (_from, _to) = (CityDemo.FactoryIndex, CityDemo.WarehouseIndex);
        RestartCurrent();
        bool waited = false;
        if (_traffic != null)
            for (int i = 0; i < 900 && _traffic.Vehicles.Any(v => !v.Arrived); i++)
            {
                _traffic.Tick(.1);
                waited |= _traffic.Vehicles.Any(v => v.HoldReason is TrafficWaitReason.FollowingVehicle or
                    TrafficWaitReason.StartingLaneOccupied or TrafficWaitReason.JunctionOccupied);
            }
        bool done = waited && _traffic?.Vehicles.Count == 2 && _traffic.Vehicles.All(v => v.Arrived);
        (_from, _to) = saved;
        RestartCurrent();
        return done;
    }

    private void RunSmoke()
    {
        bool forward = _route.Count >= 4 && _total > 0 && _demo.Traveler.Route?.Reachable == true;
        Vector2 start = _route.Count == 0 ? default : _route[0];
        Vector2 end = _route.Count == 0 ? default : _route[^1];
        int forwardCount = _route.Count;
        bool motion = forward && PositionAt(_total / 2) != start && PositionAt(_total) == end;
        var timeline = _timed;
        bool signalWaitObserved = false;
        if (timeline != null)
            for (int i = 0; i < 400 && timeline.State != TimedTravelState.Arrived; i++)
            {
                timeline.Tick(.1);
                signalWaitObserved |= timeline.State == TimedTravelState.WaitingForGreen;
            }
        bool timed = timeline?.State == TimedTravelState.Arrived && Map(timeline.Position) == end;
        int altSteps = _demo.SearchSteps.Count;
        int dijkstraSteps = _demo.Baseline?.SearchSteps.Count ?? 0;
        bool equalCost = _demo.BaselineTripCost is { } baselineCost && _demo.Traveler.Route is { } trip &&
            trip.Global.Algorithm == PathAlgorithm.Alt &&
            Math.Abs(baselineCost - trip.TotalCost) < 1e-8;
        StepOnce();
        bool step = !_playing && _searchIndex == 1 && _distance == 0;
        Reverse();
        bool reverse = _route.Count >= 4 && _route[0] == end && _route[^1] == start && Searching && _searchIndex == 0;
        bool access = RunAccessSmoke();
        bool freight = RunFreightSmoke();
        bool construction = RunConstructionSmoke();
        bool valid = forward && motion && reverse && equalCost && step && altSteps > 1 && dijkstraSteps > 1 && access && freight && construction && timed && signalWaitObserved;
        GD.Print($"CitySimLab Godot smoke: forward={forward}; waypoints={forwardCount}; interpolation={motion}; reverse={reverse}; ALT={altSteps}; Dijkstra={dijkstraSteps}; equalCost={equalCost}; step={step}; access={access}; freight={freight}; construction={construction}; timed={timed}; signalWait={signalWaitObserved}");
        GetTree().Quit(valid ? 0 : 1);
    }
}
