# Build/ 验收基建 — 来源与状态（PROVENANCE）

> 本目录**不在 Git 中**（`.gitignore` 第 391 行 `/Build`）。本文件是它唯一的来源凭据。
> 生成于 P-01（恢复验收基建）。**请勿把本目录的文件当作"原基线"引用** —— 见下方状态列。

## 运行前置（必读）

| 项 | 要求 | 原因 |
|---|---|---|
| PowerShell | **必须 PowerShell 7+（`pwsh`）**，不能用 Windows PowerShell 5.1 | 脚本用 `[IO.Path]::GetRelativePath`，该方法**只存在于 .NET Core**；5.1 会报 `System.IO.Path 不包含名为"GetRelativePath"的方法` |
| 执行策略 | 需 `-ExecutionPolicy Bypass`（或 `Set-ExecutionPolicy -Scope Process Bypass`） | 本机默认策略禁止运行脚本 |
| 本机 pwsh 路径 | `C:\Users\91632\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe` | 该机 `pwsh` 不在 PATH 上 |

调用示例：
```powershell
& $pwsh -NoProfile -ExecutionPolicy Bypass -File "$repo\Build\Test-ArchitectureBaseline.ps1" `
    -Mode Verify -BaselinePath "$repo\Build\Baselines\Architecture-Reconstructed-2026-10-08.json"
