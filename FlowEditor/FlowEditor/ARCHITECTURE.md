# FlowEditor 架构

> 本文档描述当前架构和必须遵守的目标依赖方向。完整迁移步骤见
> [`ARCHITECTURE_OPTIMIZATION_PLAN.md`](ARCHITECTURE_OPTIMIZATION_PLAN.md)。

## 当前状态

FlowEditor 当前是单个 `net9.0-windows` WPF 类库。它采用 MVVM，但尚未形成完全单向的分层结构。

```text
Views <-> ViewModels <-> Services
  ^             ^          |
  |             |          v
Nodes <------ Core <---- Models
```

当前已知的历史依赖包括：

- `Core` 已禁止引用 `ViewModels` 和 Prism；节点 factory 使用实例注册表，不再持有宿主级静态 resolver。
- `IFlowChartService` 仍暴露 `NodeViewModel`，但已不再暴露 Prism 容器；文档存储 seam 也不暴露 ViewModel。
- `NodeModel` 持有 `IFlowChartService`，用于查询和修改画布连接。
- 节点编辑、信息提示和动作命令已通过 Presentation adapter 与 WPF 隔离。
- `FlowChartService` 仍同时承担执行、编辑、持久化和视口状态协调；输出缓冲、截断与节流已迁入 `FlowOutputBuffer`。

这些依赖是待消除的迁移基线，不是允许新增依赖的架构规则。架构约束测试使用显式 allowlist 锁定现有债务；新增同类依赖必须失败。

当前迁移进展：执行引擎只依赖 `INodeExecutable`；执行范围使用保留调用方类型的泛型子图；静态校验和文档构建只接收节点模型。引擎的调试前置回调只传递节点 ID，由 `FlowChartService` 映射回当前画布 ViewModel。`NodeViewModel` 不再实现任何运行时执行接口。输出栏由 `FlowOutputBuffer` 隐藏并发缓冲、容量截断和节流细节，通过 `IUiDispatcher` 连接 WPF 或无头宿主。`FlowDocumentMapper` 统一单流程、多流程、快照和节点复制的 DTO 转换，`IFlowDocumentStore` 通过 JSON 与内存 adapter 隔离存储介质。标签页和工具箱节点创建分别使用 `IFlowTabFactory` 与 `INodeFactory`；Prism 解析仅保留在组合入口和 WPF adapter。应用模块、ViewModel、工具箱模型和 ToolBlock 窗口已不再使用 Service Locator，测试通过硬约束防止依赖回流。

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

在依赖环被消除之前，先在当前项目内遵守上述命名空间方向；最终再抽取纯 `net9.0` 的 `FlowEditor.Runtime` 项目。

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
