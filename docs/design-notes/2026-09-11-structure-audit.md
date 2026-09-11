# 结构排查（2026-09-11）

阅读对象：用户监督 + 后续 AI

## 已改

- 建筑不再 `IssueTo`。出行走 `TripStarter.Request`。
- `Trip` 两端是 `RouteEnd`。
- `PathCenter`：收请求、排队。`ComputeScheduler` 管中心。
- 点对点寻路也进中心队列。
- 不绑死调度核。

职责：

- `Building`：地点 + 门口 + 路上缓存
- `TripStarter`：要不要出发
- `PathCenter`：寻路请求队列
- 寻路作业：只是计算
- `ComputeScheduler`：管中心、分算力
- `RoadNetwork`：图和 PlanTrip
- `Thing`：收路线、跳点

## 还不合理、先不动

1. 车道和 `_adj` 两套拓扑
2. `AttachNearest` 扫全段、无最大距离
3. 路改了 Attachment 不失效
4. Core 多数类型没有 namespace
5. `World` 既当坐标盒子又当距离计算器
6. `Thing.Advance` 一次跳一个路点
