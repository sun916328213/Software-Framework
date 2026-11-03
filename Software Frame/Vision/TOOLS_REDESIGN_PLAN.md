# Tools 重建计划

## 1. 目标与执行范围

本计划承接当前干净版 Vision.Core，按新契约重建 Tools，不按旧接口迁移兼容。
这是允许破坏旧 API 的设计调整和功能建设，不是行为完全不变的机械重构。

当前 T0、T1及T2两批基础交付已完成：OpenCV可执行定位→跟随ROI→Blob→数量/面积/存在性判定，并提供单独轮廓口径的面积/周长/圆度及筛选。HALCON仅声明矩阵中的较小范围，未接Blob，不要求厂商强行等同。更多厂商/特征扩展仍按实际需求推进；T3基础像素链已交付：定位跟随卡尺直线/边缘对/圆，以及公共无图几何/公差。T4已交付均匀比例毫米长度链及二维仿射点对标定/双向世界点/完整直线映射、毫米点距/垂距公差链；一般跨派生图映射、完整边界等厚判定/非等比圆尺寸、独立边缘间距仍待后续。T5单/多标准QR→精确文本/返回集成功解码数量判定、固定域RGB/HSV8及显式对齐双图差分→二值Blob/计数面积判定已交付，其余T5仍待后续；T6已补分类发布边界及无SDK快照显式规则判定，但真实模型推理与新预处理未验。第32节经用户明确确认开始FlowNode破坏性迁移，当前交付四节点模型链；Controls/Workbench及其余旧消费者仍未迁移。

**当前验证状态（第29～31节）**：历史T5记录中的HALCON/AI `NETSDK1004`已通过同一artifacts路径的现有依赖定向还原解除，无工程/包版本/授权源码修改。HALCON、HALCON AI、OpenCV现均构建成功；传统HALCON依赖保留106警告。本次新构建/传递DLL的96项定向回归通过，不代表新HALCON原生、真实AI或宿主验收。历史各批失败日志不覆盖，依赖恢复、分类发布守卫及快照显式规则判定证据见第29～31节。

### 必须遵守

- 业务执行入口仍然只有 IVisionEngine，不再公开 ITool、Provider、Session 等平行业务入口。
- 不恢复 Core Legacy，不增加旧类型别名、转发壳、兼容目录或旧结果转换链。
- 旧算法有价值的部分可以重写或提取复用，但不能继续依赖旧图像、参数、结果和编辑会话。
- 一个传统视觉实现，加可选的一个 AI 实现；选择冻结，未选 SDK 不初始化，失败不切厂商、不降级设备。
- 不要求 OpenCV 与 HALCON 实现全部相同功能；支持范围必须真实声明，不能把近似算法包装成等价能力。
- 不以工具数量、节点数量或测试数量作为完成标准，优先完成可组合的工业检测链路。
- 保持现有框架和包版本，不借本轮升级 .NET、OpenCvSharp、HALCON 或 UI 依赖。
- 中文备注解释用途、单位、坐标、限制和所有权；独立语句分行，控制块完整，不保留历史阶段流水账。

### 生产代码范围

| 范围 | 处理原则 |
|---|---|
| Tools/Runtime | 统一执行、内部装配、上下文、模型和释放事务的主要实现 |
| Tools/OpenCv、Tools/Halcon | 对应 SDK 的算法与原生资源实现 |
| Tools/AI | 清退旧 AIToolBase/Provider 转发链，不再保留只转发请求的单独业务层 |
| AI/Halcon/Engine 等实际 AI 适配 | 逻辑上同属算法实现层；随所选模块清理其旧 Provider 依赖 |
| Vision.Core | 既有语义不倒退；只在具体功能确需时增加强类型契约，不预建完整空壳工具集 |
| 现有 Vision.Tests | 仅调整本次实现真正需要的测试和引用，不新增测试工程或框架 |
| Controls、FlowNode、Workbench | 第31节之前不改生产实现；第32节经用户确认仅扩至FlowNode四节点；Controls/Workbench仍不改，不为旧消费者恢复API |
| MachineApp | 不修改、不构建、不验证 |

旧消费者在重建期间可能无法编译，这是已接受的接口断点，不是恢复旧壳的理由。
工程删除涉及的 Vision 解决方案登记可同步清理；不得为了求整个仓库编译而扩散修改宿主。

## 2. T0 实施前盘点（历史基线，不是当前状态）

本次以实际源码和项目文件为依据；知识图谱对新增执行文件缺少新鲜记录，未将旧图谱当作完整现状。

- Tools 当前有 Runtime、OpenCv、Halcon、AI 四个项目；AI 的实际 SDK 适配另在 AI 目录。
- Runtime 已存在 VisionEngine、VisionEngineFactory 和内部 IVisionBackend，不需要再建第二套执行管理器。
- 新 Core 已定义模板匹配、AI 分类、区间阈值、灰度统计四种操作，但不能因此宣称整套传统工具已接通。
- OpenCvBackend 仍经 AlgorithmImage 使用已删除的 VisionImage，并使用旧 TemplateMatchParameters。
- TemplateMatchSearch 仍依赖旧参数、结果和 ValidationResult；它不是已经完全脱离旧 API 的纯内核。
- 旧 ToolBase、ToolResult、Session、属性编辑特性和 Provider 依赖仍散布在厂商工具中。
- HALCON 的新 Engine 路径已有直接 SDK 实现，但同项目仍含旧工具和会话，必须按项目整体清理。
- Runtime README 中的旧逻辑、宿主和原生测试数量是历史记录，不作为本轮重建后的通过证据。
- 本次计划制定未运行构建、测试、宿主或原生 SDK。

## 3. 主流功能对齐：对齐任务与数据链，不复制产品外壳

参考 MERLIC、VisionPro、KEYENCE 的官方工具资料。它们共同强调定位、区域、测量、读码、检查和结果判定，以及图像和几何结果之间的组合。

功能对齐不表示算法等同、授权等同、性能等同或工业精度等同；也不表示要复制商业软件的全部功能。

| 功能组 | 目标能力和关键输出 | 优先阶段 |
|---|---|---|
| 图像输入与预处理 | 文件离线输入、显式格式转换、裁剪、滤波、灰度变换、阈值、形态学；输出图像和坐标关系 | T1 |
| 定位与位置补正 | 灰度相关/形状匹配、模板掩膜、锚点、旋转缩放、多目标；定位结果驱动后续 ROI | T0、T2 |
| Blob 与区域检查 | 连通域、面积、质心、外接框、轮廓及孔洞、特征筛选、排序和计数 | T2 |
| 边缘与测量 | 卡尺边缘、直线、圆、边缘对宽度、距离和角度；提供参与点及拟合质量 | T3 |
| 几何与判定 | 点线关系、交点、距离、角度、存在性、数量和公差 OK/NG | T3 |
| 标定与坐标 | 像素比例、二维仿射点对标定、像素/世界坐标转换；明确单位及残差 | T4 |
| 缺陷与颜色 | 背景差异/污点、颜色分割、区域面积及位置判定；不是给阈值工具换名 | T5 |
| 编码与字符 | 一维码、二维码、后续 OCR/OCV；字符串、位置、类型和可用质量信息 | T5；OCR/OCV 待模型与授权确认 |
| AI 推理 | 先完成可用分类，再按真实模型需要增加检测、分割、异常检测 | T6 |
| 执行诊断 | 本次执行状态、稳定错误码、耗时口径、输入来源、模型修订；失败不得展示旧结果 | T0 起贯穿 |

### 第一交付目标：两个离线闭环

1. 输入图像 → 定位 → ROI 跟随 → 阈值/Blob → 面积或数量判定。
2. 输入图像 → 定位 → 卡尺直线/圆 → 尺寸 → 已验证标定换算 → 公差判定。

Tools 阶段先以强类型操作组合证明数据链可用。可视化参数编辑、节点端口、画布绑定、保存重开和批量回放界面由后续层实现，不在 Tools 里复制 FlowEditor。

读码与 AI 是后续独立闭环，不作为上述基础闭环迟迟无法交付的理由。

## 4. 目标结构与依赖

保留现有主要项目，不按每个工具新增项目，不预建插件市场或通用算法框架。

```text
Tools/
  Runtime/                  # Vision.Tools：唯一引擎实现与内部协作
    Execution/              # 校验、执行、状态和发布
    Resources/              # 上下文、当前模型、释放事务；按实际体量拆分
    Common/                 # 真正无 SDK 的公共算法，不是后备厂商
    VisionEngineFactory.cs
  OpenCv/
    Engine/                 # 内部后端与操作分派
    Imaging/                # VisionFrame/掩膜与 Mat 的边界转换
    Processing/
    Matching/
    Inspection/
    Measurement/
  Halcon/
    Engine/
    Imaging/                # HImage、HRegion 等资源适配
    Processing/
    Matching/
    Inspection/
    Measurement/
    Identification/
    Calibration/
AI/
  Halcon/Engine/            # 实际选中的 AI SDK 实现
```

这是职责示意，不要求建立空目录；只有存在实际实现时才建立对应文件。

- Runtime 只依赖 Core，不直接依赖视觉 SDK、WPF、Prism、Controls 或 FlowNode。
- 厂商模块依赖 Core、Runtime 及规定的 SDK 入口，不互相引用或借用对方的算法补缺。
- HALCON 暂沿用仓库现有 Extension SDK 引用入口，不在本轮擅自新增另一份 HALCON 包或升级版本。
- 模块只按明确身份装配，不扫描全部程序集，不接受任意插件路径。
- 内部适配协议可以存在，但不变成第二个公开执行接口；无实际复用价值的转发层直接删除。
- 纯几何和公差算法放在真正的公共实现中，不复制到两个厂商目录，也不伪装成第三个可选厂商。
- Tools/AI 中可复用的实际逻辑归入相应推理实现。旧转发链清退后删除空项目及登记，不保留空兼容工程。
- Invi、Aiditron 等旧 Provider 不因历史存在而自动列入新能力表；有具体 SDK、模型和需求后再接入。

## 5. 所有工具共同遵守的契约

### 5.1 请求、能力与结果

- 参数为强类型、不可变执行快照；不携带 UI 绑定状态、属性编辑特性或任意 object 参数字典。
- 结果不暴露 Mat、HObject、HTuple、SDK 模型句柄或必须依靠编辑会话才能读取的对象。
- 对外集合、区域和轮廓为独立只读结果；原生资源失效后，已发布结果仍可读取。
- 能力按操作、格式、方法、ROI、采样模式及资源限制如实说明；具体组合仍由 ValidateAsync 核对。
- 不支持时在 SDK 工作前拒绝；不能声称支持某功能，再悄悄忽略参数或替换算法。
- 检测为 NG 是业务结果，不等于执行 Failed；空匹配和找不到边缘也不能携带上次成功值。
- 统一记录本次诊断和耗时，说明是否含校验/排队/模型准备，不把普通耗时记录包装成实时性保证。

### 5.2 图像、ROI 与坐标

- 明确 Mono8/Mono16/颜色格式、通道顺序、stride 和数值域，不隐式转灰、降位深或归一化。
- ROI 跟随定位应使用明确变换，不修改原始 ROI；请求期间必须冻结参数和参与域。
- 裁剪产生独立帧及坐标空间，并提供局部到来源的明确映射；不凭相同 FrameId 或坐标数值推断来源等价。
- 模板参与域、锚点搜索域和普通处理 ROI 分开定义，不把三者当同一种 SDK 掩膜。
- Core 正角度为图像屏幕顺时针；HALCON Row/Column、角度和半像素约定在适配边界转换。
- 像素、毫米等单位显式表达；像素比例、仿射标定、镜头畸变校正不是同一能力。
- 不同空间的数据不能直接求距离/角度或做公差比较；缺映射时明确拒绝。

### 5.3 模型、取消与关闭

| 对象 | 创建与拥有 | 借用 | 结束方式 |
|---|---|---|---|
| 引擎 | 外层装配创建并拥有 | 工具调用方 | DisposeAsync 等待实际调用结束 |
| 上下文 | 工具调用方创建并拥有 | 编辑器或本次调用 | ReleaseContextAsync，可重新等待关闭 |
| 当前模型 | 后端创建，上下文拥有 | 本次执行 | 成功替换后清理旧模型，或关闭时释放 |
| SDK 图像/临时区域 | 算法边界创建并拥有 | 当前 SDK 调用 | 调用真实结束后释放 |
| Core 图像与结果 | 托管快照 | 多个只读消费者 | 不依赖原生会话存活 |

- 每上下文一个当前模型，成功原子替换，准备失败保原模型；不为未来多模型场景预建槽位系统。
- 同上下文拒绝重入，不同上下文隔离；禁止共享可变原生模型或以相同 GUID 伪造有效引用。
- 取消等待不等于 SDK 停止，不能提前销毁模型/图像，不能发布迟到结果。
- 部分准备失败、清理失败、关闭重试和原错误保留继续沿用现有正确约定，不为减少测试删掉安全机制。
- ValueTask 每次返回值只消费一次，共享等待只转换一次 Task。
- 不用 Task.Run、线程中止或丢弃任务宣称已实现硬超时；进程隔离不属于本轮基础工具重建。

## 6. 分阶段实施

### T0：清退旧入口，恢复干净实现基线

- [x] 列出要保留的算法行为和要删除的旧契约，分别处理，避免删接口时无意丢掉有效算法。
- [x] 清退厂商 ToolBase、Session、旧 Provider、旧图像/结果及属性编辑依赖；不搬进新的 Legacy 目录。
- [x] OpenCV 图像边界改为直接接收 VisionFrame；模板内核改用明确参数和 Mat/中立数据，不借旧 Tool 类承载参数。
- [x] 保留并整理现有唯一 VisionEngine，按职责拆分过大的实现，不另起管理器。
- [x] 模型路由使用明确类型/种类映射，未知模型拒绝；取消“非模板就当 AI”的隐式兜底，为后续模型扩展留清晰边界。
- [x] 整理工厂、内部协议和结果发布校验，保持冻结选择、无回退、独立上下文与资源账本。
- [x] 恢复当前四项操作：模板匹配、区间阈值、灰度统计、HALCON CPU 分类；恢复不等于扩展 AI 支持范围。
- [x] 清理实际退役项目的登记和测试引用，重写 Tools README，删除过时的完成声明。

完成标准：Runtime 与实际交付的厂商模块能按新 Core 独立构建；不再依赖已删除的 Core 类型；支持能力可列明。外层宿主本阶段不要求编译。

### T1：图像处理与可组合的 ROI/来源契约

- [x] OpenCV接通绝对路径文件主图解码，保留实际位深/颜色，并报告路径、编码内容指纹和失败；未接HALCON文件输入，相机生命周期仍由Device层负责。
- [x] 完成 Mono8/Mono16 整数矩形裁剪，保留位深并创建独立来源映射。
- [x] OpenCV接通显式颜色/位深存储转换及灰度gain/offset线性变换；Mono16到Mono8不猜范围，HALCON暂不声明这些能力。
- [x] OpenCV完成均值、高斯、中值基础滤波，明确核尺寸、边界和位深限制；非线性灰度变换按需另加，不预建空模式。
- [x] OpenCV完成原位深Otsu/反向、Mono8局部均值/高斯阈值和0/255二值腐蚀/膨胀/开/闭；参数、范围和输出语义明确，HALCON未接通项不宣称支持。
- [x] 补处理 ROI 和裁剪来源映射；阈值域外清零、统计域外忽略，空参与域和越界裁剪拒绝。
- [x] 第一批只补实际需要的 Core 请求、结果和映射值，未将文件存储、解码器或 SDK 放回 Core。
- [x] 拆分无图强类型请求基类和必须带图像的请求基类，不再要求 1×1 假帧；T2 再补具体数量/面积判定操作和公共执行实现。

完成标准：图像处理结果能继续作为下一操作输入，ROI 和裁剪坐标可追踪，不丢位深、不猜来源；下一阶段的判定不被迫携带假图像。

### T2：定位、位置补正与 Blob，交付第一闭环

- [x] 保留既有模板锚点、掩膜、旋转、等比缩放、多目标及模型修订；本轮重跑相关OpenCV姿态/掩膜/目标回归，离散和原生自动仍为不同能力。
- [ ] HALCON 自定义锚点搜索域正确适配后才声明支持；本轮维持Unsupported，不拿默认质心域替代。
- [x] VisionRegionBinding定义模板局部ROI→实际图像变换输入，拒绝跨帧、跨空间、重造同GUID分支及失效修订复用；执行前/发布前均核验。
- [x] OpenCV Blob接通显式连通性、保孔/填孔、面积/质心/框/轮廓、All/Any特征范围、排序及完整计数；HALCON未接通。
- [x] 已说明像素面积、轮廓几何、Extent、模板IoU及得分口径；第二批接通轮廓净面积/含孔洞周长/圆度，退化圆度为null，不伪造零值或强求跨厂商数值等同。
- [x] BlobJudgmentOperation提供无图ForPresence/ForCount/ForArea，经公共实现实际执行；面积明确选中区域，NG不当作SDK失败。
- [x] 真实OpenCV离线合成场景完成“定位→跟随ROI→阈值/Blob→数量/面积判定”，不是测试手写if。

完成标准：第一闭环在已声明的所选实现上可用；不要求所有厂商同时完成全部扩展项。

### T3：边缘、几何与公差，完成像素测量链

- [x] OpenCV固定路径卡尺支持扫描方向、极性、原灰度阈值、切向平均/高斯平滑及第一/最后/最强选边；参数真实影响结果。
- [x] 卡尺直线与圆已提供完整样本、参与/剔除、正交/径向残差，圆另有覆盖角守卫；不把离散采样估计宣称工业亚像素精度。
- [x] 三类卡尺已显式接入本次帧/空间、原定位对象与原输入快照及模型修订守卫；真实0°/90°像素闭环通过，不等于T4毫米闭环完成。
- [x] 同一扫描剖面内的相邻异极性峰配对已交付条带宽度、逐卡尺诊断及统计；不把两条检测线距离冒充测宽，独立边缘间距仍按后续需求增加。
- [x] 公共几何已接通点点/无限直线点距、明确方向角度及交点，测量闭区间公差输出OK/NG；像素/角度单位显式匹配，不冒充边缘对宽度。
- [x] 几何与公差复用无图强类型请求，统一由Runtime公共算法执行，不进入厂商SDK或第二执行接口。
- [x] 几何输入显式指定；Blob质心须选择一基Index，卡尺直线须本次有效，不默认首项或沿用NoMatch旧线。

完成标准：定位、ROI、边缘和几何结果能形成像素单位的测量与判定链；退化几何和无有效边缘有明确结果。

### T4：标定、单位与尺寸闭环

- [x] 已交付均匀像素比例，以及二维点对仿射的像素→毫米世界正向/显式逆向映射、适用空间/单位、逐点/RMS/最大拟合残差；不把拟合残差当独立精度验收。
- [x] 均匀比例拒绝短/退化参考、无效比例、错空间/单位及不可表示数值；二维仿射诊断共线/近共线、奇异/病态映射和超残差，NoMatch不发布旧矩阵。
- [x] 在明确均匀比例假设下完成“定位→跟随测宽→比例换算→毫米长度→公差”离线链，不等于一般仿射/世界坐标闭环或真实工件验收。
- [ ] 镜头畸变、透视校正、标定板、手眼标定分别列为后续能力，不以 N 点仿射宣称全部标定完成。

完成标准：有明确标定依据的毫米尺寸闭环可用，不把像素数简单改单位标签。

### T5：读码、颜色与基础缺陷检查

- [ ] 单/多标准QR已明确Mono8、整数矩形窗、独立文本/四角/读取状态、返回集候选与成功解码计数及1..32预算，空读码与SDK异常分开；一维码/其他码制仍待后续，不宣称场景无漏码或全码制。
- [x] 本批QR不虚构质量等级或原始字节/ECI/GS1，不承诺其他部署/无授权可运行；未支持厂商不借用OpenCV回退。
- [x] 首批RGB编码值分割明确R/G/B各0..255闭区间、Rgb24/Bgr24存储顺序、固定ROI及二值Blob/判定；不是HSV/Lab/色差或颜色质量认证。
- [x] HSV8分割明确H0..179/SV0..255量化编码、闭区间/跨零及S8=0显式策略，固定ROI输出二值候选接Blob/判定；不是连续HSV/色度精度承诺。
- [ ] Lab/感知色差及更多缺陷仍待后续；已交付编码值分割和显式对齐灰度差分，不冒称全部颜色/缺陷或真实工件质量验收。
- [x] 显式Current/Reference双图、同尺寸/格式/空间与来源核验，绝对/变亮/变暗原位深正闭区间及固定ROI，二值候选接明确Blob/数量面积判定；不偷读全局背景或自动配准。
- [ ] OCR/OCV 在模型、字符集、许可证与真实样本明确后单列实现，不先加只返回 Unsupported 的空工具。

完成标准：每个交付项有真实算法和可消费结果，不把工具名出现在目录里当成功能完成。

### T6：AI 按真实工件与模型逐项接通

- [ ] 现有HALCON CPU分类准备/推理仍需真实模型与已确认输入配方验收，准备成功与真正推理成功分开报告。
- [x] 统一发布层拒绝分类超量/阈值外/重复ID/错误排序，不重排截断坏结果，保持合法同分、阈值边界与空NoMatch语义；仅属发布边界修复，不冒称真实推理交付。
- [x] 已完成分类快照经显式零基预测位置、期望类别ID及最低分数作无SDK OK/NG；不猜业务类别、不改选其他预测，缺项NG Success，不替代真实模型验收。
- [ ] 预处理作为明确模型契约：输入尺寸、通道、范围、缩放/填充和归一化可核对，不猜测训练配方。
- [ ] 随真实需求增加检测、分割和异常检测契约，包含类别、分数、框/掩膜及到原图的映射。
- [ ] GPU、其他厂商和设备仅在实际支持、授权和模型准备好后接入；不静默转 CPU 或传统算法。
- [ ] 训练平台、数据集管理和商业产品全部深度学习功能不列为本轮必交付内容。

完成标准：只能对已验证的模型、预处理和设备声明支持；fake 不能替代真实模型验收。

## 7. 验证策略：少而关键，不按工具机械复制测试

### 必需覆盖

| 风险 | 最小充分证据 |
|---|---|
| 新操作/算法 | 一个代表性正常输入，加真正影响决策的空结果或非法输入；相近变体参数化 |
| 图像和坐标 | 在共享转换边界覆盖位深、padding、ROI/裁剪映射，不在每个工具重复同一批尺寸测试 |
| 统一执行器 | 集中保留准备失败保原模型、跨上下文拒绝、取消迟到结果、关闭等待和清理重试 |
| 主流任务链 | 第一、第二离线闭环各一个代表场景，核验数值与来源，不铺完整工具排列组合 |
| 原生 SDK | 仅在授权范围内做必要冒烟；SDK 返回行为不能用 fake 的一致返回代替 |
| 精度与节拍 | 在有工件、镜头和模型依据后单独记录；不把合成图或单元测试称为工业验收 |

