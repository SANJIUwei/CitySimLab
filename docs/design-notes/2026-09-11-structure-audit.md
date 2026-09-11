# 结构排查（2026-09-11）

阅读对象：用户监督 + 后续 AI
状态：已改一部分；其余按优先级记下，不一次做完

## 这一刀已改

- 建筑不再 `IssueTo`。发起出行只走 `TripStarter.Request`。
- 未接入直接返回 false，不进队列。
- 删掉编不过、且和 `RoadNetwork.FindPath` 重复的 `TripPlanner`。

职责现在是：

- `Building`：地点 + 门口 + 路上缓存
- `TripStarter`：要不要出发、把指令交给事物
- `Pathfinder`：排队、定额算
- `RoadNetwork`：图和 `PlanTrip`
- `Thing`：收路线、沿步骤走

## 还不合理、先不动

1. **`Trip` 已改为 `RouteEnd`**  
   建筑只是造 RouteEnd 的一种方式。路上的点用 `OnRoad`。

2. **车道和 `_adj` 两套拓扑**  
   单行、转向前必须并成一份，否则改一边漏一边。

3. **`Pathfinder.Request(node, node)` 绕过队列**  
   和建筑出行两套入口。等路线不绑建筑时一起收。

4. **`AttachNearest` 扫全段、无最大距离**  
   城市一大就慢，离路很远也会挂上。

5. **路改了 Attachment 不失效**

6. **Core 里多数类型没有 namespace**，和测试的 `CitySim.Tests` 混用全局类型。能跑，但以后文件一多容易撞名。

7. **`World` 既当坐标命名空间又当距离计算器**，用时还要 `new World()`。以后可改成静态方法，现在不影响路线。

8. **`Thing.Advance` 一次跳一个路点**，没有速度和节拍。

不要把建筑塞进全局图，也不要给每条车道再造 Node。
