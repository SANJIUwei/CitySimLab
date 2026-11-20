# CitySimLab · Godot 预览

用 Godot 4.7.2 .NET 版打开本目录的 `project.godot`，需要 .NET 10 SDK。按 F6 运行 `CityView.tscn` 或按 F5 运行项目。标准版 Godot 不加载 C#。

演示地图包含 20 个路口、27 段道路和 6 栋建筑。预览按顺序回放 Core 的 ALT 搜索、显示金色路线，再让白点沿路点移动。金色为本步确认，青色为已确认，蓝色为待确认或本步更新。地图数字是已走代价，右侧还显示地标估计得到的总代价。

路线确定后，面板对比 ALT 和 Dijkstra 在同一起终路口确认的节点数。对照搜索仅用于演示，普通出行请求不会执行两遍。空格暂停或继续，右方向键单步寻路，R 重播，「交换起终点」对调两端。

- `CityDemo.cs`：示例数据和 Core 适配，不引用 Godot 类型。
- `CityView.cs`：渲染、界面和展示速度，不替代 Core，也没有新增世界时钟。
- Core 仍可独立运行；Godot 示例是可选项目，没有加进主解决方案。

单独构建与冒烟验证（在仓库根目录运行）：

```sh
dotnet build src/CitySim.Godot/CitySim.Godot.csproj
godot --headless --path src/CitySim.Godot -- --smoke
```

运行时命令的 `godot` 必须指向 .NET 版。
