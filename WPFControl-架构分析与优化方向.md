# WPFControl 架构分析与优化方向

> 分析日期：2026-09-22（v3：2026-09-23 补「六、落地记录」，并回填各项状态）
> 分析对象：`WPFControl/WPFControl`（主模块）+ `WPFControl/WPFControl.Tests`
> 方法：静态扫描（全仓检索 + 脚本统计）+ 关键文件精读。**所有数字均可复现**，命令见附录 A。
> 口径声明：文中凡标 **[实测]** 的为脚本/检索直接得出；标 **[推断]** 的为基于事实的推理，需运行时确认。

---

## 一、结论摘要

**功能组织是健康的，问题集中在「装配层」和「依赖声明」两处。**

按目录按业务域（Alarm / Data / Device / Flow / Log / Printer / Shell / User / Vision）
严格四件套（Models / Services / ViewModels / Views）切分，Models 层基本守住纪律 —— 这部分做得好。

但在这个健康骨架之上，有三条主线问题：

| # | 问题 | 一句话 | 严重度 | 状态 |
|---|---|---|---|---|
| 1 | **依赖声明不完整** | csproj 声明了 4 个工程引用 + 7 个包引用，但源码实际用到 **22** 个根命名空间，其中 **12** 个完全没声明，全靠传递引用撑着 | 🔴 高 | ✅ **已修复** |
| 2 | **装配层是「全单例 + 全具体类型 + 服务定位器」** | 31 个服务全部注册为单例、其中 20 个持有 `ObservableCollection`；**48** 个文件注入 `IContainerProvider`；全模块**只有 1 个 interface** | 🔴 高 | ⏳ 待办（阶段二） |
| 3 | **可验证性接近零** | 173 个源文件对应 **1 个**测试用例 | 🔴 高 | 🟡 已加 4 个架构守卫用例 |

问题 1 让「改一处、崩一片」的半径不可预测（**已消除**）；问题 2 让「状态隔离」和「可替换」都做不到，
是问题 3 的**直接原因**（没有接口就没有接缝，没有接缝就没法测）。

---

## 二、现状

### 2.1 模块定位

`WPFControl` 是一个 **Prism 模块**（`IModule` 实现），提供工业上位机的「程序控制」界面层：
菜单/状态栏/资源监视等外壳（Shell），以及报警、数据、设备、流程、日志、打印、用户、视觉
各业务域页面。它被 `Packing Machine` 作为整机宿主引用。

对外只暴露一个入口：`WPFControlModule`（`RegisterTypes` + `OnInitialized`）。
**但实际耦合远超这个入口** —— 见 2.3 与 3.1。

### 2.2 目录组织 [实测]

```
WPFControl/WPFControl/
├── WPFControlModule.cs          ← 模块入口（唯一对外契约）
├── Helper/          (2)   ColorHelper、TryCatchHelper
├── Method/          (1)
├── ValueConvert/   (11)   IValueConverter 集合
├── WindowUI/        (2)   窗口类
├── Resources/       (2 xaml)  Resources.xaml、NavigationStyle.xaml
├── CustomControl/   ← 偏「可复用控件」
│   ├── Models(4) Services(5) ViewModels(2) Views(17+17xaml)
└── ProgramControl/  ← 偏「本机业务页面」
    ├── Models(31) Services(31) ViewModels(32) Views(33+33xaml)
```

规模 [实测]：**173 个 .cs（13,084 行）+ 53 个 .xaml**（50 在 `Views/`、2 在 `Resources/`、1 `MainWindow`）；
最大单文件 `ResourceMonitorService.cs` 425 行、`MenuService.cs` 354 行。

**做得好的地方**

- 按业务域分目录，四件套齐全，命名一致（`XxxView` / `XxxViewModel` / `XxxService`）。
- **Models 层基本守住纪律** [实测]：`ProgramControl/Models` 与 `CustomControl/Models` 中
  **没有任何一处** `using ...Services` 或 `using ...ViewModels`。**但有 1 处越界**（见 3.11）：
  `ProgramControl/Models/Shell/DisplayModel.cs:4` → `using WPFControl.CustomControl.Views.Display;`
  —— Model 直接依赖了 View，比依赖 Service 更越界。
- 34 个 ViewModel **全部**有同名 `XxxView`，无孤儿 VM [实测]。反向则有 15 个 View
  是子视图/对话框（`AddUser` / `Login` / `Pagination` / `ChartsPie` …），本就不需要独立 VM。
- 视图装配走 Prism 约定，未出现 `ContainerLocator` 静态服务定位 [实测 0 处]。

### 2.3 依赖图（问题所在）—— ✅ 已修复

**csproj 的声明分两类** [实测]。

**7 个 `PackageReference`**：

```
CommunityToolkit.Mvvm 8.4.2        HarfBuzzSharp 8.3.1.1
LiveChartsCore.SkiaSharpView.WPF 2.0.0
MaterialDesignThemes 5.3.0         Prism.Core 8.1.97
Prism.Wpf 8.1.97                   System.Management 10.0.0
```

**`ProjectReference`：修复前 4 个 → 修复后 10 个**：

```
DataManager/DataManager.csproj                                  （原有）
FlowNodes/FlowNodes.csproj                                      （原有）
FlowEditor/FlowEditor.DataManagerLogging.csproj                 （原有）
DataManager/DataManager.Controls.csproj                         （原有）
Extension/Extension/Extension.csproj                            ← 2026-09-23 补
ScriptEditor/ScriptEditor/ScriptEditor.csproj                   ← 2026-09-23 补
Secs/Secs/Secs.csproj                                           ← 2026-09-23 补
Device/Device/Device.csproj                                     ← 2026-09-23 补
Vision/Ctr_WPF_F48/Ctr_WPF_F48.csproj                           ← 2026-09-23 补
Vision/DPLibrary_Halcon_F48/DPLibrary_Halcon_F48.csproj         ← 2026-09-23 补
```

**源码实际 `using` 了 22 个根命名空间** [实测]。去掉 BCL（`System` / `Microsoft`）
与自身（`WPFControl`）后剩 19 个；其中 7 个被包引用覆盖
（`CommunityToolkit` / `DataManager` / `FlowEditor` / `LiveChartsCore` /
`MaterialDesignThemes` / `Prism` / `WPFCustomControls`），
**剩下 12 个完全没有声明**（即修复前的问题）：

| # | 未声明命名空间 | 实际来源 | 类型 | 中转路径 | 状态 |
|---|---|---|---|---|---|
| 1 | `Extension` | `Extension.csproj` | 工程 | `DataManager` / `FlowNodes` | ✅ 已显式声明 |
| 2 | `ScriptEditor` | `ScriptEditor.csproj` | 工程 | `FlowNodes` | ✅ 已显式声明 |
| 3 | `Secs` | `Secs.csproj` | 工程 | `FlowNodes` | ✅ 已显式声明 |
| 4 | `Device` | `Device.csproj` | 工程 | `FlowNodes` | ✅ 已显式声明 |
| 5 | `Ctr_WPF_F48` | `Vision/Ctr_WPF_F48` | 工程 | `FlowNodes` | ✅ 已显式声明 |
| 6 | `DPLibrary_Halcon_F48` | `Vision/DPLibrary_Halcon_F48` | 工程 | `FlowNodes` | ✅ 已显式声明 |
| 7 | `HalconDotNet` | `MVTec.HalconDotNet 23110.0.0` | 包 | `Extension` | 保持不声明（见下） |
| 8 | `Mapster` | `Mapster 7.3.0` | 包 | `Extension` | 保持不声明（见下） |
| 9 | `FastDeepCloner` | `FastDeepCloner 1.3.6` | 包 | `Extension` | 保持不声明（见下） |
| 10 | `AvalonDock` | `Dirkster.AvalonDock 5.0.0` | 包 | `FlowEditor` | 保持不声明（见下） |
| 11 | `Dm` | `SqlSugarCore.Dm 8.8.1` | 包 | `DataManager`（经 `SqlSugarCore`） | 保持不声明（见下） |
| 12 | `SkiaSharp` | `SkiaSharp`（LiveChartsCore 的传递包） | 包 | `LiveChartsCore` | 保持不声明（见下） |

> **为什么 #7–#12 这 6 个包不补声明**（2026-09-23 用户拍板「只补 6 个工程引用」）：
> 它们都是**上游工程的传递依赖**，补声明意味着把版本号复制到 `WPFControl.csproj`。
> 其中 `MVTec.HalconDotNet` 更直接违反项目硬规则
> ——「Halcon 只在 `Extension.csproj` 一处锁 `23110.0.0`，其余靠 `ProjectReference` 传递」。
> ⇒ 这 6 条边保留为「经传递」，由 6.2 的守卫测试保证它们的**上游**不会被悄悄摘掉。