```

⚠️ **性能**：一次 Capture/Verify 要遍历 `Software Frame/` + `MachineApp/` 下约 27k 文件并对每个
`.cs` 跑 4 条正则，本机实测**约 25 分钟**。不要在前台跑。

## 为什么会有这个文件

`Build/` 从未被提交过（`git rev-list --all --objects` 全库只有 1 个 `Build/` 路径），
磁盘上也没有备份。原始脚本**全部丢失**。这里的内容是**取证恢复**的结果：
从 git 悬空对象（dangling blobs）与 Codex 会话转录中逐字节还原。

## 恢复来源（穷尽清单）

| 来源 | 结果 |
|---|---|
| 文件系统（Desktop / Temp / .codex） | 无任何副本 |
| `git stash` / `reflog` | 空 |
| `git rev-list --all --objects`（全部 ref） | 仅 `Build/Test-Integration.ps1` |
| `git cat-file --batch-all-objects`（13171 blob，含**悬空对象**） | **主要来源**，见下表 |
| Codex `sessions/**/*.jsonl`（177 个） | `Test-Integration.ps1`（早期版）、`Baselines/Chinese-Code-Comments-Execution.md` |
| Codex `archived_sessions/**`（23 个） | 无 |
| Codex `thread_history_1.sqlite` / `logs_2.sqlite` | 仅调用记录，无文件内容 |

## 已恢复文件（verbatim，逐字节还原）

| 文件 | 字节 | 来源 blob | 状态 |
|---|---|---|---|
| `Test-Integration.ps1` | 6480 | `ede41e98c248` | ✅ **已于 T02 按新布局改写**（`Software Frame.sln` + `MachineApp/`），见文末「重建」节 |
| `Test-ArchitectureBaseline.ps1` | 8287 | `4b4c63457494` | ⚠️ 可执行，但依赖的 `Baselines/*.json` 已丢失 |
| `Test-ArchitectureClosure.ps1` | 8114 | `ab8d7e269a6f` | ✅ 已适配新布局（`MachineApp/MachineApp/MachineApp.csproj`） |
| `Test-WorkbenchVisual.ps1` | 1902 | `1692044927e3` | ✅ |
| `Test-WPFControlsContent.ps1` | 1890 | `342dbac8c17e` | ⚠️ 依赖 `Baselines/WPFControls-business-content.json`（已丢失） |
| `Restore-VendorDependencies.ps1` | 5433 | `99440e6cf29c` | ✅ |
| `Dependencies/vendor-manifest.json` | 15729 | `c138d95f0b3e` | ✅ 17 个 artifact |
| `Baselines/Chinese-Code-Comments-Execution.md` | 1792 | Codex 转录 | ✅ |
| `Baselines/Architecture-Reconstructed-2026-10-08.json` | 991925 | **重新生成**（非恢复） | 🆕 见下"重建基线" |
| `Test-ArchitectureMigration-S4.ps1` | 6069 | `cec999085b0f` | 🗑 单次性；依赖已丢失的 S3/S4 基线 |
| `Test-ArchitectureMigration-S5.ps1` | 5377 | `2918862da403` | 🗑 单次性；同上 |
| `Test-ArchitectureMigration-S6.ps1` | 5005 | `9ad4a78f4eb9` | 🗑 单次性；同上 |

## 重建基线（决策 A 的产物）

`Baselines/Architecture-Reconstructed-2026-10-08.json` **不是恢复物，是新建物**。
原 `Baselines/*.json` 全部丢失且不可推导，因此按用户决定以**当前仓库状态**为新锚点。

快照顶部写入了 `baselineOrigin` 标记（`kind: "reconstructed"` / `originalLost: true` /
`predecessorLayout`），使"这是重建基线"**永久可见**。

> **它意味着什么**：本快照把 2026-10-08 之前已经存在的架构漂移**一次性认定为基线**。
> 这套守卫从今往后只能发现**新增**漂移，发现不了**既有**漂移。
> 若日后需要"干净基线"，必须回到决策 B（先审计、再锚定）。

**已验证可用**（往返一致，0 漂移）：

```
> & $pwsh -NoProfile -ExecutionPolicy Bypass -File Build/Test-ArchitectureBaseline.ps1 `
    -Mode Capture -BaselinePath Build/Baselines/Architecture-Reconstructed-2026-10-08.json
Architecture baseline Capture succeeded: 84 projects, 1688 sources, 167 XAML files,
29 configuration/fixture files, 16 solutions.

> ... -Mode Verify -BaselinePath <同上>
Architecture baseline Verify succeeded: 84 projects, 1688 sources, 167 XAML files,
29 configuration/fixture files, 16 solutions.        # 退出码 0，无 drift
```

即：注入 `baselineOrigin` 标记**不影响** Verify 判定（脚本只读顶层 `schemaVersion`/`scope`
与 5 个分节，多一个顶层键是安全的）。

> `Test-ArchitectureMigration-S*.ps1` 的**文件名是推断的**（原文件头只有 `param()`，无自述），
> 依据其读写的 `Baselines/Architecture-S*-migrations.json` 命名。

## 全部候选版本（未采用者也保留，可回溯）

同一脚本存在多个历史版本；上表取**最大（最新）**版本。完整集合：

```
Test-ArchitectureBaseline.ps1 : 4b4c63457494(8287) e8a92ea77cce(8277) 77723291a230(8275)
Test-ArchitectureClosure.ps1  : ab8d7e269a6f(8114) fa8d2a89fde4(8109) e0f810e46ec4(7106)
Test-Integration.ps1          : ede41e98c248(6480) 187c6212c7fd(6458) 198d4cb5e232(6433)
                                d217e84224c6(5862) 84eb7863759d(5165)
Test-WorkbenchVisual.ps1      : 1692044927e3(1902)
Test-WPFControlsContent.ps1   : 342dbac8c17e(1890) 7217b8103b31(1459)
Restore-VendorDependencies.ps1: 99440e6cf29c(5433)
Migration S4                   : cec999085b0f(6069) 5b11446cd930(6084)
Migration S5                   : 2918862da403(5377) d5d2022c0367(5383)
Migration S6                   : 9ad4a78f4eb9(5005) 4fd53d93fe51(5357) f722fa591423(5341)
```

（字节数含 BOM；括号内为 blob 前 12 位。`d217e84224c6` 是唯一可从 ref 到达的版本。）

## 无法恢复（全部丢失，需重建）

| 文件 | 证据 |
|---|---|
| `Test-FlowNodesArchitecture.ps1` | 仅存在于文档引用中，无 blob、无转录创建记录 |
| `Test-MotionWorkbenchStability.ps1` | 同上 |
| `Test-WorkbenchTestHostShutdown.ps1` | 仅存在于文档引用中，无 blob、无转录创建记录 → **已于 T02 重建，见文末「重建」节** |
| `Test-UserFormsVisual.ps1` | 同上 |
| `Test-ExtensionBoundary.ps1` | 同上 |
| `Test-VisionHostBoundary.ps1` | 同上 |
| `TestSupport/WorkbenchTestLifetime.cs` | 只找到**引用**它的 csproj 与 fixture，未找到 `class WorkbenchTestLifetime` 本体 → **已于 T02 重建，见文末「重建」节** |
| `Baselines/Architecture-{P0,S1..S7,Closure}.json` | 架构快照本体，无 blob |
| `Baselines/WPFControls-{Consolidation,FinalClosure,business-content,embedded-script-content}.json` | 同上 |
| `Baselines/*-migrations.json` | 仅存 3 个迁移清单（S6/S7/Closure），其余丢失 |
| `ChineseCodeCommentAnnotator.cs` | 无 blob |

### ⚠️ 其中 `TestSupport/WorkbenchTestLifetime.cs` 正在**阻断构建**

它不只是一个"丢失的产物"——**7 个 Workbench 测试工程**通过链接引用它：

```xml
<Compile Include="../../../../Build/TestSupport/WorkbenchTestLifetime.cs" Link="TestSupport/WorkbenchTestLifetime.cs" />
```

受影响工程（当前**编译不过**，缺文件）：

```
Software Frame/Communication/Tests/Communication.Workbench.Tests
Software Frame/DataManager/Tests/DataManager.Workbench.Tests
Software Frame/Device/Tests/Device.Workbench.Tests
Software Frame/MotionControl/Tests/MotionControl.Workbench.Tests
Software Frame/ScriptEditor/Tests/ScriptEditor.Workbench.Tests
Software Frame/SecsGem/Tests/SecsGem.Workbench.Tests
Software Frame/Vision/Tests/Vision.Workbench.Tests
```

它们的 `WorkbenchAppFixture.cs` 里有 `private WorkbenchTestLifetime? _lifetime;` /
`_lifetime = new WorkbenchTestLifetime(app);`。**决策 C 选择暂不重建，因此这 7 个工程
维持不可编译状态** —— 后续任何需要跑 Workbench 测试的任务都会撞上它。

## 重建：`TestSupport/WorkbenchTestLifetime.cs`（T02，2026-10-08）

**这是重建物，不是恢复物。** 原文件从未入 Git、无 blob、无转录，**不可恢复**；
本文件按**七个夹具的实际用法**推断出最小 API 面后重写。**不要把它当原文件引用。**

推断依据（全仓 grep，无其他调用点）：

| 成员 | 出处 |
|---|---|
| `WorkbenchTestLifetime(Application app)` | 七夹具均为 `new WorkbenchTestLifetime(app)`（`app` = 各 Workbench 的 `App`） |
| `void ObserveMainWindow()` | 七夹具在 `Startup` 后经 `Dispatcher.BeginInvoke(..., ApplicationIdle)` 调用 |
| `void RecordThreadFailure(Exception)` | Device / MotionControl / ScriptEditor / SecsGem / Vision 夹具的 `catch` 分支 |
| `void CloseAndWait(Thread)` | 六个夹具的 `Dispose()`，参数是它们自己创建并 Start 的 UI 线程 |
| `void CloseAndWait(Thread, Action<Application>)` | MotionControl 夹具的 `Dispose()`——第二个参数是「关闭前把界面摆成运行中」的场景钩子（`StartRunningShutdownScenario`） |

> 该 2 参重载是**编译期发现的**：首轮重建只按 6 个夹具推断出 1 参版本，
> 构建 MotionControl.Workbench.Tests 时报 `CS1501 没有采用 2 个参数的重载`，据此补上。
> 这也说明「按调用点反推 API」必须以**全部**调用点为准，且必须真的编译一次才能确认。

命名空间 `Workbench.TestSupport`——由夹具首行的 `using Workbench.TestSupport;` 确定。

语义对齐生产 `WPFCustomControls/Workbench/WorkbenchLifetime.cs`：
真实走 `Window.Close → Closing(排空资源) → Closed → Application.Exit → UI 线程退出`；
等待上限 60 秒（生产排空默认 30 秒，留余量）。`Closed` / `Exit` 未触发、线程异常、
线程未退出都会抛出——避免夹具把「没关成」当成通过。

> ⚠️ **该文件位于 `/Build`（`.gitignore` 第 391 行），默认不入 Git。**
> 但它是 7 个**已入库**测试工程的编译依赖——不纳管就意味着干净克隆永远编译不过
> 这 7 个工程（这正是 P-01 记录的验收缺口的结构性成因）。
> 因此 T02 用 `git add -f` 把它**显式纳管**。其余 `Build/` 内容（基线 JSON、恢复脚本等）
> 仍保持本机本地产物，不受影响。

## 重建：`Test-WorkbenchTestHostShutdown.ps1`（T02，2026-10-08）

**这是重建物，不是恢复物。** 原文件从未入 Git、无 blob、无转录，全仓**零引用**
（`grep -ri WorkbenchTestHostShutdown` 无命中），因此只能**按规格重写**，不能按调用点反推。

规格（来自 T02 计划条目）：跑七夹具、`-Runs N`、每进程断言退出码 0 + 完整 TRX。

重建后的脚本做三件事，比"跑一遍看红不红"更强：

1. **反查而非写死七个路径** —— 扫 `Software Frame/**/*.csproj` 找带
   `<Compile Include=".../WorkbenchTestLifetime.cs" />` 的工程。新增/改名 Workbench.Tests
   工程会自动纳管，不会静默漏跑。同时守卫每个消费方夹具必须调用 `ObserveMainWindow` + `CloseAndWait`。
2. **逐工程 × N 轮独立进程**，断言退出码 0、TRX 存在且 `total>0`、`failed==0`。
3. **显式搜索关闭链路失败文案**（`主窗口未触发 Closed 事件` / `Application.Exit 未触发` /
   `工作台 UI 线程在…未退出` / `关闭前的运行场景失败` …）。这一步是必要的：只断言"测试全过"
   是**间接推断**——夹具 `Dispose` 抛错在 xUnit 里的呈现方式可能被误读。直接搜抛出点文案
   才能让"关闭没关成"在报告里显形。

**默认排除 `Category=ExternalDatabase`**：`DataManager.Workbench.Tests` 的
`SqlViewModelSmokeTests` 带 `[Trait("Category","ExternalDatabase")]`，连本机真实 PG
（`DM_PG_*`）。没有库的机器上它会 30 秒超时失败 —— 那是环境问题、不是关闭链路问题。
与 `Test-Integration.ps1` 同一口径默认排除（要连库加 `-IncludeDatabaseTests`）。

实测（本机，`-Runs 3 -NoBuild`，pwsh 7）：

```
21/21 个独立进程通过；903 条测试；0 失败；全部退出码 0；21 个 TRX 齐全。
Communication 16 / DataManager 95 / Device 114 / MotionControl 14 /
ScriptEditor 10 / SecsGem 18 / Vision 34   （每工程 × 3 轮）
```

> 该脚本位于 `/Build`（`.gitignore` 第 391 行）。与 `WorkbenchTestLifetime.cs` 不同，
> 它**不是任何工程的编译依赖**，因此**没有** `git add -f`；与其余 `Build/` 内容一致保持本地。
> 代价与 P-01 记录的一致：`Build/` 再次丢失时本脚本需重写。若要根除这个循环，
> 应把整个 `Build/` 纳入版本控制（属 T33/T34 的决策，不在 T02 范围）。

## 改写：`Test-Integration.ps1` → 新布局（T02，2026-10-08）

原版硬失败点（第 12–13、24–25 行）：引用已不存在的 `Software-Framework.sln` 与
`Packing Machine/Packing Machine/Packing Machine.csproj`。改写映射：

| 原版 | 新版 |
|---|---|
| `Software-Framework.sln` | `Software Frame.sln`（76 个工程） |
| `Packing Machine/…/Packing Machine.csproj` | `MachineApp/MachineApp/MachineApp.csproj` |
| owned 目录 `@('Software Frame','Packing Machine')` | `@('Software Frame','MachineApp')` |
| 工程数守卫 `< 50`（旧布局 72 个） | `< 60`（新布局 76 个；真正的完备性由全量比对保证，魔数只是兜底） |

**改写原则：能从工程文件推导的都不写死。** 原版把宿主输出目录硬编码成
`net9.0-windows7.0`、把必需部署配置硬编码成四条路径。而 MachineApp 的
`TargetFramework` 是 **`net9.0-windows10.0.19041.0`**，其 `bin/Debug/` 下**同时残留**一个
`net9.0-windows7.0`（Packing Machine 时期遗留，2026-10-04，490 文件；真产物是
2026-10-05，502 文件）。实测两个目录**都含全部 15 个必需配置项** ——
也就是说**原版硬编码路径不会报错，而是静默地校验一份过期产物并报通过**。

因此新版：TFM 读 `//TargetFramework`；必需配置读 csproj 中带 `CopyToOutputDirectory`
的 `None` 项（当前 15 条）。工程改了脚本自动跟上。

