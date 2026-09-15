# 给 AI 的设计思考记录

这个文件夹专门记录用户自己的设计思考。它不是功能清单，也不是允许 AI 直接实现玩法的授权。

## AI 使用规则

- 讨论设计前，先读最新的相关笔记。
- 把这些笔记当成公司/家里两个环境之间的连续记忆。
- 保留用户的疑惑、假设和推理过程，不要过早整理成“最终架构”。
- 如果下一步是学习任务，先让用户解释或尝试一个小问题，再建议代码方向。
- 用户有重要设计观察时，在会话结束前更新这个文件夹。
- 记录用户思考时默认使用中文，除非用户明确要求英文。

## 索引

- `2026-09-01-road-network-and-building-access.md`：路网图、建筑入口、道路接入关系，以及“最后 100 米”的局部接近思路。
- `2026-09-10-placement-logic-vs-visual-naturalness.md`：天际线依附道路 vs 狂热运输自由摆放；逻辑上依附道路更清晰，但视觉自然感应先记着，等理解更深后再讨论。
- `2026-09-10-segment-and-lanes.md`：路段从一开始就包含车道；多个起止在车道上，段两端仍是两个节点。
- `2026-09-10-pathfinding-pipeline.md`：PathCenter 排队，Scheduler 管中心；RouteEnd；局部 t→t / 跨段 Node。
- `2026-09-11-current-problems.md`：活清单。最大距离已 Closed；Open：车道并图、连续移动、缓存失效、没有世界节拍。
- `2026-09-11-structure-audit.md`：权属拆分现状。

用户可读的进度图：`docs/human/路网思维导图.md`（大进度 / 小进度）。
- `2026-09-11-skylines-scale.md`：百万人口 vs 路上载具上限；160×160 图上 2 万次寻路。
- `2026-09-11-compute-scheduler.md`：通用算力调度合同；寻路只是其中一个 Category。
- `2026-09-11-session-work.md`：2026-09-11 工作机结束摘要，回家先读这份。
- `2026-09-12-gpu-pathfinding-benchmark.md`：RTX 3060 Laptop GPU 上 2000 次 GPU 寻路结果；端到端瓶颈主要是 readback。