> 口径说明：上面「22 个」是按 `using` 的**第一段**去重得到的（原始命中 24 个，
> 其中 `RelayCommand` / `Timer` 是 `using X = Y;` 别名，已剔除）。
> 另有 3 处别名 using 未计入：`Timer`、`RelayCommand`×2。

**两条关键风险（修复前的成因，留档）**：

1. **`FlowNodes` 是一条「空引用」** [实测]：它被 WPFControl **直接引用**，
   但源码里 **0 处**出现 `FlowNodes` 命名空间 —— 它存在的唯一作用就是当
   上表 #1–#6 的**中转站**。也就是说，这条 `ProjectReference` 的真实语义
   是「我要 ScriptEditor/Secs/Device/两个 Vision 库」。
   （2026-09-23 后语义已显式化；`FlowNodes` 直引是否还需要，待单独确认 —— 见 6.3。）
2. **`Extension` 是第二个隐形枢纽**：Halcon / Mapster / FastDeepCloner 这 3 个包
   在 WPFControl 里**根本没法从自己 csproj 看出来**，只能经 `Extension` 拿到。
   （这也解释了为什么全仓只有 `Extension.csproj` 一处锁 Halcon 版本。）
3. **一旦 `FlowNodes.csproj` 或 `Extension.csproj` 删掉任一引用，
   `WPFControl` 会直接编译失败**，而报错点看起来跟改动者毫无关系。
   —— #1–#6 已在 2026-09-23 显式声明，这条风险对**工程**已消除；
   对**包**（#7–#12）仍然存在，只是被守卫测试钉住了上游。

### 2.4 装配机制

模块用两个**约定式批量注册**扩展（定义在 `Extension/Extension/Integration/Prism/`）：

```csharp
// WPFControlModule.RegisterTypes
containerRegistry.RegisterNavigationViewsFromNamespace(asm, "WPFControl.CustomControl.Views");
containerRegistry.RegisterServicesFromNamespace(asm, "WPFControl.ProgramControl.Services");
containerRegistry.RegisterSingleton<IFlowLogSink>(p => new DataManagerFlowLogSink(p.Resolve<LogManager>()));
containerRegistry.RegisterNavigationViewsFromNamespace(asm, "WPFControl.ProgramControl.Views", t => t != typeof(UserInfoView));
containerRegistry.RegisterSingleton<UserInfoView>();
containerRegistry.RegisterDialog<UserInfoView>();
```

**`RegisterServicesFromNamespace` 的实现语义**（`RegisterServicesEx.cs:44-64`）：
把命名空间前缀下**所有非抽象类**，一律 `RegisterSingleton(type)` —— 即
**按具体类型注册为单例，无接口、无过滤、无生命周期选择**。

由此推出四条**实测事实**：

1. **31 个 `ProgramControl.Services` 全部是单例**。
2. **`CustomControl.Services` 的 5 个类（`LightControlService`/`PrintService`/
   `UserManagerService`/`ChartsLineService`/`ChartsPieService`）从未被注册** [实测：
   全仓检索这 5 个名字的 `Register*` 调用，**0 命中**]。它们靠容器的
   「未注册具体类型自动解析」被 `container.Resolve<T>()` 直接取到。
   **[推断]** DryIoc 对未注册具体类型的自动解析是**瞬态**（transient），
   与那 31 个显式单例**生命周期不一致**。→ 建议用一行 `ReferenceEquals` 探针确认（附录 A8）。
3. **34 个 ViewModel 也全部未注册**，同样走自动解析。
4. 这个扩展方法**全仓只有 WPFControl 用了这一次** [实测] —— 它是为本模块定制的，
   不是通用约定。

**导航视图是单例**（`RegisterNavigationViewEx.cs`）：视图同时注册为
「自身类型单例」+「`(object, 导航名)` 单例」。注释说明了动机 —— Prism 的
`RegisterForNavigation` 在 DryIoc 下是瞬时注册，会让 App 层的预热实例被丢弃。
⇒ **50 个视图全部常驻内存、跨导航保状态**。

### 2.5 页面装配链路

```
宿主 App.PrewarmNavigationViews()
  └─ Container.Resolve(typeof(object), "<视图名>")   ← 硬编码字符串清单（23 个名字）
       └─ 视图单例（首次解析时构造）
            └─ Prism AutowireViewModel → ViewModelLocationProvider 按约定
               解析 "<View 所在命名空间>.ViewModels.<ViewName>ViewModel"
                 └─ VM 构造 → container.Resolve<XxxService>() → 单例服务
```

两个要点：

- **XAML 里写不写 `AutoWireViewModel="True"` 都能工作** [实测：53 个 xaml 里只有 11 个写了]。
  因为 Prism 在**区域导航**构造视图时会调 `MvvmHelpers.AutowireViewModel`：
  若 `DataContext == null` 且附加属性未设置，就把它设为 `true` 从而触发约定装配。
  ⇒ **42 个视图的 VM 绑定是"隐式"的**，从 XAML 上看不出来。
- 宿主预热清单是**字符串数组**，且**必须与视图名逐字一致**。

### 2.6 跨切面机制

| 关注点 | 实现 | 状态 |
|---|---|---|
| 主题 | 各壳 `App.xaml` 的 `BundledTheme` 为唯一真相源；`ThemeService` 全仓唯一 | 健康 |
| 资源 | 模块合并 `Resources.xaml` + `NavigationStyle.xaml`；**32/53 个 xaml 自行合并**页面级资源 | 分散但符合「样式跟页面走」 |
| 日志 | 三类同构（`FlowLog`/`RunLog`/`SEMILog`），一律 `logManager["<Name>"]` 取用 | 健康（09-17 已去接口化） |
| 异常 | `TryCatchHelper` 统一包裹 | ⚠️ 见 3.5 |
| 预热 | 宿主 `PrewarmNavigationViews` + `ForceFullRender` | 🟡 innerException 已修（3.8） |

---

## 三、问题清单

### 3.1 【P0】依赖声明不完整，靠传递引用撑着 —— ✅ 已修复（2026-09-23）

- **现象**：2.3 的 12 个未声明命名空间。
- **证据**：`WPFControl.csproj` 只有 4 条 `ProjectReference` + 7 条 `PackageReference`；
  源码 `using` 覆盖 22 个根命名空间；`FlowNodes.csproj` 同时引用
  `Communication`/`CommunicationNew`/`DataManager`/`Device`/`Extension`/`FlowEditor`/
  `ScriptEditor`/`Secs`/`Ctr_WPF_F48`/`DPLibrary_Halcon_F48`/`WPFCustomControls`
  —— 其中 6 个是 WPFControl 拿不到的；而 WPFControl 自己**一处也没用** `FlowNodes` 的命名空间。
- **影响**：编译依赖不可预测；改动者无法从 csproj 看出真实依赖；重构 `FlowNodes` 会误伤 WPFControl。
- **同类**：`Packing Machine.csproj` 里 `WPFControl.csproj` 被引用了**两次**（`:33` 与 `:96`）[实测]。
- **修复**：补 6 个 `ProjectReference`（见 6.1）。包引用按用户拍板不动。

### 3.2 【P0】全量单例 + UI 状态住在服务里

- **现象**：31 个 `ProgramControl.Services` 显式单例，其中 **20 个持有 `ObservableCollection`** [实测]。
  再加上 `CustomControl.Services` 的 5 个里有 4 个也持有，**全模块共 24 个服务把页面集合当字段存着**。
- **影响**：
  - 页面数据集合是**进程级全局状态**。导航离开再回来，看到的是上次的残留（有时是需求，
    有时是 bug）；任何"清空"都必须手写。
  - 无法为两个页面同时持有同一服务的不同数据集。
  - 测试之间无法隔离（单例跨测试存活）。
- **注**：`CapacityStatisticsService` / `AlarmInfoQueryService` 等以「查询」语义命名的服务
  持有结果集合，说明这里混了**页面状态**与**领域服务**两种职责。
- **状态**：⏳ 未动（阶段二/三，结构性改造）。起手前必须先加标记，见 6.3。

### 3.3 【P1】服务定位器：48 个文件注入 `IContainerProvider`

- **现象**：**48 个 .cs 文件**（占 173 个源文件的 28%）在构造函数里拿 `IContainerProvider`
  再 `container.Resolve<T>()`；全模块 **54 处 `.Resolve<`** [实测]。分布 [实测]：

  | 位置 | 注入文件数 | 占该层比例 |
  |---|---|---|
  | `ProgramControl/Services` | 25 | 25 / 31 |
  | `ProgramControl/ViewModels` | 15 | 15 / 34 |
  | `CustomControl/*` | 4 | — |
  | 其它（Helper / Method / ValueConvert …） | 4 | — |
  | **合计** | **48** | — |

