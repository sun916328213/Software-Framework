# Extension 代码分类分析报告

> 范围：`Extension/Extension/`（单 csproj，92 个 `.cs`，16,613 行）
> 方法：目录/命名空间清单比对 + 全仓引用矩阵 + 依赖清单审计
> 结论：**分类方向对，粒度错；集中复用对，"全塞一个程序集"错。**

---

## ⛔ 全篇勘误（2026-09-15）：本报告所有「删除」类建议一律作废

> **用户明确表态：「没有使用的你不应该删除」。**
> ⇒ 「零调用 / 零引用 / 不可达 / 是空类 / 与另一份重复」**都不构成删除理由**。
>
> **本报告以下位置的处方全部作废，按此勘误改写：**
>
> | 位置 | 原文（❌ 作废） | 改写后（✅ 有效） |
> |---|---|---|
> | §2.3 / §2.6 / §3.x | 「删掉 `ScheduledDeletionEx`」「删 2 个空类 + 5 个零调用方法」 | **一行都不删**；要隔离依赖就 `git mv` 进卫星程序集 |
> | §4.4 优先级表 P0 | 「删死代码以摘掉 Cognex / OpenCV」 | 「**搬**进 `Extension.Vision.Cognex` / `.Halcon` / `.OpenCv`」 |
> | §4.7 / §5 | `DesignPattern/**/*Example.cs`「`samples/` 或删除」 | **只移不删**，移到 `samples/DesignPattern.Samples/` |
> | 全篇 | 空目录、空 `<Folder Include>`「可删」 | **保留** |
> | 全篇 | 空 `catch` / `Thread.Abort()` 等隐患「顺手修」 | **发现即记录，不顺手改**，单独提出问用户 |
>
> **诊断不变、处方更换**：本报告最有价值的部分 ——
> 「`Extension` 单程序集把 Cognex / Halcon / OpenCV / MaterialDesign / Prism 的传递依赖
> 强加给 19 个下游项目」—— **这个结论完全成立**（§0 / §2.8 / §3.2 / §6）。
> 变的只是解法：**从「删掉用到 vendor 的死代码」改成「把 vendor 相关类型搬进独立卫星程序集」**。
> 效果相同（`Extension.dll` 不再引用 Cognex / Halcon / OpenCV），但**代码一行不少**。
>
> 可执行的 v2 方案见 `MIGRATION-CHECKLIST.md`；被否的那次尝试的复盘见 `PHASE1-2A-REVIEW.md`。
>
> ### ❗ 追加更正（2026-09-15 实测，见 §2.9）：本报告对「依赖污染」的估计**偏大**
>
> 用户反问「为什么一定分项目 全部走一个不行吗」后去仓库实测，发现：
>
> - **Halcon 的 84 MB 不是 Extension 强加的** —— **7 个项目自己就引了 halcondotnet**，
>   拆了照样有。§0 / §2.8 / §3.2 里「把 Halcon 强加给 19 个下游」的说法**不准确**。
> - **真正的问题只有 Cognex**：全仓 **0 个项目**直接引用它，33 份 bin 副本共 **203 MB**
>   100% 来自 `Extension.csproj`，而它只被一个空类 + 2 个零调用方法撑着。
> - ⇒ **「全部走一个」是可以的，拆程序集属于过度设计。**
>   三个不动代码的选项（什么都不做 / `<Private>false</Private>` / 拆项目）对比见 **§2.9**。
>   **推荐 A 或 B，不推荐 C。**
>
> ⚠️ 另注：本报告 §2.6 / §4.4 里「`IntPtrEx` 5 个方法全部零调用」这句**是事实错误**
> —— `ToHImageByte` / `ToHImageRGB` 被 `BitmapEx.ToHImage` 调用（`BitmapEx.cs:28,32`），
> 照删会报 2 个 `CS1061`。详见 `PHASE1-2A-REVIEW.md` §3.1。

---

> **状态更新（2026-09-20）**：本文记录的是重构前的分类与阶段性决策。当前架构决定已取代其中“保留兼容接口”的结论：ScheduledDeletionEx 与 DeleteExpiredFiles1 均已移除，Retention 只保留 ScheduledDeletion.DeleteExpiredFiles 一个公开操作。以下相关内容仅作为历史问题记录，不再是实施处方。
## 0. 先给结论

| 你的问题 | 结论 |
|---|---|
| 目前的分类是否合适？ | **不合适。** 顶层 5 个目录用了 **5 套不同的分类标准**（技术域 / 模式名 / 模糊词 / 模糊词 / 语言构造），且存在重复实现、空目录、教学示例入库。 |
| 可复用代码都放在 Extension 是否合适？ | **方向对，粒度错。** 集中复用避免复制粘贴是对的；但**单程序集 + 全依赖捆绑**让 19 个下游项目被迫承担 Cognex / Halcon / OpenCV / MaterialDesign / Prism 的传递依赖。 |
| 还有没有别的问题？ | 有。**有些东西根本不该在"通用库"里**（业务契约、反调试、教学示例），**有些真正该复用的契约又没进来**（日志/Excel/SQL 三套并行实现，缺的是接口不是实现）。 |

> 上述判断已与 Microsoft 官方设计准则及经典架构原则逐条核对（见 **§6**），全部有出处；核对过程也修正了原分析的三处不严谨之处（§6.2）。

---

## 1. 现状盘点

```
Extension.sln
├── Extension/Extension.csproj        [Library, net9.0-windows7.0, UseWPF=true]
│   ├── Core/        17 个子目录, 7,328 行, 52 文件   ← 含 2 个空目录
│   ├── DesignPattern/  6 个模式 × (基础+Advanced+Example), 7,973 行, 24 文件
│   ├── Helper/      4 个子目录,   686 行,  5 文件
│   ├── Tool/                        432 行,  2 文件
│   └── Struct/                      194 行,  4 文件
└── LicenseTool/LicenseTool.csproj    [WinExe, 配套 SecurityEx 的授权签发工具]
```

**规模分布（行数）**：`DesignPattern` 7,973（48%）> `Core` 7,328（44%）> `Helper` 686 > `Tool` 432 > `Struct` 194

**Core 子目录文件数**：

| 目录 | 文件数 | 目录 | 文件数 | 目录 | 文件数 |
|---|---|---|---|---|---|
| ConvertEx | 12 | ControlEx | 7 | ImageEx | 6 |
| SecurityEx | 6 | SystemEx | 6 | FileEx | 5 |
| ProtectionEx | 4 | DataEx | 3 | ReflectionEx | 3 |
| IntPtrEx | 2 | ThemeEx | 2 | CollectionEx / CompareEx / EnumEx / TaskEx | 各 1 |
| **SemaphoreEx** | **0** | **FlowAttribute** | **0** | | |

**外部依赖（这是关键）**：

```
<Reference>       Cognex.VisionPro / Cognex.VisionPro.Core / halcondotnet     ← 3 个非托管视觉 SDK
<PackageReference> FastDeepCloner / Mapster / MaterialDesignThemes /
                   MiniExcel / OpenCvSharp4 / Prism.Core / System.Drawing.Common   ← 7 个包
```

**下游引用**：`Frame.sln` 内 **19 个 csproj** 直接 `ProjectReference` 了 `Extension.csproj`。

---

## 2. 分类诊断：目前的分类哪里不合适