### 明确不做

- 不为简单 DTO、属性赋值、纯转发、每条注释和每个错误文案新增测试。
- 不默认重复全量逻辑、WPF、宿主和跨模块回归，不执行 MachineApp。
- 不把旧接口签名、旧类名和旧包装器继续存在作为兼容测试目标。
- 不为了省时删除仍有效的算法/安全测试；只随明确退役功能清退失去意义的旧 API 测试，并记录替代的业务覆盖。
- 不通过排除仍应交付的生产源码、恢复旧壳或跳过失败断言来获得绿色结果。
- 不要求每个后端对同一几何/数值辅助函数再复制一份测试。
- 不新增测试工程、测试框架、临时兼容测试层或每工具一套 mock 基类。

### 每批执行方式

1. 先判断本批影响；缺陷修复取得针对症状的真实失败证据，新功能先明确预期行为。
2. 跑直接相关的现有测试和本批必要补充，不重复已经覆盖且未受影响的全模块测试。
3. 对受影响交付项目做必要构建，不为了阶段进度牵动未迁移宿主。
4. 分开报告编译、逻辑、fake、实际 SDK、真实工件证据；失败构建不是算法红灯。
5. 每阶段关闭时核对能力矩阵和对应离线闭环，不按固定测试数量验收。

## 8. 实施顺序与阶段完成定义

执行顺序：T0 → T1 → T2 → T3 → T4；T5、T6 根据实际项目需求排期，不自动扩成一次性交付。

T0～T2基础交付及T3前两批已完成，覆盖图像处理、定位/Blob闭环、卡尺直线及公共几何/公差。T3基础像素链已完成，后续进入T4标定，独立边缘间距按实际需求追加；不因工具扩展自动迁移UI或宿主。

后续每批必须交付：

- 实际可运行的操作及参数/结果说明。
- 如实更新的后端支持、格式、限制和未完成项。
- 最小充分的关键验证与必要构建结果。
- 与下一阶段组合时需要的数据来源和所有权约定。

Tools 完成不等于五层接入完成。后续 Controls 只负责编辑展示，FlowNode 只负责端口/绑定/文档，Workbench 只负责配置与生命周期装配；不能将这些实现再塞回 Tools。

## 9. 官方功能参考

以下资料仅用于核对功能分类和工作流，不作为升级 SDK、复制商业算法或声称产品等价的依据。