- **影响**：
  - 真实依赖**不出现在构造函数签名里** —— 读代码看不出一个服务依赖了谁，
    必须逐行看方法体。
  - 无法用构造函数注入替身；`Resolve` 在构造期执行，出错时机提前且难定位。
  - 这是 3.4 的直接放大器。

### 3.4 【P1】全模块只有 1 个 interface —— 没有接缝

- **现象**：173 个源文件里，**只定义了 1 个 interface**
  （`ISQLDataBase`，`ProgramControl/Models/Data/DataQueryModel.cs:13`），
  **0 个 abstract class** [实测]。
- **影响**：所有依赖都是具体类型 ⇒ 无法替换、无法 mock ⇒ 3.6 的测试空白是必然结果，
  而不是"没来得及写"。

### 3.5 【P1】`TryCatchHelper` 的静态可变状态（隐患，未修）

- **现象**：`Helper/TryCatchHelper.cs:16` 是 `private static LogInfoService _logInfoService;`，
  **只在构造函数里赋值**。而模块在 `OnInitialized` 里 `new TryCatchHelper(containerProvider)`
  后**把结果丢弃**（`WPFControlModule.cs:49`，变量未使用）。
- **影响**：
  - 任何**没有构造过** `TryCatchHelper` 的宿主/测试环境，`_logInfoService` 是 `null`
    ⇒ `TryExecute` 的 catch **静默吞掉所有错误上报**。
  - 这正是 09-17 那次「递归回归测试是假阴性」的根因。
- **背景**：09-17 已修好日志上报的**递归环**（新增 `TryExecuteWithoutReport` +
  `AddRunInfo` 的 `[ThreadStatic]` 重入闸门），但**这个静态状态本身没动**（要改 7 个静态方法
  + 全仓调用方，风险面大）。

### 3.6 【P1】测试覆盖 ≈ 0 —— 🟡 已加 4 个架构守卫用例（2026-09-23）

- **现象**：`WPFControl.Tests` **只有 1 个测试文件、1 个用例**（`LogInfoServiceRecursionTests`）
  对应 173 个源文件 [实测]。
- **影响**：3.2/3.3/3.4 使得补测成本极高；而模块又被整机宿主直接依赖，
  回归只能靠手工点界面。
- **进展**：新增 `ArchitectureDependencyTests`（4 个用例，5/5 绿 + 变异验证），
  钉住分层方向与 csproj 直接引用（见 6.2）。业务行为测试仍是空白。

### 3.7 【P2】`ProgramControl` ↔ `CustomControl` 双向依赖

- **现象**：`ProgramControl → CustomControl` 29 个文件；**`CustomControl → ProgramControl` 2 个文件** [实测]：
  - `CustomControl/Models/PrintModel.cs` → `using WPFControl.ProgramControl.Models`
  - `CustomControl/Views/User/ModifyUserView.xaml.cs` → `using WPFControl.ProgramControl.Models`
- **另有 3 处分层穿透** [实测]（都指向 `CustomControl.Views.Display`）：
  - `ProgramControl/ViewModels/Device/CamManagerViewModel.cs` → `using ...CustomControl.Views.Display`
  - `ProgramControl/Services/Shell/DisplayService.cs` → `using ...CustomControl.Views.Display`
  - `ProgramControl/Models/Shell/DisplayModel.cs:4` → `using ...CustomControl.Views.Display`（**Model → View**，见 3.11）
- **影响**：`CustomControl` 名义上是「可复用层」，实际依赖了上层业务模型 ⇒
  **无法独立抽走或复用**。同程序集内不会编译报错，所以很容易继续恶化。
- **状态**：🟡 已被 6.2 的守卫测试钉住（不会新增，也不会被悄悄抹平）。

### 3.8 【P2】宿主与模块的耦合点偏多

- **硬编码视图名**：宿主 `App.xaml.cs` 里 `NavigationViewNames` 是 23 个字符串，
  与视图名逐字对应；新增页面若忘了加，就不会被预热（无编译期保护）。
- **宿主直接引用模块内部类型**：`using WPFControl.ProgramControl.ViewModels;` /
  `...Views;` / `...Services;`，并注册
  `RegisterNavigationView<DataQueryView, DataQueryViewModel<CSQLData, CSQLDataDTO>>()`、
  `RegisterSingleton<DataQueryService<CSQLData, CSQLDataDTO>>()`。
  ⇒ **宿主必须知道模块的泛型参数**才能闭合这些类型。
- **异常信息被丢弃** [实测] —— ✅ **已修复（2026-09-23）**：
  `catch (Exception ex) { throw new Exception("View预热失败"); }`
  改为 `throw new Exception($"View预热失败：视图「{name}」", ex);`
  —— 补上 `innerException`，并带上失败的视图名，预热失败时可直接定位。

### 3.9 【P3】文档与代码漂移

- `App.xaml.cs` 的 `ForceFullRender` 注释写「用 `RenderTargetBitmap` 执行一次真实离屏渲染」，
  但**方法体里没有 `RenderTargetBitmap`**，只有 `Measure`/`Arrange`/`UpdateLayout`
  ⇒ 注释承诺的「支付绘制开销」并未发生。
- **状态**：未动（P3，未授权）。

### 3.10 【P3】死配置与冗余

- `WPFControl.csproj` 里 **12 行** `<Compile Remove>` / `<EmbeddedResource Remove>` /
  `<None Remove>` / `<Page Remove>` 指向 `ProgramControl/{Models,Services,ViewModels}/LogR/**`，
  而**这 3 个目录已不存在** [实测] —— ✅ **已清理（2026-09-23）**，原地留 3 行注释说明去向。
- `AllowUnsafeBlocks=true`，但全模块 **0 处** `unsafe` / `fixed` [实测] —— 保留未动（「不删」规则）。
- 完全空的 `catch { }` **4 处，全部集中在同一个文件** [实测]：
  `ProgramControl/Services/Shell/ResourceMonitorService.cs` 的 `:173`、`:193`、`:210`、`:226`
  —— 前 3 处在 `ReadSystemCpu()`（`:166` 起）里，第 4 处在 `RefreshResources()`（`:218` 起）里。
  这几处正好包着 `CalCPUSystem()` / `CalMemorySystem()` / `CalDisk("D")` 的调用，
  所以采集失败时 **CPU / 内存 / 磁盘读数会静默变成默认值**，界面上看不出任何异常。
  （同文件另有 8 处 `catch` 带注释或兜底赋值，不算空 catch。）

### 3.11 【P2】Model 层有 1 处直接依赖 View（本轮新发现）

- **现象**：全模块 `Models` 目录**只有 1 处**反向依赖 [实测]：
  `ProgramControl/Models/Shell/DisplayModel.cs:4` → `using WPFControl.CustomControl.Views.Display;`
- **为什么比 3.7 更严重**：3.7 的双向依赖是「业务层之间」互相引用，还在同一抽象层；
  这一条是 **Model（最底层）直接引用 View（最上层）** —— 依赖方向整个倒过来了。
  `DisplayModel` 因此**不可能被单独序列化、单独测试、单独搬走**。
- **性质**：同程序集内不会编译报错，也没有任何守卫测试拦它，所以属于「会一直烂下去」的那类。
- **状态**：🟡 已被 6.2 的守卫测试登记为已知债务。

---

## 四、优化方向

原则：**只增不删**（不删任何现有代码/文件）、**每一步都能单独回滚**、
**先用测试锁住行为再动结构**。

### 阶段一：先能看见（低风险）—— ✅ 已于 2026-09-23 完成

| 动作 | 目的 | 验收 | 状态 |
|---|---|---|---|
| **1.1 补全 `WPFControl.csproj` 的直接引用** | 让依赖图真实可读 | 补 6 个工程引用；`dotnet build Frame.sln -m` 0 错误 | ✅ |
| **1.2 加依赖方向守卫测试** | 防止 3.7 / 3.11 继续恶化 | 4 个用例，含 `Models → Views` 断言；5/5 绿 + 变异验证 | ✅ |
| **1.3 清死配置** | 降噪 | 删 `LogR` 的 12 行 Remove；`AllowUnsafeBlocks` 按「不删」保留 | ✅ 部分 |
| **1.4 修异常丢失** | 预热失败可定位 | 补 `innerException` + 视图名 | ✅ |

### 阶段二：给装配层加接缝（结构性，1–2 周）—— ⏳ 未开始