### 2.1 【核心问题】顶层 5 个目录 = 5 套分类标准

这是"分类不合适"的根因 —— 不是某个文件放错了，而是**分类维度本身不统一**：

| 目录 | 实际使用的分类标准 | 反例 |
|---|---|---|
| `Core/` | 按**技术域** | 但 `ControlEx`（WPF UI）、`ImageEx`（视觉 SDK）、`ProtectionEx`（发布形态）混在名为"Core"的层里 |
| `DesignPattern/` | 按**设计模式名** | 且 `*Example.cs`（教学）与运行时实现混编 |
| `Helper/` | 按**模糊词** | 与 `Tool/` 无任何可判定的边界 |
| `Tool/` | 按**模糊词** | 同上 |
| `Struct/` | 按 **C# 语言构造** | 里面装的是 enum + class + struct 三种，还有 Mapster 映射类 |

**后果**：新功能该放哪，没有可推导的规则。`ZIPEX`（文件压缩）放进了 `DataEx`，`ScheduledDeletion`（文件清理）同时出现在 `SystemEx` 和 `FileEx` —— 这不是谁手滑，是**没有准入规则**的必然结果。

### 2.2 `Ex` 后缀描述的是"写法来源"，不是"职责"

14 个 `*Ex` 目录，命名逻辑是"这个文件里写了扩展方法"。但这个规律**在自己目录里就失效**：

- `Core/FileEx/ScheduledDeletionEx.cs` → `internal class`，不是扩展方法
- `Core/SystemEx/` 里 `ProcessEx` / `CpuMonitorEx` / `LangSwitchEx` 是静态类，`ScheduledDeletion` 干脆不带 `Ex`
- `Core/SecurityEx/` 里 `LicenseInfo.cs`、`LicenseManagerEx.cs` 是实例类
- `Core/ReflectionEx/ReflectionEx.cs` 的命名空间却是 `Extension.Core.Reflection`（少个 Ex）

按"名字"分类而不是按"职责"分类，一旦职责与写法不一致就崩。

### 2.3 边界重叠与重复实现（有实证）

| 问题 | 证据 |
|---|---|
| **同一算法两份实现** | `Core/SystemEx/ScheduledDeletion.cs`（`public static`）与 `Core/FileEx/ScheduledDeletionEx.cs`（`internal class`）—— 见下方 §2.3.1 的逐方法实测。**全仓核实后的实际使用情况**：`SystemEx/ScheduledDeletion` 被 `Packing Machine/Business/Flow/AutoProcess.cs:192`、`Packing Machine/Config/Flow/StartupFlow/05_初始化后台.cs:110` 使用；**`FileEx/ScheduledDeletionEx` 零引用**（且它是 `internal`，零引用即**不可达**）。→ ⛔ **原文「动作是删掉 `ScheduledDeletionEx`」已作废**（2026-09-15 勘误）：两份**都保留**，零引用不是删除理由 |
| **同名类不同目录** | `Core/IntPtrEx/` 目录 vs `Core/ImageEx/IntPtrEx.cs` 文件 |
| **归档错位** | `Core/DataEx/ZIPEX.cs` 是压缩包操作 → 属于 IO；`Core/DataEx/BufferEx.cs` 是字节缓冲区 → 近 ConvertEx；`Core/DataEx/CloneEx.cs` 是对象深拷贝 → 近 Reflection。三者被 `DataEx` 这个筐装在一起，唯一共性是"处理数据" |
| **Helper / Tool 语义重叠** | `Helper/Channel`、`Helper/TaskHelper` 与 `Core/TaskEx` 都在做并发原语；`Tool/ConcurrentList`、`Tool/RangeObservableCollection` 与 `Core/CollectionEx/ListEx` 都在做集合 |
| **`Helper` vs `Tool` 无规则** | `ConcurrentList` 在 `Tool`，`ChannelHelper` 在 `Helper` —— 都是"辅助类"，边界靠感觉 |

#### 2.3.1 `ScheduledDeletion` 两份实现的逐方法实测（2026-09-14 复核）

**重复面比"两份文件"更大**：两边各有一个 `public class DateFolderInfo`（分别在
`Extension.Core.FileEx` / `Extension.Core.SystemEx`，**不撞名**），加上 4 个同名方法，共 **5 处重复**。

**两份的差异只有两个系统性维度**（其余行归一化后逐行相同）：

| 维度 | `FileEx/ScheduledDeletionEx`（226 行） | `SystemEx/ScheduledDeletion`（340 行） |
|---|---|---|
| 静态性 | `internal class` + 实例方法 | `public static class` + 全 `static` |
| **失败策略** | **抛异常** `throw new Exception(...)` | **记日志后吞掉** `Log(...)`；`GetDirectorySize` 额外 `return 0;` |

⇒ 差异**不是"日志文案"，而是错误语义**。所以「合并成一份」必须先定策略（抛 or 吞），
**不能当成纯格式重复直接删**。

> ⛔ **勘误（2026-09-15）**：原文接着写「按当前使用情况，**直接删 FileEx 那份**才是无风险动作」
> —— **已作废**。两份**都保留**。用户原则：「没有使用的你不应该删除」。
> 重复实现也不是删除理由。

**新发现（本次复核补充）：被保留的 `SystemEx` 版自己也有一个零调用方法。**
`ScheduledDeletion.cs:115` 有个 `DeleteExpiredFiles1(...)`（约 94 行，**无 XML 注释**，
与 `DeleteExpiredFiles` 同名同签名只差 `1` 后缀），**全仓 0 处调用**。

> ⛔ **勘误（2026-09-15）**：原文据此推出「删 `ScheduledDeletionEx` 整个文件 + 删 `DeleteExpiredFiles1`」
> —— **已作废，一个都不删**。这条零调用发现的价值只在于**说明该文件确实冗余**，不构成删除许可。

> ⚠️ **更正（2026-09-14 二次核实）：`Log` 无论如何都不能删。**
> 曾经的说法「`Log` 只服务于 `DeleteExpiredFiles1`」实测**不成立**。
> `private static void Log(string)` 共被调用 **25 处**：
> - `DeleteExpiredFiles`（**在用的那个**）：25、28、40、55、59、69、73、92、96、100、105 行 —— **11 处**
> - `DeleteExpiredFiles1`（零调用）：121、124、136、151、156、166、170、189、193、197、202 行 —— 11 处
> - 三个**共用**私有助手：`GetAllDateFolders`(235)、`TryParseDateFromPath`(275)、`GetDirectorySize`(310) —— 3 处
>
> 删 `Log` 会让 `DeleteExpiredFiles` 与三个助手**直接编译不过**（CS0103）。
> **`Log` 必须保留** —— 按新原则，`DeleteExpiredFiles1` 同样保留。


### 2.4 命名空间与目录不一致（目录不是真相源）

- `Core/ReflectionEx/ReflectionEx.cs` → `namespace Extension.Core.Reflection`（目录多 Ex，命名空间没有）
- `DesignPattern/*/XxxExample.cs` → 多一级 `.Examples`
- `Struct/` 下 4 个文件全在 `Extension.Struct` 扁平命名空间

不一定是错，但意味着**目录结构不能作为"分类是否合理"的依据**，也让 IDE 的 namespace-folder 对齐检查失效。

### 2.5 一个文件混多种关注点

