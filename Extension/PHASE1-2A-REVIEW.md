# 复盘：阶段 1 + 2A 为什么被否（2026-09-15）

> **本文件不是改动清单。** 它记录一次**已被用户整体还原**的尝试，以及从中得到的两条硬结论。
> 当时那份 737 行 diff 已作废（`git diff -- Extension/` 现在是空的，
> 旧输出留在 `.workbuddy-ai/preview/extension-phase1-2A.diff`，**仅作历史存档，不要照它改代码**）。

---

## 1. 发生了什么

2026-09-14 按 `MIGRATION-CHECKLIST.md` v1 执行了「阶段 1（零风险清理）+ 阶段 2A（删死代码摘依赖）」，
共改动 `Extension/` 下 11 个文件、净删 531 行。构建通过（Extension 单项目 0 错误）。

**2026-09-15 用户整体还原，并给出理由：「没有使用的你不应该删除」。**

---

## 2. 被否的根本原因（不是技术问题，是原则问题）

v1 的整套推理建立在一条假设上：

> 「零调用 / 不可达 / 重复实现 ⇒ 可以删」

**这条假设是错的。** 用户的立场是：**没被用到的代码不该删**。
这类代码可能是留着备用的、当参考实现的、还没接线的、或将来要用的 ——
**「现在没人调」不等于「可以消失」**。

于是 v1 里这些「依据」全部失效：

| v1 的依据 | 实际判定 |
|---|---|
| `ScheduledDeletionEx` 是 `internal` 且全仓零引用 | ❌ **不构成删除理由** |
| `DeleteExpiredFiles1` 与 `DeleteExpiredFiles` 逻辑重复 | ❌ 重复 ≠ 可删 |
| `CogImageEx` / `MatEx` 是零成员空类 | ❌ 空 ≠ 可删 |
| `ToCogImage8Grey` / `ToHImageUint2` 零调用 | ❌ 不构成删除理由 |
| `ToMat()` 只出现在注释里 | ❌ 不构成删除理由 |
| 3 个空目录没有文件 | ❌ 空目录也不删 |
| 空 `catch` / `Thread.Abort()` 是隐患 | ❌ **发现即记录，不顺手改** |

**⇒ 修正后的处方**：依赖隔离**只靠「搬家」**，不靠「删代码」。
把 vendor 相关类型 `git mv` 进独立卫星程序集（`Extension.Vision.Cognex` / `.Halcon` / `.OpenCv`），
让它们**继续存在**，只是换了程序集边界。`Extension.dll` 因此不再 `Reference` Cognex/Halcon/OpenCV，
**下游的依赖污染问题照样解决，且一行代码没少。**

> 新方案见 `MIGRATION-CHECKLIST.md` v2。

---

## 3. 仍然有价值的核实结果（这些结论与「删不删」无关，可直接复用）

### 3.1 ⚠️ v1 checklist 有一处**事实错误**，会让人「照做即编译不过」

v1 §2A 写：

> `Core/ImageEx/IntPtrEx.cs`：5 个方法**全部零调用**，其中 `ToCogImage8Grey` / `ToCogImage24PlanarColor`
> 是**全仓唯一的 Cognex 使用点** → 删这 5 个方法

**「5 个全部零调用」是错的。** 实际：

| 方法 | 真实调用情况 |
|---|---|
| `ToCogImage8Grey` | 全仓 0 调用 |
| `ToCogImage24PlanarColor` | 全仓 0 调用 |
| `ToHImageByte` | **被 `BitmapEx.ToHImage` 调用（`BitmapEx.cs:28`）** |
| `ToHImageRGB` | **被 `BitmapEx.ToHImage` 调用（`BitmapEx.cs:32`）** |
| `ToHImageUint2` | 全仓 0 调用 |

⇒ 照 v1 删 5 个方法，`BitmapEx.cs` 立刻报 2 个 `CS1061`。

**根因（这条教训要长期记住）**：`IntPtrEx` 是**扩展方法类**，`ToCogImage8Grey(...)` 按
`.ToCogImage8Grey(` 的形式被调用，**光 grep 类名会漏掉全部调用点**。
⇒ **做「谁在用」统计必须 grep 方法名。**

### 3.2 一条更深的调用链（供拆程序集时参考）

```
BitmapEx.ToHImage(Bitmap)          ← 全仓 0 调用
   └─ IntPtrEx.ToHImageByte        ← 只因它而存在
   └─ IntPtrEx.ToHImageRGB         ← 只因它而存在
IntPtrEx.ToHImageUint2             ← 全仓 0 调用
```

全仓 `.ToHImage(` 共 4 处外部调用，**全部解析到 `RawEx.ToHImage(byte[], int, int)` 这个另一个重载**：