| 动作 | 目的 | 备注 |
|---|---|---|
| **2.1 把 `RegisterServicesFromNamespace` 从"全部单例"改成"按标记选择"** | 让生命周期显式 | 例如加 `[SingletonService]` 特性，无标记的默认不注册；或改为显式注册。**保留旧方法不删**，新模块用新的 |
| **2.2 为"查询类"服务引入接口** | 让页面状态可隔离 | 先给 3–5 个高频服务（`AlarmInfoQueryService`/`LogQueryService`/`CapacityStatisticsService`）加 `IXxxQueryService`，注册时绑接口 → 具体类型。**只增接口，不改现有类名** |
| **2.3 逐步替换服务定位器** | 依赖变显式、可测 | 对 3.3 的 48 个文件，**每次只改一个**：把 `Resolve<T>()` 的字段提升为构造函数参数。优先改 ViewModel（15 个），风险低于服务 |
| **2.4 明确 5 个未注册服务的生命周期** | 消除不一致 | 先按附录 A8 的探针确认是瞬态还是单例，再显式注册成想要的那个 |

⚠️ **2.1/2.3 必须逐文件小步做**：`RegisterServicesFromNamespace` 是"全都要"语义，
一旦改成选择式，漏标的服务会在**运行时**才报解析失败（不是编译期）。
建议先给全部 31 个服务补标记，再切语义。

### 阶段三：把状态从服务里搬出来（长期，按页面推进）—— ⏳ 未开始

| 动作 | 目的 |
|---|---|
| **3.1 页面状态下沉到 ViewModel** | 服务只保留"取数"能力，返回新集合；集合的所有权归 VM |
| **3.2 单例服务改为无状态** | 消除跨页面串扰；让服务可并发使用 |
| **3.3 每改一个页面，补一组特征测试** | 逐步把 3.6 的空白填上 |

建议**从 `AlarmInfoQueryService` 或 `LogQueryService` 起手**：它们逻辑单一、被引用少、
结果集合语义清晰，适合当第一个样板。

### 不建议做的事（避免踩已知的坑）

- ❌ **不要为日志再抽接口**：09-17 已定案 `FlowLog`/`RunLog`/`SEMILog` 同构，
  一律 `logManager["<Name>"]`，宿主只装配 adapter（有架构测试钉住）。
- ❌ **不要把页面样式搬到宿主合并**：样式跟页面走，页面换宿主不该要宿主配合。
- ❌ **不要给导航视图改成瞬时注册**：会破坏 App 层预热（`RegisterNavigationViewEx` 注释已说明）。
- ❌ **不要用 `ContainerLocator` 静态定位**替换构造函数注入 —— 方向相反。
- ❌ **不要把 Halcon 包引用复制到 `WPFControl.csproj`** —— 违反「只在 `Extension.csproj` 一处锁版本」。

---

## 五、附录

### A. 度量方法与可复现命令

```bash
cd "C:/Users/91632/Desktop/repos/控件/C#框架"     # 以下均以仓库根为工作目录

# A1 规模
find WPFControl/WPFControl -name '*.cs'   -not -path '*/obj/*' -not -path '*/bin/*' | wc -l   # 173
find WPFControl/WPFControl -name '*.xaml' -not -path '*/obj/*' -not -path '*/bin/*' | wc -l   # 53
# ⛔ 行数：`wc -l` 数的是 `\n` 字节数，而本模块有 **76 个文件末行没有换行符**，
#    那 76 行**任何** `wc -l` 口径都不会计入（`cat ... | wc -l` 与 xargs 实测都是 13,084）。
#    初稿写的 13,257 **无法复现**，别沿用。
find WPFControl/WPFControl -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -print0 \
  | xargs -0 wc -l | tail -1                                                                  # 13084

# A2 依赖声明 vs 实际使用
grep -nE '<ProjectReference|<PackageReference' WPFControl/WPFControl/WPFControl.csproj        # 10 + 7
# ⛔ 别用 `^using ([A-Za-z_][A-Za-z0-9_]*)\.` —— 会把 `using X = Y;` 的别名算成命名空间。
#    正确做法是先把别名剔掉：
grep -rhoE '^using (static )?[A-Za-z_][A-Za-z0-9_]*' --include=*.cs WPFControl/WPFControl \
  | sed -E 's/^using (static )?//' | sort -u | wc -l                                          # 24（含 2 个别名）
grep -rn --include=*.cs -E '^using [A-Za-z_][A-Za-z0-9_]*\s*=' WPFControl/WPFControl          # 3 处别名
# ⇒ 真实根命名空间 = 24 − 2 = 22

# A3 服务定位器
grep -ro '\.Resolve<' --include=*.cs WPFControl/WPFControl | wc -l                            # 54 处
grep -rl 'IContainerProvider' --include=*.cs WPFControl/WPFControl | wc -l                    # 48 个文件
grep -rl 'IContainerProvider' --include=*.cs WPFControl/WPFControl/ProgramControl/Services | wc -l  # 25
grep -rl 'IContainerProvider' --include=*.cs WPFControl/WPFControl/ProgramControl/ViewModels | wc -l # 15

# A4 抽象数量
grep -rn '^\s*\(public\|internal\)\? *interface ' --include=*.cs WPFControl/WPFControl       # 1
grep -rn '\babstract class\b' --include=*.cs WPFControl/WPFControl                           # 0

# A5 单例 + UI 状态
grep -rl 'ObservableCollection' --include=*.cs WPFControl/WPFControl/ProgramControl/Services | wc -l  # 20
grep -rl 'ObservableCollection' --include=*.cs WPFControl/WPFControl/CustomControl/Services  | wc -l  # 4

# A6 分层纪律（Models 反向依赖）
grep -rn --include=*.cs 'using WPFControl.*\.\(Services\|ViewModels\|Views\)' \
  WPFControl/WPFControl/ProgramControl/Models WPFControl/WPFControl/CustomControl/Models      # 1（DisplayModel.cs:4）

# A7 空 catch
grep -rn --include=*.cs -E 'catch[^A-Za-z]*\{\s*\}' WPFControl/WPFControl                     # 4（同一文件）

# A8 未注册服务的生命周期探针（[推断] → 实测）
#   在 WPFControl.Tests 里加一个临时用例，断言下面两行，跑完删掉：
#   var a = container.Resolve<LightControlService>();
#   var b = container.Resolve<LightControlService>();
#   Assert.True(ReferenceEquals(a, b), "瞬态：与其余 31 个单例不一致");
```

### B. 本次未做的事（边界声明）

- **分析阶段没有修改任何产品代码**（2026-09-22）。
- **2026-09-23 的修复仅限**：`WPFControl.csproj`（6 条引用 + 12 行死配置）、
  `Packing Machine/App.xaml.cs`（innerException）、
  `WPFControl.Tests/ArchitectureDependencyTests.cs`（新增）。
  **没有改任何 sln、没有改任何其他产品代码。**
- `AllowUnsafeBlocks`（3.10 第二项）按仓库「不删」规则**保留原样**。
- 3.5 的 `TryCatchHelper` 静态状态**未动**（风险面大，需单独授权）。
- 3.9 的注释漂移**未动**（P3）。
- 附录 A8 的探针是需要时才加的一次性验证，不是建议长期保留的用例。
- 所有 **[推断]** 项（主要是 A8 的生命周期问题）**未经运行时确认**，不要当成既成事实引用。
- 3.11 的 Model→View 依赖是**本轮新发现**，此前未在任何文档里出现过。

### C. 修订记录

**v2（2026-09-22）**：初稿完成后，对**每一条可量化结论**都重跑了一遍命令复算，共修正 7 处：

| 项 | 初稿 | 复算后 | 原因 |
|---|---|---|---|
| 源码总行数 | 13,257 | **13,084** | 初稿数字**无法复现**（差额 173 恰等于文件数，但 `cat` 拼接与 `xargs wc -l` 两种口径实测都是 13,084） |
| 根命名空间 | 13 | **22** | 初稿漏算了一批，且把 `WPFCustomControls` 误列为未声明 |
| 未声明命名空间 | 9 | **12** | 漏了 `Dm` / `FastDeepCloner` / `HalconDotNet` / `SkiaSharp`，多算了 `WPFCustomControls` |
| 注入 `IContainerProvider` | 47 个类 | **48 个文件** | 服务 27→**25**、VM 16→**15** |
| 持有 `ObservableCollection` | 24（"31 个里"） | **20**（31 个里）+ 4（CustomControl）= 24 | 初稿把两个目录合并后写成"其中" |
| 空 `catch { }` | 1 处 | **4 处** | 初稿只列了最后一个行号 |
| 空 catch 的归属方法 | 未写 | `ReadSystemCpu()`×3 + `RefreshResources()`×1 | 初稿想当然写成 `Cal*` 组，实测不是 |

另新增 1 条问题（3.11）与 1 条发现（`FlowNodes` 是零使用的「空引用」）。

**v3（2026-09-23）**：补「六、落地记录」，并把 3.1 / 3.6 / 3.8 / 3.10 的状态回填。
数字部分**未再变动**。

---

## 六、落地记录（2026-09-23）

