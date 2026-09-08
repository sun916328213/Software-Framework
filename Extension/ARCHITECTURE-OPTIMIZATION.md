# Extension 架构优化方案

> 项目:`Extension`(单 csproj,`.NET 9.0-windows7.0`,WPF + Cognex/Halcon/OpenCV + Prism)
> 模式:Architecture Audit → Refactor Roadmap
> 范围:`Extension/`(约 70 个 `.cs`、1 个 csproj、4 层目录、零测试项目)

---

## 0. 当前架构速览

```
Extension.sln
└── Extension/                                       [单 csproj]
    ├── Core/                                       (14 个 Ex 子目录,无横向规则)
    │   ├── CollectionEx/  ConvertEx/  EnumEx/  CompareEx/  DataEx/
    │   ├── ControlEx/     FileEx/     ImageEx/ IntPtrEx/   ReflectionEx/
    │   ├── SystemEx/      TaskEx/     SemaphoreEx/ (空)
    ├── DesignPattern/                              (基础 + Advanced + Example 三件套 ×6)
    ├── Helper/                                     (Channel/Mapping/Notify/TaskHelper)
    ├── Tool/                                       (ConcurrentList / RangeObservableCollection)
    └── Struct/                                     (KeyValue/LogInfo/ImageInfo/DeviceStatus)
```

**主要劣化信号(详见 §1 审计):**

- 14 个 `*Ex` 子目录、无准入规则,新人难以判断新功能该放哪。
- `Struct/` 与 `Helper/Mapping/` 双向耦合 + 静态 `_isInitialized` 开关。
- `Core/ImageEx/` 同时引用 Cognex/Halcon/OpenCV,污染整个 `Extension.dll`。
- `DesignPattern/Example/*` 与运行时库混编,且与已依赖的 `Prism.Core` 重叠。
- `Core/IntPtrEx/DynamicDllLoaderEx` 构造 `try/catch` 后吞掉所有异常(`空 catch` 反模式)。
- `Core/FileEx/FileEx.Write` `catch { }` 静默吞错。
- `ReflectionEx/` 实际做 Prism IoC 注册,命名误导。
- `Helper/` 与 `Tool/` 边界模糊。

---

## 1. 审计结论(Health Score)

| 维度 | 评分 | 说明 |
|---|---|---|
| Dependency Disorder | 3/10 | Struct↔Helper 双向耦合、Core/14 子目录无准入规则 |
| Domain Model | 4/10 | `Struct/` 命名错位,4 种语义混编 |
| Knowledge Duplication | 6/10 | `DesignPattern` 与 Prism 重叠 |
| Accidental Complexity | 5/10 | 空目录、Example 入库、Helper/Tool 分裂 |
| Change Propagation | 5/10 | 引入新 SDK 必须改 csproj |
| Cognitive Overload | 4/10 | 新人无法判断新功能该放哪 |
| **Overall** | **4.5/10** | **Warning → 必须主动重构** |

---

## 2. Findings 与优先级

| ID | 标题 | 严重度 | 优先级 |
|---|---|---|---|
| **F1** | Struct↔Helper 双向耦合 + DTO/Mapping 内嵌 | 🔴 Critical | **P0** |
| **F2** | `Struct/` 命名错位(struct/class/enum/record 混编) | 🔴 Critical | **P1** |
| **F3** | `Core/` 单层 14 子目录,无横向规则 | 🔴 Critical | **P1** |
| **F4** | `ImageEx/` 引入 3 个非托管视觉库,污染全程序集 | 🟡 Warning | **P0** |
| **F5** | `DesignPattern/*Example` 与运行时库混编 + 与 Prism 重叠 | 🟡 Warning | **P2** |
| **F6** | `ConcurrentList<T>` 用 `lock` 自实现,且无 seam | 🟡 Warning | **P3** |
| **F7** | `ReflectionEx/` 实际做 DI/MVVM 注册,命名误导 | 🟡 Warning | **P2** |
| **F8** | `Helper/` 与 `Tool/` 边界模糊 | 🟡 Warning | **P2** |
| **F9** | `DynamicDllLoaderEx` 空 catch + `FileEx.Write` 静默吞错 | 🟡 Warning | **P1** |
| **F10** | 零测试项目(seam 缺失) | 🟡 Warning | **P1** |

