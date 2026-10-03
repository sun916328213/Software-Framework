# 全仓集成入口

从本文件所在的 Git 仓库根目录运行。根解决方案为 `Software-Framework.sln`，包含 80 个工程、26 个测试工程和仓库内的 `MachineApp/MachineApp` 整机宿主。框架模块位于 `Software Frame/`，整机目录与框架目录并列。

最新验证（2026-10-02）：架构统一为 `MachineApp → WPFControls → 各功能模块`；80 工程构建成功，29 个测试目标共 3066 通过、0 失败、9 跳过。MachineApp 和 FlowNodes.Workbench 发布依赖检查通过，242 个原始源码组和 4 个嵌入启动脚本的业务内容守卫通过。详见 [当前架构收尾记录](Build/Baselines/WPFControls-FinalClosure-Execution.md)。真实硬件、生产数据库和完整授权启动尚未验收；原有被排除编译的 05 启动脚本语法错误留待业务阶段修复。

Solution 按 Infrastructure、Hardware、Automation、Protocols、Vision、UI、Applications 分组，保留模块内目录。根 Solution 纳入全部 74 个自有工程及闭包内的 6 个 secs4net 上游工程，包括生产依赖、可选适配器、厂商包制作、工具、测试、Workbench 与整机宿主。

当前宿主测试位于 `MachineApp/Tests`，VS 中位于 `Applications → MachineApp → Tests`；Extension 位于 `Infrastructure → Extension`。模块职责见 [WPFControls 架构](Software%20Frame/WPFControls/ARCHITECTURE.md)。

## 执行

```powershell
# 无 SDK、无 NuGet、无数据库也能检查项目路径和根 Solution 依赖闭包。
./Build/Test-Integration.ps1 -CheckOnly

# 有预配置的私有包源与厂商 SDK：restore -> build -> 逻辑测试 -> WPF 测试 -> 整机 build。
./Build/Test-Integration.ps1 -NuGetConfig C:/private/NuGet.Config -VendorDllRoot C:/private/Dll

# 已准备好测试专用 MySQL、PostgreSQL、SQL Server 后，追加真数据库测试。
./Build/Test-Integration.ps1 -NuGetConfig C:/private/NuGet.Config -VendorDllRoot C:/private/Dll -IncludeDatabaseTests
```

`-VendorDllRoot` 只复制项目 HintPath 实际引用的文件到仓库内 `Dll/`（已忽略）。不修改机器 SDK、不安装 COM 运行时、不提交厂商二进制。当前 7 个必需编译引用为 Basler、Hik、Dahua、I-Tek、Zebra、Matrox、BarTender；保留目录层级，具体路径见 `Software Frame/Device/Device.Runtime/Device.Runtime.csproj`。共享 DLL 目录位于共同父目录 `Dll/`，厂商包制作和清单校验路径同步。Cognex SDK 验证独立于本入口。

配置的 NuGet 源必须提供公开包和内部包 `Mitsubishi.MxComponent.Interop/5.0.0-internal.3`。本轮只消费内部包，不重新打包或公开发布。

常规测试运行所有参与 Solution 的非外部数据库测试，包括 SQLite 测试。四个真数据库测试类使用 `Category=ExternalDatabase` 标记，只有 `-IncludeDatabaseTests` 才执行。连接通过现有 `DM_MYSQL_*`、`DM_PG_*`、`DM_MSSQL_*` 环境变量设置；必须使用测试专用数据库，这些测试会创建、修改并清理表。失败不会被吞掉。Vision 原有需要 GPU、模型、厂商运行时的跳过项保持不变。

测试结果输出到 `TestResults/*.trx`。整机最后还会检查输出中的 `FlowNodes.dll` 与 `MachineApp.deps.json` 条目，守住 `Assembly.Load("FlowNodes")` 的部署依赖；此检查不等于完成了实际整机 UI、PLC、相机或 SECS 联机验收。

已完成 restore/build 后，可运行 `./Build/Test-ArchitectureClosure.ps1 -Publish`，验证当前结构、业务内容和两个宿主的发布依赖。

## GitHub Actions

`.github/workflows/integration.yml` 在 push、PR 与手动运行时触发：

