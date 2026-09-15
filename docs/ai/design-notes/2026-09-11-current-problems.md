# 当前逻辑层问题

日期：2026-09-15
阅读对象：AI
状态：活清单。已解决标 Closed。

## Closed：接入可设最大距离

`Building.AttachNearest(network, maxDistance = +∞)`。超过最大距离不挂，`Attachment` 为 null。不传距离时行为与从前相同。

`AttachNearest` 仍扫全部路段。没有空间索引，按设计先不做。

## Open：Thing 还不会连续走

`Thing.Advance()` 一次跳一个逻辑点。没有速度，没有每帧走一段距离。

影响：载具/行人运动还没有可替换的移动模型；世界节拍也还没有落点。

Roadmap Next 第 2 项。缺 `Simulation Clock` 时，不要把时间模型写死进 `Thing`。

## Open：车道和图是两套拓扑

`RoadLane` 有 Forward / Reverse。`RoadNetwork._adj` 一律双向。

影响：单行路以后两套拓扑会对不上。

Roadmap Next 第 1 项。不要给每条车道再造 Node。

## Open：路改了，建筑缓存不会失效

建筑身上的 `segment + t` 不会随路网编辑失效。

影响：以后改路、拆路时，未接入和错误接入会静默留着。

排在车道并图和连续移动之后。

## Open：还没有种类和世界节拍

没有车/人差别，没有每帧 `Tick` 的世界循环。Headless 只处理一次队列。

影响：Traffic 和连续移动都依赖这一层。现在做车辆系统会把未定的时间接口绑死。

## 建议的下一刀

1. 车道方向并进图拓扑。
2. `Thing` 按距离/节拍移动。

不要把建筑塞进全局图，也不要给每条车道再造 Node。