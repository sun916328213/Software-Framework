# Packing Machine 优化项（2026-09-28）

## P1：整机宿主缺自动化验收入口

**Symptom**：`Packing Machine/Packing Machine/Packing Machine.csproj` 是单一 WPF `WinExe`，直接引用 13 个项目，包含 FlowNodes 的运行时加载依赖；本模块没有测试项目。项目文件还明确排除了四份 `StartupFlow` 源码，实际启动脚本来自 `.taskproj`/配置文件。

**Source**：`How Google Tests Software` 的集成层测试；`Software Engineering at Google` 的依赖管理。

**Consequence**：项目构建只能证明静态引用可解析，不能证明启动配置、资源、节点 DLL、Prism 注册和无硬件诊断能在实际宿主中协同工作。配置资源遗漏可能到启动时才暴露。

**Remedy**：建立不连接设备的宿主启动冒烟路径，验证模块注册、关键资源/配置存在、FlowNodes 加载和故障诊断；再用设备模拟或测试机覆盖硬件初始化。保持现有直接项目引用，因它们已明确表达编译和运行时依赖。

**验收**：干净输出目录中，在 `Any CPU` 下构建并启动到可诊断状态；确认 FlowNodes DLL 与配置文件均在输出中。硬件行为仍须在目标设备上验收。