用户指令「先修改 P0」，范围经确认为「**只补 6 个工程引用**」+ 顺手三项。**全部完成并验证**。

| 对应问题 | 动作 | 文件 | 验证 |
|---|---|---|---|
| 3.1 | 补 6 个 `ProjectReference` | `WPFControl/WPFControl/WPFControl.csproj` | `Frame.sln` **0 错误** |
| 3.10 | 删 12 行 LogR 死配置 | 同上 | 同上 |
| 3.8 | 修 `innerException`（并带上视图名） | `Packing Machine/Packing Machine/App.xaml.cs` | `Frame.sln` **0 错误** |
| 1.2 / 3.6 | 新增 4 个架构守卫用例 | `WPFControl/WPFControl.Tests/ArchitectureDependencyTests.cs` | **5/5 通过** + 变异验证 |

### 6.1 补的 6 个工程引用

> ⚠️ **后续更正（见 6.6）**：这轮**漏了 2 个**（`FlowEditor` / `WPFCustomControls`），
> 已于同日补上 ⇒ 该工程 `ProjectReference` 实为 **12 条**（4 原有 + 6 本轮 + 2 补漏）。

```
..\..\Extension\Extension\Extension.csproj
..\..\ScriptEditor\ScriptEditor\ScriptEditor.csproj
..\..\Secs\Secs\Secs.csproj
..\..\Device\Device\Device.csproj
..\..\Vision\Ctr_WPF_F48\Ctr_WPF_F48.csproj
..\..\Vision\DPLibrary_Halcon_F48\DPLibrary_Halcon_F48.csproj
```

**为什么安全**：

- 这 6 个工程本来就在传递闭包里（经 `FlowNodes` / `Extension` / `DataManager`）
  ⇒ **构建闭包不变**，不会多编任何工程。
- 已核实**它们及其依赖链都不引用 `WPFControl`** ⇒ 不会引入循环引用。
  （全仓只有 `Packing Machine` 与 `WPFControl.Tests` 引用 `WPFControl.csproj`。）
- ⛔ **Halcon 包没有重复声明** —— 仍只在 `Extension.csproj` 一处锁 `23110.0.0`，符合项目约定。

### 6.2 守卫测试（`ArchitectureDependencyTests`，4 个用例）

> ⚠️ **后续更新**：用例数 4 → **6**（+`HostProject_DeclaresItsDirectlyUsedProjects`，见 6.4；
> 下表第 3 行已改名 `ProjectFile_DeclaresTheDirectlyUsedProjects` 且期望 6 → 8 条，见 6.6）。

| 用例 | 钉住什么 | 当前登记（已知债务） |
|---|---|---|
| `Models_DoNotDependOnUpperLayers` | `Models` 不得依赖 `Services`/`ViewModels`/`Views` | 1 条：`ProgramControl/Models/Shell/DisplayModel.cs :: WPFControl.CustomControl.Views` |
| `CustomControl_DoNotDependOnProgramControl` | `CustomControl` 不得反向依赖 `ProgramControl` | 2 条：`PrintModel.cs`、`ModifyUserView.xaml.cs`（均 `:: ...ProgramControl.Models`） |
| `ProjectFile_DeclaresTheSixDirectlyUsedProjects`<br>（现名 `ProjectFile_DeclaresTheDirectlyUsedProjects`） | 直接引用不得被回退；LogR 死配置不得回潮 | — |
| `DirectDependencies_ExistAndDoNotReferenceWpfControl` | 引用的工程必须存在，且不得反引 `WPFControl`（防循环） | — |

两点设计说明：

- **登记粒度是「文件 :: 依赖」对，不是文件** —— 第一版只记文件，
  变异验证时发现「给一个已在清单里的文件再叠一条越界依赖」不会让测试变红，遂改为对。
- **正则同时匹配 `using` 与完全限定名** —— 只扫 `using` 会被完全限定名绕过
  （这是 Extension 分级那次踩过的坑）。

**变异验证（已做）**：注入两处变异 → 两个用例分别变红；还原后 5/5 绿。

- 变异 A：`DisplayModel.cs` 加 `using WPFControl.ProgramControl.Services;`
  → `Models_DoNotDependOnUpperLayers` 变红（Actual 多出 1 条）
- 变异 B：csproj 摘掉 `Secs` 引用 → `ProjectFile_DeclaresTheSixDirectlyUsedProjects` 变红

### 6.3 仍未做（等批准）

- **P0#2 全量单例 / UI 状态住服务里**（3.2）：属阶段二/三，结构性改造。
  起手前**必须先给 31 个服务加标记**（如 `[SingletonService]`）再切「选择式注册」语义，
  否则漏标的服务会在**运行时**才报解析失败。
- **`FlowNodes` 直引是否还需要**：WPFControl **一处也没用它**（`.cs` 0 命中、`.xaml` 0 命中，
  `FlowNodes` 这个词只出现在 csproj 里），6 个工程又已显式声明 ⇒ 对 WPFControl 而言它已是冗余。
  ⛔ **但不能就此摘掉**：它还是 `Packing Machine` 的桥 —— 见 6.4，摘它必须**先**把
  `Packing Machine` 的直接引用补齐。按「不删」规则本轮未动，**需单独确认**。
- `AllowUnsafeBlocks`（3.10 第二项）：保留。
- 3.5 `TryCatchHelper` 静态状态：风险面大（要动 7 个静态方法 + 全仓调用方）。
- 3.9 `ForceFullRender` 注释与实现不符：P3，未动。
- `Packing Machine.csproj` 里 `WPFControl.csproj` 的**重复引用**（`:33` 与 `:96`）：未动。

### 6.4 `Packing Machine` 是同一问题的更极端版本 —— ✅ 已修复（2026-09-23）

`Packing Machine.csproj`（**`WinExe` 应用宿主**）目前只声明了：

- `ProjectReference`：**只有 1 个不同的工程** —— `WPFControl`，且**重复声明两次**（`:33` / `:96`）；
- `PackageReference`：只有 3 条（`Prism.Core` / `Prism.DryIoc` / `Prism.Wpf`）。

但它的 **47 个 `.cs`** 实际用到 **11 个工程**的命名空间：

| 工程 | 证据（括号为 using 次数） |
|---|---|
| `Extension` | `Extension.UI.Controls`(16)、`Extension.Core.Conversion`(13)、`Extension.Integration.Cloning`(8)、`Extension.Core.Buffers`(8)、`Extension.IO.Compression`(8) … |
| `DataManager` | `DataManager.Log.Definition`(15)、`DataManager.SQL`(5) |
| `DataManager.Controls` | `DataManager.Controls.Models` |
| `Device` | `Device.Camera.*`、`Device.Light.*`、`Device.Scan.*`、`Device.RFID` |
| `DPLibrary_Halcon_F48` | `…Tools.FindLine`(4)、`…Tools`(4)、`…Tools.AI`/`.Find`/`.TemplateMatch` |
| `Communication` | `Communication`(7)、`Communication.MC`(5) |
| `Secs` | `Secs`(11)、`Secs.Order.S6` |
| `DPLibrary_Base_F48` | `DPLibrary_Base_F48.Vision` |
| `ScriptEditor` | `ScriptEditor`、`ScriptEditor.Services` |
| `FlowEditor` | `FlowEditor`、`FlowEditor.Nodes` |
| `WPFCustomControls` | `WPFCustomControls`、`WPFCustomControls.Window.Views`(3) |
| `WPFControl` | ✅ 已声明 |

包侧还有 `HalconDotNet`(4)、`CommunityToolkit.Mvvm.*`(4)、`MaterialDesignThemes.Wpf`(2)、
`SqlSugar`(2)、`Secs4Net`(2)、`Mapster`、`ImTools` —— 全部靠闭包送来。

⛔ **注意 `FlowEditor.Nodes` 在主工程 `FlowEditor/FlowEditor/` 里，不在 `FlowEditor.Runtime`** ——
它够得到是因为 **`FlowNodes` → `FlowEditor`**。也就是说 `Packing Machine` 能否编译，
**同样**挂在 `FlowNodes` 的引用表上（它引了 `Communication` / `CommunicationNew` / `FlowEditor` /
`WPFCustomControls` / `Device` / `Extension` / `Secs` / `Ctr_WPF_F48` / `DPLibrary_Halcon_F48` / `DataManager`）。
⇒ 3.1 那套「借来的依赖」在这里**规模更大、而且多了一个中转站**。

**建议动作**（与 6.1 同构，**纯新增、不含删除**）：补 11 条 `ProjectReference`。
⛔ 包引用一条都不补 —— `HalconDotNet` 尤其必须留在 `Extension.csproj` 一处锁 `23110.0.0`。
`Packing Machine` 位于依赖图**顶端**（全仓无人引用它）⇒ 不会引入循环引用，构建闭包不变。

