# WPFControls 架构

依赖方向：`MachineApp → WPFControls → FlowNodes / Communication / DataManager / Device / SecsGem / 其他可复用模块`。

WPFControls 是一个 WPF 程序集，包含通用业务页面、展示模型、ViewModel、查询和模块装配。原 WPFControl、WPFControl.Application、WPFControl.Controls 已合并，不保留旧 DLL、类型转发器或旧模块入口。查询与统计写入逻辑仍通过读写契约与页面分离，作为普通类存在，不再单独建立 Application 项目。

目录按功能组织：`Features/Alarm`、`Log`、`Data`、`Flow`、`Device`、`Printer`、`Shell`、`User`、`Display`。功能内部放置 Models、Services、Queries、Presentation、Views、ViewModels。分页、用户输入等通用控件放在 `Shared`；图表由独立 `WPFControls.Charts` 程序集提供，源码物理位于 `WPFControls/WPFControls.Charts`，不再链接 `Shared` 源码，也没有对应的 Compile/Page 排除项。根命名空间统一为 `WPFControls`。`WPFCustomControls` 继续是独立基础控件库。

MachineApp 只直接引用 WPFControls。其他模块的项目依赖与通用装配入口由 WPFControls 提供，宿主可以使用它公开组合的模块契约；该聚合不代表隐藏了底层运行时或厂商依赖。

## 图表边界

`WPFControls → WPFControls.Charts → LiveCharts / SkiaSharp + OxyPlot`。图表的 Models、Services、Views 均由 WPFControls.Charts 编译，不反向引用 WPFControls；业务查询与统计仍归 WPFControls。为保持现有应用语言选择，WPFControls.Charts 暂时保留对 Extension 的依赖。项目物理路径、项目文件名与程序集名统一为 `WPFControls/WPFControls.Charts/WPFControls.Charts.csproj`。

后端控件与服务沿用现有 CLR 命名空间，共用输入使用 `WPFControls.Charts.Models`；外部 XAML 需要 `assembly=WPFControls.Charts`。历史持久化类型通过 FrameworkTypeResolver 精确映射到新程序集，不提供旧 DLL 或类型转发器。详见 `WPFControls.Charts/README.md`。

OxyPlot 折线、饼图/圆环图以独立控件与原有 LiveCharts 并存，源码直接放在图表项目内。输入复用共用模型 ChartLineData/ChartPieData，采用快照更新，不改变现有业务页面。原地修改数据时由宿主在 UI 线程显式调用 RefreshChart，不引入设备或定时采样。高级图表可直接使用 OxyPlotChartService 创建独立 PlotModel。详见图表 README 的后端列表与使用约定。

## 装配和导航

宿主在 `App.RegisterTypes` 中调用 `WPFControlsComposition.RegisterServices(registry)`，在其他页面模块注册前固定节点目录；已有宿主替换不被覆盖。在 `ConfigureModuleCatalog` 调用 `WPFControlsComposition.ConfigureModules<SqlConfigEntity, ExcelConfigEntity>(catalog)` 装配模块。WPFControlsModule 统一注册业务页面和 Shared 控件，幂等合并资源字典。

WPFControls 注册可用页面，不生成机型菜单，也不发起页面导航。MachineApp 的 `Business/Flow/Init.cs` 定义菜单与 RequestNavigate，App 定义预热列表。页面注册不连接生产硬件、不选择数据库和机型、不执行生产启动流程。

## 数据库和业务归属

机型工艺、工站、PLC 点位、配方、数据库配置、具体实体、设备连接和生命周期属于 MachineApp。具体实体放在 `Infra/Persistence/Entities`，SQL 适配放在 `Adapters`，实体与展示 DTO 的 Mapster 映射放在 `Mapping`；展示 DTO 保留在 UI。

WPFControls 的 `CapacityStatisticsRow` 是页面数据，不实现 ISQLDataBase。MachineApp 的 `CapacityStatisticsInfo` 是具体数据库实体，继承页面数据并实现查询页所需契约。SQL 读写适配由 MachineApp 注册，实际 SQL 对象在查询或写入时延迟解析。原 CapacityStatisticsInfo、CSQLData 表名、分表特性和字段不变。

旧持久化 CLR 类型名通过 Extension.Core 的精确迁移映射读取，目标是当前 WPFControls/MachineApp 程序集。这是数据迁移，不提供旧源码 API 或旧程序集入口；外部源码插件需使用新命名空间重新编译。

## 验证

`Build/Test-Integration.ps1` 验证总解决方案、逻辑/WPF 测试和 MachineApp 宿主；`Build/Test-ArchitectureClosure.ps1 -Publish` 验证当前快照、业务内容指纹、底层无 UI 依赖闭包及发布目录。当前结构快照为 `Build/Baselines/WPFControls-FinalClosure.json`，迁移与业务内容记录以 `Build/Baselines/WPFControls-*` 为准；Architecture-S4/S5/S6/S7 与 Architecture-Closure 保留为历史证据。

真实设备联机与生产启动不由模块装配测试证明。排除编译的旧 `05_初始化后台.cs` 及其嵌入脚本已有语法问题，本轮保留原业务内容并以全 token 指纹核对，待业务阶段处理。

查询包装服务 DataQueryService、LogQueryService 和 SEMILogQueryService 直接注入展示模型、日志管理器及查询契约，默认 reader 在 QueryComposition 注册。VisionInspectionItemService 不再接收未使用的容器参数。UserMessage 的命名空间为 WPFControls.ProgramControl.Presentation.User，旧持久化别名指向该实际声明。其他公共命名空间保留当前功能语义，不为全面匹配目录再做批量重命名。
