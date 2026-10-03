# Vision.Tests — 纯逻辑测试线

本工程是 Vision 模块的**无 WPF**测试线：Core / Tools / AI 的算法与契约。
生产项目禁止引用本工程（`SolutionStructureTests.ProductionProjects_DoNotReferenceTestProjects` 守卫）。

⛔ 本工程**不得引用 WPF 工程**（`Vision.Controls` / `Vision.Workbench`）。
控件 VM 与 Workbench 装配测试已迁到姊妹工程 **`Vision.Workbench.Tests`**（`UseWPF` + STA 宿主），
两条线各自独占一种运行环境、可独立构建，CI 才能分组跑。

## 目录 ↔ 测试域

| 目录 | 测试域 | 说明 |
|---|---|---|
| `Architecture/` | 架构守卫 | sln/引用方向/Core 纯度/后端独立/退场审计 |
| `Core/` | Core 单元 | 几何/ROI/坐标链纯逻辑 |
| `Tools/Contracts/` | 公共 Contract | 14 个传统 Tool × 统一断言形状 |
| `Tools/Halcon/`、`Tools/OpenCv/` | 后端单测/回归 | 行为深度 + 世界坐标 |
| `Tools/Comparison/` | 跨后端对比 | 业务容差口径，不逐像素一致 |
| `Tools/AI/` | AI Tool + Provider | fake 全流程 + 三家无 SDK 行为 |
| `TestAssets/` | Golden 回归 | 合成图 + 解析期望（改动黄金须同步改注释） |
| `Performance/` | 性能基线 | 只记录、不设阈值 |

## Category 分类法与执行边界

| Category | 含义 | 默认执行 |
|---|---|---|
| `Unit` | 纯逻辑/真实 CPU 算子，无外部依赖 | 普通 CI 必跑 |
| `Contract` | 统一行为契约（fake/无 Runtime 路径） | 普通 CI 必跑 |
| `Architecture` | 结构守卫 | 普通 CI 必跑 |
| `Regression` | Golden 输出锁 | 普通 CI 必跑（稳定子集） |
| `Performance` | 耗时记录，无阈值断言 | 跑（永不因波动变红）；基线对比看日志 |
| `RequiresRuntime` | 需真实 SDK/DLL/GPU/License/模型 | `Skip`，有环境时手动去掉 Skip 执行 |

常用过滤：

```powershell
dotnet test Vision.Tests.csproj -c Debug --filter "Category!=RequiresRuntime"
dotnet test Vision.Tests.csproj -c Debug --filter "Category=Performance"
dotnet test Vision.Tests.csproj -c Debug --filter "Category=Architecture"
```

## 新增测试约定

1. 新 Tool：到 `Tools/Contracts/StandardToolContractTests` 加一行条目（默认有效性 + 致 invalid 修改器）。
2. 新 Golden：先定解析期望，再写 `TestAssets/GoldenImages.cs` + 回归 fact。
3. 真实环境用例：一律 `[Fact(Skip = "...")]` + `[Trait("Category", "RequiresRuntime")]`，注释写清所需环境。
4. 性能相关：一律 `Performance/` + `Category=Performance`，禁止在 Unit/Contract 里断言耗时阈值。
5. 注释提到 SDK/厂商类型不算依赖（各守卫测试均先剥注释再扫描）。

## 测试线边界

Vision 模块按**运行环境**分两条测试线，不再往同一个工程里塞：

| 工程 | 运行环境 | 覆盖 |
|---|---|---|
| `Vision.Tests`（本工程） | 无 WPF | Core 几何/ROI/坐标、Tools 契约与后端、AI Tool 与 Provider、Golden、性能 |
| `Vision.Workbench.Tests` | `UseWPF` + STA 宿主 | 控件 VM/展示、Workbench 装配、App.xaml 资源合并与 pack URI 解析 |

判定口径：**测试或夹具需要 WPF 程序集 / STA / `Application` 就归 `Vision.Workbench.Tests`**，否则留在本工程。

暂不进一步拆（如 `Vision.Tests.Integration.*`）：当真实 SDK/GPU 集成测试显著拖慢构建，
或部署依赖互相冲突时再拆；不要提前创建 `Vision.*.Tests` 碎片工程。
