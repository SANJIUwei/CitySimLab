# 寻路流水线

日期：2026-09-10，2026-09-11 按现码更新
状态：设计记录

## 权属

- 寻路计算是作业，不属于建筑或载具。
- `PathCenter`：收请求、组织排队。被 `ComputeScheduler.Manage`。
- `ComputeScheduler`：Tick 时先让中心 Dispatch，再按分数/虚拟时间算。
- `Pathfinder`：旧名，转给 PathCenter。点对点 `Request` 也会进中心队列。

## 已定流程

1. 建筑生成时投影入口，缓存 `segment + t`。寻路不现场搜段。
2. 两端是 `RouteEnd`（门口或路上的点）。`TripStarter.Request` 发起。
3. 入口 Node 看缓存 t（≤0.5 Start，否则 End）。
4. 全局：Node 之间按 `Segment.Cost` 做 Dijkstra。
5. 同一段：局部 t→t。跨段：出门→t→入口 Node→全局→出口 Node→t→进门。
6. `Length` 是几何，`Cost` 是图权重。
7. `Thing.Advance` 按逻辑点跳；还没有速度。

## 调用

```
TripStarter / Thing
  → PathCenter.Enqueue
  → Scheduler.Tick（中心 Dispatch → 计算 → Apply）
  → Thing 收 Trip
```
