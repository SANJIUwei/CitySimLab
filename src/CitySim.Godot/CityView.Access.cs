using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Core;
using global::Godot;

namespace CitySim.Godot;

public partial class CityView
{
    private bool _accessMode;
    private Button _accessButton = null!;
    private Button _freightButton = null!;
    private int _accessBuilding = -1;
    private int _candidateIndex;
    private IReadOnlyList<RoadAccessCandidate> _accessCandidates = Array.Empty<RoadAccessCandidate>();
    private string _accessMessage = "";

    private void InstallAccessControls()
    {
        _accessButton = AddButton("开口［A］", 836, 49, ToggleAccess);
        _accessButton.Size = new Vector2(122, 35);
        _freightButton = AddButton("货运［F］", 1115, 56, ToggleFreight);
        _freightButton.Size = new Vector2(124, 35);
    }

    private void ToggleAccess()
    {
        if (!_accessMode && _constructionMode) ToggleConstruction();
        _accessMode = !_accessMode;
        _accessButton.Text = _accessMode ? "退出［A］" : "开口［A］";
        _accessBuilding = -1;
        _accessMessage = "";
        _accessCandidates = Array.Empty<RoadAccessCandidate>();
        if (_accessMode)
        {
            _playing = false;
            SyncButtons();
        }
        QueueRedraw();
    }

    private void ToggleFreight()
    {
        if (_constructionMode) ToggleConstruction();
        if (_accessMode) ToggleAccess();
        bool freight = _from == CityDemo.FactoryIndex || _to == CityDemo.FactoryIndex;
        (_from, _to) = freight ? (0, 1) : (CityDemo.FactoryIndex, CityDemo.WarehouseIndex);
        RestartCurrent();
    }

    private bool PickAccessBuilding(Vector2 position)
    {
        for (int i = _demo.Buildings.Count - 1; i >= 0; i--)
        {
            var display = _demo.Buildings[i];
            var size = new Vector2(display.Width, display.Height) * .9f + new Vector2(10, 10);
            if (!new Rect2(Map(display.Building.Position) - size / 2, size).HasPoint(position))
                continue;
            SelectAccessBuilding(i);
            return true;
        }
        return false;
    }

    private void SelectAccessBuilding(int i)
    {
        _accessBuilding = i;
        _candidateIndex = 0;
        var b = _demo.Buildings[i].Building;
        // Show eligible candidates in policy order plus near misses; avoid scrolling across the whole map.
        _accessCandidates = b.PreviewAccess(_demo.Network, 85)
            .Where(x => x.IsEligible || x.Distance < 160).ToArray();
        _accessMessage = "";
        QueueRedraw();
    }

    private void CycleAccess(int offset)
    {
        if (_accessCandidates.Count == 0) return;
        _candidateIndex = (_candidateIndex + offset + _accessCandidates.Count) % _accessCandidates.Count;
        _accessMessage = "";
        QueueRedraw();
    }

    private void ApplyAccessSelection()
    {
        if (_accessBuilding < 0 || _accessCandidates.Count == 0) return;
        var candidate = _accessCandidates[_candidateIndex];
        var building = _demo.Buildings[_accessBuilding].Building;
        if (!building.TryAttachTo(_demo.Network, candidate.Segment, candidate.T, out var failure, maxDistance: 85))
        {
            _accessMessage = AccessReason(failure);
            QueueRedraw();
            return;
        }
        SelectAccessBuilding(_accessBuilding);
        _accessMessage = "车辆开口已修改，出行路线会重算。";
        RebuildMapAfterEdit();
    }

    private void CloseAccessSelection()
    {
        if (_accessBuilding < 0) return;
        var building = _demo.Buildings[_accessBuilding].Building;
        building.DetachAccess(_demo.Network);
        _accessMessage = "此建筑的车辆开口已关闭；车辆不可从这里进出。";
        RebuildMapAfterEdit();
    }

    private void DrawAccess()
    {
        if (!_accessMode) return;
        var panel = new Rect2(57, 639, 892, 62);
        if (_accessBuilding >= 0 && _accessCandidates.Count > 0)
        {
            var selected = _demo.Buildings[_accessBuilding].Building;
            var current = _accessCandidates[_candidateIndex];
            var b = Map(current.Segment.PointAt(current.T));
            var tint = current.IsEligible ? Teal : Coral;
            Dashed(Map(selected.VehicleAccessPoint ?? selected.Entrance), b, tint, 3, 5, 5);
            DrawCircle(b, 6, tint);
        }
        DrawRect(panel, Panel);
        DrawRect(panel, Border, false, 1);
        if (_accessBuilding < 0)
        {
            Text(71, 663, "建筑车行开口", 15, Ink);
            Text(71, 687, "点击建筑，查看可接入路段与被排除原因。", 13, Teal);
            return;
        }
        var name = _demo.Buildings[_accessBuilding].Name;
        if (_accessCandidates.Count == 0)
        {
            Text(71, 663, name + " / 无附近候选道路", 15, Ink);
            Text(71, 687, _accessMessage.Length > 0 ? _accessMessage : "没有符合条件的车行开口。", 13, Coral);
            return;
        }
        var candidate = _accessCandidates[_candidateIndex];
        string title = $"{name} / 候选 {_candidateIndex + 1}/{_accessCandidates.Count} · {candidate.Segment.StartNode.ID}→{candidate.Segment.EndNode.ID}";
        Text(71, 663, title + "    ↑↓切换 · Enter 接入 · Delete 关闭", 13, Ink);
        string reason = candidate.IsEligible ? $"可接入 · {DirectionText(candidate.AllowedDirections)}" : AccessReason(candidate.Failure);
        Text(71, 687, _accessMessage.Length > 0 ? _accessMessage : reason, 13, candidate.IsEligible ? Teal : Coral);
    }

    private static string DirectionText(TravelDirection direction) => direction switch
    {
        TravelDirection.Forward => "仅顺向进出",
        TravelDirection.Reverse => "仅反向进出",
        TravelDirection.Both => "双方向均可接入",
        _ => "没有可用方向"
    };

    private static string AccessReason(RoadAccessFailure failure) => failure switch
    {
        RoadAccessFailure.RoadProhibitsAccess => "此类道路不允许地块直接开口。",
        RoadAccessFailure.WrongLevel => "开口与道路不在同一层。",
        RoadAccessFailure.BlockedConnection => "到道路的连接被障碍阻挡。",
        RoadAccessFailure.TooFar => "道路超出开口接入范围。",
        RoadAccessFailure.IntersectionSetback => "开口离路口太近，未满足配置间距。",
        RoadAccessFailure.NoMotorVehicleLane => "此处没有可用机动车道。",
        RoadAccessFailure.MedianOrDirection or RoadAccessFailure.InvalidDirection => "分隔带或车道方向不允许开口。",
        RoadAccessFailure.RoadNotInNetwork => "道路已不在路网中。",
        _ => $"无法接入：{failure}"
    };

    private bool RunAccessSmoke()
    {
        SelectAccessBuilding(CityDemo.FactoryIndex);
        bool eligible = _accessCandidates.Any(x => x.IsEligible);
        _accessBuilding = -1;
        _accessCandidates = Array.Empty<RoadAccessCandidate>();
        return eligible;
    }
}
