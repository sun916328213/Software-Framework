# 28 个工具属性操作 UI：逐项审查与联网案例

## 实施进展：按批落地（非28项全部完成）

- 模板页新增取样→搜索参数→执行的说明、实际取样尺寸，以及 OpenCV 固定角/预算边界和需训练工具的显式训练/加载说明；不是实时预算估算。731逻辑/604 WPF通过，之前WPF全量有一次挂起和一次共享应用为空连锁失败，根因未证实，详见 `Software Frame/Vision/Tests/EDITOR_REFINEMENT.md`。
- ROI 用途、裁剪待应用状态及Blob新选phi负范围已完成；原ROI配置快照已深拷贝。

- 已实现：Morphology/ImageFilter/AdaptiveThreshold奇数参数步进2；ImageFilter仅Gaussian显示Sigma；IntensityTransform按Linear/Gamma/Normalize显示生效字段；ColorSegment随HSV/RGB更新六个通道标签、有效范围说明和H环绕提示，保留原区间。
- 原工具ValidationResult直接用于参数区摘要及字段提示；按Member定位，缺少Member的问题保留摘要。仍参与原全量校验的隐藏字段若有问题会强制露出，修复后再隐藏，避免“藏着错误却无法运行”。HSV上限错误定位到上限字段，不改原校验接受/拒绝规则。
- 不新增平行DTO/校验器；显示派生属性不写JSON；仍默认显式执行、改参清旧结果，不改流程布局。完整731逻辑/587WPF通过，隔离宿主增量0警告/0错误。
- 后续仍待：ImageConvert/Geometry生效项、ROI用途/裁剪待应用、Blob特征默认范围/命名、模板任务式编辑、AI模型区整合、标定及完整结果查看。见 `Software Frame/Vision/Tests/EDITOR_REFINEMENT.md` 顶部命令与证据。
- 本批测试通过实际数值控件模拟用户编辑；并非为所有原始DTO字段补齐外部程序写值通知。任意外部直接修改无通知属性的编辑器同步、非法导入值控件回写、空参数组收拢等仍需后续专项处理。

---

## 1. 范围、证据和结论

本次只检查**完整工具编辑器内部**：参数操作、ROI/取样、模型加载、校验、试运行和结果反馈。**不讨论流程图布局、工具箱、节点属性栏或连线交互。**

覆盖当前正式目录 **OpenCV 14 + HALCON 8 + AI 4 + Common 2 = 28 项**。逐项核对实际 DTO、ToolProperty 元数据、专用适配、公共控件及结果路径，并联网读取官方交互说明、查看 Aurora 模板和几何编辑器截图。

证据分级：
- **S：源码确认**，指具体字段、绑定或执行路径存在的事实；不等于已亲自操作 UI。
- **U：需要人工操作验证**，例如首次使用者是否误解、键盘提交/焦点、窄窗口、DPI。
- **R：后端/授权环境待验证**，例如真实模型、HALCON 算子、精度、耗时。

优先级：**P1＝易误配置/结果误解，应先处理；P2＝操作效率和可发现性。** 本次没有凭静态阅读宣称新的现场 P0 事故。

**总体结论：不应重写 28 套页面。** 当前已有不少专用交互，主要缺口是：参数有时未按任务/模式收敛、工具级错误未充分关联参数行、模型操作分散、ROI 的用途提示不够就近，以及后端能力和可编辑项不完全一致。

本次仅新增此报告，未修改生产/测试代码，未运行宿主、测试或硬件，未加载模型，保留进入本轮前工作树和暂存区。

## 2. 联网参考：实际读取的官方案例

以下编号用于后文，不代表复制对方算法或承诺相同能力。

