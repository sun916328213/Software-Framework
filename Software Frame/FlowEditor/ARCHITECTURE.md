# FlowEditor 当前架构与维护入口

此文描述当前工作区的实际实现，不是对已缺失的2026-10-03中心报告的复原，也不声称完成全仓重构。根解决方案为仓库根的 `Software Frame.sln`；独立模块入口为本目录 `FlowEditor.sln`。

## 依赖与职责

- FlowEditor.Core：节点/端口执行契约、文档DTO、结构校验、资源语义；不依赖WPF或厂商SDK。
- FlowEditor.Runtime → Core：FlowChartExecutionEngine、已编译图、PortValueTransfer、取消/失败/调试；不反向依赖Controls。
- FlowEditor.Controls → Core/Runtime、WPFCustomControls：现有WPF NodeModel、画布/连线、会话、文档事务和Prism扩展。当前NodeModel仍带WPF，不把依赖它的插件称为纯Core。
- FlowEditor.Workbench：通用离线宿主与装配；FlowEditor.DataManagerLogging为按需日志适配。
- 独立插件夹具通过ReferenceOutputAssembly=false和Assembly.LoadFrom验证发现、物化、生命周期和文档恢复，不能退化为同程序集假插件测试。

## 文档与扩展

FlowDocumentMapper负责原文档/节点标识/端口映射，ReflectionNodeFactory保留旧反射兼容行为。实现INodeDocumentState的节点严格捕获/恢复自己的配置，错误不能静默跳过。FlowDocumentSession先准备完整候选再替换画布；JsonFlowDocumentStore用同目录临时写入/替换保留旧文件，不承诺任意文件系统的断电原子性。

流程组打开先完整物化所有候选页，失败等待候选节点释放并保留旧页；成功才提交。DocumentProperties原样恢复，保存/打开不绕过运行/文档锁，关闭等待原容器命令。宿主可通过SetToolboxItemsAsync提供目录投影，原发现/分组/搜索及界面继续共用。

宿主可提供IFlowNodeCatalog、INodeFactory、INodeEditorHandler和IFlowDocumentStore；仅替换必要边界，不另建调度器。通用文档允许宿主扩展状态，但模块本身不理解视觉模型、相机或设备业务。

Vision.Workbench像FlowNodes.Workbench一样整页复用FlowChartContainerView及其操作栏/工具箱/标签页/属性/输出，视觉完整编辑器按需开窗；Vision.FlowNode只适配原工具会话及原属性元数据；视觉文档和模型边界见[视觉实施计划](../Vision/Vision.Controls/docs/IMPLEMENTATION_PLAN.md)。生产宿主/业务归属见[WPFControls架构](../WPFControls/ARCHITECTURE.md)。

## 生命周期与验证

视图卸载只退订，不等于工具或流程释放。拥有者在实际调用结束后归还会话；取消不能伪装SDK已中断。失败清理需继续尝试其它资源并保留错误。输入/结果借用与拥有者须按插件明确声明。

Tests/FlowEditor.Tests为Core/Runtime逻辑验证，Tests/FlowEditor.Workbench.Tests验证WPF、文档、独立插件、装配及源码边界。解决方案项目与SolutionItems必须引用真实存在的文件；更新维护入口时保留这项校验，不能创建空占位历史报告或以Skip求绿。测试结果及真实设备验收严格分开，未授权不启动生产流程。

已知兼容限制：Vision.Controls仍保留三个旧公开视觉任务/节点类型，受视觉架构守卫约束；旧类型身份与P6兼容收口并不意味着当前流程依赖已消失。缩放/镜像和真实SDK能力不由通用执行器作推断。
