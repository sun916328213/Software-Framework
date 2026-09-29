# ADR-0005 测试分层与旧宿主冻结

状态：已实施（P6–P7）。

背景：`Script.Tests`（WPF Exe）既是人工宿主又是自动测试载体，CI 无法运行。

决策：
- 新增 `ScriptEditor.Tests`（`net9.0`，xUnit，`dotnet test`）：只覆盖 Core/Runtime，
  不依赖 WPF；性能阈值收敛为 CI 回归线，人工观察留在 Workbench/旧宿主。
- 旧宿主冻结：不再新增用例，仅保留 `--core/--ui` 手工与性能报告能力；
  其 CoreTests 与新工程重复部分不再同步。
- 统一 Composition Root：`ScriptEditorModule` 注册 `IScriptRuntime` →
  `RoslynScriptRuntime.Shared` 实例；`ScriptManagerService` 收敛为单一三参构造，
  `ScriptEditorService` 持有 `IScriptRuntime` 契约并允许注入（默认仍为共享实例）。

后果：自动测试与人工宿主分离；Workbench（`--smoke` 存活自检）为独立演示入口；
未来 ScriptWorker 只需实现 `IScriptRuntime` 并在组合根替换注册。