```
Packing Machine/.../Infra/Tool/VisionTool.cs:85    RawEx.ToHImage(...)          ← 显式限定
Packing Machine/.../Infra/Tool/VisionTool.cs:137   imagedata.RawData.ToHImage() ← byte[] 重载
WPFControl/.../CamManagerService.cs:78             image.RawData.ToHImage()     ← byte[] 重载
WPFControl/.../CamManagerService.cs:121            image.RawData.ToHImage()     ← byte[] 重载
```

`BitmapEx.ToImageSource` **在用**（`WPFControl/.../PrintService.cs:63,113`）。

> ⚠️ 按新原则，上面这些**一个都不删**。这条链的价值在于：
> 拆 `Extension.Vision.Halcon` 时，`BitmapEx` / `IntPtrEx` 都属于「有 Halcon 依赖」的一侧，一起搬走。

### 3.3 vendor 使用点全清单（拆卫星程序集的下游 `ProjectReference` 依据）

| 符号 | 下游文件 |
|---|---|
| `RawEx.ToHImage` | `Packing Machine/.../Infra/Tool/VisionTool.cs:85,137`、`WPFControl/.../CamManagerService.cs:78,121` |
| `HImageEx.SaveImage` / `OpenImage` / `ToIntPtr` | `Vision/Ctr_WPF_F48/.../HSmartWindowView.xaml.cs:105`、`Vision/Vision/Control/HSmartWindowView.xaml.cs:106`、`WPFControl/.../DisplayQueryView.xaml.cs:80`、`Vision/DPLibrary_Halcon_F48/.../InviHelper.cs:108`、`Packing Machine/.../VisionTool.cs:137,143` |
| `BitmapEx.ToImageSource` | `WPFControl/.../CustomControl/Services/PrintService.cs:63,113` |
| `DynamicDllLoaderEx` + `SafeMemoryHandle` | `Vision/DPLibrary_Halcon_F48/.../InviHelper.cs`（**无 vendor 依赖 ⇒ 留核心**） |

**Cognex 的使用点全在 `Extension` 内部**，下游没有直接使用 ⇒ 阶段 1 理论上无需给任何下游补引用。

### 3.4 基线构建状态

`Frame.sln` 全量构建 = **1 错误 / 387 警告**。那 1 个错误是 **HEAD 既有失败**，与本重构无关：

```
FlowNodes/FlowNodes/Vision/SingleTool/Definition/HVisionToolNodeBase.cs(141,25):
error CS1061: "IFlowChartService" 未包含 "IsBatchOperation" 的定义
```

三重核实：

1. `grep -rn "IsBatchOperation" .` → 全仓**只有这 1 处使用、0 处定义**。
2. `git log -S"IsBatchOperation"` → 该符号在提交 `a05287c（Codex架构优化）` 里被从接口移除，
   调用点没跟着改。
3. `git diff HEAD -- HVisionToolNodeBase.cs` 与 `IFlowChartService.cs` 均**为空**
   ⇒ **HEAD 上就是坏的**。

**这是你那次 FlowEditor 大重构的收尾遗漏，需要你处理**（补回接口成员，或改掉调用点）。
否则 DoD 的「错误数不高于基线」只能按 1 计。

---

## 4. 已确认的两处隐患（**按新原则，未修，仅备案**）

1. `Core/IntPtrEx/DynamicDllLoaderEx.cs`
   - 构造函数与 `GetFunction` 各有一处**空 `catch`**，会把 `LoadLibrary` / `GetProcAddress` 失败
     **咽掉**，产出一个 `_moduleHandle == 0` 的「假可用」实例；`GetFunction` 还会返回
     `Marshal.GetDelegateForFunctionPointer<T>(IntPtr.Zero)` —— 调用时表现为**访问冲突**而非可读异常。
     （唯一调用方 `Vision/DPLibrary_Halcon_F48/.../InviHelper.cs:64` 自己也 `try/catch(Exception){}`。）
   - `Dispose` 第 78 行 `freeThread.Abort()` 在 .NET 5+ **已废弃并抛 `PlatformNotSupportedException`**，
     只在 `FreeLibrary` 超过 2 秒时才会走到，且是在**终结器线程**上抛。
2. `Core/FileEx/FileEx.cs` 的备份 `catch { }`（失败不阻断写入，但完全无声）。

**要不要修，等你发话，不打包进重构。**

---

## 5. 仓库协作注意

- 工作区常有**上百个**用户未提交文件（本次 107 个：FlowEditor 60+、DataManager 等）。
- **任何提交都必须只指定明确路径**（如 `git commit -- Extension/...`），**绝不能 `git add -A`**。
