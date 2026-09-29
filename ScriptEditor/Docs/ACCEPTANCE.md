# ScriptEditor 优化最终验收（P7，对照计划第 9 节）

日期：2026-09-29。基线：test 分支。以下每项均有证据，无“无调用者/无残留”类断言未经核验。

| # | 标准 | 状态 | 证据 |
|---|---|---|---|
| 1 | Core 无 WPF/RoslynPad/Prism 引用；Runtime 无 WPF 控件引用 | 通过 | 全仓 grep：Core 下零命中；Runtime 仅注释与可选程序集名字符串；csproj：Core `net9.0` + CommunityToolkit，Runtime + RoslynPad.Roslyn |
| 2 | ScriptItem 不持有 ScriptEngine/Window/Dispatcher/MetadataReference | 通过 | `Core/Models/ScriptItem.cs` 仅 doc 提及契约；视图状态（光标/滚动）`JsonIgnore` 不持久化 |
| 3 | 每个活动脚本只有一个 ScriptSession 事实源 | 通过 | `ScriptEditorModel` 无运行状态（仅注释提及）；会话经 `RoslynScriptRuntime` 按脚本对象关联；`SameItemReusesSession` |
| 4 | View 不再承担 Runtime/持久化编排，体积显著降低 | 通过 | `ScriptEditorView.xaml.cs` 1287 → 490 行；RoslynPad/生命周期/会话同步分别在 Adapter/Binder/Lifecycle；`RenameSymbol` 对话框入 Service，文件读写入 Runtime |
| 5 | 不依赖隐式 ContainerLocator；生命周期可描述可测试 | 通过 | `ContainerLocator` 仅出现在 Workbench/旧宿主组合根；`ScriptManagerService` 单一三参构造；`ScriptEditorService` 持 `IScriptRuntime` 契约可注入 |
| 6 | .taskproj 有 FormatVersion，旧格式有兼容/迁移入口 | 通过 | V2 `FormatVersion: 2`，`ScriptProjectMigration.Migrate`（V1 旧形状迁移）；`StoreLoadsLegacyFile`、`StoreRotatesBackup` |
| 7 | 不同 ScriptProject 的 imports/reference 互不污染 | 通过 | `ScriptProjectContext` 按任务隔离；`ProjectContextsDoNotPolluteEachOther`、`ProjectContextUsingsTakeEffect`；用户引用共享缓存按路径+时间命中 |
| 8 | Core/Runtime 自动化测试；Workbench 可单独验证 UI | 通过 | `dotnet test` 40/40（无 WPF）；Workbench `--smoke` exit=0（P6 修复 DI 双构造故障后首次可独立启动） |
| 9 | 性能不低于 P0 基线 | 部分通过 | P0 未留下书面基线数，无法逐项对比；本轮以阈值固化为回归线（见 README），自动化 40/40 + 旧宿主 47/47 含长跑/洪泛/内存用例；首次初始化与 Tab 切页属人工项，由 Workbench/`--ui` 观察 |
| 10 | 第二后端/ScriptWorker 无需重写 Controls/Core 契约 | 通过 | 调用方只依赖 `IScriptRuntime`/`IScriptSession`；动态程序集不可卸载的评估与边界记入 `ScriptExecutionService` 与契约注释；ADR-0005 |

遗留与后续（非阻塞）：P0 书面基线缺失（建议下次性能观察时把 Workbench/`--ui` 数据落盘为正式基线）；
旧宿主已冻结（不再新增用例）；ScriptWorker 进程隔离未启动（见 ADR-0003/0005）。