`Struct/LogInfo.cs` 一个文件里塞了 **4 类东西**：

```csharp
public enum ELogLevel { ... }        // 枚举
public struct LogInfo { get; set; }  // 可变值类型
public struct LogInfoDTO { ... }     // 传输对象
public static class LogInfoMapping { ... }  // Mapster 映射配置（还带静态构造函数副作用）
```

`Struct/ImageInfo.cs` 同理。由此产生两个连带问题：

1. **可变 `public struct` 带 `get; set;`** —— 值类型 + 可变 = 经典反模式（改副本不生效、装箱、相等性陷阱）。
2. **`Struct/` ↔ `Helper/Mapping/` 双向耦合**：`Struct/LogInfo.cs` `using Mapster`，`Helper/Mapping/MapsterHelper.cs` 反过来 `using Extension.Struct`，中间靠 `static bool _isInitialized` 手动开关 + 硬编码登记（`InitializeAllMappings()` 里逐个列 `ImageInfoMapping.Configure` / `LogInfoMapping.Configure`）——**加新 DTO 忘了登记就静默不映射**。
3. **`Struct/` 这个名字与内容不符**：装的是 enum、class、struct 三类，叫 `Struct` 名不副实。

### 2.6 空壳与"不该进生产 dll"的内容

> ⛔ **本节标题原名「死代码与空壳」，处方是「删」—— 已按 2026-09-15 勘误改写为「不删，只搬」。**

| 项 | 说明 | 处置（v2） |
|---|---|---|
| `Core/SemaphoreEx/` | **空目录**，而且 `Extension.csproj` 里还留着 `<Folder Include="Core\SemaphoreEx\" />` | **保留**（空目录与 `<Folder Include>` 都不动） |
| `Core/FlowAttribute/` | **空目录** | **保留** |
| `Properties/` | **空目录** | **保留** |
| `DesignPattern/**/*Example.cs` | **8 个文件 / 2,149 行**（占 DesignPattern 的 27%，占全库 13%），纯控制台演示代码，**会被编译进生产 `Extension.dll`** | **只移不删** → `samples/DesignPattern.Samples/` |

### 2.7 引用矩阵：大量代码实际无人使用

全仓（不含 Extension 自身）对 Extension 各命名空间的引用次数：

| 命名空间 | 次数 | 命名空间 | 次数 | 命名空间 | 次数 |
|---|---|---|---|---|---|
| `Struct` | 81 | `Core.ImageEx` | 8 | `Core.IntPtrEx` | 1 |
| `Core.ConvertEx` | 60 | `Helper.TaskHelper` | 8 | `Core.CollectionEx` | 1 |
| `Core.ControlEx` | 38 | `DesignPattern` | 7 | `Helper.Notify` | **0** |
| `Core.SystemEx` | 38 | `Core.ReflectionEx` | 6 | `Core.SecurityEx` | **0** * |
| `Core.FileEx` | 21 | `Core.TaskEx` | 3 | `Core.ProtectionEx` | **0** |
| `Core.DataEx` | 19 | `Helper.Mapping` | 2 | `Core.Reflection` | **0** |
| `Core.CompareEx` | 18 | `Tool` | 12 | | |
| `Core.ThemeEx` | 16 | `Helper.Channel` | 10 | | |
| `Core.EnumEx` | 13 | | | | |

\* `Core.SecurityEx` 的唯一使用者是同解决方案内的 `LicenseTool`。

> **⚠️ 计数口径与本轮复核（2026-09-14）**：上表**未记录统计口径**，导致数字无法精确复现。
> 用「`grep -rn "\b<标识>\b" --include=*.cs .` 再剔除 `obj/`、`bin/`、`Extension/Extension/` 自身」复算：
>
> | 标识 | 报告值 | 复算值 | 标识 | 报告值 | 复算值 |
> |---|---|---|---|---|---|
> | `Struct` | 81 | **88** | `CompareEx` | 18 | **12** |
> | `ConvertEx` | 60 | **56** | `ThemeEx` | 16 | **15** |
> | `ControlEx` | 38 | **28** | `ReflectionEx` | 6 | 6 ✓ |
> | `SystemEx` | 38 | **33** | `ProtectionEx` | 0 | 0 ✓ |
> | `FileEx` | 21 | **19** | `Core.Reflection` | 0 | 0 ✓ |
> | `DataEx` | 19 | 19 ✓ | `Helper.Notify` | 0 | 0 ✓ |
>
> 偏差都在 ±10 以内、**排序与量级不变**，结论（`Struct`/`ConvertEx` 独大；
> `ProtectionEx`/`Core.Reflection`/`Helper.Notify` 确为 0）不受影响。
> **但具体数字以复算值为准**，上表原值仅作参考。
> 另外 `Core.Reflection` 的 0 很容易误判 —— `grep Core\.Reflection` 会命中 6 处
> `Core.ReflectionEx`，**必须按词边界匹配**才得到 0。

**三个"零外部使用者"的模块**（`Helper.Notify`、`Core.ProtectionEx`、`Core.Reflection`）+ **ProtectionEx 全仓 0 引用**，仍然要被 19 个项目编译期引用、打进 bin。

**引用量极低的下游项目**（纯负担）：

| 项目 | 引用 Extension 的次数 |
|---|---|
| `Vision/DPLibrary_Base_F48` | **0** |
| `FlowNodes/FlowNodes.Workbench` | **0** |
| `Communication/CommunicationDemo` | **1** |

---

### 2.8 vendor 依赖的真实分布：**两个空类撑起了 Cognex 和 OpenCV 的依赖**

逐文件核实 `Core/ImageEx/`（**必须按"扩展方法的实际调用点"统计，只 grep 类名会全部漏掉**）：

| 文件 | public 成员 | 外部真实调用 | vendor 依赖 |
|---|---|---|---|
| `CogImageEx.cs` | **无 —— 空类，只剩一句 `using Cognex.VisionPro;`** | **0** | Cognex |
| `MatEx.cs` | **无 —— 空类，只剩 using** | **0** | Halcon + **OpenCvSharp** |
| `BitmapEx.cs` | `ToHImage(Bitmap)`、`ToImageSource(Bitmap)` | `ToImageSource` **2 处**（`WPFControl/.../PrintService.cs:63,113`） | Halcon + System.Drawing |
| `RawEx.cs` | `ToBitmap(byte[])`、`ToHImage(byte[])` | `ToHImage` **4 处**（`Packing Machine/.../VisionTool.cs:85,137`、`WPFControl/.../CamManagerService.cs:78,121`）；`ToBitmap` **0 处** | Halcon + System.Drawing |
| `HImageEx.cs` | `SaveImage`×2、`OpenImage`×2、`ToIntPtr`、`ToMat` | `SaveImage` **4 处**、`OpenImage` **1 处**、`ToIntPtr` **1 处**；**`ToMat` 只出现在注释里（0 处真实调用）** | Halcon + **OpenCvSharp** |
| `IntPtrEx.cs` | `ToHImageByte` / `ToHImageUint2` / `ToHImageRGB`（Halcon）、`ToCogImage8Grey` / `ToCogImage24PlanarColor`（Cognex） | ⚠️ **原文「5 个方法全部 0 处调用」是错的**（见下方更正）。实际：`ToCogImage8Grey` / `ToCogImage24PlanarColor` / `ToHImageUint2` **0 处**；`ToHImageByte` / `ToHImageRGB` **各 1 处**，调用方是 `BitmapEx.ToHImage`（`BitmapEx.cs:28,32`） | **Cognex** + Halcon |