**落地（2026-09-23）**：11 条 `ProjectReference` 已补齐（**纯新增、无任何删除**；包引用一条未补）。
文件为 CRLF + 2 行裸 LF（第 2、4 行）+ 末行无换行符，用「按 `\n` 切分再重组」原样保住。
**构建闭包实测不变（改前 21 = 改后 21，多出 0 个工程）**；`dotnet build Frame.sln -m` **0 错误**。
守卫 `HostProject_DeclaresItsDirectlyUsedProjects` 已加（在 `WPFControl.Tests`，`[Fact]` 4 → 5；
变异验证过：摘掉 `Extension` 那条即变红）。详见 `.workbuddy-ai/memory/2026-09-23.md` 第七节。
⛔ **仍未动**：`:33` / `:96` 那条**重复的** `WPFControl` 引用 —— 属删除动作，按规则待批。

### 6.5 ✅ 全量回归首次全绿（2026-09-23 13:07）

`DM_PG_PASSWORD=123 dotnet test Frame.sln -m --no-restore` ⇒ **退出码 0**、
**1694 用例 / 1694 通过 / 0 失败 / 0 跳过**、`: error ` **0 条**。
⛔ 不带 `DM_PG_PASSWORD` 时 DataManager 系仍有 **12 条**环境失败（5 集成 + 5 新功能 + 2 冒烟），**非回归**。

同轮修掉了 `FlowEditor` 的 4 条架构守卫失败（`FlowEditor.sln` + 测试里共 9 处硬编码旧路径，
`FlowEditorTest` → `FlowEditor.Test`、`FlowEditor.Runtime.Smoke.csproj` → `FlowEditor.Runtime.Test.csproj`）；
`FlowEditor.sln` 由「加载不了」变为 7 个工程路径全部有效，单跑 **345/345**。详见 `.workbuddy-ai/memory/2026-09-23.md` 第四节。

### 6.6 全仓「声明 ≠ 使用」审计 —— 又扫出 2 个漏项（2026-09-23）

起因是用户问「**传递引用不合适吗**」。结论要先澄清：**传递引用本身没问题**，
有问题的是「**借来的依赖**」—— 源码直接用了某工程的类型，csproj 却不声明它。
这条边不归自己管，上游一清理就断，而且**报错落在没改过的工程上**（正是 6.4 的情形）。

为把范围一次查清，做了一次全仓审计。

**算法**：正则扫每个 `.cs` 的 `namespace`（= 拥有者）与 `using`（= 使用者）→ 建工程依赖图
→ 对每个工程算**传递闭包** → 分两类：

- **A 类（疑似断裂）**：用到的命名空间**没有任何拥有者在自己的闭包内** ⇒ 编译本该失败；
- **B 类（借来的依赖）**：在闭包内、但**没有直接声明** ⇒ 上游一断就炸。

**A 类结论：全仓 0 条真断裂。** 扫出的两条全是**误报**，原因值得记下来：

| 误报 | 真因 |
|---|---|
| `Communication.Protocols.OpcUa` | 是 `Communication1` **自己的子命名空间** —— `owners()` 往上找祖先找错了 |
| `CommunicationNew.Protocols.Mitsubishi` | `MxComponentBridge` 用 `<Compile Include="..\CommunicationNew\...">` **链接编译**了别的工程的源文件 ⇒ 命名空间其实归自己 |

其余噪音（`Secs4Net → WpfVisualizer` 等）是**命名空间前缀撞车**，
已排除 `secs4net-base/`、`_wpftmp` 临时工程与包根命名空间。
⚠️ 另一个坑：按**根**命名空间统计会误判 —— `using FlowEditor.DataManagerLogging;`
会被算成「FlowEditor 工程」，实际归 `FlowEditor.DataManagerLogging`。

**B 类结论：`WPFControl` 在 6.1 那轮漏了 2 个工程** —— 它们同样只在闭包里（经 `FlowNodes`）：

| 工程 | 源码实际使用（`grep` 实测） |
|---|---|
| `FlowEditor` | 7 个文件：`FlowEditor.Core`(1) / `FlowEditor.ViewModels`(4) / `Views`(2 个 `.xaml`) |
| `WPFCustomControls` | 51 个文件：`Controls`(15) / `Window.Views`(9) —— 且 `.xaml` 里带 `assembly=WPFCustomControls`，**markup 编译期硬依赖** |

**落地**：`WPFControl.csproj` 的 `ProjectReference` 由 10 → **12 条**（纯新增）。
编码原样保住（CRLF 94 / 裸 LF 仍 2 / 无 BOM / 末字节 `>`）。
守卫 `ProjectFile_DeclaresTheSixDirectlyUsedProjects` 相应改名
`ProjectFile_DeclaresTheDirectlyUsedProjects`，期望数组 6 → **8 条**。

**变异验证（这次拿到了真红，不是假红）**：

⛔ 陷阱：这两个工程是**承重依赖**，直接摘掉 `ProjectReference` 会让**构建先挂**
（`error CS0234` / markup 找不到 assembly）⇒ 拿到的是「假红」——只证明引用承重，
**没证明断言有牙齿**。

改用「**删 `ProjectReference`，换成 `Reference` + `HintPath` 指向已编译 dll**」：
构建仍能通过（类型解析得到），但 `ProjectReference` 确实没了。结果：

```
失败 WPFControl.Tests.ArchitectureDependencyTests.ProjectFile_DeclaresTheDirectlyUsedProjects [2 ms]
   Assert.Contains() Failure: Sub-string not found
Not found: "..\\..\\WPFCustomControls\\WPFCustomControls"···
```

**构建 0 错误 + 恰好 1 条红 + 红的正是那条** ⇒ 真红。还原后 md5 逐字节一致
（`6b2c591a8d9c2c54a2f81562bf275df6`），无残留。

**其余 B 类（8 个工程，未动、等批准）**：`VisionDemo` / `Device.Workbench` /
`WPFControl.Tests` / `DataManager.Workbench.Tests` / `FlowEditor.Tests` / `FlowNodes` /
`Ctr_WPF_F48` / `Secs.Tests` —— 同一模式，处理方式与 6.1 / 6.4 相同（纯新增引用）。
⚠️ 其中 `Ctr_WPF_F48` 需先确认它不是 `Vision` 里的第三方目录。

> ⚠️ **上列 8 个是「上一轮口径」的结论，本轮重跑后已修正** —— 见 6.6.1。
> 准确口径下 B 类有 **46 个**工程，且「借来的依赖」**本身是 .NET 的常态**，
> **不建议**把这 8 个当作待办清单。

> 本节修正 6.1 的「6 个」为 **8 个**、6.2 的「4 个用例」为 **6 个**
> （4 原始 + `HostProject_DeclaresItsDirectlyUsedProjects` + 本轮改名那条）。
> 原文保留不删，以本节为准。

#### 6.6.1 口径修正与复核（同日重跑完整脚本）

6.6 里「其余 B 类 8 个工程」是**上一轮口径**的结论，本轮用完整脚本（58 个 `csproj` 全量）重跑后修正。

**A 类（真断裂）：仍是 2 条，且仍是那两条误报 ⇒ 全仓 0 条真断裂。** 结论稳定。

**B 类（借来的依赖）：58 个 `csproj` 里 46 个命中。** 这说明「借来的依赖」在 .NET 里
**是常态而非缺陷** —— `ProjectReference` 默认就是**传递**的（`PrivateAssets` 不含 `compile`），
「A 引用 B、B 引用 C、A 直接使用 C 的命名空间」本来就是合法且常见的写法。
⇒ ⛔ **不能一刀切全改**（会引入几百条引用，噪音远大于收益），
也不该把那 8 个当作待办清单。

**真正值得处理的判据**（6.4 那个才是真问题）：
提供方是**纯中转站**、且**它自己并不需要**被借用的那条引用 ⇒ 上游一清理就**静默断链**，
而报错会落在没改过的工程上。按此判据，**主链路只有 2 个**：
`WPFControl`（6.1 + 6.6）与 `Packing Machine`（6.4）—— **均已修**。

**复核「是否还有漏项」**（对已修的两个工程，逐命名空间反查归属工程）：

| 工程 | 闭包 | 借来的命名空间归属 | 未声明 |
|---|---|---|---|
| `WPFControl` | 21 个工程 | 12 个工程 | **0** ✅ |
| `Packing Machine` | 22 个工程 | 13 个工程 | **0** ✅ |

初筛各剩 1 条「未声明」，**逐条核实后均为误报** —— 都是**同名命名空间跨工程**：

- `FlowEditor.Core` 的 owner 是 `{FlowEditor, FlowEditor.Runtime}` ——
  `FlowEditor.Runtime/Core/*.cs` 里也写着 `namespace FlowEditor.Core;`；
  真正的提供方 `FlowEditor` **已声明**。
