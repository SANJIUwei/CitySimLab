# 寻路流水线（未全部实现）

日期：2026-09-10
状态：设计记录。排队调度以后再写。

## 已定流程（建筑 A → 建筑 B）

1. 建筑生成时自己对入口做最近点投影，把 `segment + t` 存进 Attachment。不切段，寻路时不现场搜段。
2. 入口 Node：只看该段缓存的 t，靠近 0 走 Start，靠近 1 走 End（不是扫全图，也不是门口直线距离）。
3. 全局：Node 之间按 Segment.Cost 做最短路（Dijkstra，不用 BFS，因为 BFS 无视边权）。
4. 路线发给载具后，到第一个 Node 之前、离开最后一个 Node 之后，用局部本能（沿段走到 t / 进门）。
5. Cost 是寻路边权；局部沿路距离用 Segment.Length，不要混。
6. 同一段上的两栋建筑：`PlanTrip` 只走局部 t→t，不进全局图。跨段才找入口 Node 再跑全局。
7. 入口 Node 只看缓存的 t（≤0.5 走 Start，否则 End），不用门口直线距离。
8. 最先一段 `LocalDeparture`：出门 → 路上的 t → 入口 Node。最后一段 `LocalApproach`：出口 Node → t → 进门。同一段两端都没有 Node。

## 调用入口

建筑只发 `TripCommand`（A/B），然后不管。`Thing` 把指令交给 `Pathfinder.Submit`，`Process(budget)` 定额处理后把 `Trip` 发回 `Thing.ReceiveRoute`。事物再做局部判定。不直接问 `RoadNetwork`。
