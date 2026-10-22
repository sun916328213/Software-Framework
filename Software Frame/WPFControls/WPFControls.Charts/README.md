# WPFControls.Charts

可复用的 WPF 图表程序集，提供 LiveCharts 和 OxyPlot 两套可并存的控件。不包含报警、产能、数据库查询等业务。

## 图表后端

| 方案 | 定位 | 当前状态 |
| --- | --- | --- |
| LiveCharts 2 | 动画、统计展示、仪表盘 | 折线、分组柱状图、甜甜圈、多仪表盘 |
| OxyPlot 2.2.0（MIT） | 常规统计、科学绘图，支持缩放和平移 | 折线、饼图/圆环图 |
| ScottPlot | 大量采样点、信号与波形展示 | 可作为后续扩展，本次未引入，性能需按实际数据验证 |
| SciChart | 商业高性能图表 | 本次未引入，需另行评估商业许可 |

本项目目前同时引用 LiveCharts 和 OxyPlot，消费者会获得两套依赖，并非按后端分别发布的包。现有业务页面不自动切换后端。

## 目录与边界

```text
WPFControls/WPFControls.Charts/
├─ Models/                   共用输入：ChartLineData、ChartPieData
├─ LiveCharts/Services/      ChartsLineService、ChartsColumnService、ChartsPieService
├─ LiveCharts/Views/         ChartsLineView、ChartsColumnView、ChartsPieView、ChartsMultipleGaugeView
├─ OxyPlot/Services|Views/   OxyPlotChartService、OxyPlotLineView、OxyPlotPieView
└─ Themes/                  ChartTheme 附加属性与 Tracker 主题资源
```

- `MachineApp → WPFControls → WPFControls.Charts → LiveCharts / SkiaSharp + OxyPlot`。
- 项目目录、项目文件、程序集名统一为 `WPFControls.Charts`，输出 `WPFControls.Charts.dll`。
- 共用输入使用 `WPFControls.Charts.Models` 命名空间；后端控件与服务仍沿用 `WPFControls.Controls.Services.Charts`、`WPFControls.Controls.Views.Charts`，未批量改名，以免打断已有 XAML 与业务脚本。
- 为保持饼图标签原有的应用语言判断，仍引用 `Extension` 的 `LangSwitchEx`。这会传递引入 Extension 的运行依赖；本次未拆分其语言基础设施。
- 无 WPFControls 项目引用。`ChartsLineService` 的可选参数改为 `Action<Action>` 执行器；宿主可通过 `new ChartsLineService(action => errors.TryExecute(action))` 适配原错误处理。默认仍捕获操作异常并写 Trace，但不再使用 WPFControls 的诊断节流器。

## 源码归属

图表源码全部物理位于本工程目录，`WPFControls.csproj` 既不再链接这些源文件，也不再有对应的排除项。每个类型只有一个编译所有者，没有源码副本。

原先的 `ChartsLineModel`、`ChartsPieModel` 只是包装服务内部状态，已取消：点大小、字体大小、Series、坐标轴、增量数据集合与饼图 Series 集合改为服务私有字段，行为保持不变。`FrameworkTypeAliases.json` 中指向这两个类型的映射已一并删除，不留悬空映射。

## 共用输入

| 原类型 | 现类型 | 说明 |
| --- | --- | --- |
| `LineSeries` | `ChartLineData` | 曲线名称、数值集合、颜色 |
| `PieData` | `ChartPieData` | 分类名称、数值、颜色 |

两个模型只表达展示所需的数据，不包含 LiveCharts 的 `ISeries`/`Axis`、OxyPlot 的 `PlotModel`/`DataPoint`、控件实例，也不包含查询、设备或定时器。折线输入按索引排列，`AxisX` 提供类别标签，本轮不扩展散点、时间序列或多 Y 轴契约。

## 使用与兼容

外部 XAML 使用：

```xml
xmlns:charts="clr-namespace:WPFControls.Controls.Views.Charts;assembly=WPFControls.Charts"
```

现有依赖属性、图表样式和数据更新逻辑保持不变。`FrameworkTypeAliases.json` 的历史类型迁移指向 `WPFControls.Charts`：旧的 `LineSeries`/`PieData` 身份精确迁移到新的共用模型，此前持久化的 `WPFControls` 程序集身份仍可解析。此映射只适用于 `FrameworkTypeResolver`；不提供旧二进制插件兼容或 CLR 类型转发，外部源码消费者需要重新编译。

## OxyPlot 控件

```xml
xmlns:oxy="clr-namespace:WPFControls.Charts.Views.OxyPlot;assembly=WPFControls.Charts"

<oxy:OxyPlotLineView ItemSource="{Binding Lines}"
                     AxisX="{Binding Labels}"
                     AxisFontSize="14" />
<oxy:OxyPlotPieView ItemSource="{Binding PieData}"
                    InnerDiameter="0.6" />
```

