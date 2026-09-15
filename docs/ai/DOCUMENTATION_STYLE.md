# 文档写法

文档用来建立正确的系统模型，并在需要时找到具体信息。

优先级：正确 → 清晰 → 可扫描 → 信息密度 → 一致性。

## 不要写成作文

禁止无信息量套话：「本文主要介绍」「首先让我们」「综上所述」「值得注意的是」「为了更好地理解」。

先结论，后解释。一段一个意思。能用表格、流程、短列表就不用长段落。

标题要有信息量。不要用「一些想法」「关于这个问题」「其他」。

## 文档分工

| 类型 | 记什么 | 位置 |
|---|---|---|
| Architecture | 现在是什么：职责、边界、依赖、接口 | `docs/human/02-architecture.md` |
| Roadmap | Now / Next / Later / Blocked | `docs/human/01-roadmap.md` |
| 进度图 | 给人扫的大/小进度 | `docs/human/路网思维导图.md` |
| Design Note | 为什么变成这样：尝试、放弃、未决 | `docs/ai/design-notes/` |
| Benchmark | 测出来什么：数字、环境、限制 | `docs/ai/design-notes/` |
| Session Note | 这次做了什么 | `docs/ai/design-notes/` |
| Current Problems | 现在有什么问题 | `docs/ai/design-notes/2026-09-11-current-problems.md` |
| Handoff | 下一会话从哪接 | `docs/ai/AI_HANDOFF.md` |

一个文件不要同时当教程、日志、Benchmark 和 TODO。信息已在别处就引用，不复制。

## Architecture 结构

一句话职责。然后：负责 / 不负责 / 接口 / 依赖 / 数据流 / 扩展时改谁。不要写成长篇历史。

## Design Note

保留疑惑、假设、尝试、失败、取舍。允许第一人称。不要改写成已确定的最终架构。

## Benchmark

必须有环境、数字、发现、限制。禁止只写「性能显著提升」。区分 Algorithm / Backend / Transfer / Sync / End-to-End。

## Current Problems

每个问题写清：是什么、为什么是问题、影响、状态（Open / Closed）。

## 事实和判断分开

事实：`ComputeScheduler` 当前 11 个 priority buckets。

判断：11 个桶目前够用。

实验：见对应 Benchmark。

没有证据的「高效 / 优雅 / 可扩展」不要写。

## 和代码一致

代码没实现，文档不许写成已实现。Benchmark 没跑，不许写成已验证。问题已解决，清单里要改状态。不确定标 `Unknown` 或 `TODO`。

冲突时：

```text
代码 / 测试 / Benchmark
    ↓
Architecture
    ↓
Design Notes
    ↓
Session Notes
```

不要擅自把用户的 Design Note 改写成最终架构。

## 改文档的时机

只有变化动到职责、接口、架构关系、设计决策、Benchmark、当前问题或路线时，才改长期文档。普通 bug fix 不必新开一篇。

类型和方法用反引号写准名：`PathCenter.Enqueue()`，不要写「这里」「那个类」。
