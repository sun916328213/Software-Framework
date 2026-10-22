# WPFControls.Charts.Workbench

独立的离线人工验证宿主，使用 Prism、CommunityToolkit.Mvvm 和 MaterialDesignThemes。

## 启动

在仓库根目录执行：

```powershell
dotnet run --project "Software Frame/WPFControls/WPFControls.Charts.Workbench/WPFControls.Charts.Workbench.csproj"
```

也可在根解决方案或 WPFControls 模块解决方案中设为启动项目。需要 Windows、.NET 9/WPF 及已有 NuGet/传递依赖。

唯一直接项目引用为 WPFControls.Charts；不引用 WPFControls 或 MachineApp，不注册生产模块、不读取机型配置、不连接设备。图表组件的 Extension 传递依赖仍存在。

## 六个独立页面

| 页面 | 数据操作 | 样式 |
| --- | --- | --- |
| LiveCharts 折线 | 点数、曲线增删、数值更新 | 轴字体、动画 |
| LiveCharts 柱状图 | 类别数量、柱数据组增删、数值更新 | 轴字体、动画 |
| LiveCharts 圆环 | 分类增删、数值更新 | 动画 |
| LiveCharts 多仪表盘 | 仪表项增删、数值更新，单位 min | 动画 |
| OxyPlot 折线 | 点数、曲线增删、数值更新 | 轴字体 |
| OxyPlot 饼图/圆环 | 分类增删、数值更新 | 内径；0 为实心饼图 |

每页拥有自己的 ChartPageViewModel、数据、命令、参数、计数和定时器。切页保留数据但停止上一页模拟，返回后不自动重启。

对照页已从枚举、导航、模板和测试中移除。当前工具不能物理删除文件，因此旧 `Views/ChartComparisonView.xaml(.cs)` 暂留磁盘，已通过项目的 Page/Compile Remove 排除；可在有 shell 的环境删除旧文件及对应排除项，不影响现有页面。

## 按钮规划

### 数据操作

- **生成 / 重置数据**：按本页参数生成一份新数据；曲线默认每组 100 点，柱状图默认 12 类，可选择 6/12/24/50 类。
- **更新首个数值**：原地修改输入，默认自动刷新。
- **添加 / 删除数据组**：只在折线、柱状图页显示；标题区分曲线与柱数据组，最多 6 组。
- **添加 / 删除分类或仪表项**：只在圆环、仪表盘页显示，最多 6 项。
- **清空并停止**：停止本页并将输入设为 null；重新生成数据后才能开始模拟。

空数据时更新、删除和开始按钮禁用；达到上限时添加按钮禁用。模拟运行时禁止手动生成、更新、增删和调整数据数量，避免下一帧覆盖操作。清空仍可停止运行。

### 样式设置

只显示当前图表支持的轴字体、动画或内径设置。主题在窗口顶部统一切换。

### 模拟运行

间隔可选 250/500/1000/2000 ms，默认不启动；开始和停止按钮按状态启用。每次生成有界快照，不保留无限历史。

### 高级诊断与生命周期

低频技术操作收进折叠区域：

- **修改后自动刷新**：默认开启。取消后，原地修改及集合增删不主动刷新，方便观察原生通知差异；生成数据仍刷新。
- **重新绘制当前数据**：显式调用当前控件 RefreshChart，不重新生成数据。
- **挂载图表**：取消时移除 Content 与 DataTemplate，真正卸载绘图视图；本页会话不因此停止，重新挂载显示最新数据。
- **重建本页**：释放本页旧会话，创建同类型的新上下文，默认停止；其他页面保持不变。
- 展示本页刷新计数、定时次数、时长、同步模型重建耗时和进程托管堆快照。

## 图表主题

原先只切宿主 BundledTheme，原生图表模型不会自动跟随。现在窗口通过继承附加属性 `ChartTheme.IsDark` 将主题传入图表组件：

- LiveCharts：控件背景、文字、图例、提示、轴标签、轴标题、分隔线及相关数据标签使用对应 Paint。
- OxyPlot：PlotModel 背景、文字、绘图区边框、坐标轴、图例，以及默认 Tracker 模板跟随主题。
- 原有数据组和分类颜色不被主题替换；圆环切片内部标签保留白色，仪表盘标签使用主题前景色。
- 每次替换 Series、坐标轴或 PlotModel 后重新应用主题，避免下一次刷新恢复浅色。
- 不使用全局静态深色状态，不订阅全局主题事件；重载后通过继承属性获得当前主题。
- 组件主题默认 null，不主动覆盖未接入主题的其他宿主；外部宿主可在容器或单个控件设置 IsDark。

此处是实现说明，仍需实际确认文字对比度、图例、提示、绘图重建及反复切换效果，不能把修改模型颜色等同于完成视觉验收。

## 所有权与观测边界

MainWindowViewModel 管理页面列表、切页停止、重建当前页与统一释放。ChartPageViewModel 管理本页模拟。ChartRefreshView 只管理 Loaded/Unloaded、DataContext 替换时的刷新订阅。

窗口关闭释放所有页面；单纯卸载图表不释放页面会话。所有输入修改和绘图更新都在 UI Dispatcher 上执行。

耗时只包含本页显式 RefreshChart 的同步模型重建，不包含数据生成、自动绑定刷新、布局、动画或实际绘制。内存来自 `GC.GetTotalMemory(false)`，是整个进程的托管堆，不是单页、原生内存或泄漏证明。

## 人工验收

以下是待验收项目，不是通过记录：

- [ ] 六种页面各自独立，存在柱状图，不再有对照入口。
- [ ] 分组按钮只显示适用操作，空数据、运行中和数量上限时启用状态正确。
- [ ] 常用增删/更新默认立即显示；关闭自动刷新后可观察原始通知，再手动重绘。
- [ ] 柱状图类别标签、分组颜色、负值和 null 清空正确。
- [ ] 深色不仅改变窗口：各图表背景、轴、图例、提示和标签清晰可读。
- [ ] 深色下更新数据、调轴字体、切页、卸载重载、重建本页后仍保持深色。
- [ ] 深色切回浅色后，各原生绘图颜色恢复，分类/曲线颜色不丢失。
- [ ] 切页停止上一页模拟；重建当前页不改变其他页数据。
- [ ] 反复挂载/卸载后，显式刷新计数每次只加一，没有旧数据或重复订阅。
- [ ] 窗口关闭停止全部会话；无设备或数据库操作。
- [ ] 中文、缩放平移、提示与图例符合各后端能力；窄窗口可换行和滚动。

## 自动验证

```powershell
dotnet build "Software Frame/WPFControls/WPFControls.Charts.Workbench/WPFControls.Charts.Workbench.csproj"
dotnet test "Software Frame/WPFControls/Tests/WPFControls.Tests/WPFControls.Tests.csproj" --filter "FullyQualifiedName~ChartsWorkbenchTests|FullyQualifiedName~ChartThemeAndColumnTests"
```

新增回归代码覆盖六种图表的原生主题颜色、刷新后保持主题、深浅回切、无主题时不覆盖宿主样式、柱状数据转换、按钮状态与页面生命周期。

目录迁移复核：已通过 PowerShell 在新路径构建 Workbench（包含图表组件），退出码 0。专项测试在构建依赖时被 Communication.Runtime.dll 文件占用错误 CS2012 阻塞，测试用例尚未执行；未终止占用进程。人工 UI 验收未执行，不能以构建通过替代视觉或行为验证。
