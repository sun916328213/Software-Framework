# Extension 文件分级方案（v2 · 2026-09-18 **已实施**）

> 范围：`Extension/Extension/` 单 csproj，**80 个 `.cs` / 14,614 行**（逐文件实测，口径 `splitlines`）。
> 方法：逐文件解析 `using` → 反查依赖包 → 按「依赖级别」分层；再按「变化原因」做归属审查。
> 落地：**不新建 csproj**，准入线由 `Extension.Tests/TierGuardTests.cs` 强制（**12/12 绿**）。
> 状态：**目录重排 + 命名空间改名已执行完毕**（80 文件移动 / 92 处 namespace 改写 / 下游 73 文件 using 改写），
> `Extension.sln` **0 错误**、`Extension.Tests` **921/921 绿**、全量 `Frame.sln` 对照基线 **0 错误**。
> 测试面：`Extension.Tests` **21 → 921 用例**（§5.10），过程中发现 **165 处** API 陷阱/缺陷（§5.11–§5.30 + 序 72 / 73），
> 其中 **序 3 / 8b（`ToBytes(this float)` + `ToASCII`）/ 27 / 28 / 32 / 36 / 37 / 41 / 43 / 47 / 57 / 60 / 64 / 65 / 72 / 73 已修**（2026-09-18，用户**逐项**明确授权 —— 授权不累积，见 §7），
> 其余**只记录 + 用测试钉住**、未改产品代码（另一个例外是序 12d 的分级修正）。
> ⚠️ 已修的 17 条分**五批打包授权**：第一批「低风险加固包」= 序 3 / 36 / 37 / 47；
> 第二批「极低风险三条包」= 序 28 / 43 / 57 / 64 / 65；
> 第三批「语义注释 + 单点零调用修复」= 序 60（4 处 XML 注释）/ 序 8b（**仅** `ToBytes(this float)` 一处）；
> 第四批「副作用消除」= 序 8b 的 `ToASCII`（改成反转**副本** + 同批**改写 1 条用例**）；
> 第五批「单点可见性修复」= 序 73（`SingletonInstanceAsync<T>` 三字段补 `volatile` + 同批**改写 1 条用例**）。
> **五批授权互不覆盖**，也都不构成对表外任何一项的授权。
> ⚠️ 序 8b **已结案**：`float` 的 `ToBytes`（第三批）与 `ToASCII`（第四批）已修；
> 第 3 处 `ToHexBools` 经第五轮只读侦察**结论为「无需改代码」**（实现正确，只有名字误导，见 §5.13）。
> ⚠️ 序 **73** **也已结案**（2026-09-18 第五批打包授权）：`SingletonInstanceAsync<T>` 的
> `_instance` / `_initialized` / `_initializationFailed` 三字段已补 `volatile`（**只改可见性、不改语义**，见 §5.30.1 第 73 条）。
> **T0 层覆盖 60 / 60 文件（100.0%）** —— 口径与可复现的复核方法见 §5.10 末尾（**四种机制并存**，
> 按"类型名出现在测试代码里"严格判只有 53/60，别只看那个 100%）。
> 结论：**原 5 个顶层目录用了 5 套互不相干的分类标准，所以"新文件该放哪"没有可推导的答案。**
> 改为「依赖分级（T0–T3）+ 职责目录」两个正交维度后，规则可推导、可校验、可机器检查。

---

> **状态更新（2026-09-20）**：本文中的 DeleteExpiredFiles1 行为描述仅用于记录旧实现缺陷；该兼容入口现已删除，Retention 只暴露 ScheduledDeletion.DeleteExpiredFiles。
## 0. 先给结论

> ### 本方案的 3 条硬约束（不可协商）
> 1. **分级依据 = 逐文件实测**（解析每个文件的真实 `using`），不按文件名、不按目录名、不凭印象。
> 2. **不新建 csproj。** 分级全部在 `Extension.csproj` **单程序集内**用「目录 + 命名空间 + 准入线」落地。
> 3. **不按 NuGet 包拆项目。** "有个包就建个工程"是最贵的错解 —— 它把"依赖治理"变成了"项目管理"。

| 问题 | 结论 |
|---|---|
| 原分级合理吗？ | **不合理。** `Core/`(技术域) / `DesignPattern/`(模式名) / `Helper/`(模糊词) / `Tool/`(模糊词) / `Struct/`(语言构造) —— **5 套标准并存**，任一新文件都找不到可推导的位置。 |
| 该怎么分？ | **按依赖级别分 T0–T3**（这是"能不能放进来"的硬门槛），**目录名按职责**（这是"放哪一格"）。两者正交，不要混成一个。 |
| 要不要拆程序集？ | **不要。** 详见 §3.2：本仓是同一 sln 内的 monorepo 内部库，拆程序集的三个正当理由一条都不满足；T3 那 5 个文件留在库里，靠**准入线 + 守卫测试**隔离即可。 |
| 分完发现了什么？ | **约 51%（7,462 行）的内容不属于"通用工具库"**：业务契约、发布形态、宿主服务、教学示例。它们不是"放错格"，是"不该在这张表上"。 |
| 依赖污染还剩多少？ | **只剩 Halcon/OpenCV 一项**（5 文件 / 434 行）；Cognex 已不在 `Extension.dll` 内。但 **21 个 bin 里仍有 129 MB 陈旧 Cognex 副本**（无工程引用，纯残留）。 |
| 重排之后结构如何？ | **内部依赖只有 4 条边**（向下 3 + 同层 1），**零反向引用**（见 §6.L）—— 7 个顶层目录构成一张分层 DAG。⚠️ 原文写的「7 条边 / 全部向下」**机械复核后已修正**（2026-09-18，序 12i） |

---

## 1. 分级依据：逐文件实测

对 80 个文件逐个解析**源码文本**（不只 `using`，也含正文里的完全限定名 —— 这条是 2026-09-18 补的，见 §3.3 / §4.7），
按「该文件用到的**最高**依赖级别」归级。结果**高度集中**：

| 级别 | 名称 | 依赖预算（准入线） | 文件 | 行数 | 占比 |
|---|---|---|---|---|---|
| **T0** | 零依赖 | **只允许 `System.*`（BCL）** | **60** | **13,044** | **89.3%** |
| **T1** | 需 UI/宿主 | 允许 `System.Windows.*` / `Microsoft.Win32` | 8 | 586 | 4.0% |
| **T2** | 需第三方包 | 允许 `MaterialDesign*` / `Prism` / `Mapster` / `MiniExcel` / `FastDeepCloner` | 7 | 550 | 3.8% |
| **T3** | 需 vendor SDK | 允许 `HalconDotNet` / `OpenCvSharp`（+`System.Drawing`） | 5 | 434 | 3.0% |
| | | **合计** | **80** | **14,614** | 100% |

> **89% 的文件零依赖** —— 这是本方案最重要的一个数字。
> 它意味着：把 T1–T3 那 20 个文件隔离出去，`Extension` 主体就是**一个纯 BCL 库**。
>
> 归级规则：取该文件全部 `using` 中的**最高**级别（例：`ThemeService.cs` 同时用 WPF(T1) 与 MaterialDesign(T2) ⇒ 记 T2）。
> 这比"按主要用途"更保守 —— 只要沾了高层依赖，就按高层对待。

**2026-09-18 补测时复算复核（80 个文件逐个重扫）**：T0=60 / T1=8 / T2=7 / T3=5 与上表**完全一致**，
合计 80 文件 / 14,614 行不变。

> ⚠️ **但总数一致 ≠ 归属正确**。这次复核之后仍然漏了**两处归属错误**（`DelayEx` 与 `SafeMemoryHandle` 刚好**互换**，
> 一进一出所以总数看不出来），是在**补单测、逐文件读源码**时才发现的 —— 见 §4.7。
> 下面三条里的第 2、3 条结论**都已被推翻**（原文保留，便于对照）。

| 复核项 | 结论 |
|---|---|
| ⚠️ **`grep '^using'` 会漏掉文件第一行** | 本仓 80 个文件里多数带 UTF-8 **BOM**，`^using` 匹配不上 `\uFEFFusing`。`Contracts/ImageInfo.cs` 的第一行正是 `using Mapster;` —— 用 `^using` 扫会把它误判成 **T0**。**本表的解析必须用不带 `^` 锚定的 `using ` 匹配**（或用 BOM 容忍的正则）。以后改守卫测试的匹配器时注意同一点。 |
| ⛔ **完全限定名引用**不靠 `using`（**此处当时下错了结论**） | 7 个文件在正文里用 `System.Windows.*` / `HalconDotNet` / `Prism` / `Mapster` / `MiniExcel` 等（有的只在 XML 注释里提一下）。原文写的结论是"**逐个核实后归级未变**" —— **这是错的**：其中 `Core/Platform/DelayEx.cs` 正是靠完全限定名把 WPF 依赖藏起来，它的归级应当从 T0 改成 **T1**。原文那句"基于 `using` 的规则对这类文件是**侥幸正确**"说对了，但"侥幸正确"被当成了"正确"。**已修正，见 §4.7。** |
| ⛔ **级别与目录名确实正交**（实测反例，**例子已更正**） | `Ui/Theme/ThemeSettings.cs`（14 行，纯 `record`，零 `using`）是 **T0 文件住在 T2 目录里** —— 正好印证 §0 的说法：级别是准入线，目录名是职责归位，两者不是一回事。原文还举了 `Core/Platform/DelayEx.cs`（"42 行，只用 `Stopwatch`"）作第二个反例，**那个例子是错的** —— 它真依赖 WPF（T1），已移到 `Ui/Controls/`。而且它与上一行**自相矛盾**：上一行说"正文里有 `System.Windows.*`"，这一行却说"只用 `Stopwatch`"。 |

---

## 2. 逐文件归级表（路径 = **重排后**的现状）

### T0 · 零依赖（60 文件 / 13,044 行）— 准入：只能 `using System.*`

```
Contracts/            DeviceStatus 45  KeyValue 36                      (2 文件, 81 行)
Core/Collections/     ConcurrentList 323  ListEx 32  RangeObservableCollection 110
                                                                        (3 文件, 465 行)
Core/Compare/         BitOperationEx 356                                (1 文件, 356 行)
Core/Concurrency/     ChannelHelper 205  EventHelper 268  TaskEx 276  ValueTaskSourceHelper 158
                                                                        (4 文件, 907 行)
Core/Conversion/      ASCIIEx 127  BinaryEx 75  BoolEx 82  ByteEx 382  DateTimeEx 96
                      DecimalEx 147  HexEx 62  JsonEx 415  ReadOnlySpanEx 59
                      RegexEX 136  RegisterEx 318  StringEx 38          (12 文件, 1,937 行)
Core/Data/            BufferEx 115  ZIPEX 32                            (2 文件, 147 行)
Core/Enums/           EnumEx 90                                         (1 文件, 90 行)
Core/Files/           CSVEx 355  DirectoryEx 154  FileEx 55  ScheduledDeletion 341
                                                                        (4 文件, 905 行)
Core/Files/ScheduledDeletionEx/  ScheduledDeletionEx 226                (1 文件, 226 行)
Core/Notify/          NotifyPropertyChanged 14  ← ⚠️ 全仓 0 引用        (1 文件, 14 行)
Core/Platform/        CpuMonitorEx 231  DynamicDllLoaderEx 120
                      FpsEx 131  ProcessEx 29  SafeMemoryHandle 46       (5 文件, 557 行)
Core/Reflection/      ReflectionEx 275                                  (1 文件, 275 行)
Distribution/Protection/  AntiDebugEx 107  AntiTamperEx 90  AntiToolProcessEx 86
                      AppProtectionEx 122                               (4 文件, 405 行)
Distribution/Security/    AesEx 175  HashEx 104  LicenseInfo 66  LicenseManagerEx 173
                      MachineCodeEx 105  RsaEx 120                      (6 文件, 743 行)
Patterns/*/            Factory 806  Mediator 1,366  Observer 756  ProducerConsumer 1,387
                      PublishSubscribe 1,296  Singleton 224            (11 文件, 5,835 行)
Ui/Controls/          DataGridViewEx 87  ← ⚠️ 见 §5.8：73/87 行是注释 + 10 行空行
Ui/Theme/             ThemeSettings 14                                  (1 文件, 14 行)
```

> `Patterns/*` 11 个文件 / 5,835 行 **全部零依赖** —— 这是 T0 里最大的一块，
> 也说明"设计模式示例"和"通用工具"的依赖特征完全相同，差别只在**归属**（见 §4.5）。

### T1 · 需 UI/宿主（8 文件 / 586 行）— 准入：允许 WPF / Win32

| 文件 | 行 | 依赖 |
|---|---|---|
| `Ui/Controls/DelayEx.cs` | 42 | `System.Windows.Threading`（**写在正文里**，见 §4.7） |
| `Ui/Controls/DispatcherEx.cs` | 112 | `System.Windows.Threading` |
| `Ui/Controls/DispatchertimerEx.cs` | 108 | `System.Windows.Threading` |
| `Ui/Controls/FileDialogEx.cs` | 32 | `Microsoft.Win32` |
| `Ui/Controls/FindVisualChildEx.cs` | 32 | `System.Windows(.Media)` |
| `Ui/Controls/LangSwitchEx.cs` | 118 | `System.Windows` |
| `Ui/Controls/PasswordBoxEx.cs` | 92 | `System.Windows(.Controls)` |
| `Ui/Controls/ScrollBarEx.cs` | 50 | `System.Windows(.Controls)` |

> **T1 里 7/8 都在 `Ui/Controls/`**，唯一例外曾长期是 `Core/Platform/SafeMemoryHandle.cs` ——
> 那是误判（它属 T0），2026-09-18 已修正，见 §4.7。

### T2 · 需第三方包（7 文件 / 550 行）

| 文件 | 行 | 依赖 |
|---|---|---|
| `Ui/Theme/ThemeService.cs` | 257 | MaterialDesign + WPF |
| `Runtime/Composition/RegisterNavigationViewEx.cs` | 89 | Prism + WPF |
| `Runtime/Composition/RegisterServicesEx.cs` | 66 | Prism |
| `Runtime/Mapping/MapsterHelper.cs` | 49 | Mapster |
| `Contracts/ImageInfo.cs` | 40 | Mapster |
| `Runtime/Spreadsheets/ExcelEx.cs` | 37 | MiniExcel |
| `Core/Data/CloneEx.cs` | 12 | FastDeepCloner |

### T3 · 需 vendor SDK（5 文件 / 434 行）

| 文件 | 行 | 依赖 | 备注 |
|---|---|---|---|
| `Vision/HImageEx.cs` | 201 | Halcon + OpenCV | 唯一 OpenCV 使用点之一（`ToMat`） |
| `Vision/BitmapEx.cs` | 88 | Halcon + `System.Drawing` + WPF | `ToImageSource` 下游 2 处 |
| `Vision/RawEx.cs` | 76 | Halcon + `System.Drawing` | `ToHImage` 下游 4 处 |
| `Vision/IntPtrEx.cs` | 54 | Halcon | `ToHImageByte/RGB` 被 `BitmapEx.ToHImage` 调用 |
| `Vision/MatEx.cs` | 15 | Halcon + OpenCV | **空类**（仅 using） |

> **`Extension.Vision.Cognex`** 也属 T3，它是 09-15 阶段 2A 的既定成果（已不在 `Extension.dll` 内）。
> 按本方案的约束，**不再新增同类卫星工程**（见 §3.2 / §7）。

> ⛔ **2026-09-18 机械复核（序 12h）**：本表 **80 / 80 个文件的层级与实测一致**（`声明层级 ≠ 实测层级` = 0）。
> 复核方式：`.workbuddy-ai/tools/audit_per_file_tier.py` —— **逐字复用守卫 `TierGuardTests` 的同一套规则**
> （`GuardedPrefixes` + `BclExemptions` + `ReferenceRegex` + `PREFIX_TIER`）重新推算每个文件的实际层级，
> 再与本表逐行比对，**不靠人眼**。
> ⚠️ 三点口径提醒：
> ① 脚本必须**排除 `obj/` / `bin/` 下的生成文件**，否则会数出 **92** 个而不是 **80**（`obj/Debug/…/*.AssemblyInfo.cs` 之类）；
> ② 唯一"有受监管引用却仍是 T0"的是 `Core/Platform/SafeMemoryHandle.cs`（走 `Microsoft.Win32.SafeHandles` 的 BCL 例外）；
> ③ 行数口径是 **`splitlines`**，**不是 `wc -l`** —— 文件末行无换行时 `wc -l` 系统性少 1（本表与 §5.30 曾因此对不上，已统一修正）。

---

## 3. 目录结构（**单程序集内**，不新建 csproj）

**分级是"准入线"，目录是"归位"。** 目录名不带 `T0/T1` 前缀（否则命名空间会变成 `Extension.T0.Primitives`，得不偿失）；
级别靠 **目录分组 + 准入线 + 守卫测试** 强制。

### 3.1 目录树（✅ 已实施）

```
Extension/Extension.csproj          ← 仍然只有一个 csproj，一个 Extension.dll
├── Core/                           【T0 准入：只允许 using System.*】
│   ├── Collections/                ← Tool/{ConcurrentList,RangeObservableCollection} + ListEx
│   ├── Compare/                    ← CompareEx/BitOperationEx
│   ├── Concurrency/                ← TaskEx/ + Helper/Channel + Helper/TaskHelper
│   ├── Conversion/                 ← ConvertEx/*（RegisterEx 移出 → Vision，见 §5.1）
│   ├── Data/                       ← DataEx/{BufferEx,ZIPEX,CloneEx}
│   ├── Enums/                      ← EnumEx/EnumEx
│   ├── Files/                      ← FileEx/{CSVEx,DirectoryEx,FileEx} + SystemEx/ScheduledDeletion
│   │   └── ScheduledDeletionEx/    ← 与上一行同名的类型，隔离在子命名空间（见 §6.H）
│   ├── Notify/                     ← Helper/Notify
│   ├── Platform/                   ← SystemEx/{Process,CpuMonitor,Fps} + IntPtrEx/{DynamicDllLoaderEx,SafeMemoryHandle}
│   │                                 （DelayEx 曾在此，实为 T1，已移入 Ui/Controls，见 §4.7）
│   └── Reflection/                 ← ReflectionEx/ReflectionEx
├── Ui/                             【T1 准入：允许 WPF / MaterialDesign / Win32】
│   ├── Controls/                   ← ControlEx/* + SystemEx/{LangSwitchEx,DelayEx}
│   └── Theme/                      ← ThemeEx/{ThemeSettings,ThemeService}（ThemeSettings 为 T0、ThemeService 为 T2）
├── Runtime/                        【T2 准入：允许 Prism / Mapster / MiniExcel / FastDeepCloner】
│   ├── Composition/                ← ReflectionEx/{RegisterServicesEx,RegisterNavigationViewEx}
│   ├── Mapping/                    ← Helper/Mapping
│   └── Spreadsheets/               ← FileEx/ExcelEx
├── Vision/                         【T3 准入：允许 Halcon / OpenCV】
│   └── ← ImageEx/{BitmapEx,RawEx,HImageEx,IntPtrEx,MatEx}
├── Contracts/                      ← Struct/*（跨模块契约，业务语义，见 §4.3）
├── Patterns/                       ← DesignPattern/*（教学 + 实现混装，见 §5.6）
│   └── <每个模式>/Advanced/        ← Advanced* 文件（统一 6 个模式的命名空间形状，见 §6.J）
└── Distribution/                   ← SecurityEx/* + ProtectionEx/*（发布形态，见 §4.5）
```

**目录 ↔ 命名空间现在是 1:1**（`Ui/Controls/` ⇒ `Extension.Ui.Controls`），
唯一的例外是 `Core/Files/ScheduledDeletionEx/`（**故意**多一级，用于隔离同名类型，见 §6.H）。

**准入规则（写进 CONTRIBUTING 的那句话）**：一个类型要进 `Core/`，必须同时满足
① 不 `using` 任何非 `System.*` 命名空间；② 不出现"设备/日志/订单/图像"这类领域名词；③ 不读 `App.xaml` / 不假设有 `Config/` 目录。

### 3.2 为什么**不**拆程序集（实测数据，不是偏好）

| 拆程序集的三个正当理由 | 本仓是否成立 |
|---|---|
| 要对外发 NuGet 包给别人用 | ❌ 同一 `Frame.sln` 内的内部库 |
| 要部署到没装 vendor 的机器，且不允许带 dll | ❌ 下游本就在用 Halcon（7 个工程） |
| 需要独立版本号 / 独立发布节奏 | ❌ 全仓同版本一起编 |

**成本对照**（拆 vs 不拆，两者效果完全相同）：

| | 不拆（本方案） | 拆 `Extension.Vision.Halcon` |
|---|---|---|
| 新增工程 | **0** | +1 csproj（+ `Frame.sln`/`Extension.sln` 双份配置条目） |
| 下游改动 | **0** | 5 个文件补 `ProjectReference` |
| 命名空间 | 不变 | 5 个文件改名 + 下游 `using` 跟着改 |
| 隔离效果 | 靠准入线 + 守卫测试 | 靠编译器 |

⇒ **多出来的成本换不到任何"不拆拿不到"的收益**，只有"将来真要用 Halcon 时报错时机从编译期变成运行期"这一个差别 ——
而下游本来就在用 Halcon，这个差别不成立。

> ⚠️ **"有个 NuGet 包就新建一个工程"是本仓最贵的错解**：`Extension.csproj` 挂着 9 个包
> （FastDeepCloner / Mapster / MaterialDesignThemes / MiniExcel / Halcon ×2 / OpenCvSharp4 / Prism / System.Drawing），
> 按包拆会得到 **9 个 csproj**，而其中 6 个包里全仓只有 1–2 个文件在用
> （FastDeepCloner 1 个 / MiniExcel 1 个 / Mapster 2 个 / MaterialDesign 1 个 / Prism 2 个）。
> **依赖治理的单位是"文件"，不是"包"。**

### 3.3 单程序集里怎么**强制**准入线（不新建工程）

**(a) 分级守卫测试 —— 已实现**（`Extension/Extension.Tests/TierGuardTests.cs`，**不新建工程**）

用 `[Fact]` 遍历源码树，逐文件断言所在目录的准入线。源码路径用 `[CallerFilePath]` 拿 ——
**编译期就固化成绝对路径，不靠 `AppContext.BaseDirectory` 上跳**（本仓曾因上跳层数错位坑过 7 个测试）：

```csharp
private static string SourceRoot([CallerFilePath] string thisFile = "")
    => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "Extension"));
```

十一条断言，分四组：

| 组 | 测试 | 作用 |
|---|---|---|
| 防假绿 | `守卫本身是有效的_不能因为扫到0个文件而假绿` | 扫到的文件数 < 80 就直接失败（路径解析错时不会静默通过） |
| 防假绿 | `匹配器不是死的_清空允许清单后必须报出违规` | **清空允许清单后必须报出 ≥10 处违规** —— 见下方"曾踩过的坑" |
| 防假绿 | `扫描器的前缀表必须与受监管前缀表一致` | `ReferenceRegex` 的前缀是**手写**的，与 `GuardedPrefixes` 走散时立刻失败（少一个前缀 = 该依赖永不检查） |
| 防盲区 | `完全限定名写法也必须被扫到_否则守卫有盲区` | 扫描器必须能从**正文**认出完全限定名；注释 / XML doc 不算依赖 |
| 防盲区 | `真实代码里确实存在完全限定名引用_这个盲区不是纸上假设` | 钉住 `Ui/Controls/DelayEx.cs` 的宿主依赖**只在正文里**、它的 `using` 块里没有 `System.Windows` |
| 准入线 | `目录不得越级引用_准入线` | 主规则：目录未登记的第三方/vendor 命名空间 → 列出**全部**违规并失败 |
| 准入线 | `允许清单本身必须是良构的_目录存在且登记项都真的会被检查` | 防止登记不存在的目录 / 永不会被检查的命名空间 |
| 依赖方向 | `低层目录不得引用高层目录_依赖方向` | T0 目录不得 `using Extension.Ui.*` / `.Runtime.*` / `.Vision.*`。⚠️ **两个限制（§6.L.3）**：① 只遍历行首 `using`、**不扫正文**（第三盲区在方向测试上**未修**）；② 只比**层级号** ⇒ **4 个 T0 目录之间任意方向的引用全是绿的** |
| 依赖方向 | `层级表必须覆盖全部顶层目录_不得有漏网的层` | 新增顶层目录若未登记层级，会失败（否则被当 T0 放过反向引用） |
| 目录契约 | `每个文件都必须有namespace_不得落在全局命名空间` | 全局命名空间会污染下游 |
| 目录契约 | `namespace必须与目录一致_目录即命名空间` | ⛔ 2026-09-18 补。§3 声称"目录 ↔ 命名空间 1:1"，但**此前没有任何用例守它** |

**⚠️ 四处必须做对**（都踩过）：

**(1) 两侧都必须用前缀匹配**：

- 受监管清单 `GuardedPrefixes` 若退回成 `ns.Split('.')[0]` 的**根段比较**，
  则 `System.Windows` / `System.Drawing` / `Microsoft.Win32` **永远匹配不上**
  （它们的根段是 `System` / `Microsoft`）⇒ 检查**静默失效**。
  这正是 `匹配器不是死的_清空允许清单后必须报出违规` 存在的原因。
- 允许清单 `Allowed` 也必须是前缀语义：登记 `System.Windows` 就该放行
  `System.Windows.Controls` / `.Media` / `.Interop` / `.Threading`，
  否则每加一个 WPF 子命名空间都要改清单，守卫会退化成噪声源。

**(2) ⛔ 扫描范围不能只看 `using` 行（第三盲区，2026-09-18 修）**：

守卫最初用 `^\s*using\s+([\w\.]+)\s*;` 扫依赖。这个匹配器有三个洞，前两个早已修掉，第三个一直漏着：

| # | 洞 | 症状 | 处置 |
|---|---|---|---|
| 1 | `^\s*using` 锚定行首 | 本仓多数文件带 UTF-8 **BOM**，`^\s*using` 匹配不上 `\uFEFFusing` ⇒ 首行 using 全漏 | 不锚定行首（`ExtractReferences` 逐行扫、不要求行首） |
| 2 | 根段比较 | 见 (1) | 前缀匹配 |
| 3 | **完全限定名** | `System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(...)` 写在**正文**里，`using` 块里根本没有 ⇒ 扫描器完全看不见 | 扫描器改成"**文本扫描**"：`ExtractReferences` 用 `ReferenceRegex` 扫每行的**所有**受监管前缀出现，`using` 行与正文一视同仁 |

第 3 个洞的代价是实打实的：`Core/Platform/DelayEx.cs` 是一个真依赖 WPF 的 **T1 文件**，
却因为把依赖写成了完全限定名，**长期住在 T0 目录里而守卫是绿的**。
⇒ 所以新增的两条"防盲区"用例一条守机制（能认出完全限定名）、一条守事实（真实代码里确实有这种写法）。

排除注释行（`^\s*//`，含 XML doc）是**必要**的 —— 否则文档里写一句 `<c>System.Drawing</c>` 就会被当成依赖。
已知残留局限：行尾注释 `code; // System.Windows.Foo` 与字符串字面量仍会被扫到，**本仓实测 0 处**。

**(3) 名字像宿主依赖、其实是 BCL 的命名空间要单列例外**（`BclExemptions`）：

`Microsoft.Win32.SafeHandles`（`SafeHandleZeroOrMinusOneIsInvalid` 等）定义在 `System.Runtime.dll`，
是 **BCL**；只有 `Microsoft.Win32.OpenFileDialog` / `SaveFileDialog` 那一族才需要宿主程序集。
两者**前缀相同**，必须按完整命名空间区分 —— 否则只能靠"给目录登记一个其实不需要的依赖"来消音，
那会让准入线比真实依赖**松**。这正是 2026-09-18 之前 `Core/Platform` 的状态（见 §4.7）。

**(4) ⛔ "目录 = 命名空间"这条契约本身也要守**（2026-09-18 补）：

`§3` 声称"目录 ↔ 命名空间 1:1"，但**此前没有任何用例守它**。
代价：重排时漏改了 `Core/Platform/DelayEx.cs` 的 namespace（留在了旧的 `Extension.Core.SystemEx`），
而下游 `using` 也一起没改 ⇒ **两边一致 ⇒ 编译通过、守卫全绿，谁都没发现**，
直到把该文件移走时才在 `git diff` 里暴露。
⇒ 补上 `namespace必须与目录一致_目录即命名空间`：逐文件断言 `namespace == "Extension." + 相对目录.replace('/', '.')`。

> 教训：**"约定"如果没被断言，就等于不存在。** 这条比前三条更普遍 ——
> 前三条是"守卫的实现细节错了"，这一条是"守卫漏守了一条自己文档里写着的规则"。

**验证记录**：

| 阶段 | 结果 |
|---|---|
| 首次运行（重排前） | **精准命中唯一一处真实违规**：`Core/ConvertEx/RegisterEx.cs → using HalconDotNet;`（17 通过 / 1 失败） |
| 删掉那行多余 `using` 后 | **18/18 全绿**，`Extension.sln` **0 错误** |
| 重排 + 目录键更新后 | **21/21 全绿**（新增 2 条防假绿/方向用例，见上表） |
| 全量 `Frame.sln` | **0 错误**（对照基线一致） |
| **2026-09-18 升级扫描器后** | **11/11 全绿**（7 → 10 → 11 条）；同时修掉 §4.7 的两处分级误判，并补上"目录 = 命名空间"这条契约 |
| **2026-09-18 机械复核后（序 12i）** | **12/12 全绿**（11 → 12 条）：方向测试补扫正文，新增 `方向扫描器必须能看见只在正文里的完全限定名`（§6.L.3） |

⇒ 守卫**会红**（不是假绿），且当前代码库已满足全部准入线与依赖方向。

**(b) `BannedApiAnalyzers`**（可选，只是往 `Extension.csproj` 加一个 `<PackageReference>`，**不是新工程**）

`BannedSymbols.txt` 里按目录禁 API 做不到（分析器无目录粒度），所以它只适合**全库级**禁令
（例如"禁止在 `Core` 语义下用 `Process.Start`"）。**主用 (a)，(b) 只在需要"编译期就红"时才加。**

---

## 4. 原分级暴露的 6 个"不合理"（有实证）

| # | 现象 | 证据 | 为什么说明分级不合理 |
|---|---|---|---|
| 1 | **`Core/ConvertEx/RegisterEx.cs` 是 Halcon 文件** | 319 行、`using HalconDotNet;`，而 `Extension.Core.ConvertEx` 是全仓引用最多的命名空间（**57 处**） | 按"技术域"分类时，一个 Halcon 文件混进了"类型转换"。**它是全库唯一一个用错目录的文件** —— 恰好证明"没有准入线"。**已修**（§5.1） |
| 2 | **`Core/IntPtrEx/` 目录里没有 IntPtr 扩展** | 目录装的是 `DynamicDllLoaderEx.cs`(120) + `SafeMemoryHandle.cs`(46)；真正的 `IntPtrEx.cs` 在 `Core/ImageEx/` | 目录名 = 某文件名的残留。MS 准则明确禁止命名空间与类型同名 |
| 3 | **`Struct/` 装的是 enum + class + struct** | `DeviceStatus.cs`(2 个 enum) / `KeyValue.cs`(class) / `ImageInfo.cs`(2 个 struct) | 按"C# 语言构造"分类，一开始就不成立 |
| 4 | **`Helper/` 与 `Tool/` 无边界** | `ConcurrentList` 在 `Tool`、`ChannelHelper` 在 `Helper`；两者都做并发原语 | 靠感觉归位 |
| 5 | **`Core/` 里混着 WPF / vendor / 发布形态** | `ControlEx`(WPF) / `ImageEx`(Halcon) / `ProtectionEx`(反调试) 都在名为"Core"的层里 | "Core" 变成了 `Shared`/`Common` 式黑洞 |
| 6 | **命名空间与目录不一致（3 处）** | `Core/ReflectionEx/*` 的命名空间是 `Extension.Core.Reflection`；`DesignPattern/*/Advanced*.cs` 的命名空间多一级 `.Advanced` | 目录不能作为分类依据 ⇒ IDE0130 永久报警 |

> **重排后 1–6 全部消除**：目录 ↔ 命名空间 1:1（唯一例外见 §6.H），
> `Core/` 只剩纯 BCL，WPF 进 `Ui/`，vendor 进 `Vision/`，发布形态进 `Distribution/`。

### 4.7 ⛔ 重排**之后**才发现的 2 处分级误判（2026-09-18 修 —— 两处刚好**互换**）

补 T0 单测时逐文件读源码，发现 §1 / §2 里有**两处归属搞反了**：

| 文件 | 原判 | 实际 | 为什么判错 |
|---|---|---|---|
| `Core/Platform/DelayEx.cs` | **T0**（"42 行，只用 `Stopwatch`"） | **T1** | 它的 `using` 块里**确实**只有 `System.*`，但方法体里写的是 `System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(...)` —— **完全限定名**，只看 `using` 的解析器**看不见** |
| `Core/Platform/SafeMemoryHandle.cs` | **T1**（"依赖 `Microsoft.Win32`"） | **T0** | `Microsoft.Win32.SafeHandles`（`SafeHandleZeroOrMinusOneIsInvalid` 等）定义在 `System.Runtime.dll`，是 **BCL**；只有 `Microsoft.Win32.OpenFileDialog` 那一族才需要宿主。**前缀相同，名字骗人** |

**为什么这件事值得单列**：两处误判刚好**一进一出** ⇒ `T0 = 60` / `T1 = 8` 的**总数完全不变**。
§1 的复核表（逐文件重扫 `using`）也照样"对得上"。**总数正确完全掩盖了归属错误** ——
如果只做加总核对，这个错会一直躺着。

**处置（2026-09-18 已执行）**：

1. `git mv Core/Platform/DelayEx.cs → Ui/Controls/DelayEx.cs`，`namespace Extension.Core.Platform` → `Extension.Ui.Controls`。
   它和 `Ui/Controls/DispatcherEx.cs` / `DispatchertimerEx.cs` 本来就是同一族（"在 UI 线程上让出时间片"）。
2. 下游**只 1 处**需要改：`WPFControl/.../Services/Shell/DisplayService.cs` 的 `using Extension.Core.Platform;` → `using Extension.Ui.Controls;`
   （全仓 `DelayEx` 的实调用只有这 2 行；`Device/…/GigeCameraBase.cs` 等 8 个 `using Extension.Core.Platform;` 用的是留在原地的 `FpsEx`/`ProcessEx`/`CpuMonitorEx`）。
3. `Core/Platform/` 现在是**纯 T0 目录**（4 文件 / 511 行）⇒ 删掉 `TierGuardTests.Allowed` 里 `["Core/Platform"] = { "Microsoft.Win32" }` 这条登记。
   原来那条登记是**为了让守卫变绿而给目录补的假依赖** —— 比真实依赖松，是分级治理里最该避免的一种"消音"。
4. `Microsoft.Win32.SafeHandles` 进 `BclExemptions`（见 §3.3 第 (3) 点）。
5. 守卫扫描器升级为**文本扫描**（§3.3 第 (2) 点），并把这两条事实钉成用例。
6. **顺带揪出重排的一处漏改**：`DelayEx.cs` 的 namespace 当时还是旧的 `Extension.Core.SystemEx`
   （重排 92 处 `namespace` 改写里漏了它），而下游 `DisplayService.cs` 的 `using` 也一起没改
   ⇒ **两边一致 ⇒ 编译通过、守卫全绿**。已改成 `Extension.Ui.Controls`。
   ⇒ 因此新增守卫用例 `namespace必须与目录一致_目录即命名空间`（§3.3 第 (4) 点）：
   **"目录 = 命名空间"这条约定此前只写在文档里，没有任何用例守它。**

> **教训**：「逐文件实测」必须测到**正文**，不能只测 `using` 块。
> `using` 只是"最常见的依赖写法"，不是"依赖的全集" —— 完全限定名、命名空间别名、`global::` 都是合法写法。
> 这也是本方案把分级守卫从"扫 `using`"改成"扫文本"的根本原因。

---

## 5. 代码优化点（本次实测）

> 原则：**不删代码**。下列全部是"改 1 行 / 加 1 个约束 / 补注解"级别的动作。

### 5.1 `Core/ConvertEx/RegisterEx.cs:1` —— 多余的 `using HalconDotNet;` ✅ **已处理**
该文件 18 个 `public` 方法**没有一个用到 Halcon 类型**（去掉首行后 `HObject|HTuple|HOperatorSet|HImage|HRegion` 零命中）。
删掉这一行，`Extension.Core.Conversion`（57 处引用）就变成**纯 BCL 命名空间**，同时把该文件从 T3 归回 T0。
（`using` 本身不产生程序集级依赖，所以这是**信号层**的收益，不是体积收益。）

> **2026-09-18 已执行**：删掉该行（字节级保留 BOM + CRLF），`Extension.sln` 重新构建 **0 错误**，
> 守卫测试从 17/18 变为 **18/18**。验证方式：删完能编过 ⇒ 证明它不是靠 `using` 引入的扩展方法。
> 该文件现为 **318 行**（全库总行数因此 14,615 → 14,614），现路径 `Core/Conversion/RegisterEx.cs`。

### 5.2 `Core/Concurrency/EventHelper.cs:145,182` —— `throw ex;`（CA2200 ×2） ✅ **已修（序 3a，2026-09-18）**
```csharp
catch (Exception ex) { /* Logger.LogError(ex, ...); */ throw ex; }
```
① `throw ex;` **重置栈**，原始异常位置丢失 ⇒ 必须改 `throw;`；
② 这个 `try/catch` 只做"原样重抛"，**等于白包一层**，且注释掉的 `Logger` 说明本意是记日志 —— 要么补日志，要么去掉整层。

**✅ 修后（序 3a）**
- 两处 `throw ex;` → `throw;`（现 148 / 188 行），保留处理器自身的栈帧。**异常类型与消息完全不变** ⇒ 下游 `catch` 行为零变化。
- ⚠️ **踩到的连带坑**：改成 `throw;` 后 `catch (Exception ex)` 的 `ex` 变成"声明了但未使用" ⇒ 会**新引入 CS0168 ×2**。
  故同步去掉参数名（`catch (Exception)`），并在原注释处写明"要记录 `ex` 就把参数写回来"。**净警告变化 = 0**（见 §7 实测）。
- ② 的"白包一层"**未动** —— 它属"要么补日志、要么去掉整层"的设计取舍，需单独授权（硬规则：授权逐项生效）。
- 用例：`EventHelperTests` 两条断言从"栈被重置"**反转**为"栈保留处理器帧"（断言 `ex.StackTrace` 含命名方法 `ThrowingHandler`）。
  ⚠️ `ThrowingHandler` 必须是**命名方法**，否则编译器生成的 lambda 名不会出现在栈里，断言会假红。

### 5.3 `Core/Compare/BitOperationEx.cs:213,221,222,308` —— 泛型指针缺 `unmanaged` 约束（CS8500 ×6） ✅ **已修（序 3b，2026-09-18）**
```csharp
unsafe static void ArrayCompare<T>(..., T* pValue1, T* pValue2, ...)   // 213 行
```
`T*` 对非 unmanaged 的 `T` 是"取托管类型地址"。应加 `where T : unmanaged`（调用方全部传 `byte`/`short`/`int`，不会破坏现有调用）。

**✅ 修后（序 3b）**
- `ArrayCompare<T>`（现 **221** 行）与 `Handle<T>`（现 **318** 行）**同时**加 `where T : unmanaged`。
- ⛔ **只加一个不行**：`ArrayCompare` 内部 4 处 `T*` 直接使用 + `Handle` 的 2 处，共 6 条 CS8500；
  只给 `ArrayCompare` 加约束，`Handle` 那 2 条**仍在**（它的形参同样是 `T*`）。
- 约束只**收紧**调用面：4 个调用点实参为 `byte*` / `ushort*` / `short*` / `bool*`（`fixed` 语句实测），
  加上 `ArrayCompare` 内部的递归调用（`T` 继承自身约束），全部满足 ⇒ **运行时行为零变化**。
- 实测 **CS8500 6 → 0**。
- 新增源码护栏用例 `BitOperationEx_泛型指针方法必须带unmanaged约束`：钉住"**恰好 2 处代码行**带 `where T : unmanaged`"
  （只数非 `///` 行 —— 说明注释里也会出现这个字面量，不排除会把断言喂饱）。

### 5.4 可变 `public struct` 反模式（3 处 / 7 个属性）
`BitOperationEx.CompareValue`（3 个 `{get;set;}`）、`Contracts/ImageInfo.cs` 的 `ImageInfo` / `ImageInfoDTO`（各 2 个）。
值类型 + 可变属性 = 改副本不生效 / 装箱 / 相等性陷阱。建议 `readonly struct` 或 `record`（**注意序列化兼容**，见 §7 风险）。

### 5.5 ⛔ `Contracts/` 与 `Runtime/Mapping/` 的耦合（2026-09-18 机械复核后修正表述）
原写「`Contracts/` ↔ `Runtime/Mapping/` **双向耦合仍在**」—— **依赖图上不是双向的**，实测：

- `Runtime/Mapping/MapsterHelper.cs` → `using Extension.Contracts`（**库内**，单向，T2 → T0）
- `Contracts/ImageInfo.cs` → `using Mapster`（**外部包**，**不是** `Extension.Runtime`）

⇒ 库内依赖**没有环**（这正是 §6.L 矩阵里 `Contracts` 一整行为空的原因）。

但**设计上的耦合确实是双向的**：`Contracts/ImageInfo.cs` 里塞了 `ImageInfoMapping.Configure`
（DTO 文件依赖 Mapster），而 `MapsterHelper` 又要靠 `static bool _isInitialized` 手动开关 +
`InitializeAllMappings()` 里**硬编码逐个登记** ⇒ **新增 DTO 忘了登记就静默不映射**。

⚠️ 顺带一个口径提醒：`Contracts/` 在守卫 `TierOf` 里是 **T0 目录**，但它包含一个 **T2 文件**
（`ImageInfo.cs`，见 §2 的 T2 表）⇒ **目录层级 ≠ 目录内每个文件的层级**，两个口径都要看。

建议把 `ImageInfoMapping` 从 DTO 文件里拆出，改程序集扫描或显式 Profile。

### 5.6 `Patterns/` 有 7 个重复类型名
| 类型 | 出现位置 |
|---|---|
| `MessagePriority` / `PriorityMessage` / `IMessageFilter` / `PriorityFilter` / `CompositeFilter` / `MessageRouter` | `MediatorPattern.Advanced` **和** `PublishSubscribePattern.Advanced` 各一份 |
| `AdvancedMessageBroker<T>` | `PublishSubscribePattern` **和** `PublishSubscribePattern.Advanced` 各一份 |

同一份"消息优先级/过滤器"代码被复制到两个模式里 —— 这是"按模式名分类"的必然产物（每个模式目录都要自成一套）。
> **实证**：samples 里必须写 `new Advanced.AdvancedMessageBroker<T>(...)` 才能选中 Advanced 那一份，
> 去掉 `Advanced.` 限定会直接 **CS0104 二义性**（见 §6.I）。**这两份都还活着。**
>
> **第六轮补测时的第二处实证**（`PatternsPublishSubscribeTests.cs`）：测试文件里同时 `using` 两个命名空间后，
> 裸写 `AdvancedMessageBroker<T>` 同样 **CS0104** ⇒ 必须用别名
> `using PSS = Extension.Patterns.PublishSubscribePattern;` + `using PSSA = …PublishSubscribePattern.Advanced;`。
> 两者的 `Type.Name` **完全相同**（都是 `AdvancedMessageBroker\`1`），只有 `Namespace`/`FullName` 不同；
> ⚠️ 而且**消息类型也不同** —— 基础版吃 `T`（实现 `IMessageBroker<T>`），Advanced 版吃 `PriorityMessage<T>`
> （实现 `IMessageBroker<PriorityMessage<T>>`）⇒ 把两者当成"同一接口的两个实现"会编译不过。
> 另有 **`MessagePriority` 枚举**也在两个 `*.Advanced` 命名空间各一份（`Low=0` / `Normal=1` / `High=2` / `Critical=3`）。
> 钉住它们的用例：`两个同名AdvancedMessageBroker只能靠命名空间区分`、`两个同名MessagePriority枚举都还在`。

### 5.7 `Patterns/ObserverPattern` 的类型与 BCL 同名
`IObserver`（非泛型）vs `System.IObserver<T>`；`IObservable` vs `System.IObservable<T>`；
`Observable<T>` vs `System.Observable`；`EventArgs<T>` vs `System.EventArgs`。
下游**确有 2 个文件**在 `using Extension.Patterns.ObserverPattern;`
（`Device/Device/Light/Definition/LightParam.cs`、`WPFControl/.../LightControlViewModel.cs`）——
一旦它们同时用到 `System.Observable` 就会歧义报错。建议加 `Ext` 前缀或改到独立命名空间。

### 5.8 `Ui/Controls/DataGridViewEx.cs` —— 87 行里 **73 行是注释 + 10 行空行**
全文有效代码只有 4 行：`using System.Reflection;` + `namespace Extension.Ui.Controls { }`。
（⚠️ **2026-09-18 复核**：原文写的是"87 行里 **84** 行是注释"，**这个数字复现不出来** ——
按 `splitlines` 口径实测是 **73 行注释 + 10 行空行**（非有效行合计 **83**，不是 84）。
凡写"多少行是注释"必须写明口径，否则又是一个"加总核对看不出来"的数字。）
注释头写着"DataGridView扩展类 **Winforms**"——一个 WinForms 控件扩展放在 `UseWPF=true` 的库里。
（本次我扫出的"空 `catch`"就是它的**注释**里的 `// catch { }` —— 是**假阳性**，全库无真空 `catch`。）
> 归级结果：它**真的是 T0**（无任何第三方 using），所以它留在 `Ui/Controls/` 里并不违反准入线 ——
> 这也说明准入线只管"依赖"，不管"这文件还有没有用"。

### 5.9 可空性注解"声明了但没兑现"
`Extension.csproj` 有 `<Nullable>enable</Nullable>`，实测 **126 条可空性警告**（占全部 133 条的 95%）：
`Core/Conversion/JsonEx.cs` 52 条（26×CS8603 + 16×CS8604 + 4×CS8601 + 4×CS8602 + 2×CS8600）、`ReflectionEx.cs` 20、`DirectoryEx.cs` 16、`CSVEx.cs` 14。
⇒ 要么补注解（T0 层是纯函数，成本可控），要么先关掉这个开关 —— **别留着当噪音**（会淹没真正的警告）。

### 5.10 `Extension.Tests` 测试面（2026-09-18：21 → **921** 全绿，T0 层 60/60 全覆盖）

`Extension` 被 **21 个 csproj** 直接引用（全仓引用最多的库），而 `Extension.Tests` 原先只有 14 个业务用例。
T0 层 60 个文件几乎全是可测纯函数（`Core/Conversion` 12 文件 / 1,937 行、`Core/Concurrency` 4 文件 / 905 行、
`Enums`、`Compare`、`Files`）—— **这是全仓最大的一块零成本测试缺口**（无需 IO / 无需 vendor / 无需 UI）。

已补齐（**纯增量，未改任何产品代码**）：

| 文件 | 用例 | 覆盖 |
|---|---|---|
| `PrimitivesTests.cs` | 14 | 既有 |
| `TierGuardTests.cs` | **12** | 分级守卫 / 依赖方向（§3.3）；2026-09-18 升级扫描器后 **7 → 11**（+1 前缀表一致性、+2 防盲区、+1 目录=命名空间契约），机械复核后再 **11 → 12**（+1 方向扫描器扫正文，§6.L.3） |
| `ConversionTests.cs` | **72** | `Core/Conversion` 全部纯函数类（`HexEx`/`BinaryEx`/`ASCIIEx`/`BoolEx`/`DecimalEx`/`ReadOnlySpanEx`/`DateTimeEx`/`RegexEX`/`StringEx`）+ API 面反射用例 |
| `RegisterExTests.cs` | **53** | `RegisterEx` 16 个方法：`bool[]`/二进制串/`int[]`/`float[]`/`double[]`/`string[]` 的打包与拆包、三档长度校验；**并把 4 处语义陷阱固化成用例**（§5.16） |
| `JsonExTests.cs` | **48** | `JsonEx` 14 个方法：`DefaultOptions` 三个副作用、`ToJson(Type)` 重载选择、`Read` vs `TryRead` 契约、`ToList` 扁平化、`Replace` 父节点冲突；含临时文件 IO 用例 |
| `CSVExTests.cs` | **50** | `CSVEx` 3 个方法：导出（分批/转义/BOM/异常）、读取（表头匹配/枚举/可空/List）、**9 处行为固化成用例**（§5.25）。⚠️ 该类有 **7 处下游调用点** |
| `DistributionSecurityTests.cs` | **49** | `Distribution/Security` 6 文件：`HashEx`（RFC 4231 向量）/ `AesEx`（CBC+GCM+文件）/ `RsaEx` / `LicenseInfo` / `LicenseManagerEx` / `MachineCodeEx`；**22 处行为**（§5.29.1；⛔ 含"签名合法也抛 NRE" —— **该条已按序 32 修复**）。⚠️ 本行是 **2026-09-18 补的**：原表格漏了它，明细合计只有 868 而声明 914（差正好 49） |
| `ByteExTests.cs` | **39** | 字节序底座：大小端往返、`ToHexBools`、字符串对齐；**并把 3 处缺陷固化成用例**（§5.13；其中 `float` 的 `ToBytes` 与 `ToASCII` **均已修**，两条用例均为**改写** ⇒ 计数不变；第 3 处 `ToHexBools` 经第五轮定案**无需改**） |
| `TaskExTests.cs` | **29** | `TaskEx` 7 个方法：超时/取消区分、`WithCancellation` 的"已完成任务也抛"陷阱、`TimesRun`/`LongTimeRun`/`ThreadRun`；**并把 4 处行为固化成用例**（§5.19） |
| `DirectoryExTests.cs` | **29** | `Core/Files/DirectoryEx` 9 个扩展方法：目录创建、过滤匹配、年月日路径分段、按子目录分组、目录复制；**并把 6 处行为固化成用例**（§5.21） |
| `CollectionsTests.cs` | **28** | `ListEx` / `ConcurrentList<T>` / `RangeObservableCollection<T>`（含多线程、快照枚举、Dispose 后语义） |
| `EventHelperTests.cs` | **24** | `EventHelper` + `CEventArgs`：多播顺序、注册/取消/清空、`WaitForEventAsync` 三条路径；**并把 2 处缺陷固化成用例**（§5.18） |
| `BufferExTests.cs` | **22** | `Core/Data/BufferEx.SlidingBuffer`：追加/消费/平移压缩/自动扩容四者的索引算术交叉（含 50 轮随机序压测） |
| `CorePlatformTests.cs` | **23** | `Core/Platform`（现为**纯 T0**）：`FpsEx` 间隔与计时、`SafeMemoryHandle` 非托管内存与释放、`DynamicDllLoaderEx` 加载/取函数/释放、`CpuMonitorEx` 降级与释放、`ProcessEx`（⚠️ 进程名只喂**不存在的**；**序 27 修复后 `null`/空串可以安全真喂**——入口先抛）；**并把 6 处行为固化成用例**（§5.28） |
| `ReflectionExTests.cs` | **21** | `ReflectionEx` 6 个公开方法 + `ConvertValue` 转换契约 + 静默行为 |
| `BitOperationExTests.cs` | **21** | `Core/Compare/BitOperationEx`：四种极性 + 四种速度模式；**并把 2 处缺陷固化成用例**（§5.15）。2026-09-18 修序 3b 后 **20 → 21**（+1 源码护栏：泛型指针方法必须带 `where T : unmanaged`） |
| `ChannelHelperTests.cs` | **20** | `ChannelHelper<T>`：FIFO、四种满模式、`Clear`/`ClearAsync`、`Complete`/`Dispose` 后语义；**并把 3 处行为固化成用例**（§5.17） |
| `ValueTaskSourceHelperTests.cs` | **18** | `ValueTaskSourceHelper<TResult>`：正常/超时/取消三条路径、可复用性、Dispose 后不对称；**并把 1 处顺序陷阱固化成用例**（§5.17） |
| `ScheduledDeletionTests.cs` | **18** | `Core/Files/ScheduledDeletion`（**在用版**）：过期判定边界、日级目录、误判数字目录、空间不足分支；**并把 8 处行为固化成用例**（§5.26） |
| `DistributionProtectionTests.cs` | **18** | `Distribution/Protection` 4 个文件：`AntiDebugEx` / `AntiTamperEx` / `AntiToolProcessEx` / `AppProtectionEx`；**并把 10 处行为固化成用例**（§5.29）。⛔ 头号约束是**绝不触发威胁处置**（`Action` 默认 `Throw`、`Exit` 走 `Environment.Exit` 会直接杀掉 testhost）。2026-09-18 修序 36 / 37 后 **16 → 18**（序 36 重写 1 条 + 新增源码护栏 1 条；序 37 把 1 条拆成 2 条） |
| `ContractsTests.cs` | **15** | `Contracts`：两个枚举的取值固定、`KeyValue` 的引用语义与"未重写 `Equals`"、`ImageInfo` 的 struct 浅拷贝陷阱、`ImageInfoMapping` 双向映射；**并把 2 处行为固化成用例**（§5.28） |
| `EnumExTests.cs` | **15** | `Core/Enums/EnumEx`：描述回退、`Enum.Parse` 的数值分支 |
| `FileExTests.cs` | **14** | `Core/Files/FileEx`：读/写、`.bak` 单代轮换、`filePath` 空分支；**并把 4 处行为固化成用例**（§5.20） |
| `NotifyPropertyChangedTests.cs` | **13** | `Core/Notify/NotifyPropertyChanged`：事件契约、`protected virtual` 继承契约、多播短路；**并把 3 处行为固化成用例**（§5.24） |
| `ZipExTests.cs` | **10** | `Core/Data/ZIPEX`：gzip 往返（ASCII / 中文 / 空 / 64 KB）、BOM 与非法 UTF-8 的静默行为；**并把 2 处行为固化成用例**（§5.22） |
| `ScheduledDeletionExTests.cs` | **10** | `…ScheduledDeletionEx.ScheduledDeletionEx`（**零引用版**，`internal` ⇒ **全程反射**）：与在用版相反的失败策略；**并把 4 处行为固化成用例**（§5.27） |
| `ThemeSettingsTests.cs` | **7** | `Ui/Theme/ThemeSettings`（**T0 record 住在 T2 目录里**，§4.7）：默认值、值相等、`with`、sealed、属性类型全在 BCL |
| `CloneExTests.cs` | **7** | `Core/Data/CloneEx`（T2，`FastDeepCloner`）：引用隔离、`string` 作根对象抛异常、null 进出；**并把 2 处行为固化成用例**（§5.23） |
| `PatternsObserverMediatorTests.cs` | **78** | `ObserverPattern`×2(756 行) + `MediatorPattern`×2(1,366 行)；**并把 25 处行为固化成用例**（§5.30.2 / §5.30.3）。⛔ 含"`lock(observable)` 与 `lock(_lock)` 锁的不是同一对象"——**必须拆成两条用例**才成立 |
| `PatternsPublishSubscribeTests.cs` | **49** | `PublishSubscribePattern`×2(1,296 行)；**并把 16 处行为固化成用例**（§5.30.5）。⛔ 全篇用命名空间别名 `PSS.` / `PSSA.`（两个同名 `AdvancedMessageBroker<T>` ⇒ 裸名 = CS0104）。2026-09-18 修序 43 / 57 后 **48 → 49**（序 43 重写 1 条 + 新增 1 条钉住 `DelegateSubscriber<T>` 这个**平行分支**；序 57 重写 1 条） |
| `PatternsSingletonFactoryTests.cs` | **47** | `SingletonPattern`(224) + `FactoryPattern`(80) + `FactoryPattern/Advanced`(726)；**并把 15 处行为固化成用例 + 序 73 增补 1 处 = 16 处**（§5.30.1）。⚠️ 每个探测类型必须专用（静态缓存跨用例存活）。2026-09-18 修序 47 / 序 73 后**用例数不变（47）** —— 两处都是**改写既有用例**（把「断言缺陷存在」的反向护栏反转成正向护栏） |
| `PatternsProducerConsumerTests.cs` | **44** | `ProducerConsumerPattern`×2(1,387 行)；**并把 13 处行为固化成用例**（§5.30.4：11 处 + 序 72 的同型缺陷 2 处）。⛔ 所有跑循环的用例**必须带 `CancellationTokenSource(300)` 兜底**，否则 `Task.WhenAll` 永不返回。**序 41 / 序 72 修复后**：3 条"忙循环"用例重写为"立刻抛"，并各补 1 条 `Dispose` 后 `ObjectDisposedException` 用例 |
| `UiControlsPlaceholderTests.cs` | **4** | `Ui/Controls/DataGridViewEx.cs`（87 行 / 73 行注释 + 10 空行、零类型）；**并把 4 处事实固化成用例**（§5.30.6） |
| | **921** | |

> 新增的 19 个文件（18 个测试文件 + 1 个共享 helper `TempWorkspace.cs`）里，
> **IO 类测试**共用那个 helper（一次性临时目录，`IDisposable`）——
> `Core/Files`、`Core/Data`、`Distribution/Security` 要测的就是"文件系统副作用"本身，
> 换内存替身会把被测对象一起替掉。
>
> 测试过程中**发现 165 处此前无人记录的 API 陷阱 / 缺陷**（§5.11–§5.30 + 序 72 / 73）—— 全部"只在运行时暴露"，
> 编译器帮不上忙。按硬规则 1，**只记录 + 用测试钉住，不顺手改**。
> **已修的例外共 17 条**（均为用户 2026-09-18 **逐项**明确授权，明细见 §7 已完成表）：
> 序 27（`ProcessEx` 参数校验）、序 32（`LicenseManagerEx` 的 NRE）、序 41（`ProducerConsumer<T>` 忙循环）、
> 序 72（同型缺陷 2 个类）、序 3（`EventHelper` 的 `throw ex` + `BitOperationEx` 缺 `unmanaged`）、
> 序 36（`AntiTamperEx.VerifyFile` 缺 null 短路）、序 37（`AntiDebugEx` 阈值缺下限）、
> 序 47（`SingletonInstanceWithFactory<T>` 缺 `volatile`）、
> 序 28（`SafeMemoryHandle.GetPointer` 判据缺 `IsClosed`）、序 43（两处 `is IDisposable` 死分支）、
> 序 57（`FilteredSubscriber<T>.Name` 固定字面量）、序 64（`CreateProduct` 误导性异常）、
> 序 65（`Products.Collaborate` 对 null 零防御）、
> 序 60（4 处语义 XML 注释：`TopicFilter<T>` / `PriorityFilter<T>` / 非泛型 `PriorityFilter` / `TypeFilter`）、
> 序 8b（`ByteEx.ToBytes(this float)` + `ToASCII` —— 同项的 `ToHexBools` 经第五轮定案**无需改代码**）、
> 序 73（`SingletonInstanceAsync<T>` 的 `_instance` / `_initialized` / `_initializationFailed` 补 `volatile`）。
> ⚠️ **授权逐项生效、不累积** —— 序 41 的授权范围写的是"1 个文件 / 1 处"，**并不**覆盖同型的序 72；
> 序 72 是**单独**取得授权之后才动的手。
> ⛔ **五批打包授权，互不覆盖**：①「低风险加固包」= 序 **3 / 36 / 37 / 47**；
> ②「极低风险三条包」= 序 **28 / 43 / 57 / 64 / 65**；③「语义注释 + 单点零调用修复」= 序 **60** 与
> **序 8b 的 `float` 一处**；④「副作用消除」= 序 **8b 的 `ToASCII`**；⑤「单点可见性修复」= 序 **73**。
> 五批都**只覆盖各自那几项**，都**不**构成对 §7 待定表里其他任何一项的授权。
> ⚠️ 另有一轮**第五轮只读侦察**（**零代码改动**）：把序 8b 的 `ToHexBools` 定案为「无需改代码」。
> ⚠️ **修一条常常会"带出"下一条，但两条的处置方式不同** —— 本轮两个实例：
> ① 修序 47（工厂版补 `volatile`）时**发现同文件的 `SingletonInstanceAsync<T>` 有同型缺陷** ⇒ **只记录、不顺手改**（硬规则 1），
> 登记为 **序 73**（⚠️ 后经**第五批**单独授权，已于 2026-09-18 修复 —— **"先只记录"与"后来单独修"并不矛盾**）；
> ② 修序 3a（`throw ex;` → `throw;`）后 `catch (Exception ex)` 的 `ex` 变成未使用 ⇒ **会新引入 CS0168 ×2**，
> 这是**本次改动自己造成的**警告 ⇒ **必须当场清掉**（把参数名去掉）。
> **"别人本来就有的隐患"和"我刚弄出来的警告"是两件事**，别混。
> 其中第六轮（`Patterns/` + `Ui/Controls/DataGridViewEx`）贡献 **72 处**（§5.30），
> 而 `Patterns/` 的 68 处里 **14 处是纯死代码**（分支不可达 / 字段没人读 / 枚举值没人用 / Timer 从不触发）。
> ⚠️ 2026-09-18 两次"修一条带出下一条"：修序 41 时**顺带新发现 1 处（序 72）**、修序 47 时**又顺带新发现 1 处（序 73）**
> ⇒ `Patterns/` 记为 **69 处**，§7 的动作序号因此排到 **73**；
> 它不在第六轮的 71 处里，是**本轮新发现的**（同型缺陷，2 个类）。
>
> **已覆盖的 T0 目录**：
> `Core/Conversion`(12) / `Core/Concurrency`(4) / `Core/Collections`(3) / `Core/Compare`(1) /
> `Core/Data`(2，`BufferEx`+`ZIPEX`) / `Core/Enums`(1) / `Core/Reflection`(1) /
> `Core/Files`(5，含 `ScheduledDeletionEx`) / `Core/Notify`(1) / `Core/Platform`(5) /
> `Contracts`(2) / `Ui/Theme`(1) /
> `Distribution/Security`(6，第五轮) / `Distribution/Protection`(4，第五轮) /
> `Patterns`(11，第六轮) / `Ui/Controls/DataGridViewEx`(1，第六轮)
> = **60 / 60 个文件（100.0%）**。
> **仍未覆盖**：**无** —— T0 层已全部覆盖。
> （`Contracts/ImageInfo`(T2)、`Core/Data/CloneEx`(T2)、`Ui/Theme/ThemeService`(T2) 也已顺带覆盖。）

#### ⛔ 「覆盖」的判定口径必须写成**四种机制**（2026-09-18 机械复核后修正）

原文只写了一句口径：「测试文件里**直接引用**了该文件的产品类型」——
**这句话对 60 个文件里的 7 个不成立**。机械复核（脚本见下）实测的结果是四种机制并存：

| # | 机制 | 文件数 | 说明 |
|---|---|---|---|
| **①** | 类型名出现在测试**代码**里 | **53** | 最常见（`new Xxx(...)` / `Xxx.Method(...)` / `typeof(Xxx)`） |
| **②** | 类型名**只**出现在 `typeof(...)`（API 面反射用例）里，行为靠**裸扩展方法调用** | **5** | `BoolEx` / `DateTimeEx` / `ReadOnlySpanEx` / `RegexEx` / `StringEx` —— 它们都是 `static class` + `(this …)`，测试写的是 `true.ToByte()`、`Sample.ToDate()`、`"123".PaddingLength(5)`，**类型名根本不出现在代码里** |
| **③** | 类型名**完全不出现**（纯裸扩展方法调用，连 `typeof` 都没有） | **1** | `ListEx`（唯一方法 `Equal`）—— 测试写 `new List<int>{1,2,3}.Equal(...)`；`ListEx` 三个字只出现在**注释**和**测试方法名**里 |
| **④** | 文件**声明了零个类型**，只能靠**源码路径断言**覆盖 | **1** | `Ui/Controls/DataGridViewEx.cs`（87 行 / 73 行注释）—— 见 §5.30.6 |
| | | **60** | |

> ⛔ **为什么要写这么细**：口径不同，结论就不同。
> 若按"**类型名出现在测试代码里**"这一条严格判，只有 **53 / 60 = 88.3%**（把注释也算进去是 58/60，因为
> `<see cref="ListEx"/>` 与测试方法名 `ListEx_Equal_*` 会造成**假命中**）。
> **⇒ 报告"100%"时必须同时说明用哪种机制判定**，否则这个数字不可复现。
>
> **可复现的复核方法**（脚本已落盘，只读、不写仓库）：
>
> ```bash
> # ① 逐文件核对"是否被测试引用"（先剔除注释，再匹配类型名；逐行报命中类型 + 次数）
> python .workbuddy-ai/tools/audit_t0_coverage.py
> # ② 查"扩展方法名冲突" —— 判断某个 `.Foo(` 到底命中哪个 *Ex 类
> python .workbuddy-ai/tools/audit_ext_method_collision.py
> ```
>
> ⚠️ **② 是必须的一步**：`ToShort` / `ToInt` / `ToShortArray` / `ToUInt` 这些名字在 `BoolEx` /
> `ByteEx` / `ReadOnlySpanEx` / `RegisterEx` 之间**撞车** ⇒ 只靠 `grep "\.ToShort("` 会把
> `byte[].ToShort()`（`ByteEx`）误当成 `bool.ToShort()`（`BoolEx`）。脚本按"**接收者类型**"区分：
> `BoolEx` 的 5 个方法里只有 `ToByte` 全仓独占，但 `true.ToShort()` / `false.ToInt()` 的**接收者是 `bool`**
> ⇒ 仍然命中 `BoolEx`；`ReadOnlySpanEx.ToUInt` 与 `ByteEx.ToUInt(this byte[])` 同名，
> 但测试里显式声明了 `ReadOnlySpan<byte> two = …; two.ToUInt()` ⇒ 命中 `ReadOnlySpanEx`。
>
> **复核结论**：60 个文件的**行为**确实都被测到了（`ListEx.Equal` 7 个用例、5 个扩展方法类各自的裸调用用例、
> `DataGridViewEx` 4 个源码级用例）⇒ **100% 成立，但成立的方式比原口径写的复杂**。
> 这与 §6.2 的约定（"测试里一律**显式静态调用**，不要裸扩展方法调用"）是同一个问题的两面：
> 裸调用**能测到行为，但让"覆盖"变成不可机械核对的**。⚠️ 本轮**不改已有用例**（硬规则 1），只把口径写准。


---

### 5.11 ⛔ `string.ToHex()` 静默绑到 **BinaryEx**（二进制解析），不是 `ASCIIEx`

```csharp
// Extension.Core.Conversion 里有两个 ToHex 都能接受 string：
public static string ToHex(this string data, bool uppercase = true)  // ASCIIEx —— 缺省参数
public static string ToHex(this string data)                          // BinaryEx —— 无缺省参数
```

C# 重载决议在「省略可选参数」与「形参数量完全匹配」之间**优先后者** ⇒ `someString.ToHex()` 绑到
**`BinaryEx.ToHex`（把字符串当二进制串解析）**，而不是看起来更"对口"的 `ASCIIEx.ToHex`。

实测（`ConversionTests.重载决议陷阱_字符串ToHex默认绑到BinaryEx而不是ASCIIEx`）：

| 表达式 | 结果 | 说明 |
|---|---|---|
| `"1010".ToHex()` | `"A"` | 二进制语义：1010b = 0xA |
| `ASCIIEx.ToHex("1010")` | `"31303130"` | ASCII 语义：'1''0''1''0' |
| `"abc".ToHex()` | **抛 `FormatException`** | "Additional non-parsable characters are at the end of the string" |
| `"abc".ToHex(true)` | `"616263"` | 传了第二实参 ⇒ 无歧义，走 ASCIIEx |

**下游之所以没出事**，是因为**一律显式限定**（这是一个"靠调用方自律才不炸"的设计）：

- `Device/Device/RFID/椹诺/XK15A_R01Helper.cs:12,39,40` → `ASCIIEx.ToHex(...)`
- `Communication/Communication/MC/MCHelper11.cs:814` → `BinaryEx.ToASC(...)`

全仓 `.ToHex(` 共 **16 处**（含测试），**没有一处**写成裸的 `xxx.ToHex()`。

---

### 5.12 ⛔ 字符串 `ToASC` 有**两个同签名**实现 ⇒ 裸调用是 `CS0121` 编译错误

```csharp
public static string ToASC(this string data)   // HexEx    —— 十六进制字符串
public static string ToASC(this string data)   // BinaryEx —— 二进制字符串
```

**签名完全相同、又在同一命名空间** ⇒ 只要 `using Extension.Core.Conversion;` 存在，
`"41".ToASC()` 就是 `CS0121 二义性`错误。这**不是**运行时问题，但同样迫使调用方显式限定
（`HexEx.ToASC(...)` / `BinaryEx.ToASC(...)`），否则编译不过。

同类问题还有 `DecimalEx` 的 `short[]` 重载陷阱（实测用例
`DecimalEx_ToASC_无参重载走的是小端_与显式大端结果相反`）：

```csharp
public static string ToASC(this short[] data)                              // 走 Buffer.BlockCopy ⇒ 机器字节序（小端）
public static string ToASC(this short[] data, EEndianType = Big_Endian)     // 走显式大端
```

无参调用按重载决议绑定到**前者** ⇒ `data.ToASC()` 与小端一致、与"默认大端"**相反**。
读代码的人几乎一定会以为无参 = 大端。

**顺带**：三个 `ToHex` 的**大小写约定各不相同**（实测用例 `十六进制大小写对照_…`）：

| 方法 | 大小写 | 来源 |
|---|---|---|
| `ASCIIEx.ToHex` | **大写**（可切小写） | `b.ToString("X2")` |
| `BinaryEx.ToHex` | **大写**（固定） | `Convert.ToByte(...).ToString("X")` |
| `DecimalEx.ToHex` | **小写**（固定） | `Convert.ToString(data, 16)` |

---

### 5.13 ⚠️ `ByteEx` 三处缺陷 / 副作用（**2 处已修；第 3 处结论：无需改代码**）

补测 `ByteExTests` 时实测出来的，已固化成用例。**第 1 / 2 处已于 2026-09-18 修掉**
（序 8b；第 1 处在第三批、第 2 处在第四批，均逐项授权）；第 3 处经 2026-09-18 **第五轮只读侦察**定案：
**实现是对的，只有名字误导 ⇒ 结论「无需改代码」**（详见下方第五轮块）。

| # | 位置 | 现象 | 实测证据 |
|---|---|---|---|
| **1** | ✅ **已修（2026-09-18，序 8b）** `ToBytes(this float)` | `stackalloc byte[4]` 却调用 `BinaryPrimitives.WriteInt64BigEndian`（要求 ≥8 字节）⇒ **任何输入都抛 `ArgumentException`**，该方法**完全不可用** | `ToBytes_float_必然抛异常_是个不可用的死方法` |
| **2** | ✅ **已修（2026-09-18，序 8b 第四批）** `ToASCII(this byte[])` | 大端分支走 `data.AsSpan().Reverse()` —— Span 的 `Reverse` 是**原地**操作 ⇒ **会改写调用方传入的 `byte[]`**，签名完全看不出。⚠️ 实测该副作用**可触发**：`SerialHelper.DataReceived` 里同一个数组被三个消费者共享（详见下方勘误块） | `ToASCII_大端反转副本_不改动调用方数组` |
| **3** | ✅ **无需修（2026-09-18，第五轮只读侦察定案）** `ToHexBools(this byte[])` | 名字是"Hex"（十六进制），实现**只取每字节的 bit4 与 bit0**，`0x20/0x40/0x80/0x02/0x04/0x08` 全被丢弃 ⇒ `0xFF` 与 `0x11` 结果完全相同。⚠️ **但这不是缺陷**：该位宽**正是三菱 MC 协议位软元件的打包方式**（每字节 2 个位软元件），只有**名字**误导 | `ToHexBools_只认0x10和0x01_其余位被忽略` |

> ⛔ **勘误（2026-09-18，只读侦察时发现）**：原文写「三者**全仓零调用**」——
> **实测只有 `ToBytes(this float)` 成立**，另两处都有**真实生产调用点**：
>
> | 方法 | 真实下游（全仓 `grep -rn`，排除 `bin`/`obj`） | 判定 |
> |---|---|---|
> | `ToBytes(this float)` | 无 | ✅ 真·零调用 |
> | `ToASCII(this byte[])` | **10 处**（⚠️ 原文写「9 处」是**算错了** —— 本行清单自己合计就是 10）：`Communication/Communication/Serial/SerialHelper.cs:82,100`、`Communication/Communication1/Communication/SerialPort/SerialHelper.cs:82,100`、`Communication/CommunicationDemo/MainWindow.xaml.cs:48`、`Device/Device/RFID/Abstract/RFIDBase_ModbusRTU.cs:49,58`、`Device/Device/RFID/椹诺/XK15A_R01Helper.cs:16,44`、`Packing Machine/Packing Machine/Business/Flow/AutoProcess.cs:227` | ❌ 前提错 |
> | `ToHexBools(this byte[])` | **1 处**：`Communication/Communication1/Protocols/Mitsubishi/MC3E/MC3EHelper.cs:182`（**三菱 MC3E 协议位软元件解析**） | ❌ 前提错 |
>
> **绑定已确认**：`ToASCII` / `ToHexBools` 全仓**各只有一个定义**（`ByteEx.cs:122` / `:156`），
> **无同名扩展遮蔽**；调用方都 `using Extension.Core.Conversion;` ⇒ 确实绑到本文档要改的那个方法。
>
> **错因**：当初只 `grep` 了 `Extension/` **一个目录**，就把"该目录内零调用"外推成了"全仓零调用"。
> ⇒ **判据**：说"零调用"必须 `grep -rn <方法名> --include=*.cs .`（**全仓**、排除 `bin`/`obj`），
> 并确认**没有同名遮蔽**（同名不同类 / 同名不同命名空间都会骗过单文件 `grep`）。
>
> ⚠️ **顺带更正两条结论**：
> ① `ToASCII` / `ToHexBools` 是**真实在用的生产接口** ⇒「零调用 ≠ 该删」这条结论**对它们不适用**
> （它们本来就不该删，理由比"还没接线"更充分）。真正属于"还没接线的接口"的只有 `ToBytes(this float)`。
> ② **序 8b 的风险等级随之作废**：原文标「低（三者全仓零调用）」—— 前提不成立。
> 现拆为：`float` 的 `ToBytes` **已修**（零调用，真·低）；`ToASCII` **已修**（第四批 ——
> 10 处调用点逐个核对 ⇒ 返回值全不变）；`ToHexBools` **经第五轮定案为「无需改代码」**（见下方第五轮块）。
>
> ⛔⛔ **`ToHexBools` 的修法里「补全位」必须删掉**：唯一调用点是**三菱 MC3E 协议位软元件解析**，
> 它按「每字节 bit4 / bit0 = 两个位软元件」的编码工作 ⇒ **改位宽会直接改协议解析结果、打断实机 PLC 通讯**。
> 只允许「正名 / 补注释」。⚠️ 该路径属**实机设备通讯**，改动前须在真实 PLC 上验证。
>
> ✅ **第四批实测（2026-09-18：只读侦察 + 修后复核）—— `ToASCII` 的修法已落地，10 处调用点逐个核对，返回值全部不变。**
> ① 文档标「需逐点确认」的 **4 处全部确认安全**：`RFIDBase_ModbusRTU.cs:49,58` 的 `data` 来自
> `ModbusRTUHelper.ReadHoldingRegisters(...)`，而后者实现是 `return value.ToArray();`（**每次新建**），
> 且 `ToASCII` 之后**不再使用 `data`**；`XK15A_R01Helper.cs:16,44` 走 **`Little_Endian`**
> （不进反转分支），且 `d1` 之后不再使用（后面只用 `d2`）。
> ② 另 6 处是 `return x.ToASCII()` / 链式调用（`…ToASCII(...).Trim()`）形式，天然安全。
>
> ⛔ **同轮新发现（文档原先没有）：`SerialHelper` 里同一个数组被三个消费者共享。**
> `DataReceived`（`Communication` 与 `Communication1` 两个副本写法相同）里，
> `Datas.Write(received)`（→ `Read()`）、`valueTaskSource.SetSignal(received)`（→ `WriteWithReply`）、
> `_dataReceived?.Invoke(sender, received)`（→ 事件订阅者，`ScanBase` 在 `Task.Run` 里**异步**用）
> **用的是同一个 `received` 实例** ⇒ 任何**就地**反转都会改到一个别的消费者仍看得见的数组 ⇒ **无锁数据竞争**。
> **但可达性把风险压住了**：其中「会反转」的两条入口 —— `SerialHelper.WriteWithReply(string,int)`（`:73`）
> 与 `SerialHelper.Read()`（`:97`）—— **全仓零调用**（两个副本都零调用；旁证：`ScanBase.cs:45` 用的是
> `DataReceivedEvent`，`RFIDBase_Serial` 用的是 `WriteWithReply(byte[])`，
> `RFIDManagerService.Read()` 走的是 `IRFID.Read(page)`）⇒ **不可达**。
> 唯一活路径（事件订阅者）走 **`Little_Endian`**（不反转）⇒ **今天无实际故障**。
> ⇒ 净效果：改「反转副本」对 10 处**返回值全不变**，并**顺带消掉这个竞争隐患**。
>
> ⚠️ **修后踩到的坑（值得记）**：先写成 `if (data != null && endianType == Big_Endian)`，
> 结果编译器在落到最后一行 `GetString(data)` 的路径上推知「`data` 可能就是 null」⇒ **净增 1 条 `CS8604`**。
> 改用**开头显式 `throw new ArgumentNullException(nameof(data))`** 后归零。
> ⛔ **只比「警告总数」会漏掉它**（124 vs 123 极易被当成口径差异）—— **必须比集合**。

> ✅ **第五轮只读侦察（2026-09-18）：`ToHexBools` 定案 —— 实现是对的，只有名字误导 ⇒ 无需改代码。**
> 关键证据是**两条路径的位宽对照**（这是此前文档没有的）：
>
> | 路径 | 位宽 | 语义 |
> |---|---|---|
> | `MC3EHelper.cs:180-184`（Qna 3E 帧） | `bv.ToHexBools()` ⇒ **每字节 2 点** | 直接发 `BuildRead` 读**位软元件**，响应里位软元件是 **nibble 打包** |
> | `MCHelper.cs:170-178` | `new bool[DeviceValue.Length * 16]` ⇒ **每字 16 位** | 拿到 `short[]` 字数据后按 16 位展开（**读字拆位**） |
>
> ⇒ 位宽差 8 倍是**协议差异，不是缺陷**。`E_McDeviceCode`（`MCDataPoint.cs:111`）把
> `M/X/Y/B` 标为 bit（`0x90/0x9C/0x9D/0xA0`）、`D/W/R` 标为 word（`0xA8/0xB4/0xAF`），正好对上。
> `& 0x10` / `& 0x01` 正是三菱 MC 协议**位软元件的 nibble 打包格式**（每点占 4 bit，
> 有效位在 nibble 的 bit0）。
> ⛔ **只看「两条路径位宽不一致」就下「其中必有一个错」的结论是误判** ——
> 必须先把两者的**协议操作**（读位 vs 读字）读清。两者名字都叫 `ValueCompare_Bool`、
> 都有 `case M/X/Y/B`，光看命名与分支根本分不出来。
>
> ⛔⛔ **由此坐实：`ToHexBools` 的任何「补全位」改法都必须否决** —— 唯一调用点
> `MC3EHelper.cs:182` 是**三菱 MC3E 协议位软元件解析**，改位宽会直接改协议解析结果、
> **打断实机 PLC 通讯**。全仓 `grep -rn "ToHexBools"`（排除 `bin`/`obj`）确认**生产代码仅此 1 处**
> （另有 1 处在 `.workbuddy-ai/encoding-backup/…`，是**编码备份、非活代码**）。
> ⚠️ 该路径属**实机设备通讯** ⇒ 若将来真要动它（哪怕只是正名），须先在真实 PLC 上验证。
>
> ⛔ **同轮顺带发现两条隐患（按硬规则 1 只记录、未改）**：
> ① **`ConfigValueArray` 缺 `case B` / `case W`**（`MCBase.cs:99-130` 只给 `M/X/Y` 建 `bool[num]`、
> `D/R` 建 `byte[num*2]`），而两个读路径（`MC3EHelper.ReadValue` / `MCHelper.FillData`）**都有 `case B`**
> （`MCHelper` 还有 `case W`）⇒ 这些分支**不可达**：`AddDataPoint`（`:171-174`）会先抛
> `FormatException`「…对应的内存1缓存没有被初始化」。
> ② **奇数点数的批量读**：Qna 3E 读 n 个位软元件 ⇒ 响应 `ceil(n/2)` 字节 ⇒ `ToHexBools`
> 返回 `2 × ceil(n/2)` 个 bool，**偶数完全匹配、奇数会多 1 个**。`ValueCompare_Bool`（`MCBase.cs:372`）的
> `Array.Copy(b2, 0, mValueGroup[code], offest1, b2.Length)` 中 `offest1 = startAddr`（**绝对地址**，
> 见 `MC3EHelper.cs:124`）⇒ 边界上可能越界。**待定**：需看 `mReadList` 的 `AddressBlock` 合并逻辑
> 能否保证起点/长度为偶数。

---

### 5.14 两处"静默语义"与 `ReflectionEx` 的 API 面不一致

**a) `ConcurrentList<T>` 的越界行为与 `List<T>` 相反**（`CollectionsTests.ConcurrentList_索引越界_…`）

| 操作 | `List<T>` | `ConcurrentList<T>` |
|---|---|---|
| `list[99]`（读越界） | 抛 `ArgumentOutOfRangeException` | **返回 `default(T)`**（int → `0`，string → `null`） |
| `list[99] = x`（写越界） | 抛 `ArgumentOutOfRangeException` | **静默丢弃** |

⇒ "写错下标"的 bug 会变成**静默的数据丢失**，而不是尽早失败。

**b) `ConcurrentList<T>.GetEnumerator()` 的 `_disposed` 检查是延迟的**

方法体含 `yield return` ⇒ 整个方法体（含 `if (_disposed) throw`）**延迟到第一次 `MoveNext` 才执行**。
所以 `Dispose()` 之后 `list.GetEnumerator()` **不抛**，`foreach` 才抛。
（顺带确认：`yield return` 在 `lock` 块内 ⇒ 锁是**每次 MoveNext 进出一次**，不跨整个枚举，
所以"枚举中 Add"不会死锁 —— 快照语义保证枚举结果稳定。）

**c) `ReflectionEx` 是全仓唯一**的**非静态** `*Ex` 类**

`HexEx` / `BinaryEx` / `BoolEx` / `DecimalEx` / `DateTimeEx` / `RegexEx` / `StringEx` / `ByteEx` /
`ReadOnlySpanEx` / `ListEx` 全是 `static class`，只有 `ReflectionEx` 是普通实例类（调用方必须先 `new`）。

**d) `ReflectionEx` 的静默 vs 抛异常，同一件事两套语义**

| 方法 | 属性不存在 / 不可写 |
|---|---|
| `SetProperty` | **抛** `ArgumentException` |
| `GetProperty` | **抛** `ArgumentException` |
| `SetProperties` | **静默跳过**（不抛） |
| `GetProperties` | 只收集公共**属性**，公共**字段**被忽略 |

**e) `ReflectionEx` 用的是 `typeof(T)`，不是 `obj.GetType()`**

⇒ 以 `object` / 基类的静态类型传入派生实例时，属性查找**必然失败**（`ArgumentException`）。
`SetProperty<T>` 的 `T` 是编译期推断的，运行时类型无关。

---

### 5.15 ⛔ `BitOperationEx` 两处缺陷（**有 5 个下游调用点，不是死代码**）

`Core/Compare/BitOperationEx.cs` 是 PLC 侧的"变化点检测"底座（上升沿/下降沿/双边沿）。
全仓 **5 个真实调用点**，全部走 `unsafe` 指针路径：

| 调用点 | 实参 |
|---|---|
| `Communication/Communication/CommunicationManager.cs:73` | `Compare_Ptr(CurData[t], LastData[t], polarity)` |
| `Communication/Communication/MC/MCHelper11.cs:916` | `Compare_Ptr(CurData, LastData, RisingEdge)` |
| `Communication/Communication1/.../MCBase.cs:363` | `Compare_Ptr(bv1.ToArray(), bv2.ToArray())`（`bool[]`） |
| `Communication/Communication1/.../MCBase.cs:392` | 同上（`byte[]`） |
| `WPFControl/.../Alarm/AlarmInfoManagerService.cs:107` | `Compare_Ptr(data, last, BothEdges)` |

#### 缺陷 1：`Sixteen` / `Thirty_Two` 模式**漏报**差异（bit 位移量写错）

`ArrayCompare<T>` 里块内取字节用的是：

```csharp
if (((xor >> (L * i)) & 0xff) > 0)     // ⛔ L = 2^pCompareSpeed，应为 8 * i
```

`L = 2^speed` ⇒ **只有 `L=1`(Eight) 与 `L=8`(Sixty_Four) 时 `L*i == 8*i` 恰好成立**；
`L=2`(Sixteen) 与 `L=4`(Thirty_Two) 时移位量偏小、覆盖的 bit 区间错位 ⇒ **漏报**：

| 模式 | `i=1` 实际覆盖的 xor 位 | 应该覆盖 | 结论 |
|---|---|---|---|
| Eight（L=1） | bit0..7 | bit0..7 | ✅ 正确 |
| **Sixteen（L=2）** | **bit2..9** | bit8..15 | ❌ bit10..15 的差异**永远检测不到** |
| **Thirty_Two（L=4）** | **bit4..11** | bit8..15 | ❌ bit12..15 漏 |
| Sixty_Four（L=8） | bit8..15 | bit8..15 | ✅ 正确 |

实测（`BitOperationExTests`）：

```csharp
var b1 = new byte[] { 0x00, 0x04 };   // 第 2 字节 = 0x04（xor 的 bit10）
var b2 = new byte[] { 0x00, 0x00 };

BitOperationEx.Compare_Ptr(b1, b2, BothEdges, ECompareMode.Eight)    // → 1 处差异（Index=1）✅
BitOperationEx.Compare_Ptr(b1, b2, BothEdges, ECompareMode.Sixteen)  // → 0 处差异 ❌ 漏报
```

> **为什么至今没炸**：`Handle` 内部会**再比一次真实值**，所以 xor 检查的**误报**会被过滤掉；
> 但**漏报**没法补救。而且 `Compare_Ptr` 的 `pCompareSpeed` 默认值是 `0`(Eight)，
> **5 个下游调用点一个都没传速度模式** ⇒ 缺陷目前是**潜伏**的，一改速度就现形。

#### 缺陷 2：两个**空数组**会抛 `NullReferenceException`

空数组经 `fixed` 得到的是 **null 指针（地址 0）**，而 C# 的指针 `<=` 比较是**无符号**的：

```csharp
T* EndAddress  = pValue1 + pLength;   // = 0
T* LastAddress = EndAddress - L;      // = 0 - 1 → 回绕成 0xFFFF…F
while (pValue1 <= LastAddress)        // 0 <= 0xFFFF…F ⇒ **true**！
    ... Handle(...)                   // 循环体带着 null 指针 → *(pValue1+0) → NullReferenceException
```

⇒ **长度相等的空数组（本应是合法输入）会崩**。这不是"没做参数校验"（长度相等是过了校验的），
而是**指针比较的符号性**导致的循环守卫失效。

**可达性实测**：

| 调用点 | 能否为空 |
|---|---|
| `CommunicationManager.cs:73` | ❌ 输入是 `new short[Int16.MaxValue]`，恒非空 |
| `MCHelper11.cs:916` | ❌ `CurData = new short[Int16.MaxValue]`，恒非空 |
| `MCBase.cs:392`（`ValueCompare_Byte`） | ⚠️ **可达** —— `length = b2.Length - offest2`，若响应只到表头（`offest2 = 11` 且 `b2.Length == 11`）则 `length = 0` ⇒ 两个空数组 ⇒ **崩** |
| `MCBase.cs:363`（`ValueCompare_Bool`） | ⚠️ **可达** —— `bv1` 长度取 `b2.Length`，为 0 即崩 |
| `AlarmInfoManagerService.cs:107` | ⚠️ **可达** —— `data` 由配置的读取长度决定，为 0 即崩 |

⇒ 建议修法（**未做**）：把循环守卫从"指针比较"改成"**索引比较**"
（`for (int idx = 0; idx + L <= pLength; idx += L)`），既修空数组、也让 `LastAddress` 不再依赖无符号回绕。

---

### 5.16 ⛔ `RegisterEx` 四处语义陷阱（打包/拆包**不对称**）

`RegisterEx` 是 PLC/Modbus 寄存器数组与 `int`/`float`/`double`/`string` 之间的唯一桥梁（16 个方法），
零引用风险不高（下游在用），但下面 4 条都**只在运行时暴露**：

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **1** | `ToShortArray(this string)` | 走 `Encoding.UTF32` 且末尾 `.Where(x => x != 0)` ⇒ **吞掉所有 0 值**：`"A\0B"` 与 `"AB"` 得到**完全相同**的 `short[]`；码位 U+10000 正确应为 `{0,1}`，实际只剩 `{1}` ⇒ **不可逆地丢失数据** | `ToShortArray_string_内嵌NUL会被静默丢弃_是有损的`、`ToShortArray_string_码位U10000的低字0被丢弃_数据被破坏` |
| **2** | `ToStringArray(this short[])` | 走 `Encoding.UTF8` 逐 2 字节解码 ⇒ 与 **#1 不是互逆**：`"AB".ToShortArray().ToStringArray()` 得到 `{"A\0","B\0"}`；非法字节序列（如 `0x00FF`）被替换成 `U+FFFD`，不可逆 | `ToStringArray_与string的ToShortArray不是互逆关系`、`ToStringArray_非法UTF8字节会被替换成UFFFD_信息不可逆` |
| **3** | 长度校验**三档不一致** | `ToIntArray`/`ToFloatArray` 要 **2** 的倍数、`ToDoubleArray` 要 **4** 的倍数；且 null/空在**校验之前**返回空数组。另外 `ToBoolArray`/`ToList_BoolArray`/`ToBinaryArray(short[])` **完全没有 null 保护**（抛 `NullReferenceException`），而 `ToIntArray` 一族有 ⇒ **同一族两种 null 语义** | `ToDoubleArray_null或空返回空数组_长度必须是4的倍数`、`ToList_BoolArray_null会抛NullReferenceException_没有null保护` |
| **4** | `ToBoolArray` 的 `1 << i` | `data` 提升为 `int` ⇒ `i ≥ 16` 时按 **32 位**取值：`((short)-1).ToBoolArray(32)` 给出 **32 个 true**。而 `ToBinaryArray` 恒为 16 字符 ⇒ 两个"16bit 位视图"语义不同 | `ToBoolArray_长度超过16位时_高位按int提升后的补码给出true` |

> 已用**属性测试**把 #4 的两种表示钉在一起：`ToBoolArray` 逆序拼串 == `ToBinaryArray`（16 字符，MSB 在前）。

### 5.17 ⛔ `ChannelHelper<T>` / `ValueTaskSourceHelper<TResult>` 三处行为

`Core/Concurrency` 是设备回调与日志队列的缓冲底座（4 文件 / 905 行），此前零用例覆盖：

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **1** | `ChannelHelper<T>` 构造 | 签名默认满模式是 **`DropOldest`**，而 XML 注释写的是"默认为**等待**" ⇒ 默认配置下写满**静默丢弃最旧数据** | `构造_默认满模式其实是DropOldest_与XML注释说的等待不符` |
| **2** | `ChannelHelper<T>.Write` | 内部是 `_writer.TryWrite(item)` 且**丢弃返回值** ⇒ 满模式为 `Wait` 时写满**既不阻塞也不报错，新数据静默丢失**（只有 `WriteAsync` 会阻塞）。<br>另：`ClearAsync` 的 `while (await WaitToReadAsync())` 在通道未完成时空转 ⇒ **空通道上永久挂起**，只有 `Complete()` 后才返回；`TryReadAsync` 名不副实（内部是 `ReadAsync`）⇒ 空通道挂起、已完成空通道抛 `ChannelClosedException`；`IsCompleted` 镜像的是 `Reader.Completion`，与私有 `_isCompleted` 标志**不是一回事**（`Complete()` 后缓冲未读尽仍为 `false`） | `Write_满模式为Wait时写满会静默丢新数据_是缺陷`、`ClearAsync_空通道上会一直挂着_只有Complete之后才返回`、`TryReadAsync_名字像Try但空通道上是挂起而不是返回默认值`、`IsCompleted_镜像Reader_Completion_缓冲未读尽时仍为false` |
| **3** | `ValueTaskSourceHelper<TResult>` | **调用顺序敏感**：构造后 `_tcs.Version == 0` 且状态已是 `Pending` ⇒ **先 `SetSignal` 再 `WaitSignal`** 时，`WaitSignal` 里的 `Reset()` 会把刚设的结果丢掉，等待方**永远等不到**（只能靠超时/取消兜底）。<br>另：Dispose 后行为**不对称**（`WaitSignal` 抛 `ObjectDisposedException`，`SetSignal`/`SetException` 只返回 `false`）；`WaitSignal(0)` 表示**不设超时**（见 §5.19 #1 的相反语义） | `SetSignal在WaitSignal之前调用会被Reset丢掉_信号永久丢失`、`SetSignal与SetException在Dispose之后只返回false不抛异常` |

### 5.18 ⛔ `EventHelper` 两处缺陷（SECS 指令分发底座）

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **1** | `EventHelper.cs:145,182` —— `throw ex;` | 与 §5.2 同源（CA2200 ×2）。后果有**两个**，都不只是"警告"：① **栈跟踪被重置**，处理器那一帧丢失，排查时看不到真正的出错点；② 异常外传 ⇒ **后续处理器被整体跳过** —— 与注释"逐个调用并捕获异常"的意图相反。<br>断言方式：处理器用**命名方法**（不是 lambda），于是可以精确断言 `ex.StackTrace` **不含**处理器名、**含** `TriggerEvent` | `TriggerEvent_处理器抛异常时异常会外传_且后续处理器被整体跳过`、`TriggerEvent_用的是throw_ex_栈跟踪被重置_处理器那一帧丢失` |
| **2** | `WaitForEventAsync` 的清理 | **超时路径不会 `unregister`**（`finally` 只 `Dispose` 了取消注册）⇒ 处理器永久残留在 `_Events` 里，**每次超时泄漏一个**。用反射读私有静态 `_Events` 直接断言 `HandlerCount == 1`。<br>**取消路径的清理是竞态**的：本方法在 `cts.Token` 上的注册与 `WithTimeout` 内部 `linkedCts` 的注册按 **LIFO** 执行，linked 那条先跑 ⇒ 异常先冒出来 ⇒ `finally` 抢先 Dispose 掉本注册 ⇒ 清理回调可能**根本轮不到执行**（实测处理器留了下来，与超时路径同病） | `WaitForEventAsync_超时抛TimeoutException_但处理器没有被取消注册_是缺陷`、`WaitForEventAsync_取消令牌触发时抛OperationCanceledException` |

> 附带发现：`EventHelper` 声明是 `public class` 而**不是** `static class`（8 个公开静态成员）⇒ 可以被 `new` 出来；
> 两个 `WaitForEventAsync` 重载的默认超时**不一致**（委托版 10000 / 命令版 100000，差 10 倍）。
> `WaitForEventAsync` 的**参数校验异常是装进 Task 的**（`async` 方法），必须用 `ThrowsAsync` 才能捕到。

### 5.19 ⛔ `TaskEx` 四处行为（全仓异步超时的统一入口）

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **1** | `WithTimeout` 的 `timeout = 0` | 表示**立即超时**（`new CancellationTokenSource(0)` 创建即已取消），而 `ValueTaskSourceHelper.WaitSignal(0)` 表示**不设超时** ⇒ **同一语义在两个类里相反** | `WithTimeout_timeout为0会立即超时_与WaitSignal的0表示不超时相反` |
| **2** | `WithTimeout` 超时后 | **不会取消原始任务**（只是"不再等"）⇒ 原任务仍在后台继续跑，之后仍可正常完成 | `WithTimeout_超时不会取消原始任务_它仍在后台继续跑` |
| **3** | `WithCancellation` | 先查 `token.IsCancellationRequested` 再 `WhenAny` ⇒ **令牌已取消时，即使任务早已完成也会抛异常** | `WithCancellation_令牌已取消时_即使任务早已完成也会抛异常` |
| **4** | `LongTimeRun` / `TimesRun` | 用 `await Task.Factory.StartNew(async () => {...}, LongRunning)` ⇒ 拿到的是 `Task<Task>`，`await` 只等**外层委托**返回 ⇒ **`await` 一返回循环其实还在后台跑**，返回的 Task **不代表循环生命周期**（经典 `StartNew` + `async` 坑） | `LongTimeRun_await返回后循环其实还在后台跑_返回的Task不代表循环生命周期` |

> 附带发现：`ThreadRun` **没有任何参数校验**（`LongTimeRun`/`TimesRun` 都有）⇒ 传 `null` 会在后台线程上抛未处理异常，
> 在 .NET Core 上**可能直接终结进程** ⇒ 测试**故意不覆盖**该分支；其 `threadPriority` 参数**从未被使用**（死参数）。
> 另：`WithTimeout` 内部的 `timeoutCts` / `linkedCts` **没有 `using`**，每次调用泄漏一个 CTS 注册。

---

### 5.20 ⛔ `FileEx` 四处行为（"写前 `.bak` 轮换"的副作用语义）

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **1** | `Write` 覆盖已存在文件 | 先 `File.Copy(path, path + ".bak", true)` 再写 ⇒ **`.bak` 只保留一代**：第 N 次写入后 `.bak` 里是第 N-1 版，更早的版本永久丢失 | `Write_bak只保留一代_三次写入后bak里是倒数第二版` |
| **2** | `Write` 备份失败 | `catch (...) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or SecurityException)` ⇒ **吞掉异常并继续写入**，只在 `Debug.WriteLine` 留痕 ⇒ **发布版（无 Debug 监听器）完全静默**，直到真需要 `.bak` 时才发现它从来没生成过 | `Write_备份失败不阻断写入_异常被过滤器吞掉` |
| **3** | `Write(data: null)` | ⛔ **不抛异常，而是把文件静默截断成 0 字节**。`File.WriteAllText` 只校验 `path`；`new StreamWriter(path, false, ...)` **先截断文件**，随后 `writer.Write(null)` 是 no-op ⇒ 数据丢失且无任何错误信号（旧内容此时只剩 `.bak` 一份） | `Write_数据为null时不抛异常_而是把文件静默清空` |
| **4** | API 面不对称 | `Write` 是 `this string` **扩展方法**，`Read(filePath, fileName)` **不是** ⇒ 同一个类里两种调用风格 | `API面_Write是扩展方法_Read不是` |

> 附带（.NET 语义，已实测）：`Path.Combine(filePath, fileName)` 在 `fileName` 为**绝对路径**时
> **直接丢弃 `filePath`** ⇒ 误传绝对路径会静默读写到另一个位置。

---

### 5.21 ⛔ `DirectoryEx` 六处行为（"年/月/日/产品ID 分层落盘"的公共底座）

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **1** | `GetParentSecond` | ⛔ `Directory.GetParent(pPath).Parent` —— **第一层没做 null 检查** ⇒ 根路径直接 `NullReferenceException`；而 `GetParent` 对同一个根路径返回 `null`（安全）⇒ **同一族两个方法一个安全一个崩**（实测 `Directory.GetParent(@"C:\")` 返回 `null`） | `GetParentSecond_根路径抛NullReferenceException_第一层没做null检查` |
| **2** | `GetDirName` | `var dirInfo = new DirectoryInfo(pPath); if (dirInfo != null) {...} return null;` —— `DirectoryInfo` 构造函数**永不返回 null**（空串直接抛 `ArgumentException`）⇒ `return null` 是**死代码**，该方法不可能返回 null | `GetDirName_的null检查不可达_永不返回null` |
| **3** | `CreatDir` / `GetPath` | ⛔ 月、日**不补零**（`Now.Month.ToString()`）⇒ 生成 `2026\9\18` 而不是 `2026\09\18` ⇒ 按字符串序排目录时 **10 月会排到 9 月前面** | `CreatDir_结构是基路径加年月日加产品ID`、`GetPath_月日不补零_一月二日是1与2而不是01与02` |
| **4** | `CreatDir` | 名字叫 Creat 但**只拼路径、不落盘**（不调 `CreateDirectory`）；且 `pID` 为 `null` 与空串**都少一层**（`Path.Combine(p, "")` 返回 `p`，不追加分隔符），但走的是两个不同分支 | `CreatDir_名不副实_只拼路径不落盘`、`CreatDir_pID为空字符串时_同样少一层` |
| **5** | `DirCopy` | ⛔ 用 `file.Replace(srcPath, destPath)` 做路径换算 —— **纯字符串替换**。源路径带尾分隔符而目标不带时，那个分隔符一起消失：`...\s\f.txt` → `...\df.txt` ⇒ 文件写到**目标目录的同级**、名字被粘成 `df.txt`（实测：`"C:\tmp\a\x.txt".Replace("C:\tmp\a\", "C:\tmp\b") == "C:\tmp\bx.txt"`） | `DirCopy_源路径带尾分隔符时_文件会落到目标目录之外` |
| **6** | `GetFiles(filter)` | 过滤是**"包含"匹配**（拼成 `*filter*`），既非前缀也非后缀 ⇒ 传 `"b"` 会命中 `ba.txt` / `b.txt` / `b.log` | `GetFiles_过滤是包含匹配_既非前缀也非后缀` |

> **未做成用例的隐患**（做了会让测试失控，故只记录）：`DirCopy` 的 `destPath` 若位于 `srcPath` **内部**，
> `Directory.EnumerateFiles(..., SearchOption.AllDirectories)` 是**惰性枚举**，会枚举到**自己刚创建的文件**
> ⇒ 理论上无界增长。这类"自喂"只在调用方传错路径时出现，且无法用有限断言稳定复现（会真的写满磁盘），
> 因此**只在文档里警示**，不写成测试。

---

### 5.22 ⛔ `ZIPEX` 两处行为（gzip → string）

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **1** | `new StreamReader(gzipStream)` | 默认 UTF-8 且 **BOM 探测开启** ⇒ ① 负载带 BOM 时 **BOM 被静默吃掉**（返回值比原文少 1 个字符）；② 非 UTF-8 负载（如 GBK 日志）**不报错**，而是静默替换成 `U+FFFD` ⇒ 解出来是乱码但调用方无从察觉 | `GZIPExtractToString_带BOM的负载_BOM会被静默吃掉`、`GZIPExtractToString_非UTF8负载_静默替换为UFFFD而不报错` |
| **2** | 整个类只有"解"没有"压"，也没有 `TryGZIPExtractToString` 之类的容错入口 | 文件缺失抛 `FileNotFoundException`、内容不是 gzip 抛 `InvalidDataException`，全部由调用方自己兜 | `GZIPExtractToString_文件不存在_抛FileNotFoundException`、`GZIPExtractToString_内容不是GZIP_抛InvalidDataException` |

---

### 5.23 ⛔ `CloneEx`（`FastDeepCloner`）两处行为

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **1** | `DeepClone("abc")` | ⛔ **抛异常**：`System.ArgumentException: Uninitialized Strings cannot be created.`（`FastDeepClonerCachedItems.Creator` 走"创建未初始化实例"的快速路径，而 CLR 不允许这样构造 `string`）。注意：`string` 作为**属性**（如 `Model.Name`）能正常克隆，只有作为**克隆根**才炸 | `DeepClone_字符串作为根对象会抛ArgumentException` |
| **2** | `DeepClone(null)` | **原样返回 `null`，不抛异常**（`FastDeepCloner` 内部兜了底）⇒ 调用方不必判空，但"对象意外为 null"也不会在这里暴露 | `DeepClone_null引用_原样返回null且不抛` |

> 该文件是 `Core/Data` 里**唯一**带第三方依赖的（`using FastDeepCloner;`），按"取最高级别"规则归 **T2**。

---

### 5.24 ⛔ `NotifyPropertyChanged` 三处行为（0 引用，但属 T0 公开 API 面）

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **1** | `OnPropertyChanged(string)` | 参数是**裸 `string`**，没有 `[CallerMemberName]` ⇒ 调用方必须显式传属性名；传 `null` 就是 `null`，不会被自动补成调用者名字（现代 `ObservableObject` 普遍带该特性） | `OnPropertyChanged_参数是裸string_没有CallerMemberName`、`OnPropertyChanged_属性名为null时_原样传给事件参数` |
| **2** | 覆写 `OnPropertyChanged` | 是 `protected virtual`，但**基类不做任何兜底** ⇒ 派生类漏掉 `base.OnPropertyChanged(...)` 就是**彻底静默**（不报错、不通知） | `覆写时若不调base_事件不会触发` |
| **3** | 事件触发方式 | `PropertyChanged?.Invoke(...)` 走**多播委托默认语义**：某个订阅者抛异常时，**排在它后面的订阅者不会被调用**（不是"逐个 try 隔离"）。谁把它改成逐处理器隔离，这条用例就会变红 | `某个订阅者抛异常_排在它后面的订阅者收不到通知` |

> 该类型**全仓 0 引用**。按硬规则 1 不删，已隔离到 `Core/Notify/`；上面的用例钉的是**契约本身**
> （未来 ViewModel 继承它时的行为基线），而不是当前调用方行为。

---

### 5.25 ⛔ `CSVEx` 九处行为（**7 处下游调用点，不是死代码**）

下游：`Packing Machine/…/StartupFlow/03_初始化通讯.cs`（读 `CRegisterInfo`）、`Secs/Secs/Variable/DataBase.cs`
（读写）、`Secs/…/VID/VIDBase.cs`、`WPFControl/…/Method/QuerySave.cs`、`WPFControl/…/AlarmInfoManagerService.cs` ×2。

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **1** | `ParseCsvLine` 的 `else if (c == '\\') escaped = true;` | ⛔ **读侧把 `\` 当转义前缀，写侧（`EscapeCsvValue`）却从不产生它** ⇒ 含反斜杠的值**往返即损坏**：`C:\temp\a.txt` 读回来是 `C:tempa.txt`。副产物：`a\,b` 会被读成**一个**字段 `a,b`（读侧独有的转义，写侧看不见） | `ReadCsv_反斜杠被当成转义符吞掉`、`往返_含反斜杠的值_导出后再读回会损坏` |
| **2** | 同上，`"` 的处理 | ⛔ 写侧把 `"` 双写成 `""`（RFC 4180），读侧只做 `inQuotes` 翻转、**不还原** ⇒ `say "hi"` 往返成 `say hi` | `ReadCsv_双写引号不被还原_引号被吞掉`、`往返_含双引号的值_引号在回读时丢失` |
| **3** | `ParseCsvLine` 的 `.Trim()` | ⛔ 每个字段都被 Trim ⇒ 首尾空格静默丢失，`"  a  "` 往返成 `"a"` | `ReadCsv_字段首尾空格被Trim掉`、`往返_首尾带空格的值_导出后再读回会丢空格` |
| **4** | `WriteDataLineToBuilder` 的 `value?.ToString()` | ⛔ 集合属性导出的是**类型名**：`List<int>` → `System.Collections.Generic.List\`1[System.Int32]` | `ExportToCsvAsync_集合属性_导出的是类型名而不是元素` |
| **5** | `SetPropertyValue` 的 `List<>` 分支 | ⛔ 一律 `Split(',')` 成 `List<string>` 再 `SetValue` ⇒ 属性是 `List<int>` 时抛 `ArgumentException`（`List<string>` 能过）—— 与 #4 合起来看，**集合属性读写两侧完全不配套** | `ReadCsv_ListInt属性_抛异常_因为塞进去的是ListString` |
| **6** | `List<>` 字段的书写格式 | ⛔ 必须写成**带引号**的 `"[1,2,3]"`；不加引号会先被逗号拆开，只有 `values[0]="[1"` 被用上 ⇒ **静默**变成单元素列表 `["1"]` | `ReadCsv_ListString属性_带引号的方括号列表_被拆成多个元素`、`ReadCsv_ListString属性_不加引号的方括号列表_只取到第一个元素` |
| **7** | `ReadCsv` 的两层 `catch (Exception)` | ⛔ 所有异常被包成**裸 `Exception`** ⇒ 调用方**无法按类型 catch**（文件不存在不再是 `FileNotFoundException`，而是 `Exception("读取CSV文件失败: …")`，内层再套 `Exception("处理第 N 行时出错: …")`） | `ReadCsv_文件不存在_抛裸Exception而不是FileNotFoundException`、`ReadCsv_单元格类型转换失败_异常被两层包装` |
| **8** | `ExportToCsvAsync_String` | ⛔ `includeHeader` 参数**被忽略**（内部硬编码 `ExportToCsvAsync(wrapped, filePath, false)`）；XML 注释里写的 `headerName`/`delimiter`/`encoding` 三个参数**签名里根本不存在** | `ExportToCsvAsync_String_includeHeader参数被忽略`、`API面_ExportToCsvAsync_String的签名与XML注释不符` |
| **9** | 默认编码 `Encoding.UTF8` | 会写 **UTF-8 BOM**（`EF BB BF`）⇒ 与同库 `JsonEx.Save`（无 BOM）**口径不一致**；`ReadCsv` 侧靠 `File.ReadAllLines` 的 BOM 探测才没出错 | `ExportToCsvAsync_默认编码会写UTF8_BOM`、`ExportToCsvAsync_显式传入无BOM编码_不写BOM` |

> 附带记录（无缺陷，但语义值得写清）：`ExportToCsvAsync` 按 `BatchSize = 1000` 分批 flush；
> 空字段被调用方 `!string.IsNullOrWhiteSpace(values[j])` **跳过**（属性保持默认值）——
> 这使 `SetPropertyValue` 里那段"可空类型 → `SetValue(obj, null)`"分支**在 ReadCsv 路径上不可达**；
> `ExportToCsvAsync` 对"无公共属性"的类型抛 `InvalidOperationException`，而 `ReadCsv` 对同样的类型
> **不抛**（返回空对象）—— 两侧不一致。

---

### 5.26 ⛔ `ScheduledDeletion`（**在用版**）八处行为

下游 2 处调用：`Packing Machine/…/Business/Flow/AutoProcess.cs:193`、`…/StartupFlow/05_初始化后台.cs:109`
（都在清 `GlobalData.Path_Image`）。

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **1** | 私有 `Log(string)` | ⛔ **空实现** —— 方法体只有一句被注释掉的 `Console.WriteLine` ⇒ 所有"记录日志后继续"的失败路径**完全静默**（连调试输出都没有） | `Log是空实现_调用它不产生任何输出` |
| **2** | ⛔ **只删得掉"产品 ID 子目录为空"的过期目录** | 子目录用 `item.Delete()`（`DirectoryInfo.Delete()`，**非递归**）删 ⇒ 子目录里只要有**任何文件**就抛 `IOException` → 被吞 → **日级目录根本没删掉**，方法却返回空列表。真实布局是 `…/年/月/日/产品ID/图片文件` ⇒ **这套清理在生产上基本不生效** | `DeleteExpiredFiles_产品ID子目录里有文件时_整个日级目录清不掉`、`DeleteExpiredFiles_产品ID子目录为空时_日级目录被整体删除` |
| **3** | 目录不存在 | 只 `Log` 一下就继续（`DirectoryInfo` 构造不校验存在性，`GetDirectories()` 抛的异常被 `GetAllDateFolders` 的 catch 吞掉）⇒ **返回空列表、不抛** | `DeleteExpiredFiles_目录不存在_返回空列表且不抛` |
| **4** | 删除单位 | 是**日级目录** `…/年/月/日`（其下的产品 ID 子目录属于同一次删除） | `DeleteExpiredFiles_删除单位是日级目录…` |
| **5** | `folderInfo.Date < expireDate.Date` | **严格小于** ⇒ 正好 `keepDays` 天前的目录**不删**，多一天才删 | `DeleteExpiredFiles_边界_正好keepDays天前不删_多一天才删` |
| **6** | `TryParseDateFromPath` | ⛔ 只看"**最后三段是否都是数字**" ⇒ `…/1/2/3` 被解析成公元 1 年 2 月 3 日（合法 `DateTime`）并被**当作过期目录删掉** —— 数字命名的目录树有被误删的风险。非法日期（`2025/13/45`）则 `catch` 后 `return false`、**只跳过不抛** | `DeleteExpiredFiles_最后三段都是数字的目录_被误判成日期并删除`、`DeleteExpiredFiles_非法日期目录_被跳过且不抛异常` |
| **7** | 空间不足分支的 `releasedMB` | ⛔ `GetDirectorySize() / (1024 * 1024)` 是**整数除法**，小于 1 MB 的目录一律算 0 MB ⇒ `releasedMB` 永远追不上 `needReleaseMB` ⇒ `break` **永不触发** ⇒ **把所有未过期目录全部删光** | `DeleteExpiredFiles_空间不足分支_releasedMB恒为0导致删光所有未过期目录` |
| **8** | `DeleteExpiredFiles1`（**零调用**） | 与 `DeleteExpiredFiles` 行为**不同**：① 没有 `Directory.Delete(dayFolder, true)` ⇒ 日级目录（已空）**留在磁盘上**；② `deletedFolderPaths.Add` 写在 `foreach` **内部** ⇒ 同一路径被**重复加入 N 次**；③ 日级目录下没有子目录时**什么都不做** | `DeleteExpiredFiles1_子目录为空时_只删子目录_日期目录本身留下`、`DeleteExpiredFiles1_日级目录没有子目录时_什么都不做` |

> 另外两处无缺陷但值得记录：`deletedCount` 在两个方法里都是**赋值后从不读取的死变量**；
> `DeleteExpiredFiles1` 的 `item.Delete()` 同样是非递归的（子目录非空时一个都删不掉）。

---

### 5.27 ⛔ `ScheduledDeletionEx`（**零引用版**）四处行为 —— 与在用版**策略相反**

⚠️ 类型是 `internal`，仓里**没有 `InternalsVisibleTo`** ⇒ 测试程序集**拿不到**，本节的用例全程走反射。
这本身就是一条记录：**这份实现外部根本调不到**。

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **1** | 目录不存在 | `throw new Exception("目录不存在: …")` —— 与在用版**相反** | `DeleteExpiredFiles_目录不存在_抛Exception_与在用版相反` |
| **2** | ⛔ 过期目录**删除成功** | **也抛异常**：过期分支里把日志语句写成了 `throw new Exception($"删除过期文件夹: …")`，紧接着又被自己的 `catch` 包成 `"删除文件夹失败: …"` ⇒ **只要真删掉一个目录，方法就抛异常**（而删除动作已经执行了） | `DeleteExpiredFiles_成功删除过期目录也会抛异常` |
| **3** | `TryParseDateFromPath` / `GetAllDateFolders` 的 `catch` | 都是 `throw` ⇒ 一个非法日期目录名（`2025/13/45`）就能让**整个方法挂掉**；异常还被 `GetAllDateFolders` **逐层再包**（形成 4 层嵌套消息） | `DeleteExpiredFiles_非法日期目录名_导致整个方法抛异常` |
| **4** | 返回值 `List<string>` | ⛔ **只可能是空列表** —— 过期分支抛、空间不足分支也抛 ⇒ 任何"删了东西"的路径都到不了 `return`，返回类型形同虚设 | `DeleteExpiredFiles_返回值只可能是空列表` |

> 隔离验证：`Extension.Core.Files.DateFolderInfo` 与 `Extension.Core.Files.ScheduledDeletionEx.DateFolderInfo`
> 在**同一程序集**内共存（同名不同命名空间，§6.H 的处置手段）→ 用例 `API面_两个DateFolderInfo同名不同命名空间_同程序集内共存`。

### 5.28 ⛔ `Core/Platform` + `Contracts` + `ThemeSettings` 十处行为（第四轮补测；2026-09-18 第 1 / 2 条按授权修复）

> ⚠️ 本表**原为"只记录 + 钉住"**，例外是**第 1 条**（`ProcessEx`，见 §7 序 27）与**第 2 条**
> （`SafeMemoryHandle`，见 §7 序 28）—— 均 2026-09-18 按用户授权修复。

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **1** | ✅ **已修（2026-09-18，序 27）** `ProcessEx.CloseProcess(null)` | **修前真的会去杀进程**。`Process.GetProcessesByName(null)` **既不抛异常、也不返回空数组**，而是返回一批**无关**进程 ⇒ 紧接着的 `process.Kill()` 就执行了，实测抛 `Win32Exception: 拒绝访问`（说明 Kill **确实被调用**，只是权限不够）。以管理员身份运行时后果是"杀掉一批不该杀的进程"。**修法**：入口加 `null` / 空串两道校验，**在碰进程之前**抛 —— 空串实测同样返回一批进程，所以两个都要挡。<br>⛔ **修后这条用例从"静态核对"升级成"真的执行"**：入口已先抛，`CloseProcess(null)` 不可能走到 `GetProcessesByName` ⇒ 可以安全地真调（这正是把"刻意不执行"换成"真执行"的判据） | `ProcessEx_传null或空串_在碰进程之前就抛异常`（原 `ProcessEx_没有参数校验_传null会真的去杀进程_故只做静态核对`，已随修复重写） + `ProcessEx_源码里必须保留参数校验`（静态护栏） |
| **2** | ✅ **已修（2026-09-18，序 28）** `SafeMemoryHandle.GetPointer()` | **修前**：释放后**不抛**，返回**悬垂指针**（实测等于释放前的地址 `1767847578464`，**不是** `IntPtr.Zero`）。根因：判据用 `SafeHandle.IsInvalid`（读句柄**值**是否为 0/-1），而 `Dispose()` 之后内部字段**并不清零** ⇒ 检查失效。后果：`Marshal.Read*`/`Write*` 变成 use-after-free —— 轻则读到垃圾数据，重则 `AccessViolationException` **直接崩进程**（catch 不住）。<br>**修法**：判据改成 `IsInvalid \|\| IsClosed`。⚠️ **为什么用 `\|\|` 而不是直接换成 `IsClosed`** —— `IsInvalid` 额外覆盖"从未分配成功"（`AllocHGlobal` 返回 0）这种**既未释放、也无有效指针**的情形，丢掉它会漏一档。<br>⚠️ **`ReleaseHandle()` 里的 `IsInvalid` 刻意不动**：那个方法 CLR 在释放时**至多调用一次**，执行到它时 `IsClosed` 必然还是 `false`；那里 `IsInvalid` 表达的语义是"当初就没分配成功 ⇒ 释放失败（返回 false）"，是对的。<br>✅ 全仓**零生产调用点**（只有测试 + `Vision/.../InviHelper.cs` 里一行注释掉的代码） | `SafeMemoryHandle_释放后取指针_抛ObjectDisposedException`（原 `SafeMemoryHandle_释放后取指针_不抛且返回悬垂指针`，断言的是**缺陷本身**，已随修复反转） |
| **3** | ⛔ `CpuMonitorEx.Dispose()` | 释放后**仍能读到真实值**（实测 9.94）—— `Dispose()` 既没把 `isInitialized` 置 false，也没把两个字段置 null。更隐蔽的后果：`NextValue()` 在内部句柄已释放时会**重新获取**一个 ⇒ Dispose 之后又分配了资源，等于**释放没生效** | `CpuMonitorEx_释放后读取_不抛且取值仍在0到100` |
| **4** | ⛔ `FpsEx.Tick()` | 达到计算间隔时**无条件**执行 `_stopwatch.Restart()`，而 `Restart()` 对**已 `Stop()`** 的秒表等价于"清零并 Start" ⇒ `Stop()` 被悄悄撤销，秒表自己又跑起来 | `FpsEx_Stop之后Tick到间隔_会把停掉的秒表重启` |
| **5** | `FpsEx.Tick()` | `elapsed == 0` 时**跳过**帧率更新，但计数**已经重置** ⇒ 高帧率场景（10 帧耗时不足 1 ms）下 `CurrentFps` 长期停在 0，且每 10 帧白算一次。测它必须用 `Sleep(3)` 保证 `elapsed > 0` | `FpsEx_达到计算间隔_算出帧率` |
| **6** | `FpsEx(int)` | 间隔 ≤ 0 时**静默回落**到 10 —— 行为本身合理，但调用方拿不到任何提示（`_calculationInterval` 被改成了 10） | `FpsEx_间隔参数非正数_回落到10` |
| **7** | ⛔ `KeyValue` | 名字是个"值对象"，却**没重写** `Equals`/`GetHashCode` ⇒ `Contains`/`Distinct`/`Dictionary` 全按**引用**走：内容相同的两份配置判不相等、`Distinct()` 去不掉重 | `KeyValue_没有重写Equals_内容相同也不相等` |
| **8** | ⛔ `ImageInfo`（struct + 可变 `List<>` 成员） | `var b = a;` 之后改 `b.ImagePath` **会改到 `a`** —— struct 是浅拷贝，引用成员被共享。这类 bug 从 `var b = a;` 这一行**完全看不出来**（§5.4 那个反模式的实例） | `ImageInfo_结构体里的引用成员_浅拷贝共享同一个List` |
| **9** | ✅ `DynamicDllLoaderEx` | **刻意不吞异常**：加载失败抛 `DllNotFoundException`（带 Win32 错误码）、取不到导出抛 `EntryPointNotFoundException`、释放后再取抛 `ObjectDisposedException`，并在 XML doc 里写明"若静默继续，后续会拿到无效句柄并在调用时崩溃，无法定位根因"。**与 §6.H 里两份 `ScheduledDeletion` 的做法形成正面对照** —— 这类"失败后对象不可用"的场景就该抛 | 5 条 `DynamicDllLoaderEx_*` 用例 |
| **10** | ✅ `CpuMonitorEx.IsCounterUnavailable` | **正面样板**：把"环境问题"（类别不存在 / 权限不足 / 平台不支持 / 计数器损坏）与"代码缺陷"分开 —— 前者降级返回 0f 并 `Debug.WriteLine` 留痕，后者继续抛。⚠️ 但 `ObjectDisposedException` 是 `InvalidOperationException` 的**子类**，所以"释放后读取"被划到了降级侧（见第 3 条） | `CpuMonitorEx_计数器不可用时的降级_返回0而不抛` |

> **本轮最重要的两条方法论**（都已写进技能）：
>
> 1. ⛔ **`Dispose()` 之后的对象状态，绝不能靠"应该会抛 `ObjectDisposedException`"来推断**。
>    本轮两条这样的断言（`SafeMemoryHandle` / `CpuMonitorEx`）**全部猜错** —— 实测一个是"返回悬垂指针"、
>    一个是"照常返回真实值"。**BCL 的释放语义不统一（`SafeHandle` / `Component` / `PerformanceCounter` 各一套），必须实测**。
> 2. ⛔ **不要为了验证"缺参数校验"而给会操作外部世界的 API 传非法参数**。
>    `ProcessEx.CloseProcess(null)` 差点真的去杀进程。缺校验只能用**静态核对**（读源码断言）来钉，
>    或者用**确定不存在**的合法入参。与 §5.19 里 `TaskEx.ThreadRun` 的处理原则一致。

### 5.29 ⛔ `Distribution/` 三十二处行为（第五轮补测）—— 发布形态的两块：`Security`(22) + `Protection`(10)

`Distribution/` 10 文件 / 1,148 行，**全部是 T0**（只 `using System.*`，但用 P/Invoke 碰 `kernel32`/`ntdll`）。
这一轮的最大特点是：**它是全仓唯一一处"安全语义靠约定、而约定没写下来"的地方** —— 同一个类里
fail-open / fail-closed 混用，文档措辞与实际方向相反。全部只记录 + 钉住
（**例外：本表第 1 条已于 2026-09-18 按授权修复，见 §7 序 32**）。

#### 5.29.1 `Security`（22 处）

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **1** | ✅ **已修（2026-09-18，序 32）** `LicenseManagerEx.Verify` + `LicenseInfo.Features` | **修前：签名完全合法的 License 也能让客户端抛 `NullReferenceException`**。链路：`Features` 的属性初始化器 `= new()` **只在 JSON 缺字段时生效**，厂商签发工具若把 `Features` 置 `null` 再序列化，payload 里就是 `"Features":null`；客户端反序列化回来仍是 `null`，而 `Verify` 查功能点时直接读 `info.Features.Count` ⇒ NRE **不在任何 catch 名单里**，逃出到调用方（拒绝服务）。**只在"要求功能点"的调用点暴露**。<br>**修法**：在**反序列化边界**做归一化 —— `info.Features ??= new(); info.Extra ??= new();`，语义对齐 `LicenseInfo.Features` 的文档契约「**空 = 全部功能**」⇒ 与"缺字段"完全一致。<br>⚠️ **修后是 fail-open（放行），不是 `FeatureDenied`** —— 归一化后 `Features.Count == 0`，功能点检查的 `Count > 0` 前置条件不成立 ⇒ 整段检查被跳过。敢 fail-open 的理由：payload **签过名**，攻击者没有私钥造不出 `"Features":null`；这里防的是**厂商签发工具的 bug**，不是攻击者。<br>⚠️ `Extra` 的归一化 **`Verify` 自己用不到**（它不读 `Extra`），价值在**保护调用方**（如 `LicenseTool/MainWindow.xaml.cs` 打印"扩展字段"时会 `.Count`） | `LicenseManagerEx_签名合法但Features为null_修后归一化成空集合按全功能放行`（原 `…查功能点时抛NullReferenceException`，已随修复重写）+ `LicenseInfo_JSON里的显式null会覆盖属性初始化器`（**测反序列化语义本身，不受修复影响，刻意保留原断言**） |
| **2** | ⛔ `LicenseManagerEx.clockSkew` | **方向与文档措辞相反**。实现把"当前时间"往**前**推：`now = UtcNow + skew; if (now > ExpireAtUtc) Expired;` ⇒ 实际过期时刻是 `ExpireAtUtc - 5min`，即**提前 5 分钟失效**（保守 / fail-closed）。而 XML 文档写"过期时间容差（防止临界抖动，默认 5 分钟）"，读起来像宽限期。后果：客户手里"还剩 4 分钟"的 License 被判"已过期" | `LicenseManagerEx_clockSkew是提前5分钟判过期_方向与文档的容差说法相反` |
| **3** | ⛔ `AesEx.AesGcmEncrypt` / `AesGcmDecrypt` | **空明文往返断裂**：加密空串产出 28 字节（`nonce12 + tag16 + 密文0`），而解密要求 `>= NonceSize + TagSize + 1` = **29** ⇒ **自己加密出来的东西自己解不开**（抛 `CryptographicException: 密文格式非法。`）。`AesEx_CBC_空串能往返` 证明 CBC 没这个问题 | `AesEx_GCM_空明文能加密_但解不开_往返断裂` |
| **4** | ⛔ `AesEx`（CBC 路径） | **没有完整性保护**：改 IV 一个字节 ⇒ 只有第一个明文块被搅乱、填充仍合法 ⇒ **不抛异常，返回乱码**。唯一的兜底是 PKCS7 填充校验，而错误口令解出的填充**约有 1/256 概率恰好合法**（那时也不抛）⇒ 断言必须写成"绝不会返回原文"，不能写成"一定抛" | `AesEx_CBC没有完整性保护_改一个字节不抛但明文变了` + `AesEx_CBC口令错误_绝不会返回原文` |
| **5** | `AesEx` 异常类型 | 同一个类里三种异常：① GCM 篡改抛 `AuthenticationTagMismatchException`（`CryptographicException` 的**子类**，用 `Assert.Throws<CryptographicException>` 会判失败 —— 精确类型匹配）；② 密文长度不足抛 `CryptographicException`；③ 非 Base64 输入抛 `FormatException` | `AesEx_GCM_密文或tag被改_抛AuthenticationTagMismatchException` 等 |
| **6** | ⛔ `AesEx.AesEncrypt(null)` | 返回 `string.Empty`（**不是密文**），而 `string.Empty` 拿去解密会因长度不足抛 ⇒ 语义不对称：调用方把 `null` 当"空内容"传，会在**解密侧**才炸 | `AesEx_CBC_明文为null时返回空串_而空串是解不开的` |
| **7** | ⛔ `HashEx.Sha256Hex(null)` / `Sha512Hex(null)` | 返回**空串**，不抛。三个重载（`string` ×2 + `byte[]`）都一样。空串是个"看起来像结果"的值 ⇒ 拿它去比较哈希会静默判等/判不等 | `HashEx_Sha256Hex_对null返回空串而不是抛异常` |
| **8** | ⛔ `RsaEx.RsaSign` / `RsaVerify` 的 null | `RsaSign(null)` == `RsaSign("")`（`(data ?? string.Empty)`），且 `RsaVerify(null, sigOfEmpty, pub)` 返回 **true** ⇒ null 与空串在签名语义上不可区分 | `RsaEx_null明文被静默当成空串签名` |
| **9** | ⛔ `RsaEx` 错误处理风格不统一 | `RsaVerify` 吞 `CryptographicException`/`FormatException`/`ArgumentException`/`ArgumentNullException` 返回 `false`（fail-closed，安全上正确）；而 `RsaEncrypt`/`RsaDecrypt`/`RsaSign` **一律抛**。同一个类里两种契约，调用方容易漏 catch | `RsaEx_验签失败一律返回false不抛_符合fail_closed` + `RsaEx_解密失败是抛异常_与验签的fail_closed风格不同` |
| **10** | ⛔ `RsaEx.FromPrivateKey` / `FromPublicKey` | **只认 PKCS#1 DER**（`ImportRSAPrivateKey` / `ImportRSAPublicKey`），**不认 PKCS#8 与 SubjectPublicKeyInfo** ⇒ 与 openssl 默认输出（`openssl genrsa` / `rsa -pubout`）不兼容，会抛 `CryptographicException`。另外非 Base64 抛 `FormatException`、`null` 抛 `ArgumentNullException`（都逃出，无兜底） | `RsaEx_只认PKCS1格式的DER_不认PKCS8和SPKI` + `RsaEx_导入非Base64密钥_抛FormatException或ArgumentNullException` |
| **11** | `RsaEx.RsaEncrypt` | 2048bit + OAEP-SHA256 的明文上限 **190 字节**（`256 - 2*32 - 2`），超了抛 `CryptographicException`（**不静默截断**，这是对的）。190 通过 / 191 抛 | `RsaEx_加密解密往返_且超过OAEP上限时抛异常而不是静默截断` |
| **12** | ⛔ `LicenseInfo.IsPermanent` | 判据是 `ExpireAtUtc >= DateTime.MaxValue.AddDays(-1)`，比文档"MaxValue = 永不过期"**宽松 1 天**：少一天仍算永久，退两天就不算。这个 1 天窗口是隐式的 | `LicenseInfo_IsPermanent的判据是距MaxValue不超过一天_比文档宽松` |
| **13** | ⛔ `LicenseManagerEx` 功能点语义 | **fail-open**：`Features` 为空列表时**任何功能都放行**（文档说"空 = 全部功能"）⇒ 签发时忘了填 Features，等于发了全功能授权。同理 `MachineCode` 为空 = 不绑定（也是 fail-open，但文档已写明） | `LicenseManagerEx_功能点为空列表时_任何功能都放行` + `LicenseManagerEx_机器码绑定_空机器码等于不绑定` |
| **14** | ⛔ `LicenseManagerEx.Verify(".")` | 判 **`InvalidSignature`** 而非 `Malformed`：`FromBase64Url("")` 对空串**不抛**（空 Base64 合法，解出 0 字节）⇒ 两个字段都"解码成功"，一路走到验签才失败。**"格式错误"这一档没有覆盖"字段为空"**，报错文案会误导排查方向 | `LicenseManagerEx_空payload加空签名被判签名无效_而不是格式错误` |
| **15** | `LicenseManagerEx.Create` / `CreateFile` | 私钥非法 / `privateKeyBase64 == null` 时**无兜底**，异常直接逃出（厂商工具侧，可接受，但要知道客户端侧 `Verify` 是有兜底的，两侧不一致） | 由 `RsaEx_*` 用例间接覆盖 |
| **16** | `MachineCodeEx.GetMachineCode` | 只取 SHA256 hex 的**前 16 个字符** ⇒ 有效区分度 **64 bit**。格式固定 `XXXX-XXXX-XXXX-XXXX`（19 字符），同机多次调用稳定 | `MachineCodeEx_机器码格式固定_同机多次调用稳定` |
| **17** | ✅ `MachineCodeEx` 降级 | 采集不到 MAC / 卷序列号时用 `NOMAC` / `NOSERIAL` 占位（保证机器码仍可生成），代价是**区分度下降**（大量机器可能机器码重复 ⇒ 授权误判"换机"）⇒ 两处降级**都配了 `Debug.WriteLine` 留痕**。**正面样板**：与 §5.28 第 10 条同风格 | `MachineCodeEx_采集失败时降级为占位符_但两处都必须留痕` |
| **18** | ⛔ `MachineCodeEx` 的 P/Invoke | 用 `DllImport("kernel32.dll")` 取卷序列号。**守卫扫的是"受监管的托管命名空间前缀"，`DllImport` 是字符串、不在扫描范围内** ⇒ 这不是漏网（P/Invoke 不引入托管依赖，与 T0 定义不冲突），但要知道 **T0 目录里的文件仍可能在非 Windows 上直接炸**（已用占位符兜住） | `MachineCodeEx_用PInvoke取卷序列号_守卫扫不到DllImport字符串_这不是漏网` |
| **19** | ✅ `HashEx.Pbkdf2` / `SecureRandom` | 默认 `iterations: 60000`、`keyLength: 32`；盐由调用方提供（不自己生成）。`SecureRandom` 走 `RandomNumberGenerator.Fill`（不是 `Random`）—— 这两处都正确 | `HashEx_Pbkdf2_长度可控且同输入稳定_换盐即换密钥` 等 |
| **20** | ✅ `AesEx` 密钥生命周期 | 四个方法全部在 `finally` 里 `CryptographicOperations.ZeroMemory(key)`；salt/nonce 用 `SecureRandom` 而非 `Random`；**不落静态字段**（每次现派生）—— 这是正确的做法 | `AesEx_*` 全部用例 |
| **21** | ⛔ `LicenseManagerEx` 不校验签发时间 | `IssuedAtUtc` 完全不参与校验 ⇒ 一份"签发时间在未来"的 License 照样有效（不是漏洞，但没有时间窗约束） | 由 `LicenseManagerEx_签发与校验往返_永久License` 间接覆盖 |
| **22** | `LicenseManagerEx` 消息文案 | 永久 License 的 `Message` 含"永久"；非永久含"剩余约 N 天"；`N` 用 `(int)TotalDays` **截断**（+30 天 → "剩余约 29 天"）。`daysLeft` 用**未加 skew** 的 `DateTime.UtcNow` 算，与过期判定用的 `now` 不同源 | `LicenseManagerEx_CreateTrial_绑定本机并打Trial标记` |

#### 5.29.2 `Protection`（10 处）

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **23** | ⛔⛔ `AppProtectionEx` 的默认动作 | `ProtectionOptions.Action` **默认就是 `ThreatAction.Throw`**（不是 `NotifyOnly`）；`ThreatAction.Exit` 走 `Environment.Exit(unchecked((int)0xDEAD0001))` ⇒ 一旦命中，**测试进程立刻死掉**，表现为"测试无输出、无汇总"，排查起来像随机崩溃。**这是本轮写测试的头号约束**：凡是会走到处置分支的用例一律显式用 `NotifyOnly` | `ProtectionOptions_默认值_默认就是抛异常且不轮询` + `AppProtectionEx_Exit动作会直接杀进程_测试里绝不能让它命中` |
| **24** | ⛔ `AppProtectionEx.CheckOnce` 完整性分支 | **fail-open**：`CheckIntegrity && !string.IsNullOrWhiteSpace(ExpectedEntryHash) && ...` 三段与短路 ⇒ **只开开关、不给哈希时既不检查也不报警**，"防护已开启"是假象 | `AppProtectionEx_开了完整性校验但没给期望哈希_会静默跳过` |
| **25** | ⛔ `AppProtectionEx` 的静态状态 | `_timer` 是**全进程单例**（`Protect` 会 `_timer?.Dispose()` 顶掉上一个）、`OnThreatDetected` 是**静态事件** ⇒ 多处调用/多处订阅会互相干扰，且**测试必须退订**（否则 handler 泄漏到下一个用例） | `AppProtectionEx_三项检查全关时Protect不抛且可反复启停` + `AppProtectionEx_完整性校验失败时NotifyOnly只触发事件不中断` |
| **26** | ✅ `AppProtectionEx` Timer 回调 | 回调里自己吞异常 + `Debug.WriteLine` 留痕 —— 注释写明"Timer 回调运行在线程池线程上，异常会直接终止进程"。**正面样板**（与 §5.28 第 10 条、本表第 17 条同风格） | `AppProtectionEx_Exit动作会直接杀进程_测试里绝不能让它命中` |
| **27** | ✅ **已修（2026-09-18，序 37）** `AntiDebugEx.IsTimingAnomaly` | **修前**：`thresholdMs = 0` 时 **恒返回 true**（`Sleep(100)` 后 `ElapsedMilliseconds > 0` 必然成立），**没有任何下限保护** ⇒ 调用方从配置读来 0（"未配置"）会立刻误报，且**无法与真的被调试区分**。默认 500ms 对 `Sleep(100)` 不会误报。<br>**修法**：`thresholdMs <= 0` 抛 `ArgumentOutOfRangeException` —— 与本表第 28 条注释写的"探测不可用才吞，其余异常继续抛出，避免掩盖真实缺陷"口径一致。<br>⚠️ **修后仍有一条同类隐患（已记录、未修）**：本方法固定 `Sleep(100)` ⇒ 阈值 **1..99** 同样恒真；本次只拦 ≤ 0 这个"未配置"哨兵值（拦 1..99 要引入"最小阈值"策略，属行为变更，需单独授权）。<br>⚠️ **零生产调用点**（全仓只有测试引用）⇒ 下游影响面为零 | `AntiDebugEx_计时陷阱的阈值非正数直接抛_不再恒真误报` + `AntiDebugEx_计时陷阱的阈值低于Sleep时长仍恒真_已记录未修`（原 `…传0就是恒真…`，已随修复重写） |
| **28** | ✅ `AntiDebugEx` 三探测点 | 三个 Win32/NT 探测各自 `try` + `IsProbeUnsupported(ex)` 白名单（`DllNotFoundException` / `EntryPointNotFoundException` / `Win32Exception` / `PlatformNotSupportedException` / `NotSupportedException`）+ **各一次 `Debug.WriteLine` 留痕**。注释写明"探测不可用不应让检测逻辑本身崩溃，否则会误杀正常环境" —— **正面样板** | `AntiDebugEx_三个Win32探测点都必须吞环境异常并留痕` |
| **29** | ✅ **已修（2026-09-18，序 36）** `AntiTamperEx` 两个校验方法对 `null` 的处理**已对齐** | **修前**：`VerifyEntryAssembly(null)` 有显式 `IsNullOrWhiteSpace` 短路 ⇒ 返回 `false`；而 `VerifyFile(path, null)` 直接 `expectedSha256Hex.Trim()`，抛出的 `NullReferenceException` **不在 `IsHashUnavailable` 名单里** ⇒ 逃出 catch，调用方拿到 NRE 而不是 `false`。同类里两个"校验"方法对同一种坏输入给出不同结果，很容易踩。<br>**修法**：`VerifyFile` 开头补 `if (string.IsNullOrWhiteSpace(expectedSha256Hex)) return false;`，与 `VerifyEntryAssembly` 同口径（fail-closed）。<br>⚠️ **行为变化仅限 `null`**：由"抛 NRE"变为"返回 `false`"；空串 / 空白串修前就走 `Trim()` 后比不相等、同样返回 `false`，结果不变。<br>⚠️ `path` 为 `null` 时 `File.Exists(null)` **本就返回 `false`**（不抛），不在本次范围、修前修后一致 | `AntiTamperEx_两个校验方法对null期望值的处理已对齐`（原 `…处理不一致`，已随修复重写）+ `AntiTamperEx_源码里VerifyFile必须保留期望值空值短路`（源码护栏，直指根因那一行） |
| **30** | `AntiTamperEx.IsIldasmSuppressed` | 用**字符串比特性全名**（`"System.Runtime.CompilerServices.SuppressIldasmAttribute"`）而不是引用类型 —— 因为该特性在 .NET Core 里未必存在，直接引用会编译失败。`Extension.dll` 本身没声明它（文档要求在**主程序**的 AssemblyInfo 里加） | `AntiTamperEx_ildasm抑制检查_按特性全名做字符串比较` |
| **31** | ⛔ `AntiToolProcessEx.GetSuspiciousProcesses()` | 把 `Process.GetProcessesByName` 返回的 `Process` 对象**原样返回、自身不 `Dispose`** ⇒ **句柄泄漏由调用方承担**（`IsSuspiciousToolRunning` 也没释放）。测试里必须自己补上那一步 | `AntiToolProcessEx_枚举是只读操作_返回的Process必须由调用方释放` |
| **32** | ⛔ `AntiToolProcessEx.SuspiciousProcessNames` | 实测是 **27 项**（不是 26），且**漏了 64 位版本号与改名后的工具**：`procexp64` / `dotpeek32` / `systeminformer`（Process Hacker 已于 2023 年改名 **System Informer**，进程名变成 `SystemInformer`）。黑名单必然滞后 —— 这是设计取舍，只记录 | `AntiToolProcessEx_名单是27项小写且不含exe后缀`（27 是"名单被改过"的哨兵） |

> **本轮最重要的三条方法论**（前两条已写进技能）：
>
> 1. ⛔ **同一个 API 面上，fail-open 与 fail-closed 可以并存 —— 必须逐个方法实测，不能按类推断。**
>    本轮 `RsaVerify` 吞异常返回 `false`，而 `RsaEncrypt`/`RsaDecrypt`/`RsaSign` 一律抛；
>    `VerifyEntryAssembly` 对 `null` 返回 `false`，而 `VerifyFile` 对 `null` 抛 NRE。
>    两次"按类推断"都猜错了。
> 2. ⛔ **XML 文档写的语义方向可能是反的**。`clockSkew` 文档说"容差（防止临界抖动）"，
>    实现是 `now = UtcNow + skew` ⇒ **提前**判过期。凡是"容差 / 宽限 / 兜底"这类词，
>    都要去读实现确认加在哪个方向，并且**把方向写进用例名**（本轮用例名就带了"提前5分钟"）。
> 3. ⛔ **属性初始化器挡不住 JSON 里的显式 `null`**。`public List<string> Features { get; set; } = new();`
>    看着很安全，但只要 payload 里出现 `"Features":null`，反序列化就会把它置成 `null`。
>    凡是"签名/校验"链路上的对象，都要在**反序列化之后**补一次空值兜底（而不是只依赖初始化器）。
>    这条对全仓所有走 JSON 的 DTO 都适用 —— 本轮只钉住了 `LicenseInfo` 这一个实例。

---

### 5.30 ⛔ `Patterns/` 六十九处行为 + `Ui/Controls/DataGridViewEx` 四处（第六轮补测；2026-09-18 增补 **序 72 / 序 73**，序 41 / 43 / 47 / 57 / 64 / 65 / 72 / 73 已修）—— T0 层最后一块

`Patterns/` 11 文件 / 5,835 行 + `Ui/Controls/DataGridViewEx.cs` 87 行，**全部 T0**。
（⚠️ 行数口径 = 全文声明的 **`splitlines`**；第六轮初稿误用了 `wc -l`，对末行无换行的文件少算 1，
已逐条修正 —— 与 §2 的 `5,835` / `Singleton 224` 现已一致。）
这是 T0 层**最后一块未覆盖区域**，也是"模式样板代码"最容易"看着对、其实没接线"的地方：
69 处里有 **14 处是纯死代码**（分支不可达 / 字段没人读 / 枚举值没人用 / Timer 从不触发），
而它们的注释都写得很像在正常工作。全部只记录 + 钉住，**未改任何产品代码**
（**例外：序 41 / 序 43 / 序 47 / 序 57 / 序 64 / 序 65 / 序 72 / 序 73 已于 2026-09-18 按授权修复，见 §7**）。

#### 5.30.1 `SingletonPattern` + `FactoryPattern`（3 文件 / 1,030 行）—— 15 处 + **序 73 增补 1 处 = 16 处**（其中 **序 47 / 64 / 65 / 73 已修**）

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **1** | ⛔⛔ `Singleton.GetInstance<T>()` vs `GetInstance<T>(factory)` | **同一个类型会得到两个互不相干的"单例"**。两条路径落在两个不同的静态持有者上：无参版走 `SingletonInstance<T>`（`Lazy<T>`），工厂版走 `SingletonInstanceWithFactory<T>`（双重检查锁）。各自稳定，但彼此完全不知道对方存在 | `Singleton_无参路径与工厂路径互不相干_同一个类型会得到两个单例` |
| **2** | ⛔ `Singleton` 的失败策略 | **同一 API 面上相反**：同步工厂版抛 `null` 之后**不记忆失败**（`_instance` 仍为 `null` ⇒ 下次再调工厂）；异步版 `_initializationFailed` **一旦置位永不复位** ⇒ 连工厂都不碰，直接抛"不会重试" | `Singleton_工厂抛null之后不记忆失败_下次仍会重试` + `Singleton_异步工厂失败后永不重试_抛不会重试` |
| **3** | ✅ **已修（2026-09-18，序 47）** `SingletonInstanceWithFactory<T>._instance` | **修前**：用**双重检查锁定**却**没有 `volatile`**；同一个文件里的 `ThreadSafeSingleton<T>` 却写了 `private static volatile T _instance;` —— 同一份实现里的两种写法。缺 `volatile` 时，锁外快路径（`GetInstance` 的第一个 `if`）没有获取/释放语义，可能读到"引用已非 null、但对象内部字段尚未发布"的**半初始化对象**。<br>**修法**：`private static T _instance;` → `private static volatile T _instance;`（`where T : class` ⇒ 引用类型，`volatile` 合法；只加关键字、**不改任何逻辑**）。<br>✅ **同文件还有一条同类隐患（序 73）—— 已于 2026-09-18 第五批授权后修复**：`SingletonInstanceAsync<T>` 的**锁外快路径**读 `_initialized` / `_instance`，而写入顺序是 `_instance = await factory()` → `_initialized = true`，两者**都不是 `volatile`** ⇒ 另一线程可能看到 `_initialized == true` 而 `_instance` 仍为 `null`。修法与序 47 同型：给 `_instance` / `_initialized` / `_initializationFailed` **三个字段都加 `volatile`**（**只改可见性、不改语义** —— `_initializationFailed` 一旦置位永不复位的既有行为**一行未动**），详见第 73 条。<br>⚠️ **改这条用例时踩到的坑**：原文用"全文 Contains 两条相反断言"，修后**会假通过** —— 同文件 `SingletonInstanceAsync<T>` 里那个非 volatile 的 `_instance` 正好能把 `Contains("private static T _instance;")` 喂饱。故改为**按内部类切段**核对，并把"异步版仍缺"钉成显式断言（⚠️ **该反向断言已在 序 73 修复时反转成正向护栏**，见第 73 条） | `Singleton_双重检查锁均已补volatile_工厂版与异步版`（原 `Singleton_工厂版的双重检查锁缺volatile_而ThreadSafeSingleton有` → 序 47 时改过一次 → **序 73 时又改成现名**） |
| **4** | `ThreadSafeSingleton<T>.ResetInstance()` | 全库**唯一**一个能重置单例的入口（注释写明"仅用于测试场景"）。`Lazy<T>` 那两条路径都**没有**重置手段 | `ThreadSafeSingleton_Instance唯一_ResetInstance之后是新实例` |
| **5** | ✅ `SingletonBase<T>` / `SingletonInstance<T>` | 用 `Lazy<T>(..., LazyThreadSafetyMode.ExecutionAndPublication)` —— 这是**正确**做法（`volatile` 那条问题的对照组） | `SingletonBase_Instance是懒加载且唯一` |
| **6** | ✅ **已修（2026-09-18，序 65）** `ConcreteProductB1/B2.Collaborate` | **修前**：直接取 `productA.Name`，对 `null` **零防御** ⇒ 裸 `NullReferenceException`（异常里既没有参数名、也没有"哪个参数错了"的信息，排查只能靠读源码反推）。<br>**修法**：补 `ArgumentNullException(nameof(productA), …)` 参数校验，与 `IAdvancedProduct` 系列的校验风格一致。⚠️ 两个类的 `Collaborate` 方法体**逐字相同**，必须**同时**改（漏一个就还是裸 NRE）。<br>✅ 全仓**零生产调用点**（只有测试调用） | `Products_Collaborate对null产品抛ArgumentNullException`（原 `Products_Collaborate对null产品抛NullReferenceException`，已随修复重写；并断言 `ParamName == "productA"`） |
| **7** | `Products.cs` | 6 个具体产品的 `Name` 是**中文常量**；`UseFeatureA/B`、`Collaborate` 一律 `void` + `Console.WriteLine`（无返回值、无副作用可断言） | `Products_六个具体产品的Name是固定常量` + `Products_全部行为方法返回void_只写控制台` |
| **8** | ⛔⛔ `CacheStrategy.Singleton` | **死枚举值**：`CreateProduct` 的"查缓存"与"写缓存"两处判断都只写 `!= CacheStrategy.None` ⇒ `Singleton` 与 `All` **行为完全一致**，这个枚举值从未被单独分支处理 | `AdvancedFactory_CacheStrategy_Singleton与All行为完全一致` |
| **9** | ✅ **已修（2026-09-18，序 64）** `AdvancedFactoryBase.CreateProduct` | **修前**：创建器**存在但返回 `null`** 时，不区分这种情况，统一抛 `KeyNotFoundException("…未找到ID为 'p' 的产品创建器")` ⇒ **报错信息与真实原因不符** —— 明明 `RegisterProduct` 注册过，却被告知"没注册"，排查会被带偏。<br>**修法**：在"创建器存在"分支内、`if (product != null)` **之后**单独抛 `InvalidOperationException($"工厂 '{FactoryName}' 中ID为 '{productId}' 的产品创建器返回了 null。")` ⇒ 与"创建器不存在"的 `KeyNotFoundException` **可区分**。<br>✅ 下游：生产调用点只有 `Extension/samples/` 里的示例，且都**不按异常类型 `catch`** ⇒ 零影响。<br>⚠️ XML doc 已补 `<exception cref="InvalidOperationException">` | `AdvancedFactory_创建器返回null时抛InvalidOperationException_与未注册可区分`（原 `…抛的是KeyNotFoundException_信息误导`，已随修复重写；并带一条**对照组**断言"真的没注册时仍是 `KeyNotFoundException`"） |
| **10** | ⛔ `FactoryBuilder.Build<T>()` | `_manager` 永远是 `new FactoryManager()` ⇒ `_manager as T` 只有 `T == FactoryManager` 时非 null，**任何派生类型都抛 `InvalidCastException`**。这个"泛型版本"实际不可用 | `FactoryBuilder_Build_T_只对FactoryManager本身成立` |
| **11** | ⛔ `FactoryBuilder.AddFactory(name, factory, strategy)` | 用**反射**写 `CacheStrategy`（该属性是 `protected set`）⇒ 实测**确实**绕过了可见性限制，值被真的写进去了 | `FactoryBuilder_AddFactory带策略_反射能写进protected_setter` |
| **12** | ⛔ `AdvancedProductBase.Dispose()` | 声明为 `public new void Dispose()`（**`new` 隐藏，不是 `override`**，因为基类的 `Dispose` 非虚）⇒ 同一个对象存在**两条 Dispose 入口**：`((IDisposable)p).Dispose()` 走基类实现，`p.Dispose()` 走派生类实现。两条共享同一个 `_disposed` 标志 ⇒ 合计只生效一次 | `AdvancedProductBase_两条Dispose入口共享同一个幂等标志` |
| **13** | ⛔ `ProductLifecycleBase` | `Initialize` 与 `Dispose` 各维护独立的 `Interlocked` 标志、**互不检查** ⇒ 已销毁的产品仍可被重新 `Initialize()`，且 `IsDisposed` 保持 `true` | `ProductLifecycle_Dispose之后仍可Initialize_两个标志互相独立` |
| **14** | `ProductLifecycleBase.IsInitialized` | 用 `Interlocked.CompareExchange(ref _initialized, 0, 0) == 1` 做"读"（RMW 读，功能正确但会在缓存行上造成无谓的写竞争） | 由 `ProductLifecycle_*` 用例间接覆盖 |
| **15** | `FactoryManager.RegisterFactory` vs `Mediator.Register` | **同一件事两种策略**：`FactoryManager` 用 `_factories[name] = factory` ⇒ **覆盖**；`Mediator.Register` 用 `TryAdd` ⇒ **静默忽略**（见 §5.30.3 第 1 条） | `FactoryManager_重复注册同名工厂是覆盖_不是去重` |
| **73** | ✅ **已修（2026-09-18，第五批打包授权「单点可见性修复」）** `SingletonInstanceAsync<T>._instance` / `_initialized` / `_initializationFailed` | **与第 3 条同型、但更靠前**：`GetInstanceAsync` 开头就是**锁外快路径** —— `if (_initialized) return _instance;` / `if (_initializationFailed) throw …`。写入顺序是 `_instance = await factory()` → `_initialized = true`，而三个字段**都不是 `volatile`** ⇒ 另一线程可能看到 `_initialized == true` **而 `_instance` 仍是 `null`**（拿到 null 单例）。<br>⚠️ 与第 3 条的区别：工厂版那个 `_instance` 至少**在锁内写、锁外读**；这里是**纯无锁读**，且 `_initialized` 与 `_instance` 是**两个独立字段**，没有 happens-before 边把它们绑起来。<br>✅ **修法**（与序 47 同型）：三个字段全部加 `volatile` —— `private static volatile T _instance;` / `private static volatile bool _initialized = false;` / `private static volatile bool _initializationFailed = false;`（`where T : class` ⇒ 引用类型，`volatile T` 合法）。`volatile` 写带 release、`volatile` 读带 acquire ⇒ 正好把「`_instance` 写 → `_initialized` 写」与「`_initialized` 读 → `_instance` 读」两对顺序钉住。<br>⚠️ **只改可见性、不改语义**：`_initializationFailed` 一旦置位**永不复位**（"失败后不重试"）的既有行为**一行未动**（第 2 条描述的行为**不变**）。<br>⛔ **当初"不敢顺手改"的理由经复核不成立**：当时判断"给 3 个字段加 `volatile` 会牵连 `_initializationFailed` 的语义" —— 实际上 `volatile` 只影响**可见性 / 重排序**，不影响"是否重试"的任何分支。**登记时的顾虑与修完的事实不同，两者都如实留在文档里**（原顾虑见 §7 待定表 序 73 行）。<br>✅ **下游影响面为零**：`Singleton.GetInstanceAsync<T>` 全仓**唯一**生产调用点是 `Extension/samples/DesignPattern.Samples/SingletonPattern/SingletonPatternExample.cs:109`（示例、单线程）⇒ 该竞态本就不可达。<br>用例：**改写 1 条**（把"异步版仍缺"的**反向护栏**反转成**正向护栏**），用例数不变 | `Singleton_双重检查锁均已补volatile_工厂版与异步版`（原 `Singleton_工厂版的双重检查锁已补volatile_异步版仍缺` —— 旧名本身是在**描述缺陷**，已随本次修复重命名） |

#### 5.30.2 `ObserverPattern`（2 文件 / 756 行）—— 9 处

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **16** | ⛔⛔ `Observer<T>.Update` + `Observable<T>.Data` | **两把不同的锁**。`Update` 写的是 `lock (observable)`（锁 `Observable<T>` **实例**），而 `Data` 的 getter/setter 锁的是它**内部的 `_lock` 字段**。⇒ 外层那把锁**不保护数据**：持有它的时候别的线程照样能读写 `Data` | `Observer_Update用lock_observable_但Data用内部锁_锁的不是同一对象` |
| **17** | ⛔ `Observer<T>.Update` 的实例锁**真的**会被拿住 | 上一条的反面：只要**挂了观察者**，持锁期间的任何 `Data` 赋值都会被 `Notify() → Update() → lock (observable)` 卡住 ⇒ 两条合起来证明"数据靠 `_lock`、`Update` 拿另一把锁"。**这也是本轮写这条用例时踩的坑**：第一版挂了观察者，结果被卡住的是 `Notify` 链路而不是 `Data` 本身，断言假失败 | `Observer_Update拿的是实例锁_持锁期间挂了观察者的赋值会被卡住` |
| **18** | ⛔ `Observer<T>.Update` 的类型分支 | `subject` 不是 `Observable<T>` 时只写一行控制台警告，**不抛、也不回调 `HandleUpdate`** ⇒ 静默丢数据 | `Observer_Update对不匹配的subject类型只写控制台_不抛也不回调` |
| **19** | ⛔ `ObservableBase` 的 Dispose 后策略 | 同一个类里两种：`Attach`/`Detach`/`ClearObservers`/`ContainsObserver` 抛 `ObjectDisposedException`，而 `Notify`/`NotifyAsync` **静默返回** | `ObservableBase_Dispose之后Attach抛但Notify静默返回` |
| **20** | ✅ `Observable<T>.UpdateData` | 在**锁外**调 `Notify()`（注释写明"避免死锁风险"）—— 正确做法 | `Observable_默认开启相等性检查_同值不通知` |
| **21** | `Observable<T>` 相等性检查 | 默认**开启**（`Observable() : this(true)`）：`null` ↔ 非 null 都算变化；引用类型走 `Equals` ⇒ 不同实例但值相等**不通知** | `Observable_null与非null互换都会通知` + `Observable_引用类型按Equals比较` |
| **22** | ⛔ `AdvancedObservable<T>.Publish` | 只 `new` **一个** `EventArgs<T>` 实例给**所有**订阅者共享 ⇒ 任一订阅者把 `e` 存起来，其余订阅者拿到的是同一个对象 | `AdvancedObservable_所有订阅者共享同一个EventArgs实例` |
| **23** | ⛔ `EventArgs<T>` | 与 BCL 的 `System.EventArgs` **同名**（§5.7）；继承链是 `EventArgs<T>` → `EventArgsBase` → `System.EventArgs` | `EventArgs_T_继承链挂到BCL的System_EventArgs上` |
| **24** | `DelegateObserver<T>.OnEvent` / `ObservableBase.Notify` | ① `OnEvent` 用 `_handler?.Invoke`（`?.` 多余，构造函数已保证非 null）；② `Notify` 一个观察者抛异常不影响其他（吞掉 + 只 `Console.WriteLine` 消息与堆栈） | `AdvancedObservable_一个订阅者抛异常不影响其他订阅者` |

#### 5.30.3 `MediatorPattern`（2 文件 / 1,366 行）—— 16 处（其中 **序 43 已修**；**第 33 条已于 2026-09-18 补语义注释，序 60**）

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **25** | ⛔⛔ `Mediator.Register` / `AdvancedMediator.Register` | `TryAdd` 去重，但 `colleague.SetMediator(this)` 在 `TryAdd` **之前**执行 ⇒ 同 Id 第二次注册**静默忽略**，而新对象**已经持有 mediator** ⇒ 结果是"**能发不能收**"：`second.Send(...)` 能发出并投给 `first`，但 `second` 永远收不到消息 | `Mediator_重复Id注册被静默忽略_但新对象的SetMediator已生效` |
| **26** | ⛔⛔ `DelegateColleague` 覆写 `Receive`/`ReceiveAsync` | 基类 `ColleagueBase.ReceiveAsync` **检查 `_disposed`**，而 `DelegateColleague` 覆写后**丢掉了这个检查** ⇒ `Dispose()` 之后：`Send`/`Broadcast`/`SetMediator` 抛 `ObjectDisposedException`，而 `Receive`/`ReceiveAsync` **照常工作** | `ColleagueBase_Dispose之后Send抛但DelegateColleague的Receive不抛` + `ColleagueBase_未覆写ReceiveAsync时Dispose后会抛` |
| **27** | ⛔ `Mediator.BroadcastAsync` / `AdvancedMediator.BroadcastAsync` | `try/catch` **只包住了 `tasks.Add(...)` 那一行**，而真正会抛的 `await Task.WhenAll(tasks)` 在 `foreach` **外面** ⇒ 接收者的异常会原样抛给调用方，**与注释"记录异常但不中断其他同事的处理"相反**（`ColleagueBase.ReceiveAsync` 是 `async` 方法，同步段异常也会进 Task ⇒ `tasks.Add` 那行其实永不抛，catch 是死代码） | `Mediator_BroadcastAsync_接收者抛异常时会向外抛_与注释相反` + `AdvancedMediator_BroadcastAsync_接收者抛异常会向外抛` |
| **28** | ⛔ `Mediator` / `AdvancedMediator` 的 Dispose 后策略 | 同一个类里两种：`Register`/`Unregister`/`ClearColleagues`（Advanced 还有 `RegisterWithFilter`）抛 `ObjectDisposedException`；而 `Send`/`SendAsync`/`Broadcast`/`BroadcastAsync`（Advanced 还有两个 `*WithPriority*`）**静默返回** | `Mediator_Dispose之后Send静默返回但Register抛` + `AdvancedMediator_Dispose之后Send静默返回但Register抛` |
| **29** | `ColleagueBase` 的四个发送方法 | `Send`/`Broadcast`/`SendAsync`/`BroadcastAsync` 一律 `_mediator?.` 空条件 ⇒ **没注册到 mediator 时静默丢弃**（不抛、不记日志） | 由 `Mediator_*` 用例间接覆盖 |
| **30** | ✅ `Mediator.Broadcast` | 用 `colleague.Id == from` 跳过发送者自己；用 `_lock` 做 `Values.ToArray()` 快照（注释写明"避免在迭代时修改集合"）—— 正确做法 | `Mediator_Broadcast会跳过发送者自己` |
| **31** | ⛔⛔ `FilteredColleague.Receive` | 条件是 `message is PriorityMessage pm && _filter.ShouldProcess(pm)` ⇒ **不是 `PriorityMessage` 的消息整条 `if` 直接为 false**，无论过滤器怎么写都会被**静默丢弃**（`ReceiveAsync` 同理） | `FilteredColleague_会丢弃所有非PriorityMessage消息` + `FilteredColleague_的丢弃语义来自is_PriorityMessage这个形态` |
| **32** | ✅ **已修（2026-09-18，序 43）** `FilteredColleague.Dispose` | **修前**：`if (_innerColleague is IDisposable disposable) disposable.Dispose();` —— 但 **`IColleague` 不继承 `IDisposable`**，且仓内两个实现（`ColleagueBase:125` / `DelegateColleague:537`）也**都没声明**它 ⇒ 任何"只实现了 `IColleague`"的对象都不满足这个 `is` ⇒ **该分支是死代码**，内层的 `Dispose` 永远不会被调用（包装器"释放"了、内层还活着）。<br>✅ **前提经硬证据复核**：全仓 `grep IDisposable` 在 `MediatorPattern.cs` 里**只命中 `IMediator : IDisposable`（`:13`）** —— 同事体系一个都没声明，死分支成立。<br>**修法（方案②，仓内闭环）**：补 `else if (_innerColleague is ColleagueBase colleagueBase) colleagueBase.Dispose();`，**不动** `IColleague` 的公开契约（让接口继承 `IDisposable` 属破坏性变更：全仓所有实现类都要补成员）。⚠️ 用 `else if` 而非独立 `if` ⇒ 将来若出现"既是 `IDisposable` 又派生自 `ColleagueBase`"的类型也只释放一次 | `FilteredColleague_Dispose会连带Dispose内层`（原 `…_Dispose不会连带Dispose内层_因为IColleague不继承IDisposable`，断言的是**缺陷本身**，已随修复反转） |
| **33** | ✅ **已补语义注释（2026-09-18，序 60；行为未改）** ⛔⛔ `TypeFilter` + `PriorityMessage.MessageType` | `TypeFilter` 对空 `MessageType` 的规则是"白名单为空 **或** 含空串才放行"；而 `PriorityMessage` 的 `messageType` **默认 `null`**，`SendWithPriority`/`BroadcastWithPriority` **不传这个参数** ⇒ 凡走这两个入口的消息 `MessageType` 永远是空 ⇒ **非空白名单 + 这两个入口 = 消息全被丢** | `TypeFilter_非空白名单会丢弃MessageType为空的消息` + `AdvancedMediator_SendWithPriority会把消息包装成PriorityMessage` |
| **34** | ⛔ `AdvancedMediator.GetColleaguePriority` | 签名是 `(IColleague colleague, object message)`，但**实现只读 `message`、完全忽略 `colleague`** ⇒ 所有同事拿到同一个排序 key，`OrderBy` 退化成**恒等排序**，真实投递顺序取决于 `ConcurrentDictionary.Values` 的枚举顺序（不可预测）。注释却写着"按优先级排序处理同事" | `AdvancedMediator_广播不保证投递顺序_只保证都收到` |
| **35** | ⛔ `MessageRouter`（非泛型） | ① `_mediators` 是**普通 `Dictionary`**（非线程安全）而 `RegisterChannel` **无锁**；② `RouteAndSend` 在通道名称为空/未注册时**静默丢弃**；③ `Dispose` 会**连带 Dispose 所有 mediator** | `MessageRouter_通道未注册或路由结果为空时静默丢弃` + `MessageRouter_Dispose会Dispose所有中介者` |
| **36** | `AdvancedMediator.RegisterWithFilter` | 创建 `FilteredColleague` 后交给 `Register` ⇒ 注册表里存的是**包装器**，`Id` 沿用内层的 ⇒ 外部仍按内层 Id 寻址（行为正确，但"谁在表里"容易被误解） | `AdvancedMediator_RegisterWithFilter会包成FilteredColleague并沿用内层Id` |
| **37** | `CompositeFilter`（非泛型） | 用 `Enumerable.All` ⇒ **空过滤器列表 = 全通过**（`All` 对空集合返回 `true`） | `CompositeFilter_空过滤器列表一律通过` |
| **38** | `PriorityMessage`（非泛型） | `CreationTime = DateTime.Now`（**本地时间**，非 UTC）；`Data`/`Priority`/`MessageType` 只读 | 由 `AdvancedMediator_*` 用例间接覆盖 |
| **39** | `AdvancedMediator` 的参数校验 | `RegisterRange` 跳过 `null` 元素；`Register` 对 `colleague.Id` 为空抛的是 **`ArgumentException`**（不是 `ArgumentNullException`）—— 这两个类型在 `Assert.Throws<T>` 下**不可互换** | `AdvancedMediator_Register与RegisterWithFilter的参数校验` |
| **40** | ⛔ `FilteredColleague` 的发送路径 | `Send`/`SendAsync`/`Broadcast`/`BroadcastAsync` 都经 `_mediator?.` ⇒ 若没被 `Register`（因而没 `SetMediator`），这些方法**静默无操作** | `FilteredColleague_Dispose之后自身全部入口抛ObjectDisposedException` |

#### 5.30.4 `ProducerConsumerPattern`（2 文件 / 1,387 行）—— 11 处 + **序 72 增补 1 处 = 12 处**（其中 **序 41 / 序 72 已修**）

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **41** | ✅ **已修（2026-09-18，序 41）** `ProducerConsumer<T>.Stop()` + `StartAsync` | **修前**：`Stop()` 做两件事：`_running = false` + `_writer.Complete()`（channel **永久关闭**）。但 `StartAsync` 第一件事就是把 `_running` 置回 `true` —— 它**不知道 channel 已经关了**。于是生产者 `WriteAsync` 抛 `ChannelClosedException`（被吞）→ 继续循环；消费者 `WaitToReadAsync` 在已完成的 channel 上**立即返回 false** → 外层 `while` 看到 `_running` 仍是 `true` → 继续循环 ⇒ **两个任务同时忙循环**，`Task.WhenAll` 永不返回，只能靠取消令牌逃出来。<br>**修后**：`Stop()` 是**终态**（新增 `_stopped` 标志），`StartAsync` 一进门就抛 —— `_disposed` ⇒ `ObjectDisposedException`；`_stopped` ⇒ `InvalidOperationException`。**不再忙循环**，且失败是**立刻**的 | `ProducerConsumer_Stop之后StartAsync抛InvalidOperationException_不再忙循环挂起`（原 `…无法恢复_生产被静默吞掉`，已随修复重写） + `ProducerConsumer_Dispose之后StartAsync抛ObjectDisposedException` |
| **42** | ⛔ `EnhancedProducerConsumer<T>` 的 Dispose 后策略 | 同一个类里两种：`Clear()` 检查 `_disposed` 抛 `ObjectDisposedException`；`TryProduceItem`/`TryConsumeItem` 检查 `_running` 返回 `false` | `EnhancedProducerConsumer_Clear在Dispose后抛但TryProduceItem返回false` |
| **43** | ⛔ 两个类的 Stop 后写入行为**相反** | `EnhancedProducerConsumer.ProduceItemAsync`/`ConsumeItemAsync` 在 `!_running` 时**抛** `InvalidOperationException`；而 `ConfigurableProducerConsumer.ProduceAsync` **不检查状态**，只靠 `while (_running)` ⇒ **安静返回、什么都不做** | `EnhancedProducerConsumer_Stop之后写入抛读取抛而Try版本返回false` + `ConfigurableProducerConsumer_Stop之后ProduceAsync安静返回不抛` |
| **44** | ⛔ `ProducerConsumer<T>.StartAsync` 里 `Task.Run` 的 catch | 写的是 `catch (Exception ex) { Console.WriteLine(...); throw; }`，但 `ProduceAsync` 自己**吞掉所有非取消异常** ⇒ `await ProduceAsync(...)` 永不抛 ⇒ **这个 `throw` 是死代码**（`EnhancedProducerConsumer` 的同位置甚至没有 `throw`） | 由 `ProducerConsumer_能真的生产并累计计数` 间接覆盖 |
| **45** | ⛔ `StartAsync` 的计数器重置 | `_producedCount = 0` 是**非原子写**，而自增走 `Interlocked.Increment` ⇒ 混用。注意 `Stop()` 本身**不**重置计数，只有 `StartAsync` 会 | `ProducerConsumer_Stop不重置计数器` |
| **46** | `ProducerConsumer<T>.Stop()` | 只在 `!_disposed && _running` 时动作 ⇒ **幂等**；`Dispose()` 之后 `Stop()` 是空操作（`!_disposed` 为 false） | `ProducerConsumer_Stop是幂等的` + `ProducerConsumer_Dispose之后Stop是空操作` |
| **47** | ⛔⛔ `PriorityQueueProducerConsumer<T>.StartAsync` | 接口 `IProducerConsumer<T>` 声明了它，实现体是 `throw new NotImplementedException("…请使用StartConsumersAsync方法")`。而且它**不是 `async` 方法**（无 `AsyncStateMachine` 标记）⇒ 异常是**同步抛出**的，调用方用 `await` 包不住，必须直接 `try/catch`。写这条用例时也不能用 `Assert.Throws<T>(Func<Task>)`（那会选到过时的异步重载，语义相反） | `PriorityQueueProducerConsumer_StartAsync必抛NotImplementedException且是同步抛` |
| **48** | ⛔ `ConfigurableProducerConsumer<T>.ConsumeWithBatchingAsync` 的 Timer | 创建了 `var batchTimer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);` —— **从头到尾没使用过**（只在 `finally` 里 `Dispose`）。批处理的超时其实是靠 `Task.Delay(BatchTimeoutMs)` + `Task.WhenAny` 实现的 ⇒ **纯死代码** | `ConfigurableProducerConsumer_批处理里的Timer创建后从未使用` |
| **49** | ⛔ `ConsumeWithBatchingAsync` 的 `Task.WhenAny` | 每轮循环都新建 `Task.Delay(BatchTimeoutMs, token)`，`WhenAny` 之后**输掉的那个 Task 不被取消**（`waitTask` 长期挂着）⇒ 高频轮询下 Task 堆积 | 由 `ConfigurableProducerConsumer_批处理_批次满时会成批处理` 间接覆盖 |
| **50** | ⛔ `TryDequeueHighestPriorityItem` | 实现是"**全取出 → 排序 → 取第 0 个 → 其余重入队 → `_semaphore.Release(items.Count - 1)`**"，**非原子** ⇒ 并发消费者会互相看到"空队列"并各自重排一遍 | 由 `PriorityQueueProducerConsumer_按优先级从高到低消费` 间接覆盖 |
| **51** | `PriorityItem<T>.CompareTo` / `ProducerConsumerConfig` | ① `CompareTo` 是**反向比较**（`other.Priority.CompareTo(Priority)`）⇒ 优先级数字**大的排前面**，同优先级按 `CreationTime` **先进先出**；② `Config` 默认值：`Capacity 100` / `FullMode.Wait` / `BatchSize 1` / `BatchTimeoutMs 1000` / 两个开关 `false` | `PriorityItem_优先级数字大的排前面` + `PriorityItem_同优先级按创建时间先进先出` + `ProducerConsumerConfig_默认值` |
| **72** | ✅ **已修（2026-09-18，序 72）** 同型缺陷：`EnhancedProducerConsumer<T>` 与 `ConfigurableProducerConsumer<T>` | 与 **序 41** 逐字同型 —— `Stop()` 同样 `_writer.Complete()`、`StartAsync` 同样无条件把 `_running` 置回 `true`、两个循环同样 `while (_running)` 且 `WaitToReadAsync` 在已完成的 channel 上**立即返回 false** ⇒ **同样是两个任务一起忙循环、`Task.WhenAll` 永不返回**。<br>**修法照抄序 41**：两个类各加 `private volatile bool _stopped = false;` + `StartAsync` 两道闸门（`_disposed` ⇒ `ObjectDisposedException`；`_stopped` ⇒ `InvalidOperationException`）+ `Stop()` 补"终态"`<remarks>`。<br>⚠️ **为什么不能"重建 channel 让它可以重启"**：同序 41 —— `_running` 是共享标志，`Stop()` 后旧任务要等下一次 `while` 检查才退出（**有窗口期**）；此时重建 channel 并置回 `_running = true` 会让**旧任务"复活"并共用新 channel** ⇒ 生产者/消费者各变 2 份（静默重复，比抛异常更糟）。<br>⛔ **`PriorityQueueProducerConsumer<T>` 未碰** —— 它没有这条缺陷（`StartAsync` 本来就是必抛 `NotImplementedException`，见第 47 条） | `EnhancedProducerConsumer_Stop之后StartAsync抛InvalidOperationException` + `ConfigurableProducerConsumer_Stop之后StartAsync抛InvalidOperationException`（原 `…仍是忙循环_同型缺陷未修`，已随修复重写）+ 两条配套 `…_Dispose之后StartAsync抛ObjectDisposedException` |

#### 5.30.5 `PublishSubscribePattern`（2 文件 / 1,296 行）—— 16 处（其中 **序 43 / 序 57 / 序 60 已修**）

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **52** | ⛔⛔ `SubscriberBase<T>._disposed` | **死字段**：声明了 `_disposed`、`Dispose(bool)` 里也置了位，但 `HandleMessage` 是 `abstract`、基类**没有任何一处读它** ⇒ `Dispose()` 之后 `HandleMessage` 照样能被调用。对照组：`DelegateSubscriber<T>` 自己维护并**真的**检查 `_disposed`（`Dispose` 后抛 `ObjectDisposedException`）⇒ **两个都实现 `ISubscriber<T>` 的类，`Dispose` 后行为相反** | `SubscriberBase_Dispose之后HandleMessage仍可调用_没有_disposed检查` + `DelegateSubscriber_Dispose之后HandleMessage抛ObjectDisposedException` |
| **53** | ⛔ `MessageBroker<T>` 的 Dispose 后策略 | 同一个类里两种：`Subscribe`/`Unsubscribe`/`ClearSubscribers` 抛 `ObjectDisposedException`；`Publish`/`PublishAsync` **静默返回**（与 §5.30.3 第 28 条同型） | `MessageBroker_Dispose之后Publish静默返回但Subscribe抛` |
| **54** | `MessageBroker<T>.Subscribe` / `SubscribeRange` | 按实例**去重**（同一订阅者订两次 ⇒ `SubscriberCount` 仍是 1）；`SubscribeRange`/`UnsubscribeRange` **跳过 `null` 元素** | `MessageBroker_Subscribe去重_SubscriberCount` + `MessageBroker_批量订阅与取消会跳过null` |
| **55** | ✅ `MessageBroker<T>.Publish` | 一个订阅者抛异常**不影响其他订阅者**（吞掉）—— 正确做法，与 §5.30.2 第 24 条同型 | `MessageBroker_一个订阅者抛异常不影响其他订阅者` |
| **56** | ⛔⛔ `AdvancedMessageBroker<T>` 的两套名单 | 维护 `_allSubscribers`（`Subscribe` 写入）与 `_topicSubscribers`（`SubscribeToTopic` 写入）**两套互不相通的名单**：`Publish` 只查前者、`PublishToTopic` 只查后者 ⇒ **两类订阅者永远收不到对方那条路的消息**（双向隔离） | `AdvancedMessageBroker_主题订阅者不会收到Publish` + `AdvancedMessageBroker_PublishToTopic只发给该主题的订阅者` |
| **57** | ⛔ `AdvancedMessageBroker<T>.SubscriberCount` | 只返回 `_allSubscribers.Count`，**主题订阅者完全不计入** ⇒ 只做主题订阅时它**恒为 0** | `AdvancedMessageBroker_SubscriberCount只算全局订阅者` + `Advanced_AdvancedMessageBroker_SubscriberCount只算全局订阅者` |
| **58** | `AdvancedMessageBroker<T>` 的取消语义 | `Unsubscribe` 会**同时从所有主题移除**该订阅者；`UnsubscribeFromTopic` 只影响指定主题；`ClearSubscribers` **连主题名单一起清空** | `AdvancedMessageBroker_Unsubscribe会同时从所有主题移除` + `AdvancedMessageBroker_UnsubscribeFromTopic只影响该主题` + `AdvancedMessageBroker_ClearSubscribers会连主题名单一起清空` |
| **59** | ✅ **已补语义注释（2026-09-18，序 60；行为未改）** ⛔ `TopicFilter<T>` 的空 topic 语义 | 规则是"**白名单为空 或 含空串**才放行"⇒ 空 topic 既不是"全通过"也不是"全拦"，而是**只有 `MessageType` 为空的才过**。注意 `PriorityMessage<T>` 的 `messageType` **默认 `null`** ⇒ 与 §5.30.3 第 33 条是同一个坑 | `TopicFilter_空Topic的语义是空白名单才放行` |
| **60** | ✅ **已补语义注释（2026-09-18，序 60；行为未改）** ⛔ `PriorityFilter<T>` | 是"**最低优先级**"语义（`>=` 阈值才通过），不是精确匹配 ⇒ 传 `High` 时 `Critical` 也会通过 | `PriorityFilter_T_按最低优先级过滤` |
| **61** | `CompositeFilter<T>`（Advanced） | 用 `Enumerable.All` ⇒ **空过滤器列表 = 全通过**（与 §5.30.3 第 37 条同型）。⚠️ 组合 `TopicFilter<T>`/`PriorityFilter<T>` 时 `CompositeFilter` 的实参必须是 `PriorityMessage<T>` —— 这两个过滤器本身实现的是 `IMessageFilter<PriorityMessage<T>>`，写 `CompositeFilter<string>` 会 `CS0266`/`CS1503` | `CompositeFilter_T_空列表一律通过` + `CompositeFilter_T_必须全部通过才通过` |
| **62** | ✅ **已修（2026-09-18，序 43）** `FilteredSubscriber<T>.Dispose` | **修前**：与 `FilteredColleague` **完全同型** —— `if (_innerSubscriber is IDisposable disposable) disposable.Dispose();`，但 `ISubscriber<T>`（`:13`）**不继承 `IDisposable`**，仓内两个实现（`SubscriberBase<T>:87` / `DelegateSubscriber<T>:138`）也都没声明 ⇒ **分支不可达**，内层订阅者的 `Dispose` **永远不会被调用**。<br>**修法（方案②，仓内闭环）**：补两个 `else if` 分支按具体类型释放。<br>⛔ **关键坑：两个具体类型是"平行"的** —— `DelegateSubscriber<T>` **不**派生自 `SubscriberBase<T>`（两者都直接实现 `ISubscriber<T>`）⇒ **只补基类那个分支会漏掉它**。故新增一条用例**专门钉住 `DelegateSubscriber<T>` 这一路**（它不像 `SubscriberBase<T>._disposed` 那样是死字段，可用 `Dispose` 后 `HandleMessage` 抛 `ObjectDisposedException` 做**可观测证据**） | `FilteredSubscriber_Dispose会连带Dispose内层`（原 `…_Dispose不会连带Dispose内层_因为ISubscriber不继承IDisposable`，已反转）+ **新增** `FilteredSubscriber_Dispose会连带Dispose内层的DelegateSubscriber` |
| **63** | ✅ **已修（2026-09-18，序 57）** `FilteredSubscriber<T>.Name` | **修前**：只在"内层是 `SubscriberBase<T>`"时转发真名，**否则一律返回固定字面量 `"FilteredSubscriber"`** ⇒ 多个包装器名字全一样，日志里分不出谁是谁。<br>⛔ **另一个方向也不对**（原描述没提）：内层**是** `SubscriberBase<T>` 时返回的是内层**裸名字**，与内层本身**完全同名** ⇒ 反而看不出"这里套了一层包装"。<br>**修法**：统一成 `$"Filtered({内层名})"` —— 内层是 `SubscriberBase<T>` / `DelegateSubscriber<T>` 时分别取各自的 `Name`，两者都匹配不上时退回**运行时类型名**。<br>⚠️ **不能写成 `_inner?.Name`**：`ISubscriber<T>` 上**没有** `Name` 成员（见第 52 条那一族的接口设计）⇒ 只能按具体类型取。<br>✅ 下游：`Name` 在生产代码里**零调用点**（全仓 `Patterns/` 内唯一的 `.Name` 就是这行定义本身），只有测试断言它 | `FilteredSubscriber_Name会带上Filtered前缀_内层两种实现都能分辨`（原 `…_Name对内层非SubscriberBase返回固定字面量`，已随修复重写） |
| **64** | ⛔⛔ `Advanced.AdvancedMessageBroker<T>.GetSubscriberPriority` | 签名是 `(ISubscriber<PriorityMessage<T>> subscriber, PriorityMessage<T> message)`，实现只有 `return -(int)message.Priority;` —— **完全忽略 `subscriber`** ⇒ 所有元素同一个 key，`OrderBy` 退化成**恒等排序**，真实投递顺序取决于 `ConcurrentBag` 的枚举顺序（不可预测）。与 §5.30.3 第 34 条同型 | `Advanced_AdvancedMessageBroker_投递顺序不可由优先级决定` + `Advanced_AdvancedMessageBroker_的排序键与订阅者无关`（源码文本） |
| **65** | ⛔ `MessageRouter<T>` | ① 通道名称为空 / 通道未注册 ⇒ **静默丢弃**；② `RegisterChannel` 同名**覆盖**（后注册的赢）；③ `Dispose` **连带 Dispose 所有 broker**，之后 `RegisterChannel` 抛 `ObjectDisposedException` 而 `RouteAndPublish`/`RouteAndPublishAsync` **静默返回** | `MessageRouter_T_通道未注册或路由结果为空时静默丢弃` + `MessageRouter_T_参数校验与重复通道覆盖` + `MessageRouter_T_Dispose会Dispose所有broker` + `MessageRouter_T_Dispose之后路由静默返回` |
| **66** | ⛔⛔ **两个同名 `AdvancedMessageBroker<T>`**（§5.6 实证） | `Extension.Patterns.PublishSubscribePattern.AdvancedMessageBroker<T>` 与 `…PublishSubscribePattern.Advanced.AdvancedMessageBroker<T>`：`Name` 都是 `AdvancedMessageBroker`1`，**只有 `Namespace`/`FullName` 不同** ⇒ 同时 `using` 两个命名空间写裸名就是 **CS0104 二义性**，测试里必须用别名（`using PSS = …; using PSSA = …;`）。⚠️ 两者的**消息类型也不同**：基础版吃 `T`，Advanced 版吃 `PriorityMessage<T>` | `两个同名AdvancedMessageBroker只能靠命名空间区分` |
| **67** | ⛔ `MessagePriority` 枚举 | 在 `MediatorPattern.Advanced` 与 `PublishSubscribePattern.Advanced` 各有一份**同名同元数**的副本（同 §5.6）；取值 `Low=0` / `Normal=1` / `High=2` / `Critical=3` | `两个同名MessagePriority枚举都还在` |

#### 5.30.6 `Ui/Controls/DataGridViewEx.cs`（1 文件 / 87 行）—— 4 处

87 行里 **73 行是注释 + 10 行空行**，有效代码只有 `using System.Reflection;` + `namespace Extension.Ui.Controls { }`（**零类型**）。

| # | 位置 | 实测行为 | 钉住它的用例 |
|---|---|---|---|
| **68** | ⛔ 文件名 | **文件名承诺了一个不存在的类型**：整个 `Extension.Ui.Controls` 命名空间里**没有任何 `DataGridView*` 类型**（同目录另外 8 个文件各自都贡献了真实类型，只有它贡献 0） | `Ui_Controls命名空间里没有DataGridView相关类型` |
| **69** | ⛔ 有效代码 | 去掉注释行后**只剩 4 行**：`using System.Reflection;` + `namespace …` + `{` + `}` ⇒ **零类型、零成员**。⚠️ 用文本断言时 **BOM 不算空白字符**（必须显式 `.TrimStart('\uFEFF')`，否则首行永远判不空） | `DataGridViewEx_有效代码只有using与命名空间声明` |
| **70** | ⛔ 位置与内容矛盾（§5.8） | 注释自称是 **Winforms** 扩展，但所在工程 `Extension.csproj` 是 **`<UseWPF>true</UseWPF>`** 的库 | `Extension工程是UseWPF的_而该文件自称Winforms扩展` |
| **71** | ✅ 准入线"只管依赖、不管有没有用"的实例 | `Ui/Controls` 是 **T1 目录**（允许 `System.Windows` / `Microsoft.Win32`），而这个文件按实测是 **T0** —— 它**确实没有任何宿主/第三方 `using`**（`System.Windows` / `Microsoft.Win32` / `MaterialDesign` 一个都没有）⇒ 归级只看依赖，不看"这文件还有没有用" | `DataGridViewEx_没有任何宿主或第三方using` |

> **四条方法论（本轮踩出来的，写给以后的补测）**：
> 1. ⛔ **"锁的不是同一个对象"要拆成两条用例才成立**。`Observer<T>.Update` 锁 `Observable<T>` 实例、`Data` 锁内部 `_lock` ——
>    第一版用例**挂了观察者**，结果被卡住的是 `Notify → Update → lock(observable)` 这条链路、**不是 `Data` 本身**，断言假失败。
>    **只有"不挂观察者（证明 `Data` 与实例锁无关）+ 挂观察者（证明 `Update` 拿实例锁）"两条合起来，结论才完整**（§5.30.2 第 16/17 条）。
> 2. ⛔ **`Dispose(bool)` 里的 `is IDisposable` 可能是死分支**：`IColleague` / `ISubscriber<T>` **都不继承 `IDisposable`**，
>    所以 `FilteredColleague` / `FilteredSubscriber<T>` 里那两处 `is IDisposable` **恒为 false**。
>    **看到"接口 + `is IDisposable`"组合就该先确认接口本身有没有继承 `IDisposable`**（§5.30.3 第 32 条、§5.30.5 第 62 条）。
> 3. ⛔ **断言"同步抛"不能用 `Assert.Throws<T>(Func<Task>)`**：xunit 2.4.2 里那个重载已过时（`CS0619` + `xUnit2014`），
>    而且它**本身就在假设"异常进 Task"** —— 与本用例要证明的事相反。必须包成 `Action call = () => …; Assert.Throws<T>(call);`，
>    再用反射断言方法**没有** `AsyncStateMachineAttribute` 标记（§5.30.4 第 47 条）。
> 4. ⚠️ **本环境的 `Edit` 可能报成功但不落盘**（本轮遇到 3 次）⇒ 每次 `Edit` 之后**立即用 `Grep` 复核关键行**，未落盘就拆小重做。

---

## 6. 其他实测发现（未在本次提问范围内）

> ⛔ **2026-09-18 机械复核（序 12j）**：本节 A–G 七条都是**具体数字**，最容易过期。用
> `.workbuddy-ai/tools/audit_section6_claims.py`（只读）逐条从磁盘重推，结果：
>
> | # | 断言 | 实测 | 判定 |
> |---|---|---|---|
> | **A** | Cognex 是 32 位；Halcon / Extension 是 AnyCPU | `Cognex.VisionPro(.Core).dll` = `Machine=0x014c` + `Characteristics=0x2102`（32BIT_MACHINE）⇒ 真 x86；`halcondotnet.dll` / `Extension.dll` = `0x2022` ⇒ AnyCPU/MSIL | ✅ **完全吻合** |
> | **B** | 21 个 bin 里 129 MB 陈旧副本 | `Cognex.VisionPro.Core.dll` **21 份 / 122.6 MB**；`Cognex.VisionPro.dll` **21 份 / 6.4 MB**（合计 129.0 MB）；`ProjectReference → Cognex` **0 处** | ✅ **精确吻合** |
> | **C** | Extension.sln 的 x64/x86 是死配置 | 6 个解决方案配置 ✓；`Debug\|x64`/`x86`、`Release\|x64`/`x86` 的 `ActiveCfg` **全部 → `Any CPU`** ✓ | ⚠️ **结论对，口径要补**（见下方 §6.C） |
> | **D** | 4 个工程在两个 sln 里 GUID 不同 | `Extension.Vision.Cognex` / `Extension.Tests` / `DesignPattern.Samples` / `LicenseTool` **4 个全不同**，`Extension` 本体一致 | ✅ **完全吻合**（GUID 前缀逐个对上） |
> | **E** | `Extension/` 根目录 4 份 124 KB 的 md，全被跟踪 | 那 4 份实测 **122.4 KB**（46.9+50.1+18.1+7.3）**已全部跟踪** ✓ —— **但现在变成 5 份**：`FILE-TIERING.md`(187.9 KB) 也在同层且**未跟踪** | ⚠️ **数字已过期**（见下方 §6.E） |
> | **F** | samples 是 Library；2,149 行示例代码 | `OutputType = Library` ✓；行数实测 **2,164 行**（10 个 `.cs`，`splitlines` 口径） | ⚠️ **数字已过期**（见下方 §6.F） |
> | **G** | Cognex 被 Frame.sln 收录但零引用 | `Frame.sln` 里确有 `Extension.Vision.Cognex` ✓；`ProjectReference → Cognex` **0 处** ✓ | ✅ **完全吻合** |
>
> **四条吻合、三条要修**（C 是口径、E/F 是漂移）。E/F 的漂移**都是重排自己造成的** ——
> 这正是「结论有保质期」的又一实例：文档写完当天就已经和磁盘不一致了。

### A. ⛔ Cognex 是 **32 位**程序集，而全仓是 AnyCPU
实测 PE 头：`Cognex.VisionPro.dll` / `Cognex.VisionPro.Core.dll` = `Machine=0x014c` + `32BIT_MACHINE` + 非 IL-only ⇒ **真 x86**。
（对照：`halcondotnet.dll` 与自研 `Extension.dll` 均为 `Characteristics=0x2022`，即 AnyCPU/MSIL —— **Halcon 不是 x86**。）
构建实测 MSB3270，逐字为：
> 所生成项目的处理器架构"MSIL"与引用"Cognex.VisionPro.Core"的处理器架构"x86"不匹配。

⇒ 搬进 `Extension.Vision.Cognex` 之后，**这个工程在 x64 宿主里永远加载不了**。要么给它 `<PlatformTarget>x86</PlatformTarget>`，要么承认它只是"归档占位"。

> ✅ **2026-09-18 复核：逐位吻合。** 全仓扫到 `Cognex.VisionPro.dll` / `.Core.dll` **各 21 份**，
> `Characteristics` **全部是 `0x2102`**（`DLL|EXE|32BIT_MACHINE`）⇒ 真 x86，无一例外。
> `halcondotnet.dll` 20 份全部 `0x2022` ⇒ AnyCPU/MSIL。
> ⚠️ 顺带一个**新发现**：`Extension.dll` 全仓 111 份里，**有 6 份是 `Machine=0x8664`（AMD64）**，
> 且**全部**位于 `Extension/Extension/obj/x64/Release/net9.0-windows7.0/` —— 是 09-17 那次 x64 构建的
> **残留物**（见 §6.C：产物时间 18:44，早于 20:33 的「统一项目平台」提交）。
> ⇒ 判某个 dll 的真架构**不能只看 `Machine`**（AnyCPU 与 x86 都是 `0x014c`），要看 `Characteristics`。

### B. Cognex 搬迁已完成，但 **21 个 bin 里还有 129 MB 陈旧副本**
全仓已 **0 个 csproj** 引用 Cognex、**0 个 `.cs`** 使用它（除卫星工程自身）⇒ 这些副本是**增量构建不清理的残留**。

| | 搬迁前 | 现在 |
|---|---|---|
| `Cognex.VisionPro.Core.dll` 副本 | 33 份 / 192.7 MB | **21 份 / 122.6 MB** |
| `Cognex.VisionPro.dll` 副本 | 33 份 / 10.1 MB | **21 份 / 6.4 MB** |

降幅全部来自"**少了 12 份**"，**不是"每份不再复制"** —— 真正回收要一次 clean。

> ✅ **2026-09-18 复核：两个数字逐位吻合。** 实测 `Cognex.VisionPro.Core.dll` = **21 份 / 122.6 MB**、
> `Cognex.VisionPro.dll` = **21 份 / 6.4 MB**（合计 129.0 MB，与标题的「129 MB」一致）。
> 「0 个 csproj 引用 Cognex」也成立：`<ProjectReference>` 指向 Cognex 的 = **0 处**；
> 含 "Cognex" 字样的 csproj 只有卫星工程**自身** 1 个。
> 「0 个 `.cs` 使用它」也成立：命中 3 个，其中 2 个在卫星工程内（被"除卫星工程自身"排除），
> 第 3 个在 `.workbuddy-ai/encoding-backup/`（**非构建文件**）。
> ⚠️ 脚本注意：这一节统计的是 **bin 里的副本**，所以**不能排除 `bin`/`obj`** ——
> 排除后会只数到 `Dll/Vision/VisionPro/` 那 1 份（5.8 MB），差 21 倍。

### C. `Extension.sln` 的 x64/x86 是**死配置**
6 个解决方案配置里，`Debug|x64`、`Debug|x86`、`Release|x64`、`Release|x86` 的 `ActiveCfg` **全部映射到 `Any CPU`**（逐行核对）。
且 09-17 的 `统一项目平台` 提交（3539f99）把**全仓改回 `<Platform>AnyCPU</Platform>` + `AppendPlatformToOutputPath=false`**，现在**全仓 0 个 csproj 声明 `<Platform>x64</Platform>`**。
⇒ sln 里那 4 个配置是纯噪音（VS 工具栏会显示，但和 Any CPU 完全等价）。

> ✅ **2026-09-18 复核：结论成立，但「全仓 0 个」要加限定词。** 实测：
>
> | 口径 | 实测 |
> |---|---|
> | `Extension.sln` 解决方案配置 | **6 个** ✓（`Debug/Release` × `Any CPU/x64/x86`） |
> | 那 4 个 x64/x86 配置的 `ActiveCfg` | **全部 → `Any CPU`** ✓（`Frame.sln` 同样：`Debug\|x64` → `Debug\|Any CPU` **44/44**） |
> | **真实** csproj 声明 `<Platform>x64</Platform>` | **0 个** ✓ |
> | 声明 `<Platform>AnyCPU</Platform>` | **49 个** |
> | 未声明 `<Platform>` | **10 个** |
> | 声明 `<AppendPlatformToOutputPath>` | **51 个** |
>
> ⛔ **但磁盘上确实有 2 个 csproj 声明了 `<Platform>x64</Platform>`** ——
> `Communication/CommunicationDemo/CommunicationDemo_0gznxjis_wpftmp.csproj`（Sep 17 19:01）与
> `Vision/VisionDemo/VisionDemo_l0un3oul_wpftmp.csproj`（Sep 17 19:37）。
> 它们是 **WPF markup 编译的残留临时工程**（`*_<hash>_wpftmp.csproj`，正常应被清理），
> **不是真实工程** ⇒ 说「全仓 0 个」时必须限定为「**真实** csproj」。
> 这两个残留物本身也是证据：**x64 构建发生在 09-17 19:0x–19:3x**。
>
> **三条铁证，说明 x64 状态从未落地**：
> 1. `git log --all -S'<Platform>x64</Platform>' -- '*.csproj'` **返回空** ⇒ **从未提交过**；
> 2. `Extension/Extension/bin/` 下只有 `Debug/` 与 `Release/`，**没有 `x64/` 层**（产物路径 = AnyCPU 形态）；
>    `obj/x64/` 里的 `Extension.dll` 时间是 **09-17 18:44**，**早于** 20:33 的 `3539f99`；
> 3. `3539f99` 对 `Extension.csproj` 的 diff **只有 `+` 两行**（`<Platform>AnyCPU</Platform>` +
>    `<AppendPlatformToOutputPath>false</AppendPlatformToOutputPath>`）⇒ 它是**显式钉死 AnyCPU**，不是"改回"。
>
> 全仓规模（供对照）：**15 个 `.sln`**，其中 **13 个**含 `Debug|x64`；**61 个 csproj**。

### D. 同一个工程在两个 sln 里 **GUID 不同**
| 工程 | `Extension.sln` | `Frame.sln` |
|---|---|---|
| `Extension.Vision.Cognex` | `E559C59E-30C7-…` | `8290692A-5F29-…` |
| `Extension.Tests` | `C3085B68-2E8B-…` | `524D7340-841B-…` |
| `DesignPattern.Samples` | `8518B74E-B518-…` | `B3E0DCCA-49E2-…` |
| `LicenseTool` | `32F0593D-8E85-…` | `382EA308-3F45-…` |

（只有 `Extension` 本体两边一致 `8D5AA921-…`。）VS 会把它们当成两个不同工程处理。

> ✅ **2026-09-18 复核：逐个 GUID 前缀都对上。** 两个 sln 共有 **5 个**工程名：
> 4 个不同 + `Extension` 一致（`8D5AA921-C598-4B04-BC26-6D69650D2A2D`）。实测 GUID：
> `E559C59E-30C7-4334-…` / `8290692A-5F29-D752-…`、`C3085B68-2E8B-4A7B-…` / `524D7340-841B-ACB8-…`、
> `8518B74E-B518-43C7-…` / `B3E0DCCA-49E2-7EF2-…`、`32F0593D-8E85-42D6-…` / `382EA308-3F45-7B66-…`。

### E. `Extension/` 根目录躺着 **4 份 md**，全被 git 跟踪
`ARCHITECTURE-OPTIMIZATION.md` 48K + `CODE-CLASSIFICATION-ANALYSIS.md` 51K + `MIGRATION-CHECKLIST.md` 18K + `PHASE1-2A-REVIEW.md` 7K。
它们和源码混在同一层。建议进 `docs/`，并把 4 份合并成 2 份（**方案** / **证据**）—— 这也是 `CODE-CLASSIFICATION-ANALYSIS.md` §5 自己给的结论。

> ⚠️ **2026-09-18 复核：数字已漂移。** 实测那 4 份 = **122.4 KB**（46.9 + 50.1 + 18.1 + 7.3，按 1024 计），
> **4 份全部被 git 跟踪** ✓ —— 但**现在同层已经有 5 份**：
>
> | 文件 | 大小 | 跟踪 |
> |---|---|---|
> | `ARCHITECTURE-OPTIMIZATION.md` | 46.9 KB | 已跟踪 |
> | `CODE-CLASSIFICATION-ANALYSIS.md` | 50.1 KB | 已跟踪 |
> | `MIGRATION-CHECKLIST.md` | 18.1 KB | 已跟踪 |
> | `PHASE1-2A-REVIEW.md` | 7.3 KB | 已跟踪 |
| **`FILE-TIERING.md`（本文档）** | **281.8 KB**（写作时，⚠️ 会漂） | **未跟踪** |
> | | **合计 404.2 KB**（= 4 份 122.4 + 本文档 281.8） |
>
> ⇒ 「4 份 124 KB」这句**写完当天就过期了**：本文档自己成了第 5 份，而且是最大的一份。
> 建议按原结论一并收进 `docs/`（本文档与那 4 份正好构成「方案 + 证据」两堆）。
>
> ⛔⛔ **本文档自己的大小一天内过期了四次**：`124 KB`（初稿）→ `122.4 KB`（复核时实测）→
> `187.9 KB`（复核块写下时）→ `212.4 KB`（修 序 27/32/72 之后）→ `233.0 KB`（修 序 3/36/37/47 之后）
→ **`249.6 KB`（修 序 28/43/57/64/65 之后）** → **`262.1 KB`（修 序 60 / 序 8b 的 `float` 之后）**
> → **`268.5 KB`（修 序 8b 的 `ToASCII` 之后）**
> → **`272.7 KB`（第五轮文档收口 序 8b 之后）**
> → **`273.5 KB`（恢复待定表 8b 行 + 标注结案）**
> → **`281.8 KB`（序 73 文档同步）**
> —— 每次都是**我自己**在改这份文档。
> ⇒ **教训：不要把"正在被写入的文件的元数据"写进它自己。** 这个数字**永远**会漂，
> 而且每次同步文档都要再改一遍（净收益为负）。**保留它只作为"漂移速度"的示例，不要再当成事实引用。**
> 同类反模式（本文档里还有）：§5.10 的用例总数、§6.B 的 bin 占用 —— 它们至少**变化频率低**，
> 而"本文档自己的字节数"是**每次编辑都变**。

### F. `samples/DesignPattern.Samples` 是 **Library 不是 Exe**
一个"示例/演示"工程 `OutputType=Library`，**没法直接运行** —— 那 2,149 行示例代码只能靠测试或引用去跑。

> ⚠️ **2026-09-18 复核：`OutputType=Library` 吻合 ✓，行数已变。** 实测 samples 下 **10 个 `.cs` / 2,164 行**
> （`splitlines` 口径），不是 2,149。
> **漂移原因可解释**：重排给 samples 补了显式 `using`（§6.I 通路 2 的处置 3 个文件 +
> §6.K 的 `AdvancedFactoryPatternExample` 1 行）⇒ 行数自然上升。
> ⇒ 这是「**结论有保质期**」的又一实例：**不是结论错了，是代码变了**。

### G. `Extension.Vision.Cognex` 被 `Frame.sln` 收录但 **零引用**
每次全量构建都会编译它（并贡献 MSB3270）。它是 09-15 阶段 2A 的成果，**本方案不动它**。
它曾是收敛工程数时**唯一**一个"合回 `Extension` 而不损失任何东西"的候选（代价：Cognex 的 x86
`<Reference>` 回到主工程，MSB3270 从卫星工程转到主工程）。

> ⛔ **2026-09-18 决策：不合回（用户明确指示）。** 用户原话「`Extension.Vision.Cognex` 不用回归」。
> ⇒ 该卫星工程**保持独立**，**不并入** `Extension.csproj`；上段的"候选"属性**作废**，不要再据它提案。
> 理由与后果已登记进 §7「⛔ 明确**不做**的动作」表。
> 注意：不合回**不影响**本方案任何结论 —— 分级线、覆盖率、依赖边全部照旧（它本来就在 `Extension.dll` 之外）。

> ✅ **2026-09-18 复核：两条都成立。** `Frame.sln` 里确有 `Extension.Vision.Cognex` ✓；
> 全仓 `<ProjectReference>` 指向 Cognex 的 = **0 处** ✓（与 §6.B 同一条证据）。
> ⚠️ 但「每次全量构建都会编译它」这句**取决于构建入口**：`Frame.sln` 收录它，所以编 `Frame.sln` 会编；
> 而 `Extension.sln` 也收录它（`E559C59E-…`）⇒ 编 `Extension.sln` 同样会编。
> 真正**不会**编到它的入口是 `dotnet build Extension/Extension/Extension.csproj` 这种单工程构建。

### H. `ScheduledDeletion` / `DateFolderInfo` 各有**两份实现**，靠子命名空间隔离
`Core/Files/ScheduledDeletion.cs` 与 `Core/Files/ScheduledDeletionEx/ScheduledDeletionEx.cs` 的差异
**不是日志文案而是失败策略**。两份还各自声明了一个 `public class DateFolderInfo`。

> ⚠️ **2026-09-18 校正**：本节的策略描述**早期版本写反了**（原文写"FileEx 版 `throw`、
> SystemEx 版 `log 后吞掉`"）。补测时逐条实测，真实关系是**相反**的：
>
> | | `Core/Files/ScheduledDeletion`（**在用**） | `…/ScheduledDeletionEx/ScheduledDeletionEx`（`internal`、零引用） |
> |---|---|---|
> | 目录不存在 | 只 `Log` 一下**继续**，最终返回空列表 | `throw new Exception("目录不存在")` |
> | 过期目录删除成功 | 正常返回 | ⛔ **也抛异常**（把日志语句写成了 `throw`，又被自己的 `catch` 再包一层） |
> | 非法日期目录名 | `catch` 后 `return false`，**跳过** | ⛔ `catch` 里 `throw` ⇒ **整个方法挂掉** |
> | `GetDirectorySize` 出错 | `Log` + **`return 0`** | `throw` |
> | 上报手段 | 私有 `Log(string)`，但**方法体是空的**（`//Console.WriteLine` 被注释掉） | 没有 `Log`，只有 `throw` |
> | 返回值 | 可能非空 | **只可能是空列表**（任何"删了东西"的路径都先抛） |
>
> ⇒ 一句话：**在用版"处处吞"（且吞得连日志都没有），零引用版"处处抛"（且成功路径也抛）**。
> 两版都不具备可用的失败上报。实测细节与钉住它们的用例 → **§5.26 / §5.27**。

> 重排时这两份被并进同一个命名空间 `Extension.Core.Files` ⇒ **直接 `CS0101` 编译失败**。
> 处置（**没有删任何代码**）：把**零引用**的那份移进独立子目录 `Core/Files/ScheduledDeletionEx/`，
> 命名空间改为 `Extension.Core.Files.ScheduledDeletionEx`（.NET 允许父子命名空间共存）。
> 数据依据：在用版 `ScheduledDeletion` 被下游 2 处调用
> （`Packing Machine/.../Business/Flow/AutoProcess.cs:193`、`.../Config/Flow/StartupFlow/05_初始化后台.cs:109`）；
> `ScheduledDeletionEx` 是 `internal` 且**零引用**（仓里**没有 `InternalsVisibleTo`** ⇒ 测试只能走反射）；
> `DateFolderInfo` **下游零使用**。
> ⇒ 这是 §4.6"目录≠命名空间"被消除后**必然暴露**的既有冲突，不是新引入的问题。

### I. ⛔ 重排踩到的坑：C# 名称解析有**三条隐式通路**，光改 `using` 不够
本次重排 92 处 `namespace` + 73 个下游文件的 `using` 之后，编译器又逐个揪出 3 类**没有出现在 `using` 里**的引用。
它们都属于"**简单名**"解析，改 `using` 文本是抓不到的：

| # | 症状 | 根因 | 处置 |
|---|---|---|---|
| 1 | `error CS0234: 命名空间"Extension.Core.Convert"中不存在类型或命名空间名"ToInt32"` | 新目录名 `Convert` 与 **`System.Convert`（类型）** 同名。在 `Extension.Core.*` 内部，简单名 `Convert` 会先命中兄弟命名空间 `Extension.Core.Convert` ⇒ 遮蔽了 `System.Convert` | **目录改名 `Core/Conversion`**（`Extension.Core.Conversion`）。不是去把 27 处 `Convert.ToInt32` 全限定 —— 那样留着一颗地雷 |
| 2 | `error CS0103/CS0246: 未能找到 Singleton / ColleagueBase / AdvancedProductBase` | samples 自己的命名空间是 `Extension.DesignPattern.Xxx.Examples`，**镜像**了库的 `Extension.DesignPattern.*`，于是很多类型靠**外层命名空间链**隐式解析，**从不写 `using`**。库改到 `Extension.Patterns.*` 后镜像断了 | 给 samples 补显式 `using`（3 个文件） |
| 3 | `error CS0246: 未能找到类型或命名空间名"Advanced"` | 同理：`new Advanced.AdvancedMessageBroker<T>()` 里的 `Advanced` 是**命名空间简单名** | 加命名空间别名 `using Advanced = Extension.Patterns.PublishSubscribePattern.Advanced;`（**不能去掉限定** —— 基命名空间也有一份同名类型，会 CS0104） |

**三条通路的通用结论（下次重排必看）**：

1. **兄弟命名空间遮蔽**：`Extension.Core.Convert` 会让 `Extension.Core.*` 里所有 `Convert.X` 解析错。
   ⇒ 目录/命名空间**不要与 `System.*` 里的类型或命名空间同名**。
   本次实测扫描 21 个候选段，只有 `Convert` 真中招；`Reflection` / `Collections` / `Data` / `Security`
   虽然也与 `System.*` 同名，但实测 0 处简单名引用，**故未改名**（数据说了算）。
2. **外层命名空间链**：调用方若把自己的命名空间"镜像"成库的命名空间，就会形成隐式依赖。
   ⇒ 重排后必须**全量编译**，`grep using` 是**不充分**的。
3. **命名空间简单名**（`Advanced.Foo`）：`using` 文本里完全看不见。
   ⇒ 扫描重排影响面时，要同时搜「命名空间末段 + `.`」这个模式。

> 本次的收尾扫描脚本：按「新命名空间末段」在 Extension 树内做前缀匹配扫简单名引用，
> 并全仓扫"旧命名空间残留的 using / namespace"（含 `using static`）。
> 后者抓到 4 组漏网：`Extension.Tests/PrimitivesTests.cs`（3 处，上一轮把整棵 `Extension/` 当"内部"漏掉了）、
> `Communication1/.../ModuleSplitter.cs`（`using static …DataEx.BufferEx`）、
> `samples/.../SingletonPatternExample.cs`、`.workbuddy-ai/tools/*.template.cs`（3 个非构建文件）。

### J. `git mv` 不会删掉搬空的源目录
重排后 `Extension/Extension/` 下残留 `DesignPattern/` `Helper/` `Struct/` `Tool/` **4 个空目录树**
（0 文件）。git 不跟踪空目录，所以 `git status` 完全看不到它们 —— 只有我自己的守卫用例
`层级表必须覆盖全部顶层目录` 才把它们揪出来。
处置：`find <dir> -depth -type d -empty -delete`（`-empty` 保证**只删空目录，有内容就拒绝**）。
⚠️ 该用例此后改为**从实际扫描到的 .cs 反推顶层目录**，不再用 `Directory.EnumerateDirectories` ——
否则 `bin` / `obj` / `.vs` 与将来任何非源码目录都会变成噪声。

### K. `Patterns/*/Advanced/` 是**统一化**，不是新增层级
HEAD 时 6 个模式里 **5 个**的 `Advanced*` 文件已在 `.Advanced` 子命名空间，
**只有 `FactoryPattern` 是异类**（`AdvancedFactoryPattern.cs` 声明了扁平的 `Extension.DesignPattern.FactoryPattern`，
把 `AdvancedProductBase` / `AdvancedFactoryBase` 混进了基类命名空间）。
重排把 6 个模式统一成 `<Pattern>/Advanced/` ⇒ 修掉了这处不一致。
**代价**：`samples/.../AdvancedFactoryPatternExample.cs` 需要补一行 `using ...FactoryPattern.Advanced;`
（原先是靠扁平命名空间拿到的）—— 这是**公开 API 命名空间的真实变化**，已知并已修。

### L. ⛔ 内部依赖边实测（2026-09-18 机械复核，序 12i）—— **是 4 条，不是 7 条**

§0 结论表曾写「**内部依赖只有 7 条边**，且全部向下、零反向引用」。用
`.workbuddy-ai/tools/audit_dependency_edges.py`（只读）从磁盘重推，**这个数字复现不出来**。

**判据**（脚本与守卫同源，但补上守卫漏掉的两点）：

1. 内部引用 = 命名空间以 `Extension.` 开头；
2. **两种写法都算**：`using Extension.X…;` **和**正文里的完全限定名 `Extension.X.Y.Z`；
3. 注释（`//` 与 `/* */`）里的名字**不算**依赖；
4. 顶层目录 = 路径第一段；层级取守卫 `TierGuardTests.TierOf` 的同一张表；
5. 目标目录靠「命名空间 → 目录」**最长前缀匹配**解析，不靠猜。

#### L.1 实测矩阵（行 = 引用方，列 = 被引用方，数字 = **文件**数）

```
                  Contracts     Core          Distribution  Patterns      Runtime       Ui            Vision        层
Contracts (T0)    —             ·             ·             ·             ·             ·             ·             T0
Core (T0)         ·             —             ·             ·             ·             ·             ·             T0
Distribution (T0) ·             ·             —             ·             ·             ·             ·             T0
Patterns (T0)     ·             2             ·             —             ·             ·             ·             T0
Runtime (T2)      1             ·             ·             ·             —             ·             ·             T2
Ui (T1)           ·             1             ·             ·             ·             —             ·             T1
Vision (T3)       ·             1             ·             ·             ·             ·             —             T3
```

#### L.2 真实有向边（共 **4** 条）

| # | 边 | 方向 | 引用点 | 代表文件 |
|---|---|---|---|---|
| 1 | `Patterns`(T0) → `Core`(T0) | ⚠️ **同层**（守卫不管） | 2 | `Patterns/ObserverPattern/ObserverPattern.cs`、`…/Advanced/AdvancedObserverPattern.cs` |
| 2 | `Runtime`(T2) → `Contracts`(T0) | ✅ 向下 | 1 | `Runtime/Mapping/MapsterHelper.cs` |
| 3 | `Ui`(T1) → `Core`(T0) | ✅ 向下 | 1 | `Ui/Theme/ThemeService.cs` |
| 4 | `Vision`(T3) → `Core`(T0) | ✅ 向下 | 1 | `Vision/HImageEx.cs` |

**逐条核对原断言**：

| 原断言 | 实测 | 判定 |
|---|---|---|
| 「只有 **7** 条边」 | **4** 条 | ⛔ **数字不对** |
| 「**全部向下**」 | 向下 3 + **同层 1** | ⚠️ 不严谨（`Patterns → Core` 两边都是 T0） |
| 「**零反向引用**」 | 反向 **0** 条 | ✅ **成立**（没有低层目录引用高层目录） |

**「7」从哪来？** 换四种粒度都不等于 7：顶层有向边 **4** / 顶层无向边 **4** / 子目录级 **5** / 文件级 **5**。
最可能的来源是**把「7 个顶层目录」写成了「7 条边」**（原句后半段正好是"7 个顶层目录构成一张分层 DAG"）。
⇒ 按硬规则 3 的口径：**复现不出来的数字一律按错的处理**，已改为 4 并在此留下推导。

#### L.3 ⛔ 比数字更要紧的两件事

**(1) §0 的交叉引用指错了。** 原句写「见 §6.I」，但 §6.I 是「C# 名称解析有**三条隐式通路**」，
跟依赖边毫无关系。**这是"结论引用了不存在的落点"** —— 也就是说这条断言
**在文档里根本没有推导过程**，只有一个孤零零的数字。现已落到本节。
（同一处错误也存在于 `TierGuardTests.cs` 的 `TierOf` XML 注释里，已一并修正。）

**(2) ⛔ 守卫的「依赖方向」测试有两个限制，且它**并不校验**这个数字。**

| # | 限制 | 后果 |
|---|---|---|
| 1 | 只遍历 `UsingRegex`（行首 `using`），**不扫正文** | §3.3(2) 修的「第三盲区」**只修了「准入」测试，没修「方向」测试** —— 一个 T0 文件在正文里写 `Extension.Runtime.Foo`，方向测试**看不见**。<br>本次实测该盲区**存在但未触发**（0 处只在正文里的跨目录引用）。 |
| 2 | 只比**层级号**（`toTier > fromTier` 才失败） | **同层之间的引用它完全不管**。而 7 个顶层目录里 **T0 就占了 4 个**（`Contracts`/`Core`/`Patterns`/`Distribution`）⇒ 这 4 个之间**任意方向、任意数量**的引用都是绿的。这正是 §5.5「`Contracts/` ↔ `Runtime/Mapping/` 双向耦合仍在」那类问题**能长期存在的原因之一**。 |

⇒ 结论：**「零反向引用」是"守卫能保证"的部分（层级号单调）；「只有 N 条边」「全部向下」都不是守卫保证的**，
它们只是某次人工统计的结果，**必须靠脚本重推**。

**(3) 附带复核**：目录 ↔ 命名空间契约 **0 处不一致**（80 文件全部满足「目录 = 命名空间」），
与守卫的 `namespace必须与目录一致_目录即命名空间` 用例互为印证。

---

## 7. 执行记录（按风险排序）

### ✅ 已完成（2026-09-18）

| 序 | 动作 | 结果 |
|---|---|---|
| **1** | 删 `RegisterEx.cs:1` 多余 `using HalconDotNet;`（§5.1） | ✅ 构建 **0 错误**；`Core/Conversion/` 100% 归 T0 |
| **2** | 加分级守卫测试 `Extension.Tests/TierGuardTests.cs`（§3.3） | ✅ **不新建工程**；先命中 1 处真实违规，修完后 **18/18 全绿**（现为 **12/12**，2026-09-18 升级扫描器 + 补"目录=命名空间"契约 + 方向测试扫正文） |
| **6** | **按 §3.1 目录重排 + 命名空间同步改名**（用户选"一起改"） | ✅ 80 文件 `git mv`、92 处 `namespace` 改写、下游 **73 文件 / 75 处** `using` 改写、samples 补 3 处显式 `using` |
| **6b** | 同步更新 `TierGuardTests`（目录键 + 修复匹配器缺陷 + 加依赖方向用例） | ✅ **21/21 全绿**（14 业务 + 7 守卫/方向） |
| **6c** | 清理 `git mv` 搬空的 4 个空目录树（§6.J） | ✅ `Extension/Extension/` 只剩 7 个职责目录 |
| **6d** | 隔离 `ScheduledDeletionEx` 的 `DateFolderInfo` 重名（§6.H） | ✅ **未删任何代码**，靠子命名空间隔离 |
| **8** | 补 T0 层单测·第一轮（§5.10） | ✅ **21 → 430 用例全绿**（13 个文件：`ConversionTests` 72 / `RegisterExTests` 53 / `JsonExTests` 48 / `ByteExTests` 39 / `TaskExTests` 29 / `CollectionsTests` 28 / `EventHelperTests` 24 / `BufferExTests` 22 / `ReflectionExTests` 21 / `BitOperationExTests` 20 / `ChannelHelperTests` 20 / `ValueTaskSourceHelperTests` 18 / `EnumExTests` 15）；T0 覆盖 **19 → 23 / 60**；**未改任何产品代码** |
| **8c** | 补 T0 层单测·第二轮（§5.10） | ✅ **430 → 503 用例全绿**（新增 5 个测试文件 + 1 个共享 helper：`DirectoryExTests` 29 / `FileExTests` 14 / `NotifyPropertyChangedTests` 13 / `ZipExTests` 10 / `CloneExTests` 7 / `TempWorkspace.cs`）；T0 覆盖 **23 → 27 / 60（45.0%）**：补掉 `Core/Files` 的 2 个（`FileEx`/`DirectoryEx`）、`Core/Data` 的 `ZIPEX`、`Core/Notify`；**未改任何产品代码** |
| **11** | 记录补测过程中发现的 **13** 处 API 陷阱 / 缺陷（§5.11–§5.19） | ✅ 全部**只记录 + 用测试钉住**，未改产品代码（硬规则 1） |
| **11b** | 记录第二轮发现的 **17** 处行为 / 缺陷（§5.20–§5.24） | ✅ 同上：只记录 + 钉住。其中 **6 处是真缺陷**（`FileEx` 的 null 静默清空、`DirectoryEx` 的 `GetParentSecond` NRE / 月日不补零 / `DirCopy` 路径替换、`ZIPEX` 的静默编码替换、`CloneEx` 的 `string` 抛异常） |
| **8d** | 补 T0 层单测·第三轮（§5.10） | ✅ **503 → 581 用例全绿**（新增 3 个文件：`CSVExTests` 50 / `ScheduledDeletionTests` 18 / `ScheduledDeletionExTests` 10）；T0 覆盖 **27 → 30 / 60（50.0%）** —— `Core/Files` 的 5 个文件**全部覆盖**；`ScheduledDeletionEx` 是 `internal`（仓里无 `InternalsVisibleTo`）⇒ **全程反射**；**未改任何产品代码** |
| **11c** | 记录第三轮发现的 **21** 处行为 / 缺陷（§5.25–§5.27） | ✅ 同上：只记录 + 钉住。其中 **16 处是真缺陷**（`CSVEx` 8 / `ScheduledDeletion` 5 / `ScheduledDeletionEx` 3） |
| **12c** | ⛔ **校正 §6.H 的错误结论**（两份 `ScheduledDeletion` 的策略描述**写反了**） | ✅ 逐条实测后重写，并补上六行对照表。**这是硬规则 3 的实例**：结论有保质期，动手前必须重新核对代码——原结论是照着"看起来合理"的推断写的，从没被验证过 |
| **8e** | 补 T0 层单测·第四轮（§5.10） | ✅ **581 → 629 用例全绿**（新增 3 个文件：`CorePlatformTests` 22 / `ContractsTests` 15 / `ThemeSettingsTests` 7；`TierGuardTests` 7 → 11）；T0 覆盖 **30 → 38 / 60（63.3%）** —— `Core/Platform` 变成纯 T0 后**整个目录覆盖**；⚠️ **本轮是唯一一次改了产品代码**（见 12d） |
| **11d** | 记录第四轮发现的 **10** 处行为 / 缺陷（§5.28） | ✅ 只记录 + 钉住。其中 **7 处是真缺陷**（`ProcessEx` 无校验就去杀进程 / `SafeMemoryHandle` 悬垂指针 / `CpuMonitorEx` 释放无效 / `FpsEx` 两处 / `KeyValue` 未重写 `Equals` / `ImageInfo` 浅拷贝），另 **3 处是正面样板或合理降级**（`DynamicDllLoaderEx` 不吞异常、`CpuMonitorEx.IsCounterUnavailable` 区分环境与缺陷、`FpsEx` 间隔回落） |
| **12d** | ⛔ **修正两处分级误判**（§4.7 —— T0/T1 刚好**互换**） | ✅ `git mv DelayEx.cs → Ui/Controls/`（namespace 同步）+ 下游 `DisplayService.cs` **1 行** `using`；`Core/Platform` 变纯 T0 ⇒ 删掉 `Allowed` 里那条"**为了让守卫变绿而补的假依赖**"登记；`Microsoft.Win32.SafeHandles` 进 `BclExemptions`。**T0=60 / T1=8 总数不变** —— 这正是它长期没被发现的原因 |
| **12e** | ⛔ **修守卫测试的第三盲区：完全限定名**（§3.3） | ✅ 扫描器从"扫 `^\s*using`"升级为"**扫文本** + 排除注释行"；新增 3 条用例（前缀表一致性 + 2 条防盲区）；**7 → 10 全绿**（随后 12f 补到 **11**）。旧扫描器看不见 `DelayEx` **正文**里的 `System.Windows.Threading` —— 一个 T1 依赖因此长期住在 T0 目录里，而守卫一直是绿的 |
| **12f** | ⛔ **补上"目录 = 命名空间"这条契约的守卫**（§3.3 第 (4) 点） | ✅ 新增 `namespace必须与目录一致_目录即命名空间`；**10 → 11 全绿**。**这是"重排漏改"能长期藏住的原因** —— §3 写着"目录 ↔ 命名空间 1:1"，却**没有任何用例守它**：重排时漏改了 `Core/Platform/DelayEx.cs` 的 namespace，而下游 `using` 也一起没改 ⇒ 两边一致 ⇒ 编译通过、守卫全绿。**教训：约定没被断言就等于不存在** |
| **12** | 复核分级表（80 文件逐个重扫 `using`） | ✅ **总数** T0=60 / T1=8 / T2=7 / T3=5 与 §1 一致（⚠️ 但归属有 **2 处错**，见 12d —— 加总核对**看不出来**）；并发现「`grep '^using'` 漏 BOM 首行」这一**方法论坑**（§1 复核表） |
| **8f** | 补 T0 层单测·第五轮（§5.10） | ✅ **629 → 694 用例全绿**（新增 2 个文件：`DistributionSecurityTests` 49 / `DistributionProtectionTests` 16）；T0 覆盖 **38 → 48 / 60（80.0%）** —— `Distribution/Security`(6) + `Distribution/Protection`(4) **两个目录整体覆盖**；**未改任何产品代码** |
| **11e** | 记录第五轮发现的 **32** 处行为 / 缺陷（§5.29） | ✅ 只记录 + 钉住。其中 **24 处是真缺陷**（Security 17 / Protection 7），另 **8 处是正面样板或合理取舍**（`MachineCodeEx` 双占位符留痕、`AppProtectionEx` Timer 自吞异常、`AntiDebugEx` 三探测白名单、`AesEx` 密钥 `ZeroMemory`、`HashEx` 用 `RandomNumberGenerator`、`AntiToolProcessEx` 黑名单滞后等）。⛔ 其中 **1 处是高危**：`LicenseManagerEx` 的"签名合法但 `Features` 为 null ⇒ NRE" |
| **8g** | 补 T0 层单测·第六轮（§5.10）—— **T0 收官** | ✅ **694 → 910 用例全绿**（新增 5 个文件：`PatternsObserverMediatorTests` 78 / `PatternsPublishSubscribeTests` 48 / `PatternsSingletonFactoryTests` 47 / `PatternsProducerConsumerTests` 39 / `UiControlsPlaceholderTests` 4）；T0 覆盖 **48 → 60 / 60（100.0%）** —— `Patterns`(11) + `Ui/Controls/DataGridViewEx`(1) 是本轮补掉的最后 12 个 T0 文件；**未改任何产品代码**；**0 错误 0 警告** |
| **11f** | 记录第六轮发现的 **71** 处行为 / 缺陷（§5.30） | ✅ 只记录 + 钉住。`Patterns/` **67 处**（其中 **14 处是纯死代码**）+ `Ui/Controls/DataGridViewEx` **4 处**。⛔ 本轮三条最值得记的：① **`lock(observable)` 与 `lock(_lock)` 锁的不是同一个对象**（§5.30.2 第 16/17 条）；② **`Dispose(bool)` 里的 `is IDisposable` 可以是死分支**（`IColleague`/`ISubscriber<T>` 都不继承 `IDisposable`，§5.30.3 第 32 条 + §5.30.5 第 62 条）；③ **`Stop()` 之后再 `StartAsync` 会让生产者/消费者同时忙循环**（channel 已 `Complete`、`_running` 却被置回 `true`，§5.30.4 第 41 条） |
| **12g** | ⛔ **机械复核「T0 覆盖 60/60」这个结论**（§5.10 末尾） | ✅ 用两个可复现脚本（`.workbuddy-ai/tools/audit_t0_coverage.py` + `audit_ext_method_collision.py`）逐文件核对，**结论：60/60 成立，但原文写的口径只对 53 个文件成立**。实测四种机制并存：① 类型名出现在测试代码里 **53**；② 类型名只在 `typeof(...)` 里、行为靠**裸扩展调用** **5**（`BoolEx`/`DateTimeEx`/`ReadOnlySpanEx`/`RegexEx`/`StringEx`）；③ 类型名**完全不出现** **1**（`ListEx`）；④ **零类型声明**、靠源码路径断言 **1**（`DataGridViewEx`）。⚠️ 关键坑：`ToShort`/`ToInt`/`ToUInt` 这些**方法名在多个 `*Ex` 类之间撞车**，只靠 `grep` 会把 `byte[].ToShort()`（`ByteEx`）误当成 `bool.ToShort()`（`BoolEx`）⇒ 必须按**接收者类型**判。**口径已改写进 §5.10** |
| **12h** | ⛔ **机械复核 §2 的逐文件层级表**（80 文件） | ✅ 用第三个可复现脚本 `.workbuddy-ai/tools/audit_per_file_tier.py`，**逐字复用守卫的同一套规则**（`GuardedPrefixes` + `BclExemptions` + `ReferenceRegex` + `PREFIX_TIER`）重推每个文件的实际层级，再与 §2 声明比对：**80 / 80 一致，`声明层级 ≠ 实测层级` = 0**（其中 59 个文件"无任何受监管引用 ⇒ 推算 T0"，另 1 个是走 `Microsoft.Win32.SafeHandles` 例外仍是 T0 的 `SafeMemoryHandle.cs`）。⚠️ 脚本要**排除 `obj/` / `bin/` 下的生成文件**（否则数出 92 个而不是 80）。**顺带挖出两类纯文档数字错误**：① **行数口径混用** —— §5.30 用 `wc -l`，而全文声明的口径是 `splitlines`，文件末行无换行时系统性差 1 ⇒ 六组行数各少 1（`1,027/754/1,364/1,385/1,294/86` → `1,030/756/1,366/1,387/1,296/87`），并连带 §5.10 表格 3 处；② `DataGridViewEx.cs` 的注释行数**任何口径都对不上**（原文写 `84`，实测 `73` 注释 + `10` 空行 + `4` 有效行 = `87`）⇒ 已修 §5.8 / §2 树注释 / §5.10 / §5.30.6 / §7 共 5 处 |
| **12i** | ⛔ **机械复核「内部依赖只有 7 条边」这个结论**（新 §6.L） | ✅ 用第四个可复现脚本 `.workbuddy-ai/tools/audit_dependency_edges.py` 从磁盘重推依赖边。**结论：实测 4 条，不是 7 条**（顶层有向边 4 / 顶层无向边 4 / 子目录级 5 / 文件级 5 —— **四种粒度都不是 7**）。逐条核对原断言：① 「只有 7 条边」**⛔ 数字不对**（实测 4）；② 「全部向下」**⚠️ 不严谨**（向下 3 + **同层 1**：`Patterns`(T0) → `Core`(T0)）；③ 「**零反向引用**」**✅ 成立**（反向 0 条）。⛔ 两个比数字更要紧的发现：**(a) §0 的交叉引用指错了** —— 原句写「见 §6.I」，而 §6.I 是「三条隐式通路」，**这条断言在文档里根本没有落点**（同一个错误也在 `TierGuardTests.cs` 的 `TierOf` XML 注释里）；**(b) 守卫的「依赖方向」测试有两个限制** —— 它**只遍历 `UsingRegex`（不扫正文）**（§3.3(2) 修的第三盲区**只修了准入测试**）+ **只比层级号**（⇒ 4 个 T0 目录之间任意方向引用**全是绿的**）。⇒ **「零反向引用」是守卫能保证的，「只有 N 条边」「全部向下」都不是**，必须靠脚本重推。附带复核：目录 ↔ 命名空间 **0 处不一致**。**(c) 顺带修正 §5.5 的「双向耦合」表述** —— 依赖图上 `Runtime/Mapping → Contracts` 是**单向**（`Contracts/ImageInfo.cs` 用的是**外部包** Mapster，不是 `Extension.Runtime`），库内**没有环**；且 `Contracts/` 是 T0 **目录**却含 **1 个 T2 文件**（目录层级 ≠ 文件层级）。**(d) 顺手把方向测试的盲区修掉** —— 新增 `InternalRefRegex` + `ExtractInternalReferences`（扫正文、跳注释），方向测试改为「using 行 ∪ 正文」并去重；新增用例 `方向扫描器必须能看见只在正文里的完全限定名`（用**构造的假源码**做输入，因为仓库当前恰好 0 处 ⇒ 测现状等于没测）⇒ `TierGuardTests` **11 → 12 全绿**，`Extension.Tests` **910 → 911 全绿** |
| **12j** | ⛔ **机械复核 §6「其他实测发现」A–G 的七条具体数字** | ✅ 用第五个可复现脚本 `.workbuddy-ai/tools/audit_section6_claims.py` 逐条从磁盘重推。**四条吻合、三条要修**：**A** ✅ 逐位吻合（`Cognex.VisionPro(.Core).dll` 21 份**全部** `Characteristics=0x2102` ⇒ 真 x86；`halcondotnet.dll` 20 份 `0x2022` ⇒ AnyCPU）；**B** ✅ **精确吻合**（`Cognex.VisionPro.Core.dll` **21 份 / 122.6 MB**、`Cognex.VisionPro.dll` **21 份 / 6.4 MB**）；**C** ⚠️ 结论对但**「全仓 0 个 csproj 声明 x64」要加限定词** —— 真实工程确实 0 个，但磁盘上有 **2 个 `*_wpftmp.csproj`**（WPF markup 编译残留，Sep 17 19:0x）声明了 x64；**D** ✅ 四个工程 GUID 逐个对上；**E** ⚠️ **数字写完当天就过期** —— 原「4 份 124 KB」实测 4 份 **122.4 KB**，但**同层现在已有 5 份**（`FILE-TIERING.md` 自己 187.9 KB，未跟踪）；**F** ⚠️ `OutputType=Library` ✓，但行数 **2,149 → 2,164**（重排给 samples 补 `using` 导致，**不是结论错、是代码变了**）；**G** ✅ `Frame.sln` 收录 ✓ + `ProjectReference` **0 处** ✓。⛔ 三个额外发现：① `Extension.dll` 全仓 111 份里**有 6 份是 `Machine=0x8664`（AMD64）**，全在 `obj/x64/Release/` ⇒ 09-17 x64 构建的残留；② 铁证 **`git log --all -S'<Platform>x64</Platform>' -- '*.csproj'` 返回空** ⇒ x64 状态**从未提交过**，`3539f99` 的 diff **只有 `+` 两行**（显式钉死 AnyCPU）；③ 全仓 **15 个 `.sln`**（13 个含 `Debug\|x64`）、**61 个 csproj**（49 AnyCPU / 10 未声明 / 2 wpftmp） |
| **12k** | ✅ **登记「不合回 `Extension.Vision.Cognex`」决策**（用户 2026-09-18 明确指示：「不用回归」） | ✅ **纯文档动作，零代码改动。** §6.G 的"唯一可合回候选"表述**作废**并加决策块；§7「⛔ 明确**不做**的动作」表新增一行。不合回**不影响本方案任何结论** —— 它本来就在 `Extension.dll` 之外，`ProjectReference → Cognex` 实测 **0 处**（与 §6.B 同一条证据），分级线 / 覆盖率 / 依赖边全部照旧 |
| **41** | ✅ **修 `ProducerConsumer<T>` 的"`Stop()` 后 `StartAsync` 忙循环"**（§5.30.4 第 41 条）—— ⛔ **本方案里第一处按用户授权修改的产品代码**（2026-09-18，授权范围"1 个文件 / 1 处"） | ✅ 新增 `_stopped` 标志 + `StartAsync` 两道闸门（`_disposed` ⇒ `ObjectDisposedException`；`_stopped` ⇒ `InvalidOperationException`），`Stop()` 的 XML doc 补"终态操作"说明。**实测证据**：修前那条用例只能靠 300 ms 取消令牌逃出来，修后 **15 ms 立刻抛**（对照：当时同型缺陷的 `EnhancedProducerConsumer` 仍是 **308 ms** —— 该缺陷**现已由序 72 修复**，见下行）。用例 **39 → 42**（重写 1 + 新增 3），`Extension.Tests` **911 → 914 全绿**；`Extension.sln` **0 错误**。<br>⛔ **为什么选"抛"而不是"重建 channel"**：`_running` 是**共享标志**，`Stop()` 之后旧任务要等下一次 `while` 检查才退出 —— 若此时重建 channel 并置回 `_running = true`，**旧任务会"复活"并共用新 channel** ⇒ 生产者/消费者各变 **2 份**（静默重复，比抛异常更糟）。抛则让旧任务正常退出、新调用立刻失败。<br>⛔ **同轮新发现 序 72**：`EnhancedProducerConsumer<T>` / `ConfigurableProducerConsumer<T>` **逐字同型**，当轮**未动**（授权范围外），各加 1 条"显式断言确实挂起"的用例钉住；**已在后续取得逐项授权后一并修复**，见下方序 72 行 |
| **72** | ✅ **修 `EnhancedProducerConsumer<T>` / `ConfigurableProducerConsumer<T>` 的忙循环**（§5.30.4 第 72 条，与序 41 **逐字同型**）—— 用户 2026-09-18 **逐项**授权（⚠️ 序 41 的"1 个文件 / 1 处"**不覆盖**它） | ✅ 两个类各加 `private volatile bool _stopped = false;` + `StartAsync` 两道闸门（`_disposed` ⇒ `ObjectDisposedException`；`_stopped` ⇒ `InvalidOperationException`）+ `Stop()` 补"终态"`<remarks>`。**实测证据**：修前那两条用例必须**耗尽 150 ms 令牌预算**才能"证明它挂住了"；修后 **1–41 ms** 就抛出（首条含 JIT 预热）。<br>⛔ **为什么不能"重建 channel 让它可以重启"**：同序 41 —— `_running` 是**共享标志**，`Stop()` 后旧任务要等下一次 `while` 检查才退出（**存在窗口期**）；此时重建 channel 并置回 `_running = true` 会让**旧任务"复活"并共用新 channel** ⇒ 生产者/消费者各变 2 份（静默重复，比抛异常更糟）。<br>⛔ **`PriorityQueueProducerConsumer<T>` 未碰** —— 它没有这条缺陷（`StartAsync` 本来就是必抛 `NotImplementedException`，§5.30.4 第 47 条）。<br>用例 **42 → 44**（重写 2 条"忙循环" + 新增 2 条 `Dispose` 后 `ObjectDisposedException`） |
| **27** | ✅ **修 `ProcessEx.CloseProcess` 的"传 `null` 就真的去杀进程"**（§5.28 第 1 条）—— 用户 2026-09-18 逐项授权 | ✅ 入口加**两道**校验：`null` ⇒ `ArgumentNullException`；空串 ⇒ `ArgumentException` —— **都在 `Process.GetProcessesByName` 之前**。⛔ 实测 `GetProcessesByName(null)` **既不抛异常、也不返回空数组**，而是返回一批**无关**进程 ⇒ 紧接着的 `Kill()` 真会执行（原用例实测以 `Win32Exception: 拒绝访问` 收场，**证明 Kill 确实被调用**；以管理员身份运行则不可逆）。**空串实测同样返回一批进程** ⇒ 两个都要挡。<br>✅ **修后测试强度升级**：原用例是"**静态核对、刻意不执行**"（因为执行真的会杀进程），修后入口先抛 ⇒ 改成**真的执行**并断言抛异常；另加 1 条源码护栏 `ProcessEx_源码里必须保留参数校验`。用例 `CorePlatformTests` **22 → 23**（净 +1） |
| **32** | ✅ **修 `LicenseManagerEx.Verify` 的"签名合法但 `Features` 为 null ⇒ NRE"**（§5.29 第 1 条）—— 用户 2026-09-18 逐项授权 | ✅ 在**反序列化边界**补 `info.Features ??= new(); info.Extra ??= new();`，语义对齐 `LicenseInfo.Features` 的文档契约「**空 = 全部功能**」。⛔ 根因：属性初始化器 `= new()` **只在 JSON 缺字段时生效**，厂商签发工具显式写出 `"Features":null` 会把属性**直接覆盖成 null** ⇒ 下面 `info.Features.Count` 抛 NRE，而 NRE **不在 `Verify` 任何 catch 名单里** ⇒ 逃到调用方（**拒绝服务，客户端无从防御**）。<br>⚠️ **修后是 fail-open（放行）而不是 `FeatureDenied`**：归一化后 `Count == 0`，功能点检查的 `Count > 0` 前置不成立 ⇒ 整段检查被跳过。敢 fail-open 的理由是 payload **签过名**，攻击者没有私钥造不出 `"Features":null`；这里防的是**签发工具的 bug**，不是攻击者。⚠️ `Extra` 的归一化 **`Verify` 自己用不到**，价值在**保护调用方**（`LicenseTool/MainWindow.xaml.cs` 打印"扩展字段"时会 `.Count`）。<br>用例：**重写 1 条**（断言从"抛 NRE"改成"返回 `Valid`，且 `Features`/`Extra` 非 null 的空集合"）+ 加**前提自证**（先断言 `JsonSerializer.Serialize` 真的会写出 `"Features":null`，否则用例会退化成"测了个普通 License"而仍然绿）⇒ **用例数不变**。⛔ 相邻的 `LicenseInfo_JSON里的显式null会覆盖属性初始化器` **测的是反序列化语义本身，不受修复影响，刻意保留原断言** |
| **3** | ✅ **修 `EventHelper` 的 `throw ex;` + `BitOperationEx` 泛型指针缺 `unmanaged`**（§5.2 / §5.3）—— 用户 2026-09-18 **打包**授权（"低风险加固包"，与序 36 / 37 / 47 同一批；⚠️ 该批授权**只覆盖这 4 项**，不构成对表外任何一项的授权） | ✅ **2 个文件 / 3 处**，实施时拆成 **3a / 3b**：<br>**3a**（`Core/Concurrency/EventHelper.cs`，2 处）`throw ex;` → `throw;`（现 **148 / 188** 行）⇒ 保留处理器自身的栈帧，消掉 **CA2200 ×2**。**异常类型与消息完全不变** ⇒ 下游 `catch` 行为零变化。<br>**3b**（`Core/Compare/BitOperationEx.cs`，2 处）`ArrayCompare<T>`（现 **221** 行）与 `Handle<T>`（现 **318** 行）**同时**加 `where T : unmanaged` ⇒ 消掉 **CS8500 ×6**。⚠️ **只加一个只能消 4 条**（`Handle` 的形参同样是 `T*`）。<br>⛔ **3a 带出的连带坑**：改成 `throw;` 后 `catch (Exception ex)` 的 `ex` 变成未使用 ⇒ **会新引入 CS0168 ×2**。这是**本次改动自己造成的**警告（与"别人本来就有的隐患"不同）⇒ 必须当场清掉：去掉参数名（`catch (Exception)`），并在原注释处写明"要记录 `ex` 就把参数写回来"。<br>✅ **净警告变化 = 0**：`Extension.csproj` 去重后 **123 → 121** 条（= 恰好抵掉那 2 条新 CS0168），**CA2200 / CS8500 / CS0168 三项实测全部归零**。<br>用例：`EventHelperTests` 2 条断言**反转**为"栈保留处理器帧"（⚠️ 依赖 `ThrowingHandler` 是**命名方法** —— lambda 的编译器生成名不会出现在栈里，断言会假红）；`BitOperationExTests` **+1 条源码护栏**（钉住"**恰好 2 处代码行**带 `where T : unmanaged`"，只数非 `///` 行）。`Extension.Tests` **917 → 918 全绿** |
| **36** | ✅ **修 `AntiTamperEx.VerifyFile` 的 null 兜底**（§5.29 第 29 条）—— 用户 2026-09-18 **打包**授权（"低风险加固包"） | ✅ `VerifyFile` 开头补 `if (string.IsNullOrWhiteSpace(expectedSha256Hex)) return false;`，与 `VerifyEntryAssembly`（现 35 行）**同口径**（fail-closed）。<br>⛔ 根因：`VerifyFile(path, null)` 会在 `expectedSha256Hex.Trim()` 处抛 `NullReferenceException`，而 NRE **不在 `IsHashUnavailable` 白名单里** ⇒ 逃出 catch ⇒ 同类两个"校验"方法对同一种坏输入给出**不同结果**（一个 `false`、一个 NRE）。<br>⚠️ **行为变化仅限 `null`**：由"抛 NRE"变为"返回 `false`"；空串 / 空白串修前就返回 `false`，结果不变。`path` 为 `null` 时 `File.Exists(null)` 本就返回 `false`，**不在本次范围**。<br>用例：**重写 1 条**（原断言 `Assert.Throws<NullReferenceException>` ⇒ 改为 4 条 `Assert.False`）+ **新增 1 条源码护栏**（直指根因那一行）。`Extension.Tests` **918 → 919 全绿** |
| **37** | ✅ **修 `AntiDebugEx.IsTimingAnomaly` 的阈值下限**（§5.29 第 27 条）—— 用户 2026-09-18 **打包**授权（"低风险加固包"） | ✅ `thresholdMs <= 0` ⇒ 抛 `ArgumentOutOfRangeException`（与本模块"参数错误一律抛出、不静默吞掉"的口径一致 —— 见 `IsProbeUnsupported` 的注释："其余异常继续抛出，避免掩盖真实缺陷"）。<br>⛔ 根因：本方法固定 `Sleep(100)` ⇒ `thresholdMs = 0` 时 `ElapsedMilliseconds > 0` **必然成立** ⇒ **恒返回 true**；调用方从配置读来 0（"未配置"）会立刻误报，且**无法与真的被调试区分**。<br>⚠️ **修后仍有一条同类隐患（已记录、未修）**：阈值 **1..99** 同样恒真；本次只拦 ≤ 0 这个哨兵值（拦 1..99 要引入"最小阈值"策略，属行为变更）。<br>⚠️ **零生产调用点**（全仓只有测试引用）⇒ 下游影响面为零。<br>用例：原 1 条**拆成 2 条**（"非正数直接抛" + "低于 `Sleep` 时长仍恒真，已记录未修"）。⚠️ 新用例只断言阈值 **1 / 50** —— `Sleep(100)` 保证"至少 100ms"，而阈值正好取 100 时是 `> 100`，理论上可能恰好相等 ⇒ **不写进断言，避免偶发 flaky**。`Extension.Tests` **919 → 920 全绿** |
| **47** | ✅ **修 `SingletonInstanceWithFactory<T>` 的双重检查锁补 `volatile`**（§5.30.1 第 3 条）—— 用户 2026-09-18 **打包**授权（"低风险加固包"） | ✅ `private static T _instance;` → `private static volatile T _instance;`（`where T : class` ⇒ 引用类型，`volatile` 合法；**只加关键字、不改任何逻辑**）。<br>⛔ 根因：双重检查锁定在**锁外**读 `_instance`，缺 `volatile` 时没有获取/释放语义 ⇒ 可能读到"引用已非 null、但对象内部字段尚未发布"的**半初始化对象**。同文件的 `ThreadSafeSingleton<T>` 一直是 `volatile`，此处属**遗漏**。<br>⛔ **同轮带出 序 73**：`SingletonInstanceAsync<T>` 的**锁外快路径**（`if (_initialized) return _instance;`）读的是两个**都非 `volatile`** 的字段，写入顺序 `_instance = await factory()` → `_initialized = true` ⇒ 可能看到 `_initialized == true` 而 `_instance` 仍为 `null`。按硬规则 1 **只记录、未修**，登记为 **序 73** 进待定表。<br>⚠️ **改这条用例时踩到的坑**：原用例是"全文两条相反 `Assert.Contains`"，修后**会假通过** —— 同文件 `SingletonInstanceAsync<T>` 里那个非 volatile 的 `_instance` 正好能把 `Contains("private static T _instance;")` 喂饱。故改为**按内部类切段**核对，并把"异步版仍缺"钉成显式断言（⚠️ **该反向断言已在 序 73 修复时反转成正向护栏**，见第 73 条）。用例数不变（**1 → 1**） |
| **28** | ✅ **修 `SafeMemoryHandle.GetPointer()` 的"释放后返回悬垂指针"**（§5.28 第 2 条）—— 用户 2026-09-18 **打包**授权（"极低风险三条包"，与序 43 / 57 / 64 / 65 同一批） | ✅ 判据 `IsInvalid` → **`IsInvalid \|\| IsClosed`**（1 文件 / 1 行）。<br>⛔ 根因：`SafeHandle.IsInvalid` 读的是句柄**值**（0 或 -1），而 `Dispose()` **并不把内部 `handle` 字段清零** ⇒ 实测释放后 `handle` 仍是释放前那个非零地址（`1767847578464`）⇒ 检查失效，调用方拿到**悬垂指针**，解引用即 use-after-free（轻则垃圾数据，重则 `AccessViolationException` **崩进程**、catch 不住）。<br>⚠️ **为什么是 `\|\|` 而不是直接换成 `IsClosed`**：`IsInvalid` 额外覆盖"从未分配成功"（`AllocHGlobal` 返回 0）这种**既未释放、也无有效指针**的情形，丢掉它会漏一档。<br>⚠️ **`ReleaseHandle()` 里的 `IsInvalid` 刻意不动**：CLR 释放时**至多调用它一次**，执行到它时 `IsClosed` 必然还是 `false`；那里 `IsInvalid` 表达的语义（"当初没分配成功 ⇒ 释放失败"）是对的。<br>✅ 全仓**零生产调用点**。用例：**反转**（原断言"不抛且返回同一个悬垂地址" ⇒ 改断言 `Assert.True(handle.IsClosed)` + `Assert.Throws<ObjectDisposedException>`）。⚠️ **刻意不断言 `IsInvalid == false`** —— 那描述的是 BCL 实现细节，换运行时版本会假红 |
| **43** | ✅ **修两处 `Dispose(bool)` 里的 `is IDisposable` 死分支**（§5.30.3 第 32 条 + §5.30.5 第 62 条）—— 用户 2026-09-18 **打包**授权（"极低风险三条包"） | ✅ **2 个文件 / 2 处**：`FilteredColleague` 补 `else if (_innerColleague is ColleagueBase cb) cb.Dispose();`；`FilteredSubscriber<T>` 补**两个** `else if`（`SubscriberBase<T>` + `DelegateSubscriber<T>`）。<br>⛔ **走方案②（仓内闭环）**：**不动** `IColleague` / `ISubscriber<T>` 的公开契约 —— 让接口继承 `IDisposable` 属**破坏性变更**（全仓所有实现类都要补成员），方案①被否。<br>✅ **前提用硬证据复核过**：全仓 `grep IDisposable` 在 `MediatorPattern.cs` 里**只命中 `IMediator : IDisposable`（`:13`）**、在 `PublishSubscribePattern.cs` 里只命中 `IMessageBroker<T>` ⇒ 同事 / 订阅者体系**一个都没声明** ⇒ 死分支成立（摘要里曾误记"`ColleagueBase` 实现了 `IDisposable`"，已按 grep 结果纠正）。<br>⛔ **最容易漏的一点**：`DelegateSubscriber<T>` **不**派生自 `SubscriberBase<T>`（两者平行实现 `ISubscriber<T>`）⇒ **只补基类那个分支会漏掉它**，故为它**单独加了一条用例**。<br>⚠️ 用 `else if` 串起来 ⇒ 将来若出现"既声明 `IDisposable` 又是这两个类型"的类也只释放一次。✅ 两个包装器在生产代码里都**没有 `new` 的调用点** |
| **57** | ✅ **修 `FilteredSubscriber<T>.Name` 的固定字面量**（§5.30.5 第 63 条）—— 用户 2026-09-18 **打包**授权（"极低风险三条包"） | ✅ 统一成 `$"Filtered({内层名})"`：内层是 `SubscriberBase<T>` / `DelegateSubscriber<T>` 时分别取各自 `Name`，都匹配不上时退回**运行时类型名**。<br>⛔ **原计划写的 `$"Filtered({_inner?.Name})"` 编译不过** —— `ISubscriber<T>` 上**没有** `Name` 成员（§5.30.5 第 52 条那一族的接口设计）⇒ 只能按具体类型取。这也正是**为什么必须先探再改**。<br>⛔ 原描述只说了"非 `SubscriberBase<T>` 时返回固定字面量"，实测**另一个方向也不对**：内层**是** `SubscriberBase<T>` 时返回的是内层**裸名字**，与内层本身**完全同名** ⇒ 看不出"套了一层包装"。<br>✅ 下游：`Name` 生产代码**零调用点**。用例：**重写**（改成 4 条断言，含 `Assert.NotEqual` 钉住"两个包装器名字必须不同"，以及第三方实现退回类型名那一路） |
| **64** | ✅ **修 `AdvancedFactoryBase.CreateProduct` 的误导性异常**（§5.30.1 第 9 条）—— 用户 2026-09-18 **打包**授权（"极低风险三条包"） | ✅ 在"创建器存在"分支内、`if (product != null)` **之后**单独抛 `InvalidOperationException($"工厂 '{FactoryName}' 中ID为 '{productId}' 的产品创建器返回了 null。")`，与"创建器不存在"的 `KeyNotFoundException` **可区分**。<br>⛔ 根因：两种情况原来共用同一个 `KeyNotFoundException("…未找到ID为 'p' 的产品创建器")` ⇒ 明明 `RegisterProduct` 注册过却被告知"没注册"，排查被带偏。<br>⚠️ **改法是"最小外科手术"**：只在 `if (product != null)` 块**之后**插入新分支，**不重排**内层缩进（该区段有空行带尾随空白，重排会引入大量无意义 diff）。<br>✅ 下游：生产调用点只有 `Extension/samples/` 示例，且都不按异常类型 `catch`。XML doc 补了 `<exception cref="InvalidOperationException">`。用例：**重写**并带一条**对照组**（"真的没注册时仍是 `KeyNotFoundException`"） |
| **65** | ✅ **修 `Products.cs` 的 `Collaborate` 对 `null` 零防御**（§5.30.1 第 6 条）—— 用户 2026-09-18 **打包**授权（"极低风险三条包"） | ✅ 两个类各补 `if (productA == null) throw new ArgumentNullException(nameof(productA), "协作对象不能为 null。");`。<br>⛔ 根因：直接取 `productA.Name` ⇒ 裸 `NullReferenceException`（无参数名、无"哪个参数错了"的信息）。<br>⚠️ **两个类的 `Collaborate` 方法体逐字相同** ⇒ 用 `replace_all` 一次改两处（漏一个就还是裸 NRE）。<br>✅ 全仓**零生产调用点**。用例：**重写**（`Assert.Throws<ArgumentNullException>` + 断言 `ParamName == "productA"`） |
| **60** | ✅ **补 4 处语义 XML 注释**（§5.30.5 第 59/60 条 + §5.30.3 第 33 条）—— 用户 2026-09-18 **打包**授权（第三批「语义注释 + 单点零调用修复」） | ✅ **2 个文件 / 4 处**，**纯注释、零行为变化**：<br>① `AdvancedPublishSubscribePattern.cs` 的 `TopicFilter<T>` —— 写清「空 topic **既不是全通过也不是全拦**：白名单为空 或 含空串才放行」。<br>② 同文件的 `PriorityFilter<T>` —— 写清「**最低优先级**（`>=`）语义，传 `High` 时 `Critical` 也会过」。<br>③ `AdvancedMediatorPattern.cs` 的**非泛型 `PriorityFilter`** —— `>=` 语义与 ② **逐字相同**。<br>④ 同文件的 `TypeFilter` —— 空 `MessageType` 语义，与 ① 同型。<br>⛔ **文档原写「1 个文件 / 2 处」、理由是「两个 Advanced 各一份」—— 理由是错的**：`TopicFilter<T>` / `PriorityFilter<T>` **同在** `AdvancedPublishSubscribePattern.cs`（`:169` / `:191`）；`AdvancedMediatorPattern.cs:76` 是**另一个非泛型 `PriorityFilter`**（不同类、不同接口）。经用户确认**扩到 4 处**，把同族兄弟一次写清。<br>⚠️ 第 ④ 处（`TypeFilter`）在待定表里**没有对应条目**（§5.30.3 第 33 条只记录、未排期）⇒ 本次属**顺带补齐**，已在 §5.30.3 表头标注。<br>✅ 无下游影响（注释不参与编译语义）；两处**源码护栏用例**（`PatternsObserverMediatorTests:965` 断言 `FilteredColleague` 那行 `is PriorityMessage`、`PatternsPublishSubscribeTests:646` 断言 `GetSubscriberPriority` 签名）**都不在编辑区域内**，已核对不会变红 |
| **8b** | ✅ **修 `ByteEx.ToBytes(this float)` 的「任何输入必抛」**（§5.13 第 1 条）—— 用户 2026-09-18 **打包**授权（第三批；⚠️ 授权**只覆盖这一处**） | ✅ **1 个文件 / 1 处**：`stackalloc byte[4]` **保持不变**（`float` 的 IEEE754 就是 4 字节），改 `BitConverter.SingleToUInt32Bits` + **`WriteUInt32BigEndian` / `WriteUInt32LittleEndian`**（原为 `WriteInt64*`，要求 ≥ 8 字节 ⇒ **任何输入都在边界检查处抛 `ArgumentOutOfRangeException`**）。<br>⚠️ **文档原「修法」里并列的另一个选项「改 `stackalloc byte[8]`」是错的** —— 那会让 `float` 返回 **8 字节**，与 `ToDouble` 的 8 字节混淆。新用例**专门断言 `Length == 4`** 钉住这一点。<br>✅ 该处**全仓零调用**（`grep -rn` 全仓确认）⇒ 下游影响面为零。<br>⛔ **同项的另两处（`ToASCII` / `ToHexBools`）未修、也不在本批授权内** —— 侦察时发现它们**都有真实生产调用点**（`ToASCII` 9 处 / `ToHexBools` 1 处），文档原写「三者全仓零调用」**是错的**，已在 §5.13 加勘误块并重定级。<br>用例：**重写** 1 条（原 `ToBytes_float_必然抛异常_是个不可用的死方法` 断言的是**缺陷本身** ⇒ 改为 `ToBytes_float_按IEEE754_固定4字节`，含大小端位模式、符号位、**长度 = 4**、与 `ToFloat` 往返、`double` 对照组）⇒ **用例数不变** |

| **8b**<br>（续） | ✅ **修 `ByteEx.ToASCII` 的「就地反转调用方数组」**（§5.13 第 2 条）—— 用户 2026-09-18 **打包**授权（**第四批「副作用消除」**；⚠️ 授权**只覆盖这一处**，**不**覆盖同项的 `ToHexBools`） | ✅ **1 个文件 / 1 处**：大端分支由 `data.AsSpan().Reverse()`（**原地**改写调用方数组）改为 **`(byte[])data.Clone()` 之后反转副本** ⇒ 调用方数组不再被触碰。<br>⚠️ **同批必须改写 1 条用例**：`ToASCII_大端会就地反转调用方数组_不是纯函数` 断言的是**副作用本身**，改后必红 ⇒ 重写为 `ToASCII_大端反转副本_不改动调用方数组`（新增「调用方数组未被改动」「幂等」「小端同样不碰」三条断言）⇒ **用例数不变**。<br>⛔ **空引用契约**：原实现让 `null` 落到 `Encoding.UTF8.GetString(null)` 抛 `ArgumentNullException`。改法是**开头显式** `throw new ArgumentNullException(nameof(data))` —— 既保住该契约，又让编译器在后续路径上确定 `data` 非空。⚠️ 若图省事写成 `if (data != null && …)`，会**净增 1 条 `CS8604`**（实测踩过）。<br>✅ **10 处调用点逐个核对 ⇒ 返回值全部不变**：4 处走 `Little_Endian`（本就不反转）；其余是 `return x.ToASCII()` / `…ToASCII(…).Trim()`，且 `data` 之后不再使用。⚠️ **「会反转」的两条入口（`WriteWithReply(string,int)` / `Read()`）全仓零调用** ⇒ 不改变任何**可达**路径的行为。<br>⚠️ **顺带消掉一个隐患**：`SerialHelper.DataReceived` 里**同一个数组**被三个消费者共享（Channel 队列 / `ValueTaskSource` / 事件订阅者，后者在 `Task.Run` 里**异步**读）⇒ 原实现构成**无锁数据竞争**，改副本后消失（详见 §5.13）。 |
| **8b**<br>（结案） | ✅ **`ToHexBools` 定案「无需改代码」**（§5.13 第 3 处）—— 2026-09-18 **第五轮只读侦察**；⚠️ **本行是结论，不是代码改动**（零行代码变更） | ✅ **结论**：`& 0x10` / `& 0x01` 正是三菱 MC 协议（Qna 3E）**位软元件的 nibble 打包格式**（每点占 4 bit，有效位在 nibble 的 bit0）⇒ 实现**是对的**，只有名字误导。<br>⛔ **判据是「两条路径位宽对照」**：`MC3EHelper.cs:180-184`（Qna 3E，读**位软元件** ⇒ `ToHexBools` 每字节 2 点）vs `MCHelper.cs:170-178`（读**字**后按 16 位展开 ⇒ `Length * 16`）—— **位宽差 8 倍是协议差异，不是缺陷**。<br>⛔ **否决「补全位」**：唯一调用点 `MC3EHelper.cs:182` 属实机 PLC 通讯路径，改位宽会打断通讯（真机验证前不得动）。<br>⛔ **同轮顺带发现两条隐患（只记录、未改）**：① `ConfigValueArray`（`MCBase.cs:99-130`）缺 `case B` / `case W` ⇒ 读路径的 `case B`（`MCHelper` 还有 `case W`）**不可达**；② **奇数点数**批量读时 `ToHexBools` 会多返回 1 个 bool，`Array.Copy`（`MCBase.cs:372`，`offest1 = startAddr` 绝对地址）边界上可能越界。（把 8b 彻底收口，消除「最后一个接线即踩的雷」这个错误预期）<br>✅ **验证记录**：`Extension.sln` **0 错误**；`Extension.Tests` **921/921 绿** |
| **73** | ✅ **修 `SingletonInstanceAsync<T>` 锁外快路径缺 `volatile`**（§5.30.1 第 73 条；⚠️ **修序 47 时带出的新发现**）—— 用户 2026-09-18 **第五批打包**授权（「单点可见性修复」；⚠️ 该批授权**只覆盖这一项**） | ✅ **1 个文件 / 3 处**：`Patterns/SingletonPattern/SingletonPattern.cs` 的 `SingletonInstanceAsync<T>` 三字段全部加 `volatile`（`private static volatile T _instance;` / `private static volatile bool _initialized = false;` / `private static volatile bool _initializationFailed = false;`；`where T : class` ⇒ 引用类型，`volatile T` 合法）。<br>⛔ 根因：`GetInstanceAsync` 开头是**锁外快路径**（`if (_initialized) return _instance;`），写入顺序是 `_instance = await factory()` → `_initialized = true`，三个字段**都不是 `volatile`** ⇒ 缺 release / acquire 语义 ⇒ 另一线程可能看到 `_initialized == true` 而 `_instance` 仍为 `null`（**拿到 null 单例**）。`volatile` 写带 release、`volatile` 读带 acquire ⇒ 正好把「`_instance` 写 → `_initialized` 写」与「`_initialized` 读 → `_instance` 读」两对顺序钉住。<br>⚠️ **只改可见性、不改语义**：`_initializationFailed` 一旦置位**永不复位**（"失败后不重试"）的既有行为**一行未动**（第 2 条描述的行为不变）—— 当初"担心会牵连语义"的顾虑经复核**是多余的**。<br>✅ **下游影响面为零**：`Singleton.GetInstanceAsync<T>` 全仓**唯一**生产调用点是 `Extension/samples/DesignPattern.Samples/SingletonPattern/SingletonPatternExample.cs:109`（示例、单线程，竞态不可达）。<br>用例：**改写 1 条**（把「异步版仍缺」的**反向护栏**反转成**正向护栏**，并把作用域从 `source.Substring(asyncIdx)`（**一直到文件末尾**，会串到后面 `ThreadSafeSingleton<T>` 的同名字段 ⇒ 可能**假通过**）**收紧到类体内** —— 新增 `asyncEnd = source.IndexOf("class SingletonBase<T>", asyncIdx)` 后按 `//` 过滤注释行再断言）⇒ 用例数不变。`Extension.sln` **0 错误**；汇总行 **123 个警告 / 0 个错误**；**归一化集合比对 `ONLY-NEW = 0 / ONLY-OLD = 0`**；`Extension.Tests` **921 / 921 通过 / 0 失败 / 0 跳过** |
（第六轮 910 → 序 12i 加 1 条 → **911** → 序 41 加 3 条 → **914** → 序 72 加 2 条 → 序 27 加 1 条 → **917**
→ 序 3b 加 1 条 → **918** → 序 36 加 1 条 → **919** → 序 37 把 1 条拆成 2 条 → **920**
→ 序 43 为 `DelegateSubscriber<T>` 这个**平行分支**补 1 条 → **921**）。
⚠️ 本批另外 4 项（序 28 / 57 / 64 / 65）都是**改写既有用例**（原来断言的是缺陷本身）⇒ 用例数不变，只增加断言。
✅ **第三批（序 60 / 序 8b）同样不改用例总数**：序 60 是**纯注释**（无用例涉及）；
序 8b 把 `ByteExTests` 里那 1 条**重写**（`…_必然抛异常_是个不可用的死方法` → `…_按IEEE754_固定4字节`）
⇒ `Extension.Tests` 仍是 **921 / 921 绿**、**0 失败 0 跳过**；
并单独跑了一次过滤验证：`--filter "FullyQualifiedName~ToBytes_float"` ⇒
`已通过 Extension.Tests.ByteExTests.ToBytes_float_按IEEE754_固定4字节`、**测试总数 1 / 通过数 1**（旧用例名已消失）。
✅ **第四批（序 8b 的 `ToASCII`）同样不改用例总数**：那 1 条断言**副作用本身**的用例被**重写**
（`ToASCII_大端会就地反转调用方数组_不是纯函数` → `ToASCII_大端反转副本_不改动调用方数组`）
⇒ `Extension.Tests` 仍是 **921 / 921 绿**、**0 失败 0 跳过**；
并单独跑了一次过滤验证：`--filter "FullyQualifiedName~ToASCII"` ⇒ 新用例名出现、旧用例名消失。
✅ **警告数交叉验证（2026-09-18，序 3 修复前后）**：`Extension.csproj` 实测 **129 → 121 条**，
差值 **8 = 6 条 CS8500 + 2 条 CA2200** —— 与"本次要消掉的警告数"**精确吻合** ⇒ 既证明目标警告**全部归零**，
也证明**没有引入任何新警告**（序 3a 带出的 CS0168 ×2 已当场清掉，净变化 0）。
✅ **第二批（序 28 / 43 / 57 / 64 / 65）同样做了这个交叉验证**：`Extension.csproj` 实测 **121 → 121**，
**净变化 0** —— 本批**既不消警告、也不该引入警告**（改的是逻辑，不是签名 / 修饰符），数字吻合即证明没引入。
✅ **第三批（序 60 / 序 8b）同样做了这个交叉验证**：`Extension.csproj` 实测 **121 → 121**，**净变化 0**。
本批改的是**注释**与**一个零调用方法的方法体**，既不消警告、也不该引入警告。
✅ **第四批（序 8b 的 `ToASCII`）**：`Extension.sln` 口径唯一警告 **123 → 123**，且做了**集合比对** ⇒
**ONLY-NEW = 0 / ONLY-OLD = 0**（逐条完全一致）。⚠️ **首次尝试并非如此**：守卫写成
`if (data != null && …)` 时**净增 1 条 `CS8604`**（`ByteEx.cs(154,44)`）——
**只比总数会把它误当成「原始计数 124 vs 唯一 123」的口径差异**。这是「必须比集合」的又一实证。
⚠️ 本批**首次用集合比对而非只比总数**（防「消一条旧的、冒一条新的」互相抵消）：
`comm` 比对前后两份**唯一警告清单** ⇒ **差异恰好只有一条**：
`AdvancedMediatorPattern.cs(121,16)` CS8618 → **`(143,16)`** —— 行号**正好下移 22 行**，
等于我在它上方插入的行数（`TypeFilter` 补 12 行 + 非泛型 `PriorityFilter` 补 10 行）⇒
**是同一条既有警告因插入行而平移，不是新增**。其余 120 条逐字未变。
⚠️ 另：本次构建的是 **`Extension.sln`**（含 `Extension.Vision.Cognex`），唯一警告数显示 **123** ——
多出的 2 条是该工程既有的 **MSB3270**（`Cognex.VisionPro(.Core)` 是 x86、工程是 MSIL），
与本次改动无关；**按 `Extension.csproj` 口径才是可比的 121**。
⚠️ 还要**逐条确认那 3 条落在改动文件上的警告是"本来就有的"**（防"消一条旧的、冒一条新的"互相抵消）：
`AdvancedMediatorPattern.cs(121,16)` CS8618、`AdvancedMediatorPattern.cs(30,92)` CS8625、
`AdvancedPublishSubscribePattern.cs(31,81)` CS8625 —— 三个行号**都在我编辑的行号之上** ⇒ 既有警告，未被触碰。
✅ **第五批（序 73）同样做了这个交叉验证**：`Extension.sln` 口径唯一警告 **123 → 123**，且做了**集合比对** ⇒
**归一化后 `ONLY-NEW = 0 / ONLY-OLD = 0`**。⚠️ **原始集合比对并非如此**：显示 `ONLY-NEW = 3 / ONLY-OLD = 3` ——
`SingletonPattern.cs(128,35)` / `(201,35)` / `(238,29)` vs `(116,26)` / `(189,35)` / `(226,29)`（CS8618 ×2 + CS8625 ×1）。
把**行号列号归一化**（`re.sub(r'\(\d+,\d+\)', '(:)', …)`）后差异**归零** ⇒ 三条都是**同一条既有警告因插入注释而平移**：
行号一律 **+12**（= 我在 `SingletonInstanceAsync<T>` 上方插入的注释行数），列号只有第一条从 **26 → 35**（= 该行从
`private static T _instance;` 变成 `private static volatile T _instance;`，多出 9 个字符），另两条列号**逐字未变**。
⚠️ 这是「**集合比对必须归一化行列号**」的实证 —— 只比总数（123 vs 123）看不出，只比集合**不归一化**又会误报 3 条新增。
⚠️ 本批**未改任何产品逻辑**（只加 `volatile` 关键字 + 注释），因此**既不消警告、也不该引入警告**，数字吻合即证明没引入。
另 `SafeMemoryHandle.cs` / `Products.cs` / `AdvancedFactoryPattern.cs` **零警告** ⇒ 本批新增的
`<remarks>` / `<list>` / `<exception>` XML 文档**没有产生 CS1570 / CS1573 / CS1591**。
⚠️ 原文写的"129 条**既有** nullable 告警"表述不准确 —— 129 里含那 8 条待修警告，**真正的既有基线是 121 条**。
⚠️ **口径**：`dotnet build Extension/Extension.Tests/Extension.Tests.csproj -v:m` 落盘后取 MSBuild 汇总行
（`grep ": warning " | sort -u | wc -l` = **121**）；⚠️ 日志里每条警告会**打印两次**，行数是 **242** —— **别按行数报数**。
121 条全部是 nullable / 隐式接口实现一类（CS8603 / 8600 / 8618 / 8604 / 8625 / 8602 / 8601 / 8767 / 8714 / 8620 / 8612 / 0219），
**CA2200 / CS8500 / CS0168 三项实测归零**。`Extension.Tests` **工程自身** 0 警告。
全量 `Frame.sln` 对照基线（0 错误 / 1,439 警告）**0 错误**。

**✅ 序 3a / 3b / 27 / 32 / 36 / 37 / 47 / 72 的「下游影响面」已逐处实测（2026-09-18）** —— 八处全部为**零影响**
（序 32 是"只会变好"），因此**不需要**重跑全量 `Frame.sln` 就能判定没有下游回归：

| 改动 | 全仓下游调用方（已排除 `obj/` `bin/` 与测试工程） | 影响 |
|---|---|---|
| 序 3a（`TriggerEvent` 的 `throw ex;` → `throw;`） | **4 处，全在 `Secs/`**：`Secs/Secs/Manager/StatusManager.cs:109,138`、`Secs/Secs/Order/S2/S2F23.cs:63`、`Secs/Secs/SecsControl.cs:324` | **零**：`throw;` 与 `throw ex;` 抛的是**同一个异常对象**，类型与消息**逐字不变** ⇒ 下游 `catch` 判定零变化。`StatusManager` 两处外面是 `catch (Exception ex) { throw new Exception($"…{ex.Message}", ex); }` ⇒ 只看 `Message`，**不受影响**；`SecsControl:324` 的异常由接收循环兜底（S9Fx），同样只看类型/消息。**唯一区别是 `StackTrace` 变长了**（这正是修复目的 —— 处理器帧回来了） |
| 序 3b（`ArrayCompare<T>` / `Handle<T>` 加 `where T : unmanaged`） | **仓内 4 处，全在同一文件内**：`BitOperationEx.cs:100,132,164,196` 的 `fixed (byte*/ushort*/short*/bool* …)` + `:290` 的内部递归调用 | **零**：约束只**收紧**调用面，4 个实参全部是 `unmanaged` 指针类型，递归调用继承自身约束 ⇒ 全部满足。**运行时行为逐位不变**（只影响编译期） |
| 序 36（`VerifyFile` 补 null 短路） | **0 处**（`AntiTamperEx.VerifyFile` 全仓**零调用**；`grep` 命中的另一条是 `LicenseManagerEx.VerifyFile`，**同名不同类**） | **零** |
| 序 37（`IsTimingAnomaly` 加下限） | **0 处**（全仓只有测试引用） | **零** |
| 序 47（`_instance` 加 `volatile`） | `SingletonInstanceWithFactory<T>` 是 `internal`，只经 `Singleton.GetInstance<T>(factory)` 暴露；该方法全仓**唯一**下游是 `Extension/samples/DesignPattern.Samples/SingletonPattern/SingletonPatternExample.cs:89`（`Singleton.GetInstance(() => new ExampleService("自定义服务"))`） | **零**：只加 `volatile` 关键字、**不改任何逻辑**；该样例传的是**非 null 工厂**，与"锁外读到半初始化对象"这个竞态无关（样例本来就是单线程） |
| 序 72（两个类的 `_stopped` 闸门） | `samples/…/ProducerConsumerPatternExample.cs:104`（`EnhancedProducerConsumer`）、`…/AdvancedProducerConsumerPatternExample.cs:58`（`ConfigurableProducerConsumer`）—— **各自只调一次 `StartAsync`，全文不出现 `.Stop()`** | **零**：新闸门只在 `Stop()` / `Dispose()` 之后才可能命中，而这两条路径样例根本没走 |
| 序 27（`CloseProcess` 参数校验） | `Device/Device/PrintSoftware/BartenderHelper.cs:51`、`WPFControl/…/Printer/PrintManagerService.cs:60` —— **两处都是 `ProcessEx.CloseProcess("BarTender")` 字面量** | **零**：非 `null`、非空串 ⇒ 新校验不触发 |
| 序 32（`Features` / `Extra` 归一化） | `Extension/LicenseTool/MainWindow.xaml.cs:221`（厂商签发工具调 `Verify`） | **只会变好**：归一化只在原本要抛 NRE 的坏数据上生效 ⇒ 把"崩溃"变成"按全功能放行" |
| 序 28（`GetPointer` 判据加 `IsClosed`） | **0 处**（全仓 `grep GetPointer()` 只命中测试 + `Vision/DPLibrary_Halcon_F48/Tools/AI/Invi/InviHelper.cs:100` 一行**注释掉的**代码） | **零**：`IsClosed` 只在**已经释放**的对象上才为 `true`，而修前那种情况本来就返回悬垂指针 ⇒ 行为变化**只发生在原本就错的路径上** |
| 序 43（两处包装器补"按具体类型释放"） | **0 处**：`FilteredColleague` 只由 `AdvancedMediator.RegisterWithFilter` 创建（存进注册表）；`FilteredSubscriber<T>` 全仓**没有任何生产代码 `new` 它**（只有测试） | **零**：新增分支只在**包装器自己被 `Dispose`** 时触发；且 `ColleagueBase.Dispose` / `SubscriberBase<T>.Dispose` / `DelegateSubscriber<T>.Dispose` 全部**幂等**（各自有 `_disposed` 闸门）⇒ 即便被重复调用也无副作用。⚠️ 中介者 / 消息中心的 `Dispose` 都只**清空集合**、不遍历 `Dispose` 成员 ⇒ 不存在"级联释放"的新路径 |
| 序 57（`FilteredSubscriber<T>.Name` 加 `Filtered(...)` 前缀） | **0 处**（全仓 `Patterns/` 内唯一的 `.Name` 就是这行定义本身） | **零**：`Name` 无生产消费者；唯一变化是测试断言从 `"真实名字"` / `"FilteredSubscriber"` 改成 `"Filtered(真实名字)"` / `"Filtered(委托名)"` |
| 序 64（`CreateProduct` 创建器返回 null 改抛 `InvalidOperationException`） | `Extension/samples/DesignPattern.Samples/FactoryPattern/AdvancedFactoryPatternExample.cs:72,73,85,91,109`（**全是示例**，且**都不按异常类型 `catch`**） | **零**：只有"创建器存在且返回 `null`"这一条路径的**异常类型**变了（`KeyNotFoundException` → `InvalidOperationException`）；"创建器不存在"仍是 `KeyNotFoundException`（新增用例带**对照组**钉住） |
| 序 65（`Products.Collaborate` 补 null 校验） | **0 处**（全仓只有测试调用 `Collaborate`） | **零**：只有"传 `null`"这一条路径变了（裸 NRE → 带参数名的 `ArgumentNullException`） |
| 序 60（4 处语义 XML 注释） | **不适用**：注释不参与编译语义、无运行时行为 | **零** |
| 序 8b（`ByteEx.ToBytes(this float)`） | **0 处**（全仓 `grep -rn '\.ToBytes('` 后逐个核对**接收者类型**：命中的都是 `int` / `uint` / `long` / `double` / `string` 重载，**没有一处**接 `float`） | **零**：该重载修前**任何输入都抛** ⇒ 本来就不可能有能工作的调用方；修后只新增「可用」这一种可能 |
| 序 73（`SingletonInstanceAsync<T>` 三字段加 `volatile`） | `Singleton.GetInstanceAsync<T>` 全仓**唯一**生产调用点：`Extension/samples/DesignPattern.Samples/SingletonPattern/SingletonPatternExample.cs:109`（`await Singleton.GetInstanceAsync(async () => …)`）—— 其余命中全在 `Extension.Tests/PatternsSingletonFactoryTests.cs` | **零**：只加 `volatile` 关键字、**不改任何逻辑**；该样例是**单线程**且只调一次 ⇒ "锁外快路径读到未发布对象"这个竞态本来就不可能出现 |

> ⚠️ 对照：序 41（`ProducerConsumer<T>`）当时也是靠"产品代码零调用 + samples 从不 `Stop()`"判定的零影响 ——
> **同一套判据**。⚠️ 但**判据本身有保质期**：以后若有人给 samples 补 `Stop()` 演示，或新增下游调用方，
> 这张表就过期了 ⇒ **改这些 API 的语义前先重跑一遍这个 grep**。
> ⚠️ 序 36 / 37 两条"**零调用**"结论同样是**当天的快照**（硬规则 3：结论有保质期）。

| 时点 | `Extension.Tests` | `dotnet test Frame.sln -m` |
|---|---|---|
| 重排前 | 21 / 21 | 943 / 931 / **12 失败** |
| 补测第一轮后 | 430 / 430 | 1,135 / 1,123 / **12 失败** |
| 补测第二轮后 | 503 / 503 | 1,208 / 1,196 / **12 失败** |
| 补测第三轮后 | 581 / 581 | 1,286 / 1,274 / **12 失败** |
| 补测第四轮后 | 629 / 629 | 1,334 / 1,322 / **12 失败** |
| 补测第五轮后 | 694 / 694 | 1,399 / 1,387 / **12 失败** |
| **补测第六轮后** | 910 / 910 | **1,615 / 1,602 / 13 失败**（12 既有 DB + 1 条**时序敏感**用例） |
| **机械复核后（序 12i）** | **911 / 911** | 未重跑全量（本次只改 `TierGuardTests` + 文档；`Extension.Tests` 911 全绿） |
| **序 41 修复后** | **914 / 914** | 未重跑全量（本次只改 `ProducerConsumer<T>` + `PatternsProducerConsumerTests`；`Extension.Tests` **914 全绿**） |
| **序 72 / 27 / 32 修复后** | **917 / 917** | 未重跑全量（本次改 `ProducerConsumerPattern`×2 + `ProcessEx` + `LicenseManagerEx` + 3 个测试文件；`Extension.Tests` **917 全绿**、`Extension.sln` **0 错误**） |
| **序 3 / 36 / 37 / 47 修复后** | **920 / 920** | 未重跑全量（本次改 `EventHelper` + `BitOperationEx` + `AntiTamperEx` + `AntiDebugEx` + `SingletonPattern` + 4 个测试文件；`Extension.Tests` **920 全绿**、`Extension.sln` **0 错误**、`Extension.csproj` 警告 **129 → 121**、**CA2200 / CS8500 / CS0168 归零**） |
| **序 28 / 43 / 57 / 64 / 65 修复后** | **921 / 921** | 未重跑全量（本次改 `SafeMemoryHandle` + `AdvancedMediatorPattern` + `AdvancedPublishSubscribePattern` + `Products` + `AdvancedFactoryPattern` + 4 个测试文件；`Extension.Tests` **921 全绿**、`Extension.sln` **0 错误**、`Extension.csproj` 警告 **121 → 121（净变化 0）**） |
| **序 60 / 序 8b 的 `float` 修复后** | **921 / 921** | 未重跑全量（本次改 `AdvancedPublishSubscribePattern` + `AdvancedMediatorPattern`（**纯注释**）+ `ByteEx`（1 处方法体）+ `ByteExTests`（重写 1 条）；`Extension.Tests` **921 全绿 / 0 失败**、`Extension.sln` **0 错误**、`Extension.csproj` 警告 **121 → 121（净变化 0，集合比对差异仅为行号平移 22 行）**） |
| **序 8b 的 `ToASCII` 修复后（当前）** | **921 / 921** | 未重跑全量（本次只改 `ByteEx`（1 处方法体）+ `ByteExTests`（重写 1 条）；`Extension.Tests` **921 全绿 / 0 失败 0 跳过**、`Extension.sln` **0 错误**、`Extension.sln` 口径唯一警告 **123 → 123** 且**集合比对 ONLY-NEW=0 / ONLY-OLD=0**） |

> 第六轮 **+216 用例、零新增真实失败**（`Extension.Tests` 694 → 910：`PatternsObserverMediatorTests` 78 +
> `PatternsPublishSubscribeTests` 48 + `PatternsSingletonFactoryTests` 47 + `PatternsProducerConsumerTests` 39 +
> `UiControlsPlaceholderTests` 4），**未改任何产品代码**，**0 错误 0 警告**。
> 12 个失败逐条核对为**同一批 DB 环境依赖**，与产品代码无关：
> `DataManager.Tests` 的 `PostgresIntegrationTests` ×5 + `PostgresFeatureTests` ×5（报 `28P01` = 密码错）、
> `DataManager.Workbench.Tests` 的 `SqlViewModelSmokeTests` ×2（报连接超时）。
> 分程序集用例数：`Extension.Tests` **917** / `FlowEditor.Tests` 341 / `DataManager.Tests` 180 /
> `WPFCustomControls.Tests` 100 / `DataManager.Workbench.Tests` 58 / `Secs.Tests` 25 / `WPFControl.Tests` 1。
>
> ⚠️ **第 13 个失败已定性为"时序敏感"，不是本轮引入的回归**：
> `FlowEditorTest.ExecutionEngineTests.Cycle_RepeatsUntilCancelled` 的断言是
> `using var cts = new CancellationTokenSource(300);` + `Assert.True(xModel.Executions >= 2, …)`
> —— **墙钟 300 ms 内必须跑完两轮**。实测：`Frame.sln -m` 并行跑时失败（`实际 1 次`），
> 单独跑 1 次失败（`实际 0 次`）、**连跑 3 次全部通过**（327/323/325 ms）⇒ **同一条 dll、同样的输入、结果不同**。
> 且 `FlowEditor/` 下**没有任何本轮改动**（`git status` 只有 1 个无关的 `DispatchertimerExTests.cs`，
> `ExecutionEngineTests.cs` 最后改动是 09-14 的 `8f1ace9`）⇒ **与第六轮无关**。
> ⛔ 按硬规则 1 **不去改这条用例**（改它=动别人的测试契约），只在此登记；
> 后续若要稳定它，方向是"把 300 ms 预算改成事件驱动（等到 2 次执行再取消）"而不是调大数字。
>
> ⚠️ 第六轮第一遍编译有 **9 个错误**，第二遍有 **6 条断言失败**，全部是"我按直觉猜产品行为"猜错：
> ① `Assert.Throws<T>(Func<Task>)` 选到了**过时重载**（`CS0619` + `xUnit2014`）⇒ 包成 `Action`；
> ② `CompositeFilter<string>` 里塞 `TopicFilter<string>`/`PriorityFilter<string>`（`CS0266`/`CS1503`）；
> ③ `FilteredSubscriber` 的泛型实参写错；④ `FactoryManager.GetFactory<T>` 我以为是"类型不匹配就抛"，
> 实际探测类**本身就是**注册进去的实例 ⇒ 不抛；⑤ `FilteredColleague`/`FilteredSubscriber` 的
> `Dispose` 我以为是"会连带"，实际**分支不可达** ⇒ **这两条反而挖出新缺陷**；
> ⑥ `Mediator` 重复 Id 用例里 `Received` 的 `Key` 是**发送者标识**、`Value` 才是消息（我写反了）；
> ⑦ `lock(observable)` 用例第一版**挂了观察者** ⇒ 被卡住的是 `Notify` 链路而非 `Data` 本身。

> ⚠️ 第五轮第一遍有 **3 条断言失败，全部是"我按直觉猜产品行为"猜错**（§5.29 第 2/5/9 条）：
> ① `clockSkew` 我以为是宽限（实际提前判过期）；② GCM 篡改我以为抛 `CryptographicException`
> （实际抛子类 `AuthenticationTagMismatchException`，而 `Assert.Throws<T>` 是**精确类型匹配**）；
> ③ `RsaDecrypt("not-base64")` 我以为抛 `CryptographicException`（实际在 `Convert.FromBase64String` 就抛 `FormatException`）。
> **三条修正后都变成了更准确的文档** —— 这正是"先探再写"的价值。

### 待定（按风险排序）

| 序 | 动作 | 改动面 | 风险 | 收益 |
|---|---|---|---|---|
| **4** | `docs/` 收拢 4 份 md；`Extension.sln` 去掉 4 个死配置；对齐两个 sln 的 GUID（§6.C/§6.D/§6.E） | 配置 / 文档 | **低** | 消噪 |
| **5** | 清一次 `bin`（回收 129 MB 陈旧 Cognex，§6.B） | 构建产物（git 不跟踪） | **低** | 真回收 129 MB |
| **7** | `Contracts/*` → `readonly struct`/`record`；`ImageInfoMapping` 拆出 DTO 文件（§5.4/§5.5） | 3 文件 | **中**（序列化/二进制兼容） | 消除反模式 |
| **8b** | ✅ **已结案（2026-09-18，第五轮只读侦察）：`ToHexBools` 无需改代码** —— 原计划「只正名 / 补注释」；侦察后确认其 `& 0x10` / `& 0x01` **正是三菱 MC 协议位软元件的 nibble 打包格式**（每点占 4 bit，有效位在 nibble 的 bit0）⇒ **实现是对的，只有名字误导**（详见 §5.13 第五轮块 + §7 已完成表） | 0 个文件 / **零代码改动** | ✅ **无需改**（原标「中」已作废）：唯一调用点 `MC3EHelper.cs:182` 属**实机 PLC 通讯路径**，但**没有缺陷可修**；⛔ **「补全位」仍然禁止**（改位宽会打断通讯，真机验证前不得动） | 把 8b 彻底收口（消除「最后一个接线即踩的雷」这个错误预期） |
| **9** | 消除 `Patterns/` 的 7 个重复类型名（§5.6） | 2 个模式目录 | **中**（下游 `using` 会歧义） | 去重 |
| **10** | `Patterns/*/Examples` 命名空间与库命名空间脱钩（samples 自身命名空间仍叫 `Extension.DesignPattern.*.Examples`，§6.I 通路 2 的残留） | 12 个示例文件 | **低**（纯 samples） | 消歧 |
| **12** | 消 `ToHex` 重载陷阱（§5.11）：给 `BinaryEx.ToHex` 改名或给 `ASCIIEx.ToHex` 去掉缺省参数 | 1–2 个文件 + 下游 | **中**（下游 16 处调用点） | 消掉最隐蔽的运行时陷阱 |
| **13** | 消 `ToASC` 同签名二义性（§5.12）：给两个 `ToASC(string)` 之一改名 | 1 个文件 + 下游 | **中** | 消掉"必须全限定才能编译"的约束 |
| **14** | 修 `BitOperationEx` 两处缺陷（§5.15）：① 块内位移 `L*i` → `8*i`；② 循环守卫改**索引比较**（修空数组崩溃） | 1 个文件 / 2 处 | **低**（默认模式 Eight 下行为不变） | 消掉"改速度就漏报"+"空数组就崩" |
| **15** | 修 `ChannelHelper<T>` 行为（§5.17）：① 默认满模式改 `Wait`（或修注释）；② `Write` 检查 `TryWrite` 返回值（至少返回 `bool`）；③ `ClearAsync` 改"非阻塞排空"；④ `TryReadAsync` 正名 | 1 个文件 / 4 处 | **中**（① 会改变默认丢数据行为） | 消掉静默丢数据 + 永久挂起 |
| **16** | 修 `EventHelper`（§5.18）：① 超时路径补 `unregister`（放 `finally`）；② 取消路径的清理竞态；③ `public class` → `static class`；④ 两个重载默认超时对齐 | 1 个文件 / 4 处 | **低–中**（③ 会让 `new EventHelper()` 的调用点编译失败，需先扫下游） | 消掉处理器泄漏 |
| **17** | 修 `TaskEx`（§5.19）：① `WithTimeout` 的 `timeoutCts`/`linkedCts` 补 `using`；② `ThreadRun` 补参数校验 + 真正设置 `threadPriority`；③ `LongTimeRun`/`TimesRun` 改 `Task.Run`（或 `StartNew` + `Unwrap`） | 1 个文件 / 3 处 | **中**（③ 会改"await 立即返回"的既有行为） | 消掉 CTS 泄漏 + 让返回的 Task 真正代表循环 |
| **18** | 修 `RegisterEx` 的有损转换（§5.16）：`ToShortArray(this string)` 去掉 `.Where(x => x != 0)`（或改成显式长度前缀）；`ToStringArray` 与 `ToShortArray` 的编码口径统一 | 1 个文件 / 2 处 | **中**（下游若依赖"丢掉 0"的行为会变） | 消掉静默数据损坏 |
| **19** | 修 `FileEx.Write` 的 null 静默清空（§5.20）：`data` 为 `null` 时**先抛** `ArgumentNullException`（或明确当作"写入空文件"并在 XML 注释里写明） | 1 个文件 / 1 行 | **低**（下游若真在传 null 会由"静默丢数据"变成"立刻报错"，是期望行为） | 消掉最危险的一条静默数据丢失 |
| **20** | 修 `DirectoryEx`（§5.21）：① `GetParentSecond` 补第一层 null 检查（返回 `null`，与 `GetParent` 对齐）；② `CreatDir`/`GetPath` 的月日改 `ToString("00")`（**会改变既有目录名**，须先确认无历史目录依赖）；③ `DirCopy` 改 `Path.GetRelativePath` + `Path.Combine` 换算路径 | 1 个文件 / 3 处 | **中**（② 会改落盘路径；③ 修正后行为与当前"错位落盘"不同） | 消掉 NRE + 跨月排序错乱 + 文件错位落盘 |
| **21** | 修 `ZIPEX`（§5.22）：`GZIPExtractToString` 增加 `Encoding` 可选参数（默认保持 UTF-8 兼容），并补一个 `TryGZIPExtractToString` | 1 个文件 / 2 处 | **低**（纯增量，默认参数不变） | 让 GBK 负载可显式解码 |
| **22** | 修 `CloneEx`（§5.23）：`DeepClone` 对 `string` 根对象特判（`obj is string s ? s : ...`） | 1 个文件 / 1 行 | **低**（当前行为是抛异常，特判后返回原值） | 消掉"克隆一个 string 就炸" |
| **23** | 修 `NotifyPropertyChanged`（§5.24）：`OnPropertyChanged` 参数加 `[CallerMemberName]` | 1 个文件 / 1 行 | **低**（纯增量，显式传参仍有效） | 与现代 `ObservableObject` 口径一致 |
| **24** | 修 `CSVEx`（§5.25）：① `ParseCsvLine` 去掉 `\` 转义（或让 `EscapeCsvValue` 也转义 `\`，两侧对齐）；② 把 `""` 还原成一个 `"`；③ 去掉逐字段 `.Trim()`；④ `List<>` 分支按元素类型转换（而非一律 `List<string>`）；⑤ `ReadCsv` 不再把异常包成裸 `Exception`；⑥ `ExportToCsvAsync_String` 落实 `includeHeader`；⑦ 默认编码改无 BOM | 1 个文件 / 7 处 | **中–高**（下游 **7 处**调用点；②③ 会改变已落盘 CSV 的解析结果，需先确认存量文件格式） | 消掉"含路径/引号/空格的字段往返即损坏" |
| **25** | 修 `ScheduledDeletion`（§5.26）：① 子目录改用 `Directory.Delete(path, true)`（当前非递归 ⇒ **清理根本不生效**）；② `Log` 恢复输出或换成 `Trace`；③ `TryParseDateFromPath` 加年份范围校验（防 `1/2/3` 误判）；④ 空间分支的 `releasedMB` 改用字节比较 | 1 个文件 / 4 处 | **中**（①会**真的开始删文件**）<br>⛔ **2026-09-18 已把这条风险量化（读代码实测，未动任何代码）**：<br>· **只有 2 个生产调用点**，都在 `Packing Machine/`：`Business/Flow/AutoProcess.cs:193` + `Config/Flow/StartupFlow/05_初始化后台.cs:109`，两处都是 `ScheduledDeletion.DeleteExpiredFiles(GlobalData.Path_Image, GlobalData.SaveImageDay)`<br>· **根路径 = `GlobalData.Path_Image`，默认 `"D:\\Image"`**（`Data/GlobalData.cs:18`）<br>· **留存天数 = `GlobalData.SaveImageDay`，默认 `100`**（`Data/GlobalData.cs:22`）<br>· 同目录 `Infra/Tool/VisionTool.cs:91,142` 用 `DirectoryEx.CreatDir(GlobalData.Path_Image, FosbID)` **往里写现场图片**<br>⇒ **改成递归后，现场机器的 `D:\Image` 下会真的按 100 天留存开始删子目录**。这正是"一次性删掉历史图片"的路径与天数 —— 改之前必须先在测试目录验证 | 让清理真正生效 + 消掉误删风险 |
| **26** | 处置 `ScheduledDeletionEx`（§5.27）：它是 `internal` + 零引用 + **成功路径也抛异常** ⇒ 三个选项：(a) 原样保留只留文档；(b) 删（**违反硬规则 1，需用户明确点头**）；(c) 修好并让在用版改用它的失败策略 | 1 个文件 | **低**（零引用，改动不影响任何调用方） | 消掉一份"看起来能用其实必抛"的实现 |
| **29** | 修 `CpuMonitorEx`（§5.28 第 3 条）：`Dispose()` 里把 `isInitialized` 置 `false`、两个计数器字段置 `null`（并考虑 `Interlocked`/`volatile`） | 1 个文件 / 1 处 | **低** | 让 `Dispose` 真正生效（现在释放后仍读真实值、还会重新分配句柄） |
| **30** | 修 `FpsEx`（§5.28 第 4/5 条）：① 达到间隔时先判 `_stopwatch.IsRunning` 再决定是否 `Restart()`（别把 `Stop()` 撤销）；② `elapsed == 0` 时**不要**重置计数（保留到下一帧再算） | 1 个文件 / 2 处 | **低** | 消掉"Stop 后自己复活"+"高帧率下帧率恒为 0" |
| **31** | 修 `KeyValue` / `ImageInfo`（§5.28 第 7/8 条）：`KeyValue` 重写 `Equals`/`GetHashCode`（或改 `record`）；`ImageInfo` 的 `List<>` 成员改 `IReadOnlyList<>` 或整体改 `class`（与序 7 合并做） | 2 个文件 | **中**（下游比较语义会变） | 消除"值对象不按值比较"+"struct 浅拷贝串改" |
| **33** | 修 `AesEx.AesGcmDecrypt` 的长度下限（§5.29 第 3 条）：`NonceSize + TagSize + 1` → `NonceSize + TagSize`（加密侧允许空明文，解密侧必须对齐） | 1 个文件 / 1 行 | **低**（只放宽 1 字节下限） | 消掉"自己加密的空串自己解不开" |
| **34** | 修 `AesEx.AesEncrypt(null)` 的语义（§5.29 第 6 条）：改成抛 `ArgumentNullException`，或返回**可解密**的空内容密文（当前返回 `string.Empty`，在解密侧才炸） | 1 个文件 / 1 行 | **低–中**（改成抛会让"传 null"的调用点由静默变报错，是期望行为） | 消掉"加密成功、解密必炸"的语义不对称 |
| **35** | 修 `HashEx.Sha256Hex(null)` / `Sha512Hex(null)`（§5.29 第 7 条）：三个重载统一改为抛 `ArgumentNullException`（或返回 `null`）—— 当前返回空串，与"空内容的哈希"不可区分 | 1 个文件 / 3 处 | **中**（需先扫下游：`MachineCodeEx` 用到了 `Sha256Hex`，但走的是非 null 路径） | 消掉"用空串当哈希去比较"的静默 |
| **38** | 修 `AntiToolProcessEx.GetSuspiciousProcesses` 的句柄（§5.29 第 31 条）：① `IsSuspiciousToolRunning` 内部用完即 `Dispose`；② 或把公开 API 改成返回 `IReadOnlyList<string>`（不把 `Process` 泄漏给调用方） | 1 个文件 / 2 处 | **中**（② 会改公开签名，需先扫下游；`GetSuspiciousProcesses` 当前仓内零调用） | 消掉 27 个 `Process` 句柄的泄漏 |
| **39** | 补 `AntiToolProcessEx.SuspiciousProcessNames`（§5.29 第 32 条）：加 `procexp64` / `dotpeek32` / `systeminformer`（Process Hacker 2023 已改名 System Informer） | 1 个文件 / 1 行 | ⚠️ **2026-09-18 只读侦察后重定级：低**（原标「极低」过于乐观），理由三点：<br>① **不是纯增量**：`AppProtectionEx.CheckOnce`（`:96-99`）真的调 `IsSuspiciousToolRunning`，而 `Handle` 默认 `Action = Throw` ⇒ **名单变宽 = 更多运行环境被抛异常拒绝启动**，属**产品行为变更**（不是「补漏」）；<br>② **与文档自身结论冲突**：§5.29 第 32 条与本表「11e」行把「黑名单滞后」明确列为「**合理取舍、不是缺陷**」+「**只记录**」⇒ 修它等于推翻该结论；<br>③ **边际价值有限**：`processhacker` / `procexp` / `dotpeek64` **都已在名单**，新增三项只是它们的**改名 / 位宽变体** —— 真正的漏检是「工具会持续改名」，加 3 项只是把滞后窗口推后。<br>⚠️ 仍会**故意让 `AntiToolProcessEx_名单是27项…` 变红**，需同步改**用例名**与哨兵数字（27 → 30） | 补上漏检的 3 个常见工具 |
| **40** | 统一 `RsaEx` 的错误处理契约（§5.29 第 9/10 条）：① 明确"验签失败返回 `false`、其余抛异常"并写进类注释；② 让 `FromPrivateKey`/`FromPublicKey` 至少能识别 PKCS#8/SPKI 并抛出**带说明的**异常（当前只抛裸 `CryptographicException`） | 1 个文件 / 3 处 | **低**（① 纯注释；② 只改异常消息，不改成功路径） | 消掉"与 openssl 互操作时只看到 ASN.1 报错、查不出是格式不对" |
| **42** | ⛔⛔ 修 `Observer<T>.Update` 的**锁错对象**（§5.30.2 第 16/17 条）：`Update` 改锁 `Observable<T>` 内部的 `_lock`（或由 `Observable<T>` 暴露 `SyncRoot`），让"外层持锁"真的能保护数据 | 2 个文件 / 2 处 | **中**（改锁的范围会动并发语义；必须保留 `UpdateData` 在**锁外**调 `Notify()` 这一点，否则引入死锁） | 消掉"看着加了锁、其实没保护到数据" |
| **44** | ⛔ 处置 `PriorityQueueProducerConsumer<T>.StartAsync` 的必抛 `NotImplementedException`（§5.30.4 第 47 条）：① 实现它（转调 `StartConsumersAsync`）；② 或把它从 `IProducerConsumer<T>` 接口里摘掉（**破坏性**） | 1 个文件 / 1 处 | **低–中**（② 会让所有实现类少一个成员） | 消掉"接口声明了、调用必炸" |
| **45** | ⛔ 修两处"**排序键忽略排序对象**"（§5.30.3 第 34 条 + §5.30.5 第 64 条）：`GetColleaguePriority(colleague, message)` / `GetSubscriberPriority(subscriber, message)` 都只读 `message` ⇒ `OrderBy` 退化成恒等排序。改成真读第一参数（例如按 `Id` 稳定排序），或**删掉 `OrderBy` 并把注释改对** | 2 个文件 / 2 处 | **低**（当前顺序本就不可预测，改成稳定排序只会让顺序可预测） | 让"按优先级排序"这句注释变成真的 |
| **46** | ⛔ 统一 `Singleton` 的两条路径与两种失败策略（§5.30.1 第 1/2 条）：① 无参版与工厂版落到**同一个**持有者（现在同一类型会得到两个互不相干的"单例"）；② 异步版失败后允许重试（或把"一次失败永久失败"写进 XML 注释） | 1 个文件 / 2 处 | **中**（① 会改变"两条路径互不相干"的既有行为，需先扫下游有没有依赖） | 消掉"同一个类型两个单例"+"同步/异步失败策略相反" |
| **48** | 修 `Mediator.Register` / `AdvancedMediator.Register` 的 `TryAdd` 顺序（§5.30.3 第 25 条）：把 `colleague.SetMediator(this)` 移到 `TryAdd` **成功之后**（或先查重再设） | 2 个文件 / 2 处 | **低**（当前是"能发不能收"，改了只会更一致） | 消掉"重复 Id 注册后新对象成了半残废" |
| **49** | 修 `Mediator.BroadcastAsync` / `AdvancedMediator.BroadcastAsync` 的 catch 范围（§5.30.3 第 27 条）：把 `await Task.WhenAll(tasks)` 包进 `try`，**或**把注释改成与实现一致 | 2 个文件 / 2 处 | **低**（① 会让接收者异常由"抛给调用方"变成"被吞掉"，是行为变化，需先定哪个是期望语义） | 消掉"注释与实现相反" |
| **50** | ⛔ 修 `FilteredColleague.Receive` / `FilteredSubscriber` 丢弃非 `PriorityMessage`（§5.30.3 第 31 条）：把 `message is PriorityMessage pm` 这个**形态判断**改成"非 `PriorityMessage` 时按默认优先级包一条再交给过滤器" | 1 个文件 / 1 处 | **中**（会改变"静默丢弃"的既有行为，需先确认下游有没有依赖） | 消掉"过滤器写得再对也没用，消息在形态判断那一步就没了" |
| **51** | 修 `TypeFilter` + 空 `MessageType` 的组合坑（§5.30.3 第 33 条）：让 `SendWithPriority`/`BroadcastWithPriority` 也把 `messageType` 透传下去，或把 `TypeFilter` 对空值的语义从"丢弃"改成"放行" | 1 个文件 / 2 处 | **中**（同上，属行为语义变更） | 消掉"非空白名单 + `*WithPriority` 入口 = 消息全被丢" |
| **52** | 登记 `Patterns/` 的 **14 处纯死代码**（§5.30）：`CacheStrategy.Singleton` 死枚举值、`SubscriberBase<T>._disposed` 死字段、`ConfigurableProducerConsumer` 的 `batchTimer`、`ProducerConsumer<T>.StartAsync` 里永不触发的 `throw`、`Mediator.BroadcastAsync` 的死 `catch` 等 | 多个文件 | **待定**（⛔ 按硬规则 1 **不删**，只登记） | 让"死代码清单"可被一次性决策 |
| **53** | 消 `Patterns/` 的重复类型名（§5.6 本轮又实证 **2 个同名 `AdvancedMessageBroker<T>`** + **2 个同名 `MessagePriority` 枚举**） | 2 个模式目录 | **中**（下游 `using` 会歧义，同序 9） | 去重（**与序 9 合并做**） |
| **54** | 修 `SubscriberBase<T>` 与 `DelegateSubscriber<T>` 的 Dispose 后行为**相反**（§5.30.5 第 52 条）：基类补 `_disposed` 检查（把 `HandleMessage` 改成非 `abstract` 模板方法 + `ThrowIfDisposed`），或干脆删掉那个死字段 | 1 个文件 / 1 处 | **中**（基类加检查会让**所有**派生类 Dispose 后行为变化） | 消掉"同为 `ISubscriber<T>`，Dispose 后一个能用一个抛" |
| **55** | 修 `MessageRouter<T>`（§5.30.3 第 35 条 + §5.30.5 第 65 条）：① `_mediators` 换 `ConcurrentDictionary`（现在普通 `Dictionary` + 无锁 `RegisterChannel`）；② "通道未注册/名称为空"至少加日志 | 2 个文件 / 2 处 | **低** | 消掉非线程安全 + 静默丢弃 |
| **56** | 修 `AdvancedMessageBroker<T>.SubscriberCount` 的语义（§5.30.5 第 57 条）：改成"全局 + 主题去重后总数"，或把属性改名成 `GlobalSubscriberCount`（当前只做主题订阅时**恒为 0**） | 2 个文件 / 2 处 | **中**（下游若依赖当前值会变） | 消掉"计数与实际订阅者数量不符" |
| **58** | 统一 `MessageBroker<T>` 的 Dispose 后策略（§5.30.5 第 53 条）：`Publish`/`PublishAsync` 也抛 `ObjectDisposedException`（或把 `Subscribe` 系列改成静默） | 1 个文件 / 2 处 | **低** | 消掉同类内两种策略 |
| **59** | 修 `ColleagueBase` 四个发送方法的"未注册即静默丢弃"（§5.30.3 第 29 条）：加日志或抛 | 1 个文件 / 2 处 | **低** | 消掉静默无操作 |
| **61** | 修 `AdvancedProductBase.Dispose()` 的 `new` 隐藏（§5.30.1 第 12 条）：基类 `Dispose` 改 `virtual`、派生类改 `override`（消除两条 Dispose 入口） | 1 个文件 / 1 行 | **中**（动虚表，需确认无下游按非虚方式绑定） | 消掉"同一个对象两条 Dispose 路径" |
| **62** | 修 `ProductLifecycleBase` 的两个独立标志（§5.30.1 第 13 条）：`Initialize` 里补 `IsDisposed` 检查（当前已销毁的产品仍可被重新 `Initialize`） | 1 个文件 / 1 行 | **低** | 消掉"销毁后可复活" |
| **63** | 修 `FactoryBuilder.Build<T>()` 的不可用泛型（§5.30.1 第 10 条）：改成 `Build()` 返回 `FactoryManager`，或加 `where T : FactoryManager, new()`（当前**只有 `T == FactoryManager` 才不抛**） | 1 个文件 / 1 处 | **中**（改公开签名） | 消掉一个"泛型版实际不可用"的 API |
| **66** | 统一 `ObservableBase` 的 Dispose 后策略（§5.30.2 第 19 条）：`Notify`/`NotifyAsync` 也抛 `ObjectDisposedException` | 1 个文件 / 2 处 | **低** | 消掉同类内两种策略 |
| **67** | 修 `AdvancedObservable<T>.Publish` 共享同一个 `EventArgs<T>` 实例（§5.30.2 第 22 条）：每个订阅者一份副本 | 1 个文件 / 1 处 | **低** | 消掉"订阅者存下 `e` 会互相串改" |
| **68** | 修 `DelegateColleague` 覆写后丢失 `_disposed` 检查（§5.30.3 第 26 条）：覆写的 `Receive`/`ReceiveAsync` 里补 `ThrowIfDisposed` | 1 个文件 / 2 处 | **低** | 消掉"Dispose 后 Send 抛、Receive 却能调" |
| **69** | 修 `ConfigurableProducerConsumer`（§5.30.4 第 49/50 条）：① `Task.WhenAny` 输掉的 `Task.Delay` 要取消（现在长期挂着、高频轮询下堆积）；② `TryDequeueHighestPriorityItem` 的"全取出→排序→重入队"非原子 | 1 个文件 / 2 处 | **中**（② 要加锁或换真正的优先队列） | 消掉 Task 堆积 + 并发消费者互相看到空队列 |
| **70** | 统一两个生产者消费者类的 Stop 后写入行为（§5.30.4 第 43 条）：`EnhancedProducerConsumer` **抛**、`ConfigurableProducerConsumer` **安静返回** ⇒ 统一成一种 | 2 个文件 / 2 处 | **低** | 消掉"同名操作两套失败策略" |
| **71** | 修 `Patterns/` 其余行为不一致项（§5.30.1 第 15 条、§5.30.3 第 39/40 条、§5.30.5 第 54/55/58 条）：注册去重策略（`[name]=` 覆盖 vs `TryAdd` 静默忽略）、参数异常类型（`ArgumentException` vs `ArgumentNullException`）、`Unsubscribe` 的连带范围等 —— **需先定"哪个是期望语义"再改** | 多文件 | **中** | 消掉"同一件事两种做法" |
| **73** | ✅ **已结案（2026-09-18，第五批打包授权「单点可见性修复」）** · ~~修 `SingletonInstanceAsync<T>` 的锁外快路径缺 `volatile`~~（§5.30.1 第 73 条；⚠️ **2026-09-18 修序 47 时带出的新发现**）：给 `_instance` / `_initialized` / `_initializationFailed` 加 `volatile`（或改成"先建对象、再发布标志"的正确发布方式）—— **已按前者实现**：三字段全部加 `volatile`，**只改可见性、不改语义**；⚠️ 本条"风险"栏写的"`_initializationFailed` 语义会被牵连"经复核**不成立**（`volatile` 不影响"是否重试"的任何分支），但**原判断如实保留在此**，见 §5.30.1 第 73 条的对照说明 | 1 个文件 / 3 行 | **低–中**（`_initializationFailed` 一旦置位**永不复位**的语义会被牵连，见 §5.30.1 第 2 条 ⇒ 需先定"失败后是否允许重试"） | 消掉"另一线程看到 `_initialized == true` 但 `_instance` 仍为 `null`"的发布竞态（拿到 `null` 单例） |


> 序 **6 / 8 / 8b（`float` 的 `ToBytes` + `ToASCII`；`ToHexBools` 经第五轮定案**无需改**）/ 8c / 8d / 8e / 8f / 8g / 11 / 11b / 11c / 11d / 11e / 11f / 12 / 12c / 12d / 12e / 12f / 12g / 12h / 12i / 12j / 12k / 3 / 27 / 28 / 32 / 36 / 37 / 41 / 43 / 47 / 57 / 60 / 64 / 65 / 72** 已完成。
> 其中 **12k 是"决策登记"而非复核**（用户明确指示「`Extension.Vision.Cognex` 不用回归」）⇒ 纯文档动作、零代码改动。
> 其中 **27 / 32 / 41 / 72 是"按用户逐项授权修改产品代码"**（均 2026-09-18）：**41 是第一处**（授权范围"1 个文件 / 1 处"）；
> ⛔ 该范围**不覆盖**同型的序 72 —— 序 72 是**单独**取得授权之后才动的手。**授权逐项生效、不累积**。
> ⚠️ **序 8b 已结案**：`float` 的 `ToBytes`（第三批）与 `ToASCII`（第四批）已修；
> `ToHexBools` 经 2026-09-18 **第五轮只读侦察**定案为「**无需改代码**」（实现正确、仅名字误导）——
> 它仍有真实生产调用点、且属实机 PLC 通讯路径，但**没有缺陷可修**（详见 §5.13 第五轮块）。
> **`TierGuardTests.Allowed` 的目录键已同步更新**（这是守卫的已知代价，本次已验证）：
> 守卫是**目录敏感**的，且**两侧都必须前缀匹配** —— 详见 §3.3 的"四处必须做对"。
>
> 序 **8b / 12 / 13 / 14 … 40**（以及 **73**）是补测过程中**新发现**的（§5.11–§5.29、§5.30.1），此前不在待定表里；
> 序 **42 – 71** 是**第六轮**（`Patterns/` + `Ui/Controls/DataGridViewEx`）补测时新发现的（§5.30）——
> 它们都要求改产品代码，因此按硬规则 1 **只记录、不动手**，等明确指示。
> **例外 1**：序 12d（`DelayEx` 移位）属"分级修正"而非"缺陷修复"，已按用户"分级还是可以做吧 + 一起改"的既有授权执行。
> **例外 2（现共 17 条）**：序 **41**（原属第六轮新发现）、序 **72**（同轮顺带发现，**后续单独授权**）、
> 序 **27** 与 序 **32**（均 2026-09-18 逐项授权）、序 **3 / 36 / 37 / 47**（2026-09-18 **打包**授权，
> 即"低风险加固包" —— ⚠️ 该批授权**只覆盖这 4 项**）、
> 序 **28 / 43 / 57 / 64 / 65**（2026-09-18 **第二批打包**授权，即"极低风险三条包" ——
> ⚠️ 该批授权**只覆盖这 5 项**，与第一批**互不覆盖**）；
> 序 **60** 与 序 **8b**（2026-09-18 **第三批打包**授权，即「语义注释 + 单点零调用修复」——
> ⚠️ 该批授权**只覆盖这 2 项**；且 **序 8b 仅覆盖 `float` 的 `ToBytes` 一处**，
> 同项的 `ToASCII` 在第四批单独授权；`ToHexBools` 经第五轮只读侦察定案**无需改代码**、不涉及授权）；
> 序 **8b** 的 `ToASCII`（2026-09-18 **第四批打包**授权，即「副作用消除」——
> ⚠️ 该批授权**只覆盖这一处**；同项的 `ToHexBools` 经第五轮只读侦察定案**无需改代码**，不涉及授权）、
> 序 **73**（2026-09-18 **第五批打包**授权，即「单点可见性修复」——
> ⚠️ 该批授权**只覆盖这一项**）
> —— 都已从待定表移出，明细见上方已完成表。
> ⚠️ **本轮新登记 1 条待定项**：序 **73**（`SingletonInstanceAsync<T>` 的锁外快路径缺 `volatile`）——
> 它是**修序 47 时带出的新发现**，登记时按硬规则 1 **只记录、未改**；
> ✅ **随后经第五批单独授权，已于 2026-09-18 修复**（⚠️ 待定表里**保留原行**，只就地标注结案 ——
> 遵循"不删除"约束：**登记时的原判断与结案后的结论并列保留**，不做整行搬移或删除）。
>
> ⚠️ 提醒：序 14–73（含已修的 3 / 27 / 28 / 32 / 36 / 37 / 41 / 43 / 47 / 57 / 64 / 65 / 72 / 73）的缺陷**全部已被测试钉住** ⇒ 修完必须同步跑 `Extension.Tests`，
> 那些用例会**故意变红**（它们断言的是"当前行为"，修好后要连同用例一起更新为"正确行为"）。
> **十六条已修项各自的改写方向都不同**，值得记一下：
> - 序 41 / 72 —— 老用例断言的是**缺陷本身**（"生产再也不会成功" / "150 ms 内确实挂住"）⇒ 重写成"**立刻抛**"。
> - 序 27 —— 老用例是"**静态核对、刻意不执行**"（真执行会去杀进程）⇒ 修后入口先抛，**升级成真的执行**。
> - 序 32 —— 老用例断言"**抛 `NullReferenceException`**" ⇒ 改成断言"**返回 `Valid`**"（fail-open）。
> - 序 3a —— 老用例断言"**栈被重置、处理器帧丢失**" ⇒ **反转**成"栈里必须能找到处理器那一帧"。
> - 序 36 —— 老用例断言"**抛 `NullReferenceException`**" ⇒ 改成断言"**返回 `false`**"（fail-closed）。
> - 序 37 —— 老用例断言"**传 0 恒真**" ⇒ **拆成 2 条**："非正数直接抛" + "1..99 仍恒真（已记录未修）"。
> - 序 3b / 47 —— 原**没有**行为用例可反转，改为**源码护栏**（断言签名/字段修饰符），
>   ⚠️ 且护栏必须**按所属类型/代码行切段**（全文级 `Contains` 会被同文件的"另一个同类字段"喂饱 ⇒ 假通过）。
> - 序 28 —— 老用例断言"**不抛、且返回释放前那个悬垂地址**" ⇒ **反转**成"`IsClosed` 为真 + 取指针抛 `ObjectDisposedException`"；
>   ⚠️ 但**刻意不断言 `IsInvalid == false`**（那是 BCL 实现细节，换运行时版本会假红）。
> - 序 43 —— 老用例断言"**内层 `DisposeCount == 0`**"（缺陷本身）⇒ 反转成"**`== 1`**"，并补**幂等**断言（再 `Dispose` 一次仍是 1）；
>   ✅ **另新增 1 条**专钉 `DelegateSubscriber<T>` 这个**平行分支**（用"`Dispose` 后 `HandleMessage` 抛 `ObjectDisposedException`"做**可观测证据** ——
>   `SubscriberBase<T>._disposed` 是死字段，只能靠探测子类覆写 `Dispose(bool)` 计数）。
> - 序 57 —— 老用例断言"内层非 `SubscriberBase<T>` 时返回固定字面量 `FilteredSubscriber`" ⇒ 改成断言带 `Filtered(...)` 前缀的**两种内层形态** + `Assert.NotEqual`。
> - 序 64 —— 老用例断言"**抛 `KeyNotFoundException`**" ⇒ 改成断言"**抛 `InvalidOperationException`**"，并**加对照组**（未注册时仍 `KeyNotFoundException`）。
> - 序 65 —— 老用例断言「抛 `NullReferenceException`」⇒ 改成断言「抛 `ArgumentNullException` 且 `ParamName == productA`」。
> - 序 60 —— **没有用例涉及**（纯注释）⇒ 无需改写；但**必须核对源码护栏用例**：
>   `PatternsObserverMediatorTests:965` 断言的是 `FilteredColleague` 里那行 `is PriorityMessage`、
>   `PatternsPublishSubscribeTests:646` 断言的是 `GetSubscriberPriority` 签名 ⇒ **都不在注释编辑区域内**，实测未变红。
> - 序 8b —— 老用例 `ToBytes_float_必然抛异常_是个不可用的死方法` 断言的是**缺陷本身** ⇒ 重写成
>   `ToBytes_float_按IEEE754_固定4字节`，并**专门断言 `Length == 4`**（钉住「文档那条 `byte[8]` 建议是错的」）。
> - 序 73 —— 老用例是**反向护栏**（`Assert.Contains("private static T _instance;", asyncSection)`，断言缺陷**存在**）⇒
>   **反转成正向护栏**（`Assert.Contains(…volatile…)` + `Assert.DoesNotContain(…非 volatile…)`），并**把作用域收紧**：
>   原写法 `source.Substring(asyncIdx)` **一直到文件末尾**，会把后面 `ThreadSafeSingleton<T>` 的同名 volatile 字段算进来 ⇒ **可能假通过**；
>   现改为 `asyncEnd = source.IndexOf("class SingletonBase<T>", asyncIdx)` 精确限定在类体内。
>   ⚠️ 另一个坑：**必须过滤 `//` 注释行**再断言 —— 上方注释会引用旧写法（`private static T _instance;`），整段文本断言会被注释里的字面量判失败（**假红**）。
>   ⚠️ 同时**改用例名**（旧名 `…_异步版仍缺` 本身是在**描述缺陷**）⇒ 现名 `Singleton_双重检查锁均已补volatile_工厂版与异步版`。
> 其中 **10 条用例是"源码文本断言"**（用 `[CallerFilePath]` 换算路径后 `Assert.Contains` 产品源码片段）——
> 产品代码一改它们就红，这是**故意的**：它们的作用就是"提醒同步更新文档"。
> （序 27 的 `ProcessEx_源码里必须保留参数校验`、序 3b 的 `BitOperationEx_泛型指针方法必须带unmanaged约束`、
> 序 36 的 `AntiTamperEx_源码里VerifyFile必须保留期望值空值短路` 都属这一类。）
>
> ⛔ 风险排序（原"都要求改产品代码，未动"；**第 1 条序 41、第 2 条序 72、第 4 条序 27、第 5 条序 32
> 均已于 2026-09-18 修复**；另 **序 3 / 36 / 37 / 47 属"低风险加固包"、同批修复**；
> **第 6 条序 28、第 7 条序 43 属第二批"极低风险三条包"、也已修复**；**序 60 与 序 8b（仅 `float` 一处）属第三批「语义注释 + 单点零调用修复」、同样已修复**，其余未动）：
> 1. ~~**序 41**~~ ✅ **已修（2026-09-18）** —— `ProducerConsumer<T>` 的"`Stop()` 后 `StartAsync`"曾让生产者与消费者
>    **同时忙循环**、`Task.WhenAll` 永不返回（**调用方若不传令牌就是永久挂死**）。现已改成 `Stop()` 终态、
>    `StartAsync` 立刻抛异常（**15 ms** 而不是 300 ms 才靠令牌逃出）。
> 2. ~~**序 72**~~ ✅ **已修（2026-09-18，单独授权）** —— `EnhancedProducerConsumer<T>` / `ConfigurableProducerConsumer<T>`
>    与序 41 **逐字同型**：`Stop()` 同样 `_writer.Complete()`、`StartAsync` 同样把 `_running` 置回 `true`
>    ⇒ 同样**双任务忙循环、`Task.WhenAll` 永不返回**。修法照抄序 41（加 `_stopped` 闸门）。
>    **实测**：修前那两条用例必须耗尽 **150 ms** 令牌预算才能"证明它挂住了"，修后 **1–41 ms** 就抛出。
> 3. **序 25①** —— `ScheduledDeletion` 的子目录删除改成递归之后，这个**长期静默失效**的清理开关
>    会**突然开始真的删文件**。改之前必须先在测试目录上验证，否则可能一次性删掉现场所有的历史图片。
> 4. ~~**序 27**~~ ✅ **已修（2026-09-18）** —— `ProcessEx.CloseProcess(null)` 曾**真的会去杀进程**
>    （实测抛 `Win32Exception: 拒绝访问`，**证明 `Kill()` 确实被调用**）。入口已加 `null` / 空串两道校验，
>    都在 `Process.GetProcessesByName` **之前**。⛔ 注意空串**同样**返回一批进程 ⇒ 只挡 `null` 是不够的。
> 5. ~~**序 32**~~ ✅ **已修（2026-09-18）** —— `LicenseManagerEx` 的"签名合法但 `Features` 为 null ⇒ NRE"。
>    危害是**拒绝服务**而非数据损坏，触发条件是**厂商签发工具的一次疏忽**、客户端无从防御。
>    已按"空 = 全部功能"在反序列化边界归一化（⚠️ 是 fail-open，见 §5.29.1 第 1 条）。
> 6. ~~**序 28**~~ ✅ **已修（2026-09-18，第二批打包授权）** —— `SafeMemoryHandle.GetPointer()` 释放后返回悬垂指针，
>    `Marshal` 读写会 `AccessViolationException` **崩进程**（catch 不住）。判据已改成 `IsInvalid || IsClosed`。
> 7. ~~**序 43**~~ ✅ **已修（2026-09-18，第二批打包授权）** —— 两处 `Dispose(bool)` 里的死分支是**资源泄漏**（不是崩溃）。
>    ⚠️ **原判断"要动公开契约、影响面是第六轮里最大的一条"被方案②推翻了**：走"包装器补具体类型分支"的**仓内闭环**路线后，
>    `IColleague` / `ISubscriber<T>` **一个字都不用改**，实际改动面只有 2 个文件 / 2 处。
>    ⛔ **教训**：这个结论是在**没把方案②想透**的情况下写下的 —— 它把一个"低风险"项误标成了"中风险、建议单独排期"。
>    真正的成本不在"要不要改接口"，而在"内层具体类型有几个、是不是平行的"（本项答案：2 个，且**平行** ⇒ 只补一个会漏）。
> 8. **序 73**（**2026-09-18 新登记**）—— `SingletonInstanceAsync<T>` 的**锁外快路径**读两个**都非 `volatile`** 的字段
>    （`_initialized` / `_instance`），而写入顺序是 `_instance = await factory()` → `_initialized = true`
>    ⇒ 另一线程可能看到 `_initialized == true` 而 `_instance` 仍为 `null`（**拿到 null 单例**）。
>    ⚠️ 与已修的序 47 是**同型但更靠前**的问题（序 47 那个至少是"锁内写、锁外读"，这里是**纯无锁读**，
>    且 `_initialized` 与 `_instance` 之间没有任何 happens-before 边）。修它要同时给 3 个字段加 `volatile`，
>    并会牵连 `_initializationFailed`"一次失败永久失败"的语义 ⇒ 需单独授权。
>
> ⚠️ 已修的 **序 3 / 36 / 37 / 47** 四条**都不在这个风险榜上** —— 它们原本标的风险就是"**极低 / 低**"
> （序 36 / 37 更是**全仓零调用**），这正是它们能被**一次打包授权**修掉的原因。
> **第二批的序 28 / 43 / 57 / 64 / 65 也是同理**：57 / 64 / 65 原本就标"极低"；28 标"低"（零引用）；
> 43 虽被标"中"，但**复核后确认走方案②就是"低"**（见上一条的教训）⇒ 五项都落在"可打包"区间。
> **第三批的序 60 / 序 8b 也是同理，但多了一层教训**：序 60 是**纯注释**（真·零风险）；
> 序 8b 之所以能进这一批，是因为**只挑了它三处里唯一真·零调用的那一处**。
> ⛔ **这正是本轮最有价值的一条**：文档把序 8b 标成「低（**三者全仓零调用**）」，
实测**只有 1/3 成立**（`ToASCII` **10 处** / `ToHexBools` 1 处有真实生产调用）。
> 若照文档原样打包修三处，**会直接动到三菱 MC3E 实机通讯路径**。
> ⇒ **打包授权前必须自己重跑一遍「零调用」证据**，不能沿用文档里那句结论
> （错因是当初只 `grep` 了 `Extension/` 一个目录就外推成「全仓」）。
> ✅ **第四批补了一条反向教训**：这一批之所以能安全落地，靠的是**把「10 处调用点逐个核对」做完**
> 之后才动手 —— 文档原先那句「另 5 处天然安全」其实**不准确**（那 4 处 `return data.ToASCII();`
> 所在的两个入口虽然零调用，但底层数组确实被三个消费者共享）。**「天然安全」这种话必须给出判据。**
> 反过来，本榜剩下的 **序 25① / 73** 仍有"会真的删文件 / 要定失败语义"这类
> **必须先做决策**的前提 ⇒ 不宜打包。

### ⛔ 明确**不做**的动作

| 不做 | 理由 |
|---|---|
| 拆 `Extension.Vision.Halcon` / `.OpenCv` / `.UI` / `.Runtime` … | **不新建 csproj。** 见 §3.2：三个正当理由一条都不成立，多出的成本换不到收益 |
| 按 NuGet 包拆项目 | `Extension.csproj` 挂 9 个包 ⇒ 会得到 9 个工程，其中 6 个包里全仓只有 1–2 个文件在用。**依赖治理的单位是"文件"，不是"包"** |
| 把 T3 的 5 个文件搬出 `Extension.dll` | 它们**就是** `Extension` 的一部分（下游在用），隔离靠准入线，不靠程序集边界 |
| 把 `Extension.Vision.Cognex` 合回主工程 | **用户已明确决定（2026-09-18）：「不用回归」。** 它是 09-15 阶段 2A 的既定成果，保持独立卫星工程；合回的唯一"收益"是少一个工程，代价却是 Cognex 的 x86 `<Reference>` 与 MSB3270 从卫星工程转回主工程 —— 收益不足。见 §6.G |
| 为了"分级"改 `TargetFramework` / 平台 | 09-17 `统一项目平台`（`3539f99`）刚把全仓统一成 `net9.0-windows7.0` + AnyCPU，不要回退 |
| 删任何"零引用/重复/空类"代码 | **不删代码是硬规则**。`NotifyPropertyChanged`(0 引用)、`MatEx`(空类)、`DataGridViewEx`(73/87 行注释)、两份 `ScheduledDeletion` 全部**原样保留**，只做隔离 |

---

## 8. 与已有 4 份文档的关系

| 文件 | 关系 |
|---|---|
| `CODE-CLASSIFICATION-ANALYSIS.md` | 诊断"分类标准不统一"的**证据**来源；本方案把它从"5 套标准"收敛成"1 套分级 + 1 套职责" |
| `ARCHITECTURE-OPTIMIZATION.md` | 结构审计（9-07）；它给的 `Extension.Abstractions/Core/Infrastructure/Imaging/Vision.*/UI/Wiring` 拆 csproj 方案 **本方案不采纳**（§3.2），但其"按依赖方向分层"的思路就是 T0–T3 的来源 |
| `MIGRATION-CHECKLIST.md` | 可执行清单；**§7 第 1–3 序**可直接并入 |
