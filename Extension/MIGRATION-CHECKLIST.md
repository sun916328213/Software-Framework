# Extension 迁移 Checklist（v2 —— 已按「不删代码」原则重写）

> 配套 `CODE-CLASSIFICATION-ANALYSIS.md`。
>
> ## ⛔ v1 作废声明（2026-09-15）
>
> v1 的**阶段 1（零风险清理）与阶段 2A（删死代码以摘掉 Cognex/OpenCV）已全部作废**，
> 按它执行的改动已被用户**整体还原**。
>
> **原因**：用户明确表态「**没有使用的你不应该删除**」。
> ⇒ 「零调用 / 零引用 / 不可达 / 是空类 / 与另一份重复」**一律不构成删除理由**。
> 源码（类、方法、文件、空目录）**一行都不删**。
>
> **诊断保留、处方更换**：报告里「`Extension` 单程序集把 Cognex/Halcon/OpenCV 传递污染到全仓」
> 这个**结论仍然成立**；但解法从「删掉用到 vendor 的死代码」换成
> 「**把 vendor 相关类型搬进独立卫星程序集**」—— 代码**继续存在**，只是换了程序集边界。

---

## 硬规则（每次动手前读一遍）

1. **不删任何源码**：不删类、不删方法、不删文件、不删空目录、不删 csproj 里的 `<Folder Include>`。
2. **不改公共 API 签名**：不改方法名、不改返回值、不删重载、不改可见性。
3. **不改行为**：空 `catch`、`Thread.Abort()`、重复实现……**发现即记录，不顺手修**。
   要修就单独提出来问用户。
4. **依赖隔离只靠「搬家」**：`git mv` 到新程序集 + 补 `ProjectReference`，仅此而已。
5. **提交必须指定明确路径**：工作区常有上百个用户未提交文件，**绝不能 `git add -A`**。

> 违反任一条 = 白做（会被整体还原）。

---

## ⚠️ 先做这个决定：到底要不要拆程序集？（2026-09-15 实测后新增）

**结论先行：本仓是同一个 `Frame.sln` 内的 monorepo 内部库，「全部走一个」是可以的，
拆程序集属于过度设计。** 实测依据见 `CODE-CLASSIFICATION-ANALYSIS.md` §2.9。

实测数据（下游 bin 副本，已排除 `obj/`）：

| vendor dll | 副本数 | 总占用 | 全仓有几个项目**直接**引用 |
|---|---|---|---|
| Cognex（Core + 主） | 33 | **203 MB** | **0 个** ⚠️ 纯污染 |
| halcondotnet | 56 | 84 MB | **7 个**（自己就要用，拆了照样有） |
| OpenCvSharp | 12 | 11 MB | 2 个 |

⇒ 真正的问题只有一句：**Cognex 的 203 MB 被复制到每个下游 bin，而全仓没人用它。**

| 方案 | 成本 | 效果 | 副作用 | 推荐 |
|---|---|---|---|---|
| **A. 什么都不做** | **0** | 现状 | 203 MB bin 冗余 | ✅ 最保守 |
| **B. Cognex 的 `<Reference>` 加 `<Private>false</Private>`** | 改 **2 行** | 不再复制到任何 bin | ⚠️ 将来真调 `ToCogImage*` 时是**运行时** `FileNotFoundException` | ✅ 省钱首选 |
| **C. 拆 `Extension.Vision.Cognex`**（下文阶段 1 / 2） | 建 csproj + 搬文件 + 改命名空间 | 同 B | 将来要用时是**编译期**报错，比 B 安全 | ⬜ 仅在要对外发包时才做 |

**B 与 C 效果完全相同**，差别只在「将来真要用这个 API 时」的报错时机。

> **阶段 1 / 2 只在选定方案 C 时才执行。** 选 A 或 B 的话，下文阶段 1/2 整段跳过。

---

## 阶段 0：准备（不改任何代码）

- [x] **0.1 确认工作区状态** ✅ 2026-09-14
  实测有 **107 个**用户未提交文件（FlowEditor 60+、DataManager 等）。对策：只提交明确路径。
- [x] **0.2 固化基线构建结果** ✅ 2026-09-14
  ```bash
  env "APPDATA=C:\Users\91632\AppData\Roaming" "ProgramData=C:\ProgramData" \
      "ALLUSERSPROFILE=C:\ProgramData" "ProgramFiles=C:\Program Files" \
      "ProgramFiles(x86)=C:\Program Files (x86)" \
      "CommonProgramFiles=C:\Program Files\Common Files" \
      dotnet build Frame.sln --no-restore -p:DebugType=none -p:DebugSymbols=false 2>&1 | tail -40
  ```
  **基线 = 1 错误 / 387 警告**。那 1 个错误是 **HEAD 既有失败**，与本重构无关：
  `FlowNodes/.../HVisionToolNodeBase.cs(141,25): CS1061 "IFlowChartService" 未包含 "IsBatchOperation"`
  —— 该符号在提交 `a05287c（Codex架构优化）` 里从接口移除，调用点没跟着改。
  **这条不修，DoD 的「错误数不高于基线」就只能按 1 计。**