- `DPLibrary_Halcon_F48.Tools` 的 owner 是 `{DPLibrary_Halcon_F48, DPLibrary_Base_F48}` ——
  `Vision/DPLibrary_Base_F48/Tools/ToolMge.cs` 里写的是 `namespace DPLibrary_Halcon_F48.Tools`；
  真正的提供方 `DPLibrary_Halcon_F48` **已声明**。

⇒ **两个工程的修复是完整的**，无残留漏项。

> ⛔ 审计脚本自身的噪音（下次跑必须先排除）：`Device - 副本/`、`.tmp-render/`、
> `secs4net-base/`、`_wpftmp` 临时工程；
> 以及**别按根命名空间统计**（`using FlowEditor.DataManagerLogging;` 会被算成 `FlowEditor` 工程，
> 实际归 `FlowEditor.DataManagerLogging`）。

### 6.7 ✅ 本轮最终验证（2026-09-23 13:45）

| 项 | 结果 |
|---|---|
| `dotnet build WPFControl.sln --no-restore -m` | **0 错误**（258 警告） |
| `dotnet test WPFControl.sln` | **6/6 通过** |
| 变异验证（见 6.6） | **真红**：构建 0 错误 + 恰好 1 条红，红的正是目标用例；还原后 md5 逐字节一致 |
| `DM_PG_PASSWORD=123 dotnet test Frame.sln -m --no-restore` | **退出码 0**、**1695 / 1695 通过 / 0 失败 / 0 跳过**、`: error ` **0 条** |

分工程明细：`Secs.Tests` 25 / `DataManager.Tests` 180 / `Device.Drivers.Tests` 77 /
`Extension.Tests` 905 / `WPFCustomControls.Tests` 103 / `FlowEditor.Tests` 341 /
`DataManager.Workbench.Tests` 58 / `WPFControl.Tests` 6。

⚠️ 上一轮（6.4 / 6.5）出现过的 `BranchMergeNodeTests.Cycle_…` 时序抖动本轮**未复现**
（`TimeoutException` 出现 **0** 次）—— 印证那是并行负载下的抖动，不是回归。

**本轮改动文件（4 个，全部纯新增/改名，零删除）**：

| 文件 | 改动 |
|---|---|
| `WPFControl/WPFControl/WPFControl.csproj` | `ProjectReference` 10 → **12**（+`FlowEditor` / +`WPFCustomControls`） |
| `WPFControl/WPFControl.Tests/ArchitectureDependencyTests.cs` | 用例改名 + 期望 6 → 8 条（BOM 已核回） |
| `WPFControl-架构分析与优化方向.md` | 新增 6.6 / 6.6.1 / 6.7 |
| `.workbuddy-ai/memory/2026-09-23.md` | 新增第九节 |

⛔ **未提交** —— 仓库处于并发状态（`.git/index` mtime `13:16:37`，暂存区混有别的进程的
`AGENTS.md` / `.gitignore`），提交有碰撞风险。详见记忆第八节。

### 6.8 ⛔⛔ 一个**编译期看不见**的运行时炸弹：`Assembly.Load("FlowNodes")`（2026-09-23）

6.3 遗留项里有一条「`FlowNodes` 直引是否还需要摘除」。本轮做了**中转站审计**后，
这条边看起来是「纯冗余」（`WPFControl` 自己一处不用、补完 12 条后也无人借用）——
**但实测推翻了这个结论**。

**发现**：`Packing Machine/App.xaml.cs:268-269`

```csharp
// 强制加载 FlowNodes 程序集，确保其节点能被工具箱扫描到
System.Reflection.Assembly.Load("FlowNodes");
```

这是**运行时反射加载**，编译期完全看不见它。

**实测（摘掉 `WPFControl → FlowNodes` 这条边）**：

| 指标 | 正常 | 摘掉后 |
|---|---|---|
| `Packing Machine` 编译 | 0 错误 | **仍 0 错误** ⚠️ |
| 输出目录 `FlowNodes.dll` | 在（146944 字节） | **消失** |
| `deps.json`（201 个库） | 含 `FlowNodes/1.0.0` | **不含** |
| 运行时 `Assembly.Load` | OK | **`FileNotFoundException`** |

⇒ 那条边是 `Packing Machine` 拿到 FlowNodes.dll 的**唯一通道**。
**编译全绿、测试全绿，一启动就炸** —— 这类依赖最难排查。

**修复**（纯新增）：`Packing Machine.csproj` 补第 12 条工程引用
`..\..\FlowNodes\FlowNodes\FlowNodes.csproj`（该文件 `ProjectReference` 由 13 行 → **14 行**，
其中 `WPFControl` 仍重复声明两次，未动）。

**修复验证**：在「上游那条边已摘 **且** 手工删掉 `FlowNodes.dll`」的状态下重建 ⇒
`FlowNodes.dll` **重新出现**、`deps.json` **有** `FlowNodes/1.0.0` ⇒ 宿主已自给自足。

**守卫**（`WPFControl.Tests`，用例 6 → **7**）：

| 用例 | 钉住什么 |
|---|---|
| `HostProject_DeclaresItsDirectlyUsedProjects` | 数组 11 → **12** 条（加 `FlowNodes`） |
| `HostProject_RuntimeLoadedAssemblies_AreDeclared`（**新增**） | 扫宿主源码里所有 `Assembly.Load("X")` 字面量，断言 csproj 声明了 `X.csproj`；扫不到就 `Assert.NotEmpty` 红（防正则失效造成假绿） |

**变异验证**：摘掉 `Packing Machine` 的 `FlowNodes` 引用 ⇒ 构建 **0 错误**、
**恰好 2 条红**（正是这两个用例）、5 条绿 ⇒ **真红，守卫有牙齿**。

> ⛔ **对 6.3 遗留项的修正**：`FlowNodes` **不能摘**（至少在有 `Assembly.Load` 的前提下）。
> 正确顺序是**先**让 `Packing Machine` 显式声明它（本轮已做），**再**评估摘除 ——
> 且摘除前必须重跑「输出目录 + `deps.json`」检查，**不能只看编译**。

> ⛔ **审计方法的盲区**：`using` / `namespace` 扫描**扫不到** `Assembly.Load`、
> `XamlReader`、`Activator.CreateInstance` 这类反射加载。
> 全仓 grep `Assembly.Load` 是这类审计**必要的补充动作**。

> 本节改动文件 +1 ⇒ 本轮共 **5 个**（上表 4 个 + `Packing Machine.csproj`）。
> 另外：`WPFControl → DataManager.Controls` 也是纯冗余边（自己不用、无人借用），
> 实测摘掉后编译仍 0 错误 —— 按「不删」规则**未动**，仅记录。

### 6.9 ✅ 第二轮最终验证（2026-09-23 14:06）

| 项 | 结果 |
|---|---|
| `dotnet build WPFControl.sln` / `Packing Machine.csproj` | **0 错误** |
| `dotnet test WPFControl.sln` | **7/7 通过**（+1 = 新增的运行时守卫） |
| 变异验证（摘掉宿主 `FlowNodes` 引用） | **真红**：构建 0 错误 + 恰好 **2 条红** + 5 绿 |
| 修复验证（上游边已摘 **且** 手工删 dll） | `FlowNodes.dll` **重新出现**、`deps.json` **有** `FlowNodes/1.0.0` |
| `DM_PG_PASSWORD=123 dotnet test Frame.sln -m --no-restore` | **退出码 0**、**1696 / 1696 通过 / 0 失败 / 0 跳过**、`: error ` **0 条** |

分工程：`Secs.Tests` 25 / `DataManager.Tests` 180 / `Device.Drivers.Tests` 77 /
`Extension.Tests` 905 / `WPFCustomControls.Tests` 103 / `FlowEditor.Tests` 341 /
`DataManager.Workbench.Tests` 58 / `WPFControl.Tests` **7**。

**本轮累计改动 5 个文件**（4 个见 6.7 表 + `Packing Machine.csproj`）：

| 文件 | 改动 | md5 |
|---|---|---|
| `WPFControl/WPFControl/WPFControl.csproj` | `ProjectReference` 10 → **12** | `6b2c591a…` |
| `Packing Machine/Packing Machine/Packing Machine.csproj` | 13 → **14** 行（+`FlowNodes`，运行时依赖） | `ff71a9b0…` |
| `WPFControl/WPFControl.Tests/ArchitectureDependencyTests.cs` | `[Fact]` 5 → **6**；两个期望数组分别为 **8** / **12** 条 | `7e53907a…` |
| `WPFControl-架构分析与优化方向.md` | 新增 6.6 / 6.6.1 / 6.7 / 6.8 / 6.9 | — |
| `.workbuddy-ai/memory/2026-09-23.md` | 新增第九 / 十节 | — |

