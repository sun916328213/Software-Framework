# Vision 优化项（2026-09-28）

## P1：InvI 模型生命周期

**Symptom**：`Vision/AI/Invi/InvIToolMgr.cs` 的目标检测分支在 `CreateInferAPI(...)` 返回 `true` 时抛“创建失败”（约 193–203 行），与分类、分割分支的判断相反；异常文案还误称 `Segmentation`。`DeleteInvICount` 在执行 `DestoryInfer` 和移除列表后，无条件抛“Delete Success!”异常（约 438–467 行）。加载时写入 `m_dictInvIUsing`（约 155、176、201 行），删除时未清理该映射。`DPLibrary_Halcon_F48/Tools/AI/Invi/InvIToolMgr.cs` 是逐字相同的第二份源码，问题同步存在。

**Source**：`Code Complete` 的错误路径与防御式编程；`The Pragmatic Programmer` 的 DRY（同一生命周期决策有两个副本）。

**Consequence**：成功创建检测模型可能被当成失败；成功删除仍向调用方报告异常；同一路径再次加载时可能因字典中旧键导致异常。两份实现若只改一份，会产生不同程序集间的行为漂移。

**触达范围**：仓库内能找到分类和分割的 `AddInvIClient` 调用，暂未找到检测分支或 `DeleteInvICount` 的直接调用；公开类型仍可能由外部程序集使用。故这是源码可确认的错误，实际现场触发频率待确认。

**Remedy**：先为原生 `CreateInferAPI`/`DestoryInfer` 引入可替换调用边界，覆盖成功、失败、删除、同路径重新加载四种行为；再纠正检测分支条件与异常文案，删除时同步更新列表和使用状态映射，成功路径正常返回，并保留原始异常作为内部异常。核对两程序集的消费者后，选择单一实现或共享源码，避免直接删除仍在使用的副本。

**验收**：无硬件单元测试可模拟原生返回值；有 SDK 的 Windows 环境再做真实模型加载/释放验证。此文档只记录诊断，尚未修改实现。

## P1：视觉验证入口

五个 Vision 项目中没有独立 `*Tests.csproj`。`Vision.sln` 与 `Frame.sln` 可构建部分项目，但构建不会验证模型生命周期和 HALCON 资源释放。建议先从上述 InvI 生命周期与一个资源释放路径建立测试，再决定其他算法的测试面。`Vision/Vision.csproj` 不在主解决方案中，需确认它是仍在交付的库还是独立/历史入口；现有 `DPLibrary_Halcon_F48` 的引用不能替它作验证结论。