---

## 3. 目标架构(重构后)

```
Extension.sln
├── Extension.Abstractions/                        [接口 + DTO + 枚举,netstandard2.1,无外部依赖]
│   ├── Imaging/IImageSource.cs
│   ├── Imaging/IImageStore.cs
│   ├── IO/IFileSystem.cs
│   ├── Process/IProcessRunner.cs
│   └── Domain/{Logging,Device,Imaging,Common}/...
│
├── Extension.Core/                                [无 SDK 依赖的纯工具,net9.0]
│   ├── Primitives/       (StringEx/ConvertEx/EnumEx/CompareEx/BitOperationEx)
│   ├── Collections/      (ListEx/RangeObservableCollection → 迁自 Tool/)
│   ├── Concurrency/      (TaskEx/ChannelHelper/ValueTaskSourceHelper/EventHelper)
│   ├── Mapping/          (MapsterHelper + Profiles/)
│   ├── Notify/           (NotifyPropertyChanged)
│   └── Reflection/       (ReflectionEx,移除 Register* 系列 → F7)
│
├── Extension.Infrastructure/                      [所有外部 IO,net9.0]
│   ├── FileSystem/       (FileEx/DirectoryEx/CSVEx/ExcelEx/ZIPEX/ScheduledDeletionEx)
│   └── Process/          (ProcessEx/CpuMonitorEx/DelayEx/FpsEx/LangSwitchEx)
│
├── Extension.Imaging/                             [net9.0-windows,图像基座]
│   ├── Bitmap/           (BitmapEx/RawEx/IntPtrEx/SafeMemoryHandle/DynamicDllLoaderEx)
│   └── Abstractions/     (IImage<TPixel>、FormatAdapter 等)
│
├── Extension.Vision.Cognex/                       [仅 Cognex dll]
├── Extension.Vision.Halcon/                       [仅 Halcon dll]
├── Extension.Vision.OpenCv/                       [仅 OpenCvSharp4]
│
├── Extension.UI/                                  [WPF 控件,net9.0-windows,UseWPF]
│   └── Controls/         (DataGridViewEx/DispatcherEx/FileDialogEx/... + Navigation 注册)
│
├── Extension.Wiring/                              [桥接到 Prism IContainerRegistry]
│   └── Bootstrap/        (RegisterNavigationViewEx/RegisterServicesEx → 改名 Bootstrap)
│
└── tests/
    ├── Extension.Core.Tests/
    ├── Extension.Infrastructure.Tests/
    └── Extension.Imaging.Tests/
```

**强制依赖方向(单向,反向即报错):**

```
Extension.UI ──┐
Extension.Vision.* ──┐
Extension.Imaging ──┐
Extension.Infrastructure ──┐
Extension.Wiring ──┐          ──> Extension.Core ──> Extension.Abstractions
                              (所有跨层 import 必须在 CI 用 Roslyn 分析器检查)
```

---

## 4. 优化方向详解

### 4.1 [P0] F4 — 拆视觉库为独立 csproj

**症状:** `Extension.csproj` 同时引用 `Cognex.VisionPro / Cognex.VisionPro.Core / halcondotnet / OpenCvSharp4`。任何只要用 `StringEx` 的下游都必须拖上 Cognex/Halcon 的 native dll。

**目标结构:**

```text
src/
├── Extension.Imaging/Extension.Imaging.csproj       TargetFramework=net9.0-windows7.0,UseWPF=false
├── Extension.Vision.Cognex/Extension.Vision.Cognex.csproj   + Reference Cognex.VisionPro(.dll)
├── Extension.Vision.Halcon/Extension.Vision.Halcon.csproj   + Reference halcondotnet
└── Extension.Vision.OpenCv/Extension.Vision.OpenCv.csproj   + PackageReference OpenCvSharp4
```

