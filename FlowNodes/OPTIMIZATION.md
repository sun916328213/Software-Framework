# FlowNodes 优化项（2026-09-28）

## P1：节点库的验证与依赖半径

**Symptom**：`FlowNodes/FlowNodes/FlowNodes.csproj` 直接引用 Communication、DataManager、Device.Core、Extension、FlowEditor、ScriptEditor、Secs、两个 Vision 项目和 WPFCustomControls，共 10 个项目；模块有 Workbench，但没有 `FlowNodes.Tests.csproj`。整机宿主通过 `Assembly.Load("FlowNodes")` 发现节点，因而节点 DLL 是否进入输出目录也是运行时契约。

**Source**：`Software Engineering at Google` 的依赖管理；`Working Effectively with Legacy Code` 的测试接缝。

**Consequence**：改动任何一个厂商/协议节点可能触发整个节点库的构建与装配验证；仅凭编译成功不能证明插件发现、旧流程文档还原和无设备运行行为。

**Remedy**：先在模块侧加入不接硬件的契约测试，覆盖节点发现、实例化、历史标识还原和依赖缺失诊断，并用 Workbench 做装配冒烟。统计各领域节点的实际引用与变更频率后，再决定是否按通信、视觉、设备拆出独立插件程序集；拆分前保留当前节点标识与宿主加载契约。

**验收**：在 `Any CPU` 下运行模块测试与 Workbench 冒烟；整机宿主仍能发现全部节点并打开既有流程文件。高引用数本身并非拆分理由。
