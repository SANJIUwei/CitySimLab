# 架构笔记

长期学习型项目。核心规则在纯 C# 里，不引用 Unity。

## 模块边界

- `CitySim.Core`：规则。坐标、路网、建筑、出行、调度。
- `CitySim.Headless`：没有 Unity 时的运行入口。
- `CitySim.UnityMock`：少量生命周期模拟。
- `CitySim.Tests`：验证规则。
- `docs`：为什么这样写。进度图见 `docs/路网思维导图.md`。

## 当前逻辑分层

```
地点：Building / RouteEnd
发起：TripStarter
寻路中心：PathCenter（收请求、排队）
计算作业：PathSearchJob / NodeSearchJob（不属于任何人）
算力：ComputeScheduler（管中心、分预算）
图：RoadNetwork（Node / Segment / Lane，PlanTrip / FindPath）
走路：Thing（收 Trip，Advance 跳点）
```

全局图不当建筑节点。建筑挂在 `segment + t` 上。

## 简单原则

- 先写纯 C#，再想 Unity 表现。
- 先写小规则，再写大系统。
- 先能验证，再谈真实感。

## 暂时不做

- 不做真实 Unity 工程。
- 不做转向、标志、画面。
- 不把建筑塞进全局图，不给每条车道再造 Node。
