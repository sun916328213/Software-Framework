# FlowEditor 架构

> 本文档描述当前架构和必须遵守的目标依赖方向。完整迁移步骤见
> [`ARCHITECTURE_OPTIMIZATION_PLAN.md`](ARCHITECTURE_OPTIMIZATION_PLAN.md)。

## 当前状态

FlowEditor 当前由 `FlowEditor.Runtime`（纯 `net9.0`）、`FlowEditor.Runtime.Smoke`（无界面控制台验收）和 `FlowEditor`（`net9.0-windows` WPF）组成。P5 已把运行节点与结构化作用域契约、端口、连线、文档 DTO、程序定义、图编译、校验、调试控制、执行追踪、运行实例、执行引擎及循环/并行 runner 迁入 Runtime；WPF 项目单向引用 Runtime。

```text
Views -> ViewModels -> Canvas / Documents / Execution / Output
                           ^
                           |
             FlowChartService（组合根）
                           |
                           v
        IFlowChartService（节点最小上下文） -> Editor Models / Nodes
                           |
                           v
                    FlowEditor.Runtime

Infrastructure adapters -> Application seams
```

当前依赖状态包括：

- `Core` 已禁止引用 `ViewModels` 和 Prism；节点 factory 使用实例注册表，不再持有宿主级静态 resolver。
- 通用 `Interfaces` 不再引用具体 ViewModel；`IFlowTabFactory` 归属 ViewModel 层，由 Presentation 的 Prism adapter 单向实现。
- `IFlowChartService` 只暴露 `NodeModel` 视角的节点集合与查询，不再把 `NodeViewModel` 泄漏给领域节点；`FlowChartService` 通过显式接口实现完成画布适配。
- `NodeModel` 持有 `IFlowChartService`，用于查询所属画布的节点和连线。
- 节点编辑、信息提示和动作命令已通过 Presentation adapter 与 WPF 隔离。
- `FlowChartService` 仅负责依赖组装和节点上下文；执行、文档、画布和输出操作不再经过兼容转发门面。
- `FlowOutputBuffer` 拥有输出缓冲、容量截断、节流、尾批刷新和清空一致性。
- Runtime 自有 `FlowLogLevel` 和 `IFlowLogSink` 结构化日志契约；编辑器核心、执行模块和日志节点不再引用 `DataManager` 或 `Extension.Struct`，外部日志框架只在宿主 adapter 中获取配置并转换等级。

仍存在的历史依赖是待消除的迁移基线，不是允许新增依赖的架构规则。架构约束测试使用显式 allowlist 锁定现有债务，并禁止节点上下文重新引入 ViewModel。

当前迁移进展：执行引擎只依赖 `INodeExecutable` 和 `IExecutionLogContext`；静态校验和顶层图只依赖 `IFlowValidationNode`、`IParallelForkNode`、`IExecutionScopeDescriptor` 等语义契约，不识别任何编辑器具体节点类。桌面端以 `FlowLogExecutionContextAdapter` 保留流程、节点及运行序号日志，无头宿主使用无副作用默认实现。`FlowRuntimeInstance<TNode>` 拥有活动图筛选、作用域注入、调试、追踪和执行，WPF `FlowInstance` 仅保留具体节点物化、属性通知和流程日志作用域适配。并行泳道、嵌套容器和运行实例使用独立引擎状态，避免共享运行序号竞争。输出栏由 `FlowOutputBuffer` 隐藏并发缓冲、容量截断和节流细节，通过 `IUiDispatcher` 连接 WPF 或无头宿主。`FlowDocumentMapper` 统一单流程、多流程、快照和节点复制的 DTO 转换，`IFlowDocumentStore` 通过 JSON 与内存 adapter 隔离存储介质。标签页和工具箱节点创建分别使用 `IFlowTabFactory` 与 `INodeFactory`；Prism 解析仅保留在组合入口和 WPF adapter。应用模块、ViewModel、工具箱模型和 ToolBlock 窗口已不再使用 Service Locator，测试通过硬约束防止依赖回流。