| 编号 | 官方资料 | 已核实的交互 | 本项目适合借鉴的部分 |
|---|---|---|---|
| R1 | [MERLIC 5.8：Adjusting Parameters](https://www.mvtec.com/doc/merlic/5.8/manual/en-us/Content/Creator/Building_mvapps/Configuring_tools/adjusting_parameters.html) | Basic/Additional 参数；数值键入、步进和滑动；easyTouch 图像选择反馈；改动参数短时高亮 | 常用/高级分层；阈值适用时提供滑动+精确数值；当前修改项清晰可见 |
| R2 | [MERLIC：Tool Workspace](https://www.mvtec.com/doc/merlic/5.8/manual/en-us/Content/Creator/User_interface_creator/tool_workspace.html) | 就近图像辅助操作、可折叠 Quick Info、训练参数和运行参数分区 | 用简短任务提示取代长状态串；明确取样/训练/执行的区别 |
| R3 | [MERLIC：Working with ROIs](https://www.mvtec.com/doc/merlic/5.8/manual/en-us/Content/Creator/Building_mvapps/Configuring_tools/working_with_rois.html) | 形状绘制、可拖边高亮、方向箭头、中心拖动、键盘微调/删除 | 在已有 ROI 编辑器上改进用途标识和方向反馈；不是从零补 ROI |
| R4 | [MERLIC：Measure Circle](https://www.mvtec.com/doc/merlic/5.8/manual/en-us/Content/Tool_reference/Processing/Measuring/measure_circle.html) | 图像圆预览、确认、边缘方向、基础/附加参数及单位说明 | 找圆时把候选、期望圆心、圆心选择域的角色讲清楚；easyTouch 自动估参属于另一个算法任务 |
| R5 | [MERLIC：Locate with Matching](https://www.mvtec.com/doc/merlic/5.8/manual/en-us/Content/Tool_reference/Processing/Position_determination/locate_with_matching.html) | 当前处理图/训练图切换；活动模式高亮；搜索与训练参数区分 | 模板取样、训练、搜索参数和当前图像上下文更明确 |
| R6 | [Aurora：Connecting and Configuring Filters](https://docs.adaptive-vision.com/current/studio/user_interface/ConnectingAndConfiguringFilters.html) | 图像上下文中的几何编辑、点坐标调整、缩放/平移；参数变化试验及其限制 | 复用现有几何控件；精确数值与画面相互定位；不要无条件自动重执行 |
| R7 | [Aurora：Creating Models for Template Matching](https://docs.adaptive-vision.com/current/studio/user_interface/CreatingModelsForTemplateMatching.html) | Basic/Expert 两种编辑复杂度；参考图、矩形取样、旋转容许范围、效果预览、模型矩形确认 | 模板工具优先采用“准备模板→设置搜索→试运行”顺序；角度范围与速度的关系就近解释 |
| R8 | [Aurora：Preparing Rectification Transform Map](https://docs.adaptive-vision.com/current/studio/user_interface/PreparingCalibrationMap.html) | 分步骤标定编辑器，输入/计算/结果有清晰阶段 | N 点标定组织点对、单位、有效性和结果；不移植相机内参/畸变功能冒充当前能力 |
| R9 | [Aurora：Auto-tuning Datacode Parameters](https://docs.adaptive-vision.com/current/studio/user_interface/AutoTune.html) | 码制选择、已解码标注、刷新控制、耗时和优化预算、确认后写回 | 先借鉴码制候选、当前请求说明和读码反馈；自动调参/多图优化需要独立实现，不能作为 UI 小改 |
| R10 | [MERLIC：Segment Image Pixel-Precisely](https://www.mvtec.com/doc/merlic/5.8/manual/en-us/Content/Tool_reference/Processing/Deep_learning_ai/segment_image_pixel_precisely.html) | 模型、处理设备、精度的能力关联；类别区域/类别名/叠加图 | AI 模型上下文与真实能力关联；不可让不支持的选项表现得可正常使用 |
| R11 | [Aurora：Creating Deep Learning Model](https://docs.adaptive-vision.com/current/studio/user_interface/DeepLearningTraining.html) | 任务分类、模型准备分阶段；明确独立 Add-on/License/训练服务要求 | 借鉴任务措辞与状态分层，不新增本项目没有的 AI 训练平台 |

联网情况：上述正文读取成功；MERLIC ROI 页一次 SSL 失败后重试成功。Cognex 官方 EasyBuilder 文档返回 **429**，本次不引用其细节作为审查证据。最初两个猜测的旧路径返回 404，已通过官方目录定位正确正文；搜索结果页不作为产品行为依据。

实际查看 Aurora 官方的 Basic 模板编辑器截图和几何编辑器截图：前者把参考图、矩形取样、旋转容许范围放在同一任务界面；后者明确背景图、坐标系、缩放和可用图形。截图仅作为参考查看，未复制到仓库，也不把截图外观当作本项目已验收的交互。

**不可直接照搬的行为：** MERLIC 自动估参与自动训练、Aurora 暂停状态自动重执行都有各自前提。本项目仍应保持显式模型加载/训练、默认显式执行、参数变更清结果、运行锁和 Native 完成后再释放。若做轻量预览，应另行定义不写回正式结果的边界。

## 3. 共用编辑器：保留什么，先改什么

以下路径均以 `Software Frame/Vision/` 为根；`C/` 表示 `Vision.Controls/`，`O/` 表示 `Tools/OpenCv/`，`H/` 表示 `Tools/Halcon/`。

### 已有能力，不应误报缺失

- 唯一完整界面已有参数/ROI/结果/模型/模板分区；五个主操作按钮固定存在。`C/Views/Editor/VisionToolEditorView.xaml:39–141`，`C/Views/Editor/ToolDetailsView.xaml:301–535`。
- 已有 Int/Double 数值控件、范围/步长、枚举下拉、复选框、点坐标编辑器、文件选择和列表/类表格。`C/Views/PropertyGrid/VisionPropertyPanel.xaml.cs:111–170,222–320`。
- 已有按布尔属性显隐的机制，Threshold/FindLine 实际使用；不是所有工具都没有模式联动。`C/Presentation/PropertyGrid/VisionPropertyItem.cs:20–55`。
- Blob 已有专用参数适配、特征多选/范围表；线 ROI 已有专用几何编辑；模板已有取样/预览/加载/保存/清除及按能力训练。
- 参数修改会失效旧结果并校验，运行时锁编辑；没有理由为了“即时反馈”恢复陈旧结果或随意跨 SDK 线程执行。`C/Services/Execution/ToolCardWorkspace.cs:81–87,136–164`。

### 共用问题与建议

| 问题 | 证据与级别 | 建议 |
|---|---|---|
| 工具校验主要汇入整体状态，关系错误未充分落在字段旁 | S/P1；`ToolCardWorkspace.cs:81–87`；数值绑定已有异常校验，但普通 DTO 的关系错误不一定抛异常 | 复用 ValidationResult/属性名；显示字段错误、分组错误计数和可定位摘要，别再增加一套校验规则 |
| 所有参数组默认展开，按组名排序，不是按任务复杂度分层 | S/P2；`ToolDetailsView.xaml:329–336`；`VisionPropertyGridBuilder.cs:23–29` | 常用默认展开，高级折叠；保留原 DTO 与属性控件，不为每个工具造新页面 |
| 右侧默认 340 宽，标签宽 160；复杂参数/表格在窄窗口中存在空间压力 | S 的布局事实 / U 的可用性风险；`VisionToolEditorView.xaml:151–154`，`Themes/VisionPropertyPanelStyles.xaml:8` | 保留分隔条；核验 800/1000/1300 宽、长中文、DPI、表格编辑。表格**已经全宽跨列**，不能误报为被强制塞入右半列 |
| 帮助主要在标签 Tooltip；枚举项直接使用原枚举，中文解释在帮助中 | S/P2；`VisionPropertyItem.cs:42–48`；`VisionPropertyPanel.xaml.cs:222–238` | 常用模式显示友好名称，保留枚举身份；最关键单位/适用条件就近常驻，不强迫悬停查找 |
| 成功执行自动切换到结果图 | S/P2，是否打断调参属 U；`ViewModels/Editor/VisionToolEditorSession.cs:148–151` | 可考虑“执行后转结果 / 保持当前视图”的显式交互策略；不要破坏已有结果互选 |
| 面积 ROI 页对不支持搜索域的工具仍用于标注/取样等，已有说明但位置偏后 | S 的能力事实 / U 的误用风险；`ToolDetailsView.xaml:374–385`，`VisionToolEditorSession.cs:51–70` | ROI 页顶部明确标识“算法搜索域 / 圆心选择域 / 模板取样 / 裁剪待应用 / 仅标注”，不能简单把画框都称搜索区域 |

## 4. OpenCV：14/14

### 01 Threshold — P1：区间关系就近校验
- **已有 S**：Range/Otsu/OtsuInverse；自动模式隐藏闲置区间；实际自动阈值、前景数/占比有结果表。
- **问题 S**：上下限单独受 0–255 限制，但大小关系到工具校验才整体反馈。建议区间两行联动错误，自动模式显示实际采用阈值；可参考 R1 的数值+滑动，但不新增默认自动执行。
- **依据**：`O/Processing/ProcessingTools.cs:28–53,112–146`；`C/Presentation/Results/VisionResultTable.cs:51–56`。

### 02 Morphology — P1：奇数步进；P2：耗时提示
- **已有 S**：操作下拉、核尺寸、迭代数、奇数校验、输出图和前景统计。
- **问题 S**：核尺寸默认步进 1，可从合法值点到非法偶数。建议步进 2，并对手输偶数定位提示；不是只改控件就取消后端校验。
- **风险 U**：工具未声明后台能力，编辑器有同步执行回退；大图/大核/多迭代的窗口响应未实测。先做耗时提示和测量，不未经授权把 SDK 调用随意搬线程。
- **依据**：`O/Processing/ProcessingTools.cs:150–176,195–209`；`C/Services/Execution/ToolCardWorkspace.cs:150–153`；参考 R1、R6。

### 03 Blob — P1：特征默认区间；P2：名称/单位
- **已有 S**：专用适配器、灰度/面积预筛、特征多选及范围表、And/Or、搜索域、真实轮廓、结果表图互选。
- **问题 S**：新勾选特征统一从 `[0,99999]` 起步，`phi` 可能为负、且为弧度，可能让“勾选一个测量特征”同时意外排除目标。建议按特征定义默认合法区间，显示中文/单位，变更过滤含义要明示。
- **依据**：`C/ViewModels/Parameters/BlobParametersEditorViewModel.cs:159–166`；`O/Inspection/BlobTool.cs:320–324`。参考 R1、R3；不把现有专用适配重写掉。

### 04 TemplateMatch — P1：操作顺序与资源预警
- **已有 S**：独立模板取样/预览/保存/加载；OpenCV 无需训练；实际角度/尺度/四角及结果互选；最新大图预算修复也已计入本次审查。
- **问题 S/U**：取样在 ROI 页选择矩形，再去模板页提交；搜索参数又在参数页。建议沿原页做“模板准备→搜索设置→试运行”任务引导；就近显示模板尺寸、角度起止/范围、是否固定方向、粗步长含义及资源预警。
- **边界**：模板取样 ROI 不等于搜索 ROI；不能把 UI 调整描述成已实现用户掩膜、连续精化、任意原点或搜索域。当前大图降采样回退不应再误报为未接入。
- **依据**：`O/Matching/TemplateMatchTool.cs:37–72`；`TemplateMatchTool.Pose.cs:48–84`；`C/Views/Editor/ToolDetailsView.xaml:458–535`；参考 R5、R7。

### 05 FindLine — P1：参数边界；P2：方向可视化
- **已有 S**：Calipers/EdgeBand 已分模式显隐；有专用线 ROI、端点/带宽/旋转/拖动及 Esc 回滚，不能报成缺少线编辑器。
- **问题 S**：部分元数据允许零，实际梯度/带宽等要求正数。建议输入边界与校验一致；在 ROI 旁展示当前模式、测量法向和 First/Last 含义。
- **依据**：`O/Measurement/MeasureTools.cs:25–103,162–167`；`FindLineTool.Calipers.cs:15–20`；`C/Views/Roi/LineRoiEditorView.xaml:9–40`；参考 R3、R6。

### 06 FindCircle — P1：圆形操作语义，需人工验收
- **已有 S**：期望圆心、半径范围、Hough 阈值和面积 ROI；参数说明已明确候选就近选择、ROI 仅筛圆心；结果有本次期望圆心。
- **风险 U**：用户可能把“画圆 ROI”理解成设置期望圆/半径，实际没有此写回。建议显著区分“圆心选择域”和“目标半径范围”，参数与画面交叉高亮；自动拾圆另立算法范围。
- **依据**：`O/Measurement/MeasureTools.cs:239–260,302–349`；`C/Views/Editor/ToolDetailsView.xaml:372–389`；参考 R4。

### 07 Crop — P1：未应用状态
- **已有 S**：直接编辑 X/Y/宽高；“使用选中矩形作为裁剪区域”显式提交；输出范围/坐标偏移已有说明。
- **问题 S**：提交后再改 ROI 只清结果，并不更新当前裁剪参数。建议标记“框已变化，尚未应用”，显示当前生效矩形；保留显式提交，不偷偷改成自动裁剪输入。
- **依据**：`O/Processing/CropTool.cs:11–28,42–49`；`C/Services/Execution/VisionToolInstanceWorkspace.cs:156–164,202–209`；参考 R3、R6。

### 08 ImageConvert — P1：输入格式与可用参数
- **已有 S**：亮度/R/G/B 下拉、16 位映射区间、输出格式/尺寸反馈。
- **问题 S**：16 位映射在非16位输入时仍显示并统一校验；单色输入仍能选 RGB 通道，直到执行才拒绝。建议就近显示输入格式及适用性，避免可选项暗示当前能执行。
- **注意**：若只隐藏区间但仍全量校验，会产生看不到的错误；显示策略与既有验证语义必须一起核实，不静默重写旧值。
- **依据**：`O/Processing/ImageConvertTool.cs:19–29,42–82`；参考 R1。

### 09 ImageFilter — P1：模式联动和奇数步进
- **已有 S**：模式/边界下拉、核尺寸、Sigma、非法组合拒绝、输出图。
- **问题 S**：Mean/Median 仍显示 Sigma 且参与校验；Median 可选不支持的边界；核步进 1；Sigma UI 下限 0.01 而后端允许 `(0,100]`。
- **建议**：按模式表明生效项，限制或解释非法边界，奇数步进 2；对齐旧合法值展示。不将“隐藏字段”当作豁免验证的隐式修改。
- **依据**：`O/Processing/ImageFilterTool.cs:24–36,48–82`；参考 R1、R6。

### 10 GrayStatistics — P2：完整直方图入口
- **已有 S**：Mono8/Mono16、搜索 ROI、分档宽、统计表；**已有直方图计数文本**。
- **问题 S**：只列前 995 个非零档，完整数据位于结果对象，界面无对应完整查看入口。建议图表/分页计数及导出，区分显示分档和实际统计值；不重写统计算法。
- **依据**：`O/Inspection/GrayStatisticsTool.cs:12–16,44–72`；`C/Presentation/Rendering/VisionDisplay.cs:63–68`。

### 11 IntensityTransform — P1：无关参数仍生效于校验
- **已有 S**：Linear/Gamma/Normalize，明确公式、Gamma 方向和常量图语义；参数切换不自动运行。
- **问题 S**：三种模式参数全显示/全校验；Normalize 可被不用的 Gamma 阻断；Gamma UI/后端下限不同。
- **建议**：按模式组织有效字段，公式小示意和输入/输出对照；保留无关字段值，先明确其验证策略。参考 R1、R6，不把显示器亮度与算法变换混在一起。
- **依据**：`O/Processing/IntensityTransformTool.cs:16–28,42–70`。

### 12 AdaptiveThreshold — P1：邻域奇数输入
- **已有 S**：Mean/Gaussian、邻域尺寸、Offset、Invert；两模式共用这些项，不应误加无必要显隐。
- **问题 S**：邻域步进 1 可落到偶数。建议奇数步进及行内校验，解释邻域/Offset 对前景的作用；输出统计已存在，可配局部观察。
- **依据**：`O/Processing/AdaptiveThresholdTool.cs:17–35,42–62`；`C/Presentation/Results/VisionResultTable.cs:57–58`；参考 R1。

### 13 ColorSegment — P1：颜色空间切换
- **已有 S**：HSV/RGB、六通道界限、H 跨零环绕；已有“切换不重置区间”的明确策略。
- **问题 S**：H 控件仍可输到255而实际只允许0–179；标签混用 R/H；默认 H 的170→10切到RGB后不合法。
- **建议**：动态标签/范围，“H环绕”可见状态；切换保留原值但提示哪些区间需调整，不能无声交换上下限。取色器/颜色条可另行设计，不承诺已有取色算法。
- **依据**：`O/Processing/ColorSegmentTool.cs:18–43,50–61,90–99`；参考 R1、R3。

### 14 ImageFile — P2：参数有效与文件已读分开
- **已有 S**：文件选择器、路径直输、输出实际路径/尺寸；保留支持位深/通道，不自动灰度化/旋转。
- **问题 S**：“校验通过”主要是路径非空，不代表文件可访问/可解码。建议独立显示“尚未读取 / 读取成功 / 失败”，非阻断存在性检查可选；不要在 getter 中读文件。
- **依据**：`O/Acquisition/ImageFileTool.cs:17–23,41–68`；`C/Views/PropertyGrid/VisionPropertyPanel.xaml.cs:111–120`。

## 5. HALCON：8/8

### 15 Blob — P1：未知/重复特征状态
- **已有 S**：同一专用 Blob 适配器、范围表、逻辑选择、搜索域、真实轮廓、结果互选。
- **问题 S**：适配器有意保留导入的未知/重复范围，但选择列表去重，容易忽略同名多行。建议显式标“未知/重复”，不在打开界面时清洗用户配方。通用特征默认区间/单位问题同03。
- **相关后端**：特征合法性、有限性和 Logic 枚举校验不完整，见第8节；不能仅换多选控件解决。
- **依据**：`H/Inspection/BlobTool.cs:51–71,155–181`；`C/ViewModels/Parameters/BlobParametersEditorViewModel.cs:45–47,159–178`；参考 R1。

### 16 ShapeMatch — P1：训练与运行状态合并呈现
- **已有 S**：专用模板页、训练参数/训练/存取/清除、模型引用、取样与局部轮廓；模板变化后旧模型不可当新模型执行。
- **问题 S/U**：训练与运行分组、模型需重训的原因、缩放严格正数等宜就近显示；训练/缩放元数据允许零而后端拒绝。建议任务引导保留显式训练，不照搬自动训练。
- **依据**：`H/Matching/ShapeMatchTool.cs:29–62,110–134`；`ShapeMatchTool.Editor.cs:22–25,35–53,144–148`；`C/Views/Editor/ToolDetailsView.xaml:458–535`；参考 R5、R7。真实模型训练/加载属 R。

### 17 FindLine — P2：方向、极性与单位
- **已有 S**：专用线 ROI，阈值、Sigma、极性、First/Last、忽略点数及退化线验证。
- **问题 S/U**：First/Last 与测量轴有关，文字“首个/最后一个”不够直观；结果坐标表头缺 px。建议方向/极性示意和单位。残差指标当前结果契约未提供，不能只改表就宣称具备。
- **依据**：`H/Measurement/FindLineTool.cs:45–96,110–134,157–172`；`C/Views/Roi/LineRoiEditorView.xaml:9–36`；参考 R3、R6。

### 18 Barcode — P1：码制选择及结果字段命名
- **已有 S**：码制文本、空值 auto；解码文本表；界面没有把默认(0,0)伪装成真实定位。
- **问题 S**：自由文本且请求校验为空；结果“码制”回填请求值，auto不是实际识别类型。建议版本适配的候选选择+可选自定义入口，标“请求码制”，实际类型取得前不冒充。
- **边界**：固定最小元素尺寸4，图像真实定位尚未提供；应解释限制，不伪造新参数/定位覆盖。
- **依据**：`H/Identification/CodeReaderTools.cs:39–46,81,96–105`；`C/Presentation/Results/VisionResultTable.cs:64,110–112`；参考 R9。

### 19 DataCode — P1：请求语义和实际能力
- **已有 S**：码制文本、空值解释、解码结果表、资源释放路径。
- **问题 S**：请求缺校验；结果码制是候选串，不是逐码真实类型；固定最多10个结果。建议候选/数量限制提示、请求/实际码制区分。
- **R**：空值一次向 CreateDataCode2dModel 传入两类码制的合法性需按 HALCON23.11 核实；不是增加下拉就能解决，见第8节。
- **依据**：`H/Identification/CodeReaderTools.cs:55–62,139,153–162`；参考 R9。自动调参/多图优化不在本次 UI 范围。

### 20 NPointsCalibration — P1：点对操作与单位
- **已有 S**：**已经有像素/世界坐标点对表格**，中文列名、增删、单元格提交；至少三点/有限值校验，结果有当量/矩阵。
- **问题 S/U**：单位依赖文字约定；提示非共线但未预检重复/共线；长表在窄栏中的配对可用性需实测。建议“输入点对→有效性→计算→结果”阶段说明，点击图像拾点作为后续交互增强。
- **边界**：残差、病态条件/质量指标需计算契约支持，不能凭 UI 展示凭空补出；不自动采集/移动设备。
- **依据**：`H/Calibration/NPointsCalibrationTool.cs:25–51,80–103,125–128`；`C/Views/PropertyGrid/VisionPropertyTableEditor.xaml.cs:21–33,94–107`；参考 R8。

### 21 Fixture — P1：变换语义；P2：结构化输出
- **已有 S**：基准点/目标点的 X(列)/Y(行)专用输入、角度、缩放、矩阵/Pose 结果。
- **问题 S**：当前变换明确先绕原点缩放再刚体，非单位缩放不能让用户想当然地理解为“基准点必然映射到目标点”；结果缺专用矩阵表。
- **建议**：小示意明确原点、方向和变换顺序，显示当前矩阵/Pose，区分 Pose 不包含尺度；不更改现有运算语义。有限坐标校验见第8节。
- **依据**：`H/Coordinate/FixtureTool.cs:28–55,70–77,90–108,134–142`；`C/Views/PropertyGrid/Editors/PointPropertyEditor.cs:15–51`；参考 R6。

### 22 ImageFile — P1：原生成功与公共输出分开
- **已有 S**：路径选择器、校验、原生读取、公共快照；公共转换不支持时，Message **已经明确说明**“原生已读取但公共输出不可用”，不应报为完全没有提示。
- **问题 S/U**：该降级状态主要为文字，不如常驻状态明确。建议分开显示原生读取状态/公共输出可用性、实际路径/尺寸；不存在文件不要只显示笼统“校验通过”。
- **依据**：`H/Acquisition/ImageFileTool.cs:23–57,80–92,100–135`；`C/Presentation/Results/ToolResultText.cs:31–112`，默认路径保留 ToString 的 Message。真实格式读取属 R。

## 6. AI：4/4

共用事实：模型路径、DeviceId、阈值、最大结果数在参数页；Provider 在模型页；显式加载按钮在参数页底部，模型页用文字提示返回参数页。**已有模型加载安全机制**，但操作上下文跨页。`C/Views/Editor/ToolDetailsView.xaml:301–336,419–455`。

### 23 Classification — P1：有效参数说明；P2：模型路径选择
- **已有 S**：类别ID/名称/分数表，模型/设备参数和显式加载。
- **问题 S**：路径没有 FileDialogFilter，实际仍用文本输入，未复用现有文件选择器；路径到底是文件/目录由 Provider 解释。HALCON 路径分类没有消费公共阈值/数量请求，需就近说明适用性，见第8节。
- **建议**：Provider、模型文件/目录、设备、加载按钮及加载状态放在一个模型任务区；类型选择器按 Provider 规则，不靠猜扩展名、不隐式加载。
- **依据**：`Tools/AI/AIModelParameters.cs:23–41`；`Tools/AI/Classification/ClassificationTool.cs:22–39`；`C/Presentation/Results/VisionResultTable.cs:77–79`；参考 R10、R11。

### 24 Detection — P1：阈值后端适用性；P2：框尺寸
- **已有 S**：最小框面积(px²)、关闭语义、数量后过滤；专用检测结果表和图像互选。
- **问题 S**：HALCON 分数阈值未兑现，见第8节；专用表未列已有框宽高且表头单位不足。建议显示实际生效过滤及 W/H(px)，复用已有结果字段。
- **依据**：`Tools/AI/Detection/DetectionTool.cs:21–23,45–50`；`C/Views/Editor/ToolDetailsView.xaml:115–157`；`C/Presentation/Results/VisionResultTable.cs:80–83`；参考 R10。

### 25 Segmentation — P1：阈值/面积含义；P2：结果呈现
- **已有 S**：最小外接框面积、数量后过滤、逐项结果、轮廓绘制、是否提供掩膜字段。
- **问题 S**：HALCON 阈值未消费；不要将外接框面积解释为真实分割面积。建议按已有后端结果显示“轮廓/框/掩膜是否可用”，补像素单位；对未提供的掩膜不伪造叠加图。
- **依据**：`Tools/AI/Segmentation/SegmentationTool.cs:20–22,44–49`；`C/Presentation/Results/VisionResultTable.cs:84–88`；`C/Presentation/Rendering/VisionResultDrawing.cs:38–48`；参考 R10。

### 26 AnomalyDetection — P1：任务能力、阈值语义
- **已有 S**：异常判定/原始分数/定位空态；后端按模型解释分数，非法能力会校验拒绝，不是静默执行。
- **问题 S**：Provider 下拉只显示是否可用，不显示是否支持当前异常任务；“置信度阈值/最大结果数”容易被理解为分类/检测语义。
- **建议**：支持性说明、任务级“异常判定阈值”名称及默认来源；无用途的数量参数不假装有效。设备同样按已实现能力呈现，不能宣称已有 GPU 选择。
- **依据**：`Tools/AI/AnomalyDetection/AnomalyDetectionTool.cs:13–30`；`Tools/AI/Internal/AIToolBase.cs:98–102`；`AI/Halcon/HalconAIProvider.cs:438–440`；`AI/Aiditron/AiditronProvider.cs:37–38`；`AI/Invi/InviProvider.cs:44–45`；参考 R10、R11。

## 7. Common：2/2

### 27 Geometry — P1：按运算只显示参与项
- **已有 S**：点距、无限线求交、圆直径、较小夹角；专用点编辑；对应运算结果/单位状态。注册真实指向 `H/Measurement/GeometryTool.cs`，不是另一个假定实现。
- **问题 S**：运算切换不隐藏第二组线/圆心/半径等无关项；手动配方值和本次输入单位易混淆。
- **建议**：按运算显示有效字段与图形示意；注明有限线段/无限支撑线和0–90度无方向较小夹角。不能把平行线夹角0度禁掉，也不能把未声明单位硬标mm/Pixel。
- **依据**：`H/Measurement/GeometryTool.cs:15–38,110–177,183–235`；`C/Services/Catalog/ToolRegistration.cs:31–34`；参考 R6。

### 28 Tolerance — P1：缺测量输入的可见状态
- **已有 S**：上下限含边界、单位、显式本次测量输入、OK/NG结构化结果；缺输入会明确拒绝，不拿0判定。
- **问题 S**：配方参数校验通过并不代表本次测量已提供；工具参数区没有只读的测量值/单位/缺失状态。
- **建议**：工具内部显示“本次测量值/单位/可用状态”，缺失时就近解释；单位行标明“匹配输入单位，不做换算”。不增加持久化测量值当作假输入。
- **依据**：`Vision.Core/Tools/ToleranceTool.cs:12–17,38–75`；`C/Presentation/Results/VisionResultTable.cs:36–38`。

## 8. 不能只靠 UI 修正的关联发现

这些是逐工具审查发现的执行/校验风险，**本次未修改，不混入纯 UI 工作量**：

| 项 | 静态证据 / 待验边界 | 正确处理方式 |
|---|---|---|
| AI/HALCON 分类的阈值/数量、检测和分割的分数阈值 | S：`AI/Halcon/HalconAIProvider.cs:183–188,312–319`；`AIToolBase.cs:230–265`。请求传入，但分类/检测/分割读取不消费 ScoreThreshold；分类无后过滤，检测/分割现有后过滤不含分数 | UI先诚实呈现适用性；是否补统一过滤需明确语义并另做真实红绿，不靠提示声称功能已实现 |
| AI设备能力 | S：DeviceId为自由文本；HALCON仅默认/CPU，Aiditron/Invi拒绝非空设备请求 | 按已实现能力提示/限制；不擅自枚举GPU、自动降级或安装Runtime |
| HALCON Blob | S：有限值/合法特征/Logic检查不足，`H/Inspection/BlobTool.cs:155–181` | 补后端守卫与字段错误映射，保留旧配置并可诊断；真实特征口径另验 |
| HALCON ShapeMatch | S：大小比较可能让NaN绕过，起始角有限性不足，`H/Matching/ShapeMatchTool.cs:110–134` | 有限值验证；不得认为数值控件能替代导入配置校验 |
| HALCON DataCode空值路径 | S：一次以两类码制数组建模型；是否允许为R，`CodeReaderTools.cs:153–156` | 核对23.11正式契约/授权Runtime后修正，不仅加下拉框 |
| N点标定重复/共线和质量指标 | S：至少三点/有限值，不含重复共线预检；残差未提供 | 预检与后端质量计算独立立项，不用UI伪造残差 |
| Fixture非有限点和变换顺序 | S：点坐标有限性不足；缩放后刚体的既有语义，`FixtureTool.cs:70–77,90–108` | 补验证并准确解释；不借UI优化悄悄改变矩阵语义 |

## 9. 建议实施顺序与验收条件

### 第一批：减少错误操作，不改布局骨架
1. 共用字段/分组错误定位：关联现有验证，关系错误可定位到参与字段；保留绑定异常提示。
2. Morphology/ImageFilter/AdaptiveThreshold 奇数步进，ImageConvert/ImageFilter/IntensityTransform/Geometry 模式生效项；ColorSegment 标签/边界/环绕状态。**显隐和验证策略一并核实，避免隐藏字段仍阻断却找不到原因。**
3. ROI 页用途标识；Crop“已应用/未应用”；FindCircle“圆心选择域”；找线测量方向。保留原ROI确认/取消机制，不默认自动提交。
4. AI模型上下文集中，Provider/任务/设备适用性、显式加载与需重载状态一起可见。

### 第二批：高频复杂工具的任务式编辑
1. TemplateMatch/ShapeMatch：模板准备、训练状态（仅HALCON）、搜索范围及预算预警、试运行结果的步骤引导；不新增第二个完整编辑器。
2. Blob：特征中文名/单位、合理默认范围、未知/重复配置提示；范围集合直接代理原DTO。
3. N点标定：点对表可用性、单位与退化提示、原有矩阵结果；图像拾点另做明确交互设计。

### 第三批：结果可读性和操作效率
- 统计直方图完整查看；检测框宽高/像素单位；Fixture矩阵；图像文件实际读取状态；可选择的执行后视图策略。
- 轻量实时预览、调参撤销、自动估参/自动调参需要分别界定状态与资源所有权；本次没有默认批准这些运行机制。

### 后续验证矩阵
- 28项逐一验证：默认配置可理解、切模式只操作适用项、空/非法输入定位、输入/参数变化清旧结果、结果与当前输入一致。
- 纯UI行为进既有Workbench/STA测试；业务校验用既有逻辑测试。新增模式显隐需测试通知，不是只添加元数据字符串。
- 表格：新增/删除/当前未提交单元格/执行、键盘粘贴、空值、中间编辑态；文件：取消、错误路径、运行锁、对话框迟到。
- 手工覆盖窄/宽窗口、深浅主题、DPI、键盘焦点和ROI确认/取消；源码审查不能代替这些验收。
- HALCON/AI真实模型、设备、精度、节拍在授权环境验收；不触碰MachineApp、不强停Native、不解除RequiresRuntime/Skip、不自动加载模型。

实施约束：复用原DTO、Session、VisionPropertyPanel、专用适配、ROI编辑器、结果会话与文件机制；不新增平行参数存储/执行器/注册表，不拆出28个新页面，不变更模型/坐标/来源契约。
