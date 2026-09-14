# FlowEditor 架构

> 本文档描述当前架构和必须遵守的目标依赖方向。完整迁移步骤见
> [`ARCHITECTURE_OPTIMIZATION_PLAN.md`](ARCHITECTURE_OPTIMIZATION_PLAN.md)。

## 当前状态

FlowEditor 当前由 `FlowEditor.Runtime`（纯 `net9.0`）、`FlowEditor.Runtime.Smoke`（无界面控制台验收）和 `FlowEditor`（`net9.0-windows` WPF）组成。P5 已把运行节点与结构化作用域契约、端口、连线、文档 DTO、程序定义、图编译、校验、调试控制、执行追踪、运行实例、执行引擎及循环/并行 runner 迁入 Runtime；WPF 项目单向引用 Runtime。

```text
Views -> ViewModels -> IFlowChartService（兼容门面）
                           |
             +-------------+-------------+
             v             v             v
     FlowCanvasSession  FlowDocumentSession  FlowExecutionCoordinator
             |             |             |
             +-------------+-------------+
                           v
             FlowEditor.Runtime + Editor Models / Nodes

Infrastructure adapters -> Application seams
```

当前已知的历史依赖包括：

- `Core` 已禁止引用 `ViewModels` 和 Prism；节点 factory 使用实例注册表，不再持有宿主级静态 resolver。
- `IFlowChartService` 仍暴露 `NodeViewModel`，但已不再暴露 Prism 容器；文档存储 seam 也不暴露 ViewModel。
- `NodeModel` 持有 `IFlowChartService`，用于查询和修改画布连接。
- 节点编辑、信息提示和动作命令已通过 Presentation adapter 与 WPF 隔离。
- `FlowChartService` 保留兼容门面和依赖组装；执行、文档、画布生命周期分别由内部深模块拥有实现。
- `FlowOutputBuffer` 拥有输出缓冲、容量截断、节流、尾批刷新和清空一致性。

这些依赖是待消除的迁移基线，不是允许新增依赖的架构规则。架构约束测试使用显式 allowlist 锁定现有债务；新增同类依赖必须失败。

当前迁移进展：执行引擎只依赖 `INodeExecutable` 和 `IExecutionLogContext`；静态校验和顶层图只依赖 `IFlowValidationNode`、`IParallelForkNode`、`IExecutionScopeDescriptor` 等语义契约，不识别任何编辑器具体节点类。桌面端以 `FlowLogExecutionContextAdapter` 保留流程、节点及运行序号日志，无头宿主使用无副作用默认实现。`FlowRuntimeInstance<TNode>` 拥有活动图筛选、作用域注入、调试、追踪和执行，WPF `FlowInstance` 仅保留具体节点物化、属性通知和流程日志作用域适配。并行泳道、嵌套容器和运行实例使用独立引擎状态，避免共享运行序号竞争。输出栏由 `FlowOutputBuffer` 隐藏并发缓冲、容量截断和节流细节，通过 `IUiDispatcher` 连接 WPF 或无头宿主。`FlowDocumentMapper` 统一单流程、多流程、快照和节点复制的 DTO 转换，`IFlowDocumentStore` 通过 JSON 与内存 adapter 隔离存储介质。标签页和工具箱节点创建分别使用 `IFlowTabFactory` 与 `INodeFactory`；Prism 解析仅保留在组合入口和 WPF adapter。应用模块、ViewModel、工具箱模型和 ToolBlock 窗口已不再使用 Service Locator，测试通过硬约束防止依赖回流。

## Phase 4 应用模块

| 模块 | 拥有的实现 | 对外使用方式 |
| --- | --- | --- |
| `FlowExecutionCoordinator` | 校验、单次/循环运行、取消、调试、失败恢复、运行日志和状态收尾 | 由 `FlowChartService` 兼容门面委托 |
| `FlowDocumentSession` | 保存、加载、快照、节点 DTO 映射、重建事务和错误反馈 | 由 `FlowChartService` 兼容门面委托 |
| `FlowCanvasSession` | 节点附加/卸载、事件订阅释放、批量清理、缩放和自适应视口 | 由 `FlowChartService` 兼容门面委托 |
| `FlowOutputBuffer` | 并发缓冲、容量限制、节流发布、尾批刷新和清空 | 由执行及画布模块复用 |

这些模块属于 Application，实现保持 `internal`。它们只有一个实际实现，因此不额外创建浅接口；测试继续从现有 `IFlowChartService` seam 验证可观察行为，并用架构测试防止职责回流。节点复制和 ToolBlock 恢复所需的节点 DTO 转换也由文档模块完成，门面不直接持有 mapper。

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
  -> FlowChartView（邻近查找、吸附、坐标换算）
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
