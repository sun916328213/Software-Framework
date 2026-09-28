# Vision 优化项（2026-09-28）

> **P10 更新（2026-09-28）**：本文件原「修 `InvIToolMgr`」方向**已作废**。P10 审计确认
> `Vision.InvI.InvIToolMgr` 属于**已被 `InviHelper` 替代的旧链**，继续修它等于长期维护两套 native
> 协议、两套模型状态、两套推理结果结构。P10 改为 **旧栈删除 + 新 `InviHelper` 生命周期收口**。
> 下方「原始诊断」作为历史记录保留，实际处置见紧随其后的「P10 处置」。

## P1：InvI 模型生命周期 —— 旧栈已退场，现行链已收口

### 原始诊断（P10 前记录，方向已作废）

**Symptom**：`Vision/AI/Invi/InvIToolMgr.cs` 的目标检测分支在 `CreateInferAPI(...)` 返回 `true` 时抛“创建失败”（约 193–203 行），与分类、分割分支的判断相反；异常文案还误称 `Segmentation`。`DeleteInvICount` 在执行 `DestoryInfer` 和移除列表后，无条件抛“Delete Success!”异常（约 438–467 行）。加载时写入 `m_dictInvIUsing`（约 155、176、201 行），删除时未清理该映射。`DPLibrary_Halcon_F48/Tools/AI/Invi/InvIToolMgr.cs` 是逐字相同的第二份源码，问题同步存在。

**Source**：`Code Complete` 的错误路径与防御式编程；`The Pragmatic Programmer` 的 DRY（同一生命周期决策有两个副本）。

**Consequence**：成功创建检测模型可能被当成失败；成功删除仍向调用方报告异常；同一路径再次加载时可能因字典中旧键导致异常。两份实现若只改一份，会产生不同程序集间的行为漂移。

**触达范围**：仓库内能找到分类和分割的 `AddInvIClient` 调用，暂未找到检测分支或 `DeleteInvICount` 的直接调用；公开类型仍可能由外部程序集使用。故这是源码可确认的错误，实际现场触发频率待确认。

**原 Remedy（已作废）**：先为原生 `CreateInferAPI`/`DestoryInfer` 引入可替换调用边界，覆盖成功、失败、删除、同路径重新加载四种行为；再纠正检测分支条件与异常文案，删除时同步更新列表和使用状态映射，成功路径正常返回，并保留原始异常作为内部异常。核对两程序集的消费者后，选择单一实现或共享源码，避免直接删除仍在使用的副本。

> 作废理由：该 Remedy 的前提是「旧栈仍要维护」。P10 判定旧链不是新链的兼容适配层，而是一套**独立实现**，因此不再投入修复成本。

**原验收（已作废）**：无硬件单元测试可模拟原生返回值；有 SDK 的 Windows 环境再做真实模型加载/释放验证。此文档只记录诊断，尚未修改实现。

### P10 处置（2026-09-28 执行）

**方向**：不修旧栈，直接删除；只对现行单链 `AITool -> InviHelper -> INVI API.dll` 补生命周期、失败回滚与自动化测试。