**步骤:**

1. 新建 4 个 csproj(`Extension.Imaging` 留基座)。
2. 移动文件:
   - `Core/ImageEx/BitmapEx.cs`、`RawEx.cs`、`IntPtrEx.cs` → `Extension.Imaging/Bitmap/`
   - `Core/IntPtrEx/SafeMemoryHandle.cs`、`DynamicDllLoaderEx.cs` → `Extension.Imaging/Bitmap/`
   - `Core/ImageEx/CogImageEx.cs` → `Extension.Vision.Cognex/`
   - `Core/ImageEx/HImageEx.cs` → `Extension.Vision.Halcon/`
   - `Core/ImageEx/MatEx.cs` → `Extension.Vision.OpenCv/`
3. 在 `Extension.Imaging` 定义 `IImageSource` / `IImage<TPixel>`,三个 Vendor 包各自实现 Anti-Corruption Adapter(`CogImageSource : IImageSource` 等)。
4. 原 `Extension.csproj` 删除 Cognex/Halcon/OpenCv 的 `<Reference>`/`<PackageReference>`。
5. `Extension.UI` 引用 `Extension.Vision.*` 由消费者按需选择,不再"全捆绑"。

**验证:**

- 删掉 `bin/` 后,`Extension.Core.dll` 不再依赖 native Halcon/Cognex(用 `dotnet list Extension.Core package` + `dumpbin /dependents` 验证)。
- `Extension.UI` 不再隐式拖入 Cognex 授权 dll。

---

### 4.2 [P0] F1 — 切断 Struct↔Mapping 双向耦合

**症状:** `Struct/LogInfo.cs`、`Struct/ImageInfo.cs` 内嵌 `*Mapping.Configure(TypeAdapterConfig)`;`Helper/Mapping/MapsterHelper.MappingConfig.InitializeAllMappings()` 静态开关 + 手动登记。

**改造:**

```text
src/
├── Extension.Abstractions/Domain/Logging/LogInfo.cs        (纯 POCO)
├── Extension.Abstractions/Domain/Imaging/ImageInfo.cs      (纯 POCO)
└── Extension.Core/Mapping/Profiles/
        ├── LogInfoProfile.cs        : IRegister  (Mapster 9 风格)
        └── ImageInfoProfile.cs      : IRegister
```

**关键改动:**

1. `Struct/LogInfo.cs`、`Struct/ImageInfo.cs` 删除 `using Mapster;` 与所有 `*Mapping.Configure` 静态类。
2. `LogInfoMapping.Configure` → `LogInfoProfile : IRegister`(或保留 `static Configure` 但放在 `Mapping/Profiles/`)。
3. `MappingConfig.InitializeAllMappings()` 改为:

   ```csharp
   // Extension.Core.Mapping.MappingBootstrapper
   public static class MappingBootstrapper
   {
       public static void Initialize() =>
           TypeAdapterConfig.GlobalSettings.Scan(
               typeof(MappingBootstrapper).Assembly,
               t => t.IsDefined(typeof(MapperAttribute), false) || t.Name.EndsWith("Profile"));
   }
   ```
4. 调用方只在应用启动调用 `MappingBootstrapper.Initialize()` 一次,无 `_isInitialized` 开关、无"忘登记"的隐式契约。
5. 命名统一:`LogInfoDTO` → `LogInfoDto`(全代码库搜一次替换,统一风格)。

**验证:** 删 `Struct/LogInfo.cs` 中的 `using Mapster;` 后仍能编译,且 `MapsterHelper` 不再 `using Extension.Struct;`(单向依赖达成)。

---

### 4.3 [P1] F2 — `Struct/` 改 `Domain/`、可变 struct 改 record/class