`FlowExecutionCoordinator` 通过节点模型快照 seam 和只读连线列表执行当前画布，不引用 `NodeViewModel` 或可变 UI 集合类型。单步和失败恢复接口直接接收 `NodeModel`；ViewModel 只负责把当前选择映射为模型并触发命令，从而保持执行规则向 Runtime/Domain 单向依赖。

`FlowDocumentSession` 同样通过节点模型快照生成保存文档和撤销快照，不引用 `NodeViewModel`。加载与快照恢复仍通过 `Action<NodeModel>` 和清空回调进入画布模块，由画布统一负责节点包装、附加/卸载和事件订阅生命周期。

动态节点插件通过 `FlowEditor.PluginContractFixture` 独立程序集做发布 seam 验收。测试项目仅以 `ReferenceOutputAssembly="false"` 构建夹具，不编译引用插件类型；运行时从 DLL 加载并验证工具发现、反射创建、生命周期回调、历史文档类型名还原和无界面执行。该结构用于捕获插件公开契约或程序集加载规则的回归。

仓库级构建与回归统一使用根目录的 `FlowEditor.sln`，其中包含 Runtime、WPF 编辑器、DataManager 日志 adapter、插件契约夹具、核心测试、宿主日志适配测试和 Runtime Smoke。旧 `Flow.sln` 仍是上层产品聚合入口；架构测试会验证独立解决方案内的每个项目路径真实存在。宿主日志测试只引用 adapter、Runtime 和 DataManager，不引用完整 WPFControl；核心测试仍保持无宿主反向依赖。

`FlowChartService` 通过构造参数接收 `IFlowLogSink`，`FlowExecutionCoordinator` 在完整执行、单步、失败恢复和循环执行时建立异步日志作用域。`FlowLogRecord` 一次性携带 `ProcessName`、`FlowName`、`NodeName`、`RunId`、`Message` 和 `Level`；校验警告、执行取消、重试/容错和失败分别使用匹配的日志等级，而不是全部降级为 `Information`。Runtime 的 `FlowLogRoute` 是流程日志身份的唯一值对象：运行序号以结构化字段相等性分桶，显示键与稳定摘要也由该模块生成，避免各调用方重复拼接分隔字符串。标签页关闭、文档替换、容器名变更和标签改名统一释放旧路由的进程级运行序号；活动执行作用域持有可脱离全局字典的计数桶，因此异步收尾不会污染随后重开的同名任务。宿主构造后设置容器名时必须向已创建的首个标签页传播。

DataManager 集成位于宿主组合根：宿主 adapter 实现 `IFlowLogSink`，由 IoC 同时注入 `LogManager` 和 `LogConfigService`。构造依赖保证日志配置先加载；adapter 再按记录中的流程、任务身份查找或基于当前 `FlowLog` 模板创建 `ILog`，并通过显式 `FlowLogLevel -> ELogLevel` 映射写入。不得依赖枚举序号强转，以免未来一侧插入等级后静默错级。逻辑路由身份与物理目录映射必须分离：不得拆分扁平 `LogKey` 来生成目录；流程名和任务名分别作为单一路径段清理非法字符、`.`/`..` 与 Windows 保留名，发生规范化或截断时附加稳定摘要以避免不同名称落入同一目录，且单段长度上限为 96。该 adapter 以 `IFlowLogSink` 单例注册，FlowEditor 不应直接引用 DataManager。旧 `.flow` 文档中的 `Information`、`Warning`、`Reset`、`Error` 和 `SysError` 名称及数值保持不变。