⛔ **仍未提交**（并发状态未解除，理由同 6.7）。

### 6.10 ✅ 全仓反射加载排查（2026-09-23 14:20）—— 只有 1 处真问题

按 6.8 的教训，把「全仓 grep `Assembly.Load`」这条补充动作真正做到底。结果：

| 工程 | 调用 | 声明 | 结论 |
|---|---|---|---|
| `Packing Machine` | `Assembly.Load("FlowNodes")` | 本轮补 | ✅ **唯一真问题**，已修 |
| `FlowNodes.Workbench` | `Assembly.Load("FlowNodes")` | 已声明 | ✅ 正常 |
| `FlowEditor` | `Assembly.Load("FlowNodes")` | 未声明 | ✅ **故意的** —— `FlowNodes` 反过来引 `FlowEditor`，声明会**循环**；用 `try { … } catch { return null; }` 降级（失败只是工具箱扫不到节点） |
| `ScriptEditor` | `Assembly.Load("RoslynPad.Roslyn.Windows")`<br>`Assembly.Load("RoslynPad.Editor.Windows")` | NuGet 包 | ✅ 正常 —— 前者是显式 `PackageReference`，后者是它的**传递依赖**（实测在 `deps.json` 里） |
| `WPFControl.Tests` | — | — | ✅ 误报（我自己守卫注释里的 `Assembly.Load("X")`） |

**其余反射 API 全仓为空**：`XamlReader.*` **0** 处、`Type.GetType("…")` **0** 处、
`Activator.CreateInstance("…")` **0** 处。

**顺带修掉审计脚本的两个误报源**（已回写 skill 脚本）：

1. 只查 `ProjectReference` ⇒ NuGet 包被判「未声明」→ 改为同时认 `Include="包名"`；
2. 不剥注释 ⇒ 文档注释里的示例被当成真实调用 → 先剥掉整行 `//`；
3. 输出加提示：**循环依赖**与 **NuGet 传递依赖**是「未声明」的两类**合理例外**。

> **结论：`Assembly.Load` 这条线收口。** 全仓唯一的运行时炸弹就是 `Packing Machine`，
> 已修，且已有守卫 `HostProject_RuntimeLoadedAssemblies_AreDeclared` 钉住。

### 6.11 ✅ 依赖审计的「五条线」全部收口（2026-09-23 15:40）

6.6 / 6.8 / 6.10 之后，把「一个工程需要另一个工程」的**所有通路**逐个查完 ——
`using` 只是其中一条，**漏掉任何一条都会得到假结论**。

| # | 通路 | 怎么查 | 全仓结果 |
|---|---|---|---|
| 1 | `.cs` 的 `using` / 完全限定名 | 正则 + 最长前缀匹配 | A 类 **0** 真断裂；B 类 46 个工程（主链路 2 个已修） |
| 2 | `Assembly.Load("X")` **反射加载** | **额外 grep**（编译期看不见） | 4 处调用 ⇒ **1 处真问题**（`Packing Machine`，已修） |
| 3 | `.xaml` 的 `assembly=X` | 扫 `.xaml`（**不能只扫 `.cs`**） | 11 个本仓程序集 ⇒ **0 漏项** |
| 4 | `<Private>` / `PrivateAssets` / `ExcludeAssets` | grep csproj | 24 处，**全是测试工程标准写法**（xunit）+ 1 处正常 `<Private>true</Private>` ⇒ **无危险设置** |
| 5 | `<Compile Include="..\Other\X.cs">` **链接编译** | 扫 csproj 的 `Compile Include` | 3 处，全在 `CommunicationNew.MxComponentBridge`；那些文件**只用 BCL**（自包含纯算法类），且该工程**没有任何 `ProjectReference`** ⇒ **0 漏项，是故意的** |

**结论：全仓依赖图没有残留的「声明 ≠ 使用」漏项。**
唯一真正的运行时隐患（`Packing Machine` 的 `Assembly.Load("FlowNodes")`）已修复并有守卫。

> ⛔ **方法论**：判「某条依赖边能不能摘」**必须把五条通路都查一遍**；
> 第 2 条要用「输出目录 + `deps.json`」验证（编译通过说明不了什么），
> 第 3 / 5 条**不能靠 `.cs` 扫描覆盖**。已回写 skill 脚本（新增反射加载检查）。

### 6.12 🚨 别的进程把我的改动提交了 + merge 后回归（2026-09-23 15:38~15:50）

**15:38 检查时发现历史变了**：`git log` 顶部从 `9e1c657` 变成 **`488cb2a`**（merge），
中间多了 `55dd3fd` / `b3fb20c` / **`ce39c6b WPFControl架构优化`** / `8589576` 四个提交，
`.git/index` mtime = **`15:38:50`**。

**`ce39c6b` 里混着我的改动**（8 文件 = 我的 6 个 + 别的进程的 `.gitignore` / `AGENTS.md`）
⇒ 这正是 6.7 担心的「谁先 commit 都会带上对方的东西」，**成真了**。
⛔ **不是我提交的** —— 我全程没执行过 `git commit`。

**✅ 但工作区与 HEAD 都完好**（逐项核过；用户记忆里那条「Desktop 路径 git 写入会被回滚」这次没发生）：

| 检查 | 工作区 | HEAD |
|---|---|---|
| `WPFControl.csproj` 引用数 | **12** | **12** |
| `Packing Machine.csproj` 引用数 | **14** | **14** |
| 守卫 `[Fact]` 数 | **6** | **6** |

**merge 后回归**（`DM_PG_PASSWORD=123 dotnet test Frame.sln -m --no-restore`）：
**总计 1853 / 通过 1841 / 失败 12**。

| 工程 | 结果 | 备注 |
|---|---|---|
| `DataManager.Tests` | **268 ✅ / 12 ❌** | 280 个（原 180，**+100** SQL 测试） |
| `Device.Controls.Tests` | 53 ✅ | **新工程**（merge 带来） |
| `WPFControl.Tests` | **11 ✅** | 原 7，**+4** = `SerializationContractTests`（`8589576` 引入） |
| 其余 6 个 | 全绿 | `Extension` 905 / `FlowEditor` 341 / `WPFCustomControls` 103 / `Device.Drivers` 77 / `DataManager.Workbench` 58 / `Secs` 25 |

**12 个失败全是环境缺失**：`MySqlException : Unable to connect to any of the specified MySQL hosts`
（`MySQLIntegrationTests` 6 + `SqlServerIntegrationTests` 6）—— 本机只有 PostgreSQL，
没有 MySQL / SqlServer 实例；这些测试由 **`b3fb20c`**（别人的提交）引入 ⇒ **非回归**。

> ⚠️ 另一个新情况：Git 报
> `LF will be replaced by CRLF the next time Git touches it`（本文档）⇒
> `core.autocrlf` 会把纯 LF 文档转成 CRLF。要保住 LF 需加 `.gitattributes`（**新文件，需批准**）。

### 6.13 ⚠️ 那 12 个失败需要什么环境（只读分析，2026-09-23 16:00）

| 测试类 | 需要 | 默认连接 | 覆盖用环境变量 |
|---|---|---|---|
| `MySQLIntegrationTests`（6 个 `[Fact]`） | 本机 MySQL | `127.0.0.1:3306`，`root` / `mysql`，库 `dm_integration` | `DM_MYSQL_HOST` / `_PORT` / `_USER` / `_PASSWORD` / `_DB` |
| `SqlServerIntegrationTests`（6 个 `[Fact]`） | 本机 SqlServer **命名实例** | `localhost\BARTENDER`，库 `dm_integration`，**Windows 身份验证**（账号默认空） | `DM_MSSQL_HOST` / `_DB` / `_USER` / `_PASSWORD` |

⛔ **两者都没有 `Skip` / `Ignore` 机制** —— 全文件只有 `Environment.GetEnvironmentVariable`
读配置（各在 `:61`），环境不可用时**硬失败**。
⇒ 「没装这两个库的机器上，全量回归永远不干净」，**基线不可信**。

**建议（待批准；属改别人的测试代码，未动）**：加环境探测 + `Assert.Skip`
（或 `[SkippableFact]`），探测不到就**跳过而不是失败**。

**⚠️ 别混淆两批「12 条失败」**：

| | 触发条件 | 性质 |
|---|---|---|
| PostgreSQL 那 12 条（5+5+2） | **不带** `DM_PG_PASSWORD` | 本机有 PG，给了密码就过 |
| 本次这 12 条（MySQL 6 + SqlServer 6） | 本机**没有** MySQL / SqlServer 实例 | 给密码也没用，缺的是实例 |

⇒ 两者**不是同一批**，都不构成本轮回归。