> ### ❗ 对上一行的更正（2026-09-15）
>
> 「`IntPtrEx` 5 个方法全部零调用」**是事实错误**。`ToHImageByte` / `ToHImageRGB` 被
> `BitmapEx.ToHImage` 调用（`BitmapEx.cs` 第 28 / 32 行）。**照原文删 5 个方法会报 2 个 `CS1061`。**
>
> **根因**：`IntPtrEx` 是扩展方法类，调用形如 `.ToCogImage8Grey(` / `.ToHImageByte(`，
> **只 grep 类名会漏掉全部调用点**。⇒ 做「谁在用」统计**必须 grep 方法名**。
>
> **`BitmapEx.ToHImage` 本身倒是 0 调用**，所以真实调用链是：
> `BitmapEx.ToHImage`（0 调用）→ `IntPtrEx.ToHImageByte` / `ToHImageRGB`。
> 按新的「不删代码」原则，**这条链一个都不删**，拆程序集时整体归入 Halcon 卫星项目即可。

**三个"零外部使用者 + 撑起 vendor 依赖"的代码块**（按新原则**一个都不删**，只搬）：

1. `Core/ImageEx/CogImageEx.cs` —— **空类，零成员** → 搬进 `Extension.Vision.Cognex`
2. `Core/ImageEx/MatEx.cs` —— **空类，零成员** → 搬进 `Extension.Vision.Halcon`（它同时 `using` Halcon 与 OpenCvSharp）
3. `Core/ImageEx/IntPtrEx.cs` 的 2 个 `ToCogImage*` 方法 → 搬进 `Extension.Vision.Cognex`；
   同文件的 3 个 `ToHImage*` 方法 → 搬进 `Extension.Vision.Halcon`

**推论（处方已按「不删」原则改写）：**

> **把**上述几块连同 `HImageEx.ToMat()`、`RawEx.ToBitmap()` **一起搬进卫星程序集**之后，
> `Extension.csproj` 里的 **Cognex ×2 与 OpenCvSharp4 的引用就可以摘掉** ——
> **`Extension.dll` 的依赖同样从 10 → 7，且下游一行都不用改、代码一行都没少。**
>
> 剩下的真实 vendor 依赖**只有 Halcon**（`RawEx.ToHImage`、`HImageEx.SaveImage/OpenImage/ToIntPtr`、
> `BitmapEx.ToImageSource`）—— **只有它值得独立打包成 `Extension.Vision.Halcon`**。
>
> ⛔ 原文这里是「删掉……之后」，**已作废**：正确动作是「**搬走**……之后」。

---

### 2.9 ❗ 实测复核（2026-09-15）：本报告对「污染」的估计**偏大**，且拆项目**未必划算**

> 触发这次复核的是用户的一句反问：「**为什么一定分项目 全部走一个不行吗**」。
> 去仓库里实测后，发现本报告前面几处把话说重了。**结论：单程序集完全可行，拆项目属于过度设计。**

#### 实测数据（`find . -path "*/bin/*" -iname "<dll>"`，已排除 `obj/`）

| vendor dll | 单文件 | 下游 bin 副本数 | 总占用 | **全仓有几个项目「直接」引用它** |
|---|---|---|---|---|
| `Cognex.VisionPro.Core.dll` | 5.9 MB | **33** | **192.7 MB** | **0 个** ⚠️ |
| `Cognex.VisionPro.dll` | 312 KB | 33 | 10.1 MB | **0 个** ⚠️ |
| `halcondotnet.dll` | 1.6 MB | 56 | 84.4 MB | **7 个** |
| `OpenCvSharp.dll` | — | 12 | 11.0 MB | **2 个** |

**「7 个项目自己引了 halcondotnet」**（逐个查 csproj 确认）：
`FlowNodes/FlowNodes`、`FlowNodes/FlowNodes.Workbench`、`Packing Machine/Packing Machine`、
`Vision/Ctr_WPF_F48`、`Vision/DPLibrary_Halcon_F48`、`Vision/Vision`、`Vision/VisionDemo`、`WPFControl/WPFControl`。

**「2 个项目自己引了 OpenCvSharp4」**：`Vision/DPLibrary_Halcon_F48`、`Vision/VisionDemo`。

#### 由此产生的两处更正

> **更正 1 —— Halcon 的 84 MB 不是 Extension 强加的。**
> 本报告 §0 / §2.8 / §3.2 反复说「Extension 把 Halcon 强加给 19 个下游」——
> **这话不对**。那 7 个项目**自己就在用 Halcon**，拆了项目它们的 bin 里照样有。
> Halcon 的「污染」实际只影响剩下那几个不碰视觉的项目，且**总量里绝大部分是合理需求**。

> **更正 2 —— 真正的问题只有一句话：Cognex 的 203 MB 是纯污染。**
> 全仓 **没有任何项目**直接引用 Cognex，33 份副本 100% 来自 `Extension.csproj` 的
> `<Reference>`。而它只被**一个空类 + 2 个零调用方法**撑着。
> 最直观的受害者：**`DataManager` 全家 3 个项目 + `Communication` / `Device` / `Secs` /
> `ScriptEditor` 的 bin 里，各自躺着一份 6.2 MB 的 Cognex 视觉库** —— 一个数据管理模块背着机器视觉库。

#### 所以：要不要拆项目？—— 三个方案，都不删代码

| 方案 | 成本 | 效果 | 副作用 |
|---|---|---|---|
| **A. 什么都不做** | **0** | 现状 | 203 MB bin 冗余（Cognex） |
| **B. 给 Cognex 的 `<Reference>` 加 `<Private>false</Private>`** | 改 **2 行** csproj | 不再复制到任何 bin | ⚠️ 将来真去调 `ToCogImage*` 时是**运行时** `FileNotFoundException`，编译期不报 |
| **C. 拆 `Extension.Vision.Cognex`**（原 v2 处方） | 建 1 个 csproj + 搬 2 个文件 + 改命名空间 | 同 B | 将来要用时是**编译期**报错（会提示你补 `ProjectReference`），**比 B 安全**，但成本高一个数量级 |

**B 与 C 的效果完全相同**，差别只在「将来真要用这个 API 时」的报错时机。

#### 什么情况下才**真的**该拆程序集（本仓一条都不满足）

1. 要**发布 NuGet 包**给别人用 —— 别人不该被强塞 vendor；
2. 有项目要**部署到没装 Cognex / Halcon 的机器**，且不允许带这些 dll；
3. 需要**独立的版本号 / 发布节奏**。

本仓是**同一个 `Frame.sln` 内的 monorepo 内部库**，三条都不成立。
⇒ **「全部走一个」是可以的；拆程序集属于过度设计。**

> **推荐**：**A 或 B**。若 203 MB 不心疼 → A（最保守，完全不动）；
> 若在意 → B（两行改动，不建项目、不删代码、不动命名空间、不改任何下游）。
> **C 仅在将来要对外发包时才值得做。**

---

## 3. 「可复用代码都放 Extension」是否合适

### 3.1 集中本身是对的

