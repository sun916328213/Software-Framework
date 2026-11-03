# Vision.Core 单一入口重建计划

## 状态与实施口径

用户已确认：Core / Vision.Tools / Controls / FlowNode / Workbench 五层边界；不以旧API兼容为设计约束。此计划是破坏性架构重建，不是行为不变的重构。

P1契约已完成，P2首批完成共享Tools所有权与OpenCV模板真实执行；第二批接入HALCON AI/cpu分类直接SDK调用路径（尚未真实模型验收）。第三批接入传统HALCON形状匹配SDK路径（同样未真实SDK验收）；第四批接入启动配置冻结与延迟DI装配；当前为P3前置补齐基础无模型处理；用户提供HALCON23.11安装路径并明确授权后，5项传统HALCON合成图原生验证已通过。旧消费者迁移、AI及其他SDK场景/实图验收仍未完成。过渡期间原接口仍被Tools、Controls、FlowNodes源码消费；不新增旧接口转发或兼容壳，P4必须删除旧入口。禁止把新接口声明完成说成后端已接入、配置切换已生效或全Core已只剩一个接口。已有模板旋转/缩放、UI及Core基类等工作树修改不覆盖。

## 1. 五层硬边界

| 层 | 所有职责 | 禁止职责 |
|---|---|---|
| Vision.Core | IVisionEngine唯一新业务入口；参数、图像快照、几何、模型引用、能力、结果与诊断契约 | SDK、厂商实现、DI容器、配置文件读取、WPF、节点、存盘引擎 |
| Vision.Tools.* / AI实现 | 被选中的传统视觉与AI后端；算法、原生图像转换、模型资源、线程与取消策略 | 页面、流程图执行器、宿主导航 |
| Vision.Controls | 参数/ROI/模板掩膜编辑、图像和结果显示、借用编辑会话、可复用装配入口 | SDK算法、厂商结果直接绑定、另建执行引擎 |
| Vision.FlowNode | 厂商中立工具节点、端口、数据绑定、流程文档状态；调用IVisionEngine | 引用具体Tools/Controls、选择后端、模型原生句柄 |
| Vision.Workbench | 启动、配置来源、选择模块、导航、关闭 | 参数定义、工具表、算法或文件引擎 |

依赖只能朝向Core；Tools不能引用Controls/FlowNode/Workbench，FlowNode不能引用Controls/具体Tools。其他宿主（包括FlowNodes.Workbench）可选择是否装配视觉能力，不能被Vision.Workbench绑定。MachineApp不修改、不构建、不验证。

## 2. 唯一入口及唯一后端

业务侧只注入IVisionEngine。传统视觉配置只能给出一个BackendId，AI只能给出一个BackendId及明确DeviceId（也可不启用AI）。操作请求不带后端选择字段。选择在启动时冻结，只有实际停止、释放并重新装配后才能切换。

Core仅定义选择值；绑定配置、校验已部署实现/License/设备、注册唯一实例属于装配层。禁止未知实现、缺运行库、License或GPU失败时静默改用其他后端或CPU。未选择的实现不创建、不探测厂商SDK。相同公共契约不代表厂商模型、得分或精度等价。

IVisionEngine负责查询描述、校验请求、创建上下文、准备模型、强类型执行、释放上下文/引擎。所有异步入口携带CancellationToken。上下文和模型只返回中立标识，不返回第二个公开Session/Provider接口，不暴露对象字典或原生句柄。

## 3. P1已交付契约