独立的 `FlowEditor.DataManagerLogging` adapter 项目实现 `DataManagerFlowLogSink`，只依赖 Runtime、DataManager 及小型 `IFlowLogConfiguration` seam；WPFControl 的 `LogConfigService` 实现该配置接口，并在组合根把 sink 注册为 `IFlowLogSink` 单例。宿主日志测试不再引用完整 WPFControl，从而避免日志改动连带构建通信、视觉和设备模块。`LogConfigService.Load()` 会先完成配置预检与新实例构造，再调用 `LogManager.ReplaceConfigured()` 在管理器单一锁内验证所有权并原子替换基础日志；失败保留完整旧快照，成功提交后才释放旧实例。成功重载后，sink 会失效并释放已创建的动态流程日志，下次写入按新模板重建。动态键与现有基础日志重名时自动使用 `FlowLogRoute.StableId` 隔离。日志后端异常不得改变流程结果，但必须经 30 秒限频的 `Trace` 备用通道报告；备用监听器自身故障同样被隔离。`FlowLogContext.FileSink` 及宿主静态赋值已删除，避免不同流程会话共享进程级可变日志路由。

端口值传递由 Runtime 的 `PortValueTransfer` 统一完成端口查找、声明类型与运行时值验证、稳定诊断及赋值。执行引擎仅在成功传递后追加 Journal，循环容器仅筛选跨边界数据连接，避免两条执行路径产生不同的数据语义。

## Phase 4 应用模块

| 模块 | 拥有的实现 | 对外使用方式 |
| --- | --- | --- |
| `FlowExecutionCoordinator` | 校验、单次/循环运行、取消、调试、失败恢复、运行日志和状态收尾 | 通过 `NodeModel` 快照和只读连线工作，ViewModel 只负责编排 |
| `FlowDocumentSession` | 保存、加载、快照、节点 DTO 映射、重建事务和错误反馈 | 通过 `NodeModel` 快照读取，借助画布回调恢复节点生命周期 |
| `FlowCanvasSession` | 节点附加/卸载、容器归属、拖动成员、作用域连线、缩放和自适应视口 | 只接收 `NodeModel` 快照，由 `FlowCanvasViewModelAdapter` 转换节点投影 |
| `FlowOutputBuffer` | 并发缓冲、容量限制、节流发布、尾批刷新和清空 | ViewModel、执行及画布模块复用 |

这些模块属于 Application，实现保持 `internal`。它们只有一个实际实现，因此不额外创建浅接口；执行与文档模块由 ViewModel 直接编排，模型级画布模块通过同层的 `FlowCanvasViewModelAdapter` 维护 `NodeViewModel` 投影。架构测试禁止操作转发重新膨胀回 `FlowChartService`，并禁止三个应用模块重新引用 ViewModel。`IFlowChartService` 只作为节点模型的生命周期 seam；节点复制使用文档模块，ToolBlock 仅通过节点上下文重建内部节点。

`FlowChartViewModel` 按变化原因组织为 partial 文件：主文件只保留依赖组装和节点编辑入口；`Interaction`、`Clipboard`、`Execution`、`Documents`、`Connections`、`Presentation` 和 `Commands` 分别承载交互状态、剪贴板事务、运行恢复、文档事务、连线提交、表现层入口和命令初始化。partial 只改善代码局部性，不形成新的公共 seam；实际业务仍委托给上述 Application 模块。容器几何、拖动成员展开、执行容器/泳道归属及越界连线清理由平台无关的 `FlowCanvasSession` 统一拥有；`FlowCanvasViewModelAdapter` 只负责模型与现有节点投影集合之间的转换。

`NodeModel` 按变化原因拆为命名 partial：`NodeModel.DataBindings.cs` 集中画布生命周期、连线订阅、上游解析和数据绑定；`NodeModel.Ports.cs` 集中标准/动态端口集合、编辑与配置；`NodeModel.PortValues.cs` 集中端口查询、执行前后属性同步和反射缓存；`NodeModel.PropertyGrid.cs` 集中属性面板接口声明和扩展命令入口。主文件只保留核心状态、节点动作与执行契约，已由 1,063 行降至 330 行。PropertyGrid 扩展只依赖纯 `net9.0` 的 `WPFCustomControls.PropertyGrid.Core` 契约程序集，不引用控件实现；partial 不改变节点插件的继承入口、原 PropertyGrid 接口识别和 `OnAttached(IFlowChartService)` 契约。