- `structure` 使用公共 Windows runner 检查路径与项目闭包，无私有依赖。
- `integration` 使用标签 `self-hosted, Windows, X64, software-framework` 的私有 runner，安装 .NET 9 SDK，再执行同一集成脚本。失败时上传已产生的 TRX。Fork PR 不运行私有 runner。
- 配置仓库变量 `FRAMEWORK_VENDOR_DLL_ROOT`（runner 上的 SDK DLL 根目录）和 `FRAMEWORK_NUGET_CONFIG`（runner 上含私有包源的 NuGet.Config 路径）。凭据保留在 runner 的受控配置中。
- 手动运行可启用 `database_tests`。数据库连接变量由 runner 环境配置。

本地文件不会自动注册 runner、设置仓库变量、启用分支保护或发布工作流；这些部署步骤尚未执行。

## 历史执行记录

以下记录保留当时的名称、路径、计数、阻塞和设计方案，不代表当前状态。原 WPFControl.Application/Controls 已合并到 WPFControls，Packing Machine 已重命名为 MachineApp；当前操作入口和结果以本文开头及当前架构实施记录为准。

## 2026-09-30 执行记录

依据用户提供的 `software-framework-architecture-report.pdf` 第 6 页 P0 清单执行。

| 工作项 | 结果 |
| --- | --- |
| DataManager 根发现 | 移除对已删除 `Frame.sln` 的依赖；通过实际源码工程标记定位模块根。基线 18 个架构测试中 6 失败，修复后 18/18 通过。 |
| 同类测试基础设施 | 同步修复 Communication 和 Vision 的源码根发现；FlowEditor 命名守卫指向新根 Solution，仍禁止 `FlowNodes.Infrastructure` 兜底工程，允许根 Solution 逻辑分组 Infrastructure。 |
| SECS 引用 | 修复 WPFControl 1 条、FlowNodes 2 条、Packing Machine 2 条 `Secs` -> `SecsGem`。 |
| WPFCustomControls 去向 | 本地已存在并纳入 Git 的独立控件库、PropertyGrid.Core 与测试。保留引用；该工程不是 WPFControl 自身，未删除或替换。103/103 测试通过。 |
| 整机纳入 | 用户明确选择纳入同一仓库。解除 Packing Machine 忽略；内外副本业务内容一致、仅换行符不同，调整为共同父目录作为 Git 根目录，采用并列的外层整机工程；重复内层副本归档在 `.git/layout-backup/packing-machine-inner`。两个根 Solution 均指向外层整机。 |
| 根集成入口 | 72 工程、23 测试工程的引用闭包检查通过；根 Solution restore 成功（使用本机已有包缓存，不能据此声称干净机器私有包恢复成功）。 |
| 业务回归 | DataManager 常规测试 300/300；Communication 277/277；Vision 260 通过、8 个原有运行时测试跳过；FlowEditor 命名守卫 7/7。 |
| 数据库基线 | 初次 DataManager 全套测试 300 通过、22 个真数据库测试失败；本机相应数据库/连接未满足测试要求，未改业务逻辑或吞掉失败。 |
| 全仓构建 | 配置本机已有厂商 DLL 后，构建仍有 30 个编译错误，集中在 FlowNodes 对已退役视觉 API 的引用；尚未完成整机编译和运行验收。 |

### P0 尚未闭环的阻塞

`FlowNodes/FlowNodes/Vision/SingleTool` 仍使用 `DPLibrary_Halcon_F48.Tools.*`、`HVisionTool`、`HTool_*` 和 `Ctr_HVisionTool_DP`。当前视觉实现已转为 `Vision.Tools.Halcon` 的 `HalconToolBase`、`BlobTool`、`ShapeMatchTool`、`FindLineTool` 等类型。Packing Machine 还有相同的旧 API 消费点。

这不是仅把一个 using 或 ProjectReference 改名就能修复的问题：类型、配置模型、结果映射、编辑器入口和旧流程持久化兼容性都需要逐项对齐。本轮没有恢复已退役项目、伪造空实现、排除节点源码或删除整机功能来让构建变绿。根 CI 会持续报告这条断链，直到消费者迁移完成。