- [x] **0.3 打印依赖证据** ✅ 2026-09-14
  ```bash
  dotnet list Extension/Extension/Extension.csproj package --include-transitive
  ```
  基线 vendor 依赖：`halcondotnet`（`<Reference>`）、`Cognex.VisionPro`×2（`<Reference>`）、
  `OpenCvSharp4`（NuGet）、`MaterialDesignThemes`、`Prism.Core`、`Mapster`、`MiniExcel`、
  `FastDeepCloner`、`System.Drawing.Common`。
- [ ] **0.4 建工作分支**：`git checkout -b refactor/extension-layering`
  → ⚠️ **暂缓，待用户拍板**。当前在 `master` 且有 107 个未提交文件，切分支会把它们全带过去。

> ⚠️ 本机构建三条硬约束（否则会看到假故障）：
> ① 一律 `--no-restore`（本环境 `restore` 不可用）；② 校验 XAML 必须走完整 `build`，**不能**用 `-t:Compile`；
> ③ pdb 常被 VS 锁住 → 加 `-p:DebugType=none -p:DebugSymbols=false`。**千万别删测试项目的 `obj/`。**

---

## 阶段 1（⚠️ 仅当选定方案 C 才做）：拆出 `Extension.Vision.Cognex`

**目标**：让 `Extension.dll` 不再 `Reference` Cognex —— **代码一行不删**，只是换程序集。

| 要搬的东西 | 现位置 | 说明 |
|---|---|---|
| `CogImageEx`（空类） | `Core/ImageEx/CogImageEx.cs` | 全文只有 `using Cognex.VisionPro;` |
| `ToCogImage8Grey` | `Core/ImageEx/IntPtrEx.cs` | 全仓零调用，但**保留** |
| `ToCogImage24PlanarColor` | `Core/ImageEx/IntPtrEx.cs` | 同上 |

- [x] **1.1 建 `Extension.Vision.Cognex`** ✅ 2026-09-16
  `net9.0-windows7.0`（无 `UseWPF`，Cognex 类型不需要），
  `<Reference Include="Cognex.VisionPro">` + `<Reference Include="Cognex.VisionPro.Core">`
  （`HintPath` 与原 `Extension.csproj` 相同：`..\..\Dll\Vision\VisionPro\...`），
  `ProjectReference` → `Extension`（方向：卫星 → 核心，单向）
- [x] **1.2 `git mv` 移入** ✅ 2026-09-16（本机无 git，用文件移动等效执行，待你手动提交）
  - `Core/ImageEx/CogImageEx.cs` → 新项目根目录，仅改命名空间
    `Extension.Core.ImageEx` → `Extension.Vision.Cognex`，其余逐字节保留；
  - `Core/ImageEx/IntPtrEx.cs` 的 2 个 `ToCogImage*` 方法（连 XML 注释、方法体原样）
    → 新项目 `IntPtrExCognex.cs`（类名随文件名，零调用方，见 `PHASE1-2A-REVIEW.md` §3.1）；
    原文件仅删这 2 个方法 + `using Cognex.VisionPro;`，3 个 `ToHImage*` 原样保留
- [x] **1.3 命名空间** ✅ 2026-09-16：`Extension.Core.ImageEx` → `Extension.Vision.Cognex`（2 个新文件）
- [x] **1.4 从 `Extension.csproj` 删掉两个 Cognex `<Reference>`** ✅ 2026-09-16
  （**唯一**许可从 csproj 删东西的场景：代码已搬走，引用才敢删；
  `halcondotnet` 保留，`SemaphoreEx` 的 `<Folder Include>` 未动）
- [x] **1.5 确认下游** ✅ 2026-09-16：`Extension/Extension` 之外全仓 grep
  `Cognex|ToCogImage|CogImageEx` → **0 命中**，无需给任何下游补 `ProjectReference`