### ⚠️ 改写后立即暴露的一个既有缺陷（不属 T02，留 T33）

`-CheckOnly` 在本仓库当前状态下**会失败**，且是**真实缺陷**而非脚本问题：

```
Integration solution omits owned projects:
  Software Frame\FlowNodes\Tests\FlowNodes.Workbench.Tests\FlowNodes.Workbench.Tests.csproj
```

- 该工程**已入库**（`355750a 软件优化` 引入），**曾经在某个 sln 里**；
- 提交 `2d3fded`（*删除过时入口与文档：INTEGRATION.md、Software-Framework.sln、FlowEditor 旧架构文档*）
  删掉了旧 sln，但**没有把它迁进 `Software Frame.sln`** ⇒ 成了孤儿。
- 后果：它不在唯一解决方案里 ⇒ `dotnet test <sln>` 永远不会跑到它；集成脚本的测试扫描也漏掉它。
  它是 `Microsoft.NET.Test.Sdk` 测试工程，但**不**消费 `WorkbenchTestLifetime`，
  所以不在 T02 的七夹具范围内。
- 归属：**T33**（"Solution、目录责任与文档一致 …… 需重建 `Software-Framework.sln`"）。

另：sln 里另有 3 条指向 `Software Frame/SecsGem/secs4net-base/src/**` 的条目，
这些**文件确实存在**（是 vendored 第三方树），只是不在 owned 扫描范围内（原版同样排除），
不是失效条目。