**症状:** `LogInfo`/`ImageInfo` 是 `public struct { get; set; }`(值类型 + 可变 = 反模式);`KeyValue` 是 `class`;`DeviceStatus`/`ELogLevel` 是 `enum`。

**改造:**

```csharp
// 改前
public struct LogInfo
{
    public string Time { get; set; }
    public string Info { get; set; }
    public ELogLevel Level { get; set; }
}

// 改后
public sealed record LogInfo(string Time, string Info, ELogLevel Level = ELogLevel.Information);
public sealed record LogInfoDto(string Time, string Info, string Level);
```

**目录划分:**

```text
Extension.Abstractions/Domain/
├── Logging/    (LogInfo, LogInfoDto, ELogLevel, ILogSink)
├── Imaging/    (ImageInfo, ImageInfoDto, ImageFormat)
├── Device/     (EDeviceStatus, ESecsMode)
└── Common/     (KeyValue, Result<T>)
```

**规则:**

- 不再用 `struct`(除非明确需要值语义,如 `KeyValuePair`)。
- DTO 用 `record`(不可变 + 值相等 + `with`)。
- Enum 必须有 `[Description]`(已有,统一到规范)。
- 任何暴露的 public 类型必须能在 1 句话内讲清"它代表什么业务概念"。

---

### 4.4 [P1] F3 — `Core/` 按 role 重组

**目标结构见 §3。**

**对应迁移:**

| 旧路径 | 新路径 |
|---|---|
| `Core/CollectionEx/ListEx.cs` | `Core/Collections/ListEx.cs` |
| `Core/ConvertEx/*` | `Core/Primitives/ConvertEx/*`(保留 ConvertEx 子目录收纳多个文件) |
| `Core/EnumEx/EnumEx.cs` | `Core/Primitives/EnumEx.cs` |
| `Core/CompareEx/BitOperationEx.cs` | `Core/Primitives/BitOperationEx.cs` |
| `Core/DataEx/BufferEx.cs, CloneEx.cs` | `Core/Collections/(Buffer/Clone)` |
| `Core/DataEx/ZIPEX.cs` | `Infrastructure/FileSystem/ZipEx.cs`(依赖 IO) |
| `Core/FileEx/*` | `Infrastructure/FileSystem/*` |
| `Core/TaskEx/TaskEx.cs` | `Core/Concurrency/TaskEx.cs` |
| `Core/SystemEx/ProcessEx.cs, CpuMonitorEx.cs, DelayEx.cs, FpsEx.cs` | `Infrastructure/Process/` |
| `Core/SystemEx/LangSwitchEx.cs` | `Infrastructure/Platform/LangSwitchEx.cs` |
| `Core/SystemEx/ScheduledDeletion.cs` | `Infrastructure/FileSystem/ScheduledDeletionEx.cs`(注意:与 `FileEx/ScheduledDeletionEx.cs` 合并) |
| `Core/ControlEx/*` | `Extension.UI/Controls/*` |
| `Core/ReflectionEx/ReflectionEx.cs` | `Core/Reflection/ReflectionEx.cs` |
| `Core/ReflectionEx/RegisterNavigationViewEx.cs, RegisterServicesEx.cs` | `Extension.Wiring/Bootstrap/`(见 F7) |
| `Core/SemaphoreEx/`(空) | **删除** |

**准入规则(写进 CONTRIBUTING):**

> Core 层的每个文件必须能用一句话回答:
> 1. 它**不依赖**任何 UI、SDK、原生 dll、文件系统、网络。
> 2. 它只**被**更上层的 UI/Infrastructure/Vision 引用。
> 3. 它的名字反映**职责**而非**对象类型**(`Reflection` 而非 `ReflectionEx`)。

---

### 4.5 [P1] F9 — 修复静默异常

**问题代码 1:** `Core/IntPtrEx/DynamicDllLoaderEx.cs:36-40`

```csharp
try
{
    // ...
}
catch
{
    // 空 catch
}
```

**改造:**