- 全仓唯一共享库，避免了 `StringEx` 之类被复制 19 份。
- `ConvertEx`(60) / `Struct`(81) / `ControlEx`(38) 的高引用量说明它确实承担了共享职责。
- 全仓没有发现重复实现的**通用工具类**（搜出来的 `*Helper` 全是 vendor 适配器，见 §3.3）。

**所以问题不在"集中"，在"粒度"。**

### 3.2 问题一：单程序集 = 依赖污染 + 变更放大

**(a) 依赖污染（最严重）**

`Extension.csproj` 同时挂着 Cognex ×2、Halcon、OpenCvSharp4、MaterialDesignThemes、Prism.Core、Mapster、MiniExcel、FastDeepCloner、System.Drawing.Common。

因为 C# 是**程序集级**依赖：**任何只想用 `StringEx` 的项目，编译期就会拿到全部 10 个依赖，运行时 bin 里会被拖进 Cognex / Halcon / OpenCV 的 native dll。**

实测受害者：

| 项目 | 实际用了什么 | 被迫背上什么 |
|---|---|---|
| `Vision/DPLibrary_Halcon_F48` | `IntPtrEx` / `ImageEx` / `EnumEx` / `ConvertEx` / `ControlEx`（5 处） | Cognex 全套 |
| `Communication/Communication1` | `ConvertEx` / `DataEx` / `EnumEx` / `Channel`（~15 处） | Cognex + Halcon + OpenCV + MaterialDesign |
| `WPFCustomControls` | 只用 `ThemeEx` + `ConvertEx`（3 处） | Cognex + Halcon + OpenCV |

**(b) 变更放大**

改 `StringEx` 里一行 → 19 个项目重编译。`Extension.dll` 版本一 bump，全仓跟着动。而 `Extension` 是**单版本**的，无法对"我只依赖 ConvertEx"的项目做独立版本约束。

**(c) 关注点混装反过来削弱了"可复用性"**

不是所有"能复用"的东西都应该"给所有人复用"：

- `Core/ProtectionEx/`（反调试 / 防篡改 / 反工具进程）—— 是**发布形态**相关，不该出现在每个内部库
- `Core/SecurityEx/`（AES/RSA/授权）—— 同上，且是 LicenseTool 的配套
- `DesignPattern/**/Advanced*` —— 与已依赖的 `Prism.Core`（`IEventAggregator`、`IContainerRegistry`）、`System.Threading.Channels`（已有 `Helper/Channel`）**功能重叠**，属于"重造轮子 + 教学"

### 3.3 问题二：有些代码根本不该在"通用库"里

| 文件 | 为什么不该在通用库 | 建议归属 |
|---|---|---|
| `Struct/LogInfo`(+`ELogLevel`) | 是**日志业务的契约**，不是通用工具 | 契约层 / 使用方自己的 Contracts |
| `Struct/ImageInfo` | 是**图像业务 DTO**（`ID` + `ImagePath`） | 同上 |
| `Struct/DeviceStatus`(`EDeviceStatus`: Idle/Ready/Running) | 是**设备领域枚举** | 同上 |
| `Struct/KeyValue` | ⚠️ **经核实不能删**。它**实际在用**：`Communication/H101/H101Helper.cs`（`List<KeyValue>` 参数与 `new KeyValue()`）、`Communication/H101/ZS/ZSMesHelper.cs`（`List<KeyValue>` 属性）、`Device/PrintSoftware/BartenderHelper.cs`、`Device/PrintSoftware/IPrintSoftware.cs`（接口签名）。且它是**可变的 `class`**（有 `new KeyValue()` + 逐属性赋值 + 集合初始化用法），**不能等价替换成 `KeyValuePair<string,string>`**（后者是只读 struct、无无参构造、属性不可写） | **保留**。但它是**业务契约**，应归入契约层，而不是留在通用工具层 |
| `Core/ThemeEx/ThemeService` | 是**WPF 应用级服务**（带 `ConfigPath`、会落盘 `Config/Theme.json`），不是工具类 | 抽 `IThemeService` 契约留在库里，实现留给宿主 |
| `Core/ControlEx/*` | WPF UI 扩展，却放在语义为"不依赖 UI"的 `Core` 下 | UI 层（或并入 `WPFCustomControls`） |
| `Core/ProtectionEx/*` | 发布形态相关 | 独立可选包 |
| `DesignPattern/**/*Example.cs` | 教学示例，2,149 行进生产 dll | **只移不删** → `samples/DesignPattern.Samples/` |

### 3.4 问题三：真正该复用的**契约**没进来（但实现也不该进来）

全仓有大量**策略并行实现**，看起来"该抽到 Extension"，其实**不该抽实现、只该抽接口**：

| 模块 | 并行实现 | 现状 |
|---|---|---|
| 日志 | `Log4NetHelper` / `NLogHelper` / `SerilogHelper` | 各有一套，无共同接口 |
| Excel | `EPPlusHelper` / `MiniExcelHelper` | 各有一套 |
| SQL | `DapperHelper<T>` / `EFCoreHelper<T>` / `SqlSugarHelper<T>` | 各有一套 |
| 通信协议 | `Communication/` 与 `Communication1/` 两套并行（`SerialHelper`、`SocketHelper`、`MCHelper`、`ModbusRTUHelper` 重复） | 双份代码 |
| 设备 | `Device/` 下 10+ 个 vendor `*Helper`（Hik/DaHua/Basler/CST/OPT/SATO/Zebra…） | 各自实现 |

这些**都绑 vendor SDK**，把实现搬进 Extension 会让污染更严重（等于把 Cognex 问题复制成 10 份）。正确做法是：

> **Extension 只提供 `ILogSink` / `IExcelWriter` / `ISqlExecutor` / `ICamera` 这样的接口和契约；实现在各业务项目里，通过 DI 注入。**

这正好反推出 Extension 应有的边界定义 —— 见 §4.1。

---

## 4. 建议的目标分类

### 4.1 先定边界，再定目录

**Extension 的准入规则（一句话版）**：

> 一个类型能进 Extension，必须同时满足：
> 1. **无业务语义** —— 不出现"设备""日志""订单""图像任务"这类领域名词；
> 2. **无 vendor 绑定** —— 不引用任何具体 SDK（Cognex/Halcon/OpenCV/Hik…）；
> 3. **无宿主假设** —— 不读 `App.xaml`、不假设有 `Config/` 目录、不假设在 WPF 里跑（除明确的 UI 层）；
> 4. **变化原因单一** —— 它的改动不会因为"换了个视觉 SDK"或"业务改了字段"而被迫发生。

**反过来说**：`DeviceStatus`（违反 1）、`CogImageEx`（违反 2）、`ThemeService`（违反 3）、`Struct/LogInfo` 内嵌 Mapster 映射（违反 4）—— 按这条规则立刻被筛出来，与 §2、§3 的实证完全对应。

### 4.2 方案 A（推荐先做）：单 csproj 不动，按"依赖 + 变化原因"重排

成本低、可增量、不破坏 19 个下游的引用关系，只改目录与命名空间。