- [x] **1.6 验证** ✅ 2026-09-16
  - `dotnet build Extension.Vision.Cognex.csproj --no-restore` → **0 错误** / 135 警告
    （其中 2 个 `CS8600` 是随方法体搬过来的原有写法，非新增）；
  - `Extension` 源码内 `Cognex` 命中 → **0**；`Extension.csproj` 内 `Cognex` → **0**；
  - `Extension/bin` 内残留的 2 个 Cognex dll（旧 Release 构建残留）已清理，
    新构建不再复制；`Vision.Cognex/bin` 内 Cognex ×2 + `Extension.dll` 正常
- [ ] **1.7 提交**：`refactor(extension): 拆出 Extension.Vision.Cognex，核心程序集不再引用 Cognex`
  → ⚠️ **待你手动提交**（本机无 git）。必须只指定明确路径，**绝不能 `git add -A`**：
  `Extension/Extension.Vision.Cognex/`（新目录）、`Extension/Extension/Core/ImageEx/IntPtrEx.cs`、
  `Extension/Extension/Extension.csproj`、`Extension/Extension.sln`；
  原 `Extension/Extension/Core/ImageEx/CogImageEx.cs` 的删除即“搬家”另一半，同批提交。
  另注：`Extension.sln` 原有一条悬空引用 `Extension.UI`（目录不存在，动工前已坏），本次未动。

---

## 阶段 2（⚠️ 仅当选定方案 C 才做）：拆出 `Extension.Vision.Halcon` + `Extension.Vision.OpenCv`

**目标**：`Extension.dll` 不再 `Reference` halcondotnet、不再 `PackageReference` OpenCvSharp4。
**代码一行不删。**

### 2A `Extension.Vision.Halcon`

| 要搬的东西 | 现位置 | 备注 |
|---|---|---|
| `HImageEx` | `Core/ImageEx/HImageEx.cs` | `SaveImage`×2 / `OpenImage`×2 / `ToIntPtr` **在用**；`ToMat()` 零调用但**保留** |
| `RawEx` | `Core/ImageEx/RawEx.cs` | `ToHImage` **在用** 4 处；`ToBitmap` 零调用但**保留** |
| `BitmapEx` | `Core/ImageEx/BitmapEx.cs` | `ToImageSource` **在用** 2 处；`ToHImage` 零调用但**保留** |
| `IntPtrEx` 整个文件 | `Core/ImageEx/IntPtrEx.cs` | 删掉 `ToCogImage*` 后只剩 Halcon 相关方法 |

- [ ] **2A.1 建 `Extension.Vision.Halcon`**：`net9.0-windows7.0`，`<Reference>` halcondotnet，
      `ProjectReference` → `Extension`
- [ ] **2A.2 `git mv` 移入 4 个文件**
- [ ] **2A.3 命名空间**：`Extension.Core.ImageEx` → `Extension.Vision.Halcon`
- [ ] **2A.4 从 `Extension.csproj` 删掉 halcondotnet `<Reference>`**
- [ ] **2A.5 给下游补 `ProjectReference`**（**6 个文件，逐个加、逐个编译**）：

  | 符号 | 下游文件 |
  |---|---|
  | `RawEx.ToHImage` | `Packing Machine/.../Infra/Tool/VisionTool.cs:85,137`、`WPFControl/.../CamManagerService.cs:78,121` |
  | `HImageEx.SaveImage`/`OpenImage`/`ToIntPtr` | `Vision/Ctr_WPF_F48/.../HSmartWindowView.xaml.cs:105`、`Vision/Vision/Control/HSmartWindowView.xaml.cs:106`、`WPFControl/.../DisplayQueryView.xaml.cs:80`、`Vision/DPLibrary_Halcon_F48/.../InviHelper.cs:108`、`Packing Machine/.../VisionTool.cs:91,143` |
  | `BitmapEx.ToImageSource` | `WPFControl/.../CustomControl/Services/PrintService.cs:63,113` |

  > ⚠️ `Vision/DPLibrary_Halcon_F48/.../InviHelper.cs` 同时用到 `HImageEx.ToIntPtr`（搬到 Halcon 项目）
  > 与 `Core/IntPtrEx/` 的 `DynamicDllLoaderEx` + `SafeMemoryHandle`（**无 vendor 依赖，留核心**）——
  > 它是**唯一需要同时引两个项目**的文件，**先改它、先编译它**。

- [ ] **2A.6 验证**：`dotnet list Extension/Extension/Extension.csproj package --include-transitive | grep -i halcon`
      → 期望 0 行；完整构建错误数不高于基线 1
- [ ] **2A.7 提交**：`refactor(extension): 拆出 Extension.Vision.Halcon`

### 2B `Extension.Vision.OpenCv`

`HImageEx.ToMat()` 是**全仓唯一的 OpenCV 使用点**，它会随 `HImageEx.cs` 一起进 Halcon 项目。
两个选择，**待用户拍板**：