```csharp
catch (DllNotFoundException) { throw; }
catch (Exception ex)
{
    throw new InvalidOperationException(
        $"Failed to load native DLL '{_dllPath}'. See inner exception for details.", ex);
}
```

或直接移除 `try/catch`,让 `LoadLibrary` 抛 `DllNotFoundException`(`SafeMemoryHandle` 已在做)。

**问题代码 2:** `Core/FileEx/FileEx.cs:38`

```csharp
try { File.Copy(path, path + ".bak", true); }
catch { /* 备份失败不阻断写入 */ }
```

**改造:** 至少记录到 `ILogger`(注入),或在调试模式下 `Debug.Fail`;`catch { }` 必须删除或带日志。

**配套:** 引入 `ILogger<T>` 抽象(`Microsoft.Extensions.Logging.Abstractions`),由 Infrastructure 层提供 NLog/Serilog 实现。

---

### 4.6 [P1] F10 — 测试项目 + seam 注入

**症状:** `Tool/ConcurrentList.cs`、`Core/FileEx/*`、`Core/ImageEx/*` 全是 `static class` + 直接 IO,无法单元测试。

**改造:**

1. 新建 `tests/Extension.Core.Tests/` 与 `tests/Extension.Infrastructure.Tests/`。
2. 引入 xUnit + FluentAssertions + NSubstitute(避免重武器 Moq)。
3. 把 `FileEx` 的 `File.ReadAllText` 调用替换为注入的 `IFileSystem`:

   ```csharp
   public interface IFileSystem
   {
       string ReadAllText(string path);
       void WriteAllText(string path, string contents, bool backup);
       bool Exists(string path);
   }

   public sealed class FileEx
   {
       private readonly IFileSystem _fs;
       public FileEx(IFileSystem fs) => _fs = fs;
       public string Read(string dir, string name) => _fs.ReadAllText(Path.Combine(dir, name));
   }
   ```
4. `ProcessEx`、`ImageEx`、`DynamicDllLoaderEx` 同理抽出 `IProcessRunner / IImageSource / INativeLoader`。
5. 单元测试使用 `Substitute.For<IFileSystem>()` 验证调用参数与备份行为。
6. CI 加覆盖率门槛:`coverlet.collector` + `min 60% line coverage on Extension.Core`(逐步提高)。

---

### 4.7 [P2] F5 — 清理 DesignPattern,或迁出 samples/

**决策树:**

- 如果团队需要教学/演示 → 把 `DesignPattern/*Example.cs` 全部移到 `samples/DesignPattern.Samples/`(独立 sln,Console 输出),保留运行时实现。
- 如果只是历史遗留 → 直接删除整个 `DesignPattern/` 目录,改 nuget:
  - 单例:Prism `IContainerRegistry.RegisterSingleton`
  - 中介者:`MediatR`(可选)
  - 发布订阅:`Prism.Events`(已依赖)
  - 观察者:`System.Reactive` 或 `IObservable<T>`
  - 生产者/消费者:`System.Threading.Channels`(已有 `ChannelHelper`)

**推荐:** 采用第二种,**移除整个 `DesignPattern/`**。原因:

- 项目已依赖 `Prism.Core`,`MediatorPattern`/`PublishSubscribePattern` 都是其子集。
- `Advanced*` 与基础版并存无推荐入口,API 表面积膨胀。
- 维护教学代码占用 reviewer 时间但产生零运行时价值。

---

### 4.8 [P2] F7 — `ReflectionEx` → `Extension.Wiring/Bootstrap`

**改造:**

```text
Extension.Wiring/
└── Bootstrap/
    ├── ServiceBootstrapper.cs       (Register*Ex 改名 + Bootstrapper)
    ├── NavigationBootstrapper.cs
    └── Conventions/                (命名空间 → 生命周期 约定)
```

命名规则改为 `Bootstrap*` 而非 `Register*Ex`,与 Prism 社区对齐,新人不必猜"为什么 Reflection 文件夹里出现 DI"。

