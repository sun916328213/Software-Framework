# Extension 架构优化方案

> 项目:`Extension`(单 csproj,`.NET 9.0-windows7.0`,WPF + Cognex/Halcon/OpenCV + Prism)
> 模式:Architecture Audit → Refactor Roadmap
> 范围:`Extension/`(约 70 个 `.cs`、1 个 csproj、4 层目录、零测试项目)

---

## 落地状态(2026-09-16 更新)

本方案曾长期处于"只写没做"的状态。2026-09-16 落地了一轮**低风险安全项**(23 个文件,+328/-106),
结构性重构仍未启动。**引用本方案前请先核对本表,不要把下面的"计划"当成"现状"。**

| 项 | 内容 | 状态 |
|---|---|---|
| — | `Extension.sln` 引用不存在的 `Extension.UI.csproj`(P0 阻塞,**原方案未发现**) | ✅ 已修 |
| F9 | 20+ 处静默 `catch`(**原方案只点名 2 处**,实际远多于) | ✅ 已修 |
| — | `BitmapEx` 两处 GDI 资源泄漏(**原方案未发现**) | ✅ 已修 |
| — | `DynamicDllLoaderEx.Dispose` 中的 `Thread.Abort()`(.NET 5+ 抛 `PlatformNotSupportedException`) | ✅ 已修 |
| F7 | `Reflection`/`ReflectionEx` 命名空间分裂 | 🟡 部分(已统一为 `Extension.Core.Reflection`,改名 Bootstrap 未做) |
| — | csproj 中的 `Core\SemaphoreEx\` 空目录占位 | ✅ 已删 |
| — | **目标架构从 9 个生产项目收敛为 2 个(+1 测试)**,并补充"新建项目准入规则"(§3 / §3.1) | ✅ 已修订 |
| F1(部分) | **`LogInfo` / `ELogLevel` 迁入 `DataManager.Log.Definition`**,修正依赖倒置;同时删除无用的 `LogInfoDTO` + `LogInfoMapping`(详见 §4.11) | ✅ 已修 |
| — | LogInfo 迁移后残留的 10 处空 `using Extension.Struct;` | ✅ 已清 |
| F14 | `WPFControl` 三份 `LogR/` 死副本(在磁盘上但不参与编译) | ⚠️ 待决策 |
| F15 | `.taskproj`/`.flow` 运行时脚本的检索盲区 | ✅ 已核查,本次迁移安全(§4.14) |
| F1–F6、F8、F10 | 拆 Vision csproj、`Struct/`→`Domain/`、Helper+Tool 合并、移除 DesignPattern、补测试 | ❌ 未做 |

> ⚠️ **本机编译须知:** 仓库缺少**多批**未入库的私有 SDK(`Dll/Vision` 的 Cognex/Halcon、
> `Dll/H101` 的 Miracom、`Dll/Mitsubishi` 的 Act COM、`Dll/Print/Spread` 的 FarPoint),
> 因此多个项目 `dotnet build` 必然报 `CS0246` / `MSB3245`。**这些错误与代码改动无关,不要试图"修复"它们。**
> 各项目能否在本机验证、如何验证,见 §10.3 的矩阵。

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

**主要劣化信号(详见 §1 审计;✅ = 已于 2026-09-16 修复,🟡 = 部分修复):**

- 14 个 `*Ex` 子目录、无准入规则,新人难以判断新功能该放哪。
- `Struct/` 与 `Helper/Mapping/` 双向耦合 + 静态 `_isInitialized` 开关。
- `Core/ImageEx/` 同时引用 Cognex/Halcon/OpenCV,污染整个 `Extension.dll`。
- `DesignPattern/Example/*` 与运行时库混编,且与已依赖的 `Prism.Core` 重叠。
- ✅ `Core/IntPtrEx/DynamicDllLoaderEx` 构造 `try/catch` 后吞掉所有异常 —— 已改为直接抛出。
- ✅ `Core/FileEx/FileEx.Write` `catch { }` 静默吞错 —— 已收紧异常类型 + `Debug.WriteLine` 留痕。
- 🟡 `ReflectionEx/` 实际做 Prism IoC 注册,命名误导 —— 命名空间已统一为 `Extension.Core.Reflection`,改名 `Bootstrap` 未做。
- `Helper/` 与 `Tool/` 边界模糊。
- ✅ **全库 20+ 处静默 `catch`(原审计只发现 2 处)** —— 已全部按"保留降级语义 + 收紧异常类型 + 留痕"处理。
- ✅ **`BitmapEx` 的 GDI 句柄泄漏与位图未解锁**(原审计未发现) —— 已加 `finally` 修复。
- ✅ **`Extension.sln` 引用不存在的 `Extension.UI.csproj`**(原审计完全未发现) —— 已移除。

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

| ID | 标题 | 严重度 | 优先级 | 状态(2026-09-16) |
|---|---|---|---|---|
| **F1** | Struct↔Helper 双向耦合 + DTO/Mapping 内嵌 | 🔴 Critical | **P0** | ❌ 未做 |
| **F2** | `Struct/` 命名错位(struct/class/enum/record 混编) | 🔴 Critical | **P1** | ❌ 未做 |
| **F3** | `Core/` 单层 14 子目录,无横向规则 | 🔴 Critical | **P1** | ❌ 未做 |
| **F4** | `ImageEx/` 引入 3 个非托管视觉库,污染全程序集 | 🟡 Warning | **P0** | ❌ 未做(本机缺 SDK,见 §10) |
| **F5** | `DesignPattern/*Example` 与运行时库混编 + 与 Prism 重叠 | 🟡 Warning | **P2** | ❌ 未做 |
| **F6** | `ConcurrentList<T>` 用 `lock` 自实现,且无 seam | 🟡 Warning | **P3** | ❌ 未做 |
| **F7** | `ReflectionEx/` 实际做 DI/MVVM 注册,命名误导 | 🟡 Warning | **P2** | 🟡 部分:命名空间已统一,改名 Bootstrap 未做 |
| **F8** | `Helper/` 与 `Tool/` 边界模糊 | 🟡 Warning | **P2** | ❌ 未做 |
| **F9** | `DynamicDllLoaderEx` 空 catch + `FileEx.Write` 静默吞错 | 🟡 Warning | **P1** | ✅ 已修(实际清理 20+ 处,不止这 2 处) |
| **F10** | 零测试项目(seam 缺失) | 🟡 Warning | **P1** | ❌ 未做 |
| **F11** | `Extension.sln` 引用不存在的 `Extension.UI.csproj`,导致解决方案无法构建 | 🔴 Critical | **P0** | ✅ 已修(原审计遗漏) |
| **F12** | `BitmapEx` GDI 句柄泄漏 + `UnlockBits` 未执行 | 🟡 Warning | **P1** | ✅ 已修(原审计遗漏) |
| **F13** | `DynamicDllLoaderEx.Dispose` 使用 `Thread.Abort()`,.NET 5+ 抛 `PlatformNotSupportedException` | 🟡 Warning | **P1** | ✅ 已修(原审计遗漏) |
| **F14** | `WPFControl` 存在三份 `LogR/` 死代码副本,被 csproj `Compile Remove` 排除但仍在磁盘上 | 🟡 Warning | **P2** | ⚠️ 待决策(见 §4.13) |
| **F15** | `.taskproj` / `.flow` 内嵌 C# 脚本,运行时编译,MSBuild 与常规检索均覆盖不到 | 🔴 Critical | **P1** | ✅ 已核查:本次 LogInfo 迁移未波及(见 §4.14) |

> F11–F15 是 2026-09-16 实际排查时新发现的问题,原审计遗漏。
> 其中 **F11 是硬阻塞**:不改它,`dotnet build Extension.sln` 连编译都无法开始,
> 这比 F1–F10 里任何一项都更该优先处理——说明"先写大方案"容易漏掉脚下的坑。
>
> **F14 不阻塞编译**,但会持续误导全仓库检索:本次 LogInfo 迁移时它就让人误以为
> `LogInfoDTO` 仍有引用,差点据此放弃删除 DTO。

---

## 3. 目标架构(重构后)

> **2026-09-16 修订:** 原方案规划了 9 个生产项目 + 3 个测试项目,对一个 92 个文件 / 16613 行的
> 工具库来说是**过度拆分**。这里收敛为 **2 个生产项目 + 1 个测试项目**。
> 判断依据见 §3.1 的「新建项目的准入规则」。

```
Extension.sln
├── Extension/                          [主库:零 SDK 依赖,net9.0-windows,WPF]
│   ├── Core/                            (现有 14 个 *Ex 目录,按 role 重组 → F3)
│   │   ├── Primitives/  Collections/  Concurrency/  Mapping/
│   │   ├── IO/          System/       Security/     Protection/
│   │   ├── Wpf/         (ControlEx/* ,原 Core/ControlEx)
│   │   └── Wiring/      (Register*Ex → F7,仅改目录与命名,不独立成项目)
│   ├── Domain/                          (原 Struct/,→ F2)
│   ├── Helper/ + Tool/                  (→ F8,二选一合并)
│   └── DesignPattern/                   (→ F5,待决策)
│
├── Extension.Vision/                   [视觉:唯一"必须"隔离的部分,net9.0-windows]
│   ├── Cognex/      (CogImageEx.cs)
│   ├── Halcon/      (HImageEx.cs + BitmapEx.ToHImage)
│   ├── OpenCv/      (MatEx.cs)
│   └── Bitmap/      (RawEx.cs / IntPtrEx.cs / SafeMemoryHandle / DynamicDllLoaderEx)
│
└── tests/Extension.Tests/              [单一测试项目,xUnit + FluentAssertions]
```

**注意 `BitmapEx` 需要一分为二**(它同时碰了 Halcon 和 WPF):

| 方法 | 依赖 | 归属 |
|---|---|---|
| `ToImageSource()` | 仅 WPF + System.Drawing | **留在主库** `Core/IO` 或 `Core/Wpf` |
| `ToHImage()` | Halcon `HObject` | **移入** `Extension.Vision/Halcon/` |

**强制依赖方向(只有一条边):**

```
Extension.Vision  ──>  Extension
        (反向引用禁止;仅一条边,靠 code review 即可守住,不需要 Roslyn 分析器)
```

### 3.1 新建项目的准入规则(防止再次膨胀)

**只有满足以下任一条件,才允许新建 csproj:**

1. **依赖隔离** —— 该部分依赖的第三方 SDK 并非所有开发者机器都有
   (如 Cognex / Halcon 的私有 dll)。**这是当前唯一的真实理由。**
2. **目标框架不同** —— 该部分需要不同的 `TargetFramework`
   (例如某组件要 `netstandard2.0` 供旧项目引用)。
3. **独立发布节奏** —— 该部分有独立版本号,且被外部项目单独引用。

> ❌ **仅为了"逻辑分层"而建项目 —— 不批准。**
> 分层用**目录 + 命名空间**表达即可。原方案里的 `Extension.Abstractions` /
> `Extension.Core` / `Extension.Infrastructure` / `Extension.Imaging` /
> `Extension.UI` / `Extension.Wiring` 全部属于这一类,因此全部取消。

### 3.2 为什么砍掉这 7 个项目

| 原规划 | 处理方式 | 理由 |
|---|---|---|
| `Extension.Abstractions` | ❌ 取消 | `IFileSystem`/`IImageSource` 等接口在只有 1 个实现时,单独建程序集是纯粹的成本 |
| `Extension.Core` | ❌ 取消 | 就是主库本身,改名不等于分层 |
| `Extension.Infrastructure` | ➡️ 变目录 | 文件/进程操作放主库 `Core/IO`、`Core/System`,命名空间已能表达层次 |
| `Extension.Imaging` | ❌ 取消 | `IImage<TPixel>` 抽象只有一个消费方,属过度设计;等真出现第二个后端再说 |
| `Extension.UI` | ➡️ 变目录 | WPF 控件仅 8 个文件,且与主库同为 `net9.0-windows` + `UseWPF`,物理分离无运行时收益 |
| `Extension.Wiring` | ➡️ 变目录 | Prism 注册扩展仅 2 个文件,移入 `Core/Wiring/` 即可 |
| 3 个 `Extension.Vision.*` | ⬇️ 合并为 1 个 | 见下 |

### 3.3 视觉库:先合并,按需再拆

Cognex / Halcon / OpenCvSharp4 **合并为一个 `Extension.Vision`**,而不是拆三个。原因:

- 实际视觉项目里三者通常**同时存在**,拆开只是把"必须装 3 个"变成"必须装 1 个",收益有限;
- 拆三个会让 `Extension.sln` 多出 3 个 csproj、3 条依赖边,以及随之而来的版本对齐成本。

**只有在出现以下场景时,才把它拆成 `Extension.Vision.Cognex` / `.Halcon` / `.OpenCv`:**

- 出现了"只装 Halcon、没装 Cognex"的开发者机器或 CI 环境,导致 `Extension.Vision` 编译不了;
- 或某个下游项目只想引用其中一个 SDK。

届时从 3 个项目演进到 5 个,是**有实际需求驱动的**,而不是一开始就预设。

---

## 4. 优化方向详解

### 4.1 [P0] F4 — 拆视觉库为独立 csproj

**症状:** `Extension.csproj` 同时引用 `Cognex.VisionPro / Cognex.VisionPro.Core / halcondotnet / OpenCvSharp4`。任何只要用 `StringEx` 的下游都必须拖上 Cognex/Halcon 的 native dll。

> **2026-09-16 修订:** 原方案此处要新建 **4 个** csproj 并引入 `IImage<TPixel>` 抽象层。
> 按 §3.1 的准入规则,只有"依赖隔离"这一条成立,所以收敛为**只新建 1 个项目**,
> 且**不引入图像抽象层**(只有一个消费方,属过度设计)。将来若需按 SDK 拆分,见 §3.3。

**目标结构(只加 1 个项目):**

```text
Extension/Extension.csproj                移除 Cognex / Halcon / OpenCv 的全部引用
Extension.Vision/Extension.Vision.csproj   + Reference Cognex.VisionPro(.Core)
                                           + Reference halcondotnet
                                           + PackageReference OpenCvSharp4
                                           + ProjectReference ../Extension/Extension.csproj
```

**步骤:**

1. 新建 **1 个** `Extension.Vision` csproj,`TargetFramework=net9.0-windows7.0`,`UseWPF=true`。
2. 移动文件:
   - `Core/ImageEx/CogImageEx.cs` → `Extension.Vision/Cognex/`
   - `Core/ImageEx/HImageEx.cs` → `Extension.Vision/Halcon/`
   - `Core/ImageEx/MatEx.cs` → `Extension.Vision/OpenCv/`
   - `Core/ImageEx/RawEx.cs`、`IntPtrEx.cs` → `Extension.Vision/Bitmap/`
   - `Core/IntPtrEx/SafeMemoryHandle.cs`、`DynamicDllLoaderEx.cs` → `Extension.Vision/Bitmap/`
   - `Core/ConvertEx/RegisterEx.cs` → 确认其 Halcon 依赖后再决定去留
   - **`Core/ImageEx/BitmapEx.cs` 一分为二**:`ToImageSource()` 留主库,`ToHImage()` 移入 `Extension.Vision/Halcon/`
3. 原 `Extension.csproj` 删除 Cognex/Halcon/OpenCv 的 `<Reference>` / `<PackageReference>`。
4. 需要视觉能力的项目自行 `ProjectReference` 到 `Extension.Vision`,不再"全捆绑"。
5. **不引入** `IImageSource` / `IImage<TPixel>` 抽象 —— 只有一个实现时纯属成本。

**验证:**

- `Extension.dll` 不再依赖 native Halcon/Cognex
  (用 `dotnet list Extension package` + `dumpbin /dependents Extension.dll` 验证)。
- **本机就是现成的验证环境:** 本机器正好缺 SDK,当前 `dotnet build` 报 21 个 `CS0246`。
  拆完之后,**主库应当 0 错误**、`Extension.Vision` 才报错 —— 这就直接证明了隔离生效,
  无需额外的 CI 环境(临时验证办法见 §10)。

---

### 4.2 [P0] F1 — 切断 Struct↔Mapping 双向耦合

**症状:** `Struct/LogInfo.cs`、`Struct/ImageInfo.cs` 内嵌 `*Mapping.Configure(TypeAdapterConfig)`;`Helper/Mapping/MapsterHelper.MappingConfig.InitializeAllMappings()` 静态开关 + 手动登记。

**改造:**

```text
Extension/                        (主库;不再拆出 Extension.Abstractions / Extension.Core)
├── Domain/Logging/LogInfo.cs     (纯 POCO)
├── Domain/Imaging/ImageInfo.cs   (纯 POCO)
└── Core/Mapping/Profiles/
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
Extension/Domain/                 (主库内目录,非独立项目)
├── Logging/    —— 已于 2026-09-16 迁入 DataManager.Log.Definition,此处不再保留(见 §4.11)
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
| `Core/DataEx/ZIPEX.cs` | `Core/IO/ZipEx.cs`(依赖 IO) |
| `Core/FileEx/*` | `Core/IO/*` |
| `Core/TaskEx/TaskEx.cs` | `Core/Concurrency/TaskEx.cs` |
| `Core/SystemEx/ProcessEx.cs, CpuMonitorEx.cs, DelayEx.cs, FpsEx.cs` | `Core/System/` |
| `Core/SystemEx/LangSwitchEx.cs` | `Core/System/LangSwitchEx.cs` |
| `Core/SystemEx/ScheduledDeletion.cs` | `Core/IO/ScheduledDeletionEx.cs`(注意:与 `FileEx/ScheduledDeletionEx.cs` 合并) |
| `Core/ControlEx/*` | `Core/Wpf/*`(WPF 控件,留主库,不独立成 `Extension.UI`) |
| `Core/ReflectionEx/ReflectionEx.cs` | `Core/Reflection/ReflectionEx.cs` |
| `Core/ReflectionEx/RegisterNavigationViewEx.cs, RegisterServicesEx.cs` | `Core/Wiring/`(见 F7;**不独立成 `Extension.Wiring`**) |
| `Core/SemaphoreEx/`(空) | **删除** ✅ 已完成 |

> **2026-09-16 修订:** 目标路径中的 `Infrastructure/`、`Extension.UI/`、`Extension.Wiring/`
> 原为独立项目,现按 §3.1 收敛为主库内的 `Core/IO/`、`Core/Wpf/`、`Core/Wiring/` 目录。

**准入规则(写进 CONTRIBUTING):**

> Core 层的每个文件必须能用一句话回答:
> 1. 它**不依赖**任何 SDK、原生 dll。
>    (文件系统 / WPF / 网络**允许** —— 它们本就在主库内,不再为此单独建项目。)
> 2. 它只**被**上层的 `Extension.Vision` 或下游应用引用。
> 3. 它的名字反映**职责**而非**对象类型**(`Reflection` 而非 `ReflectionEx`)。
>    ✅ 该项命名空间部分已于 2026-09-16 完成。

---

### 4.5 [P1] F9 — 修复静默异常 ✅ 已于 2026-09-16 修复

> **已修,实际范围远大于本节描述。** 本节只点名 2 处(`DynamicDllLoaderEx`、`FileEx.Write`),
> 实际排查出 **20+ 处**静默 `catch`,分布在 `ProtectionEx/`(6 处)、`CpuMonitorEx`(6 处)、
> `ImageEx/`、`SecurityEx/`(`RsaEx`/`MachineCodeEx`/`LicenseManagerEx`)、`ConvertEx/JsonEx`、
> `FileEx/`、`SystemEx/LangSwitchEx`、`ReflectionEx`。
>
> **统一处理原则(不是一律改成抛异常):**
> 1. 先判断该 catch 是"**刻意吞掉**"还是"**真 bug 静默**"。反调试/性能计数器/验签这类探测代码,
>    失败本就该降级,不该让检测逻辑自己崩溃 —— 这类**保留降级语义**。
> 2. 但一律**收紧异常类型**(`catch` → `catch (Exception ex) when (ex is ...)`),
>    避免把空引用、参数错误这类代码缺陷也吞成"环境不支持"。
> 3. 一律**留痕**(`Debug.WriteLine`),不引入日志框架依赖,保证零新增 NuGet 包。
> 4. `catch { return null; }` 这类"返回假对象"的写法最危险,优先改掉 —— 它把错误推到调用方才爆炸。
>
> `SingletonPattern.cs` 中的 `catch { _initializationFailed = true; throw; }` 是正确写法(设标记后重抛),**不应改**。

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

1. 新建**一个** `tests/Extension.Tests/`。
   (原方案规划 3 个测试项目;当前覆盖率为 0%,先有 1 个能跑起来再说,不够用时再按层拆。)
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

### 4.8 [P2] F7 — `ReflectionEx` → `Core/Wiring/` 🟡 部分完成(原为 `Extension.Wiring/Bootstrap`)

> **2026-09-16 只做了其中一半:** 命名空间分裂已解决 —— 同目录下的
> `Extension.Core.Reflection`(`ReflectionEx.cs`)与 `Extension.Core.ReflectionEx`
> (`RegisterServicesEx.cs`、`RegisterNavigationViewEx.cs`)已统一为 **`Extension.Core.Reflection`**,
> 并同步更新了 5 个跨项目 using(Device / WPFControl / Secs / ScriptEditor / Packing Machine)。
>
> **本节尚未落地。** 且按 §3.1 的准入规则,**不再新建 `Extension.Wiring` 项目** ——
> 目标路径改为**主库内的 `Core/Wiring/` 目录**,只是改目录 + 改名,不动项目结构。

**改造:**

```text
Extension/Core/Wiring/             (主库内目录,不是独立 csproj)
├── ServiceBootstrapper.cs          (RegisterServicesEx 改名)
├── NavigationBootstrapper.cs       (RegisterNavigationViewEx 改名)
└── Conventions/                    (命名空间 → 生命周期 约定)
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

### 4.11 [P0] 日志模型迁出 Extension ✅ 已于 2026-09-16 修复

**症状(依赖方向倒置):** `Extension/Extension/Struct/LogInfo.cs` 定义了 `ELogLevel` / `LogInfo` /
`LogInfoDTO` / `LogInfoMapping`,但完整的日志模块在 **DataManager**(`Log/` 下有 `ILog`、`LogBase`、
`LogManager`,以及 Serilog / NLog / Log4Net 三套实现,还有 `Log/Definition/` 目录)。结果是:

```csharp
// DataManager/Log/ILog.cs —— 日志模块的公开 API,暴露的却是 Extension 的类型
void Add(string info, ELogLevel logLevel = ELogLevel.Information);
List<LogInfo> Query(string fileName);
```

真正的日志模块,要依赖通用工具库才能拿到日志模型 —— 方向是反的。Extension 里全是
`StringEx` / `FileEx` / `RsaEx` / `ReflectionEx` 这类无业务语义的工具,它不该知道"日志等级"是什么。

**改造:** 迁到 `DataManager.Log.Definition`(与已有的 `ELogType`、`LogParam` 同目录),
并**删除 `LogInfoDTO` 与 `LogInfoMapping`**。

**为什么可以删掉 DTO:** 调查 `WPFControl/ProgramControl/Services/Log/LogQueryService.cs` 发现,
所有业务逻辑都用 `LogInfo`,DTO 只挂在界面上:

| 功能 | 代码 | 用的是 |
|---|---|---|
| 字段筛选 | `typeof(LogInfo).GetProperty(FilterItem)` + `?.ToString().IndexOf(...)` | `LogInfo` |
| 等级筛选 | `s.Level == Model.SelectItem`(enum 比较) | `LogInfo` |
| 饼图统计 | `LogDatas.Where(s => s.Level != ELogLevel.Information)` | `LogInfo` |
| CSV 导出 | `QuerySaveCSVAsync(Model.LogDatas)` | `LogInfo` |
| DataGrid 显示 | `ItemsSource="{Binding Datas.LogInfoDTO}"` | DTO |

DataGrid 为自动生成列(`ColumnWidth="Auto"`),直接绑定 `LogInfo` 时 `Level` 走 `ToString()`,
与原 DTO 的 `Level.ToString()` 显示结果一致 —— 因此 DTO 连同为它服务的映射可以一并删除,
连"Mapster 映射注册入口"都不需要了。

**成本比预期低的关键:** 依赖链为 `WPFControl → FlowNodes → {DataManager, Extension}`。
按 §3.1 的准入规则,此处**不新增项目**:

- WPFControl 原本靠**传递引用**访问 DataManager,迁移后已补显式 `ProjectReference`(传递依赖太脆弱)
- `DataManager.Controls`、`FlowEditor.DataManagerLogging` 本来就显式引用 DataManager
- Packing Machine 经 WPFControl 传递可达

**踩坑记录(供后续类似迁移参考):**

1. `grep` 判断"文件是否用到某类型"会被**注释里的提及**误导。
   批量替换前必须过滤注释行:`grep -vE "^\s*(//|///|\*)"`。
2. **全限定名用法不会被 using 替换覆盖**。
   需单独 `grep "Extension\.Struct\."` 处理(本次有 `Extension.Struct.ELogLevel.Warning` 等 2 处)。
3. 整个 worktree 跑 grep 会因 `Secs/secs4net-base/` 过大而超时,需限定目录。

**验证:** Extension 与 DataManager 均用临时项目验证 0 错误(见 §10);
WPFControl / Packing Machine 因缺 Halcon SDK 无法编译验证,改动为 using 替换 + XAML 绑定改名。

### 4.12 [P2] `Struct/` 剩余成员的归属 —— ⚠️ 待决策(2026-09-16)

`LogInfo` 迁走后,`Struct/` 只剩 3 个文件 8 个类型。逐个分析后**结论是"暂不动"**,
理由与数据如下(依赖方向:`Extension ← DataManager ← {Secs, Communication}`、
`Extension ← Communication ← Device`、`DataManager ← WPFControl`):

| 成员 | 使用方(文件数) | 自然归属 | 能否迁 | 结论 |
|---|---|---|---|---|
| `EDeviceStatus` | Secs 4、Packing Machine 3、WPFControl 3 | Secs(上报主机用) | ❌ | **留**。Device 根本不用它;迁去 Secs 需给 WPFControl/Packing Machine 新增对 Secs 的引用 |
| `ESecsMode` | Secs 5、Packing Machine 5、WPFControl 3 | Secs | ❌ | **留**。同上,SEMI 模式是 Secs 领域概念,但消费方跨 3 个项目且都未引用 Secs |
| `ImageInfo` / `ImageInfoDTO` / `ImageInfoMapping` | **仅 WPFControl 2** | WPFControl(UI 查询页模型) | ✅ | **可迁但暂缓**。需同步把 `MapsterHelper.InitializeAllMappings()` 里的注册一起搬走 |
| `KeyValue` | Communication 2、Device 2、WPFControl 2 | 无 | ⚠️ | **留**。三方公共祖先只有 Extension;放 DataManager 语义更错 |

**两个容易误判的点:**

1. **`ImageInfoDTO` 不能像 `LogInfoDTO` 那样删。**
   它与日志 DTO 结构相似,但 `ImageInfo.ImagePath` 是 `List<string>`,
   自动生成的 DataGrid 列会显示成"(集合)",DTO 把它压成 `ImageCount` 是**真实需求**;
   而且 `ImageQueryView.xaml:60` 明确绑定了 `Datas.ImageInfoDatasDTO`。
   判定 DTO 该不该留,要看**源类型里有没有无法直接呈现的字段**,而不是看 DTO 本身。
2. **`Device` 已经不用 `Struct/` 了。** `Device/ManagerBase.cs` 的 `using Extension.Struct;`
   在迁移后变成空挂(已删)。所以"设备状态归 Device"这个直觉是错的——Device 自己不消费它。

**为什么不为 `EDeviceStatus`/`ESecsMode` 新建 `Secs.Abstractions`:**
§3.1 的准入规则只允许"依赖隔离 / 不同 TFM / 独立发布节奏"三种理由,
"让枚举有个家"不满足任何一条,而且会与"目标架构收敛到 2 个项目"直接冲突。
真要解决,应等 §4.1 拆 `Extension.Vision` 时一并评估。

### 4.13 [P2] F14 — `WPFControl` 的 `LogR/` 死代码副本 ⚠️ 待决策

`WPFControl.csproj` 里存在三组 `Compile Remove`(第 11–22 行):

```xml
<Compile Remove="ProgramControl\Models\LogR\**" />
<Compile Remove="ProgramControl\Services\LogR\**" />
<Compile Remove="ProgramControl\ViewModels\LogR\**" />
```

这三个目录在磁盘上存在,是 `Log\` 对应文件的**旧副本**(部分文件字节数完全一致),
但**永不参与编译**。危害:

- 全仓库搜索时会被命中,污染判断。本次迁移就因此误以为 `LogInfoDTO` 仍有 3 处引用。
- 会随主副本演进持续腐化,且不会有任何编译器报错提醒。
- `LogR\LogQueryModel.cs` 至今仍在用已删除的 `LogInfoDTO`,**如果哪天有人把 `Compile Remove` 去掉,会直接编译失败**。

**建议:直接删除这三个目录**(已在 Git 里有历史,可回滚)。
因涉及删除文件,**未擅自执行,待确认**。替代方案是在目录内放 `README.md` 说明其为废弃副本。

#### 全仓库 `Compile Remove` 审计(2026-09-16)

既然 `LogR` 能骗到人,就把所有排除项都查了一遍。**结论:`LogR` 是唯一的真死代码,
另外 5 处都不能按"死代码"处理。**

| 项目 | 排除项 | 路径存在 | 性质 | 处置 |
|---|---|---|---|---|
| `WPFControl` | `ProgramControl\{Models,Services,ViewModels}\LogR\**` | ✅ | **真死代码**:`Log\` 的旧副本 | 建议删 |
| `Packing Machine` | `Config\Flow\StartupFlow\01~05_初始化*.cs` | ✅ | **不是死代码**:ScriptEditor 源文件,运行时由脚本引擎编译 | **禁止删** |
| `Vision` | `Control\HSmartWindowControl*.cs` | ✅ | 有意排除:Halcon 设计器控件 | 保留 |
| `FlowNodes` | `Communication\Socket\**` | ❌ | 残留配置(目录已不存在) | 可清理该行 |
| `FlowEditor` | `Core\HObjectConverter.cs` | ❌ | 残留配置(文件已不存在) | 可清理该行 |
| `DPLibrary_Halcon_F48` | `Tools\æ–°æ–‡ä»¶å¤¹\**` | ❌ | 残留配置 + **csproj 被 GBK 写坏**("新文件夹"变成乱码) | 可清理该行 |

**教训:看到 `Compile Remove` 不要默认它是死代码。** `Packing Machine` 那 5 个文件
看着像"被排除的旧流程脚本",实际是流程编辑器的**源**,和 `StartupFlow.taskproj` 配套;
真正运行时加载的是 `.taskproj` 里内嵌的那份。

### 4.14 [P1] F15 — 运行时编译的脚本不在 MSBuild 视野内 ⚠️ 已核查,本次无影响

`Packing Machine/Config/Flow/` 下的 `.taskproj` 是 JSON,把 C# 源码以字符串形式存在
`"Scripts"[i]."Code"` 字段里,由脚本引擎在**运行时**用 Roslyn 编译。这意味着:

- 它们**不参与 MSBuild**,改了类型/MVVM 之后 `dotnet build` **不会报错**,只会在运行时炸。
- grep `*.cs` / `*.xaml` **扫不到它们** —— 本次 LogInfo 迁移的第一轮扫描就漏了。
- 全仓库同类文件:`StartupFlow.taskproj`、`ShutdownFlow.taskproj`、`AutoFlow.flow`、`InitializeFlow.flow`。

**本次迁移的核查结果(安全):**

- `StartupFlow.taskproj` 未修改(`git diff` 为空),内含 `using Extension.Struct;` 与
  `using DataManager.Log.Definition;` 两种 —— 前者是因为脚本 1/5 仍在用 `EDeviceStatus`(本次未迁走,正确)。
- 四个文件里**均不出现** `LogInfo` / `ELogLevel` / `LogInfoDTO` → 迁移不影响运行时脚本。

> **后续任何涉及 `Extension.Struct`、`WPFControl` 类型的改动,都必须把这 4 个文件纳入检索范围。**
> 检索式:`grep -r "符号名" --include=*.taskproj --include=*.flow .`

---

## 5. 落地路线图

**2026-09-16 已完成的三项(原路线图没有它们,应优先于 M0):**

| 阶段 | 任务 | 状态 |
|---|---|---|
| **前置** | F11:移除 sln 中不存在的 `Extension.UI.csproj`(否则整个 sln 无法构建) | ✅ 已完成 |
| **前置** | F9 + F12 + F13:清理 20+ 处静默 `catch`、修复 `BitmapEx` GDI 泄漏、`Thread.Abort` | ✅ 已完成 |
| **前置** | F7(部分):统一 `Reflection`/`ReflectionEx` 命名空间 + 删 `SemaphoreEx/` 占位 | ✅ 已完成 |

**剩余路线图:**

| 阶段 | 周次 | 任务 | 验收 | 状态 |
|---|---|---|---|---|
| **M0** | W1 | 建 `tests/` 空架子 + CI 跑 `dotnet build` | CI 绿 | ⬜ 待做 |
| **M1** | W2 | F4:新建**1 个** `Extension.Vision`,主库移除视觉 SDK 引用(不再是 4 个 csproj) | 主库 `Extension.dll` 不再依赖 Cognex/Halcon/OpenCV;本机缺 SDK 时主库应能编译通过 | ⬜ 待做 |
| **M2** | W3 | F1:`*Mapping.Configure` 迁 `Mapping/Profiles/`,启用 Mapster Scan | `Struct/` 不再 `using Mapster` | ⬜ 待做 |
| **M3** | W4 | F2:`Struct/` → `Domain/`,可变 struct → record | 0 个可变 `public struct` 业务类型 | ⬜ 待做 |
| **M4** | W5–W6 | F3:Core 按 role 重组(~~删 `SemaphoreEx/` 空目录~~ 已完成) | Core 子目录 ≤ 6 个,每个子目录有 README 说明职责 | 🟡 部分 |
| **M5** | W6 | ~~F9:修复两处静默 `catch`~~ | CodeQL / Roslyn 分析器 0 命中 `catch { }` | ✅ 已完成 |
| **M6** | W7 | F10:补 seam 接口 + 单元测试覆盖 Core | Coverage ≥ 60% | ⬜ 待做 |
| **M7** | W8 | F7:`Register*Ex` → `Bootstrap/`,依赖 Prism | `ReflectionEx` 文件夹不再有 DI 代码 | 🟡 部分 |
| **M8** | W9 | F5:评估并删除/迁出 `DesignPattern/` | 0 个 `*Example.cs` 在运行时 dll | ⬜ 待做 |
| **M9** | W10 | F8:Helper + Tool 合并决策与迁移 | 二选一并完成 | ⬜ 待做 |
| **M10** | W11 | F6:决定 `ConcurrentList<T>` 命运 | 决定并落到代码 | ⬜ 待做 |
| **M11** | W12 | 全量回归 + 版本号 bump 1.0 | Release notes 与 CHANGELOG | ⬜ 待做 |

> 原路线图的 12 周是按"从 M0 到 M11 顺序推进"设计的,但它把 sln 修复、静默异常这类
> **几分钟就能改完且风险极低**的项排到了 W6,却把"建测试架子"排在 W1。
> 建议后续按"**先清掉所有低成本缺陷,再动结构**"的顺序推进。

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
T:Extension.Core.ReflectionEx.RegisterServicesEx;Moved to Extension.Core.Wiring
T:Extension.Struct.LogInfoDTO;Renamed to Extension.Domain.Logging.LogInfoDto
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
- ❌ 把 `Mapping/Profiles/` 放进 `Extension.Vision`(它只依赖主库 `Extension`,放进去会造成反向依赖)。
- ❌ **为了"逻辑分层"新建 csproj**(2026-09-16 补充,见 §3.1)。
  本方案初版规划了 9 个生产项目,其中 7 个纯粹是为了分层,对 92 个文件的库是净负担。
  分层用**目录 + 命名空间**表达;只有"依赖隔离 / 目标框架不同 / 独立发布节奏"才值得建项目。
- ❌ 把 `ConcurrentList<T>` 与 `ConcurrentQueue<T>`/`ImmutableList<T>` 同时保留(选一个,文档写明何时用)。
- ❌ 用 `[Obsolete]` 软删除核心 API,半年后没人删 → 重新腐烂;要么删干净,要么写迁移指南后立刻删。
- ❌ 在新 csproj 中保留旧 `Extension.csproj` 作为转发包 — 这只是延迟决策,不要做"兼容垫片"。

---

## 8. 风险与回滚

| 风险 | 影响 | 缓解 |
|---|---|---|
| 拆 `Extension.Vision` 导致下游引用路径改名 | **中**(原评"高",是因为按 9 项目方案评估;收敛为 1 个项目后影响面大幅缩小) | 只移动 6 个视觉相关文件,且只影响用视觉能力的调用方;`InternalsVisibleTo` + 临时转发命名空间过渡(最长 2 个 minor 版本) |
| 可变 struct → record 破坏二进制兼容 | 中 | major 版本号 bump;序列化路径用 `[StructLayout]` 显式控制 |
| 删除 `DesignPattern/` 致外部调用方报错 | 中 | 在删除前一个 minor 标记 `[Obsolete("error: true")]`,CHANGELOG 注明 |
| 单元测试覆盖不足时引入 seam | 中 | seam 引入按"纯逻辑 → IO"顺序推进,先覆盖无副作用的部分 |
| 目标架构再次膨胀 | 中 | 严格遵守 §3.1 的准入规则;新增任何 csproj 都需写明满足哪一条 |

---

## 9. 验收标准(Definition of Done)

**已达成(2026-09-16):**

- [x] `Extension.sln` 引用的每个 csproj 都真实存在(`dotnet sln list` 可正常解析)。
- [x] `grep -rn "catch\s*{" Extension/Extension --include=*.cs` 返回 0 行
      (仅剩 1 处被注释掉的代码,以及 `SingletonPattern.cs` 中 `catch { _initializationFailed = true; throw; }` 这一正确写法)。
- [x] `Core/ReflectionEx/` 下不再同时存在 `Extension.Core.Reflection` 与 `Extension.Core.ReflectionEx` 两个命名空间。
- [x] `Extension.csproj` 中不再有 `Core\SemaphoreEx\` 空目录占位。
- [x] `BitmapEx` 的 `UnlockBits` 与 `DeleteObject` 均在 `finally` 中执行。

**未达成:**

- [ ] `dotnet list package` 在 `Extension.Core` 不出现 Cognex/Halcon/OpenCv/Prism。
- [ ] `grep -r "using Extension.Struct" Extension.Core/` 返回 0 行。
- [ ] `Core/` 子目录 ≤ 6 个,每个含 README.md。
- [ ] `tests/` 下覆盖率 Core ≥ 60%、Infrastructure ≥ 40%。
- [ ] `Extension.sln` 引用 **3 个** csproj:`Extension`、`Extension.Vision`、`Extension.Tests`。
- [ ] CI 含 BannedApiAnalyzer,合并前必须绿。
      (架构依赖规则只需校验 `Extension.Vision → Extension` **这一条**单向边,
      比原方案的 5 层依赖图简单得多,不必上 NDepend 这类重武器。)
- [ ] CHANGELOG 记录本方案所有破坏性变更。

---

## 10. 如何在本机验证改动(缺 Cognex/Halcon SDK 时)

`Dll/Vision/` 未入库,`dotnet build` 必然报 21 个 `CS0246` + `MSB3245`,无法用编译结果判断改动是否正确。
以下两种绕行办法均已验证可用。

### 10.0 前置:必须补 ProgramFiles 系环境变量

本机 Git Bash 会话里 `ProgramFiles(x86)`、`ProgramFiles`、`ProgramData` 等为空,
会让 NuGet 的 `ConfigurationDefaults` 静态构造抛 `Value cannot be null. (Parameter 'path1')`,
表现为 `dotnet restore`/`build` 直接失败。整条命令要带 `env` 前缀(括号变量无法 `export`):

```bash
env "APPDATA=C:\Users\91632\AppData\Roaming" \
    "ProgramData=C:\ProgramData" \
    "ALLUSERSPROFILE=C:\ProgramData" \
    "ProgramFiles=C:\Program Files" \
    "ProgramFiles(x86)=C:\Program Files (x86)" \
    "CommonProgramFiles=C:\Program Files\Common Files" \
    dotnet build <项目>
```

### 10.1 临时 csproj 排除法(改动不触及视觉库文件时)

在项目目录建临时 csproj(如 `__syntaxcheck.csproj`),排除依赖 SDK 的文件:

```xml
<PropertyGroup>
  <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  <BaseIntermediateOutputPath>obj-check\</BaseIntermediateOutputPath>
  <BaseOutputPath>bin-check\</BaseOutputPath>
</PropertyGroup>
<ItemGroup>
  <Compile Include="**\*.cs"
           Exclude="obj\**;bin\**;obj-check\**;bin-check\**;Core\ImageEx\**;Core\ConvertEx\RegisterEx.cs" />
</ItemGroup>
```

`PackageReference` 照抄原项目,**去掉** Cognex/Halcon 的 `<Reference>`。
必须指定独立的 `BaseIntermediateOutputPath`/`BaseOutputPath`,否则会和原项目的 obj 冲突。
验证完删除临时 csproj 与 `obj-check`/`bin-check`。

依赖 SDK 的文件清单:`Core/ImageEx/**`、`Core/ConvertEx/RegisterEx.cs`。

### 10.2 stub 法(改动刚好在依赖 SDK 的文件里,如 `BitmapEx.cs`)

建临时目录,写一个最小 stub 提供缺失类型,再把源文件链接进来单独编译:

```csharp
namespace HalconDotNet { public class HObject { } }

public static class HalconExtStub
{
    public static HObject ToHImageByte(this IntPtr p, int w, int h) => new HObject();
    public static HObject ToHImageRGB(this IntPtr p, int w, int h) => new HObject();
}
```

```xml
<ItemGroup>
  <Compile Include="HalconStub.cs" />
  <Compile Include="..\Core\ImageEx\BitmapEx.cs" />
</ItemGroup>
```

stub 的方法签名必须与项目内真实定义一致(见 `Core/ImageEx/IntPtrEx.cs`)。

### 10.3 更省事的排除法:临时 `Directory.Build.targets`(推荐)

§10.1 的临时 csproj 有个麻烦:下游项目(如 DataManager)仍会去构建**真的** Extension.csproj,
验证不了。在**仓库根目录**放一个临时文件即可全局生效:

```xml
<!-- Directory.Build.targets —— 临时,验证完立即删除 -->
<Project>
  <PropertyGroup Condition="'$(MSBuildProjectName)' == 'Extension'">
    <_LocalSkipVisionSdk>true</_LocalSkipVisionSdk>
  </PropertyGroup>
  <ItemGroup Condition="'$(_LocalSkipVisionSdk)' == 'true'">
    <Compile Remove="Core\ImageEx\**" />
    <Compile Remove="Core\ConvertEx\RegisterEx.cs" />
  </ItemGroup>
</Project>
```

> ⚠️ **必须是 `.targets`,不能是 `.props`。**
> `Directory.Build.props` 在项目**顶部**导入,而默认 `<Compile Include="**/*.cs">`
> 由 `Sdk.props` 生成 —— 此时还没有条目,`Remove` 是空操作(实测 21 个错误一个不少)。
> `.targets` 在项目**末尾**导入,此时默认条目已生成,`Remove` 才有效。