- [x] 唯一新业务接口IVisionEngine，不内置假后端，不从旧接口转发。
- [x] 单传统视觉/可选单AI的选择值；启动唯一性和实际初始化留P2验证。
- [x] 带引擎/上下文归属的模型引用，明确释放与失效契约。
- [x] 独立像素快照、帧/坐标空间身份、严格二值掩膜与尺寸校验。
- [x] 模板匹配强类型请求/结果：角度、等比缩放、模板有效掩膜、搜索有效掩膜、定位锚点、最低分数/重叠/数量、极性策略；明确不承诺镜像/形变/非等比缩放。
- [x] 匹配结果以锚点、角度、比例为唯一变换来源，复用Core现有Point2D/AffineTransform2D与旋转数学，不复制几何引擎。
- [x] AI模型准备与分类请求共用同一入口；检测/分割/异常契约按P2/P3继续补全，不以空实现声明能力。
- [x] 统一成功、未找到、失败、未支持、取消、超时结果；失败不携带旧数据。
- [x] 在现有Vision.Tests补契约、非法输入、快照独立性、变换、归属和架构测试；不新增测试工程。
- [x] 删除经全仓源码/XAML/配置检索确认没有消费者的BaseHelper/LiangBarskyClip与DoubleEx；不删除已被使用的ToolMge/Singleton等再隐藏编译错误。

本轮不升级.NET/C#/SDK包、不改全局nullable约定、不接真实硬件、不安装Runtime、不解除RequiresRuntime。新文件局部启用nullable。现有VisionImage仍用于未迁移旧调用；新入口使用不可变VisionFrame，P4统一替换后删除旧可变边界，不增加互相自动转换的兼容层。

## 4. 模板与坐标语义

- X为列向右、Y为行向下，旋转矩阵固定为(x*cos-y*sin, x*sin+y*cos)，因此图像显示上正角度为顺时针；厂商在Tools内换算，禁止逐处翻符号。
- 模板建立区域、模板参与掩膜、搜索区域三者分开。P2明确SearchMask限制模型锚点落点（按最近像素中心取域），不是覆盖搜索图像素的遮挡掩膜。新门面使用像素中心索引坐标，旧内核几何中心在Tools边界换算，避免0.5像素偏差。首版掩膜只接受0/1/255二值数据，统一有效含义；拒绝全空、错误尺寸、未知权重，不把填充黑边当作模板特征。
- P2新增TemplateSearchSampling明确离散角度/缩放步长（默认5度/0.1），不能把粗采样冒称连续/亚像素匹配；不做未适配掩膜的隐式精化。角度范围、正比例范围必须有限且有效；运行搜索不能超出模型准备范围。模型锚点使用模板坐标，结果位置表示该锚点在目标图中的位置。
- 结果返回模板到图像的完整仿射值及帧/坐标空间身份。缩放不得伪装成刚体Pose2D；跨Crop/物理单位/测量带宽的变换规则单独验证。
- 模板像素、掩膜、建模参数或锚点改变必须重新准备模型。原生模型带后端/引擎实例/上下文/修订归属，跨实例和跨后端不得复用。
- 未找到是成功完成搜索但没有候选，不等于SDK失败。不同后端分数保留语义，不承诺相同阈值给出相同检测行为。

## 5. 分阶段路线与验收

| 阶段 | 内容 | 完成门禁 |
|---|---|---|
| P0 计划/盘点 | 冻结本文件、边界、迁移与删除清单 | 明确API与所有权，无不明责任层 |
| P1 Core契约（已完成） | 上述契约、纯值校验与未使用辅助代码清理 | Core独立构建；契约和非Runtime逻辑测试非零、正常退出；明确仍存在的旧接口 |
| P2 Tools最小闭环 | 唯一实现装配、上下文/模型注册、选定后端初始化、模板准备/执行、AI加载/推理 | 未选SDK不初始化；错误不回退；上下文互斥、取消/释放无提前销毁；不是仅声明接口 |
| P3 Controls/FlowNode | 厂商中立节点与编辑器；原流程控制/文件机制消费新请求 | 编辑器只借上下文；参数快照隔离；掩膜编辑、匹配、定位跟随、保存重开贯通 |
| P4 Core退役清理 | 删除旧Provider/Session/Tool/Presentation接口、工具总线/单例存盘、UI元数据、无关集合和旧可变图像边界 | 新生产调用只剩IVisionEngine；不存在双轨生产调用或兼容壳；Core零SDK/UI/流程/DI/存盘依赖 |
| P5 工业能力验收 | HALCON/OpenCV各自能力与真实样本矩阵，NI可行性及后续实现 | 精度/漏检/误检、P95/P99节拍、长期内存、License/部署环境均有独立证据 |