因此本轮完成了 P0 的测试根发现、SECS 路径修复、控件去向核实及可执行集成入口；**P0 整体 build/test/整机验收仍未通过**。私有 runner 和包源配置、视觉消费者迁移、真实硬件/整机 UI 验证仍待完成。

## 并列目录布局调整

```text
仓库根目录/
├─ .git/
├─ .github/workflows/
├─ Build/
├─ Software Frame/         框架模块
├─ Packing Machine/        整机业务
├─ Dll/                    本机 SDK，忽略
├─ Software-Framework.sln  CI 集成入口
└─ Software Frame.sln      原有 Visual Studio 总解决方案
```

原提交历史、分支、remote 与暂存对象保留；索引中的框架路径增加 `Software Frame/` 前缀。原索引和配置备份在 `.git/layout-backup/`，没有改写历史或提交、推送。源码架构测试仍通过实际工程定位框架模块目录，不依赖 `.git` 的位置。

本次并列布局复验：72 项目/23 测试工程闭包通过；80 个工程 ProjectReference 无缺失；两个总 Solution 与整机 Solution 路径有效；三菱清单 6/6 通过；DataManager 架构 18/18、FlowEditor 命名 7/7 通过。

## P1 执行记录（2026-09-30，第一阶段）

| 工作项 | 本轮结果 |
| --- | --- |
| WPFControl 可移植配置 | 23 条个人 NuGet 字体路径收敛为基于 `$(NuGetPackageRoot)` 的 Noto/Roboto 字体过滤，版本与缓存位置不再写死；真实 MSBuild Content 评估未留下这两类字体。移除指向不存在的 `ProgramControl/Views/ScanManagerView.xaml.cs` 的 Compile Update。 |
| Integration Host | Packing Machine 标记 `IsIntegrationHost=true`，根脚本验证唯一宿主，并追加 PLC、报警、启动流程、SECS CEID 的输出配置检查。定位和执行方式记录在 `Packing Machine/README.md`。 |
| 设备组合归属 | 新增 `Software Frame/Device/Device.Composition`，统一五类设备工厂、Workspace 与页面注册。WPFControl 不再直接引用 Device.Runtime；Workbench 复用同一组合模块，保持两种宿主原有导航生命周期。 |
| 配置迁移归属 | 原 500 多行实现归入 Device.Controls.Configuration。WPFControl 保留原命名空间、方法与结果类型的兼容门面；旧文件不覆盖、迁移幂等及不猜测语义保留。 |
| 结构与回归 | 根 Solution 当前 73 工程、23 测试工程闭包检查通过，81 个工程的项目引用无缺失。Device 逻辑 123/123、Workbench 与新旧配置迁移回归 99/99 通过；Workbench 测试真实启动 WPF App 并验证导航。 |
| 尚未通过 | WPFControl restore 已更新，但实际构建仍受 FlowNodes 原有 30 个旧视觉 API 错误阻塞；整机部署配置的输出检查尚未走到执行阶段。 |

本轮是 P1 职责收敛的第一阶段。旧设备页面、DataManager 查询/报警/统计实现及其他 ProgramControl 业务仍需后续迁移，未宣称整个 WPFControl 已完成公共 UI 与业务物理拆分。详细依赖及守卫见 `Software Frame/WPFControl/ARCHITECTURE.md`。未做内层目录清理，未提交或推送。


## P1 执行记录（2026-09-30，第二阶段）

- 报警历史查询迁入独立 `WPFControl.Application`；通过 IAlarmHistoryReader 隔离 Excel 实现，应用层不依赖 WPF、Prism、图表库或 DataManager Runtime。
- 日期与显示集合归回 ViewModel，保留原 XAML 绑定和公共 DTO/PieData 类型；独立读取实例避免查询改写实时报警写入文件名，补齐日历日期边界、比例计算、取消和失败时保留原显示结果。
- 报警查询独立回归 13/13 通过，包含真实 Excel 文件读写、真实 Prism 容器及 STA Dispatcher 通知验证；生产宿主接入通过 AlarmHistoryComposition，尚未完成整机 UI 验收。
- 根集成引用闭包：75 工程、24 测试工程；83 个源码工程的 ProjectReference 无缺失（不计 .git 布局备份）。
- 完整 WPFControl 构建仍受 FlowNodes 30 个旧视觉 API 错误阻塞；隔离编译另发现旧 Device API 的 73 个缺失类型错误。不能根据独立查询测试宣称宿主构建已通过。