## 目标依赖方向

```text
Presentation -> Application -> Runtime/Domain
                         |
                         v
                   Infrastructure
```

- `Runtime/Domain`：节点执行契约、端口、连线、流程编译、校验和执行算法；不依赖 WPF、Prism、ViewModel 或具体窗口。
- `Application`：流程编辑、执行会话和文档用例；只通过 seam 使用文件、UI 线程封送、节点编辑器和日志落盘。
- `Infrastructure`：文件存储、WPF Dispatcher、窗口启动和外部日志等 adapter。
- `Presentation`：WPF Views、ViewModels、行为、渲染和资源。
- `Composition`：唯一允许注册具体 adapter 和调用 Prism `Resolve` 的位置。

P6 输出筛选保持在表现层：`FlowOutputPaneView` 从原始文本派生级别过滤和搜索结果，并独立控制自动滚动。过滤不修改 `FlowOutputBuffer`、执行编号或文件日志格式；当前 200,000 字符容量下不引入结构化日志集合和虚拟化列表。

P5 已完成平台边界拆分。WPF 中保留的 `FlowInstance` 与 `ReflectionNodeFactory` 是具体 `NodeModel` 的兼容门面和物化 adapter；通用运行状态位于 Runtime，不要求无头宿主引用编辑器节点基类。

## 模块设计规则

- 模块应通过较小的接口隐藏复杂实现，不创建纯转发的浅模块。
- 只有存在生产和测试两个真实 adapter 时才引入接口。
- `FlowChartExecutionEngine` 和 `ConnectionService` 是进程内纯算法，暂不为它们增加接口。
- 文件存储、UI Dispatcher、节点编辑器、标签页工厂和外部日志是实际 seam，应由 Application 定义接口。
- ViewModel 负责编排，不重复连接、校验、执行或持久化规则。
- View 只负责呈现、坐标、命中测试和 WPF 生命周期。
- 节点不得创建 Window、MessageBox、文件对话框或解析 IoC 容器。

## 连接流程

```text
PortConnectorView（开始拖动）
  -> FlowConnectionDragController（邻近查找、吸附、坐标换算）
  -> FlowChartViewModel（交互编排、撤销快照）
  -> ConnectionService（校验并规划连接）
  -> Models（持久状态）
```

新增连接规则应进入 `ConnectionService`，并通过其返回的 `ConnectionPlan` 测试。新增端口视觉行为应进入 Presentation。规则和视觉不得在 ViewModel 中重复实现。

## 端口类型颜色

| 类型 | 颜色语义 |
| --- | --- |
| `Flow` | 紫罗兰色 |
| `Boolean` | 粉红色 |
| `Integer` | 亮绿色 |
| `Double` | 浅灰色 |
| `String` | 浅蓝色 |
| `Image` | 橙色 |
| `Object` | 靛蓝色 |
| `Any` | 深灰色 |

节点属性通过 `PortTypeClassifier` 自动映射端口类型；特殊控制流端口应显式声明为 `PortType.Flow`。
输入端口使用圆形，输出端口使用菱形，因此方向和数据类型分别通过形状和颜色表达。
普通数据连线沿用源端口类型颜色；执行中、选中和控制流连线由状态样式覆盖。

## 架构完成条件

- `Core/Runtime` 不引用 Views、ViewModels、Prism 或 WPF。
- `Nodes` 不引用具体窗口或 WPF。
- Prism 解析仅存在于 Composition。
- Runtime 可以作为纯 `net9.0` 项目独立构建和测试。
- `.flow`、`.flows` 和现有节点插件保持兼容。
