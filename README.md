# CitySimLab

这是一个模拟 Unity 开发流程的学习环境。它现在只提供工程骨架、运行入口、测试入口和文档位置，不包含城市模拟玩法实现。

你的目标是在这个环境里从 0 开始写自己的城市模拟系统。我只负责帮你搭台、陪你排错、引导你验证。

## 当前结构

```text
src/CitySim.Core        你写核心玩法规则的地方
src/CitySim.Headless    命令行入口，用来在没有 Unity 的情况下运行实验
src/CitySim.UnityMock   少量 Unity 生命周期/类型模拟，帮助理解 Unity 思维
tests/CitySim.Tests     你给自己写验证用例的地方
docs/                   学习计划、架构记录、踩坑笔记
saves/                  未来存放存档或模拟快照
tools/                  未来存放辅助脚本
```

## 验证环境

```powershell
cd E:\Myself\CitySimLab
dotnet build
dotnet test
dotnet run --project src\CitySim.Headless
```

## 第一条规则

不要急着做“城市天际线”。先做第一个你能解释清楚的小东西。比如：一个格子、一条道路、一次时间推进。你来写，我来帮你验证和复盘。
