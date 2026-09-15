# 工程协议

长期学习型城市模拟。目标不是堆功能，而是在可扩展、低耦合、可验证的基础上推进。

完整优先级：

```text
可扩展性 > 解耦 > 正确性 > 可验证性 > 性能优化 > 开发便利性
```

目标：一个模块变化时，无关模块不必一起改。

## 角色

用户：产品方向、系统目标、重要设计决策、最终取舍。

AI：架构分析、模块边界、稳定接口、实现、测试、验证、文档、技术债检查、进度导航。

用户没点名某项基础设施，不代表不需要。目标依赖缺失能力时，先指出依赖。

## 讨论与执行

分析默认不改仓库：「看看」「分析一下」「你觉得呢」「研究一下」「给建议」「解释一下」。

只有明确要求才改：「实现」「修改」「重构」「修复」「加上」「删除」「提交」「推送」。

不要擅自提交或推送。

## 模块与接口

模块边界优先于类边界。寻路是模块，不是 `Pathfinder.cs`。

优先抽象稳定概念：`RouteRequest`、`RouteResult`、`IPathSolver`、`IComputeBackend`。不要为当前实现造接口。

接口存在的理由至少一条：多种实现、明确会替换、隔离依赖、测试需要、运行时切换后端。否则用普通类型。

## 依赖方向

```text
Domain → Application / Service → Infrastructure
```

单向。禁止底层反向依赖玩法。

- Core 不依赖 Unity
- 图数据不依赖 Vehicle
- 寻路算法不依赖 `Thing`
- Scheduler 不知道 Dijkstra / A*
- Backend 不知道建筑、居民、车辆
- Entity 不掌握线程、GPU、Scheduler

## 计算分层

```text
业务请求
    ↓
IComputeCenter
    ↓
ComputeScheduler
    ↓
IComputeBackend
    ↓
Result / Apply
```

业务对象不创建线程、不决定线程数、不 Dispatch/等待 GPU、不管线程池、不定全系统预算。

GPU 不是免费 CPU。必须同时看 Dispatch、Upload、Readback、同步、批大小、渲染预算、帧时、模拟延迟。Kernel 快不等于端到端优。模拟可排队、延迟、分批；不要轻易牺牲帧稳定。

## 重构

必须能回答：拆开了哪个职责、为什么会分别变化、哪个接口该稳定、哪个实现可替换、第二种实现要不要改已有核心。

不为看起来高级而加 interface / factory / manager。无法降低未来变化成本的重构不做。

## 做完一块后检查

- God Class、循环依赖、职责泄漏、不该知道的知识、实现细节漏到业务层、隐藏全局状态
- 第二种实现要改多少上层代码（CPU→GPU、Dijkstra→A*、Vehicle→Citizen）
- 是否有最小可验证边界
- 多余复制、分配、锁、主线程阻塞、全图扫描

## 主动提醒

| 情况 | 例子 |
|---|---|
| 架构债 | Traffic 直接依赖 GPU Backend |
| 缺基础 | 没有 Simulation Clock 就做按帧移动 |
| 职责膨胀 | `RoadNetwork` 同时管数据、拓扑、搜索、Trip |
| 重复抽象 | 已有 Compute 合同还再造一套线程抽象 |
| 阶段不对 | 拓扑未稳就做高级 Traffic AI |

提醒，不替用户做战略决定。

## Benchmark

区分 Algorithm / Backend / Transfer / Sync / End-to-End / Frame Impact。

实时系统最终看：Frame Time、FPS 稳定、吞吐量、延迟、队列积压、P95/P99、内存、CPU/GPU 利用率。

单项更快，不能宣布整体更优。禁止伪造数字，禁止声称跑过没跑的命令。

## 任务开始

读 `AGENTS.md`、`docs/ai/AI_HANDOFF.md`、相关设计文档、当前代码。以仓库真实状态为准。

```text
当前状态 → 用户目标 → 缺失依赖 → 影响模块 → 最小合理修改
```

改代码时：小范围、守边界、不顺手重构无关处、不为过测试而改期望、不隐藏失败。

完成后说明：改了什么、为什么、验证了什么、还剩什么、下一步建议。

卡住时先回答：系统在哪、目标是什么、缺什么基础、职责该在谁、谁不该知道、接口稳在哪、这次让未来哪个变化更容易。

## 长期系统图

```text
Simulation: World / Road / Pathfinding / Traffic / Population / Economy / Services / Persistence / Presentation
Infrastructure: Scheduler / CPU Backend / GPU Backend / Tick / Memory / Benchmark / Diagnostics
```

模块可以变。稳定的是职责边界。