```
Extension/
├── Primitives/      纯 BCL 算法与扩展   ← ConvertEx/*, CompareEx/BitOperationEx, EnumEx,
│                                          StringEx, HexEx, BoolEx, DecimalEx, RegexEX, DateTimeEx, ASCIIEx
├── Collections/     集合与缓冲         ← CollectionEx/ListEx, Tool/RangeObservableCollection,
│                                          Tool/ConcurrentList, DataEx/BufferEx
├── Concurrency/     并发原语           ← TaskEx, Helper/Channel, Helper/TaskHelper/*, Tool(并发部分)
├── Serialization/   序列化/压缩        ← ConvertEx/JsonEx, FileEx/CSVEx, DataEx/ZIPEX
├── IO/              文件系统           ← FileEx/{FileEx,DirectoryEx,ExcelEx},
│                                          ScheduledDeletion（SystemEx 与 FileEx 两份都保留）
├── Platform/        进程/系统/平台     ← SystemEx/{ProcessEx,CpuMonitorEx,DelayEx,FpsEx,LangSwitchEx},
│                                          IntPtrEx/DynamicDllLoaderEx, IntPtrEx/SafeMemoryHandle
├── Imaging/         图像基座（无 vendor）← ImageEx/{BitmapEx,RawEx,IntPtrEx}
├── Vision/          按 vendor 分子目录  ← ImageEx/CogImageEx → Vision/Cognex/
│                                          ImageEx/HImageEx  → Vision/Halcon/
│                                          ImageEx/MatEx     → Vision/OpenCv/
├── Security/        加密与授权         ← SecurityEx/*（+ LicenseTool）
├── Protection/      发布形态保护       ← ProtectionEx/*
├── Ui/              WPF 扩展与主题     ← ControlEx/*, ThemeEx/*
├── Composition/     DI / 映射装配      ← ReflectionEx/{ReflectionEx,RegisterServicesEx,RegisterNavigationViewEx},
│                                          Helper/Mapping
├── Contracts/       跨模块契约与 DTO   ← Struct/*（去掉内嵌 Mapping，改独立 Profile）
└── Samples/         教学示例（出库）   ← DesignPattern/**/*Example.cs
```

**同时必须做的清理**：
- 删 `Core/SemaphoreEx/`、`Core/FlowAttribute/`、`Properties/`（含 csproj 里的 `<Folder Include>`）
- 删 `Core/FileEx/ScheduledDeletionEx.cs`（`internal` 且全仓零引用，**不可达**）；**保留** `Core/SystemEx/ScheduledDeletion.cs`（Packing Machine 在用）
- **再删 `Core/SystemEx/ScheduledDeletion.cs` 里的 `DeleteExpiredFiles1`（约 94 行）与仅供它用的 `Log`** —— 同为全仓零调用（见 §2.3.1）
- **不要删 `Struct/KeyValue`** —— 它在 Communication / Device 里实际使用，详见 §3.3
- `Struct/` 的可变 `struct` → `record`；Mapster 配置从 DTO 文件里拆出去
- 删空 catch（`DynamicDllLoaderEx`、`FileEx.Write`）

### 4.3 方案 B（结构级，解决依赖污染）：按依赖方向拆 csproj

```
                     ┌──────────────────────────┐
                     │ Extension.Abstractions   │  接口/契约/DTO/枚举，零外部依赖
                     └────────────▲─────────────┘
                                  │
        ┌─────────────────────────┼──────────────────────────┐
        │                         │                          │
┌───────┴────────┐   ┌────────────┴───────────┐   ┌──────────┴─────────┐
│ Extension.Core │   │ Extension.Runtime      │   │ Extension.Contracts│
│ 纯 BCL 原语    │   │ 并发/映射/DI 装配      │   │ 领域契约           │
│ 零外部依赖     │   │ (Prism, Mapster)       │   │                    │
└───────▲────────┘   └────────────▲───────────┘   └────────────────────┘
        │                         │
┌───────┴─────────────────────────┴──────────────────────────────────┐
│ Extension.IO (MiniExcel) / Extension.UI (MaterialDesign) /         │
│ Extension.Imaging (System.Drawing)                                 │
└───────▲────────────────────────────────────────────────────────────┘
        │
┌───────┴────────────────────────────────────────────────────────────┐
│ Extension.Vision.Cognex / .Halcon / .OpenCv  (各带自己的 SDK)      │
│ Extension.Security / Extension.Protection                          │
└────────────────────────────────────────────────────────────────────┘
```

**关键收益**：`Communication1` 只引 `Extension.Core` 时，bin 里**不会**出现 Cognex / Halcon / OpenCV / MaterialDesign。

**代价**：19 个下游的 `ProjectReference` 要改；命名空间若同步调整，是跨 19 个项目的机械性改动。建议**按层分批提交**（先拆 Vision/Protection —— 收益最大且引用最少，见 §2.7 的引用矩阵），不要一次性全拆。

### 4.4 优先级建议

| 优先级 | 动作 | 收益 | 风险 |
|---|---|---|---|
| **P0** | **搬**（不删）2 个空类（`CogImageEx` / `MatEx`）+ `IntPtrEx` 的 2 个 `ToCogImage*` 进 `Extension.Vision.Cognex`；**搬** `HImageEx` / `RawEx` / `BitmapEx` / `IntPtrEx` 的 3 个 `ToHImage*` 进 `Extension.Vision.Halcon`；随后从 csproj 摘掉 Cognex ×2 与 OpenCvSharp4 | **外部依赖 10 → 7**，且**代码一行不少、下游基本不用改** | 低（只搬文件 + 补 `ProjectReference`） |
| **P0** | 3 个空目录与 `ScheduledDeletionEx` / `DeleteExpiredFiles1` **保持原样**（不删） | —（原「立刻减负、消除歧义」的收益已放弃，换取不动用户代码） | 无 |
| **P1** | 方案 A 的目录/命名空间重排 + 准入规则写进 CONTRIBUTING | 新人可判断"该放哪" | 中（跨 19 项目的机械改名） |
| **P1** | 拆 `Extension.Vision.Halcon`（唯一剩下的真实 vendor 依赖） | `Extension.dll` 不再拖 Halcon native dll | 低（下游 5 个文件） |
| **P1** | `DesignPattern/**/*Example.cs`（2,149 行）**移**出运行时 dll（不删） | 减 13% 代码量 | 低 |
| **P2** | 抽 `IThemeService` / `ILogSink` / `IExcelWriter` 等契约到 Abstractions | 让三套并行实现收敛 | 中（需改业务项目） |
| **P2** | `Struct/` 可变 struct → record，映射配置拆出 | 消除反模式与双向耦合 | 中（序列化/二进制兼容） |

> ⛔ **上表 P0 两行的原文（「删 2 个空类 + 5 个零调用方法」「删零引用的 `ScheduledDeletionEx` 与
> `DeleteExpiredFiles1`、3 个空目录」）已按 2026-09-15 勘误作废。** 现行处方一律是「**搬**」或「**不动**」。

> ⚠️ **`ScheduledDeletion.Log` 无论如何都不能删**（已核实）：它被**在用的** `DeleteExpiredFiles`（11 处调用）
> 以及私有 helper `GetAllDateFolders` / `TryParseDateFromPath` / `GetDirectorySize` 调用，删掉会直接 CS0103。
> **零引用的只有 `DeleteExpiredFiles1`**（第 115 行）—— 但按新原则，它同样**保留**。

---

## 5. 与已有 `ARCHITECTURE-OPTIMIZATION.md` 的关系

`Extension/ARCHITECTURE-OPTIMIZATION.md`（9-07）已经做了**结构审计**，本报告不重复它的结论，只补它没回答的两个问题：

