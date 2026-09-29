# ScriptEditor（脚本平台组件）

WPF 工业自动化脚本平台：编辑 + 执行的可复用控件库。按 P1–P6 重构为四层：

| 项目 | 职责 | 禁止 |
|---|---|---|
| `ScriptEditor.Core` (`net9.0`) | 脚本数据模型、运行契约、结果/错误模型、版本化 DTO 与迁移 | WPF、Prism、RoslynPad、具体 Roslyn 类型、文件对话框 |
| `ScriptEditor.Runtime` (`net9.0`) | Roslyn 编译/执行、Session、引用解析与缓存、持久化实现 | WPF 控件、Dialog、ViewModel |
| `ScriptEditor.Controls` (`net9.0-windows`) | 可复用 WPF 编辑器、View/ViewModel、RoslynPad 适配 | 编译/执行核心算法、进程级全局状态、业务持久化细节 |
| `ScriptEditor.Workbench` (`net9.0-windows`, WinExe) | 独立演示、集成验证、性能观察、人工回归 | 承载可复用业务实现 |
| `ScriptEditor.Tests`（sln 显示名 `ScriptEditor.AutoTests`） | Core/Runtime 自动化测试（xUnit，`dotnet test` 可运行） | WPF、人工测试宿主 |
| `Script.Tests`（sln 显示名 `ScriptEditor.Tests`，已冻结） | 旧 WPF 测试宿主：core/ui/performance 手工验证与性能报告 | 新增自动测试请进 `ScriptEditor.Tests`；本宿主不再新增用例 |

核心对象：`ScriptItem`（纯数据，随工程持久化）→ `IScriptSession`（唯一运行状态源，每活动脚本一个）
→ `IScriptRuntime`（通常为单例 `RoslynScriptRuntime.Shared`，未来 ScriptWorker 进程实现同一契约）。

## 外部接入 API（无头，无需编辑器控件）

```csharp
using ScriptEditor.Models;
using ScriptEditor.Services;

// 编译 + 执行一个脚本项目项（一次性会话，自动释放）。
var script = new ScriptProjectItem { Title = "demo", Code = "Console.WriteLine(\"hi\");" };
ScriptExecutionResult result = await RoslynScriptRuntime.Shared.ExecuteAsync(script);
bool compiled = await RoslynScriptRuntime.Shared.CompileAsync(script);

// 绑定到已有数据对象：同一 ScriptItem 始终复用同一会话（运行状态事实源）。
var item = new ScriptItem { Title = "job", Code = "Console.WriteLine(Inputs[\"k\"]);" };
var session = RoslynScriptRuntime.Shared.GetSession(item);
session.SetInputData(new Dictionary<string, object?> { ["k"] = 1 });
await session.RunAsync();          // 输出/状态经 session.Output / StatusText 读取
session.ProjectContext = context;  // 可选：任务级隐式 using 隔离（见下）
RoslynScriptRuntime.Shared.ReleaseSession(item);

// 版本化持久化（V2 写入 / V1→V2 迁移读取，引用相对工程目录）。
using ScriptEditor.Persistence;
var store = new ScriptProjectStore();
await store.SaveAsync(path, project);
ScriptProject loaded = await store.LoadAsync(path);
```

任务级隔离：`ScriptProjectContext`（`ScriptEditor.Options`）按任务持有隐式 using
与引用基准目录，会话编译只读自己任务的集合；进程默认用 `ScriptRuntimeOptions.Default`。
`CustomRoslynHost` 的旧进程静态 `GlobalImports` 入口仅为兼容保留。

## 验证

```powershell
# 自动化测试（CI）：Core/Runtime，无 WPF 依赖
dotnet test ScriptEditor/ScriptEditor.Tests/ScriptEditor.Tests.csproj -c Debug

# 手工/性能宿主（旧 TestRunner，含 UI 与性能报告）
dotnet run --project ScriptEditor/Script.Tests/ScriptEditor.Tests.csproj -- --core
dotnet run --project ScriptEditor/Script.Tests/ScriptEditor.Tests.csproj -- --ui

# Workbench 演示（含 --smoke 自检：打开即关，用于 CI 存活验证）
dotnet run --project ScriptEditor/ScriptEditor.Workbench/ScriptEditor.Workbench.csproj -- --smoke
```

性能回归阈值（`ScriptEditor.Tests/Performance/PerformanceThresholds.cs`，CI 回归线，留足余量）：

| 场景 | 阈值 |
|---|---|
| 预编译后首次执行 | < 5 s |
| 1000 行输出洪泛 | < 20 s |
| 20 脚本任务并行预编译 | < 120 s |
| 长时间循环停止 | 10 s 内结束 |
| 40 轮重编译 GC 后工作集 | < 峰值 1.5x |

Workbench 覆盖（`ScriptManagerView`：多脚本、排序、RunAll/StopAll/循环、引用面板、
.taskproj 保存/加载/迁移、外部 .cs 同步）+ 旧宿主 `--ui`（Tab 切页、智能提示/格式化/重命名人工回归）。

## 文件格式

`.taskproj` V2（`FormatVersion: 2`）：引用为结构化描述，工程目录下路径存相对路径；
V1（无版本号，`References` 为字符串列表）加载时自动迁移。保存轮换 `.taskproj.bak`。
外部 `.cs` 镜像（`NN_标题.cs`）附 `taskfiles.manifest.json`（文件名 ↔ ScriptId + 内容哈希），
回填优先按标识匹配；示例见 `Samples/Sample.taskproj`。