---

### 4.9 [P2] F8 — 合并 Helper/ + Tool/ → Toolkit/ 或并入 Core 层

**选项 A(推荐):** 全部并入 `Core/` 对应子层(F3 已规划):

| 旧 | 新 |
|---|---|
| `Helper/Channel/ChannelHelper.cs` | `Core/Concurrency/ChannelHelper.cs` |
| `Helper/Mapping/MapsterHelper.cs` | `Core/Mapping/MappingBootstrapper.cs` |
| `Helper/Notify/NotifyPropertyChanged.cs` | `Core/Notify/NotifyPropertyChanged.cs` |
| `Helper/TaskHelper/EventHelper.cs` | `Core/Concurrency/EventHelper.cs` |
| `Helper/TaskHelper/ValueTaskSourceHelper.cs` | `Core/Concurrency/ValueTaskSourceHelper.cs` |
| `Tool/ConcurrentList.cs` | `Core/Collections/ConcurrentList.cs`(或直接删,见 F6) |
| `Tool/RangeObservableCollection.cs` | `Core/Collections/RangeObservableCollection.cs` |

**选项 B:** 保留 `Toolkit/` 顶层,统一风格:

```text
Toolkit/
├── Collections/  (ConcurrentList, RangeObservableCollection, ListEx)
├── Mapping/      (MappingBootstrapper + Profiles)
├── Concurrency/  (Channel/Task/Event/ValueTaskSource)
└── Notify/       (INotifyPropertyChanged 扩展)
```

任选其一,不要让 Helper/ + Tool/ 共存。

---

### 4.10 [P3] F6 — 重审 `ConcurrentList<T>`

**决策:**

- 若消费者确实需要 `IList<T>` 并发实现:保留并改用 `ReaderWriterLockSlim`,暴露 `ISynchronizationProvider` seam,便于测试。
- 若仅在 WPF/UI 后台使用:删除,用 `ImmutableList<T>` + `Interlocked.Exchange` 切换引用(参考 `ImmutableInterlockedUtils`)。
- 若仅为"凑 API":删除,改让消费者根据场景选 `ConcurrentQueue<T>` / `ImmutableList<T>` / `lock(list)`。

**结论:** 多数情况下推荐**删除**(只保留 `RangeObservableCollection`,价值明确)。

---

## 5. 落地路线图

| 阶段 | 周次 | 任务 | 验收 |
|---|---|---|---|
| **M0** | W1 | 建 `tests/` 空架子 + CI 跑 `dotnet build` | CI 绿 |
| **M1** | W2 | F4:拆 `Extension.Vision.*` 4 个 csproj | `Extension.Core.dll` 不再依赖 Cognex/Halcon/OpenCV native |
| **M2** | W3 | F1:`*Mapping.Configure` 迁 `Mapping/Profiles/`,启用 Mapster Scan | `Struct/` 不再 `using Mapster` |
| **M3** | W4 | F2:`Struct/` → `Domain/`,可变 struct → record | 0 个可变 `public struct` 业务类型 |
| **M4** | W5–W6 | F3:Core 按 role 重组 + 删 `SemaphoreEx/` 空目录 | Core 子目录 ≤ 6 个,每个子目录有 README 说明职责 |
| **M5** | W6 | F9:修复两处静默 `catch` | CodeQL / Roslyn 分析器 0 命中 `catch { }` |
| **M6** | W7 | F10:补 seam 接口 + 单元测试覆盖 Core | Coverage ≥ 60% |
| **M7** | W8 | F7:`Register*Ex` → `Bootstrap/`,依赖 Prism | `ReflectionEx` 文件夹不再有 DI 代码 |
| **M8** | W9 | F5:评估并删除/迁出 `DesignPattern/` | 0 个 `*Example.cs` 在运行时 dll |
| **M9** | W10 | F8:Helper + Tool 合并决策与迁移 | 二选一并完成 |
| **M10** | W11 | F6:决定 `ConcurrentList<T>` 命运 | 决定并落到代码 |
| **M11** | W12 | 全量回归 + 版本号 bump 1.0 | Release notes 与 CHANGELOG |