| 计划项 | 动作 | 结果 |
| --- | --- | --- |
| P10-1 | 旧栈引用审计 + 删除闸门 | 全仓无反射字符串、无序列化类型名、无 XAML 引用；旧四类构成**闭合孤岛**（只互相引用）；`InvI.dll` 字面量只出现在旧 `InvIToolMgr` 内；无 nuspec / 无打包脚本 / 无仓外发布通道 ⇒ 判定**允许直接物理删除** |
| P10-2 | 删 `DPLibrary_Halcon_F48/Tools/AI/Invi/` 下四文件 | 已删 `InvIHelpr1.cs` / `InvIClassTool.cs` / `InvISegmentationTool.cs` / `InvIToolMgr.cs`，连带其内部独有的 `BaseParam` / `RunParam` / `ModelType` / 旧 `Bbox` / 旧 `InferResult`。**不做 shared source**，删除后不再维护 `InvI.dll` 旧协议 |
| P10-3 | 强化现行 `InviHelper` 生命周期 | ① 模型注册改为 **native 成功后才提交 managed 状态**；② 新增 `ReleaseModel(path)` / `ResetModel()`，卸载成功才移除 managed 状态，失败保持状态并上抛；③ 实现 `IDisposable`（释放模型 + 销毁管理器，**幂等且不抛**）；④ `AITool.Config()` 换 helper 前先释放旧实例，新 helper 初始化失败时回滚；⑤ 初始化失败不再空 `catch`，异常带 DLL 名 / 阶段并保留 `InnerException`；⑥ 抽最小 `IInviNativeApi` 适配层（只覆盖 manager/model 创建销毁与 infer） |
| P10-4 | 无 DLL 生命周期测试 | 新增 `Vision/Vision.Tests`（`net9.0-windows7.0` / Any CPU），26 个用例，全部基于 fake native API；**不要求真实 `INVI API.dll` / GPU / 模型文件** |
| P10-5 | `Vision/Vision` 历史副本 + 工程卫生 | 删除 `Vision/Vision/AI/Invi/` 下逐字相同的四份副本（目录已随之移除）；`Vision.sln` 加入 `Vision.Tests`，并清理全部无意义的 `x64` solution alias（只保留 `Debug|Any CPU` / `Release|Any CPU`） |

**现行唯一实现**：`AITool` 的 Invi 路径只创建 `InviHelper`（经 `AITool.CreateAIHelper()` 注入点），由 `AIToolConfigOnlyUsesInviHelper` 锁定。

**不再维护**：`InvI.dll` 旧 P/Invoke 协议。仓内 `"InvI.dll"` 字面量已清零，由 `LegacyInviDllProtocolIsGone` 锁定。

### 测试矩阵落点（对应计划 §7）

| 场景 | Fake 行为 | 预期状态 | 预期结果 | 测试用例 |
| --- | --- | --- | --- | --- |
| Manager 创建成功 | `CreateManager != 0` | helper 可用 | `InitModel` 可继续 | `ManagerCreated_HelperUsable_InitModelSucceeds` |
| Manager 创建失败（返回 0） | `CreateManager == 0` | 无已加载模型 | 明确异常，释放 native 句柄 | `ManagerCreationReturnsZero_ThrowsWithContext_AndReleasesNative` |
| Manager 创建抛异常 | `CreateManager` 抛 | 无已加载模型 | 异常带上下文，保留根因 | `ManagerCreationThrows_PreservesRootCause` |
| 模型创建成功 | `CreateModel = true` | `_loadedModels` 含 path | `InitModel` 成功 | `ModelCreationSucceeds_RegistersManagedState` |
| 模型创建失败 | `CreateModel = false` | `_loadedModels` 不含 path | 抛失败；**同 path 可再次重试** | `ModelCreationFails_LeavesNoManagedState_AndSamePathCanRetry` |
| 模型创建抛异常 | `CreateModel` 抛 | `_loadedModels` 不含 path | 状态不被污染 | `ModelCreationThrows_LeavesNoManagedState` |
| 重复加载 | 第二次同 path | 状态仅一份 | 不重复调用 native `CreateModel` | `DuplicateLoad_SingleManagedEntry_WithoutExtraNativeCall` |
| 释放模型成功 | `DestroyModel = true` | 移除 path | 之后允许重新加载 | `ReleaseModelSucceeds_RemovesPath_AndAllowsReload` |
| 释放模型失败 | `DestroyModel = false` | path 保持 | 错误上抛，**不伪装为已释放** | `ReleaseModelFails_KeepsManagedState_AndDoesNotPretendReleased` |
| 释放模型抛异常 | `DestroyModel` 抛 | path 保持 | 保留根因 | `ReleaseModelThrows_PreservesRootCause_AndKeepsManagedState` |
| 释放未加载路径 | 不触碰 native | path 不存在 | 返回 `false` | `ReleaseModel_UnknownPath_ReturnsFalse_WithoutTouchingNative` |
| 重置全部模型 | 全部成功 | 状态清空 | 返回空失败列表 | `ResetModel_ReleasesAll_AndReportsNoFailure` |
| 重置部分失败 | 部分 `DestroyModel = false` | 失败 path 保留 | 返回值报告失败 path | `ResetModel_PartialFailure_KeepsFailedPathAndReportsIt` |
| 重复 Config | 创建第二个 helper | 旧 helper 已 `Dispose` | 只保留一个 native manager | `RepeatedConfig_DisposesPreviousHelper_KeepsOnlyOne` |
| Config 初始化失败 | `InitModel` 抛 | 不留 helper | 失败 helper 已回滚释放 | `Config_InitFailure_RollsBackFailedHelper_AndLeavesNoHelper` |
| Invi 品牌装配 | 走真实 `CreateAIHelper()` | — | 使用 `InviHelper`（本机无 DLL 时以异常消息含 DLL 名兜底证明） | `InviBrand_CreatesInviHelper` |
| Dispose 两次 | `DestroyManager` | 只销毁一次，状态为空 | 幂等、不抛 | `DisposeTwice_Idempotent_DestroysManagerOnce_AndClearsState` |
| Dispose 时销毁失败 | `DestroyManager = false` | 状态清空 | 不抛 | `Dispose_WithDestroyManagerFailure_DoesNotThrow_AndClearsState` |
| Dispose 时模型释放失败 | `DestroyModel = false` | 仍销毁 manager | 不抛 | `Dispose_WithModelReleaseFailure_DoesNotThrow_AndStillDestroysManager` |
| Dispose 后调用 | — | — | 抛 `ObjectDisposedException` | `AfterDispose_OperationsThrowObjectDisposed` |
| 旧类退场 | 编译 / 架构扫描 | 无正式项目引用旧类 | 旧 API 不再被新代码引回 | `DPLibrary_Halcon_F48_NoLongerContainsLegacyInviTypes` / `LegacyVisionInvINamespaceIsGone` / `LegacySourceFilesAreDeleted` / `AIToolConfigOnlyUsesInviHelper` / `NoSourceFileReferencesLegacyInviTypes` / `LegacyInviDllProtocolIsGone` |

