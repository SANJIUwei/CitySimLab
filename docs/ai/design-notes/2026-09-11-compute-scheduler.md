# 通用算力调度

日期：2026-09-11
阅读对象：用户监督

## 合同

- 各系统只交作业，不自己抢线程。
- 业务中心（如 `PathCenter`）实现 `IComputeCenter`：自己收请求、自己排队。
- Scheduler `Manage` 中心。`Tick` 先 `Dispatch` 再计算。
- 优先级 0–100。High/Normal/Low 只是 80/50/20。
- 按分数段排队，虚拟运行时间分配；等得久会往前挤，低优先级不饿死。
- 主线程只 `Apply`。
- **不绑死一颗物理核。** `dedicatedCore` 仍在代码里，但是过时选项，默认关；Headless 也不再打开。
- 调度器不管 CPU/GPU。默认后端是 `CpuParallelBackend`。`GpuDeferredBackend` 是可选实验，不挂在调度器上。

## 寻路

计算是作业，不属于任何人。`PathCenter` 只发送和组织排队。

## 明确不是

- 不是 Unity Job System，没有依赖图。
- Tick 仍按条数，不是按毫秒硬切片。
- GPU 寻路是家里独立实验，见 `2026-09-12-gpu-pathfinding-benchmark.md`。没接到调度器，先不做。