| | ARCHITECTURE-OPTIMIZATION.md | 本报告 |
|---|---|---|
| 拆 csproj | ✅ 已给出 `Extension.Abstractions/Core/Infrastructure/Imaging/Vision.*/UI/Wiring` 方案 | 补充"**为什么必须拆**"的量化证据（19 个下游 × 10 个传递依赖 + 引用矩阵） |
| 分类标准 | ❌ 未涉及 | ✅ 指出顶层 **5 套分类标准并存**是根因 |
| "该不该都放 Extension" | ❌ 未涉及 | ✅ 给出**准入规则 4 条**，并识别出 8 类"不该在通用库"的代码 |
| 该复用但没进来 | ❌ 未涉及 | ✅ 指出日志/Excel/SQL/通信的并行实现，应**抽接口不抽实现** |

**建议**：两份合并维护 —— 把 §4.1 的准入规则和 §4.4 的优先级合并进 `ARCHITECTURE-OPTIMIZATION.md`，本报告作为其附录（证据部分）。

---

## 6. 联网核对：与官方 / 业界指导的一致性

> 检索日期 2026-09-14。核对目的：确认上面的判断不是个人偏好，而是有官方文档与业界原则支撑。

### 6.1 逐条对照

| 本报告的结论 | 权威来源 | 原文 |
|---|---|---|
| **单程序集无法独立版本化**（§3.2b） | MS《Assemblies in .NET》 | "The assembly is **the smallest versionable unit** in the common language runtime. All types and resources in the same assembly are **versioned as a unit**." 开篇更直白："Assemblies are the fundamental units of **deployment, version control, reuse, activation scoping, and security permissions**." |
| **`using StringEx` 也要背 Cognex = 强迫下游依赖不需要的东西**（§3.2a） | Clean Architecture 第 13 章 **CRP**（共同复用原则） | "**Don't force users of a component to depend on things they don't need.**" / 中文版："CRP 原则实际上是 ISP 原则的普适版……**不要依赖带有不需要的类的组件**。" |
| **分类应按"变化原因"而非"写法/名字"**（§2.1、§2.2） | 同章 **CCP**（共同闭包原则） | "**Gather into components those classes that change for the same reasons and at the same times.** Separate into different components those classes that change at different times and for different reasons." |
| **集中复用要有共同主题**（§3.1） | 同章 **REP**（复用/发布等同原则） | "**The granule of reuse is the granule of release.**" / "组件中的类与模块必须是**彼此紧密相关**的……它们之间应该有一个共同的主题或者大方向。" |
| **`*Ex` 目录名不合适**（§2.2） | MS《Extension Methods》设计准则 | "❌ **AVOID generic naming of namespaces dedicated to extension methods (e.g., "Extensions"). Use a descriptive name (e.g., "Routing") instead.**" —— 这条几乎是逐字针对 `ConvertEx/`、`FileEx/`、`DataEx/` 这类命名 |
| **扩展方法应按"所属功能"分命名空间**（§2.3） | 同上 | "❌ **DO NOT define extension methods implementing a feature in namespaces normally associated with other features.** Instead, define them in the namespace associated with the feature they belong to." |
| **`Struct/`（单数 + 按语言构造）命名不合适**（§2.5） | MS《Names of Namespaces》 | "✔️ CONSIDER using **plural** namespace names where appropriate."（举例 `System.Collections` 而非 `System.Collection`）+ "❌ DO NOT use **organizational hierarchies** as the basis for names in namespace hierarchies… **Organize the hierarchy of namespaces around groups of related technologies.**" |
| **`IntPtrEx/` 目录与 `IntPtrEx.cs` 文件同名**（§2.3） | 同上 | "❌ **DO NOT use the same name for a namespace and a type in that namespace.**" |
| **该删掉不必要的依赖**（§3.2） | MS《Dependencies and .NET libraries》 | "✔️ **DO review your .NET library for unnecessary dependencies.**"（上下文：无法预知下游还会引入什么包，减少依赖是降低 diamond dependency 风险的手段） |
| **`Core/Shared/Common` 会变成黑洞**（§2.1、§3.2c） | 《Modular Monolith in .NET》(2026-06) | "**Catch-all `Core`/`Shared`/`Common` libraries scale badly: no clear contents, no clear owner. Do not feed The Black Hole.**" 并称"按技术层切分"正是根因："One 'create user' feature ends up spread across four projects, and **none of them is *about* users**." |
| **边界应由 csproj 强制，而不是靠约定**（§4.3） | 同上 | "**The boundary is enforced by the `.csproj`, not by code review.**" / "It is not a guideline you have to remember in a pull request — it is **the compiler saying no**. That is the difference between a convention and a boundary." |
| **目录与命名空间不一致**（§2.4） | Mark Seemann《Folders versus namespaces》+ 分析器 **IDE0130** | 该约定有官方分析规则 IDE0130 守护（要求命名空间与文件夹匹配） |

**结论：本报告的 6 条核心判断全部有官方文档或经典架构原则背书**，其中 `*Ex` 命名一条几乎是 Microsoft 设计准则逐字点名的反例。

### 6.2 四处需要修正 / 补充的地方

核对过程也暴露了我原报告的三个不严谨之处，在此修正：

**(1) 拆到什么程度没有唯一答案 —— CCP 与 CRP 天然互相拉扯**

原报告把"拆 csproj"讲得像唯一正确解。Clean Architecture 明确说这三个原则**互相竞争**：

> "REP 和 CCP 原则是黏合性原则，它们会让组件变得更大，而 CRP 原则是排除性原则，它会尽量让组件变小。**软件架构师的任务就是要在这三个原则中间进行取舍。**"

而且"当下适用的分割方式可能明年就不再适用了"。**所以本报告 §4.3 的方案 B 不是"必须达到的目标架构"，而是"当前 19 个下游已经成熟到可以复用，因此该向 CRP 一侧移动"的阶段性判断。**

**(2) "加目录层级"本身不是解药**

Mark Seemann 直接质疑了"把东西分进桶里"的价值：

> "It looks as though things are organised, but **if the assignment of things to buckets is done without a predictable system, then what benefit does it provide?** Does it make things easier to find, or is the sense of order mostly illusory?"

这**强化**了 §2.1 的核心论点：Extension 的问题不是"层级不够多"（`Core/` 下已经有 17 个子目录），而是**没有可预测的准入规则**。所以 §4.1 的"准入规则 4 条"比 §4.2 的目录树更重要 —— 先有规则，目录才有意义。

**(3) 官方还提供了一个原方案没考虑的选项：shared source package**

MS《Dependencies》建议：对于**小的、内部的**功能，可以不建独立程序集，改用 shared source package（`PrivateAssets="All"`，源码随引用方一起编译，**不产生程序集依赖、无版本冲突**）。

> "✔️ CONSIDER referencing shared source packages for small, internal pieces of functionality."
> "❌ DO NOT have shared source package types in your public API."（必须是 `internal`）

对本仓的适用性：`Helper/Notify`（14 行）、`Core/DataEx/CloneEx`（12 行）这类**极小的内部工具**适合走这条路；但**不适合** `Struct/*`（要跨程序集作为公共契约，而 shared source 类型无法跨程序集交换身份）。**代价是它们不能出现在公共 API 上**，所以只能用于纯内部实现细节。