剩余 P1 范围包含旧设备/视觉消费链恢复、其他数据查询及统计业务迁出、整机完整构建和 UI 验收。未提交或推送。


## P1 新设备接入与注册归属调整

- 移除本轮新增的 Device.Composition；Device.ControlsModule 注册 Workspace 和导航，具体 Runtime 工厂与标签打印服务由两个宿主注册。
- 四个旧导航入口承载新设备 Controls，整机生产会话及启动流程内嵌脚本使用新 Core 接口；旧设备服务不再恢复。生产配置缺失直接失败，不自动使用模拟设备。
- Device.Tests 130/130；设备 Workbench 109/109，覆盖新页面、生产设备生命周期与启动脚本；报警及架构守卫 20/20。WPFControl 依赖不重建的隔离编译为 0 错误，不能替代完整依赖构建。
- 当前集成解决方案为 74 工程、24 测试工程。完整构建仍存在 FlowNodes 的旧视觉类型问题，Packing Machine 的旧视觉编辑器 XAML 尚待迁移，WPFControl 全套测试仍有旧视觉序列化类型问题。整机启动与硬件验收未通过，P1 尚未全部完成。未提交或推送。


## 全模块注册、命名空间与 Tests 整理（当时的代码状态）

本节记录上一轮代码整理；最新公共 UI、错误上报与导航专项状态见本文件开头。前面的 P0/P1 分阶段记录保留历史结果。

- 全部自有模块完成注册归属、命名空间及 Tests 整理；73 个自有工程与 10 个第三方工程列入清单，根集成入口包含 79 个工程、25 个测试工程。
- 使用标准 Prism IModule/ModuleCatalog；DataManager、Communication、Device、SecsGem、FlowEditor、ScriptEditor 不再提供兼容静态 Register，删除全局导航注册跟踪。
- 全部 41 个自有 Obsolete 声明和仓库内旧调用已删除。仅保留精确类型元数据和旧 HTTP JSON 字段读取迁移，外部插件需重新编译。
- FlowNodes、Packing Machine 的旧视觉消费链已接入当前 Vision 工具与新编辑器；完整解决方案已构建通过。整机 App/XAML/模块目录、页面渲染和导航有独立宿主测试。
- 全仓最终测试与外部数据库、HALCON 许可及真实硬件边界见 [实施记录](Documentation/Refactoring/Execution-Report.md)，模块用法见 [Prism 模块指南](Documentation/Refactoring/Prism-Module-Guide.md)。最终结果以实施记录第 6 节为准。
- 未提交、推送或发布，原有暂存内容保留。

## P2 厂商依赖恢复（2026-10-01）

后续模块整改已完成日志契约消费、固定服务显式注册、插件加载异常诊断、资源重复初始化保护及生产初始化归属调整。最新完整集成为 2947 通过、0 失败、9 跳过，79 工程构建 0 错误。详细内容见 [Prism 模块检查与整改记录](Documentation/Refactoring/Prism-Module-Audit.md)；本节下方保留 P2 完成时的历史结果。

统一入口 `Build/Restore-VendorDependencies.ps1` 已接入集成脚本：核对 17 个 DLL、38 个 secs4net 源码文件，以及 Mitsubishi/HALCON 包版本；从外部 SDK 文件库补齐缺失文件，已有文件身份不符直接失败。公共 CI 增加 10 个隔离恢复回归。

新环境的 SDK 文件库布局、私有 NuGet 配置模板和维护流程见 [依赖恢复说明](Build/Dependencies/README.md)。企业源认证和厂商运行时由 runner 配置；模板不会覆盖本机配置。

本次完整集成使用 `-NoRestore`，79 工程构建 0 错误；25 测试工程、28 个目标共 2942 通过、0 失败、9 跳过，默认排除 24 个外部数据库测试。另完成 Mitsubishi 本地私有源的独立空缓存恢复、包内六个程序集身份核对及实际 Interop 类型编译。企业远程 Feed、厂商许可与真实硬件未验收。详细证据见 [P2 实施报告](Documentation/Refactoring/P2-Vendor-Execution-Report.md)。