- 方案 A（省事）：OpenCvSharp4 的 `<PackageReference>` **跟着 `Extension.Vision.Halcon` 走** ——
  核心程序集照样干净，但 Halcon 项目会同时带 OpenCV。
- 方案 B（彻底）：再建 `Extension.Vision.OpenCv`，只放 `ToMat()` 一个方法。

> ⛔ **不要**用「删掉 `ToMat()`」来解决（v1 就是这么干的，已被还原）。

- [ ] **2B.1 选定方案并执行**
- [ ] **2B.2 验证**：`dotnet list Extension/Extension/Extension.csproj package --include-transitive | grep -i opencv` → 0 行
- [ ] **2B.3 提交**

---

## 阶段 3（P1）：目录与命名空间按「依赖 + 变化原因」重排

**只动目录与命名空间，不动逻辑，不删任何东西。**

- [ ] **3.1 先写准入规则**（放进 `Extension/CONTRIBUTING.md`）
  > 一个类型能进 `Extension` 核心，必须同时满足：① 无业务语义 ② 无 vendor 绑定 ③ 无宿主假设 ④ 变化原因单一
  > 不满足的**搬到对应卫星程序集**，**不是删掉**。
- [ ] **3.2 删掉 `*Ex` 后缀**（违反 MS《Extension Methods》设计准则，见报告 §6.1）
  - `Core/ConvertEx/` → `Primitives/`（`JsonEx` 拆去 `Serialization/`）
  - `Core/FileEx/` → `IO/`（`CSVEx` 拆去 `Serialization/`）
  - `Core/ControlEx/` → `Ui/Controls/`
  - `Core/SystemEx/` → 拆成 `Platform/` 与 `IO/`
  - `Core/ReflectionEx/` → `Composition/`（`RegisterServicesEx`/`RegisterNavigationViewEx` 改 `Bootstrap*`）
  - `Core/EnumEx/`、`Core/CompareEx/`、`Core/CollectionEx/`、`Core/DataEx` → 并入 `Primitives/` / `Collections/`
- [ ] **3.3 消除重叠**：`DataEx/ZIPEX.cs` → `Serialization/`；`BufferEx` → `Collections/`；`CloneEx` → `Composition/`
- [ ] **3.4 合并 `Helper/` + `Tool/`**（二选一，**不要让两者共存**）
- [ ] **3.5 `Struct/` → `Contracts/`**，内嵌 `*Mapping` 静态类拆到独立 `Profiles/` 文件
- [ ] **3.6 批量改下游 using**（19 个项目，机械替换）
- [ ] **3.7 验证**：`grep -rn "Core\.\(Convert\|File\|Control\|System\|Reflection\|Enum\|Compare\|Collection\|Data\)Ex" --include="*.cs" .` → 0 行；完整构建
- [ ] **3.8 提交**：`refactor(extension): 按依赖分层重排目录与命名空间`

> ⚠️ **一个层一次提交**（先 `Primitives`，再 `IO`，再 `Ui`…），每层编译通过再进下一层。**不要一个巨型提交。**

---

## 阶段 4（P1）：教学示例出库（**只移不删**）

- [x] **4.1 把 `DesignPattern/**/*Example.cs` 移出运行时 dll** ✅ 2026-09-16
  → 已移到 `samples/DesignPattern.Samples/`（独立 Library 项目，已进 `Extension.sln`，
  无任何运行时项目引用它）。**只移不删**。实测共 **10 个**文件（文档原写 8 个系旧数），
  命名空间保持 `Extension.DesignPattern.*.Examples` 不变（最小 diff，隔离靠程序集边界）。
  与清单原案的偏离：用 `Library` 而非 `Console`——示例类无 `Main` 入口，
  Library 同样达成“出运行时 dll”，且零新增代码。
- [x] **4.2 `DesignPattern/` 非 Example 部分** ✅ 2026-09-16：保持原样。
  `Mediator/PublishSubscribe/ProducerConsumer/Singleton` 与 Prism/Channels 重叠**只是重叠**，
  按不删原则不动。
- [x] **4.3 验证** ✅ 2026-09-16：下游引用面复核发现 3 处 `using ...ObserverPattern.Examples;`
 （`WPFControl` 的 `UserManagerService` / `CamManagerService` / `VisionInspectionItemService`），
  但逐类型核实**全是死 using**（未引用 Examples 内任何类型，用的是 BCL 的 `ObservableCollection`）。
  处理：删 3 行死 using（唯一下游改动，不涉逻辑）→ `Extension.sln` 0 错误，
  `WPFControl.csproj` 0 错误（498 警告既有）。