## 关键结论

1. **基线（baseline）是这套基建的实质，而它没恢复出来。** 脚本只是"读快照并比对"的外壳；
   没有快照，`Test-ArchitectureBaseline.ps1 -Mode Verify` 无法运行。
2. 快照**只能**用 `-Mode Capture` 对**当时的仓库状态**重新生成。而仓库此后已被重构
   （`Packing Machine/` → `MachineApp/`，sln 由 72 → 107 个工程）。
   现在重新 Capture 得到的是**当前状态**的快照，**不是原基线** —— 它会把既有漂移"合法化"。
3. 因此 P-01 不能是"还原"，只能是**重建**；且重建必须先决定基线锚点（见 P-01 报告）。

## 已采取的决策（2026-10-08）

| 议题 | 决定 | 后果 |
|---|---|---|
| 基线锚点 | **A — 以当前状态重建** | 生成 `Architecture-Reconstructed-2026-10-08.json` + `baselineOrigin` 标记。既有漂移被合法化；守卫只防新增漂移。 |
| 6 个丢失脚本 + `WorkbenchTestLifetime.cs` | **C — 暂不重建，只留记录** | 本文件即记录。代价：上述 7 个 Workbench 测试工程持续不可编译。 |

未决（留待后续任务）：`Test-Integration.ps1` 已由 T02 按新布局改写（见文末「重建」节），
但它当前会在"孤儿工程"守卫上失败 —— 那是 `FlowNodes.Workbench.Tests` 未迁入
`Software Frame.sln` 的**真实缺陷**，须由 **T33** 重建 sln 时一并收掉。
