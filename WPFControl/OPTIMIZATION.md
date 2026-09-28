# WPFControl 优化项（2026-09-28）

## P1：业务服务对容器定位的依赖

**Symptom**：当前 `WPFControl/WPFControl` 下 48 个 `.cs` 文件引用 `IContainerProvider`。例如 `ProgramControl/Services/Device/CamManagerService.cs:28-30` 在构造函数内解析 `CamManager`；`ProgramControl/Services/Flow/StartupFlowService.cs:36-38` 解析 `ScriptManagerService`。既有根目录报告 `WPFControl-架构分析与优化方向.md` 已将此列为阶段二待办，本次从当前源码复核仍存在。`WPFControl.Tests` 现已有架构、迁移等测试，因此不沿用旧报告“只有一个测试”的历史数字。

**Source**：`Clean Architecture` 的依赖反转；`Working Effectively with Legacy Code` 的 Seam Model。

**Consequence**：构造一个单独服务要准备容器和大量注册，依赖关系由运行时解析才显露。相机、脚本和页面状态问题更难在不启动 WPF 宿主的条件下定位。

**Remedy**：先将 `StartupFlowService` 改为显式接收 `ScriptManagerService`，再选 `CamManagerService` 的相机管理依赖作为下一例；保留 `WPFControlModule.RegisterTypes` 作为组合入口，逐个迁移而不是一次替换所有单例。对每项迁移补行为测试，并在 `Packing Machine` 实际宿主验证注册和生命周期。

**验收**：服务测试无需创建 Prism 容器；原模块入口和现有调用方编译、宿主启动仍正常。单例生命周期需逐项判断，不能仅凭 `RegisterSingleton` 数量推断错误。
