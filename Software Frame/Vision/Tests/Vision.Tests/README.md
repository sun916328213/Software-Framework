# Vision.Tests — 纯逻辑测试线

## 当前：Tools优先收口（总体计划第33～37节）

第37节新增GrayGamma25项，迁移Otsu原17项：聚焦42、相关172通过，0失败/0跳过。Gamma为明确满量程幂函数及非负半向上量化，不改变GrayLinear的SDK舍入；Otsu普通/反向、常量零阈值及Mono16原位深均明确。当前Licensing.Core独立资产缺失按同路径现有图restore后解除，未修改授权/项目/包声明，HALCON仍106依赖警告。完整旧工程当前275个编译错误；SegmentationToolsTests的逐图归一化/旧线性半向上需求仍未迁完，未删断言或恢复旧Mode JSON兼容。见总体计划第37节。

以下第36节数字保留其当批含义：

第36节补语义RGB通道提取：原BasicImageToolsTests有效需求完整迁移，18项扩成24项（3存储格式×R/G/B），另13项通道边界/真实任务链，聚焦37项、相关130项通过，0失败/0跳过。亮度76/29、线性映射128、滤波核及原位深统计解析期望保留；不恢复可变旧图像/DTO/PrepareRun。全工程当前仍292个旧契约编译错误；此批不修改外层。详情与过滤见总体计划第36节。

以下第35节数字保留其当批含义：

第35节补有效功能而非只改测试：独立GrayHistogramOperation/Output，选中OpenCV模块内托管long原位深计数/明确分箱及未量化中位数；保持旧GrayStatistics默认契约，HALCON仍Unsupported且不回退。初始22项16红/6绿，接通后补至25项，与相关旧统计/ROI/预处理/生命周期/边界fake/Core契约联合93通过、0失败、0跳过。实测2^24+1计数、Mono16原编码及偶数半整数中位数，不宣称SDK原生直方图或真实工件验收。完整旧工程仍302个编译错误；见总体计划第35节。

第34节迁移`Tools/OpenCv/MatConverterTests.cs`至当前内部OpenCvFrameAdapter，并迁移`ImageFileToolTests.cs`至IVisionEngine无图文件请求：适配器26项+文件9项，聚焦35通过、联合上批94通过、0失败、0跳过。验证padding/6种原始存储、非连续Mat和双向复制所有权、PNG/TIFF位深、内容SHA256及重复读取/关闭后快照；不恢复旧转换器或可变图像。完整工程当前仍302个编译错误，未排除旧源码求绿。浮点TIFF按当前Float32存储契约保留，而非沿用旧的一概拒绝。详情、脚本及证据见总体计划第34节。

以下第33节数字保留其当批含义：

用户已要求先收口Tools，Controls/Workbench/Nodes暂缓。本批将现有6项Golden和2项性能记录迁至IVisionEngine，保留解析黄金及有效用例；工厂直接返回VisionFrame，不恢复旧VisionImage/Tool。

- 文件：`TestAssets/GoldenImages.cs`、`TestAssets/GoldenRegressionTests.cs`、`Performance/BaselineTests.cs`。
- 聚焦8通过；联合卡尺直线/圆、预处理与生命周期59通过、0失败、0跳过，使用真实OpenCV合成图，不执行新HALCON原生、真实AI或宿主。
- 白带找线明确测下侧边缘y=101.5，不与中心线混称；腐蚀黄金仍504；性能只记录未预热单次引擎调用，不设时间阈值，不与旧ElapsedMs直接比较。
- 完整工程基线309个编译错误，迁移后仍305个旧API引用错误，尚不能全量运行；未排除生产/旧测试源码或批量删除有效用例。不将局部通过称为完整工程通过。
- 复现脚本、过滤、日志及当前TRX范围见`../../TOOLS_REDESIGN_PLAN.md`第33节。下方旧Tool/Provider约定仅为待迁移历史说明，不是恢复旧接口的目标。

## 新契约重建期间的验证边界

当前Core/Tools已切换强类型IVisionEngine；下文的旧Tool/Provider测试与历史全量数字不代表新契约已整体通过。完整旧测试工程仍有未迁移消费者，不恢复兼容壳，也不为求绿批量删除测试或排除生产源码。

