# AI Handoff

This document is for AI, not tutorial prose. Keep it compact and update it at the end of each work/home session.

## Current State

- Last updated: 2026-09-16
- Last known location: work
- Root: `E:\Myself\CitySimLab`
- Home root (next session): `E:\code\code home\city`
- Branch: `main`
- Remote: `https://github.com/SANJIUwei/CitySimLab.git`
- Tracking: `main -> origin/main`
- Protocol: `docs/ai/AGENT_PROTOCOL.md`
- Doc style: `docs/ai/DOCUMENTATION_STYLE.md`
- Doc index: `docs/README.md`
- Progress map: `docs/human/路网思维导图.md`
- Current problems: `docs/ai/design-notes/2026-09-11-current-problems.md`

## Project Intent

- Beginner city-simulation project. User plans and supervises; AI implements (2026-09-10).
- Do not ask the user to write code. Do not commit or push unless asked.
- Batch tests per `docs/ai/06-when-to-test.md`. Roles: `docs/human/分工.md`.

## Environment

- Solution: `CitySimLab.slnx`
- Core: `src/CitySim.Core`
- Headless: `src/CitySim.Headless`
- Unity mock: `src/CitySim.UnityMock`
- Tests: `tests/CitySim.Tests`
- Verify: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify.ps1`

## Git Sync Rules

- Git: plain Git, VS Code, or GitHub Desktop.
- Start: `git status --short --branch`. Pull only if the tree is clean.
- End: update this file. Verify if code changed. Commit/push only when asked.
- Work machine GitHub credential is `SANJIUwei`. Home machine `HDyouyita` had 403 write failure before.

## Latest Work (2026-09-16)

Normalized Core before the next gameplay knife.

- Human docs renamed to Chinese filenames.
- Core types live in `namespace CitySim.Core`.
- Folders: `World/` `Roads/` `Routing/` `Compute/` `Movement/`.
- `WorldPosition.Distance` replaces `new World()`.
- Removed obsolete `dedicatedCore` / `ComputeHost`.
- Gameplay unchanged: still waypoint jumps; `_adj` still bidirectional.

## Next Recommended Action

Gameplay next knives, in order:

1. Merge lane direction into graph topology (`_adj` is still bidirectional).
2. Move `Thing` by distance/tick, not waypoint jumps. Clock does not exist yet.

Do not add turning, vehicle types, Unity presentation, or GPU backend wiring unless asked.
## 2026-11-19：Godot 可视化试验

- 云电脑仓库在 `CitySimLab`，基线 `main` / `00fc96f`。新增 `src/CitySim.Godot` 可选预览，不改 Core，不进原解决方案。
- `CityDemo` 用现有 `TripStarter → PathCenter → Thing` 获取路点；`CityView` 仅渲染及插值，不加核心时钟，不改变单行路规则。
- 使用 Godot 4.7.2 .NET 及 .NET SDK 10.0.404。预览编译、正反向/插值冒烟检查、桌面画面及反向按钮、35 个 xUnit 测试、原 Headless 演示均通过。
- 未提交、未推送。下一步先按实际画面确定表现需求，核心的单行路和按时间移动仍待独立设计。

## 2026-11-20：四地标 ALT 成为单目标寻路默认

- `RoadNetwork.FindPath`、`PlanTrip` 和 `PathCenter` 默认用 ALT；显式传 `PathAlgorithm.Dijkstra` 可走原算法。同段路线绕开全局搜索，不改变建筑的道路接入点、通行方向或移动规则。
- `RoadNetwork.Landmarks.cs` 从实际双向邻接表预计算距离，最多四个地标。通过 `AddSegment` 增加道路后会丢弃旧索引，下一次 ALT 查询同步重建；批量铺路后可在模拟计算批次以外用 `PrepareLandmarks()` 预热。单向路还未进入邻接表，引入时需要分别计算往返地标距离。
- Godot 预览保留真实逐步轨迹，默认路线的搜索为 ALT 8 步，对照 Dijkstra 20 步，最低代价一致。对照只在预览运行；普通查询不额外执行 Dijkstra，也不默认采集轨迹。
- Release xUnit 42 项通过。实际 Core 在 1 万、4 万路口的生成路网中共 320 组最低代价与显式 Dijkstra 一致；4 万路口平均确认节点从约 15,101 降到 1,503。不包含实时修路、引擎帧耗时或交通模拟基准。无提交、无推送。