- [ ] **4.4 提交** → ⚠️ 待你手动提交（本机无 git），路径：
  `Extension/samples/`（新）、`Extension/Extension.sln`、
  `Extension/Extension/DesignPattern/`（10 个文件的删除=搬家另一半）、
  `WPFControl/WPFControl/.../UserManagerService.cs`、`.../CamManagerService.cs`、
  `.../VisionInspectionItemService.cs`（各删 1 行死 using）。**绝不能 `git add -A`**。

---

## 阶段 5（P2）：契约外移

- [ ] **5.1 抽 `IThemeService`** —— 接口留库，实现可留在库但由宿主注入路径
- [ ] **5.2 抽 `ILogSink` / `IExcelWriter` / `ISqlExecutor`** —— 只抽接口，**实现留在各业务项目**
- [ ] **5.3 `Struct/` 可变 `struct` → `record`** —— ⚠️ **这是改公共 API 形状**，影响序列化兼容与
      `Mapster` 映射，必须单独提交 + 跑完整测试 + **先问用户**
- [ ] **5.4 去掉 `MappingConfig._isInitialized` 静态开关**，改 Mapster `Scan` 自动发现 Profile
- [ ] **5.5 提交**

---

## 验证命令速查

```bash
# 构建（本机必须加 --no-restore 与 DebugType=none）
env "APPDATA=C:\Users\91632\AppData\Roaming" "ProgramData=C:\ProgramData" \
    "ALLUSERSPROFILE=C:\ProgramData" "ProgramFiles=C:\Program Files" \
    "ProgramFiles(x86)=C:\Program Files (x86)" \
    "CommonProgramFiles=C:\Program Files\Common Files" \
    dotnet build <proj> --no-restore -p:DebugType=none -p:DebugSymbols=false

# 不重建也能跑测试
dotnet "<SDK目录>\vstest.console.dll" <测试dll路径> [--TestCaseFilter:"..."]

# 依赖证据
dotnet list Extension/Extension/Extension.csproj package --include-transitive

# 引用排查（务必带 "Packing Machine"，目录名含空格，漏了会误判零引用）
grep -rn --include="*.cs" "<符号名>" Communication DataManager Device FlowEditor FlowNodes \
    MotionControl "Packing Machine" ScriptEditor Secs Vision WPFControl WPFCustomControls Extension

# 统计扩展方法的使用点：必须 grep 方法名，不能只 grep 类名！
grep -rn --include="*.cs" "\.ToHImageByte(\|\.ToHImageRGB(\|\.ToHImageUint2(\|\.ToCogImage" . \
    | grep -v /obj/ | grep -v /bin/
```

> **做「谁在用」结论前，必须再用一次全仓 `.` 兜底复核** —— 第一版报告就是漏了
> `Packing Machine/` 才把 `KeyValue` 和 `ScheduledDeletion` 判错的。

---

## 风险与回滚

| 阶段 | 主要风险 | 回滚方式 |
|---|---|---|
| 1 | 下游 `ProjectReference` 漏加 → 编译失败 | 每加一个包编译一次；失败即 `git revert` |
| 2A | 同上；`InviHelper.cs` 要同时引两个项目，最易漏 | 先改它、先编译它 |
| 2B | 方案 A 会把 OpenCV 带进 Halcon 项目（不够干净但不影响核心） | 改选方案 B |
| 3 | 跨 19 项目的机械改名，易漏改 | **按层拆分提交**，单层失败只回滚该层 |
| 4 | 移出 `DesignPattern` 后若被外部调用方引用会编译失败 | 先 `grep` 确认引用面（当前仅 7 处） |
| 5 | 可变 `struct` → `record` 影响序列化/二进制兼容 | 单独一个提交，跑完整测试后再合；**先问用户** |

---

## 完成标志（Definition of Done）

> ⚠️ 前 4 条**只在选定方案 C（拆程序集）时**适用；选 A / B 的话只需看最后两条。

- [ ] `dotnet list Extension/Extension/Extension.csproj package --include-transitive`
      **不含** Cognex / Halcon / OpenCV
- [ ] `Extension/Extension/` 下**无** `*Ex` 结尾的目录
- [ ] `Extension/Extension/` 下**无** `*Example.cs`
- [ ] `Extension/CONTRIBUTING.md` 里有成文的准入规则 4 条
- [ ] `Frame.sln` 全量构建错误数 **不高于基线 1**（那 1 个是 FlowNodes 的既有失败）
- [ ] ⛔ **未删除任何源码文件 / 方法 / 目录**（本版新增的硬性验收项）
