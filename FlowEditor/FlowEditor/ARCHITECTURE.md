# FlowEditor 架构

FlowEditor 采用 MVVM，并按依赖方向组织为四类模块：

```text
Views / ViewModels  ->  Services  ->  Models / Core
         |                 |
         +------ Interfaces+
```

- `Models`：节点、端口、连线等领域状态，不依赖 WPF 视图。
- `Core`：流程编译、校验与执行算法。
- `Services`：编辑器用例。连接规则统一由 `ConnectionService.PlanConnection` 暴露，内部封装方向、类型、重复连接、单输入、环路和自动绑定规则；它只返回不可变计划，不直接修改界面状态。
- `ViewModels`：命令、选择与交互状态编排；不重复实现领域规则。
- `Views`：WPF 呈现及坐标/命中测试。`PortConnectorView` 封装端口命中区、视觉反馈和类型颜色。

## 连接流程

```text
PortConnectorView（开始拖动）
  -> FlowChartView（邻近查找、吸附、放大）
  -> FlowChartViewModel（交互编排、撤销快照）
  -> ConnectionService（校验并规划连接）
  -> Models（持久状态）
```

新增连接规则应放入 `ConnectionService` 并通过其公开结果测试；新增端口视觉行为应放入 `PortConnectorView`。这样调用方只依赖小接口，规则和视觉变化各自保持局部化。

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
输入端口使用圆形，输出端口使用菱形，因此方向和数据类型分别通过形状、颜色表达。
普通数据连线沿用源端口的类型颜色；执行中、选中和控制流连线由状态样式覆盖。