- `ItemSource` 使用 `WPFControls.Charts.Models.ChartLineData` 或 `ChartPieData`；折线横坐标是点的索引，AxisX 提供类别标签。饼图 `InnerDiameter=0` 为实心，`0.6` 为圆环，允许范围为 `[0,1)`。
- 每个控件拥有独立 PlotModel，不修改输入数据，也不覆盖宿主 DataContext。
- **快照更新**：替换 ItemSource、AxisX 或样式依赖属性会重建模型；原地 Add/Remove、修改数值/颜色/名称、替换某条折线的 Value 后，必须由宿主在 UI 线程调用 `RefreshChart()`。未订阅集合、事件、Timer 或后台任务，因此没有加载卸载订阅或宿主释放问题。这不是现有 LiveCharts 自动集合更新行为的完全替代。
- 后台采集线程应先生成快照，再通过 Dispatcher 交给控件；不要在图表读取时并发修改输入集合。
- 折线非有限值显示为断点；饼图忽略非有限值和小于等于零的值。null 数据源清空显示。轴字体大小必须为正有限数。
- 高级用户可直接调用 `WPFControls.Charts.Services.OxyPlot.OxyPlotChartService.CreateLine/CreatePie` 获取模型，交给原生 `OxyPlot.Wpf.PlotView`，进一步配置标题、坐标轴和主题。不要将同一个 PlotModel 同时绑定到多个 PlotView。
- 尚未提供 OxyPlot 多仪表盘。两套后端均可接入下方 ChartTheme；字体、深浅主题、缩放和平移交互仍需 UI 验收。

## 刷新与生命周期约定

| 操作 | 行为 |
| --- | --- |
| 替换 `ItemSource` | 自动刷新 |
| 替换 `AxisX`（提供该属性的控件） | 自动刷新 |
| 修改 `AxisFontSize` 等样式依赖属性 | 立即应用并刷新 |
| `ItemSource` 设为 null | 清空显示，不残留旧图形 |
| 原地修改输入数据 | 由宿主在 UI 线程调用 `RefreshChart()` |

- 四个 LiveCharts 控件都提供公开的 `RefreshChart()`。
- `ChartsLineView` 与 `ChartsColumnView` 支持外层集合变化自动刷新：替换数据源时解除旧订阅，同一实例不重复订阅，卸载时解除订阅，重新加载时恢复订阅并刷新当前数据。
- 饼图与多仪表盘控件采用快照刷新，不额外增加订阅或空的生命周期代码。
- 图表控件只管理自己创建的绘图对象与订阅；卸载不停止宿主采集、不释放宿主 ViewModel、不关闭设备。

## 柱状图与主题

`ChartsColumnView` 提供分组柱状图，每个 ChartLineData 是一个数据组，AxisX 为类别标签；仍复用共用输入，不引入第三方 Series 到输入模型。

```xml
xmlns:charts="clr-namespace:WPFControls.Controls.Views.Charts;assembly=WPFControls.Charts"
xmlns:theme="clr-namespace:WPFControls.Charts.Themes;assembly=WPFControls.Charts"

<charts:ChartsColumnView ItemSource="{Binding Groups}" AxisX="{Binding Categories}"
                         theme:ChartTheme.IsDark="{Binding IsDarkTheme}" />
```

`ChartTheme.IsDark` 可设在容器上，由子图表继承。true/false 分别应用深/浅色；默认 null 不主动改写未接入主题的宿主。主题修改原生绘图颜色，不只是 WPF 外框：LiveCharts 的轴、图例、提示和标签 Paint，以及 OxyPlot 的 PlotModel、图例、轴和默认 Tracker 均会跟随。每次重建绘图数据后重新应用；输入指定的曲线/分类颜色不变。自定义 OxyPlot Tracker 模板保留，由宿主自行适配。

## 独立人工验证宿主

新增 `WPFControls.Charts.Workbench`，唯一直接项目引用为图表组件，不依赖 WPFControls 聚合项目或 MachineApp，不触发生产模块或设备启动。按“后端 × 图表类型”提供六个独立页面，包含新增的 LiveCharts 柱状图；对照页已退出导航和编译。各页拥有独立数据、按钮命令、参数、计数和模拟会话；支持数据替换/原地修改/增删、显式刷新、null 清空、卸载/重载和当前页 DataContext 重建。切页停止上一页模拟，宿主与图表深浅主题统一切换。常用数据操作默认自动刷新，诊断操作单独折叠；按钮按空数据、运行状态和数量上限禁用。

```powershell
dotnet run --project "Software Frame/WPFControls/WPFControls.Charts.Workbench/WPFControls.Charts.Workbench.csproj"
```

详见 [Workbench 运行说明与人工验收清单](../WPFControls.Charts.Workbench/README.md)。迁移到 WPFControls 目录后，Workbench（含图表组件）构建通过；专项测试在依赖构建阶段因 Communication.Runtime.dll 被其他进程占用而阻塞，尚未执行测试用例。人工界面验收未执行。

## 验证入口

```powershell
dotnet build "Software Frame/WPFControls/WPFControls.Charts/WPFControls.Charts.csproj"
dotnet test "Software Frame/WPFControls/Tests/WPFControls.Tests/WPFControls.Tests.csproj"
dotnet test "MachineApp/Tests/MachineApp.Host.Tests/MachineApp.Host.Tests.csproj" --filter "FullyQualifiedName~ChartsPlatformHostTests|FullyQualifiedName~OxyPlotPlatformHostTests"
dotnet build "MachineApp/MachineApp/MachineApp.csproj"
```

在仓库根执行，需要 Windows/WPF、.NET 9 以及项目已有的 NuGet/厂商依赖。宿主测试使用现有 ProductionFixture（`executeProductionStartup: false`），不执行生产硬件启动。构建、测试与 UI 验收需要分别报告，不能相互替代。