根目录原本没有 `Directory.Build.*`(`Secs/secs4net-base/` 下那两个离得更近,不受影响)。
**验证完必须删除**,否则会污染整个仓库的构建。

### 10.4 各项目本机可验证性矩阵(2026-09-16 实测)

| 项目 | 缺失的私有 SDK | 本机能否编译验证 |
|---|---|---|
| `Extension` | Cognex / Halcon(`Dll/Vision`) | ⚠️ 加 §10.3 排除后可过 |
| `DataManager` | 经 Extension 传递 | ✅ 加 §10.3 后 **0 错误**(126 警告,均为既有可空/CA) |
| `Secs` | 经 Extension 传递 | ✅ 加 §10.3 后 **0 错误** |
| `Communication` | Miracom(`Dll/H101`)、Act COM(`Dll/Mitsubishi`)、FarPoint(`Dll/Print/Spread`) | ❌ **不可** |
| `Device` | 经 Communication 传递 | ❌ **不可** |
| `WPFControl` | Cognex / Halcon | ❌ **不可** |
| `Packing Machine` | 经 WPFControl 传递 | ❌ **不可** |

`Communication` 无解的原因:缺失类型被 `CommunicationManager.cs` / `CommunicationModule.cs`
直接引用,排除 `H101\`、`MC\` 会连带砍掉主干,越排除越残。

**对不可验证项目的兜底做法:** 只做"删除已确认无引用的 using / 改绑定路径"这类机械改动,
并用脚本枚举命名空间内**全部**类型名(而不是只 grep 一个符号)来确认无引用,再批量执行。
本次即用此法处理 Communication / Device / WPFControl / Packing Machine 共 10 个文件。
这样可以在完全没有 Halcon SDK 的情况下验证 `BitmapEx` 的语法、可空标注与 `finally` 逻辑。