1. [MERLIC Tool Reference](https://www.mvtec.com/doc/merlic/26.10/manual/en-us/Content/Tool_reference/tool_reference.html)：采集、标定、定位、预处理、处理、后处理和评估；统一工具状态与本次处理时间。
2. [MERLIC Measuring](https://www.mvtec.com/doc/merlic/26.10/manual/en-us/Content/Tool_reference/Processing/Measuring/measuring.html)：圆、线段、边缘对、平行边和间距等测量任务。
3. [Cognex VisionPro 10：2D Tools](https://docs.cognex.com/vpromx_1000/web/EN/visionpro/Content/Topics/release-information/2D-tools-in-this-release.htm)：Blob、Caliper、Find/Fit、Fixture、标定、图像处理、几何和识别工具；该版本列表不代表所有当前版本功能。
4. [KEYENCE CV-X Inspection Tools](https://www.keyence.com/support/user/vision/cvx-training_inspectiontools.jsp)：面积、颜色、定位、Blob、边缘、宽度/间距、读码、OCR、尺寸几何与位置补正。
5. [HALCON 23.11 find_scaled_shape_model](https://www.mvtec.com/doc/halcon/2311/en/find_scaled_shape_model.html)：原生模型搜索域、坐标、得分及精化边界，提醒适配实现不能直接假定与中立契约等价。

## 10. T0 实施记录

### 已交付

- 清退旧 Tool/Session/Provider 及相关旧图像、结果、编辑属性依赖；删除 Tools/AI、AI/Invi、AI/Aiditron 的退役项目和两个解决方案中的登记，不保留兼容壳。
- OpenCV 搜索直接使用 Mat 和 Core 请求/结果；模板及处理图像统一经过复制有效行的输入边界，保留掩膜、锚点、姿态、多目标和预算语义。
- 唯一 VisionEngine 按上下文创建、模型事务、执行发布和关闭事务拆分；未知模型定义必须在任何后端准备前被拒绝。
- 当前四项操作保持真实支持范围；后续 Blob、测量、标定、读码和 AI 扩展仍未实现，不将旧目录删除当作功能已经恢复。
- 更新交付项目说明、测试项目引用和 VisionLayout。有效旧算法测试没有为省时批量删除，完整旧测试工程与外层消费者未完成新契约迁移。
- 原工作树源码已保存到忽略目录 `Tests/obj/t0-source-before-retirement.zip`，用于后续需求/算法对照，不参与构建或交付。

### 已执行验证

- Runtime、OpenCV、传统 HALCON、HALCON AI 四个项目最终独立构建均为0警告、0错误；框架及包版本未升级。
- 模型定义路由：3项真实断言红灯，修正后通过。
- Engine 定向：Passed: 42, Failed: 0, Skipped: 0。复用现有 xUnit/VSTest，以.NET9参考程序集隔离编译实际 Engine 测试源，不创建测试工程或生产源码排除规则。
- 最后消除 OpenCV 可空掩膜转换警告后，额外仅复核无掩膜、掩膜和准备失败保原模型三个相关案例：Passed: 3, Failed: 0, Skipped: 0。
- IDE0011控制块、空白格式及Git差异检查通过。暂存区仍为原有458项，未自动暂存、提交或回滚。

工作目录为仓库根。模块构建使用 `dotnet build` 指向对应交付项目，并带 `--no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`。

本机定向执行记录来自临时脚本 `%TEMP%/verify-vision-tools-t0.ps1 -Phase green`，经现有 VSTest 执行 Engine 测试；不是完整旧 Vision.Tests 工程的编译通过证据。直接编译测试时的 xUnit 参考程序集统一 CS1701 警告已在日志保留，不与交付模块的0警告混为一谈。

日志：`%TEMP%/vision-tools-t0-baseline.log`、`vision-tools-t0-router-red.log`、`vision-tools-t0-*-final-build.log`、`vision-tools-t0-final-tests.log`、`vision-tools-t0-final-template-check.log`。

### 未执行

未运行宿主、WPF、FlowNode、MachineApp、真实HALCON算法或真实AI模型；未连接设备。HALCON传统和AI本轮使用SDK边界fake，真实OpenCV只处理内存合成图。既往原生验证记录不能替代本轮或真实工件验收。

T0 交付时未顺带扩展图像处理功能；用户随后明确开始 T1，记录如下。

## 11. T1 第一批：处理 ROI、裁剪来源及无图请求基类

### 已交付

- VisionOperation<TResult>不再强制图像，数值请求可使用空Image；VisionImageOperation<TResult>强制真实非空输入，现有图像操作全部改用后者。没有为SDK构造假帧，也未提前添加未实现的数值工具。
- ThresholdRangeOperation和GrayStatisticsOperation支持冻结的SearchRegion。阈值在域外固定输出0，统计只计算参与像素；空参与域明确拒绝，合法域内全黑结果仍为Success。
- ProcessingRegion为单独能力位128，不复用模板掩膜或锚点搜索域能力。
- ImageCropOperation/ImageCropOutput实现Mono8/Mono16整数矩形裁剪，拒绝越界，不截断、不缩放、不降位深。输出具有独立帧/空间及到直接输入图像的平移映射，嵌套裁剪显式逐级组合。
- OpenCV直接使用Mat、参与域掩膜及裁剪；HALCON直接使用GenRegionRuns/ReduceDomain、Threshold/Intensity和CropRectangle1，保留原有资源清理事务。
- 引擎发布前核验参与像素数，以及裁剪来源、原点、尺寸和格式；错误来源映射不能作为有效结果发布。

### 验证证据

- 先取得5项真实业务红灯：请求可编译，但ROI和裁剪返回Unsupported；同批另外2项契约断言通过。未把编译失败当成业务红灯。
- 新需求7项通过，随后以最小相关旧路径合并定向：Passed: 21, Failed: 0, Skipped: 0。包含位深、padding、ROI冻结、域外0灰度误命中防护、裁剪像素/嵌套映射和下一操作输入；复核既有阈值/统计、模板、分类边界及操作基类。
- OpenCV、传统HALCON、HALCON AI模块最终必要构建均为0警告、0错误，同时构建实际引用的Core/Runtime；未升级框架或SDK版本。
- 本轮真实执行仅使用OpenCV内存合成图；HALCON裁剪路由/错误映射使用SDK边界fake。HALCON新增ROI/裁剪原生实现已编译，但没有运行新的原生算法或加载AI模型。
- 5项目整批格式处理曾超时，不记为整批验证通过；后续Core空白格式验证通过。Git差异检查通过，暂存区仍为原有458项。

工作目录仍为仓库根。测试复用现有xUnit/VSTest和.NET9参考程序集，临时脚本为 `%TEMP%/verify-vision-tools-t1.ps1 -Phase green`，最终使用定向过滤而非全量；没有新增测试工程、屏蔽生产源码或迁移完整旧测试工程。

日志：`%TEMP%/vision-tools-t1-domain-red.log`、`vision-tools-t1-domain-green.log`、`vision-tools-t1-focused-final.log`、`vision-tools-t1-*-final-build.log`、`vision-tools-t1-core-format-verify.log`。

### T1 仍未完成

第一批交付时，文件解码、格式转换、滤波、灰度变换、其他阈值和形态学仍待后续实现；其中部分已在第二批交付，记录如下。Core无图请求基类仍不能当成T2数值判定已经可执行。

## 12. T1 第二批：离线主图输入与显式像素变换

### 已交付

- ImageFileOperation使用明确绝对路径，由OpenCV读取编码字节快照并执行ImDecode(Unchanged)。保留实际位深/通道，报告来源路径与同一编码内容SHA256；每次创建独立帧/空间，不构造假输入图像。
- 文件操作只交付解码器主图，不按EXIF旋转、不遍历动画/多页、不恢复标定。64MiB为编码读取预算，不是原生解码内存的硬隔离承诺。
- ImageConvertOperation显式指定输出格式，接通已列明的8位颜色/灰度组合和Mono8到Mono16原值扩大；不把Mono16到Mono8当无损转换，也不自动按图像极值归一化。
- GrayLinearOperation明确gain/offset及输出Mono8/Mono16；double计算后限幅，再进行SDK最近整数舍入，避免巨大有限灰度值直接转整数产生错误结果。
- ImageTransformOutput保留直接输入身份、原尺寸和坐标空间，同时创建独立输出帧。统一引擎核验文件来源和转换输出格式/尺寸；损坏、缺失文件失败不携带旧数据。
- OpenCvFrameAdapter扩展为明确格式的逐行输入/输出复制，支持RGB/BGR/BGRA及Float32存储，不读取padding、不改变通道解释、不泄露原生对象。
- 本批新能力仅在OpenCV声明；HALCON未接通文件输入/转换/线性变换，明确Unsupported，不借用其他厂商回退。现有HALCON ROI/裁剪能力未撤销。

### 验证

- 新增8个关键案例，先得到7项真实业务红灯和1项参数契约通过；实现后8项全部通过。
- 最终合并本批8项、首批ROI/裁剪7项及模板/统计关键旧路径2项：Passed: 17, Failed: 0, Skipped: 0，未运行全量。
- PNG fixture按格式规范独立构造，未由被测SDK编码器反算期望值。覆盖16位原值、颜色顺序、透明通道语义、padding、内容指纹、新身份、显式线性映射/限幅、错误文件及错误来源拒绝。
- Core/Runtime/OpenCV与传统HALCON、HALCON AI的必要关联构建均完成，最终0警告、0错误；Core、Runtime、OpenCV的控制块及空白格式验证通过，Git差异检查通过。
- 没有运行HALCON原生算法、AI模型、宿主、UI或设备；没有升级框架/包、恢复旧接口、增加测试工程或修改暂存区。

工作目录为仓库根，定向脚本 `%TEMP%/verify-vision-tools-t1-io.ps1 -Phase green` 使用现有.NET9/xUnit/VSTest。最终过滤仅覆盖上述17项；直接编译测试仍保留已说明的xUnit参考程序集统一CS1701警告，不与交付模块构建警告混为一谈。

日志：`%TEMP%/vision-tools-t1-io-red.log`、`vision-tools-t1-io-green.log`、`vision-tools-t1-io-focused-final.log`、`vision-tools-t1-io-*-final-build.log`。

### 尚未完成

第二批结束时，基础滤波、其他阈值和形态学仍未交付；这些基础项已在第三批完成，记录如下。非线性灰度变换仍按实际需求追加，HALCON不能标为已支持OpenCV专属能力。

## 13. T1 第三批及基础阶段关闭

### 已交付

- ImageFilterOperation提供明确的均值/高斯/中值参数和边界，保持Mono8/Mono16位深；不携带未使用的sigma，也不忽略中值边界/核限制。
- OtsuThresholdOperation在原位深进行全图Otsu选择，支持反向，返回本次实际阈值及Mono8二值图。Mono16输入不是先降位深后假称原位深阈值。
- AdaptiveThresholdOperation提供Mono8局部均值/高斯阈值，明确奇数窗口、整数偏移、复制边界与反向，不能以全图均值或固定阈值代替。
- MorphologyOperation提供0/255二值腐蚀、膨胀、开、闭，明确中心核、矩形/椭圆/十字形状、宽高、边界和基础阶段次数。灰度输入不自动阈值化，补零不用SDK腐蚀特殊默认值。
- 统一引擎发布前核验滤波位深/尺寸/来源和Otsu阈值范围，保留取消后拒绝迟到结果及独立输出帧语义。
- 本批操作均为全图，未宣称ROI外保留或局部拼接；需要局部处理可先裁剪并保留来源映射。

### 最小关键验证

- 新增12个参数化关键案例，先得到11项真实业务红灯和1项参数拒绝通过；实现后12项全部通过。
- 预期由常量、独立邻域计数/权重和几何构造给出：滤波脉冲必须真正扩散，均值/高斯局部阈值必须对非对称样本产生不同结果，5×5矩形/十字/椭圆核分别产生25/9/17个像素，不以返回成功掩盖参数未生效。
- 覆盖16位中值及Otsu、padding、阈值反向、合法全黑输出、开闭孔隙、次数、显式补零边界以及不支持组合的拒绝。
- 最终定向为本批12项、文件/转换8项、ROI/裁剪7项及模板/无模型保原模型2项：Passed: 29, Failed: 0, Skipped: 0。未运行全量旧测试。
- OpenCV、传统HALCON、HALCON AI及关联Core/Runtime的必要构建最终均0警告、0错误。Core/Runtime/OpenCV的IDE0011与空白格式验证通过，Git差异检查通过；暂存区保持原有458项。

工作目录为仓库根，临时脚本 `%TEMP%/verify-vision-tools-t1-preprocessing.ps1 -Phase green` 复用现有.NET9/xUnit/VSTest，最终使用上述定向过滤。没有新增测试工程、排除生产源码或修改测试包。直接编译测试的xUnit旧参考程序集统一CS1701警告已保留，不与交付模块构建0警告混淆。

日志：`%TEMP%/vision-tools-t1-preprocessing-red.log`、`vision-tools-t1-preprocessing-green.log`、`vision-tools-t1-preprocessing-focused-final.log`、`vision-tools-t1-preprocessing-*-final-build.log`。

### 阶段关闭的边界

T1基础交付以OpenCV为完整主链路，现已完成；不代表全部商业功能、全部旧工具或全部厂商已经实现。HALCON保留已声明的模板、ROI阈值/统计及裁剪，本批新能力未接通、明确Unsupported，不回退OpenCV。

本轮只实际运行OpenCV内存合成图；未运行HALCON原生算法、真实AI模型、宿主、UI或设备。下一阶段为T2定位/ROI跟随/Blob及数量面积判定，本轮没有越界实现。

官方语义核对：[OpenCV 4.11 imgproc头文件](https://raw.githubusercontent.com/opencv/opencv/4.11.0/modules/imgproc/include/opencv2/imgproc.hpp)。其中中值滤波固定BORDER_REPLICATE，CV_16U核只支持3/5；生产校验与此一致。

## 14. T2首批：定位跟随、Blob与无图判定

### 已交付

- `VisionRegionBinding`冻结固定或模板局部基准域；Follow必须明确零基匹配候选，使用候选TemplateToImage做一次正等比旋转/平移，不累积改写基准ROI。
- 统一引擎登记真正发布的定位对象/原模型引用/实际输入快照。弱键及图像弱引用不无限保留逐帧结果或完整搜索图；原生资源仍只属于上下文。
- 跟随Blob在执行前和发布前核验源模型仍当前、来源上下文未关闭。跨上下文允许共享托管定位几何，但源模型被替换或关闭后不能继续发布派生结果。重建定位DTO、重造同GUID图像、派生帧或跨空间输入均不能蒙混复用。
- `BlobOperation`直接对Mono8/Mono16原数值做闭区间阈值，固定/跟随参与域在连通域前生效；四/八连通明确选择，填孔使用互补背景连通性并重新限制参与域。
- 像素面积、质心、整数半开外接框、Extent、外轮廓和孔洞输出为独立托管快照。像素中心轮廓允许点/线退化，不以其多边形面积冒充像素计数；首批时周长/圆度未接入，第二批交付见第15节。
- 面积/宽/高/Extent/孔洞数支持闭区间All/Any，空筛选不过滤；面积或质心轴排序后以行/列/SAUF行优先标签确定并列顺序。Index是一基本次位置，不是跨帧跟踪身份。
- `BlobJudgmentOperation.ForCount/ForPresence/ForArea`通过同一IVisionEngine走真正公共无SDK实现；不准备假模型或假帧。空区域为NoMatch，空数量判定仍可Success；NG也为Success，面积必须明确选中有效一基Index。
- 已发布区域数值和轮廓在源模型/上下文关闭后仍可读取与判定。整个跟随流程没有借用源原生模型，因此不因另一上下文检测而保留无关SDK句柄。
- Blob执行预算：输入2000万像素、原始组件10000个、累计组件轮廓窗口1亿像素、筛选结果1..1000上限。超限失败而非TopN冒充完整数量；不是进程/内存硬隔离。

### 验证证据

- 最初11项取得10项实际Unsupported业务红灯和1项纯几何通过；随后算法、公共执行及来源校验接通。最终本批14项通过，包含真实OpenCV算法、纯几何与两个SDK边界fake，三类证据不混称真实硬件。
- 代表场景由独立算术纹理和矩形构造：定位后只有跟随域内两个区域参与，像素面积为6与4；域外同色干扰不计入。验证数量OK/NG、显式面积Index、源上下文关闭后的快照可读与无图存在性判定。
- 覆盖多目标明确选择、非中心锚点旋转/缩放纯几何、Mono16原阈值/padding、四/八连通、环形16/25像素保孔/填孔、孔洞数量/Extent、All/Any及真实排序、空计数、预算失败无部分输出。
- 来源防护覆盖错误帧/空间、相同GUID重建图像、重造定位对象、模型修订替换及来源关闭。跨上下文执行中替换源模型使用可控SDK边界fake证明发布前拒绝迟到结果，不冒充原生SDK并发验收。
- 合并Core契约23项、执行器生命周期15项、OpenCV模板11项、T1预处理12项/ROI裁剪7项/文件转换8项及本批14项：Passed: 90, Failed: 0, Skipped: 0。不是完整旧测试工程、全仓或宿主验证。
- OpenCV、传统HALCON、HALCON AI及关联Core/Runtime的必要构建均0警告、0错误。Core/Runtime/OpenCV的IDE0011和空白格式验证通过，Git差异检查通过；原有458项暂存未改变。

工作目录为仓库根，临时脚本`%TEMP%/verify-vision-tools-t2.ps1 -Phase green`复用既有.NET9参考程序集、xUnit/VSTest，隔离编译现有相关测试源；没有新增工程、排除生产源码、升级包或删除旧有效测试。直接编译保留xUnit参考程序集统一CS1701警告，不与交付模块构建警告混淆。

最终实际测试命令（仓库根目录）：

```powershell
$filter = 'FullyQualifiedName~BlobWorkflowTests|FullyQualifiedName~VisionEngineLifetimeTests|FullyQualifiedName~VisionEngineIntegrationTests|FullyQualifiedName~ImageProcessingDomainTests|FullyQualifiedName~ImageIoConversionTests|FullyQualifiedName~ImagePreprocessingTests|FullyQualifiedName~VisionEngineContractTests'
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-t2.ps1" -Phase green -Filter $filter
```

必要构建沿用既有命令，对`Tools/OpenCv/Vision.Tools.OpenCv.csproj`、`Tools/Halcon/Vision.Tools.Halcon.csproj`及`AI/Halcon/Vision.AI.Halcon.csproj`逐项执行：`dotnet build <实际项目路径> --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`。

日志：`%TEMP%/vision-tools-t2-red.log`、`vision-tools-t2-green.log`、`vision-tools-t2-focused-final.log`及`vision-tools-t2-*-final-build.log`；TRX位于`Tests/obj/editor-verification/t2-tests/results/`。

### 交付边界

本次完成T2首批基础闭环，不代表所有商业Blob特征、稳定目标跟踪、一般跨空间映射或全部厂商已接通。HALCON自定义锚点搜索域与下游Blob仍未实现、明确Unsupported，不借OpenCV回退。T3测量及T4标定毫米链尚未开始。

本轮只运行OpenCV内存合成图和必要边界fake；没有执行HALCON原生新算子、加载真实AI模型、运行宿主/UI或连接设备。Controls、FlowNode、Workbench和完整旧测试工程未迁移；MachineApp不修改、不构建、不验证，暂存区保留用户原有458项。

## 15. T2第二批：轮廓周长、圆度与筛选

### 已交付及数值口径

- `BlobContourMetrics`为独立强类型托管快照，提供轮廓净面积（像素平方）、总周长（像素）及可空圆度；实际计算留在OpenCV实现，不把厂商对象或算法搬入Core。`BlobItem`明确持有该快照，不添加推测指标的兼容构造。
- 净面积=所有外轮廓的格林公式绝对面积−所有孔洞的绝对面积，总周长=全部外边界及孔洞的`ArcLength(closed=true)`之和。使用现有像素中心折线，不是像素单元外缘周长、真实工件填充面积、亚像素或毫米测量。ROI截断后的边界也计入，不自动补全图外目标。
- 圆度=4π×轮廓净面积/总周长²，只在正面积及正周长时有定义；不以像素计数混算，再截成1。3×3实心像素块的像素面积9、轮廓面积4、周长8、圆度π/4，两个面积口径在API和筛选枚举中明确分开。
- 单点轮廓的面积/周长均为0，两点线段的闭合长度为往返长度；零面积圆度为null，不当作0。圆度筛选遇无定义值始终不命中，范围即使包含0也不例外；All/Any仍正常组合其他特征。
- 新增`ContourArea/ContourPerimeter/ContourCircularity`筛选，范围包含上下限，圆度范围限制0..1。保孔/填孔按最终组件重新计算几何，不沿用填孔前指标。轮廓循环观察取消，但原生调用仍须真实返回后才能释放，不宣称可强停SDK。
- 结果及指标在上下文释放后仍可读取。HALCON Blob/指标未接通，明确Unsupported，不借OpenCV补缺。

### 验证

- 先对三种新筛选取得3项实际Unsupported断言失败，再接通实现；最终新增7项通过，未以编译失败充当业务红灯。
- 独立几何期望覆盖：3×3方块、单像素、4像素线段、中心挖一像素的3×3方环及填孔。方环的轮廓净面积2、总周长8+4√2；填孔后净面积4、周长8。另覆盖null圆度的0范围/All/Any行为、非有限值与范围拒绝、来源关闭后的指标快照。
- 本批7项与既有T2闭环14项合并21项通过；再合并Core契约23项，最终Passed: 44, Failed: 0, Skipped: 0。没有重跑无关全量或声称旧测试工程已迁移。
- 必要关联构建覆盖OpenCV、传统HALCON、HALCON AI及Core/Runtime，均0警告、0错误；仅编译HALCON/AI，不执行新原生算子或真实模型。Core/OpenCV的IDE0011和空白格式验证、Git差异检查通过，原有458项暂存保持不变。

仓库根目录实际测试命令：

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-t2-contours.ps1" -Phase green -Filter 'FullyQualifiedName~BlobContourMetricsTests|FullyQualifiedName~BlobWorkflowTests|FullyQualifiedName~VisionEngineContractTests'
```

沿用现有.NET9参考程序集、xUnit/VSTest隔离编译实际测试源；没有新增测试工程、排除生产源码、删除旧有效测试或升级SDK/包。直接编译仍保留已说明的xUnit参考程序集统一CS1701警告。

日志：`%TEMP%/vision-tools-t2-contours-red.log`、`vision-tools-t2-contours-green.log`、`vision-tools-t2-contours-focused-final.log`和`vision-tools-t2-contours-*-final-build.log`。TRX位于`Tests/obj/editor-verification/t2-contour-tests/results/`。

本批仍只验证OpenCV内存合成图与必要契约/事务fake，不代表真实工件精度、长期运行或性能验收。HALCON自定义锚点搜索域、一般派生图/跨空间映射、稳定目标跟踪及UI接入仍未完成。未触及MachineApp、宿主或设备，未修改原有458项暂存。

## 16. T3首批：固定路径卡尺找直线

### 已交付

- `CaliperLineOperation`及卡尺选边枚举为不可变执行快照，不引用可编辑ILineSearchRegion、旧Tool或会话。结果包括`CaliperLineFit`、`CaliperEdgeSample`及明确的拟合状态。
- 有向Start→End含端点均匀布置卡尺，右法向(dy,-dx)/长度。Forward从负半长扫到正半长，Reverse反转实际扫描及灰度极性。First/Last/Strongest按局部绝对差峰选择，平顶或强度并列取扫描较早位置。
- 原图Mono8/Mono16有效行复制后在CV_32F内插值，不归一化16位数值；切向整数采样平均，法向1像素间距，按明确sigma/核半径高斯平滑后做相邻差分。阈值为原灰度差，不是0..1得分；峰位置取相邻采样中点，不宣称真实工件亚像素精度。
- OpenCV使用WarpAffine逆映射双线性采样、沿扫描轴GaussianBlur及FitLine。先Huber初始化，按用户最大正交残差选内点，再L2重拟合并逐轮删除最大超限残差点；这是已说明的方法，不是全局RANSAC保证。原生reps/aeps=0.01仅为收敛参数。
- 全部卡尺保留中心、选中边缘、扫描有符号对比度、零基Index、是否参与拟合及最终直线距离。端点为参与点投影极值，按基准方向排列；RMS/最大残差仅统计参与点，单位像素。
- 无有效边缘、内点不足或退化均为NoMatch与本次诊断，Line为空；不拿旧直线或0残差冒充本次有效结果。统一执行器校验本次帧/空间、卡尺数量、参与点、残差限和坐标。
- 所有卡尺角点必须在图内，不截断、不以补边生成边缘。卡尺2..512把、法向半长2..512、切向半宽0..32，总采样500万；图像每边小于32767、像素不超过2000万。sigma为0或0.01..10，半长须超过ceil(3sigma)。这些是预算，不是进程内存硬隔离。
- 临时Mat由当前调用创建并using释放，原生调用仍须真实结束才释放；取消在卡尺及拟合循环间观察。托管结果在源上下文关闭后仍可读取；不借当前模型、不公开第二业务入口。

### 关键验证

- 先取得10项实际Unsupported断言红灯、1项参数边界通过；接通后本批11项全部通过，未用编译失败冒充算法红灯。
- 独立阶梯图的3条灰度边界位于11.5/19.5/29.5像素，方向/极性/First-Last-Strongest应选不同坐标并返回±40/60/180灰度差；不是只验证返回Success。
- 原灰度200阶跃在sigma0、阈值100时命中；高斯平滑后同阈值或阈值提高至201均NoMatch。没有旧直线残留。
- 单列255干扰会使窄卡尺产生离群点；残差剔除后6点拟合，保留被剔除点诊断。切向平均后7点全部有效；要求7点但只有6内点则NoMatch。另验证斜向采样/斜率和RMS、16位12000灰度差及padding、越界/退化/sigma/预算拒绝、上下文关闭后的结果可读。
- 最终合并本批11项、Core契约23项、生命周期15项及T2闭环14项：Passed: 63, Failed: 0, Skipped: 0。没有运行完整旧测试工程、WPF、宿主或全仓。
- OpenCV、传统HALCON、HALCON AI及关联Core/Runtime必要构建均0警告、0错误；Core/Runtime/OpenCV的IDE0011、空白格式验证通过。直接编译测试仍保留既有xUnit参考程序集统一CS1701警告，不与交付模块构建混淆。

实际测试命令（仓库根目录）：

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-t3-calipers.ps1" -Phase green -Filter 'FullyQualifiedName~CaliperLineEngineTests|FullyQualifiedName~VisionEngineContractTests|FullyQualifiedName~VisionEngineLifetimeTests|FullyQualifiedName~BlobWorkflowTests'
```

测试复用现有.NET9参考程序集、xUnit/VSTest及实际测试源，没有新增测试工程、排除生产源码、批量删除旧测试、升级框架或SDK。

日志：`%TEMP%/vision-tools-t3-calipers-red.log`、`vision-tools-t3-calipers-green.log`、`vision-tools-t3-calipers-focused-final.log`和`vision-tools-t3-calipers-*-final-build.log`。TRX：`Tests/obj/editor-verification/t3-caliper-tests/results/`。

### 尚未交付

首批只完成固定路径像素卡尺直线。公共几何/测量公差随后在第二批交付，见第17节；圆、边缘对、定位跟随卡尺及毫米标定仍未完成，不以一条直线宣称第二工业测量闭环已完成。HALCON不声明本能力、不借OpenCV回退；没有执行新的HALCON原生算子、AI模型、宿主、UI或设备。MachineApp不修改、不构建、不验证，暂存区保留原有458项。

## 17. T3第二批：公共几何与显式单位公差

### 已交付

- `PixelPoint/PixelLine`是独立托管的有限像素几何值，明确FrameId及CoordinateSpaceId；直线由间距大于1e-9像素且可表示的两个点定义。可显式提取Blob一基Index质心或有效卡尺拟合直线，也可建立带来源标签的参考几何；标签不是传感器/模型真实性凭证。
- 强类型无图操作接通点点欧氏距离、点到无限直线的无符号正交距离、无向锐夹角及屏幕顺时针有向角、无限直线交点。主入口仍为IVisionEngine，算法位于Runtime/Common，不进入所选SDK的Validate/Execute，不构成第三厂商或备用算法。
- 两输入必须同帧同像素空间。即使空间相同但FrameId不同也拒绝，不猜派生图/裁剪/世界映射，不拿任意线距冒充边缘对宽度。
- 距离使用Double.Hypot或单位方向叉积，避免直接平方使可表示的大距离中间溢出。真正不可表示的数值失败且无Value，不发布NaN/Inf、截断值或旧结果。
- 夹角模式必须明确：无向锐夹角[0,90]度，或第一条Start→End到第二条的屏幕顺时针有符号角(-180,180]度。反向固定+180，不隐式丢弃方向。
- 交点按无限直线计算，可在线段或视野之外；以单位方向叉积绝对值SinAngle核验稳定性，允许阈值1e-12..1、默认1e-6。平行/重合或低于阈值则NoMatch及空Point，不任取交点；结果点可继续参与几何运算。
- `ScalarMeasurementOutput`明确测量含义及由此确定的Pixels/Degrees单位，`MeasurementRangeOperation`要求显式相同单位，以有限闭区间输出OK/NG。负有向角可判定，区间不在±180自动包绕；NG仍Success，未提供毫米换标签。
- 公共输出统一核验来源、测量含义、上下限及NoMatch一致性，保留上下文独占和取消后拒绝迟到发布。结果不持有原生资源，源上下文关闭后仍可执行几何及公差。

### 验证证据

- 首轮15个案例取得14项实际Unsupported断言失败、1项源/退化边界通过；接通后15项全部通过，不把编译失败作为算法红灯。
- 纯公共路径由拒绝几何SDK调用的边界fake证明：3-4-5距离、重合点0、可表示大距离1e200、点到无限线而非端点、有向±90/+180及无向45度、线段外交点及继续求距、平行/近似平行NoMatch、跨帧/空间、退化线/无本次卡尺结果、单位不符、闭区间边界、负角及溢出无Value。
- 真实OpenCV独立阶跃/矩形图：卡尺拟合y=23.5，Blob质心(32,30)，关闭来源上下文后公共点线距离为6.5像素，再以[6.4,6.6]与[6.6,7]分别得到OK/NG。是像素位置距离链，不是边缘对测宽或毫米标定闭环。
- 最终定向合并本批15项、卡尺11项、Core契约23项、生命周期15项及T2闭环14项：Passed: 78, Failed: 0, Skipped: 0。没有运行完整旧工程、宿主或全仓。
- OpenCV、传统HALCON、HALCON AI及关联Core/Runtime必要构建均0警告、0错误；Core/Runtime的IDE0011及空白格式验证、Git差异检查通过。测试直接编译保留既有xUnit参考程序集统一CS1701警告，不与交付模块构建混淆。

实际命令（仓库根目录）：

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-t3-geometry.ps1" -Phase green -Filter 'FullyQualifiedName~GeometryMeasurementEngineTests|FullyQualifiedName~CaliperLineEngineTests|FullyQualifiedName~VisionEngineContractTests|FullyQualifiedName~VisionEngineLifetimeTests|FullyQualifiedName~BlobWorkflowTests'
```

沿用既有.NET9参考程序集、xUnit/VSTest隔离编译实际相关测试源，不新增工程、不屏蔽生产源码、不批量删除旧测试、不升级SDK或框架。

日志：`%TEMP%/vision-tools-t3-geometry-red.log`、`vision-tools-t3-geometry-green.log`、`vision-tools-t3-geometry-focused-final.log`、`vision-tools-t3-geometry-*-final-build.log`。TRX：`Tests/obj/editor-verification/t3-geometry-tests/results/`。

API核对：[.NET9 Double.Hypot](https://learn.microsoft.com/en-us/dotnet/api/system.double.hypot?view=net-9.0)，不是更换框架或数值精度承诺。

### 尚未完成

第二批结束时圆、边缘对宽度及定位跟随测量等仍未交付；边缘对宽度随后在第三批完成，见第18节。圆、定位跟随测量、一般跨空间映射及毫米标定仍未完成；Controls/FlowNode/Workbench及完整旧测试工程未迁移。本轮只有公共值/fake与OpenCV内存合成图，不代表真实工件精度、性能或长期运行验收；未执行新HALCON原生算子、真实AI模型、UI、宿主或设备。未触及MachineApp或原有458项暂存。

## 18. T3第三批：同一扫描剖面的边缘对宽度

### 已交付

- `CaliperEdgePairOperation`独立定义明/暗条带、正宽度闭区间、选对规则及最低完整对数量，仍由IVisionEngine执行。请求不借用找线请求或无关拟合参数，不准备假模型/图像。
- 同一固定像素路径含端点布置卡尺，复用唯一的原位深浮点采样、切向平均、高斯/差分峰内核；找线原平顶/极性规则保留。边缘对检测将紧邻异号峰作为两条边，不因为绝对强度相等就合并。
- 只在同一剖面里，对阈值后相邻合格异极性峰配对；LightStripe先正后负，DarkStripe先负后正。反向扫描按新的峰次序解释，不跨卡尺拼接，也不跳过其他合格峰寻找远处边缘。
- 宽度=两峰中点在请求扫描轴的偏移差，单位像素，不是两条拟合线距离、不自动纠正条带倾斜、不宣称亚像素/毫米测量。宽度闭区间先过滤，再按First/Last/Strongest选对；Strongest取两边较弱的绝对灰度差，并列取较早一对。
- 每把卡尺最多选一对，记录零基Index、中心、阈值后峰数、宽度/极性过滤后的候选对数、两侧坐标、有符号对比度及宽度。没有完整对时两边/宽度都为空，不以半边或0宽度伪装成功。
- 完整选中对达到MinimumPairs后，汇总全部选中宽度的均值、最小/最大和总体标准差，不自动剔除宽度异常或在卡尺间建立稳定条带身份；统计不足则NoMatch、Statistics为空，已找到的局部对仍保留。
- 均值以ScalarMeasurementOutput的CaliperPairMeanWidth/Pixels输出，可直接进入显式单位公差。上下文释放后仍可读取/判定；均值OK不证明每个位置均OK，逐点要求应读取Samples。
- 输入格式、图内卡尺、sigma和500万采样预算与卡尺约定一致；图像每边小于32767、总像素不超过2000万。临时Mat由当前调用using拥有，取消在卡尺之间观察，原生调用仍须真实返回，不宣称硬停/内存硬隔离。
- 统一引擎检查帧/空间、诊断数量、宽度界限、图内边缘及统计存在条件。HALCON未声明边缘对能力，不借OpenCV补缺。

### 验证证据

- 初始12项测宽实际Unsupported断言失败，既有找线11项通过；接通后扩展验证。
- 额外取得1像素条带1项实际NoMatch红灯，2像素对照1项通过。原因是原找线绝对峰平顶规则合并相邻等强异号峰；仅测宽启用异号峰分离后修复，找线原规则保留并回归通过。
- 最终新增15项通过：正反扫描的明/暗10像素条带、First/Last/Strongest多条带5/8像素差异、宽度边界先筛选、不把不同剖面的单边拼成测宽、MinimumPairs不足保留局部对但无旧统计、16位12000原灰度差、1/2像素薄带及弱边主导的强度排序。
- 3把卡尺宽度4/6/8像素得到均值6、极值4/8及总体标准差sqrt(8/3)，并在源上下文关闭后做公共公差NG；10像素宽度也直接接入公差OK。期望由独立阶跃/区间构造，不由被测SDK反算。
- 最终合并本批15项、卡尺找线11项、公共几何15项、Core契约23项及生命周期15项：Passed: 79, Failed: 0, Skipped: 0。没有默认全量或恢复旧API测试壳。
- OpenCV、传统HALCON、HALCON AI及关联Core/Runtime必要构建均0警告、0错误；Core/Runtime/OpenCV的IDE0011、空白格式验证及Git差异检查通过，原有458项暂存不变。直接编译测试仍保留既有xUnit参考程序集统一CS1701警告，不与交付模块构建混淆。

实际测试命令（仓库根目录）：

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-t3-edge-pair.ps1" -Phase green -Filter 'FullyQualifiedName~CaliperEdgePairEngineTests|FullyQualifiedName~CaliperLineEngineTests|FullyQualifiedName~GeometryMeasurementEngineTests|FullyQualifiedName~VisionEngineContractTests|FullyQualifiedName~VisionEngineLifetimeTests'
```

沿用现有.NET9参考程序集、xUnit/VSTest及实际测试源；没有新增工程、排除生产源码、批量删除旧有效测试或升级框架/SDK。

日志：`%TEMP%/vision-tools-t3-edge-pair-red.log`、`vision-tools-t3-edge-pair-thin-red.log`、`vision-tools-t3-edge-pair-green.log`、`vision-tools-t3-edge-pair-focused-final.log`及`vision-tools-t3-edge-pair-*-final-build.log`。TRX：`Tests/obj/editor-verification/t3-edge-pair-tests/results/`。

### 未完成及验收边界

第三批结束时圆等仍待后续；圆已在第四批交付，见第19节。独立边缘间距、定位跟随测量、一般跨空间映射及毫米标定仍未完成；Controls/FlowNode/Workbench和完整旧测试工程未迁移。当前只验证OpenCV内存合成图及必要边界fake，不代表真实工件精度、节拍或长期运行验收；没有运行HALCON新原生算子、真实AI模型、UI、宿主或设备。MachineApp未触及，原有458项暂存不变。

## 19. T3第四批：圆卡尺、径向拟合与直径公差

### 已交付

- `CaliperCircleOperation`冻结固定名义圆/圆弧及参数，Outward/Inward径向扫描，极性/First-Last-Strongest与原位深阈值、切向平均/高斯差分共用现有卡尺内核。名义圆仅用于放置卡尺，不是返回的测量圆。
- 正角为屏幕顺时针。全360度分布N个不同角度、不重复首尾，局部弧包含两端；名义半径必须大于搜索半长，所有卡尺矩形角点都在图内，不截断/补边或跨圆心。
- 3..512把卡尺、总采样不超过500万，图像每边小于32767且总像素不超过2000万，格式仅Mono8/16。复用浮点采样保留原灰度单位，不归一化16位数据；预算不是内存硬隔离或强停保证。
- 最多64组按样本次序分散的确定性三点种子形成有限共识初始化，不宣称穷举RANSAC或全局最优。以局部中心/尺度归一化SVD求代数初值，再最多15轮径向几何最小二乘精化，不拿最小包围圆代替测量拟合。
- 残差超限点逐轮剔除，最多5轮将精化后满足同一残差限的点回纳并重拟合。共线/病态、边缘不足、内点不足或实际覆盖角不足均为NoMatch及空Circle，仍保留本次卡尺选点诊断。
- 返回圆心、半径、参与点数、RMS/最大径向残差及覆盖角。Coverage=360度−内点相对拟合圆心的最大角间隙，有限完整环采样通常小于360度，不能以配方搜索角代替质量守卫。
- `CaliperEdgeSample`残差在找圆时是到拟合圆周的绝对径向距离；最终RMS/最大残差只统计参与点，剔除点保留坐标与最终残差。直径以CaliperCircleDiameter/Pixels标量接公共公差，上下文关闭后仍可读/判定。
- 统一引擎核验来源帧/空间、卡尺总数、参与点下限、覆盖角、最大径向残差及图内选点。临时Mat由当前调用using拥有，取消在卡尺、共识、精化/剔除循环间观察；仍须等待原生调用实际结束再释放/发布。

### 验证证据

- 首轮12项得到11项实际Unsupported断言红灯、1项参数拒绝通过。接通后11项通过、1项离群点断言失败：种子排除的4个观测在精化后已低于残差限却未回纳，导致额外假剔除。取得实际失败后补有限回纳，再验证通过，未降低断言。
- 最终新增13项通过：内/外向亮盘极性、同心15/25像素边缘的First-Last-Strongest、搜索圆心/半径偏移后真实拟合、局部径向离群点及MinimumPoints、部分弧端点与覆盖、近直线点集拒绝不稳定大圆、16位12000灰度差/padding、高阈值与高斯无边缘、非法几何/覆盖界限、释放上下文后的直径公差。
- 独立整数栅格圆盘给出圆心(48,48)、名义真实半径20；搜索中心(45,50)/半径18时仍拟合目标，而不是复制配方。局部半径26干扰只剔除该点，32点全部必需时明确NoMatch。
- 全圆/部分弧的测量来自径向采样与拟合，期望由独立栅格距离/阶跃构造，不由被测SDK反算；有限像素拟合容差不等于真实工件或亚像素精度。
- 最终合并本批13项、边缘对15项、直线11项、公共几何15项、Core契约23项及生命周期15项：Passed: 92, Failed: 0, Skipped: 0。未运行完整旧测试工程、WPF或宿主。
- OpenCV、传统HALCON、HALCON AI及关联Core/Runtime必要构建均0警告、0错误；Core/Runtime/OpenCV的IDE0011、空白格式验证及Git差异检查通过，原有458项暂存不变。直接编译测试保留既有xUnit参考程序集统一CS1701警告，不与交付模块构建混淆。

仓库根实际测试命令：

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-t3-circle.ps1" -Phase green -Filter 'FullyQualifiedName~CaliperCircleEngineTests|FullyQualifiedName~CaliperEdgePairEngineTests|FullyQualifiedName~CaliperLineEngineTests|FullyQualifiedName~GeometryMeasurementEngineTests|FullyQualifiedName~VisionEngineContractTests|FullyQualifiedName~VisionEngineLifetimeTests'
```

仍复用既有.NET9参考程序集、xUnit/VSTest及实际测试源，无新测试工程、生产源码排除、有效旧测试批量删除或框架/SDK升级。

日志：`%TEMP%/vision-tools-t3-circle-red.log`、`vision-tools-t3-circle-consensus-red.log`、`vision-tools-t3-circle-green.log`、`vision-tools-t3-circle-focused-final.log`及`vision-tools-t3-circle-*-final-build.log`。TRX：`Tests/obj/editor-verification/t3-circle-tests/results/`。

### 剩余与验收边界

第四批时固定搜索区卡尺直线/边缘对/圆及基础公差已可调用，定位跟随随后在第五批交付，见第20节。独立边缘间距、一般跨空间映射、标定及UI/FlowNode/宿主接入仍待后续。HALCON不声明圆卡尺、不借OpenCV回退；只编译关联模块，没有执行新的HALCON原生算子、真实AI模型、设备或宿主，不代表真实工件精度/节拍/长期运行验收。MachineApp未触及，原有458项暂存不变。

## 20. T3第五批：定位跟随测量与基础像素阶段关闭

### 已交付

- `TemplateLocationBinding`保存本次原定位、原模板模型引用及显式零基候选下标。只提供纯几何，不能凭GUID或构造DTO证明实际结果发布/模型存活。
- CaliperLineOperation/CaliperEdgePairOperation/CaliperCircleOperation可显式附该绑定。未绑定时既有固定像素行为不变；绑定时输入端点/圆心/名义半径/圆弧起始角为模板局部基准，Reference字段保留原值，实际名义几何只映射一次，不累积改写配方。
- 正等比缩放只改变名义几何：路径端点映射，圆心映射、半径乘Scale、圆弧起始角加定位角；跨度、数量不变。采样半长/半宽、sigma、残差限、边缘对宽度上下限仍是当前图像像素，不自动缩放/舍入整数采样或把比例当毫米标定，需变化时调用方明确重建参数。
- TemplateMatchCandidate只表达正等比旋转/平移，不接受任意剪切/镜像；变换后的几何仍受有限、非退化、全卡尺在图内及预算约束。跨帧/空间在构造时拒绝，不猜派生图/裁剪映射。
- 三类卡尺与Blob共用唯一原定位发布账本，不另建跟随引擎或SDK会话。执行前/发布前检查本引擎原定位对象、原模型引用、实际定位输入对象、来源上下文未关闭及模型仍当前。
- 重建的定位DTO、相同GUID的新图像对象、失效模型/关闭来源不能复用；跨上下文测量期间源模型被替换，迟到结果失败无Value，不自动重新定位、切厂商或使用旧输出。
- 请求自身Model仍为空，不借源原生模型。已发布的测量标量/几何是独立托管快照，来源关闭后仍可继续公共几何/公差，不能把完成结果的可读性误当作旧定位继续有效。

### 验证证据

- 新增6项；先取得4项真实断言失败、2项通过。失败明确揭示缺少通用定位守卫：实际模型替换后Validate返回空问题、重造定位被Success、执行中替换源修订仍发布Success，不是编译或缺SDK伪红灯。
- 真实OpenCV独立纹理/矩形/圆盘场景：模板置于(8,10)，模型锚点(2,3)，定位0°/90°后实际驱动跟随直线、边缘对和圆，原Reference基准不变。原图上条带上边缘y=29.5、宽度10、圆心(98,55)/半径约12；整图独立整数顺时针旋转90°后上边缘对应x=97.5、圆心(72,98)，直径接像素公差。
- 参数纯几何验证非中心锚点、90°/2倍正等比：端点/圆心/半径/圆弧起始角正确变换，采样半长/半宽/残差/宽度范围保持当前像素单位；此项不冒充新的真实SDK缩放或工件精度验收。
- 覆盖显式第二候选目标、错误帧/空间、重造定位、同GUID换图、源修订替换与关闭，对三类请求都拒绝。跨上下文执行中修订替换由可控SDK边界fake证明发布前拒绝迟到结果，不混称真实原生并发验收。
- 最终定向为本批6项、固定直线11项、边缘对15项、圆13项、Blob闭环14项及生命周期15项：Passed: 74, Failed: 0, Skipped: 0。复核既有Blob共用账本与测量默认行为，不跑无关全量。
- OpenCV、传统HALCON、HALCON AI及关联Core/Runtime必要构建均0警告、0错误；Core/Runtime的IDE0011与空白格式验证通过。直接编译测试保留既有xUnit参考程序集统一CS1701警告，不与模块构建混淆。

仓库根实际测试命令：

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-t3-follow.ps1" -Phase green -Filter 'FullyQualifiedName~CaliperLocationFollowTests|FullyQualifiedName~CaliperLineEngineTests|FullyQualifiedName~CaliperEdgePairEngineTests|FullyQualifiedName~CaliperCircleEngineTests|FullyQualifiedName~BlobWorkflowTests|FullyQualifiedName~VisionEngineLifetimeTests'
```

复用既有.NET9参考程序集、xUnit/VSTest及实际测试源，不新增工程、排除生产源码、批量删除有效旧测试或升级框架/SDK。

日志：`%TEMP%/vision-tools-t3-follow-red.log`、`vision-tools-t3-follow-green.log`、`vision-tools-t3-follow-focused-final.log`及`vision-tools-t3-follow-*-final-build.log`。TRX：`Tests/obj/editor-verification/t3-follow-tests/results/`。

### 阶段关闭与未验边界

T3基础像素链已完成：定位→跟随卡尺直线/边缘对/圆→像素几何/公差可经同一引擎调用。下一阶段为T4显式标定和毫米换算；不以像素链声称第二毫米闭环或真实工业精度已完成。

一般派生图/跨空间映射、独立边缘间距、稳定目标跟踪及UI/FlowNode/宿主接入仍待后续，完整旧测试工程未迁移。HALCON不声明未实现卡尺、不借OpenCV回退；没有运行新的HALCON原生算子、真实AI模型、UI、宿主或设备。MachineApp未触及，原有458项暂存不变。

## 21. T4首批：已知长度均匀比例与毫米公差

### 已交付与适用边界

- `PixelScaleCalibrationOperation`输入同帧同像素空间的两个明确参考点及已知正毫米长度，参考像素间距须至少1且可表示。公共实现计算像素跨度和mm/px，发布独立`UniformPixelScaleCalibration`，保留CalibrationId、参考帧/点、像素/毫米长度及适用空间，不准备假SDK模型。
- 比例标定仅表达均匀像素假设；不提供世界原点、X/Y独立尺度、二维仿射、透视或畸变校正。单参考精确约束没有独立验证残差，不虚构零残差/置信度，物理平面与镜头/焦距/采集分辨率等适用性由调用方确认。
- `LengthToMillimetersOperation`只接受Pixels长度和适用的CoordinateSpaceId，可处理点点/点线距离、条带平均宽度和圆直径。原测量Kind/Value/Unit保持不变，输出`MillimeterLengthOutput`同时保存SourcePixels和Calibration，毫米值必须与原值×比例一致，不能只改标签。
- 同一像素空间后续FrameId可不同；不同裁剪/独立解码空间拒绝，不从尺寸/文件名/相同像素推断映射。标定身份不是物理条件未变的证明，定位匹配Scale也不自动改mm/px。
- `MillimeterRangeOperation`只接带明确标定的毫米长度，按有限闭区间判OK/NG；NG仍Success，原像素/角度公差接口不被扩展成任意单位转换器。重复换算因输入强类型不同而不能隐式进行。
- 有效0像素可换算0毫米；比例为0/非有限、结果上溢或正长度下溢为0均失败无Value。上下文关闭后标定/已完成测量均为托管快照，仍可继续换算/判定，不借源原生模型。
- 实现位于Runtime/Common，无所选SDK验证/执行调用，也不是换厂商回退。Core只保留强类型值/请求与数值一致性守卫，不读文件、加载SDK或制造校准证书。

### 验证证据

- 最初10项为9失败、1边界通过，其中7项明确Unsupported断言，另外2项因前置标定未成功导致依赖为空；未将其全部混称算法红灯。已补前置成功断言，接通后10项全绿，未以编译失败冒充业务红灯。
- 公共路径用拒绝SDK调用的边界fake证明：3-4-5参考像素长度5与已知10mm得到2mm/px，4种长度7.5px均换算15mm；同空间后续帧允许使用，原像素值/单位不变，CalibrationId与源快照保持可追踪。
- 覆盖毫米闭区间边界、NG仍Success、角度/错空间及直接改标签拒绝、0长度、数值上溢和正数下溢、短/重合参考点、错误帧及非有限/非正已知长度。
- 真实OpenCV合成场景：算术模板定位→显式跟随测宽10px；独立参考条端点相距100px，配方明确赋予10mm，得到0.1mm/px；释放源上下文后换算1mm，区间[0.95,1.05]为OK，[1.1,1.2]为NG。10mm是合成配方假设，不是实际标定工件验收。
- 最终定向：本批10项、定位跟随6项、公共几何15项、生命周期15项及Core契约23项，Passed: 69, Failed: 0, Skipped: 0。没有默认全量或恢复旧API测试壳。
- 必要构建均成功、0错误：OpenCV及HALCON AI为0警告；传统HALCON关联构建记录106个警告，输出位于Extension.Core/Extension依赖（空引用等），本批未扩散修改这些依赖，不能将本批说成所有模块0警告。Core/Runtime的IDE0011、空白格式及Git差异检查通过，原有458项暂存不变。测试直接编译另有已说明的xUnit参考程序集统一CS1701警告，与模块构建分开记录。

仓库根实际测试命令：

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-t4-scale.ps1" -Phase green -Filter 'FullyQualifiedName~PixelScaleCalibrationEngineTests|FullyQualifiedName~CaliperLocationFollowTests|FullyQualifiedName~GeometryMeasurementEngineTests|FullyQualifiedName~VisionEngineLifetimeTests|FullyQualifiedName~VisionEngineContractTests'
```

复用现有.NET9参考程序集、xUnit/VSTest与实际测试源；没有新增工程、屏蔽生产源码、批量删除有效旧测试或升级框架/SDK。

日志：`%TEMP%/vision-tools-t4-scale-red.log`、`vision-tools-t4-scale-green.log`、`vision-tools-t4-scale-focused-final.log`及`vision-tools-t4-scale-*-final-build.log`。TRX：`Tests/obj/editor-verification/t4-scale-tests/results/`。

### 仍待后续

截至本节首批记录，T4仅完成明确均匀比例下的毫米长度链；二维仿射点映射/点距与残差/病态诊断后续交付见第22节。镜头畸变、透视、标定板、手眼等更不在本批范围。真实工件精度、平面/光学适用性、节拍与长期运行仍须独立验收。

一般跨空间/派生图映射、独立边缘间距、稳定目标身份及Controls/FlowNode/Workbench/完整旧测试工程迁移仍待后续。没有执行新HALCON原生算子、真实AI模型、UI、宿主或设备；MachineApp未触及，原有458项暂存不变。

## 22. T4第二批：二维仿射点对、双向世界点与毫米点距

### 已交付与边界

- `CalibrationPointPair`明确一个同帧/像素空间的`PixelPoint`和一个同世界空间的已知`MillimeterPoint`。`AffineCalibrationOperation`冻结3..4096点对，超预算失败而非截断；像素→世界方向固定，世界原点/轴向及物理平面来自明确毫米参考，不猜翻转。
- 公共Runtime执行等权归一化Householder QR最小二乘，使用全部参考点，不以正规方程求逆、不自动删掉高残差参考点。发布`AffinePixelCalibration`保留CalibrationId、参考帧/两空间、正/逆系数、全部参考点/同序毫米欧氏残差、RMS/最大残差及设计/线性映射条件数。
- 设计条件数对应居中/公共跨度归一化后的[u,v,1]矩阵；线性映射条件数为2×2部分的奇异值比。阈值有限且在[1,1e12]，默认1e6；最大毫米残差限必须显式为正。相对尺度判断可逆，允许反射，不以单位相关的绝对行列式拒绝小mm/px。
- 重合/共线、近共线病态、奇异/病态世界映射、不可表示系数/逆映射和超最大残差均为带稳定`Rejection`的NoMatch，`AffineCalibrationOutput.Calibration`为空，没有可用旧矩阵；已算出的逐点残差保留，未能计算则为空，不伪造0。
- `PixelToMillimeterPointOperation`与`MillimeterPointToPixelOperation`显式提供来源空间与逆映射目标帧。同像素空间后续帧可复用，错像素/世界空间拒绝。结果保留原点快照和标定，不裁视野/参考凸包，不承诺外推可靠性。
- `CalibratedPointDistanceOperation`对两个已映射毫米点求欧氏距离；两个像素观测须同帧同空间、同一标定快照，不能混用重标定修订。溢出或不同像素点因浮点表示变为假0距离时失败无Value。
- `IMillimeterLengthMeasurement`仅统一已交付的均匀比例长度与仿射世界点距值/来源/CalibrationId；毫米公差只接这两个明确不可变快照，拒绝第三方裸毫米实现，闭区间NG仍Success。它不是执行服务、兼容壳或任意单位字典。
- 非等比/剪切下不把裸像素标量乘平均比例；本批世界点距不是世界法向厚度，像素圆也不一定仍为世界圆。一般世界线/法向厚度、非等比圆/椭圆尺寸尚未接通；不以明确点距冒称这些测量。
- 三点可精确拟合，残差只反映本组参考点，不是独立校准误差/工件精度承诺。二维仿射不等于透视、畸变、标定板识别或手眼标定；物理平面与光学/采集分辨率适用性仍由调用方确认。
- 所有计算走唯一IVisionEngine的Common分派，未加载额外数值包或调用所选SDK验证/执行，也不准备假模型。Core只保存强类型值/一致性守卫，矩阵为值拷贝，参考和残差集合独立只读，输出不持有原生所有权。

### 验证证据

- 首轮13项中12项因Unsupported断言失败、1项输入守卫通过，不是编译红灯。接通后增加最少三点/大坐标偏移、共线世界点及数值距离边界，最终本批17项通过。
- 拒绝SDK调用的fake证明公共路径：明确仿射x_mm=0.1*x+0.05*y+10、y_mm=-0.02*x+0.2*y-5，正确发布系数、双向点映射；像素差(3,4)先变为毫米差(0.5,0.74)，点距为sqrt(0.5²+0.74²)，并接毫米OK/NG，不用5px乘平均比例。
- 覆盖反射、1e-9小单位下仍可逆、三点最小集及1e9偏移、参考冻结/数量预算/跨帧空间拒绝、裸毫米/错逆空间/无目标帧拒绝、混帧或混标定拒绝、不可表示映射/距离及正点距下溢假0拒绝。
- 多点噪声独立期望：四角和中心共5点，中心X毫米参考偏0.5，其余无扰动；等权解最大残差0.4mm、RMS0.2mm。0.5mm限接受，0.3mm限NoMatch且Calibration为空，逐点残差仍保留。共线/近共线像素、重合/共线世界及病态非等比映射均未发布可用矩阵。
- 真实OpenCV合成128×128图条带：同剖面10px完整边缘对，配方明确x_mm=0.1*x+0.05*y、y_mm=0.2*y；释放来源上下文后映射选中卡尺的两边缘点，毫米差(0.5,2)，欧氏点距sqrt(4.25)约2.06155mm，区间[2,2.1]为OK、[0.9,1.1]为NG。该值不是世界条带法向厚度；参考毫米来自合成配方，不是真实标定板/工件。
- 最终Passed: 86, Failed: 0, Skipped: 0：本批17、比例10、定位跟随6、公共几何15、生命周期15及Core契约23。没有默认全量或恢复旧API测试壳。
- 必要OpenCV/传统HALCON/HALCON AI及关联Core/Runtime增量构建成功；Core/Runtime空白与IDE0011验证通过。增量0警告不代表已修复第21节完整依赖编译暴露的106个Extension警告；测试直接编译仍另有已说明的CS1701统一警告，不混淆口径。

仓库根实际命令：

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-t4-affine.ps1" -Phase green -Filter 'FullyQualifiedName~AffineCalibrationEngineTests|FullyQualifiedName~PixelScaleCalibrationEngineTests|FullyQualifiedName~CaliperLocationFollowTests|FullyQualifiedName~GeometryMeasurementEngineTests|FullyQualifiedName~VisionEngineLifetimeTests|FullyQualifiedName~VisionEngineContractTests'
```

沿用.NET9实际源、现有xUnit/VSTest、SDK10.0.401及原OpenCV/HALCON依赖，无新工程或包升级。日志：`%TEMP%/vision-tools-t4-affine-{red,green,focused-final}.log`、`vision-tools-t4-affine-*-final-build.log`；TRX：`Tests/obj/editor-verification/t4-affine-tests/results/`。

### 尚未验收

真实标记识别/工件精度、光学平面适用性、外推、节拍与长期运行均未验；没有执行新HALCON原生、真实AI、UI/宿主或设备。本节当时未交付的世界直线/垂距后续见第23节；一般跨派生图、非等比圆/椭圆尺寸及外层迁移仍未完成。MachineApp未触及，原有458项暂存不变。

## 23. T4第三批：完整世界直线与毫米无限线垂距

### 已交付与数值口径

- `PixelLineToMillimetersOperation`输入带来源帧/像素空间的有向无限`PixelLine`及明确`AffinePixelCalibration`，完整映射两个定义点发布`CalibratedMillimeterLineOutput`。错空间拒绝，保留原线/标定，世界方向有限且非零；不使用单位相关固定毫米长度阈值，不裁有限线段。
- `CalibratedPointLineDistanceOperation`要求原像素点/线同帧同空间、同一标定快照，在已完整映射的毫米欧氏几何中重新求法向，返回无向点到无限直线的垂距。不能直接仿射变换像素法向，不能给像素垂距乘平均比例；反转直线方向不改变结果，垂足在定义点外也不改成端点距。
- `AffineMillimeterPointLineDistanceOutput`保留点/线及CalibrationId，接入既有毫米闭区间公差，NG仍Success。毫米值接口现明确支持均匀比例长度、仿射世界点距和仿射点线垂距三类真实不可变快照，不接任意第三方裸数。
- 世界坐标必须是同一物理平面的正交、同毫米单位欧氏坐标，不能拿非正交坐标标签代替物理距离。仿射可以反向/剪切，但距离计算依旧使用正确世界度量。
- 映射压塌/非有限世界直线以及非有限垂距失败无Value；合法共线0距离正常成功。非共线像素几何被浮点压成假0毫米距离时拒绝，不伪装成工件与边界重合。
- 像素拟合线的完整映射不等于按世界欧氏残差重新拟合，原卡尺拟合残差仍为像素，不把它改标签为毫米精度。任意单点/无限线距离不证明实际两边处处平行、等厚或满足整件质量；本批不新增自动等厚判断器、线段最近点或世界重拟合。
- 仍为唯一IVisionEngine的公共无SDK操作，未增加执行服务或厂商回退；未加载数值包，不准备假模型，不触及原生所有权。Core只保存强类型几何/结果与有限性/来源/数值一致性守卫。

### 关键验证

- 初始10项均因未接入公共操作失败，0通过，不是编译红灯。接通后曾有一次37项联合运行120秒超时，未形成完成摘要，未作为成功记录；拆分公共9项能结束（8通过、1极端夹具前置失败），单独真实OpenCV用例1项通过，未复现原生阻塞。
- 已定位的夹具问题：极端远坐标下，定义线跨度100px被拟合矩阵微小交叉项放大的大平移压塌，测不到目标“距离溢出”。仅将该例定义方向改为可表示长跨度，仍断言有效映射直线成功、随后的不可表示垂距Failed且Value为空，没有放宽生产守卫或降低目标断言。调整后联合37项通过；额外补倾斜世界线后最终97项通过。首次超时原因未确定，未声称已修复或由夹具失败造成。
- 无SDK边界fake证明：像素→世界为x_mm=0.1*x+0.05*y+10、y_mm=0.2*y-5，横向边界与上方10px点的世界垂距2mm，但扫描轴端点世界距离sqrt(4.25)约2.06155mm，分别保存来源/标定并接毫米OK/NG。
- 独立倾斜方向期望：x_mm=0.1*x+0.05*y、y_mm=-0.02*x+0.2*y，像素线方向(100,100)映射世界方向(15,18)，像素点(0,10)映射相对位移(0.5,2)，垂距21/hypot(15,18)，不是仅取世界Y差或缩放像素垂距。
- 覆盖无限线视野外点/反向与反射、合法0及小于1e-9mm的有效定义方向、错空间/混帧/混标定拒绝、非有限/压塌直线、非共线假0与距离溢出无Value。
- 真实OpenCV合成128×128条带，卡尺直线实际拟合上边缘y=29.5，完整边缘对另一边y=39.5；明确正交毫米平面配方x_mm=0.1*x+0.05*y、y_mm=0.2*y。来源上下文关闭后，从像素拟合快照生成世界线，点线垂距2mm，扫描轴点距约2.06155mm；垂距区间[1.99,2.01]为OK，[2.05,2.1]为NG。理想平行条带下该垂距等于法向厚度，配方参考不是实际标定板或工件验收。
- 最终Passed: 97, Failed: 0, Skipped: 0：本批11、二维仿射17、比例10、定位跟随6、公共几何15、生命周期15和Core契约23。没有默认运行完整旧测试工程、添加工程、排除生产源码或恢复旧接口。
- OpenCV/传统HALCON/HALCON AI关联增量构建通过；Core/Runtime空白及IDE0011验证通过。构建警告以对应日志为准；增量0警告不代表第21节106个Extension依赖警告已修复，测试直接编译CS1701统一警告另行保留。

仓库根实际验证命令：

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-t4-world-lines.ps1" -Phase green -Filter 'FullyQualifiedName~CalibratedWorldLineEngineTests|FullyQualifiedName~AffineCalibrationEngineTests|FullyQualifiedName~PixelScaleCalibrationEngineTests|FullyQualifiedName~CaliperLocationFollowTests|FullyQualifiedName~GeometryMeasurementEngineTests|FullyQualifiedName~VisionEngineLifetimeTests|FullyQualifiedName~VisionEngineContractTests'
```

日志：`%TEMP%/vision-tools-t4-world-lines-{red,green,green-retry,common-probe,native-probe,focused-final}.log`及`vision-tools-t4-world-lines-*-final-build.log`。TRX：`Tests/obj/editor-verification/t4-world-lines-tests/results/`。初次green超时没有成功TRX，不能将此前red文件或后续覆盖结果冒作该次完成证据。

### 未交付/未验范围

非等比圆/椭圆尺寸、一般边界等厚/多点统计、世界欧氏重新拟合、透视/畸变、跨派生图映射及外层迁移仍待后续。真实工件精度、物理平面/光学适用性、外推、节拍与长期运行均未验；没有新HALCON原生、真实AI模型、UI/宿主或设备验收。MachineApp未触及，原有458项暂存不变。

## 24. T5首批：RGB闭区间分割→二值Blob→数量/面积判定

### 已交付与范围

- `RgbColorRange`以六个字节明确R/G/B各0..255编码值闭区间，每通道下限不得超过上限；三个通道同时满足才命中。`RgbColorSegmentOperation`输入独立颜色帧及可选固定SearchRegion，不准备模型。
- 当前OpenCV仅声明Rgb24/Bgr24，按明确存储顺序重排RGB上下限，实际Cv2.InRange处理三个通道；不转灰、不线性化gamma、不猜白平衡/ICC/照明，不以RGB名称泛指HSV/Lab或感知色差。
- 固定ROI使用Core像素中心栅格，构造后冻结、不受原可编辑ROI变化影响。域外始终0，即使域外黑/白也满足范围；空域拒绝。输入预算2000万像素，逐行复制忽略padding；这不是原生内存硬隔离。
- 复用`BinaryImageOutput`，FrameId指直接颜色输入，Image为不同FrameId、同尺寸/像素空间的独立Mono8 0/255快照；临时Mat在真实调用结束后释放。全黑掩膜仍Success，不把它当NoMatch。
- 二值输出在来源上下文关闭后可继续送入`BlobOperation(image,255,255)`，必须明确255前景，不把0背景一起计为对象。空Blob为NoMatch空快照，0数量判定仍可成功；NG仍Success。
- Mono8/Mono16/Float32/Bgra32均明确UnsupportedPixelFormat，不猜浮点范围或静默忽略alpha。调用方可另选已支持的显式转换；HALCON未声明本功能，缺能力时不会回退OpenCV。
- 本批只有固定输入ROI。OpenCV模板当前为Mono8，灰度派生图与原RGB帧是不同实际快照；没有借“同空间GUID”放松定位发布账本来假装颜色跟随。一般跨分支显式映射仍需后续契约。
- 编码值范围判定不等于颜色一致性认证、材料缺陷或真实配方验收；拍摄/照明、白平衡和阈值在真实样本上的适用性仍须确认。读码、HSV/Lab/色差、显式双图差分与OCR/OCV仍未交付。

### 验证证据与关联构建阻塞

- 首轮11项9失败、2守卫通过；5项分割/闭环因缺能力Unsupported，4项格式诊断尚未接入，未将编译失败当业务红灯。接通后新增11项全绿。
- 实际OpenCV在Rgb24/Bgr24两种存储的同一8色、带5字节行尾padding场景，验证RGB各闭区间边界与三通道AND；不修改原颜色输入，输出新帧/原尺寸/原空间。固定ROI全范围测试明确矩形Pose是中心，参与x=1,2及y=0,1，不能误当左上角。
- 实际12×8 BGR合成图含两个2×2目标红区域和不匹配蓝背景，分割并关闭来源上下文后Blob数量2、像素面积各4；再关闭检测上下文，托管快照完成数量2 OK、数量3 NG及显式一基区域面积4 OK。
- 全黑掩膜Success→Blob NoMatch空集合→0计数判定Success；覆盖4类不支持格式、范围倒置/空范围/空ROI拒绝及无颜色能力的冻结后端不调用SDK/不回退。
- 最终Passed: 70, Failed: 0, Skipped: 0：本批11、Blob闭环14、固定域/图像来源7、生命周期15、Core契约23。没有默认运行全部旧消费者/测试、添加工程、排除生产源码或恢复旧API。
- OpenCV及其关联Core/Runtime最新实际源码构建通过，0警告0错误。Core/Runtime/OpenCV空白和IDE0011验证通过；测试直接编译另有已说明的CS1701统一警告。
- **传统HALCON及HALCON AI本批关联构建均未通过**：`NETSDK1004`，缺少验证产物目录中`obj/Extension.Licensing/project.assets.json`。当前Extension依赖图已引入该授权工程，本批没有修改授权项目、ProjectReference或包版本，没有擅自扩大还原/授权范围，也没有把失败说成RGB业务回归。
- 隔离测试脚本仍复用既有HALCON/AI引用缓存；选中公共边界fake验证不能代替本批当前依赖图的HALCON/AI编译，更不能作为新HALCON原生或真实AI模型证据。需授权依赖资产就绪后再补关联构建；保留失败日志而非用旧成功覆盖。

仓库根实际测试命令：

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-t5-rgb.ps1" -Phase green -Filter 'FullyQualifiedName~RgbColorSegmentEngineTests|FullyQualifiedName~BlobWorkflowTests|FullyQualifiedName~ImageProcessingDomainTests|FullyQualifiedName~VisionEngineLifetimeTests|FullyQualifiedName~VisionEngineContractTests'
```

日志：`%TEMP%/vision-tools-t5-rgb-{red,green,focused-final}.log`及`vision-tools-t5-rgb-*-final-build.log`，HALCON/AI日志明确保留失败；TRX：`Tests/obj/editor-verification/t5-rgb-tests/results/`。SDK10.0.401、.NET9及OpenCV4.11.0.20250507保持原版本，没有新增测试工程。

### 未验范围

真实颜色工件/照明鲁棒性、颜色质量、长期节拍、HSV/Lab/色差、读码及外层迁移未验；基础双图差分后续见第25节。关联HALCON/AI构建受上述资产缺失阻塞，没有执行新HALCON原生、真实AI、UI/宿主或设备。MachineApp未触及，原有458项暂存不变。

## 25. T5第二批：显式对齐双图差分→缺陷候选Blob→计数/面积判定

### 已交付与对齐边界

- `AlignedImageDifferenceOperation`明确Current（主Image）和Reference两张不可变Core快照，均须同尺寸、格式与像素空间。不同快照不能重用同一FrameId；同一个对象作两输入则合法差异0。没有全局背景、上下文缓存背景或隐式第二输入。
- 同空间/尺寸仅表示调用方声明已对齐，不证明图案配准、相机/光学条件或物理对象相同。当前不做warp、缩放、配准、归一化或光照补偿；不同空间/尺寸/格式须先明确处理，不能仅按相似图像猜映射。
- `ImageDifferenceDirection`明确Absolute=|当前-参考|、CurrentBrighter=max(当前-参考,0)、CurrentDarker=max(参考-当前,0)。OpenCV只接同位深Mono8/Mono16，原编码值差异不隐式压为8位、有符号16位或比例归一化。
- 差异以正闭区间MinimumDifference>=1、Minimum<=Maximum筛为255，其余0；Mono8上限<=255，Mono16<=65535。反向差饱和0不回绕，也不会被当正向缺陷。固定SearchRegion冻结并按像素中心栅格，域外始终0、空域拒绝。
- `ImageDifferenceOutput`保留当前/参考帧身份、方向及原位深阈值，Image为独立新帧、同尺寸/像素空间的Mono8 0/255快照，身份不得冒充任一来源。Runtime发布前核验两来源、方向、阈值、尺寸/空间和完成状态，拒绝错背景或旧结果。
- 全黑仍Success，不是NoMatch。明确255前景送Blob，再做显式数量/一基区域面积判定；空Blob为NoMatch空快照，零数量判定可正常通过。差异候选不自动等于工件缺陷，NG仍Success。
- 每张图预算2000万像素；两输入/差异/二值临时Mat由本次调用拥有，真实调用结束前不释放或发布迟到结果，不将取消等待当作原生停止。输出不保留源Mat或背景所有权，可在差分和检测上下文关闭后继续判定。
- 本批不包含颜色差、SSIM、自适应/多帧背景、自动配准、模板缺陷、抗光照/振动或实际质量承诺。没有增加包、旧API壳或第二执行入口。

### 关键证据

- 首轮14项11失败、3输入/发布守卫通过；真实差分/格式诊断未接入的红灯不是编译失败。接通后14项全绿，未通过改弱阈值或偷用全局参考求绿。
- 6个参数化场景覆盖Absolute/CurrentBrighter/CurrentDarker×Mono8/Mono16；原位深20/3000阈值、同级/反向差、下限前一值、0对255/65535最大差，验证无有符号16位溢出、无无符号回绕、两图独立padding均忽略且原输入不变。
- 固定ROI冻结及20..40闭区间含边界，19与41排除、域外差异不计入。非法方向、零/倒置阈值、空ROI、Mono8阈值>255、尺寸/格式/空间不匹配及同GUID不同快照拒绝；颜色/Float32不自动转灰或归一化。
- 同一对象作为显式两图得到全黑Success→Blob NoMatch→0数量OK；参考切换为当前帧也得到全黑，证明请求使用本次明确参考而非旧背景缓存。边界fake返回其他ReferenceFrameId被发布前拒绝，Failed无Value。
- 真实OpenCV 12×8合成参考灰度100，当前有4px变亮150区、6px变暗50区和一处105噪声；绝对差20..100只保留两块区域。差分上下文关闭后Blob像素面积[6,4]，再关闭检测上下文，数量2 OK、数量0 NG及显式Index=1面积6 OK，均为托管快照可继续消费。
- 最终Passed: 84, Failed: 0, Skipped: 0：本批14、RGB11、Blob14、固定域/来源7、生命周期15及Core契约23。没有默认全量、增加测试工程、屏蔽生产源码或恢复旧消费者接口。
- 最新OpenCV及关联Core/Runtime构建通过，Core/Runtime/OpenCV空白和IDE0011验证通过。模块0警告与测试直接编译的已说明CS1701统一警告分开报告。
- **HALCON及HALCON AI关联构建仍未通过**：本批再次执行，均为NETSDK1004，验证产物目录缺`obj/Extension.Licensing/project.assets.json`。未修改授权工程/依赖或扩大还原范围；缓存HALCON/AI引用上的边界fake回归不是当前依赖图编译成功，更不是新原生/真实模型验收。

仓库根实际命令：

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-t5-difference.ps1" -Phase green -Filter 'FullyQualifiedName~AlignedImageDifferenceEngineTests|FullyQualifiedName~RgbColorSegmentEngineTests|FullyQualifiedName~BlobWorkflowTests|FullyQualifiedName~ImageProcessingDomainTests|FullyQualifiedName~VisionEngineLifetimeTests|FullyQualifiedName~VisionEngineContractTests'
```

日志：`%TEMP%/vision-tools-t5-difference-{red,green,focused-final}.log`与`vision-tools-t5-difference-*-final-build.log`；TRX：`Tests/obj/editor-verification/t5-difference-tests/results/`。仍为.NET9、SDK10.0.401和OpenCV4.11.0.20250507，未升级SDK/包或增加工程。

### 未验与阻塞

真实缺陷样本/工件精度、对齐与照明鲁棒性、节拍/长期运行、高阶颜色及外层迁移未交付或未验；单目标QR后续见第26节。HALCON/AI关联构建仍待授权依赖资产就绪后补验。没有新HALCON原生、真实AI、UI/宿主或设备验收；MachineApp未触及，原有458项暂存不变。

## 26. T5第三批：单目标标准QR→原图四角/三态读取→精确文本判定

### 范围与契约

- 先核验现有OpenCvSharp4.11.0.20250507绑定及原生包实际解码能力，再交付`QrCodeReadOperation`。当前只有Mono8单目标标准QR，不是MicroQR、一维码、多码集合或自动码制插件，不新增包或借HALCON回退。
- 整数矩形窗[x,x+Width)×[y,y+Height)位于原图、每边>=21像素；省略宽高取起点到右下剩余区域。原图最多2000万像素，单窗最多400万且每边<32767。窗口原样裁取，不以任意ROI遮罩补造QR静区，不隐式颜色/位深转换。
- 原生每次DetectAndDecode最多选中一个；多码场景不保证全码计数/稳定选择。`DetectedCount/DecodedCount`只表示本次单目标0/1，不是场景中全部QR数量，不声明MultipleMatches。
- `QrCodeReadOutput`明确NotDetected（无四角/无文本）、DetectedUndecodable（四角/无文本）、Decoded（四角/非空文本）。前两为NoMatch并保留结构化快照，原生异常/错帧/错完成状态/窗外错误几何为Failed无Value；找到框绝不等于解码成功。
- 四角按原生循环顺序保存，有限、凸、非退化；窗口坐标明确加x/y回原图像素中心，不加半像素、不把某点固定命名为图像左上角。发布边界1e-3像素容差仅防数值误差，不是精度声明。
- 文本为原生返回UTF-8解码字符串，最多8192个.NET UTF-16代码单元，超限失败不截断。不声明原始字节/ECI/GS1/FNC1、空载荷、结构化追加、纠错或质量等级。空字符串不能识别所有失败原因，不据此猜测设备/工件根因。
- `QrTextEqualsOperation`及`QrTextJudgmentOutput`通过同一IVisionEngine公共无图路径，Ordinal大小写敏感全文匹配，不trim/补空白/大小写或Unicode规范化。未解码和文本不等都为NG Success，保留读取快照和期望全文；不是第二执行入口或假SDK模型。

### 所有权

| 对象 | 创建/拥有 | 借用与结束 |
|---|---|---|
| Core输入帧 | 调用方独立不可变快照 | 请求在执行期间持有，不借原生指针 |
| 源Mat、窗Mat视图、QRCodeDetector | 本次OpenCV调用 | 窗借源Mat；原生返回/取消检查后按读取器→视图→源逆序释放，不共享读取器，不因取消等待提前释放 |
| 文本/Point2D四角快照 | 托管结果 | 不依赖SDK/上下文存活，来源关闭后仍可无SDK判定 |

预算不是原生停止或硬内存保证；真实检测未结束时既有Runtime继续等待，不把迟到结果发布。

### 关键验证

- 正式实现前现有原生包探针实际解出公开ASCII `VISION-42`、四角(32,32)..(199,199)；破坏数据区后原生仍返回4角但空文本。不是只查API存在或用fake冒充解码器可用。
- 测试使用独立公共编码器生成的固定21×21版本1模块矩阵、每模块8px/4模块静区，期望文本来自独立输入，不从生产解码结果反造期望。矩阵已固定进现有测试源码，运行离线、不请求网络；临时PNG/探针仅为前置证据，不是生产依赖。
- 首轮12项9失败、3参数/错误边界通过，真实读取/公共判定未接通；不是编译红灯。接通后补错NoMatch标志/窗口坐标发布守卫，共新增14项通过。
- 真实0°/90°QR解出相同全文和四角；明确窗口(40,20,232,232)返回原图角范围(72,52)..(239,219)，窗口外有QR也不偷读。关闭读取上下文后全文VISION-42为OK、vision-42为NG，均Success。
- 白图为NotDetected NoMatch且0/0空角；损坏数据为DetectedUndecodable NoMatch且检测1/解码0/保留四角，全文判定NG，不伪装旧文本。没有将损坏码称作原生执行异常。
- 拒绝格式隐式转灰/降位深、非法窗口、缺文本/坏四角与状态矛盾；fake证明精确全文判定不调用SDK、不trim空白、未读码NG仍Success。模拟原生异常、错来源、错完成标志和窗外（但图内）四角均Failed无Value。
- 最终Passed: 98, Failed: 0, Skipped: 0：本批14、双图差分14、RGB11、Blob14、固定域/来源7、生命周期15、Core契约23。没有默认运行完整旧测试工程、加测试项目、屏蔽生产源码或恢复旧API。
- 最后一次“构建+测试”组合命令曾60秒超时：构建已成功（21.08秒），测试日志尚无输出，未当作测试完成。单独执行既定测试命令重试后98项通过（测试执行约1秒）；未证明超时根因，也未声称修复原生阻塞，不使用旧TRX冒充该次成功。
- OpenCV及关联Core/Runtime构建、三模块空白与IDE0011验证通过；模块0警告和直接编译测试的已说明CS1701统一警告分开记录。使用的SDK/包版本不变。
- **HALCON及HALCON AI关联构建仍NETSDK1004阻塞**，缺`obj/Extension.Licensing/project.assets.json`，保留当前失败日志，未擅改授权项目、依赖或扩大还原范围。缓存引用上的边界fake不是当前依赖图编译或新HALCON/真实AI证据。

仓库根实际命令：

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-t5-qr.ps1" -Phase green -Filter 'FullyQualifiedName~QrCodeReadEngineTests|FullyQualifiedName~AlignedImageDifferenceEngineTests|FullyQualifiedName~RgbColorSegmentEngineTests|FullyQualifiedName~BlobWorkflowTests|FullyQualifiedName~ImageProcessingDomainTests|FullyQualifiedName~VisionEngineLifetimeTests|FullyQualifiedName~VisionEngineContractTests'
```

日志：`%TEMP%/vision-tools-t5-qr-{red,green,focused-final}.log`及`vision-tools-t5-qr-*-final-build.log`；前置探针：`%TEMP%/vision-qr-native-probe/probe{,-modules}.log`；TRX：`Tests/obj/editor-verification/t5-qr-tests/results/`。

### 仍待后续/未验

截至本节单目标批次，一维码/多码等不在范围；多QR返回集后续见第27节。MicroQR、二进制/ECI/GS1、质量等级、字符OCR/OCV及外层迁移仍待后续。真实刻印/低对比/曲面/反射工件、码版本/相机光学/畸变鲁棒性、长期节拍和其他部署解码器配置未验，不把固定版本1合成码当全部QR场景验收。没有新HALCON原生、真实AI、UI/宿主或设备验收；MachineApp未触及，原有458项暂存不变。

## 27. T5第四批：多QR返回候选→独立解码状态→计数/显式内容判定

### 已交付与数量边界

- `QrCodeReadManyOperation`为独立MultipleMatches能力，明确Mono8、整数矩形窗以及MaximumCandidates=1..32，默认8。图像/窗口预算沿用单QR，不新增包、码制插件或隐藏多码退回单码路径。
- 实际DetectMulti返回四角组，再逐组Decode，不以可能不等长的文本/几何数组zip截短。检测false应为空、检测true点数须为非零4倍数；异常/候选超预算失败无Value，不截断计数、不发布部分已解码结果。
- `QrCodeReadManyOutput.Candidates`复用同源`QrCodeReadOutput`，仅包含Decoded或DetectedUndecodable，每个文本/四角独立对应。相同文本不同位置保留；完全相同四角点集重复拒绝，不暗做文本去重或空间NMS。
- 几何按原图Ymin、Xmin、Ymax、Xmax排序，同键保留原生顺序。候选是明确零基下标、不是跨次跟踪身份或行归并结果；窗坐标回原图并在发布前核验，不能将原生不稳定次序冒称稳定工件ID。
- DetectedCount为本次原生返回候选数，DecodedCount为其中非空成功文本数；不是物理场景全部码数或“无漏码”证明。至少一个成功为读取Success，可同时有失败候选；全未解码或无候选为NoMatch，仍保留完整返回集而非清空未解码几何。
- `QrDecodedCountOperation`/`QrCountJudgmentOutput`为真正无图公共闭区间判定，只数成功解码；未解码不充数、NG仍Success，保留完整多QR快照。内容判定必须显式选择Candidates零基下标后用既有QrTextEqualsOperation，不默认首个成功、不按相同文本合并。
- 原生检测器/源Mat/窗视图由本次调用独占，逐候选解码之间检查取消；当前原生调用结束前不提前释放。任一原生异常/取消不发布部分成功计数，已完成托管快照在上下文关闭后仍可判定。
- 单目标API及文本/四角/UTF-8限制保持原义，不将本批扩为一维码、MicroQR、字节协议、质量等级或所有码版本/复杂工件验收。

### 真实能力与验证证据

- 实现前原生探针实际检测两个同VISION-42不同位置为8角并各自解码；6×6模块数据破坏仍返回2候选、其中1解码，8×8亦如此。但9×9或12×12破坏时DetectMulti只返回完好候选，不能由返回数量反推物理场景全码数；不借单码重扫悄悄补齐。
- 首轮10项7失败、3参数/发布守卫通过，未接入的多码/公共计数是业务红灯；接通后补全未解码原生场景，共新增11项通过。复用已有独立21×21固定模块夹具，仅开放测试内可见Scene helper，无运行时联网、无新测试工程。
- 真实520×256图同一文本两码返回2个独立位置，原图Xmin分别40和300，排序后来源帧/空间仍一致；关闭读取上下文，解码数2为OK、数1为NG，显式Candidates[1]全文匹配OK，未合并同文本。
- 混合场景检测2/解码1，未解码候选保留4角和null文本；全部6×6破坏时检测2/解码0、NoMatch但保留两组几何，零成功解码数量判定OK。空白则NoMatch空候选，不与“检测到但全不可解码”混淆。
- 最大候选1遇2候选时Failed无Value，不截取第一个或少报计数；明确窗(268,8,232,232)只返回该窗一候选，原图角Xmin300/Ymin40。Mono16不隐式归一化。
- 覆盖非法预算/窗口、跨帧来源/无检测占位/重复几何/未排序拒绝与集合冻结；fake证明计数不调用SDK、未解码不算成功，错NoMatch标志与超请求预算的后端快照拒绝发布。
- 最终Passed: 109, Failed: 0, Skipped: 0：本批11、单QR14、双图差分14、RGB11、Blob14、固定域/来源7、生命周期15、Core契约23。没有默认全量、添加工程、排除生产源码或恢复旧接口。
- OpenCV及关联Core/Runtime构建通过，三模块空白与IDE0011验证通过；交付模块0警告与直接编译测试的已说明CS1701统一警告分开报告。SDK和依赖版本保持不变。
- **HALCON及HALCON AI关联构建仍NETSDK1004**，缺`obj/Extension.Licensing/project.assets.json`，本批实际重新执行并保留失败日志；未改授权项目、依赖或扩大还原范围。缓存引用的边界fake不代替当前依赖图编译，不是新原生/真实模型验收。

仓库根实际命令：

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-t5-many-qr.ps1" -Phase green -Filter 'FullyQualifiedName~QrCodeReadManyEngineTests|FullyQualifiedName~QrCodeReadEngineTests|FullyQualifiedName~AlignedImageDifferenceEngineTests|FullyQualifiedName~RgbColorSegmentEngineTests|FullyQualifiedName~BlobWorkflowTests|FullyQualifiedName~ImageProcessingDomainTests|FullyQualifiedName~VisionEngineLifetimeTests|FullyQualifiedName~VisionEngineContractTests'
```

日志：`%TEMP%/vision-tools-t5-many-qr-{red,green,focused-final}.log`及`vision-tools-t5-many-qr-*-final-build.log`；原生多码探针`%TEMP%/vision-qr-native-probe/many-probe.log`及相关损坏记录；TRX：`Tests/obj/editor-verification/t5-many-qr-tests/results/`。

### 未验/阻塞

DetectMulti漏检边界、候选同键稳定性、真实工件/印码及复杂光学/畸变、码版本、长期节拍均不作验收承诺；场景完整性须独立验证。一维码/更多码制、字节协议/质量等级、OCR/OCV及外层迁移仍未交付；HALCON/AI关联构建仍待授权依赖资产就绪。没有新HALCON原生、真实AI、UI/宿主或设备验收；MachineApp未触及，原有458项暂存不变。

## 28. T5第五批：量化HSV8跨零分割→二值Blob→数量/面积判定

### 单位与无色相策略

- `HsvColorRange`冻结8位HSV编码闭区间：H=0..179（约2°/档）、S/V=0..255，不是连续角度、0..1浮点或H0..255 FULL编码。源只声明Rgb24/Bgr24，`HsvColorSegmentOperation`从明确RGB存储含义转换，不猜原始输入已为HSV。
- H下限<=上限取普通闭区间，H下限>上限明确取[下限,179]并[0,上限]，0..179为全色相；上下限相等仅该量化档，不自动整圈。S/V必须有序且同时满足，不允许它们借跨零语义倒置。
- S8=0为量化后无有效色相，原生H8填0不等于红色，默认从有色相分割中排除。IncludeAchromatic=true且S区间含0才额外允许S8=0像素按V条件参与，H不参与；最低S>0或V不符仍排除，不把开关当无条件全收。
- 原生RGB2HSV/BGR2HSV后分别阈值，跨零两段并集，再按显式策略合并无色相候选、按固定SearchRegion清零域外。空参与域拒绝，冻结不受编辑ROI变化；仅满足所有实际约束才输出255，约束无命中仍全黑Success。
- 输出复用BinaryImageOutput，新帧、同尺寸/像素空间、Mono8 0/255，可继续显式255前景Blob/判定；没有暴露带厂商类型的HSV Mat或伪造Core HSV图像格式。Mono8/Mono16/Float32/Bgra32不隐式推断颜色或丢alpha。
- 图像预算沿用2000万像素。源/HSV/色相分段/无色相/ROI临时Mat由本次调用拥有，真实调用结束后释放，取消等待不授权提前释放/发布；预算不是硬内存或原生中断保证。
- H8/S8/V8受8位转换舍入影响，不承诺连续色度或跨厂商数值/得分等价；不校正gamma、白平衡、ICC、照明或传感器响应。不是Lab/感知色差、颜色质量证书或真实材料缺陷验收，也不接跨灰度定位分支跟随。

### 验证证据

- 首轮11项10失败、1输入守卫通过；8项分割/闭环未接入和2项格式诊断未接入为真实业务红灯，不把中间实现编译错误当红灯。接通后新增11项通过。
- 真实Rgb24/Bgr24同一组RGB样本、含行尾padding，H175..5跨零同时包含纯红0、近红175与5端点，174/6及绿/蓝排除；原颜色输入不变，输出为独立Mono8原尺寸/空间。
- 非跨零H60..120精确包括绿/青/蓝，不误用角度或FULL编码；S255与V100..200闭边界正确，V99/201及近饱和S254排除。
- 默认无色相排除即使H范围包含原生填充0；显式开启H60绿域仍只按S/V允许灰/白，不把它们称为绿色。V最低100排除黑，S最低1仍排除无色相；S最大0且未开启策略得到全黑Success，不扩大约束求命中。
- 固定ROI冻结、参与域外0；真实12×8图中H175和5两侧红区域各2×2，在分割上下文关闭后Blob区域像素面积[4,4]、数量2 OK、指定一基面积4 OK、数量3 NG仍Success。
- 拒绝H180/错误S/V顺序/null范围/空域与不支持格式，不猜灰度/浮点输入或静默丢alpha。既有RGB夹具仅改为测试内可见以复用，没有新测试工程/框架或运行时网络依赖。
- 最终Passed: 81, Failed: 0, Skipped: 0：HSV11、RGB11、Blob14、固定域/来源7、生命周期15、Core契约23。按当前颜色变更选相关范围，没有默认扩大为全量或排除生产源码求绿。
- OpenCV及关联Core/Runtime最新源码构建0警告0错误；三模块空白与IDE0011验证通过。测试直接编译CS1701统一警告仍单独记录，SDK10.0.401/.NET9/OpenCvSharp4.11.0.20250507不变。
- **HALCON及HALCON AI关联构建仍NETSDK1004**：本批再次实际执行，缺`obj/Extension.Licensing/project.assets.json`。未修改授权项目/依赖或扩大还原范围，失败日志保留；缓存SDK边界fake不是当前依赖图编译或新原生/真实AI证据。

仓库根实际命令：

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-t5-hsv.ps1" -Phase green -Filter 'FullyQualifiedName~HsvColorSegmentEngineTests|FullyQualifiedName~RgbColorSegmentEngineTests|FullyQualifiedName~BlobWorkflowTests|FullyQualifiedName~ImageProcessingDomainTests|FullyQualifiedName~VisionEngineLifetimeTests|FullyQualifiedName~VisionEngineContractTests'
```

日志：`%TEMP%/vision-tools-t5-hsv-{red,green,focused-final}.log`及`vision-tools-t5-hsv-*-final-build.log`；TRX：`Tests/obj/editor-verification/t5-hsv-tests/results/`。

### 未验/阻塞

真实颜色工件、照明/白平衡/相机编码鲁棒性、量化阈值配方、长期节拍、Lab/感知色差、跨分支跟随及外层迁移未验或未交付；本节当时HALCON/AI资产缺失后续已按第29节解除。没有新HALCON原生、真实AI模型、UI/宿主或设备验收；MachineApp未触及，原有458项暂存不变。

## 29. 验证收口：同路径依赖还原、关联构建与新程序集回归

### 已确认原因，不是授权逻辑修复

- 第24～28节关联构建可重复失败于NETSDK1004：`Tests/obj/editor-verification/obj/Extension.Licensing/project.assets.json`不存在。只读核查发现默认Licensing/obj中已有资产，ProtectedData9.0.0也已在本机缓存，但独立验证目录没有为当前传递依赖图还原。
- 依赖为Vision传统HALCON/AI→Extension→Extension.Core→现有Extension.Licensing；授权项目本身已声明System.Security.Cryptography.ProtectedData9.0.0。工程没有需要修改的授权或恢复接口，本轮不是修授权策略/绕过校验，也没有为编译恢复旧API。
- 官方NETSDK1004说明要求restore生成资产，dotnet restore的artifacts-path须与后续build --no-restore一致。仅对两个现有Vision HALCON项目定向还原其已声明的传递图，不运行解决方案、不触及MachineApp、不加工程或更改包版本。
- 还原前保存7个受检csproj哈希、既有包身份/版本表与Git索引哈希。两次restore成功，独立目录资产生成；所有既有包身份/版本无删除或升级，只在HALCON/Extension图补入已声明ProtectedData/9.0.0。Core/Runtime包表无变化，7个工程文件及Git索引哈希不变。

### 实际命令与构建证据

仓库根执行，与此前构建保持同一独立目录：

```powershell
dotnet restore "Software Frame/Vision/Tools/Halcon/Vision.Tools.Halcon.csproj" --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --disable-parallel
dotnet restore "Software Frame/Vision/AI/Halcon/Vision.AI.Halcon.csproj" --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --disable-parallel
```

随后分别执行三项目原有构建命令，未放宽警告或排除源码：

```powershell
$projects = @(
    'Software Frame/Vision/Tools/Halcon/Vision.Tools.Halcon.csproj',
    'Software Frame/Vision/AI/Halcon/Vision.AI.Halcon.csproj',
    'Software Frame/Vision/Tools/OpenCv/Vision.Tools.OpenCv.csproj'
)
foreach ($project in $projects)
{
    dotnet build $project --no-restore --artifacts-path 'Software Frame/Vision/Tests/obj/editor-verification' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
    if ($LASTEXITCODE -ne 0)
    {
        throw "Build failed: $project"
    }
}
```

- Vision.Tools.Halcon：构建成功，0错误、106警告；警告来自Extension/Extension.Core依赖，不在本轮修复，不宣称所有模块0警告。
- Vision.AI.Halcon：构建成功，0错误0警告（同一图关联依赖已先构建）。
- Vision.Tools.OpenCv：构建成功，0错误0警告。
- 这仅是当前工程图编译成功，不代表授权程序、HALCON新原生算子或真实AI模型推理运行成功。未执行授权/宿主/UI/设备程序，当前SDK实测仍10.0.401，未创建或修改global.json。

### 用本次输出替换旧SDK引用缓存

- 在现有隔离xUnit/VSTest流程复制已构建模块时，同步其本次全部传递DLL到验证目录，不只替换Vision.Core/Tools主DLL后继续使用旧Extension图。
- 实测测试目录中的Vision.Core、Vision.Tools、Vision.Tools.Halcon、Vision.AI.Halcon、Vision.Tools.OpenCv、Extension.Core、Extension、Extension.Licensing这8个程序集哈希全部与本次实际构建输出一致。不是用旧HALCON/AI缓存覆盖关联构建失败来求绿。
- 选择已核查为SDK边界fake的HalconBackendTests及HalconClassificationBackendTests，不选HalconEngineSyntheticRuntimeTests或真实模型/设备测试；另跑Core契约/生命周期与当前OpenCV颜色、单/多QR、双图差分闭环。
- Passed: 96, Failed: 0, Skipped: 0：HALCON边界4、AI分类边界4、Core契约23、生命周期15、HSV11、单QR14、多QR11、双图差分14。
- 本轮无新增算法、测试工程或C#生产源码修改；没有默认全量、修改授权项目、升级包/SDK、移除守卫或扩大原生授权。fake不证明授权API或完整宿主部署已验收，真实模型与新HALCON原生仍未验。

实际测试命令：

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-verification-recovery.ps1" -Phase green -Filter 'FullyQualifiedName~HalconBackendTests|FullyQualifiedName~HalconClassificationBackendTests|FullyQualifiedName~VisionEngineLifetimeTests|FullyQualifiedName~VisionEngineContractTests|FullyQualifiedName~HsvColorSegmentEngineTests|FullyQualifiedName~QrCodeReadEngineTests|FullyQualifiedName~QrCodeReadManyEngineTests|FullyQualifiedName~AlignedImageDifferenceEngineTests'
```

日志：`%TEMP%/vision-tools-verification-recovery-{Tools.Halcon,AI.Halcon}-restore.log`、`vision-tools-verification-recovery-*-build.log`、`vision-tools-verification-recovery-tests.log`；基线：`vision-tools-verification-recovery-baseline.json`；TRX：`Tests/obj/editor-verification/verification-recovery-tests/results/verification-recovery-green.trx`。

第24～28节NETSDK1004是当时真实失败证据，原日志保留不覆盖；当前本地构建阻塞已解除。若清空独立验证目录或变更传递图，先按同路径还原，不能把默认obj资产当作该目录已具备资产。

MachineApp未修改、未构建、未验证；未启动宿主/授权/UI或真实设备，未执行新HALCON原生/真实AI。原有458项暂存及Git索引哈希保持不变。

## 30. T6首批：AI分类发布契约缺口复现与修复

### 缺口与最小修复

- 审查现有CPU分类发现，Runtime当时仅对AiClassificationOutput核验帧/空间与NoMatch数量一致性，没有逐项核验请求的MaximumResults/MinimumScore、唯一类别ID及排序。正常HALCON后端虽已筛选/排序，但错误或未来后端可将坏结果作为Success发布。
- 先使用真实引擎装配、上下文、模型准备/租约和最末端发布fake，构造低于阈值、超过数量、重复类别、分数升序4类输出；首轮4项实际失败，因为原引擎返回Success，另2合法边界通过。不是编译红灯，也不加载HALCON或真实权重。
- 仅在统一发布层新增分类检查：数量<=本次MaximumResults；各分数>=MinimumScore；ClassId按StringComparer.Ordinal唯一；分数非升序。违反任一项Failed无Value，不再筛选/重排/截断来掩盖后端契约错误，没有恢复旧API或新增判定服务。
- AiClassPrediction已有的有限0..1分数及ID守卫不改变；帧/空间、NoMatch一致性、租约/取消/资源所有权仍沿用原逻辑。只补发布边界，不改变HALCON原生算子或模型预处理。

### 合法结果与非承诺

- 最低分数含边界，0.8阈值下0.8合法；同分允许，保留后端原排序，不宣称唯一最优。类别ID是稳定字符串而非数组下标/数字，017与17按Ordinal可以是两个不同类别，不trim或数字化。
- 空保留预测仍为NoMatch空快照，不与坏结果失败混淆；完成的托管快照在来源上下文关闭后仍可读。
- 分数范围0..1不等于概率已校准，也不推断分数和=1、完整后验或未返回竞争类别。没有猜正常/缺陷类别、自动业务阈值、GPU回退或预处理配方。
- 当前HALCON CPU路径仍严格原尺寸Mono8、模型单通道且输入范围0..255；哈希核验和文件读租约、设备冻结不变。Vision范围检索未发现可用.hdl/.onnx实模型；测试临时abc/虚拟路径仅为边界fake，绝不作真实推理证据。

### 验证与构建

- 新增AiClassificationPublicationTests共6项：4坏结果先复现再变为Failed无Value；合法阈值/同分/Ordinal类别ID及空预测NoMatch共2项保持通过。
- Passed: 52, Failed: 0, Skipped: 0：发布6、HALCON分类边界4、传统HALCON边界4、Core契约23、生命周期15。未默认全量，未添加工程或排除生产源码。
- 使用第29节同路径还原后的当前工程图，重新构建传统HALCON、HALCON AI与OpenCV均成功。传统HALCON关联依赖106警告（Extension/Extension.Core）未修复，其余两个构建0警告；三者均0错误，不将测试直接编译CS1701警告混作模块摘要。
- Core/Runtime空白、IDE0011验证通过；测试使用本次新模块和传递DLL，不靠旧SDK缓存。准备/分类边界全部fake，没有执行新HALCON原生或真实AI模型，当前结果不算T6真实推理验收。

仓库根实际命令：

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-t6-classification-publication.ps1" -Phase green -Filter 'FullyQualifiedName~AiClassificationPublicationTests|FullyQualifiedName~HalconClassificationBackendTests|FullyQualifiedName~HalconBackendTests|FullyQualifiedName~VisionEngineLifetimeTests|FullyQualifiedName~VisionEngineContractTests'
```

日志：`%TEMP%/vision-tools-t6-classification-publication-{red,green,focused-final}.log`及`vision-tools-t6-classification-publication-*-final-build.log`；TRX：`Tests/obj/editor-verification/t6-classification-publication-tests/results/`。

### 后续验收前置

真实CPU推理需要提供实际模型（及内容指纹/版本）、已确认的模型输入/训练预处理配方、类别ID含义和代表性输入/期望，且满足既有授权边界；未满足前不猜缩放、归一化、类别业务规则或承诺精度/节拍。检测/分割/异常检测、GPU、更多厂商及UI/FlowNode/宿主迁移仍待后续。

本轮是普通正确性修复而非行为不变重构；没有包/SDK/授权逻辑变更，MachineApp未触及，未运行授权/宿主/UI/设备，原有458项暂存不变。

## 31. T6第二批：已完成分类快照→显式位置/类别/分数→OK/NG

### 离线可交付范围

- 用户未提供真实模型/输入配方时，继续只推进可离线验证的结果消费，不伪造或自动选择权重、不扩大HALCON原生授权。本批不执行真实推理或预处理。
- `AiPredictionJudgmentOperation`明确要求Source分类快照、PredictionIndex零基位置、ExpectedClassId及MinimumScore，四项无缺省业务规则。类别必须非空，位置非负，分数有限且在[0,1]。
- 仅比较指定位置：Ordinal类别相等且Score>=MinimumScore为OK，不trim、大小写/数字化ID或搜索其他位置，不自动改选首个成功类别。位置是源快照下标，不是类别ID或跨帧身份；下游不能恢复上游已过滤/截断的预测。
- 空源/缺少该位置为NG Success，ActualPrediction=null；类别不符/分数不足也是NG Success，保留实际指定预测而不是补位。非法参数仍构造阶段拒绝，不把绑定配置悄悄修成其他规则。
- `AiPredictionJudgmentOutput`保留Source、位置、期望类别、最低分数、实际预测或null及Passed；帧/空间来自源，结果构造与发布均核验规则一致性。源快照可在模型上下文关闭后继续消费，不依赖SDK存活。
- 属公共Vision执行域的无图无SDK数值判定，AI未配置也可消费完成快照；不准备假图/模型、不再次推理，不是AI Provider或第二业务入口。
- 该判定不证明权重来源/真实性、唯一最优、校准概率或真实工件质量；正常/缺陷含义与阈值由调用方明确，真实模型/输入/授权前置不变。

### 关键验证

- 首轮9项8失败、1非法参数守卫通过，公共判定未接入为Unsupported业务红灯；接通后新增9项通过，没有用编译失败或真实模型替代品冒充红/绿证据。
- 无AI/无SDK fake工厂，显式位置1取Other/0.85在0.85闭边界OK，源/实际预测/帧/空间完整保留；17不等于017，Part不等于part或Part加空白，位置0不改选位置1，即使那里有期望类别。
- 0.9小于0.9001为NG，空源位置0和单项快照位置int.MaxValue均NG Success且实际预测null，不报NoMatch、不自动补位。负位置、空类别、NaN/无穷/超范围阈值及矛盾Passed拒绝。
- 使用实际HalconClassificationBackend+最末端SDK fake，临时abc文件只用于已存在SHA/读租约路径，不是权重；CPU适配器将8/0.85、17/0.97按契约发布为17、8。关闭推理上下文后，位置0/期望17/最低0.97为OK，位置1/期望17为NG，不重新运行模型（Run总计1次），已释放fake模型保持释放。
- Passed: 61, Failed: 0, Skipped: 0：本批9、分类发布边界6、HALCON分类边界4、传统HALCON边界4、Core契约23、生命周期15。没有默认全量、增加工程、排除生产源码或恢复旧接口。
- 第29节已还原的同一artifacts路径上，传统HALCON/HALCON AI/OpenCV关联增量构建均成功、0错误0警告；此为本次增量摘要，不代表第29～30节依赖完整编译106警告已修复。Core/Runtime空白和IDE0011验证通过；直接编译测试CS1701警告另列。
- 使用当前SDK及传递DLL，不沿用旧引用求绿，没有包/SDK/授权逻辑修改。没有执行HALCON真实模型、新原生算子、授权程序或硬件。

仓库根实际命令：

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-t6-classification-judgment.ps1" -Phase green -Filter 'FullyQualifiedName~AiPredictionJudgmentEngineTests|FullyQualifiedName~AiClassificationPublicationTests|FullyQualifiedName~HalconClassificationBackendTests|FullyQualifiedName~HalconBackendTests|FullyQualifiedName~VisionEngineLifetimeTests|FullyQualifiedName~VisionEngineContractTests'
```

日志：`%TEMP%/vision-tools-t6-classification-judgment-{red,green,focused-final}.log`及`vision-tools-t6-classification-judgment-*-final-build.log`；TRX：`Tests/obj/editor-verification/t6-classification-judgment-tests/results/`。

真实模型、已确认输入配方与代表性样本/类别预期仍需另行提供并在既有授权边界内验收；不因这条fake闭环宣称T6真实推理完成。UI/FlowNode/宿主迁移、其他AI任务/设备仍待后续；MachineApp未触及，原有458项暂存不变。

## 32. 经用户确认：FlowNode新契约四节点首批迁移

### 范围及退役

用户明确确认进入FlowNode破坏性迁移，限定文件读取→阈值→Blob→数量判定，不运行UI/宿主，不恢复兼容壳。此节扩展之前Tools-only范围，仅修改Vision.FlowNode及其离线验证；Controls/Workbench仍不迁移，MachineApp不修改、不构建、不验证。

旧FlowNode26个C#文件全部依赖旧会话/结果或其配置绑定，基线构建因已删除Core类型失败。已将28个原源码/README/csproj文件按目录结构存档至 `Tests/obj/flow-node-before-engine-migration-hierarchy.zip`，SHA256清单见同目录 `flow-node-before-engine-migration-manifest.json`，再退役26个旧C#文件。保留原csproj及依赖，不加Compile排除、不恢复旧API、不删除旧有效测试。

### 新实现及所有权

- `VisionOperationNode<TResult>`复用FlowEditor.NodeModel、NodeInput/NodeOutput、自定义端口、INodeDocumentState；只调用借用的IVisionEngine，各节点延迟创建并拥有一个上下文，不拥有引擎/上游快照。
- 四个新稳定ID：`vision.engine.image-file`、`vision.engine.threshold-range`、`vision.engine.blob`、`vision.engine.blob-count`。不复用旧身份；现有FlowNodeCatalog/ReflectionNodeFactory注册IVisionEngine即可构造，未新增节点工厂或SDK引用。
- 文件只收明确绝对路径，默认作为无IN入口；阈值全图原编码闭区间，不猜转换；Blob显式收BinaryImageOutput，255前景、八连通、保孔、面积降序、1..1000预算，超限失败不截断；数量为无图闭区间判定。
- NoMatch空Blob保留快照并允许下游零数量判断；有效NG输出false且流程执行成功。Unsupported/失败/取消不保留旧结果，不换厂商。
- 执行不可重入、执行期间不能修改配置；修改配置先撤销结果和输出端口，再通知新配置。流程上下文入口严格读取本次端口（含null），不沿用SyncInputValues跳过null后的旧属性。文档仅含Version=1及明确配置，未知/缺失/额外字段和非法区间拒绝，不持久化图像、结果、引擎或上下文。
- 关闭不强行声称原生已停：等待当前方法返回，再调用引擎ReleaseContextAsync继续保证原生释放安全；关闭后的迟到成功不发布。关闭Task可共享，ValueTask每次只消费一次；清理失败保留上下文可重试。源节点关闭不销毁已发布托管快照。

### 验证与证据

- 完整FlowNode与OpenCV生产项目构建通过，增量各0警告/0错误；未变更包或框架版本，不表示历史106依赖警告已修复。
- 既有Workbench测试线新增 `Controls/EngineVisionNodeTests.cs`，由 `Tests/Verify-FlowNodeEngine.ps1`直接编译所选现有测试源码。脚本先完整构建生产模块，按当前FlowNode资产复制包DLL并合并当前deps，再同步新项目/传递DLL；不新增测试工程，不构建整个旧Workbench测试工程。
- 首轮12项中11通过、1失败：参数变更通知仍观察到旧结果；修复后12通过、0失败、0跳过。真实OpenCV离线独立8×8 PGM验证两区域OK、空区域OK、两区域NG；其他覆盖本次null输入、失败/配置失效、重入、关闭迟到结果、清理重试、取消成功晚到、Unsupported边界、四节点JSON/端口ID往返、旧配方拒绝。外层等待/清理故障由受控引擎转发器注入，不伪造视觉算法。
- 本次直接C#编译测试有4条CS1701统一引用警告，与生产模块构建警告分开。日志 `%TEMP%/vision-flow-engine-{baseline,first-build,red,green,final}.log`；TRX `Tests/obj/editor-verification/flow-node-engine-tests/results/`。
- 模型级链不等于画布调度、编辑器交互、宿主或真实工件验收。测试不创建Application/窗口，不执行新HALCON/真实AI；HALCON Blob仍Unsupported。Controls/Workbench旧API消费者、整任务资产文档、定位/标定等节点仍待后续。原有458项暂存及索引哈希保持不变。

复现：`pwsh -NoLogo -NoProfile -File "Software Frame/Vision/Tests/Verify-FlowNodeEngine.ps1"`。独立资产缺失时先对相同artifacts路径还原现有FlowNode/OpenCV项目；脚本需既有xUnit/VSTest验证缓存，不以缓存旧生产DLL求绿。

## 33. 用户调整顺序：先收口Tools，Golden与性能测试首批迁移

### 当前执行范围

用户要求先修改Tools，Controls、Workbench、Nodes后续再改。从此批起暂缓外层迁移，只修改Tools及其必要测试/文档，不扩大新HALCON原生、真实AI、UI、宿主或设备运行范围；MachineApp继续不修改、不构建、不验证。

完整 `Tests/Vision.Tests/Vision.Tests.csproj` 基线构建实际失败：309个编译错误，主要引用已退役的VisionImage/Tool/Provider/结果及旧架构契约；依赖链产生既有106警告。此为测试迁移阻塞，不能用运行筛选解决编译，也不通过Compile排除或删除有效测试求绿。

### 本批修改

- 仅重写现有 `TestAssets/GoldenImages.cs`、`TestAssets/GoldenRegressionTests.cs`、`Performance/BaselineTests.cs` 三个文件，原6项Golden及2项性能记录全部保留，不新增测试工程、不更改生产算法来迎合旧签名。原三文件快照 `Tests/obj/tools-test-migration-golden-before.zip`。
- Golden工厂改为直接生成不可变VisionFrame，不改变矩形600像素/质心、3行白带、整数像素圆盘及模板粘贴图的原像素构造和解析真值；不改仍供其他旧测试使用的SyntheticImages。
- 六项回归统一走IVisionEngine：阈值白点600、Blob面积/质心/框、3×3腐蚀面积18×28=504、显式零原点模板定位、卡尺直线与圆拟合。保留原位置/半径容差，不录制当前算法输出反算黄金。
- 旧找线宽容差只约束白带中心附近，新测试保留该范围并明确选择Forward/DarkToLight下侧边缘y=101.5，不能把卡尺边缘冒充带中心线；旧找圆图改用已交付圆卡尺，新增覆盖角检查，不宣称旧Hough算法等价或真实精度。
- 性能用Stopwatch记录单次ExecuteAsync端到端时间，不含引擎装配/模型准备、不预热、不设时间阈值，也不与旧Tool.ElapsedMs作同口径比较。流水线真正把阈值二值派生帧交给Blob，而非再次读原图。

### 已执行与仍未完成

- 迁移前定向编译明确复现旧VisionImage缺失（4条CS0246），属于编译红灯，不冒充8项算法测试失败。迁移后原8项全部通过，0失败/0跳过。
- 合并当前卡尺直线11、圆卡尺13、预处理12、生命周期15项，共59通过、0失败、0跳过；实际OpenCV合成图，不是fake算法结果。没有运行新HALCON/真实AI。
- 三个Tools相关模块再次构建通过，增量各0警告/0错误；不表示完整依赖106警告已修复。直接编译所选测试有6条CS1701引用统一警告，与生产构建分开。
- 完整旧测试工程再次构建仍失败：305个编译错误，无本批三个文件诊断；本批没有加源码排除，没有声称完整工程通过。后续继续按有效需求迁移其他OpenCV测试、旧契约/架构守卫及HALCON/AI边界测试；未支持算法仍按需求补入，真实Runtime守卫保留。
- 暂存项仍458，索引哈希未改变；外层和MachineApp本批未修改。

日志：`%TEMP%/vision-tools-full-tests-{baseline,after-golden}.log`、`vision-tools-test-migration-golden-{red,focused,related}.log`及同前缀三个模块build日志。TRX `Tests/obj/editor-verification/tools-test-migration-golden-tests/results/tools-test-migration-golden-green.trx`对应最后相关59项；此前聚焦8项证据在focused日志，不把覆盖后的TRX当成旧运行证明。

复现定向相关验证（现有.NET9/xUnit/VSTest独立资产，先使用当前三模块及全部传递DLL）：

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-test-migration-golden.ps1" -Phase green -Filter 'FullyQualifiedName~GoldenRegressionTests|FullyQualifiedName~BaselineTests|FullyQualifiedName~CaliperLineEngineTests|FullyQualifiedName~CaliperCircleEngineTests|FullyQualifiedName~ImagePreprocessingTests|FullyQualifiedName~VisionEngineLifetimeTests'
```

## 34. Tools测试收口第二批：图像复制边界及真实文件解码

继续Tools优先，不修改Controls/Workbench/Nodes或MachineApp，不运行UI/宿主、HALCON新原生或真实AI，不恢复旧接口。

### 本批变更

- 迁移原 `Tools/OpenCv/MatConverterTests.cs` 两项有效安全需求到当前内部 `OpenCvFrameAdapter`：尾随字节不得进入图像、短缓冲不得进入原生访问。旧的“构造后缩短可变数组”在新契约中不可能，改验证构造时拒绝短缓冲、构造后修改调用者数组不影响快照及Pixels无setter。
- 适配器测试扩展为26个数据案例，覆盖Mono8/Mono16/RGB/BGR/BGRA/Float32的原始存储、行尾padding、Core与Mat双向复制隔离、非连续Mat视图逐行输出及原生对象修改/释放后的快照有效性；灰度专用复制拒绝颜色/浮点，空/存储类型不符输出及未定义解码类型明确拒绝。
- 仅新增OpenCV程序集 `Properties/AssemblyInfo.cs` 的 `InternalsVisibleTo("Vision.Tests")`，复用既有逻辑测试程序集检查实际SDK复制边界。没有公开Mat、恢复MatConverter或新增业务执行入口；未修改算法实现/包版本/项目文件。
- 迁移 `Tools/OpenCv/ImageFileToolTests.cs` 原9项案例到IVisionEngine无图文件请求：中文空格PNG、奇数宽/四种存储格式、Mono16 TIFF不归一化、绝对路径冻结与每次重新读取、独立SHA256核对、新帧/空间身份、源上下文关闭/文件删除后快照可读、空/缺失/损坏文件无Value、真正图像操作拒绝null输入。
- 旧测试曾一概拒绝浮点TIFF；当前明确存储契约已支持Float32，因此改为精确保存Float32原值，16位多通道仍Unsupported且不降位深。没有为保持旧断言反向删除已交付能力。旧可变图像/工具Dispose测试转为不可变请求与上下文生命周期，不恢复旧对象。
- 原两文件快照位于 `Tests/obj/MatConverterTests-before-migration.cs`、`ImageFileToolTests-before-migration.cs`（忽略目录，不参与编译）。

### 验证及未完成项

- 先定向编译旧文件，分别复现已删除OpenCv.Internal、Core.Tools/OpenCv.Acquisition命名空间错误；是编译红灯，不是原生算法测试失败。
- 迁移后适配器26项及文件9项，聚焦35通过、0失败、0跳过。联合上批59项共94通过、0失败、0跳过，使用实际OpenCV内存/PNG/TIFF，不以fake证明这些边界。
- 三模块最终增量构建0警告/0错误；直接C#编译测试7条CS1701引用统一警告，分开记录，不表示历史106依赖警告已解决。定向whitespace与IDE0011检查通过。
- 完整旧测试工程仍302个编译错误（上批305），本批两文件已无编译诊断；未增加Compile排除、未删除有效测试、未恢复旧API，不能称完整测试工程通过。
- `BasicImageToolsTests`等旧用例中的直方图/原位深中位数、颜色通道提取等仍有有效需求尚未以新契约接通，后续应按明确功能补请求/结果/支持矩阵，不能通过删断言假装迁移完成。
- 原有458项暂存及Git索引哈希保持不变；无新HALCON原生、真实AI、设备、UI或宿主验收。

日志：`%TEMP%/vision-tools-test-migration-adapter-{red,file-red,focused,focused-final,related}.log`、同前缀三个模块final-build日志；完整测试构建日志 `vision-tools-full-tests-after-adapter.log`。TRX `Tests/obj/editor-verification/tools-test-migration-adapter-tests/results/tools-test-migration-adapter-green.trx`对应最后94项，35项聚焦证据在focused-final日志。

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-test-migration-adapter.ps1" -Phase green -Filter 'FullyQualifiedName~MatConverterTests|FullyQualifiedName~ImageFileToolTests|FullyQualifiedName~GoldenRegressionTests|FullyQualifiedName~BaselineTests|FullyQualifiedName~CaliperLineEngineTests|FullyQualifiedName~CaliperCircleEngineTests|FullyQualifiedName~ImagePreprocessingTests|FullyQualifiedName~VisionEngineLifetimeTests'
```

## 35. Tools有效需求补齐：原位深灰度直方图及未量化中位数

第34节识别出旧BasicImageToolsTests中的直方图/中位数属于有效未接功能，本批明确补入，不通过删断言迁移。继续Tools优先；仅增加必要Core契约和Tools实现/验证，Controls、Workbench、Nodes和MachineApp均不修改，不运行UI/宿主、新HALCON原生或真实AI。

### 契约与实现

- 独立 `GrayHistogramOperation` / `GrayHistogramOutput`，Id `vision.measurement.gray-histogram`，图像必需、Model=null；不扩充GrayStatistics默认请求/结果，保持已交付HALCON统计范围和已有fake契约不变。
- 明确Mono8/Mono16原编码；箱宽分别1..256/1..65536，箱i为[i×宽,min((i+1)×宽−1,编码上限)]，尾箱可以较窄。Bins为只读long数量，非概率/密度；PixelCount等于计数和。
- Median基于完整256/65536灰度频次的顺序样本，奇数取中间值、偶数取中间两原值均值；分箱不量化Median，箱中心不能冒充原位深中位数。不转换颜色、浮点或对本图做归一化。
- 冻结SearchRegion只统计参与像素，忽略域外和padding，Mono16按小端读取；空参与域拒绝，全黑参与域仍Success且Median=0。输出保留本次输入帧/空间，不创建派生图。
- OpenCV选中模块 `OpenCvBackend.Histogram.cs` 内实现托管整数计数内核，避免SDK浮点计数不能精确表达超过2^24的单箱数量；不宣称SDK CalcHist算法，未迁至通用Runtime或借其他厂商回退。最多2000万输入像素、原始数组最多65536个long元素，逐行及长行4096采样间隔检查取消，发布仍走引擎取消/关闭守卫。
- 能力、参数、执行、输出来源及发布均接通；Runtime拒绝错帧/空间、格式、箱宽、参与数量和NoMatch矛盾，Core复制计数并拒绝负数/零总数、箱形状错误及与分箱范围矛盾的中位数，不修补坏结果。
- HALCON未声明此独立操作，继续Unsupported；原灰度统计仍可用，不把OpenCV模块内的托管内核当公共回退。当前模板模型在直方图前后仍有效，不准备假模型或撤销模型。

### 先红后绿与验证边界

- 先只加契约和测试、未接能力/实现：初始22项实际运行16失败、6通过。红灯包含Unsupported能力缺口和未约定输出/发布守卫缺口；日志保留，非仅编译失败。
- 接通后初始22项全通过；另加尾箱最高编码、2^24+1精确计数及当前模板保留，最终新增25项。联合既有灰度统计、ROI处理、预处理、生命周期、HALCON/分类SDK边界fake及Core契约，共93通过、0失败、0跳过。
- 实际验证Mono8/16原位深分箱/中位数、padding、固定圆ROI黑像素、全灰度合箱仍精确中位数、65534.5与127.5偶数中位数、16,777,217个零值的精确计数及2000万预算拒绝。OpenCV模块实际执行的托管像素算法不称SDK原生直方图；HALCON只验证适配器/fake不调用，不运行新原生或AI模型。
- 三模块最终增量构建0警告/0错误，Core/OpenCV/Runtime whitespace与IDE0011及定向测试格式检查通过；测试直接编译7条CS1701引用统一警告另记，不表示历史106依赖警告已修复。
- 完整旧测试工程依旧302个旧API编译错误，无新增直方图文件诊断，尚未迁移完毕。通道提取等其他有效旧需求和BasicImageToolsTests完整迁移仍待后续；本批没有排除/删除旧测试或恢复旧接口。458项暂存及Git索引哈希保持不变。

日志：`%TEMP%/vision-tools-histogram-{red,green,related,final}.log`及同前缀三模块final-build日志；TRX `Tests/obj/editor-verification/histogram-tests/results/histogram-green.trx`对应最终93项，旧22项及中间92项证据在各自日志，不用覆盖后的TRX证明旧运行。完整测试构建日志 `vision-tools-full-tests-after-histogram.log`。

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-histogram.ps1" -Phase green -Filter 'FullyQualifiedName~GrayHistogramEngineTests|FullyQualifiedName~GrayStatisticsEngineTests|FullyQualifiedName~ImageProcessingDomainTests|FullyQualifiedName~ImagePreprocessingTests|FullyQualifiedName~VisionEngineLifetimeTests|FullyQualifiedName~HalconBackendTests|FullyQualifiedName~HalconClassificationBackendTests|FullyQualifiedName~VisionEngineContractTests'
```

## 36. Tools通道提取及旧基础图像测试完整迁移

继续Tools优先，不修改Controls/Workbench/Nodes或MachineApp，不运行UI/宿主、新HALCON原生或真实AI。第34～35节保留的通道提取有效需求本批补齐；不恢复旧GrayConversion/DTO/PrepareRun会话。

### 生产能力

- 增加Core `RgbChannel`（Red/Green/Blue语义，不是内存下标）及独立 `ColorChannelExtractOperation`，Id `vision.processing.color-channel`，Model=null。复用强类型ImageTransformOutput，不另造结果字典或业务入口。
- OpenCV实际调用ExtractChannel：Rgb24按R0/G1/B2，Bgr24/Bgra32按R2/G1/B0；明确忽略BGRA的alpha，不与背景合成，不计算亮度或归一化。输出独立Mono8帧、原尺寸/像素空间，保留输入FrameId来源，忽略输入padding。
- 只支持明确的三种8位彩色格式及最多2000万输入像素；Mono8/16及Float32明确Unsupported，不将灰度冒充RGB，不自动裁剪/缩放/截断。HALCON未声明此能力，仍Unsupported且不回退。
- 能力、格式/预算校验、执行和发布均接通；Runtime按Mono8及原尺寸核验新图像变换，保留源帧/空间与完成状态守卫；取消/关闭仍等实际原生调用返回后再决定发布。

### 有效用例迁移

- 原 `Tools/OpenCv/BasicImageToolsTests.cs` 18个数据案例的有效需求全部迁至新请求：亮度转灰、通道原值、显式Mono16 gain/offset、独立紧凑输出、均值/高斯/中值核与边界、原位深总体统计/直方图/中位数、精确ROI、坏输入和快照生命周期。原文件保存在 `Tests/obj/BasicImageToolsTests-before-migration.cs`，未批量删除或排除旧测试。
- 原R/G/B三个案例扩成三种彩色格式×三个语义通道，含padding、完全透明/不透明alpha及来源关闭后可读，原基础文件现24项。保留亮度红/蓝76/29、线性映射128、独立滤波核数组、原位深均值/总体标准差等解析期望，不用当前输出录制黄金。
- 不可变VisionFrame不再有可变Pixels、Timestamp或旧工具Dispose；改为验证调用者数组/配置变量/原ROI变化不影响冻结请求、已释放上下文拒绝新执行、产物在另一上下文继续消费。新产物有独立FrameId，来源FrameId及空间明确，不维持旧的同帧可变输出。
- 新 `ColorChannelExtractEngineTests` 13项含真实黑通道Success、灰度/浮点拒绝、HALCON末端fake无调用和错帧/空间/尺寸/格式/NoMatch发布拒绝。另以真实2×2 RGB→红通道→原位深直方图Median=105→阈值→面积2 Blob→面积闭区间OK形成任务链，通道生产上下文关闭后仍可消费。

### 验证与边界

- 契约和迁移用例先运行：36项中15失败、21通过，复现未接通道能力及发布映射。接通后36全通过，再增加上述任务链，最终聚焦37通过、0失败、0跳过；合并直方图、文件/格式转换、预处理、统计/ROI、生命周期及Core契约共130通过、0失败、0跳过。
- 三模块最终增量构建0警告/0错误，定向whitespace及IDE0011检查通过。直接C#编译测试仍有7条CS1701引用统一警告，分开记录，不代表历史106依赖警告已修复。
- 完整旧测试工程仍292个旧API编译错误（上批302）；本批两个测试文件无编译诊断，没有Compile排除、不恢复旧壳、不声称完整工程通过。其余旧Tool/Provider/几何与架构测试仍须后续迁移。
- 原458项暂存及Git索引哈希不变，未执行真实工件/精度、UI或宿主验收。

日志：`%TEMP%/vision-tools-color-channel-{red,green,focused-final,related,final}.log`及同前缀三模块final-build日志。TRX `Tests/obj/editor-verification/color-channel-tests/results/color-channel-green.trx`为最终130项；37项证据保留在focused-final日志。完整测试构建日志 `vision-tools-full-tests-after-color-channel.log`。

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-color-channel.ps1" -Phase green -Filter 'FullyQualifiedName~BasicImageToolsTests|FullyQualifiedName~ColorChannelExtractEngineTests|FullyQualifiedName~GrayHistogramEngineTests|FullyQualifiedName~ImageIoConversionTests|FullyQualifiedName~ImagePreprocessingTests|FullyQualifiedName~GrayStatisticsEngineTests|FullyQualifiedName~ImageProcessingDomainTests|FullyQualifiedName~VisionEngineLifetimeTests|FullyQualifiedName~VisionEngineContractTests'
```

## 60. 相邻Gamma与一维码前置核对

- 对当前Gamma实现的 `floor(value+0.5)` 作定点探针：Mono8全部输入值/目标半整数附近与Mono16部分目标半整数、全部非端点输入值的采样未找到与 `Math.Round(...AwayFromZero)` 不同的例子；首次PowerShell循环120秒超时，改为托管数值探针完成。采样不是完整double指数空间证明，不因此宣称Gamma没有同类边界问题，也未改Gamma。
- 读取当前项目引用与本机OpenCvSharp 4.11.0.20250507程序集导出类型及XML，找到QRCodeDetector但未找到名称含Barcode/BarCode的导出类型。此探测只说明该托管程序集的类型名称，不证明本机所有原生实现或其他SDK绝无读码接口；未安装、升级、探测新HALCON原生或增加包。
- 一维码具体码制（例如Code128、EAN、Code39）、数量和工件样本尚未确认，不能把QR冒称一维码或自行引入外部依赖；待用户明确需求和授权后选实现。真实AI模型及HALCON原生前置仍未满足。
- 本节为离线数值/程序集检查和文档更正，没有源码修改，也未运行测试、真实设备、UI或宿主。Tools尚不能据此宣布全部完成。

## 59. 灰度线性半向上浮点中点修正

显式半向上线性内核原先使用二进制浮点 `floor(value + 0.5)`：当实际double为0.5的前一个可表示值时，加法先四舍五入成1，错误进位。实际引擎及OpenCV后端定向测试取得1项失败（期望0、实际1）；改为对已限幅非负double使用 `Math.Round(..., MidpointRounding.AwayFromZero)`，验证中点前一位→0、中点/后一位→1。默认SDK最近整数路径不变，不改变Gamma或宣称所有其他数值路径已审计。

线性、图像转换、基础处理、Gamma/归一化及生命周期相关105通过、0失败、0跳过；OpenCV增量构建通过，日志 `%TEMP%/vision-linear-midpoint-{red,green,build}.log`。未跑真实AI、HALCON原生、设备或完整旧测试工程。第58节另更正上一轮描述：真实HALCON Runtime此前已检查原始数字ID重复，后端补充的是纵深防御，不是已证实真实SDK生产回归。

## 58. 分类原始类别重复不能被过滤掩盖

更正：真实 `HalconClassificationRuntime.NativeModel.Run` 在转换SDK输出时已检查原始数字类别ID的数量、非空与唯一性；因此不能据此宣称真实HALCON重复类别会漏过。末端SDK fake可绕过该Runtime检查：发布层虽拒绝重复ID，分类后端先筛选会令fake传入的重复项被移除后返回Success。两项fake测试确实在变更前返回Success；新增后端层完整列表冻结和null/Ordinal重复ID检查是纵深防御，不是已证实的真实HALCON生产回归修复。合法无序/不同大小写类别仍按声明进行筛选、排序和取数量；未修改真实SDK或模型协议。

修复后分类后端/发布/离线判定、资源生命周期和耗时相关41通过、0失败、0跳过；三个交付模块增量构建0警告/错误，不代表历史完整依赖警告已修复。日志 `%TEMP%/vision-ai-raw-duplicate-{red,green,final,build}.log`，直接编译测试CS1701另记。没有真实模型、HALCON新原生或设备验收，也未重跑完整旧测试工程；非fake生产模型的实际输出质量仍待验证。

## 57. ExecuteAsync统一实测耗时

`VisionOutcome<TResult>.ExecutionDuration` 为可空TimeSpan：引擎取得上下文独占租约后以单调Stopwatch计时，覆盖本次校验、后端实际执行、输出核验和发布前检查；不含上下文获取、独立模型准备、调用方处理或释放。Success/NoMatch/Failed/Unsupported正常返回均带非负实测值；手工构造未测量可为null，不以假0代替，负值拒绝。取消继续抛OperationCanceledException，关闭等待实际调用，不把未返回结果包装成已完成或硬超时。当前构造函数增加可选参数，已编译消费者需要重新编译；不增加旧接口转发壳。

先添加测试时因属性和构造参数不存在编译失败；接入后含Success/NoMatch/校验失败/Unsupported、取消异常、负值/缺失和可控fake后台等待，以及已有生命周期/来源链，61通过、0失败、0跳过。后台等待对照以两段单调实测比较，不作为耗时上限或性能保证；编译期红不是运行期回归红。三模块增量构建均0警告/错误，不代表历史完整依赖106警告消失；直接编译测试7条CS1701另记。未运行UI、新HALCON原生/真实AI或设备，也未重跑旧完整测试工程。日志 `%TEMP%/vision-execution-timing-{red,green,final,build}.log`。

## 56. 用户确认：按真实Tools需求收口，而非机械迁移旧测试

### 本次源码核对

- 以当前 `Tools/OpenCv/Engine/OpenCvBackend.cs`、`Tools/Halcon/Engine/HalconBackend.cs`、`Tools/Runtime/Execution/VisionEngine.Execution.cs` 与 `Lifetime.cs`、`Vision.Core/Results/VisionOutcome.cs` 和上述支持矩阵为依据：两条优先离线合成闭环已有实现与历史定向证据；HALCON只接声明的形状匹配、阈值、统计、灰度裁剪，不能把OpenCV的Blob/卡尺/颜色/QR当HALCON能力。
- 计划5.1要求“统一记录本次诊断和耗时，说明是否含校验/排队/模型准备”；当前 `VisionOutcome<TResult>` 只包含Status/Value/Issue，`ExecuteAsync` 没有公开耗时，因此**统一执行耗时尚未交付**。取消仍由OperationCanceledException传播，不把没有返回结果的取消假装已有耗时；耗时起止、取消/验证失败是否计入、模型准备是否单列须在实现前明确，不能把旧单次Stopwatch结果等同契约。
- 原T5明确列出一维码、更多码制及OCR/OCV；当前只有标准单/多QR，不能叫完整读码。是否将一维码作为本次Tools收口的必选项需结合目标工件和可用SDK确认；OCR/OCV、检测/分割/异常AI不能凭旧接口名称视为已接通。
- 原计划T6的真实分类模型仍缺模型、预处理、样本和授权；当前CPU路径与fake测试不等于实际模型验收。新HALCON原生、真实设备及工件精度同样未验。被明确Unsupported且不在确认范围内的厂商/设备组合不自动成为缺陷，也不能算已实现。
- 跟随跨派生图、世界重拟合/完整等厚、畸变透视、Lab色差等仍属已知扩展，不因旧测试存在自动纳入。线性半向上、归一化、多格式裁剪、显式裁剪边界已分别于第38/52～54节交付。

### 后续判定与验证

1. 不再以旧测试工程错误数为目标逐文件迁移；有效算法、输入边界、生命周期和安全语义仍需新契约证据。旧测试不未经确认批量删除，过时API/动态字段/旧序列化不恢复。
2. 先明确耗时契约与本次必选读码范围，再围绕已确认的关键任务链补齐生产缺口和必要新契约测试；不按数量堆功能，也不将局部通过说成全量通过。
3. 完成前复验Core/Runtime/三个已交付模块的构建及适用的关键离线测试、支持矩阵、取消/资源所有权；区分构建、fake、OpenCV合成、真实HALCON/AI/工件的证据。外层与MachineApp仍不扩大范围。
4. 原始旧测试工程最近一次实际构建244个旧契约编译错误，归为旧源码未整理，不作为Tools完成与否的唯一判断；仍应如实报告它不能全量运行。

本节仅进行源码/文档盘点，未执行构建、测试、UI、设备或SDK。知识图谱当前候选路径新鲜度缺失，本节依据直接读取源码，不据旧图谱作完整性证明。

## 55. 旧分割测试迁移

`Tests/Vision.Tests/Tools/OpenCv/SegmentationToolsTests.cs` 改用强类型引擎：线性饱和/反相/显式半向上、Gamma、逐图归一化、自适应阈值、HSV跨零（BGRA须显式转换）、RGB闭区间、输入冻结。原单像素边界、非法格式/参数、HSV/RGB策略等在已有ImagePreprocessing/GrayGamma/GrayNormalize/Hsv/Rgb测试继续验证；不恢复旧Mode、可变参数和Tool生命周期。旧文件快照 `Tests/obj/SegmentationToolsTests-before-engine-migration.cs`。

分割及相关测试105通过、0失败、0跳过；完整旧测试工程仍244编译错误（此前248），迁移文件无诊断。仅离线合成图，不代表真实工件/设备验收。日志 `%TEMP%/vision-segmentation-migration{,-full}.log`。本批未改生产代码。

## 54. 显式灰度线性半向上舍入

`GrayLinearOperation` 维持原四参数构造及后端最近整数默认路径，新增明确 `GrayLinearRounding.HalfUp` 五参数请求：先限幅，再对非负输出floor(value+0.5)。OpenCV实现使用托管逐像素内核、原位深输入和明确Mono8/16输出、忽略padding，支持负gain，最多2000万像素并周期检查取消；默认SDK路径及HALCON未接能力不变。不假称两种舍入相等或恢复旧Mode/可变参数。

新请求在实现前编译失败，接通后覆盖2.5→3、SDK默认2.5→2、反相、padding及四种输入/输出位深组合，联合111通过、0失败、0跳过；此为编译期红，不是运行期红。三模块增量构建通过；完整工程仍248个旧契约编译错误，旧 `SegmentationToolsTests.cs` 未整体迁移。日志 `%TEMP%/vision-linear-rounding-{red,green,final,build,full}.log`；未运行新HALCON原生/真实设备。

## 53. 显式浮点裁剪边界及旧测试迁移

增加 `ImageCropOperation.FromClippedBounds`，明确先与输入视野求几何交集，再向外取整为整数半开矩形；非有限、端点溢出、空或完全不相交拒绝。普通整数构造器依旧严格拒绝越界，不隐式裁切。旧 `CropToolTests.cs` 已迁移原解析裁剪、所有多字节格式、padding、来源和参数拒绝；旧工具Dispose改为上下文释放后快照，不恢复Timestamp或可变结果。原文件快照 `Tests/obj/CropToolTests-before-engine-migration.cs`。

先因方法不存在编译失败，接通后裁剪/图像转换及契约相关75通过、0失败、0跳过；是编译期红证据而非运行期功能红测。三个模块增量构建通过，完整测试工程仍248个旧契约编译错误（此前249），本测试无诊断。HALCON格式支持不变，未执行新原生或真实设备。日志 `%TEMP%/vision-clipped-crop-{red,green,build,full}.log`。

## 52. OpenCV多格式整数矩形裁剪

OpenCV裁剪从Mono8/16扩展至Rgb24/Bgr24/Bgra32/Float32，复制完整原始像素和独立输出，不解释alpha或浮点；仍使用整数半开矩形、越界拒绝、独立帧/空间及到直接源图的映射。HALCON裁剪能力不变，仅Mono8/16，不回退。先新增四格式案例，实际4失败（Unsupported）→实现后相关73通过、0失败、0跳过。三个模块增量构建均0警告/错误，不代表历史完整依赖警告消失；完整测试工程仍249个旧契约错误。

旧 `CropToolTests.cs` 仍未整体迁移：旧浮点矩形自动向外取整并截断与现行显式整数/越界拒绝不同，不以删除断言冒充支持。日志 `%TEMP%/vision-multiformat-crop-{red,green,build,full}.log`；未运行新HALCON原生或真实设备。

## 51. Blob唯一中心显式提取

新增 `PixelPoint.FromUniqueBlobCenter`，只接受已筛选快照恰好一个区域，空或多目标拒绝，不默认首个；保留源帧与空间，真实原点合法。新增 `UniqueBlobCenterTests`，覆盖筛选、空/多区域、真实原点、关闭上下文后点距和输入校验。先编译失败（新方法不存在），实现后相关67通过、0失败、0跳过；这是编译期红证据，不是运行期行为红测。三模块增量构建通过。

旧 `BlobPointTests.cs` 仍保留且与备份哈希一致：旧世界/机器结果映射等尚无完整等价，不能以唯一中心新增测试冒充整体迁移。完整旧测试工程仍249错误，本次新增测试无诊断；未触外层或真实设备。验证时发现Git索引文件哈希与本批开始不同，暂存数量仍458；未执行git add/reset，原因未确认，不声称索引字节未变。日志 `%TEMP%/vision-unique-center-{red,green,build,full}.log`。

## 50. Blob计数小数边界及旧测试迁移

`BlobJudgmentOperation.ForCount` 边界由int改为double，允许有限非负小数闭区间（如2.5..3.5），实际数量仍来自已筛选快照的非负整数，不对边界取整；不提供旧签名转发壳，此为公开签名破坏性变更。旧动态数值字段、枚举值和可变/可释放结果不恢复。迁移 `Tests/Vision.Tests/Tools/OpenCv/BlobCountTests.cs`：实际面积4/9/16、筛选后数量、区域、空NoMatch快照、闭区间与有效NG及上下文释放后判定；无参与像素区域按新契约拒绝。原文件快照 `Tests/obj/BlobCountTests-before-engine-migration.cs`。

Blob计数/区域/轮廓与工作流相关31通过、0失败、0跳过，三模块增量构建0警告/0错误（不代表历史完整依赖警告消失）；完整测试工程仍249个旧契约编译错误（此前256），本文件无诊断。未先取得失败的行为测试，不能称红绿验证；未运行设备、UI或新HALCON原生。日志 `%TEMP%/vision-blob-count-{build,tests,full}.log`。

## 49. 归一化发布守卫与输入预算验证

补充假后端返回错误源帧/空间/输出尺寸/位深时的拒绝发布，以及2000万像素上限外的Validate/Execute拒绝；与Gamma、灰度统计及处理域联合58通过、0失败、0跳过。首次运行4项失败是测试夹具使用未注册fake选择ID，改为已选OpenCV ID的注入fake后通过，不能把该失败当生产回归或功能红测。日志 `%TEMP%/vision-normalize-guard{,-final}.log`；共用TRX已覆盖，历史运行以相应日志为准。未改生产代码；完整工程未重跑，上次256个旧契约错误，未触外层/设备。

## 48. 灰度判定指标与位深发布验证

补充四种指标各自的解析黄金（0、4、1.5、√2.75），防止均值与总体标准差错选；使用HALCON边界fake返回错位深统计，确认引擎拒绝发布，不运行HALCON原生。统计/直方图、处理域、公共几何及fake关联67通过、0失败、0跳过，日志 `%TEMP%/vision-gray-range-followup-final.log`；脚本共用TRX已覆盖，先前各次结果以各自日志为准。旧 `ToleranceTests.cs` 的任意灰度动态数值绑定仍不等价，本批未整体迁移；完整工程未重跑，上次为256个旧契约错误。本批无生产代码修改。

## 47. 灰度统计位深与显式公差

`GrayStatisticsOutput` 构造现在必须提供Mono8/Mono16来源格式，按位深限制最大值；OpenCV和HALCON适配器提供输入格式，引擎发布时核验格式一致。不保留猜测位深的旧构造器。新增 `GrayStatisticsRangeOperation/Output`：明确Minimum、Maximum、Mean或总体标准差，按来源原灰度编码单位闭区间判定，NG是Success；快照可在生产上下文关闭后使用，不借SDK，不通过字符串字段或几何单位混用。

OpenCV实际Mono8/16统计与判定、无效格式/参数、关闭后快照及关联契约共60通过、0失败、0跳过；三模块构建通过。完整旧测试工程仍256编译错误，旧 `ToleranceTests.cs` 并未整体迁移；旧任意灰度数值绑定不等价于本次统计指标判定。未执行新HALCON原生、真实设备或AI；本批未先取得红测，不能称红绿验证。日志 `%TEMP%/vision-gray-range-{build,tests,full}.log`。

## 46. 旧公差需求核对及几何边界补测

核对 `ToleranceTests.cs`：几何距离/角度闭区间与NG已由强类型 `MeasurementRangeOperation` 支持，本批补实际点距0、5.25、10、10.01的判定链及上下文释放后快照，验证缺失、错误单位、非法上下限和非有限值不能产生假判定。相关39通过、0失败、0跳过，日志 `%TEMP%/vision-tolerance-followup.log`。复用line-angle脚本的TRX被本批覆盖，第45节历史41项以原日志为准。

旧文件并未整体迁移：旧Gray8/Gray16通用公差与动态绑定没有当前强类型等价，保留原测试而非删除或冒充几何单位；负像素距离也不是合法距离值。完整工程上次实测256个旧契约编译错误，本批未重跑。无生产/外层/MachineApp修改。

## 45. 旧双线夹角测试迁移

`Tests/Vision.Tests/Tools/OpenCv/LineAngleTests.cs` 迁入公共强类型引擎，覆盖无向锐角、反转、来源、非法方向、闭区间角度公差及有效NG。原1e-200像素方向现明确被PixelLine最小长度1e-9契约拒绝；新增1e-8作为可接受小尺度，并保留1e200解析45度，不恢复无来源手工结果/旧字段或枚举兼容。原文件备份 `Tests/obj/LineAngleTests-before-engine-migration.cs`。

夹角、双线、点对及公共几何相关41通过、0失败、0跳过，日志 `%TEMP%/vision-line-angle.log`；完整测试工程仍256个旧契约编译错误（此前262），本文件无诊断，日志 `%TEMP%/vision-line-angle-full.log`。本批不改生产代码，不代表真实设备验收。

## 44. 旧点对距离测试迁移

`Tests/Vision.Tests/Tools/OpenCv/PointPairTests.cs` 迁入公共引擎的带来源像素点距离，保留零距离、3-4-5、跨帧/空间及缺值拒绝、非有限/溢出不发布和上下文释放后快照。旧动态参数、可变配方及无来源手工回退不恢复；原文件备份 `Tests/obj/PointPairTests-before-engine-migration.cs`。

点对、双线、公共几何和仿射标定相关45通过、0失败、0跳过，日志 `%TEMP%/vision-point-pair.log`；完整测试工程仍262个旧契约编译错误（此前267），本文件无诊断，日志 `%TEMP%/vision-point-pair-full.log`。本批不改生产代码，不代表真实设备验收。

## 43. 旧双线交点测试迁移

`Tests/Vision.Tests/Tools/OpenCv/LinePairTests.cs` 迁入公共引擎强类型无限线交点：保留原点/线段外交点、平行重合无假零点、来源/非有限/退化/溢出拒绝及上下文释放后的快照。旧动态字段/手工无来源及参数绑定不恢复；新契约无图运算必须使用带帧和像素空间的两条PixelLine。旧文件备份 `Tests/obj/LinePairTests-before-engine-migration.cs`。

双线、公共几何及世界线相关33通过、0失败、0跳过，日志 `%TEMP%/vision-line-pair.log`；完整测试工程实际构建仍267个旧契约编译错误（此前273），本文件无诊断，日志 `%TEMP%/vision-line-pair-full.log`。本批无生产代码更改，不代表真实工件验收。

## 42. 旧Blob参与域测试迁移

`Tests/Vision.Tests/Tools/OpenCv/BlobSearchRegionTests.cs` 四项迁至引擎：区域先于连通、0灰度不选域外、圆形区域不退化成外接框、图像/筛选/ROI冻结。旧空区域成功零目标与当前契约不同：当前构造请求时拒绝无参与像素的区域，不误报为有效零目标。原文件备份 `Tests/obj/BlobSearchRegionTests-before-engine-migration.cs`。

区域、轮廓及相关链28通过、0失败、0跳过，日志 `%TEMP%/vision-region-migration.log`；完整测试工程仍273个旧契约编译错误（此前274），本文件无编译诊断，日志 `%TEMP%/vision-region-migration-full.log`。本批无生产代码更改，不代表真实设备验收。

## 41. 旧Blob轮廓测试迁移

`Tests/Vision.Tests/Tools/OpenCv/BlobContourTests.cs` 三项迁至唯一引擎，保留凹形边界、筛选后序号与像素面积、环孔与单像素岛、上下文释放后轮廓快照和空区域断言。空区域按新契约为NoMatch且带空快照，不恢复旧成功结果壳。旧文件快照 `Tests/obj/BlobContourTests-before-engine-migration.cs`；未修改生产代码。

轮廓及Blob链相关24通过、0失败、0跳过，日志 `%TEMP%/vision-contour-migration.log`；完整测试工程实际构建仍失败274个旧契约错误（此前275），本文件无诊断，日志 `%TEMP%/vision-contour-migration-full.log`。此次不是全量通过，不代表真实工件验收。

## 40. BGRA颜色分割显式转换链

迁移旧BGRA红色色相跨零夹具的有效像素期望：直接对BGRA执行HSV分割仍为Unsupported；显式BGRA→BGR后，验证透明/不透明像素均保留原颜色、行padding不参与、色相跨零/S/V条件及帧来源与空间。丢弃alpha由独立转换请求明确表达，不等同与背景合成。相关HSV/RGB、转换、预处理和归一化共59通过、0失败、0跳过；测试直接编译7条CS1701。日志 `%TEMP%/vision-bgra-followup.log`；沿用的normalize TRX已覆盖，前批结果以各日志为准。

此批仅增加测试和文档，无生产能力变动；旧 `SegmentationToolsTests.cs` 仍未整体迁移，线性半向上差异未解决，完整工程上次实测275个旧契约编译错误，本批未重跑。未触及外层、MachineApp或真实设备。

## 39. 分割旧需求核对与边界补测

核对旧 `SegmentationToolsTests.cs`：已有新契约测试覆盖Gamma、RGB/HSV、自适应阈值及归一化的大部分有效需求；补充单像素自适应阈值在均值/高斯与-1/0/+1偏移下的复制边界和padding验证，以及归一化多行使用同一全图极值、逐行忽略padding的验证。联合相关92项通过、0失败、0跳过；直接编译有7条CS1701，详见 `%TEMP%/vision-segmentation-followup.log`。

旧文件未删除或宣称迁移完：其BGRA直接颜色分割和线性2.5半向上需求与现有新契约不等价，尚待明确；不通过放宽断言、隐式丢alpha或更改既有线性舍入来冒充完成。完整工程上次实测275个旧契约编译错误；本批仅补测试，未重新构建全工程，也未修改生产能力、外层或MachineApp。第38节TRX可能被本批同路径覆盖，历史86项以第38节日志为准。

## 38. 显式逐图灰度归一化

新增 `GrayNormalizeOperation` 和 `GrayNormalizeOutput`：Mono8/Mono16全图原位深最小/最大值映射至0和对应满量程，整数半向上舍入；常量图保持原值，输出原始极值和独立同空间帧。忽略行padding，最多2000万像素，扫描期间检查取消；不隐式转换颜色或浮点，HALCON保持Unsupported。OpenCV模块采用托管两遍整数扫描，不宣称SDK归一化算子。

先仅加入契约及测试，9项中5失败、4通过；接入后与Gamma、Otsu、预处理及引擎契约联合86项通过、0失败、0跳过。真实链覆盖归一化→阈值→Blob。三个模块增量构建各0警告/0错误，不代表历史完整依赖106警告已消除；独立测试编译另有7条CS1701。格式和IDE0011验证通过。完整Vision.Tests工程仍275个旧契约编译错误，未宣称全量通过；旧SegmentationToolsTests尚未整体迁移，线性半向上与现有SDK舍入差异仍待处理。未运行UI、真实设备或新HALCON原生/真实AI。

日志 `%TEMP%/vision-normalize-{red,green,build,full-tests}.log`；独立测试脚本 `%TEMP%/verify-vision-tools-normalize.ps1`。本批仅Core/Tools和测试，未修改外层或MachineApp。

## 37. Tools显式Gamma及Otsu测试迁移；当前关联图资产收口

继续Tools优先，必要Core契约随功能补入；Controls/Workbench/Nodes和MachineApp不修改，不运行UI/宿主、真实设备、新HALCON原生或真实AI。

### 功能及迁移

- 新 `GrayGammaOperation`，Id `vision.processing.gray-gamma`，只收有限正指数，Model=null。输出复用ImageTransformOutput，保持原Mono8/16位深/尺寸/像素空间及源帧追溯，新图有独立FrameId。
- 明确公式 `M×(pixel/M)^Gamma`，M=255/65535；指数不是倒数，不按本图极值归一化；非负输出以floor(value+0.5)量化。只规定该Gamma请求，不改变GrayLinear已有SDK最近整数舍入，也不将旧线性半向上断言悄悄替换为等价声明。
- OpenCV选中模块内托管完整灰度LUT内核，不宣称SDK Gamma指令：最多65536个ushort映射，Mono16小端逐像素映射、忽略padding、原数组不可变；最多2000万输入像素，查表构造及长行周期检查取消，发布继续走引擎守卫。0/M端点固定，正指数小于1提亮中间值、大于1压暗；颜色/浮点Unsupported，HALCON仍Unsupported不回退。
- Gamma25项验证保留旧分割Gamma的两项解析黄金，并增加Mono16解析黄金、完整65536编码恒等Gamma、极小/极大有限正指数端点、坏参数/格式/预算、冻结请求及源关闭后快照、错帧/空间/尺寸/格式/NoMatch发布拒绝。实际链Gamma→阈值→面积2 Blob→数量OK，不把显示参数存在当功能。
- 原 `Tools/OpenCv/OtsuThresholdTests.cs` 17项完整迁移，保留双峰最小阈值/严格二值规则、常量零阈值Success、反向互补、padding与不可变快照等有效需求；固定闭区间阈值和Otsu是不同强类型请求，无旧Mode/JSON或动态参数字典兼容。原文件快照 `Tests/obj/OtsuThresholdTests-before-migration.cs`。
- 旧Otsu一概拒绝Mono16的限制已过时，改验证原位深阈值1000及独立Mono8输出；缺图由构造守卫拒绝，颜色仍Unsupported。旧UnknownMode/旧JSON兼容用例按已确认破坏性新契约重分类为显式请求隔离，不恢复旧接口。
- `SegmentationToolsTests.cs` 仍保留原文件，未宣称整体迁移；逐图Normalize和旧线性半向上舍入需求仍待明确实现/差异处理，不删除相关有效断言求绿。

### 验证及关联资产

- 只加Gamma契约/测试且未接实现时，聚焦42项实际13失败、29通过；接通后42通过（Gamma25+Otsu17），0失败/0跳过。联合既有基础图像、通道、直方图、文件转换、预处理、统计/ROI、生命周期及Core契约，最终172通过、0失败、0跳过。
- 关联构建中发现当前声明图引用Licensing.Core，但同一独立产物目录缺其assets，HALCON构建NETSDK1004；未将此误报为Gamma算法失败。对现有HALCON、HALCON AI和Vision.Tests图向同一artifacts路径定向restore后解除，六个被核验csproj哈希不变；不修改授权代码/项目引用/声明包版本，不启动授权程序或MachineApp。
- 测试脚本同步当前模块全部传递DLL并合并当前三个模块deps，包含当前Licensing.Core身份，不仅借旧缓存的依赖清单运行；末端HALCON测试仍为fake，未执行新原生或真实模型。
- 最终OpenCV与AI增量构建各0警告/0错误，HALCON关联构建106警告/0错误（既有Extension/Extension.Core警告未修复）；直接编译测试7条CS1701引用统一警告另记。Core/OpenCV/Runtime及定向测试whitespace与IDE0011检查通过。
- 完整旧测试工程当前仍275个旧API编译错误（上批292），本批Gamma/Otsu文件无编译诊断，未增加Compile排除或批量删除有效测试。原458项暂存及索引哈希保持不变。

日志：`%TEMP%/vision-tools-gamma-otsu-{red,green,final}.log`及同前缀三模块final-build日志；关联资产日志 `vision-tools-gamma-otsu-current-graph-restore.log`，项目哈希 `Tests/obj/tools-gamma-restore-project-hashes.json`。TRX `Tests/obj/editor-verification/gamma-otsu-tests/results/gamma-otsu-green.trx`对应最终172项，42项聚焦证据在green日志。完整测试构建日志 `vision-tools-full-tests-after-gamma-otsu.log`。

```powershell
pwsh -NoLogo -NoProfile -File "$env:TEMP/verify-vision-tools-gamma-otsu.ps1" -Phase green -Filter 'FullyQualifiedName~GrayGammaEngineTests|FullyQualifiedName~OtsuThresholdTests|FullyQualifiedName~BasicImageToolsTests|FullyQualifiedName~ColorChannelExtractEngineTests|FullyQualifiedName~GrayHistogramEngineTests|FullyQualifiedName~ImageIoConversionTests|FullyQualifiedName~ImagePreprocessingTests|FullyQualifiedName~GrayStatisticsEngineTests|FullyQualifiedName~ImageProcessingDomainTests|FullyQualifiedName~VisionEngineLifetimeTests|FullyQualifiedName~VisionEngineContractTests'
```





