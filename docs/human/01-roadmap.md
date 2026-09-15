# 迭代路线

学习顺序。不是功能堆清单。

## Now

逻辑层能从一端走到另一端。

| 能力 | 状态 |
|---|---|
| 工程能编、能测、能跑 Headless | 完成 |
| 坐标 `WorldPosition` | 完成 |
| 路网 Node / Segment / Lane | 完成，还粗 |
| 建筑挂 `segment + t`，可选最大距离 | 完成，还粗 |
| `PathCenter` 排队 + `ComputeScheduler` 分预算 | 完成 |
| `RoadNetwork.FindPath` / `PlanTrip` | 完成 |
| `Thing.Advance` 跳逻辑点 | 完成，还粗 |

## Next

按会卡住扩展的顺序：

1. 车道方向并进图拓扑（现在 `_adj` 一律双向）
2. `Thing` 按距离/节拍移动（现在一次跳一个路点）

## Later

- 可视化
- 存档
- 交通
- 经济
- 网络同步

## Blocked / 先不做

- 真实 Unity 工程、转向、标志、画面
- 把建筑塞进全局图，或给每条车道再造 Node
- 把 GPU 寻路接到 `ComputeScheduler`（家里独立实验，本仓库没有内核代码）

细节见 `docs/human/路网思维导图.md`。模块边界见 `docs/human/02-architecture.md`。