---

## 6. CI / 工程化护栏

为防止重构后再次腐化,加入以下机器检查:

```xml
<!-- Directory.Build.props -->
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <AnalysisLevel>latest-all</AnalysisLevel>
  <NoWarn>$(NoWarn);CA1051;CA1062</NoWarn>
</PropertyGroup>
```

**Roslyn 分析器(BannedApiAnalyzer):**

```csharp
// BannedSymbols.txt
P:System.IO.File.ReadAllText;Use IFileSystem abstraction
P:System.Diagnostics.Process.Kill;Use IProcessRunner abstraction
T:Extension.Core.ReflectionEx.RegisterServicesEx;Moved to Extension.Wiring.Bootstrap
T:Extension.Struct.LogInfoDTO;Renamed to Extension.Abstractions.Domain.Logging.LogInfoDto
```

**架构规则验证(NDepend / NArchitecture):** 单向依赖,反向 import 在 PR 阶段直接失败。

**EditorConfig 强制:**

```ini
[*.cs]
csharp_style_pattern_matching_over_is_with_cast_check = true:warning
dotnet_diagnostic.CA1031.severity = error   ; 不允许吞普通异常
dotnet_diagnostic.CA1051.severity = none    ; 可见实例字段(临时)
```

---

## 7. 不要做的事(Anti-patterns)

- ❌ 在不抽 seam 的情况下给 `static class FileEx` 加单元测试(测试将退化为 IO 集成测试,毫无价值)。
- ❌ 把 `Mapping/Profiles/` 放进 `Extension.Vision.*`(它们都依赖 `Extension.Abstractions`)。
- ❌ 把 `ConcurrentList<T>` 与 `ConcurrentQueue<T>`/`ImmutableList<T>` 同时保留(选一个,文档写明何时用)。
- ❌ 用 `[Obsolete]` 软删除核心 API,半年后没人删 → 重新腐烂;要么删干净,要么写迁移指南后立刻删。
- ❌ 在新 csproj 中保留旧 `Extension.csproj` 作为转发包 — 这只是延迟决策,不要做"兼容垫片"。

---

## 8. 风险与回滚

| 风险 | 影响 | 缓解 |
|---|---|---|
| 拆 csproj 导致下游引用路径大面积改名 | 高 | 通过 `InternalsVisibleTo` + 临时转发命名空间(最长 2 个 minor 版本)过渡 |
| 可变 struct → record 破坏二进制兼容 | 中 | major 版本号 bump;序列化路径用 `[StructLayout]` 显式控制 |
| 删除 `DesignPattern/` 致外部调用方报错 | 中 | 在删除前一个 minor 标记 `[Obsolete("error: true")]`,CHANGELOG 注明 |
| 单元测试覆盖不足时引入 seam | 中 | seam 引入按 Core → Infrastructure 顺序,先 Core 100% 再扩 |

---

## 9. 验收标准(Definition of Done)

- [ ] `dotnet list package` 在 `Extension.Core` 不出现 Cognex/Halcon/OpenCv/Prism。
- [ ] `grep -r "using Extension.Struct" Extension.Core/` 返回 0 行。
- [ ] `grep -r "catch\s*{" Extension.Core Extension.Infrastructure` 返回 0 行(允许 `catch (SpecificException ex)`)。
- [ ] `Core/` 子目录 ≤ 6 个,每个含 README.md。
- [ ] `tests/` 下覆盖率 Core ≥ 60%、Infrastructure ≥ 40%。
- [ ] `Extension.sln` 引用 ≥ 4 个 csproj + ≥ 2 个测试项目。
- [ ] CI 含 BannedApiAnalyzer + 架构依赖规则检查,合并前必须绿。
- [ ] CHANGELOG 记录本方案所有破坏性变更。