新路径位于`Tools/Engine/`。T2首批`BlobWorkflowTests`的14个案例覆盖真实OpenCV离线闭环、ROI来源/修订、像素/孔洞口径与必要SDK边界fake；合并相关Core、生命周期、模板及T1回归共90通过、0失败、0跳过。使用既有xUnit/VSTest隔离编译实际测试源，没有新增测试工程。第二批`BlobContourMetricsTests`新增7项轮廓面积/周长/圆度与筛选案例，与T2闭环和Core契约合并定向44项通过。T3首批`CaliperLineEngineTests`新增11项，覆盖真实选边参数、斜向采样、Mono16单位及参与点/残差，与相关Core/生命周期/T2闭环定向63项通过。第二批`GeometryMeasurementEngineTests`新增15项无图几何/公差及实际像素距离链，关联定向78项通过。第三批`CaliperEdgePairEngineTests`新增15项同剖面测宽/统计/公差及薄带峰回归，关联定向79项通过。第四批`CaliperCircleEngineTests`新增13项圆/弧采样、拟合/覆盖与直径公差，关联定向92项通过。第五批`CaliperLocationFollowTests`新增6项真实旋转跟随/多目标/来源与修订守卫，关联定向74项通过。T4首批`PixelScaleCalibrationEngineTests`新增10项已知长度均匀比例、毫米换算/公差与实际定位跟随毫米链，关联定向69项通过。T4第二批`AffineCalibrationEngineTests`新增17项二维仿射/双向世界点/毫米点距公差与病态残差拒绝，该批联合86项通过。第三批`CalibratedWorldLineEngineTests`新增11项世界直线/毫米垂距及与扫描轴点距的真实合成图区分，该批联合97项通过。T5首批`RgbColorSegmentEngineTests`新增11项RGB/BGR闭区间、固定ROI、二值Blob闭环及格式/回退拒绝，该批相关70项通过。第二批`AlignedImageDifferenceEngineTests`新增14项显式双图/方向/位深/对齐/来源核验与缺陷Blob链，该批相关84项通过。第三批`QrCodeReadEngineTests`新增14项单目标QR实际解码/损坏/搜索窗及无SDK全文判定，该批相关98项通过。第四批`QrCodeReadManyEngineTests`新增11项多QR独立状态/重复文本位置/候选预算及无SDK解码计数，该批相关109项通过。第五批`HsvColorSegmentEngineTests`新增11项量化HSV闭区间/跨零/无色相策略及Blob链，该批相关81项通过。后续第29节以同一artifacts路径定向还原现有图，已解除Licensing资产缺失，HALCON/AI/OpenCV关联构建成功，并刷新实际传递DLL运行96项通过；未改工程/包版本，传统HALCON依赖仍有106警告，fake不作为新原生/真实AI验收。T6首批`AiClassificationPublicationTests`新增6项发布边界，4项坏结果先复现成功发布再修为失败，相关52项通过。第二批`AiPredictionJudgmentEngineTests`新增9项显式预测位置/类别/分数的无SDK判定与适配器fake闭环，当前相关61项通过；未新增真实模型/原生推理验收。确切过滤、日志、构建与未验范围见`../../TOOLS_REDESIGN_PLAN.md`第14～31节；不是全仓或真实工件验收。

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
| `Tools/Contracts/` | 公共 Contract | 15 个传统 Tool（含独立 Crop）× 统一断言形状 |
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

## 最新 Blob/ROI 回归（非 Runtime 逻辑已执行）

- `Core/SearchRegionTests`：不可变几何、真实形状/边界、裁切和无效域拒绝。
- `Tools/OpenCv/BlobSearchRegionTests`：域在连通域前生效、灰度零、原图坐标、域外为空及独立后台快照；原孔洞回归继续保留。
- `Tools/Halcon/BlobSearchRegionRuntimeTests`：同类解析域期望与后台资源行为，保留 RequiresRuntime/Skip，只在授权离线环境执行。
- UI/会话回归在既有 `Vision.Workbench.Tests/Controls`：`BlobBrowserSearchRegionTests`、`BlobExecutionSessionTests`、`BlobViewportRegressionTests`；不在本工程引用 WPF。

Git Bash 安装后已实际执行 `Verify-EditorRefinement.ps1 -Full`，本工程非 Runtime 全量 325 通过、0 失败，最新独立 XAML 属性视图轮 WPF 测试线 180 通过；Workbench 构建通过。Runtime 测试仍未执行，实际性能和人工交互未验收。完整执行记录及本次修补见 `../EDITOR_REFINEMENT.md`。

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
