# WPFControls.Charts

可复用的 WPF 图表程序集，封装折线图、甜甜圈和多仪表盘，以及图表展示模型和序列生成服务。不包含报警、产能、数据库查询等业务。

## 边界

- `MachineApp → WPFControls → WPFControls.Charts → LiveCharts / SkiaSharp`。
- 保留 `WPFControls.Controls.Models.Charts`、`Services.Charts`、`Views.Charts` 命名空间，避免批量修改业务脚本；解决方案中的项目显示名和程序集名为 `WPFControls.Charts`，输出 `WPFControls.Charts.dll`。
- 当前工具无法重命名文件，物理路径暂保留 `Charts.Controls/Charts.Controls.csproj`，通过显式 AssemblyName 设置程序集身份。
- 为保持饼图标签原有的应用语言判断，仍引用 `Extension` 的 `LangSwitchEx`。这会传递引入 Extension 的运行依赖；本次未拆分其语言基础设施。
- 无 WPFControls 项目引用。`ChartsLineService` 的可选参数改为 `Action<Action>` 执行器；宿主可通过 `new ChartsLineService(action => errors.TryExecute(action))` 适配原错误处理。默认仍捕获操作异常并写 Trace，但不再使用 WPFControls 的诊断节流器。

## 源码归属

当前通过 Compile / Page 链接编译 `WPFControls/WPFControls/Shared/{Models,Services,Views}/Charts` 下的源文件，WPFControls.csproj 已排除这些文件。每个类型和 XAML 只有 WPFControls.Charts 一个编译所有者，没有源码副本。

这是程序集拆分，尚未物理迁移源文件目录。后续物理迁移时，将文件移动到本项目的 Models / Services / Views 并删除双方的链接与排除项即可；不要在两个项目中重复编译。

## 使用与兼容

外部 XAML 使用：

```xml
xmlns:charts="clr-namespace:WPFControls.Controls.Views.Charts;assembly=WPFControls.Charts"
```

现有依赖属性、图表样式和数据更新逻辑保持不变。FrameworkTypeAliases.json 的历史类型迁移指向 WPFControls.Charts，包含此前持久化的 `WPFControls` 程序集身份。此映射只适用于 FrameworkTypeResolver；不提供旧二进制插件兼容或 CLR 类型转发，外部源码消费者需要重新编译。

## 验证入口

```powershell
dotnet build "Software Frame/Charts/Charts.Controls/Charts.Controls.csproj"
dotnet test "Software Frame/WPFControls/Tests/WPFControls.Tests/WPFControls.Tests.csproj"
dotnet test "MachineApp/Tests/MachineApp.Host.Tests/MachineApp.Host.Tests.csproj" --filter FullyQualifiedName~ChartsPlatformHostTests
dotnet build "MachineApp/MachineApp/MachineApp.csproj"
```

在仓库根执行，需要 Windows/WPF、.NET 9 以及项目已有的 NuGet/厂商依赖。宿主测试使用现有 ProductionFixture（`executeProductionStartup: false`），不执行生产硬件启动。构建和测试尚需在具备命令执行环境的机器验证，不能以源码检查代替 UI 验收。
