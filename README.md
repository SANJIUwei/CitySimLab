# CitySimLab

纯 C# 城市模拟内核。不依赖 Unity。

当前能做的事：路网、建筑挂路、排队寻路、按路点走路。还不是一座会自己转的城。

## 怎么跑

```powershell
cd E:\Myself\CitySimLab
tools\verify.ps1
```

等价命令：`dotnet build`、`dotnet test`、`dotnet run --project src\CitySim.Headless`。

## 先读哪份

| 你想知道 | 去哪 |
|---|---|
| 文档目录 | `docs/README.md` |
| 现在做到哪、下一刀是什么 | `docs/human/路网思维导图.md` |
| 模块职责和边界 | `docs/human/架构.md` |
| 现在 / 下一步 / 以后 | `docs/human/迭代路线.md` |
| 怎么验证 | `docs/human/不用Unity的验证方式.md` |
| 谁策划、谁写代码 | `docs/human/分工.md` |

## 目录

```text
src/CitySim.Core/World      坐标
src/CitySim.Core/Roads      路网、建筑挂路
src/CitySim.Core/Routing    出行排队
src/CitySim.Core/Compute    调度
src/CitySim.Core/Movement   走路
src/CitySim.Headless        命令行观察
tests/CitySim.Tests         规则测试
docs/human                  给人看的文档
docs/ai                     给 AI 的协议和笔记
tools/verify.ps1            编译 + 测试 + Headless
```

## 边界

- Core 不引用 Unity。
- 先用测试和 Headless 证明规则，再谈画面。
- GPU 寻路是可选实验，代码未进本仓库。见 `docs/ai/design-notes/2026-09-12-gpu-pathfinding-benchmark.md`。