### P4明确退役清单（不以P1暂留为兼容承诺）

- Tools/ITool、IEditableTool、IImageTool、IVisionToolSession、IModelPreparationTool、旧Tool/IVisionTool/ImageToolBase与旧结果接口；ToleranceTool等算法归Tools，Core仅留领域参数/结果。
- AI/IAIProvider、IAIModelSession；其实现协作转为Tools/AI内部，不对业务公开。
- Presentation/*、PropertyToUI/*、EnumEx的编辑元数据归Controls，Core不保留UI扩展接口。
- Tools/ToolMge、ToolBus、Serialization/JsonHelper、Singleton、ILog_DP等旧管理/文件/绑定体系删除或按实际职责迁出；不得再在Core建立新版本。
- 旧Vision/I_ROIs、I_XLDs、厂商枚举/颜色转换；统一几何/区域值类型，显示命中测试归Controls。
- ObservableCollectionEx与通用辅助方法依据实际消费者归Controls/公共基础模块或删除。
- Core.csproj的旧类型解析链接/资源及JSON包必要性在旧存盘/元数据迁出后复核，不先删除造成静默类型恢复改变。

## 6. 生命周期验收表

| 对象 | 拥有者 | 关键规则 |
|---|---|---|
| 已选引擎 | 宿主装配 | 停止接收新请求；等待实际执行结束后释放 |
| 工具上下文 | 流程工具实例/明确调用者 | 不同工具不共用可变参数/结果；关闭窗口不释放 |
| 原生模型 | Tools内上下文注册表 | 准备成功后才发布；失败清候选；跨实例/后端/修订拒绝 |
| 图像/掩膜快照 | Core值对象的托管数据 | 构造时复制；UI修改源缓冲不改变在途请求 |
| 编辑器 | Controls借用者 | 只编辑下一份配方，不把迟到结果发布到新配方 |

取消请求/超时不等于原生执行已停止。Tools未确认实际结束时必须保持资源存活并隔离迟到结果；需要硬超时隔离时另行评估工作进程，不用Task.Run/强停线程伪装安全。

## 7. 验证记录

P1契约阶段已完成（尚未完成P2～P5）。

- 修改前完整非Runtime逻辑基线731通过。新增入口/两类请求的3项实际失败（缺少新契约），实施后同3项通过；没有把最初测试缺少using Xunit的编译错误当作红灯证据。
- 最终新增VisionEngineContractTests为23项，连同Core纯度、仿射和旧图像快照的定向41项通过。覆盖帧/掩膜独立快照、错误步长/尺寸、二值权重拒绝、圆周搜索范围、缩放和锚点矩阵、能力缺失、跨引擎/上下文/后端拒绝、无模型操作、AI明确设备和内容哈希、只读集合、失败无旧结果、未找到语义，以及新API可达类型仅暴露一个Core业务接口。
- 完整逻辑754通过、0失败、0跳过，47.9557秒正常退出，RequiresRuntime仍排除。不是HALCON/AI Runtime或新IVisionEngine算法通过。
- Core显式独立构建31警告/0错误；警告均来自本轮未修改的既有nullable注解（AffineTransform2D、旧Presentation/Results及链接的FrameworkTypeResolver），新契约文件无警告。未通过改变全局nullable或删掉守卫掩盖警告。
- Vision.Workbench消费者显式构建4警告/0错误，来自未修改RoiEditorView。未运行完整WPF/FlowEditor/FlowNodes独立测试，未启动物理UI/SDK/硬件，不借历史通过数。
- 删除BaseHelper.cs、ValueEx/DoubleEx.cs前，检索全仓标识及方法调用，仅命中原定义/注释；后续全量逻辑和消费者编译通过。保留其他既有工作树和暂存内容，不提交。

根目录执行，命令均追加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

```powershell
dotnet build "Software Frame/Vision/Vision.Core/Vision.Core.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~VisionEngineContractTests|FullyQualifiedName~CorePurityTests|FullyQualifiedName~AffineTransformTests|FullyQualifiedName~VisionImageSnapshotTests' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
```

%TEMP%/vision-core-p1-{baseline,red,green,focused,core-build,logic,host-build}.log记录实际输出。P1时的下一步为Tools实现与上下文/模型生命周期，进展见下面P2首批记录。消费者迁移和旧接口删除仍不可跳过。

## 8. P2首批实际进展（不是P2全阶段完成）

### 交付

- 新增Tools/Runtime/Vision.Tools.csproj：只依赖Core的共享执行项目，承载实际IVisionEngine、上下文互斥、唯一当前模型、候选事务、资源账本、关闭/重试与迟到结果拒绝。只对Tools模块暴露internal实现协议，不向业务再加Provider/Session接口。
- VisionEngineFactory消费冻结的VisionEngineSelection，生产只激活opencv模块；未知/尚未接通的HALCON、NI、AI配置在任何SDK初始化前明确失败。没有假HALCON、空AI或静默回退。HALCON/AI的真实新适配仍是后续工作。
- 抽出OpenCV TemplateMatchSearch共享算法，原旧工具与新门面消费同一姿态内核；先确认旧40项行为不变，再加掩膜和取消语义。旧固定0角/1倍路径与旧UI的精化行为不擅自改动，最终仍随消费者迁移退役旧入口。
- 新OpenCV真实模型准备持有私有原生Mat；无纹理、无效有效域、非Mono8或未支持的形状/极性模式明确拒绝。准备失败保留原模型，成功替换立即撤销旧引用；模型资源不能跨上下文共用，也不能用同GUID新对象伪造原模型引用。
- 新门面真实执行灰度相关、离散旋转/等比缩放、二值模板掩膜、锚点搜索域与多目标；模板和有效掩膜一起变换，排除旋转补边/半覆盖像素，拒绝非有限响应和不安全候选截断；共享内核仍限制1024姿态/1亿响应像素，门面最多100目标。
- Core新增TemplateSearchSampling，显式控制角度/缩放步长；新门面不暗中做连续精化，不承诺工业节拍。搜索域限定锚点而非仅显示裁剪框，在响应图上先排除非法位置再做候选抑制。非中心锚点和旧内核几何中心在Tools内换算，不改Core几何数学。
- 两个解决方案及VisionLayout登记共享项目；OpenCV精确依赖守卫只额外允许同层共享Tools，新增守卫确认共享项目无SDK/UI/FlowNode引用。未改MachineApp/FlowNodes源码，不新增测试工程或包，不改现有SDK版本。

### 验证与未完成范围

- 修改前754完整逻辑通过；算法纯抽取后40项通过；新门面3项实际失败（实现未接通），随后同3项通过。临时未接通工厂已由真实实现替换，没有留下假后端。
- 最终定向47通过/0失败/0跳过：新API、真实OpenCV集成、资源生命周期、共享依赖。真实合成图覆盖四组角度/缩放、非中心锚点、模板掩膜反例、掩膜+旋转+缩放组合和搜索域；fake仅用于不可中断SDK、AI域协议、取消等待、清理失败/重试和跨上下文资源拒绝，不冒充AI推理。
- 完整非Runtime逻辑778通过/0失败/0跳过，39.7839秒正常退出。新增24=11个真实集成案例+12个生命周期案例+1个共享依赖守卫；原模板姿态回归仍通过。
- WPF定向32通过/0失败/0跳过，38.9144秒正常退出（TemplatePoseFlowTests、TemplateGuidanceTests、ConcreteVisionNodeTests、VisionFlowR0Tests），确认原UI/节点消费者未被算法抽取破坏；未跑完整WPF/FlowEditor/FlowNodes独立套件，不套用旧全量数量。
- 共享Tools/OpenCV显式隔离目录增量构建各0警告0错误；Vision.Workbench消费者构建4警告0错误（未修改RoiEditorView nullable）。没有生成MachineApp或启动真实硬件。
- 仍未完成：HALCON/AI新后端、NI可行性/实现、宿主appsettings/DI切换、Controls掩膜编辑接新契约、FlowNode迁移、旧接口删除、真实模型/License/精度/P95/P99验收。不能把本轮成功说成全部后端可互换。

### 本轮命令

根目录串行，均追加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~VisionEngineIntegrationTests|FullyQualifiedName~VisionEngineLifetimeTests|FullyQualifiedName~VisionEngineContractTests|FullyQualifiedName~EngineDependencyTests' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~TemplatePoseFlowTests|FullyQualifiedName~TemplateGuidanceTests|FullyQualifiedName~ConcreteVisionNodeTests|FullyQualifiedName~VisionFlowR0Tests' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/engine-p2-diagnostics"
dotnet build "Software Frame/Vision/Tools/Runtime/Vision.Tools.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
dotnet build "Software Frame/Vision/Tools/OpenCv/Vision.Tools.OpenCv.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
```

新项目首次测试省略--no-restore完成还原。%TEMP%/vision-engine-p2-{baseline,extract,red,green,focused,focused-final,logic,logic-final,wpf-focused,runtime-build,opencv-build,host-build}.log保留过程。代码与原暂存区不自动提交。

## 9. P2第二批：HALCON AI/cpu分类SDK路径

- 新增AI/Halcon/Engine中的后端和直接SDK适配，调用ReadDlModel/SetDlModelParam/ApplyDlModel/ClearDlModel，不借旧HalconAIProvider/IAIModelSession，也不转换旧ToolResult。
- 配置工厂现在接受opencv传统视觉＋可选halcon/cpu AI，先验证所有选择再激活SDK；设备原值传到AI构造器，未知Provider或gpu0等未支持设备不初始化任何后端、不回退CPU。传统视觉halcon仍未接通，不能因AI品牌相同宣称已接入。
- 模型准备实际读取文件SHA256并与定义核对，FileShare.Read租约贯穿SDK读取，普通写入/删除竞争明确失败；这不是恶意路径/符号链接替换防护。准备失败保留原模型，迟到成功候选由共享引擎接管清理。
- 首批只支持单标签classification、单通道0..255模型、原宽高Mono8输入；只转换为HALCON所需real，不猜测缩放/裁剪/归一化。该约定仍须与训练预处理对齐；不支持时明确Unsupported，下一阶段再定义中立预处理配方。
- 内部后端Validate增加已准备资源参数，让尺寸/模型元数据在SDK推理前校验，不另建平行模型注册表。Core业务API不变，不暴露任何SDK对象。
- 原始类别ID转不变文化字符串，检查ID/分数数量、重复、非空及有限[0,1]，按阈值、排序、最大结果数投影；本次无满足项为空NoMatch。
- SDK临时对象逆序清理，聚合原始/清理异常，失败对象保留到关闭重试；模型ClearDlModel成功后才撤销原生所有权。AI项目只增加同层共享Tools引用，仍经Extension使用原固定HALCON版本。
- 未改Controls/FlowNode/Workbench消费者；未修改/构建/验证MachineApp，不改变已有暂存区。没有真实模型、SDK License、CPU Runtime和精度/节拍验收证据。

### 最小验证（按用户要求，不默认全量）

新增4个关键SDK边界fake测试：实际SHA/CPU透传/分类过滤、错误指纹不加载且保留原模型、尺寸/颜色不符不推理、未知设备不激活SDK。未取得本批测试先红证据，不能以编译错误冒充业务红灯。

连同共享依赖、AI依赖、OpenCV真实模板、装配失败清理和取消关闭，共11项通过/0失败/0跳过，1.1847秒正常退出。宿主构建成功，4条未修改RoiEditorView警告/0错误。修正本项目新OpenCV可空掩膜警告和共享依赖测试分析器警告。未运行全量逻辑/WPF/跨模块套件，不将上一批778/32当成本批结果。新HALCON路径只做编译与离线边界验证，**没有运行真实HALCON分类推理**。

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~HalconClassificationBackendTests|FullyQualifiedName~EngineDependencyTests|FullyQualifiedName~AIProviderDependencyTests|FullyQualifiedName~SelectedOpenCv_ExecutesRealTemplateThroughFacade|FullyQualifiedName~FailedSelectedBackend_DoesNotFallback|FullyQualifiedName~ContextClose_WaitsForActualSdkReturn' --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
```

AI项目增加引用后首次定向执行允许还原，最终命令如上。日志%TEMP%/vision-engine-p2-ai-{main,main-final,host-build}.log。P2尚未全完成；后续继续传统HALCON实现、明确预处理/GPU与其他AI路径，再推进P3消费者迁移和P4退役。

## 10. P2第三批：传统HALCON形状匹配SDK路径

- 新Tools/Halcon/Engine直接创建/搜索/清理scaled shape模型，参与统一上下文/模型事务；未引用旧ShapeMatchTool/Session，不把旧结果转发给新接口。
- 工厂现在按(执行域, BackendId)明确选择opencv、halcon视觉或halcon/cpu AI；未知实现仍在任何SDK激活前拒绝。模型Metric仍必须匹配实际语义，HALCON形状匹配不冒充GrayCorrelation。
- Core增加显式NativeAutomatic采样与单独能力位；默认离散采样不变，HALCON拒绝指定网格，OpenCV拒绝未声明的原生模式。没有扩张第二个业务接口。
- 支持Mono8、二值模板域、非中心锚点、旋转/正等比缩放、局部极性忽略和最多100目标。模型原点从实际训练域质心换算；Row/Column按官方affine_trans_pixel示例换成像素中心位置，复用Core数学，不将SDK对象放到Core。
- 本批明确不支持SearchMask（HALCON默认质心域不等于Core锚点域）；保守拒绝跨原生训练线性边界的圆周子域，要求重建覆盖本次区间，不静默裁剪。精化输出越过角度/尺度/最低分数边界时整次Failed不带数据，不先截N后过滤非法项求绿。
- 生命周期同前：等待真实原生任务返回才释放；模型Clear成功才撤销；临时对象和失败候选的清理异常不吞，失败资源保留到关闭重试。真实SDK行为尚未验证，尤其极窄角度/固定尺度、边缘遮挡及模型采样端点需授权环境检验。
- 只增加同层共享Tools引用/friend声明，原HALCON版本及Extension唯一SDK入口不变；未改Controls/FlowNode/Workbench消费者或MachineApp，未自动提交/暂存。

### 主要验证

新增4项SDK边界fake案例：模板掩膜/锚点与顺时针尺度转换、拒绝不支持语义、精化越界不发布、圆周区间与半像素golden（来自HALCON23.11官方示例，不是由当前返回结果反算）。已有OpenCV预算/像素格式案例增加原生模式拒绝断言。未取得本批先红证据，不冒称TDD。

共12关键测试通过/0失败/0跳过，1.4097秒正常退出；宿主构建成功，35警告/0错误（31条旧Core/链接代码及4条旧RoiEditorView，新后端无警告）。没有运行真实HALCON算法、加载授权模型、安装Runtime或解除Skip，没有重复全量逻辑/WPF/跨模块验证。不能把SDK边界fake和编译通过说成形状模型已成功训练/匹配或商业精度达标。

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~HalconBackendTests|FullyQualifiedName~HalconClassificationBackendTests|FullyQualifiedName~EngineDependencyTests|FullyQualifiedName~SelectedOpenCv_ExecutesRealTemplateThroughFacade|FullyQualifiedName~ExplicitSamplingBudgetAndUnsupportedPixels|FullyQualifiedName~FailedSelectedBackend_DoesNotFallback' --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
```

首次新增引用后允许还原；日志%TEMP%/vision-engine-p2-halcon-{main,main-final,host-build}.log。官方依据及详细边界见Tools/Halcon/README.md。下一步为启动配置装配与明确的AI预处理/设备契约，再按P3切换消费者/P4删旧入口；Runtime、精度及节拍仍需授权验收。

## 11. P2第四批：启动配置与可选延迟装配

- 宿主新增VisionEngineConfiguration与随输出/发布复制的vision-engine.json。只读取配置源并形成Core不可变选择，不把配置I/O/DI或模块表塞进Core。严格拒绝未知/重复字段、错误类型、缺失必需项，文件缺失不回退默认；Core继续校验AI/设备组合。
- 共享工厂新增纯ValidateSelection，用当前已接通模块表验证身份，不创建SDK。Controls.RegisterVisionEngine消费选择并给出可复用Prism注册，宿主仅持有返回的IAsyncDisposable关闭所有权；已有IVisionEngine注册不能覆盖。
- 默认opencv、AI禁用；选择冻结于启动，首次解析才触发唯一真实引擎创建。空注册/空关闭不加载SDK。配置正确不等于Runtime/License/模型可用，初始化失败仍由原工厂明确上报且不回退。
- 关闭守卫先拒绝新解析，等待并发同步工厂真正返回；禁止迟到发布，成功创建资源仍释放，失败句柄保留可重试。不持状态锁初始化或释放SDK，不同步等待异步关闭，不建立第二个业务Session/Provider。
- App沿原WorkbenchLifetime在旧页面/节点释放后等待新引擎关闭；启动异常也保留关闭入口。新注册本身不注册页面、不创建上下文，不要求其他宿主引用Workbench。没有新建配置框架/包/测试工程。
- **这不是P3迁移完成：旧节点/工具箱/编辑会话仍走原DTO与会话，不消费新引擎，新的配置不限制旧链选择。** 不在旧Session里转发形成兼容壳，后续必须整体迁移并按P4删除旧入口，不能宣称整宿主已经唯一后端。
- MachineApp与FlowNodes宿主未改、未构建/验证；原458个暂存项未自动变动。

### 最小验证

在现有WPF测试工程新增4项：配置冻结/严格字段/缺失文件、注册延迟/唯一/重复与未知身份、空关闭不初始化、关闭与真实同步工厂返回竞争不发布迟到实例。SDK端用ProbeEngine，不启动硬件；没有取得先红证据，首次仅修正Prism8.1.97容器包装器不是IDisposable的测试编译问题，不称业务红灯。

4项通过/0失败/0跳过，2.9671秒；再单独执行实际Prism AppBoot启动/正常关闭1项，通过/0失败/0跳过，5.8634秒，确认配置随ProjectReference复制且只注册不解析SDK。实际启动仍为旧链，不能当新算法执行验证。显式宿主增量构建0警告0错误，重编出现4条未修改RoiEditorView警告；测试编译仍有原有非本批文件警告。没有重复完整逻辑/WPF/跨模块/Native验证，没有加载HALCON Runtime或授权模型。

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~VisionEngineCompositionTests' --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~AppBoot_ShowsIndependentVisionMainWindow_WithoutStartingHardware' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/engine-composition-diagnostics" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
```

首次新引用测试允许还原；日志%TEMP%/vision-engine-p2-composition-{main,boot,host-build}.log。宿主配置说明见Vision.Workbench/README，复用与关闭约定见Vision.Controls/README。下一步按P3统一中立配方与工具上下文，迁移Controls/FlowNode并退役旧链；不将仅注册新入口视为迁移已完成。

## 12. P3前置：补齐无模型区间阈值

检查真实消费者后确认：新入口只有模板/分类，直接全量切换将丢失原工具箱其他功能。因此先补中立基础处理，不新增旧会话到新接口的转发壳，不把原28项工具箱缩减为2项，也不声称节点/编辑器已迁移。

- Core新增ThresholdRangeOperation、BinaryImageOutput：全图整数灰度闭区间，两端包含，Mono8/Mono16，输出0/255 Mono8。请求无模型；不加假模型、厂商字段、通用object参数字典或Core算法。当前不包含Otsu、反向、ROI或颜色转换。
- OpenCV直接InRange，HALCON直接Threshold/RegionToBin；均打包输入stride，Mono16不降到8位后再比较。输入不变，输出保持尺寸/坐标空间、新帧身份，并记录源FrameId；错误来源、空间、尺寸拒绝发布。全黑输出Success，不混成NoMatch。
- 共享引擎对无模型操作只传null资源，不借用/撤销当前模型；原模板引用仍可继续使用。增加二值输出的统一校验，没有第二个执行入口。
- HALCON内部SDK协议/类由Shape改名为Vision以承载模型与处理，8处真实绑定均核对，无反射/序列化引用，旧工厂反射目标HalconBackend不变；未留无用别名。旧形状/采样案例继续通过。后端项目、框架和包版本不变。
- 未改UI、节点或旧DTO/算法行为，未构建/验证MachineApp；真实HALCON阈值仍待授权，fake只验证后端路由/输出归属，不能当Native结果。

### 主要验证

真实红灯3项（Mono8、Mono16、当前模板保留）：代码可编译，但实际返回Unsupported而不是Success。接入后同案例转绿，再补错误范围/颜色及HALCON边界。最终10项通过/0失败/0跳过，2.4685秒正常退出（5个新案例+4个原HALCON形状边界+1个真实OpenCV模板）。宿主构建4条旧RoiEditorView警告/0错误，新文件无警告。未重复全量逻辑/WPF，不运行真实HALCON。不据此宣称P3迁移或功能等价已完成。

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~ThresholdRangeEngineTests|FullyQualifiedName~HalconBackendTests|FullyQualifiedName~SelectedOpenCv_ExecutesRealTemplateThroughFacade' --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
```

日志%TEMP%/vision-engine-threshold-{red,main,host-build}.log。HALCON23.11依据为threshold.html（闭区间、uint2）及region_to_bin.html（byte图）；代码与文档保留于Tools/Halcon。后续继续补原消费者实际需要的统计/处理/测量等契约和所选后端实现，再统一切换Controls/FlowNode与配方文件，按P4删旧链，不长期维护两套生产入口。

## 13. 用户授权的HALCON原生合成图验证

用户提供HDevelop路径后，只读确认HALCONROOT指向HALCON-23.11-Progress、HALCONARCH=x64-win64、PATH含本地native目录，托管/原生文件版本23.11.0.0，与项目固定NuGet23110.0.0匹配。询问仅合成图原生测试后用户明确继续，本轮授权不扩张到AI模型、真实设备或整个RequiresRuntime套件。

新增现有Vision.Tests中的HalconEngineSyntheticRuntimeTests，标RequiresRuntime，不解除原有Skip。通过生产VisionEngineFactory选择halcon，输入只来自内存整数栅格，读取加载模块路径证实实际使用C:\\Program Files\\MVTec\\HALCON-23.11-Progress\\bin\\x64-win64\\halcon.DLL，GetSystem版本23.11，无替身或OpenCV代跑。

最终5项通过/0失败/0跳过，2.3156秒正常退出：
- Mono8和Mono16闭区间边界/stride/输入不变/坐标保持，实际Threshold/RegionToBin执行成功。
- 不对称二值参与域和非中心锚点，分别0度/1倍、90度/1倍、90度/2倍；在Core请求中保持完整0..360度、0.8..2.2尺度搜索。实际CreateScaledShapeModel/SetShapeModelOrigin/FindScaledShapeModel/ClearShapeModel成功。
- 场景用独立整数90度旋转和像素块复制构造，不用SDK生成golden；锚点期望依次(102,87)、(136,82)、(182.5,94.5)。最终锚点依次(101.9900,87.0026)、(135.9829,82.0061)、(182.4518,94.5135)，实际角度-0.001707/90.002318/89.975294度，尺度1.000038/0.999577/1.998535。数值为单次合成图记录，不是统计精度或节拍验收。
- 正常等待上下文/引擎释放且进程退出，没有改生产算法、安装SDK、修改环境变量、打开HDevelop、接设备、加载AI模型或触碰MachineApp。没有为通过而放宽断言。尚未覆盖任意角度/非整数缩放、SearchMask、低对比/遮挡、真实工件、泄漏压力、P95/P99或AI License/推理。

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~HalconEngineSyntheticRuntimeTests' --logger 'console;verbosity=detailed' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
```

日志%TEMP%/vision-halcon-synthetic-native-{first,final}.log。first为原4项通过，final增加旋转+2倍缩放共5项通过。只新增测试/证据文档，没有重复宿主构建或全量回归；原暂存区不变。后续仍按迁移计划施工，不能因合成图通过取消未验收能力的拒绝边界。
