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

## 2026-11-20：道路设计资料归档

- 云电脑的 `CitySimLab/docs/ai/road-research/README.md` 是道路研究入口，从 `docs/README.md` 也可进入。按用户要求持续归档其提供的资料，区分官方现实规则、项目既有决定和提议。
- 首批为中国大陆城市道路与公路的分类、通行法规、标志标线。下一步先讨论路段方向与建筑出入口，未授权实现。地形研究仍搁置。
- 四地标 ALT 的 `fb89b15` 已验证在 GitHub `main`；本轮只改本地文档，未提交、未推送、未改代码。

- 补查建筑机动车开口与辅路见 `docs/ai/road-research/building-access.md`。保留现实适用范围与游戏候选的区别，未改代码。

- 道路分类字典、规则条目库已落盘，从研究索引进入。评审稿为 `docs/human/道路系统策划案.md`；设计依据现实，仍待选择游戏适用地区与年代。未改代码、未提交或推送。

## 2026-11-20：道路系统策划案接入 Core

- 云电脑 `CitySimLab` 的 `main` 工作区。未提交、未推送；保留先前本地道路资料库与策划案。
- `RoadRules.cs` 分开记录城市功能、公路技术等级、路网地位、道路角色和结构；`RoadLane` 支持方向、车型和按查询时刻生效的通行窗口。显式车道移除至零时关闭，不恢复成虚拟双向车道。
- `RoadNetwork.Rules.cs` / `RoadNetwork.LegalSearch.cs`：按路口与来向车道搜索，区分禁转与等待代价，检查快速路连接和道路层级；ALT 只用乐观双向放松图做下界，仍从合法图构建路线。完整出行比较两端的合法接入和同段直达/绕行，不再强行贴近路段端点。
- `Building` 保留 `segment + t`，分离楼门与车行开口，筛接入层级、权限、间距和可通行方向，配置所属政策。道路/车道/转向改动会使已准备的行程停下并标记需重规划。Godot 演示有单向箭头和不可达原因。
- 验证：Linux 无需 PowerShell，使用等价的 Release build / test / Headless；54 项通过，单行顺向通过、逆向失败。Godot build 零警告零错误，`--smoke` 通过：正反出行及逐步回放正常；同一完整出行 ALT 6 步、Dijkstra 30 步、代价一致。这不是大路网性能结论。
- 未包含城市地区/年代定案及成套实测断面模板、玩家铺路编辑器、连续驾驶、实时信号相位、车流冲突/让行、跟车或沿行程递进的时间窗口。当前是查询时刻判定与等待成本，不模拟信号真实放行时刻。道路资料引用与研究范围见资料库。

## 2026-11-20：铺路预览、信号相位与单车执行

- `RoadNetwork.Construction.cs` 验证候选端点、车道、实际共用路口、同层重叠、快速路平交与不同层跨越；预览不改图，正式铺设前会再次验证。Godot 按 B 开铺路，T 选样式，选两个现有路口预览并落路。高架使用不同的节点身份。
- `SignalPlan` 维护真实循环相位和已配置的冲突对，转向有未来绿灯时仍合法。`TimedRoadTraveler` 按配置速度及实际时刻行驶，红灯停在路口，绿灯后进入，车道时段关停或地图变更会要求重规划。路由仍不把任意权重当秒。
- `UnsignalizedRightOfWay` 按明确提供的冲突快照判定让行，可接入时间游标的回调。未接真实多车冲突探测、拥堵和统一城市时钟，不宣称完整交通模拟。
- 验证：Release 编译无警告无错误，xUnit 80/80，Headless 单行烟测通过。Godot 编译通过，headless 烟测验证铺路校验、分步寻路、红灯停等、恢复并到达；同一请求 ALT 6 步、Dijkstra 30 步、代价一致。代码和文档留在云电脑，未提交、未推送。

## 2026-11-20：整份道路策划案的功能验收

- 基于本地提交 `624cf27`，本轮改动仍在云电脑工作区，未提交、未推送。
- 新增现代中国大陆 13 个参考分类模板与可选济南参考，三条分类轴与标志限速独立；必要几何条件通过施工预览验证。施工支持空地、实际节点、单端与双端中段接入，分段保留车道、信号、转向和合法建筑开口。
- 确认开口拒绝原因与手动变更；失效开口不能被排队中的旧请求复用。多车统一步进，提供排队、出口堵塞、路口冲突占用、真实信号周期及显式冲突来源。圆红放行只在配置且确认无冲突时成立。
- 逐项操作、测试与边界见 `docs/human/道路系统验收清单.md`。最终验证：Release 构建、114/114 测试、Headless、Godot 构建及冒烟均通过；Godot 验证铺路/中段接入、开口、双货车排队到达、红灯停等和 ALT 代价等价。
- 最终地图地区和年代未拍板。工程详图、行人自治、换道物理、全城性能、地形、经济与贸易不在本次验收范围内，勿理解为完整城市成品。