### 验收

- **自动化（本机无 INVI API.dll）**：`dotnet test Vision/Vision.Tests/Vision.Tests.csproj -c Debug` ⇒ **26/26 通过**。测试全程不加载真实 DLL、不需要 GPU 或模型文件。
- **变异验证**：曾故意注入两处变异（① `CreateInferAPI` 先提交 managed 再调 native；② `Config` 不释放旧 helper），恰好 3 条预期用例变红且无误报，随后已完整还原。
- **人工（待有 INVI API.dll 的环境，尚未执行）**：计划 §9 要求对**真实厂商语义**的 `DestroyModel` / `DestroyManager` 做一次「加载 → 释放 → 重载」验证。本机无该 DLL，此项**未完成**，故「真实 DLL 语义正确」仍属未验证假设。

## P1：视觉验证入口 —— 已建立（部分覆盖）

五个 Vision 项目中原无独立 `*Tests.csproj`，`Vision.sln` 与 `Frame.sln` 的构建不验证模型生命周期和 HALCON 资源释放。

**P10 处置**：已建立 `Vision/Vision.Tests`（`net9.0-windows7.0` / Any CPU / `AppendPlatformToOutputPath=false`），覆盖 InvI 生命周期状态机与旧栈退场守卫，并纳入 `Vision.sln`。

**仍未覆盖**：HALCON 资源释放路径、`Ctr_WPF_F48` / `VisionDemo` 的界面与绑定、以及其它算法工具。这些的测试面仍待评估，不要在「构建通过」上推导出「生命周期已验证」的结论。

**`Vision/Vision.csproj` 归属仍未定**：它不在 `Frame.sln`，只在 `Vision.sln`；其 `AI/Invi` 历史代码已随 P10-5 删除，但该工程本身是否仍在交付**需产品侧确认**。现有 `DPLibrary_Halcon_F48` 的测试不能替它作验证结论。