**(4) 两处引用统计错误已更正（第一版报告算错了）**

第一版统计外部引用时，**项目根目录清单里漏了 `Packing Machine`**（目录名带空格，在循环里被漏掉），导致两条结论错误。复核后的更正：

| 位置 | 第一版（错） | 复核后（对） |
|---|---|---|
| `Struct/KeyValue` | "与 BCL 重复，删除" | **不能删**，Communication / Device 在用（见 §3.3） |
| `ScheduledDeletion` | "两份重复实现，合并" | `SystemEx` 版被 `Packing Machine` 使用；`FileEx` 版**零引用**（见 §2.3）—— 但 ⛔ **零引用不等于该删**，2026-09-15 起**两份都保留** |

> ⛔ **勘误（2026-09-15）**：上表复核结论里「应**删除**后者」已作废。
> **"零引用 → 可删"这个推论本身就不成立** —— 用户原则：「没有使用的你不应该删除」。

**教训 1**：本仓有一个目录名带空格的 `Packing Machine/`，任何按目录循环的 grep 统计都必须显式带上它，
否则会漏掉唯一的使用方。**做"谁在用"结论前，必须用一次全仓 `.` 的 grep 兜底复核。**

**教训 2**：**统计扩展方法的使用点必须 grep 方法名**（`.ToHImageByte(`），
只 grep 类名会漏掉全部调用点 —— §2.8 的「`IntPtrEx` 5 个方法全部零调用」就是这么错的。

**教训 3**：**"零调用"只能用来判断"这个依赖能不能搬走"，不能用来判断"这段代码该不该删"。**

### 6.3 一条可以立刻执行的检查

.NET 10 已把 **`NuGetAuditMode` 默认设为 `all`**（默认审计传递依赖），并默认开启 **package pruning**。官方博客的量化数据：

> "projects with these defaults have **70% fewer transitive vulnerability reports**" / "pruning can reduce restore time by up to **50%**"

本仓目标框架是 `net9.0-windows7.0`。**升级到 `net10.0` 后可以直接跑一次 `dotnet list package --include-transitive`**，把 Extension 传递到 19 个下游的依赖图**打印出来当证据**——这比本报告的静态分析更有说服力，也是验证 §4.3 拆分效果（"`Extension.Core.dll` 不再依赖 Cognex/Halcon/OpenCV"）的最直接手段。

---

## 7. 如果只做一件事

> ⛔ **本节原方案（「删掉两个空类和几个零调用方法」）已按 2026-09-15 勘误作废。**
> 下面是改写后的版本：**动作从「删」换成「搬」，代码一行不少。**

**把用到 Cognex / OpenCV 的那几个类型搬进独立卫星程序集，从而把 Cognex 与 OpenCV
从 `Extension.dll` 的依赖里摘出去。**

理由：逐文件核实（§2.8）后发现，`Cognex.VisionPro` 与 `OpenCvSharp4` 这两个包
**在 `Extension` 里的实际使用面窄到可以整块搬走**：

- `Core/ImageEx/CogImageEx.cs` —— **空类，没有任何成员**，只有一句 `using Cognex.VisionPro;`
- `Core/ImageEx/MatEx.cs` —— **空类，没有任何成员**（同时 `using` Halcon 与 OpenCvSharp）
- `Core/ImageEx/IntPtrEx.cs` 的 `ToCogImage8Grey` / `ToCogImage24PlanarColor`
  —— 零调用，但是整个解决方案里**唯一的 Cognex 使用点**
  ⚠️ 同文件的 `ToHImageByte` / `ToHImageRGB` **不是零调用**（被 `BitmapEx.ToHImage` 调用），
  原文「5 个方法全部零调用」是**事实错误**，详见 §2.8 的更正块
- `HImageEx.ToMat()` —— 零调用（只出现在注释里），但是唯一的 OpenCV 使用点

所以最划算的一步**不是**"建 4 个 csproj 做大改造"，也不是"删代码"，而是：

1. **搬** `Core/ImageEx/CogImageEx.cs` → `Extension.Vision.Cognex`
2. **搬** `IntPtrEx` 里的 2 个 `ToCogImage*` 方法 → `Extension.Vision.Cognex`
3. **搬** `Core/ImageEx/MatEx.cs` → 随 Halcon 卫星项目（或单建 `Extension.Vision.OpenCv`）
4. **搬** `HImageEx.cs` / `RawEx.cs` / `BitmapEx.cs` / `IntPtrEx.cs` → `Extension.Vision.Halcon`
5. 从 `Extension.csproj` 摘掉 Cognex ×2 `<Reference>` 与 OpenCvSharp4 `<PackageReference>`
   （**只有代码搬走了才敢摘引用**）

**收益**：`Extension.dll` 的外部依赖 **10 → 7**，19 个下游的 bin 里不再出现 Cognex / OpenCV 的 native dll。
**代价**：新建 2 个 csproj + 给 6 个下游文件补 `ProjectReference`（清单见 §2.8 / `MIGRATION-CHECKLIST.md`），
**但一行源码都不删**。

做完这一步再决定要不要把 Halcon 也单独拆出去
（真正的使用点只有 3 个：`RawEx.ToHImage`、`HImageEx.SaveImage/OpenImage/ToIntPtr`、`BitmapEx.ToImageSource`）。

---

## 8. 参考资料

**Microsoft 官方**

| 文档 | 用到的内容 |
|---|---|
| [Assemblies in .NET](https://learn.microsoft.com/en-us/dotnet/standard/assembly/) | 程序集是"最小可版本化单元"，是部署/版本/复用的基本单位 |
| [Dependencies and .NET libraries](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/dependencies) | "DO review your .NET library for unnecessary dependencies"；shared source package |
| [Names of Namespaces](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-namespaces) | 按"相关技术组"组织命名空间；复数；命名空间与类型不得同名 |
| [Extension Methods](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/extension-methods) | ❌ AVOID 把扩展方法的命名空间命名为 `Extensions` 之类泛化名 |
| [NuGet Package Pruning in .NET 10](https://devblogs.microsoft.com/dotnet/nuget-package-pruning-in-dotnet-10/) | 传递依赖审计与剪枝默认开启（2026-05） |

**架构原则**

| 来源 | 用到的内容 |
|---|---|
| *Clean Architecture* 第 13 章 组件聚合 | REP（复用/发布等同）、CCP（共同闭包）、CRP（共同复用）三原则及其张力 |
| [Modular Monolith in .NET: Enforcing Boundaries with Dependency Inversion](https://gaevoy.com/2026/06/30/modular-monolith-dotnet-enforcing-boundaries-dependency-inversion.html)（2026-06） | `Core`/`Shared`/`Common` 如何变成"黑洞"；边界由 `.csproj` 而非 code review 强制 |
| [Mark Seemann — Folders versus namespaces](https://blog.ploeh.dk/2023/05/15/folders-versus-namespaces/)（2023-05） | 目录层级带来的"秩序感"可能是幻觉，规则比层级更重要 |

**本项目内的相关文档**

| 文件 | 关系 |
|---|---|
| `Extension/ARCHITECTURE-OPTIMIZATION.md`（2026-09-07） | 已做结构审计并给出拆 csproj 方案；本报告补它未回答的"分类标准"与"边界定义"问题，见 §5 |
