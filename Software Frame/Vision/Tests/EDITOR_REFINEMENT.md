# 编辑器收敛验证

## Vision 后端公共执行重构验收（本次工作区）

- 后端基类复用 `Vision.Core/Tools/ImageToolBase.cs`，统一校验、计时、错误结果和工具释放；HALCON 原生输入仍由调用方持有，公共图像转换的原生对象由工具释放。现有 `IImageTool` 保持双后端公共入口。
- AI 模型面板测试原预期未实现的独立控件；现有设计和 `Vision.Controls/docs/TOOL_UI_INTERACTION_AUDIT.md` 均为参数页编辑模型路径/设备、显式加载，模型页选择 Provider。测试改为验证实际页面、原 DTO、显式加载、只读及会话所有权；不新增或迁移界面功能。定向测试 **5/5 通过**。
- 仓库根目录执行 `dotnet build "Software Frame/Vision/Vision.sln" --no-restore -v:q`：通过。`dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-build --filter "Category!=RequiresRuntime"`：731 通过；`dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-build`：609 通过、0 失败，耗时 6.498 分钟。上次 300 秒超时不足以完成全量；此次正常退出，不能据此排除偶发挂起。
- 设置 `VISION_DUAL_BACKEND_SMOKE=1`，运行 `DualBackendRuntimeSmokeTests`：同一合成图经公共接口执行真实 HALCON/OpenCV Blob，均面积 600、中心 (29.5, 44.5)，1 项通过。证据及命令见本机 `Software Frame/Vision/artifacts/architecture-refactor/verification.md` 与同目录 TRX/log；该目录为本地产物，不应误认为已提交。
- 上述证据覆盖本次重构的构建、自动回归和双后端单图冒烟，不代表其他 HALCON Runtime 用例、AI 模型、真实设备、物理 UI 或工业精度验收。

## 模板操作引导

- 复用唯一 `ToolDetailsView` 模板页及 `TemplateEditorViewModel`：显示 ROI 轴对齐矩形取样 → 参数页设置搜索 → 执行的顺序，以及真实取样图尺寸/格式；仅加载模型不等于有取样图。
- OpenCV 参数类型才显示固定角度、累计1亿响应像素的限制和实际精化取决图像的说明；该工作量不是内存、耗时或成功率预测。需要训练的工具提示显式训练/加载及训练变更失效；不新增估算公式、模型、执行器、自动训练或实时运行。
- 测试先4项全红，随后定向7项全过；切工具、只读锁、取样/清除、只有模型无取样及借用生命周期均覆盖。完整731逻辑通过；WPF 首轮71项后90秒 hang dump 中止，点绑定及模板定向14项通过；全量复跑一次604项中126通过、478因共享 Application.Current 为 null 连锁失败；再次原命令全量 **604通过、0失败/0跳过**。前两次失败原因未证实，不称已修复偶发宿主/挂起问题。
- Vision 宿主隔离增量构建0警告/0错误。未执行人工鼠标/主题DPI、HALCON Native、真实模型或实图精度/节拍验收。保留进入本轮前暂存区和工作树，不修改MachineApp。

命令（根目录串行，追加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`）：

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/template-guidance-diagnostics"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
```

日志 `%TEMP%/vision-template-guidance-{red,green,focused,logic,wpf,hang-recheck,wpf-final,wpf-final2,host}.log`；首次超时 dump 在 `Software Frame/Vision/Tests/obj/template-guidance-diagnostics`。本轮为说明与状态展示，不声称实时预算预估、搜索ROI、用户掩膜或工业实时能力完成。

## 工具属性交互第二批：ROI 用途、裁剪状态与 Blob 角度

- 工具编辑器 ROI 页就近区分搜索域、模板取样、找圆的圆心选择域和仅标注；裁剪工具显示当前裁剪参数、生效像素范围，以及所选矩形与参数是否一致。选择、拖动、取消、重新应用均不隐式修改裁剪配方。
- 原 ExportConfiguration 的 ROI 集合此前只复制列表、未复制内部 ROI；测试发现保存快照后编辑会污染快照。导出和导入时使用原 JSON 配置约定深拷贝 ROI，保持节点/Session/DTO 身份及格式不变。
- Blob 新增 phi 特征范围覆盖正负值；已有配方范围和其他特征默认值保留。提示 OpenCV phi 以弧度表示；HALCON 具体特征口径尚需授权环境验证。
- 红测：ROI 用途5项全失败；加入实现后组合测试9项6过3失败，修复 ROI 快照引用与 phi 默认值后，发现提示文本测试并补足说明；最终定向33项通过。完整 **731逻辑/600 WPF，0失败/0跳过**，隔离宿主增量构建0警告/0错误。
- 测试包含裁剪选区、显式应用、修改、取消、配置导出/恢复、窗口切换，及两个后端参数适配；不等于真实鼠标、HALCON Native、图像精度或节拍验收。保留进入本轮前暂存区及工作树；MachineApp 不修改/构建。

仓库根目录串行执行，均追加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/roi-purpose-diagnostics"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
```

日志 `%TEMP%/vision-roi-{purpose-red,blob-next-red,blob-next-green,blob-next-focused,blob-next-logic,blob-next-wpf,blob-next-host}.log`。其他工具任务式优化仍待后续批次。

## 工具属性操作UI第一批：步进、模式、颜色标签和字段校验

### 范围与实现

- 仅工具编辑器内部，不重排流程界面、不创建新工具页/运行器/参数副本。保留原DTO/Session/属性控件/快照/文件及五主按钮契约。
- `Tools/OpenCv/Processing/ProcessingTools.cs`、`ImageFilterTool.cs`、`AdaptiveThresholdTool.cs` 三处奇数核/邻域的元数据Step改2，原默认值/上下限及手输偶数拒绝规则不变。
- ImageFilter、IntensityTransform的Mode通知复用原IsVisibleProperty机制。仅Gaussian显示Sigma；Linear显示Gain/Offset，Gamma显示指数，Normalize不显示这些闲置项；切换保留原配置值。派生条件有JsonIgnore/IsDisplay=false，原JSON字段集合/旧默认值保持。
- `ColorSegmentTool.cs` 通过原元数据新增可选DisplayNameProperty，提供HSV/RGB六通道动态名称和只读范围说明。首通道控件不动态把Maximum从255压到179，以免合法RGB旧值240被切模式静默截断；HSV非法值保留且明确报错。首通道错误Member按实际不合法上下限定位，规则/最终掩膜算法不改。
- `ToolCardModel.ParameterValidation` 只借用现有ValidationResult；ToolCardWorkspace刷新/发布/失效时更新；`VisionPropertyPanel.ParameterValidation` 通过原ToolDetails绑定呈现摘要及按Member的字段提示。没有第二套校验。无Member/不对应当前适配行的错误仍在摘要中；隐藏字段有校验问题时强制可见，修复后按模式隐藏。
- `VisionPropertyItem` 沿原弱事件处理动态标签/可见性。新增显示属性不序列化、不恢复运行结果。P4元数据守卫只显式补充新增派生属性豁免，未放宽真实参数检查。
- 本批不处理全字段外部赋值通知：WPF交互测试通过实际数值控件输入后再切模式，避免将直接修改无通知原DTO字段误当用户交互。任意外部程序写值的即时同步、非法导入值的数值控件回写/非有限值、空参数组收拢仍列后续，不暗称全部解决。

### 实际红绿与结果

- 新增 `ToolParameterInteractionTests.cs`：16逻辑项先5过11红；覆盖3处Step=2、模式条件通知/重复赋值、隐藏值保留、JSON字段与旧默认兼容、隐藏非法项仍拒绝。
- 新增 `ToolParameterInteractionUiTests.cs`：修正测试FindName编译访问后，真实6项全红；实际NumericUpDown/ComboBox写原DTO、颜色标签/区间不钳制、字段错误/清旧结果/真实ExecuteAsync恢复。后增工具切换及卸载重载不串校验，共7项。
- 中间修正ValidationResult与WPF同名类型歧义；修正测试模板Grid定位，使用具名PropertyRow定位而非误匹配Numeric控件模板。错误区颜色动态资源在真实Application中有断言，不以XAML能编译当显示有效。
- 补颜色上限240实际错误定位红测1项，先失败再修改Member，最终落在H上限而非合法H下限。
- 定向20WPF通过；最终 **731逻辑/587WPF通过，0失败/0跳过，正常退出**。WPF7.6599分钟，最后逻辑46.5873秒。未依靠Delay/改同步/缩短覆盖或拼接定向结果充当全量。
- 隔离Vision宿主增量构建 **0警告/0错误**。不重试已知被用户工作台/VS锁定的默认输出目录，不结束用户进程；看新UI须关闭旧工作台并重新生成/启动。
- MachineApp/FlowNodes/厂商AI及HALCON算法不修改不构建；未连接硬件、加载授权模型、解除Skip。全量STA截图不是人工鼠标/焦点、全主题DPI、实图精度或节拍验收。

### 命令与证据

根目录串行，均追加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/tool-ui-batch1-diagnostics"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
```

日志：%TEMP%/vision-tool-ui-batch1-{red,logic-red,green,member-red,focused,logic,logic-final,wpf,host}.log。截图在 `Software Frame/Vision/Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/tool-ui-batch1/{color-space-error,filter-field-error}.png`。

下一批按审查单继续：ImageConvert/Geometry生效项、ROI用途与裁剪待应用、Blob特征范围、模板操作步骤、AI模型区整合。当前仅完成上列第一批，不称28项全部优化完毕。保留进入本轮前工作树与暂存区，不自动暂存/提交。

## 大图全角搜索被预算提前阻止：截图问题修复

- 用户截图明确报“姿态搜索响应像素预算超过1亿”。独立1920×1080输入/96×64模板、17.3度、-180/360度搜索范围先真实复现，1项红（166ms），未使用用户实图冒充实测。
- 根因是调度顺序：RunDiscretePoseSearch先计算完整原图的全角计划并直接报错，无法进入已有降采样/局部路径；不是预算常数太小就应无条件放开，也未发现相同Native阶段重复计费证据。
- 修改 `Tools/OpenCv/Matching/TemplateMatchTool.Pose.cs`：计划量先用局部变量预估；原图计划超剩余预算且角度/模板允许安全降采样（最小模板边≥32、非零角范围、步长>0.1）时，直接复用原RunRefinedPoseSearch，不执行/不计费被跳过的全图计划。真正执行的各阶段仍共用原1亿累计预算，不重置/提高上限，不改用户角度/尺度/最终分数。不能安全降采样或后续确实超限仍失败，诊断增加已用量、本阶段需求、搜索图/模板尺寸。
- 新增测试扩展到目标靠边(40,30)和位于图像内部(900,500)，保持独立逆映射、原图中心误差≤1像素、角误差≤0.25度及原0.94分数断言；2项转绿，原小模板响应预算/拥挤候选拒绝继续通过。
- 本轮完整 **715逻辑通过（1.2859分钟），定向26WPF通过（1.2253分钟），0失败/0跳过**。WPF覆盖图像来源交替、模板角度/尺度保存重开、原节点会话、菜单/文件/任务链。未重跑完整580WPF，不将上轮结果作为本轮全量证据。用户截图对应的真实图像/模板尺寸和参数尚未提供，不能声称原实图精度或节拍验收。
- 默认目录构建受 `devenv.exe (53328)`/`Vision.Workbench.exe (17876)` 锁文件阻止，产生MSB3027/MSB3021复制错误（220警告/26错误），不归为源码编译成功。未停止用户进程；改用同一项目及现有editor-verification隔离目录，增量构建成功 **0警告/0错误**。用户须停止调试/关闭旧工作台后重新生成再启动，不能让旧进程代表新代码。

命令（根目录串行，均加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`）：

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~VisionFlowR0Tests|FullyQualifiedName~VisionFlowP6Tests|FullyQualifiedName~ToolCatalogTests|FullyQualifiedName~TemplatePoseFlowTests|FullyQualifiedName~VisionExecutionImageTests' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/pose-budget-diagnostics"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
```

%TEMP%/vision-pose-budget-{red,green,logic,wpf,host,host-isolated}.log保留红绿/默认目录锁文件和隔离构建证据。MachineApp/FlowNodes不修改不构建；不安装Runtime/解除Skip/接硬件。保留进入本轮前工作树/暂存区，不自动暂存或提交。

## 属性栏底部按钮删除与OpenCV非采样角度漏检修复

### 修改与边界

- Nodes/VisionToolNodeBase及.PropertyGrid只去掉三个PropertyGridAction注解，不删除原命令/绑定/DTO；属性栏保持参数/来源/状态。Controls/Views/Flow/VisionFlowView顶部增加“视觉任务”菜单，绑定现有LoadImageCommand及CaptureFixtureReferenceCommand。双击/节点动作/原编辑器启动器不变，不修改共享FlowEditor/PropertyGrid消费者。
- OpenCvToolCatalog仅为新建目录实例配置AngleStartDeg=-180、AngleExtentDeg=360；Raw DTO/旧配置缺字段仍默认零角度/单位尺度。旧文件不偷偷升级；已存在的角度范围0配方仍须用户设置范围。
- TemplateMatchTool.Pose及新增.Refinement复用原匹配/NMS/模型与后台快照：足额离散解保留旧结果，不足额/不安全截断才进行降采样位置候选、合并局部窗、原图0.5度角度提议与0.1度局部采样。提议阶段用较宽门槛，不降低最终用户MinScore或改最终方法；原有有效目标保留，跨阶段候选遗漏上界最终判断。全部阶段累计1亿响应像素预算、100目标及配置1024姿态守卫不放开。
- 仍为有界采样，不是连续/亚像素优化；0.1度不是精度保证，有足额粗网格结果时不自动精化。大图全角/宽尺度范围仍可能超预算；没有用户模板掩膜、任意原点、完整形状金字塔或非单位尺度下游跟随。Native不可强停，仍等真实计算结束。

### 红绿和回归

- UI原失败：新增VisionNodePropertyPanel_DoesNotExposeBottomActionButtons，1项红，实际显示原三个PropertyGridAction；去掉注解并迁移菜单后转绿，检查菜单绑定的是原任务命令，编辑器关闭不释放节点的断言保留。
- 角度原失败：独立逆映射96×64非对称纹理模板到320×240场景，17.3°/-38.2°/179.4°及三种归一化方法5项全红（空匹配），最终全绿；最终分数保持0.94以上，中心误差不超过1像素、角误差不超过0.25度，测试断言未降低。新增范围外角度拒绝，以及旋转范围下拥挤候选明确失败；原固定/整数角度/缩放/快照/跨±180度/NMS回归保留。
- 中间定位到非连续ROI的Mat转换错误、降采样角度剪枝、原图局部窗截边及BBox奇偶变化造成的角度提议漏检，修正实现后才转绿；合并重叠局部窗避免重复扫描。临时种子日志代码已删除，不保留生产文件写入。
- 定向33逻辑/22WPF通过；最终增补预算守卫后完整 **713逻辑通过（18.4175秒），580WPF通过（7.3680分钟），0失败/0跳过，正常退出**。WPF首轮580项571过9失败：5项仍找已移除的基准按钮，迁移到真实菜单并保留命令身份/定位/跟随/重开断言；4项纯布局DPI测试的4×3重复渐变模板在新全角默认下触发候选预算，显式固定方向隔离其布局目的，不删除预算守卫，新默认由目录测试独立断言。修正后定向34项通过，再重跑全量580，不仅拼接定向结果。
- 默认Vision宿主构建 **64警告/0错误**，为未修改的AI辅助/SDK及RoiEditorView等文件警告，不称0警告。MachineApp/FlowNodes/厂商算法不修改、不构建、不连接硬件。未运行HALCON Runtime、实图精度/节拍、真实鼠标/焦点/主题DPI人工验收或共享控件其他宿主全量。

### 命令与日志

仓库根串行，以下均追加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/property-angle-diagnostics"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore
```

日志在%TEMP%/vision-angle-{offgrid-red,offgrid-green,logic-final}.log及vision-property-{actions-red,actions-green,angle-ui-recheck,angle-wpf-final,angle-wpf-final2,angle-host}.log。最终日志确认非零测试数与正常退出；保留进入本轮前工作树/暂存区，不自动暂存或提交。

## 工具内与流程执行的图像来源分离

### 修改

- VisionToolInstanceWorkspace区分流程默认图与工具本地图；原LoadImage/导入配置/本地裁剪标记本地输入，任务图更新经SetTaskImage，不抹掉本地导图。
- 工具编辑器直接执行原Card：明确导过本地图时，使用它且不解析流程图像/覆盖槽或上一轮流程定位；未导本地图则保持原绑定调试行为。流程入口仍调用原IVisionToolSession.RunAsync并由节点准备显式上游输入或流程默认图。
- IVisionToolSession增加有默认兼容实现的FlowInputImage；VisionToolConfiguration v1增加可选UseLocalInputImage字段，缺省false保持旧语义。任务图继续由原DocumentProperties持久化，本地图仍保存在原节点/工具配置InputImage；不新增DTO、绑定表或文件引擎。
- 默认图解析优先保留会话显式流程输入，再回退原任务图记录；没有图像输入不回退工具本地图。保持原帧号/时间戳/修改像素守卫、DTO与会话身份和结果归属；新增节点不会继承首个工具的本地调试图。
- 属性面板只讨论职责，本轮没有删除按钮或重排UI。执行/导图入口不新增按钮。MachineApp/FlowNodes和厂商算法不修改，不接硬件/安装Runtime/解除Skip。

### 验证

- 新增两例真实均值区分测试先2项均失败：手动无接线案例的流程执行误用本地图，接线案例的工具内执行被未运行上游阻止；随后转绿。
- 回归发现手动定位和帧号/时间戳拒绝行为受影响，保留原显式配置定位、仅隔离流程派生定位，并优先会话流程图；修复生产逻辑，未放宽原测试。
- 最终定向48通过，完整 **706逻辑/579WPF，0失败/0跳过**；WPF9.7017分钟正常退出。新增四项覆盖：任务图73/本地图201、上游阈值输出255/本地图201的交替执行；任务图改92、本地图保持201、后续新增节点和.flow重开；缺流程图不回退本地图。均使用真实OpenCV统计/阈值，不用mock值替代结果。
- Vision宿主默认目录构建 **64警告/0错误**；警告位于本轮未改的厂商AI辅助/SDK及RoiEditorView等文件，日志保留，不称0警告。未执行Native/人工UI或共享FlowEditor/FlowNodes独立套件，不借旧结果充当本轮证据。

### 命令

仓库根串行，均追加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/execution-image-diagnostics"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore
```

%TEMP%/vision-execution-image-{red,green,focused,focused-final,logic,wpf,host}.log记录真实红绿、回归修复及最终退出。未新增暂存/提交，保留进入本轮前的工作树和暂存区。

## 首节点Next_IN显示修复

- 删除VisionFlowViewModel中按Chart.Nodes.Count关闭视觉节点/Switch输入端口的两处赋值。新节点保留其原类型端口默认值，开始节点仍按原定义无输入；不改变共享FlowEditor的节点或校验器。
- 发现运行前FlowValidator与执行器入口判定不同：执行器按无前驱找入口，校验器仍要求启用的IN连接。VisionToolNodeBase仅通过现有IFlowValidationNode显式实现保留“本节点是画布第一项时可作隐式入口”的原语义；后续未连入视觉节点继续E-IN-REQ，Switch沿用原严格输入校验。端口可见性与Vision首节点入口校验分离，不放宽其他节点的通用规则。
- 旧.flow仍由原Mapper/Factory恢复IsInEnabled；显式false保持隐藏，新保存的true正常重开。没有自动迁移旧文件、修改连接/端口ID或创建平行入口机制。
- 新增VisionEntryPortTests：首个视觉工具有Next_IN且真实单独运行；之后添加Start并连入，保存重开后真实运行序号正确；后续孤立节点仍报IN缺失；首个Switch保留Next_IN及RequireInputConnection；旧文件关闭端口状态保持。原P1相应断言更新为新默认。
- 首轮2项1通过/1实际失败（首节点IsInEnabled=false）；中断的首次绿测没有完成结果，未作为证据。最终定向33项、完整706逻辑/575WPF全部通过，0失败/0跳过；WPF7.4567分钟正常退出。默认Vision宿主构建4条未改RoiEditorView CS8632警告/0错误。
- 未修改MachineApp/FlowNodes、未安装Runtime/触发设备/解除Skip；不把自动化端口/流程测试当物理鼠标或Native验收。没有新增暂存或提交。

命令在仓库根串行执行，均追加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/entry-port-diagnostics"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore
```

%TEMP%/vision-entry-port-{red,green,focused,logic,wpf,host}.log记录实际过程；未重跑共享FlowEditor/FlowNodes套件，不借上轮结果充当本轮通过。

## OpenCV模板离散角度与等比缩放首批

### 能力与边界

- TemplateMatchParameters新增角度起始/范围/步长、Scale最小/最大/步长、跨姿态旋转框重叠IoU。旧配置缺字段默认0角度、1倍，仍执行原固定姿态及原邻域抑制路径；模板仍为Mono8、中心仍按原模板几何中心口径。
- 姿态路径按Pose2D角度方向生成模板及内部有效采样区，排除旋转/缩放补边；验证当前OpenCV4.11三种归一化方法，过滤非有限响应。步长离散采样含范围终点，规范化角度到(-180,180]；计算姿态四角/外接框，多目标跨角度尺度使用旋转矩形IoU去重。
- 变体上限1024、总响应像素上限1亿、最大目标100，超限明确报错；低纹理、无有效尺寸和非法/非有限参数拒绝，不静默抽稀搜索。首批不是金字塔/粗细优化、连续或亚像素精化，也不承诺工业实时。
- 模板工具实现原IBackgroundImageTool：准备独立参数、模板及输入图快照，执行新临时工具；关闭等待实际Native调用，不能保证单次调用中断。原DTO/Session、.flow、.visionproj、模型文件及执行器复用。
- TemplateMatchNode投影原DTO新增参数，结果表和Fixture输出实际角度/Scale，画布绘制并拾取真实模板旋转四角，不把AABB当真实物体轮廓。现有VisionFixtureBinding仍拒绝非单位Scale；没有改变已有同帧/直接来源保护。
- **尚未实现**用户模板掩膜、可编辑模型原点、搜索ROI、出界/遮挡容忍、尺度跟随、亚像素/连续匹配或实图性能。内部排除补边的mask不冒充用户掩膜。HALCON/AI、MachineApp及FlowNodes源码未修改/未验证；未安装Runtime或解除Skip。

### 实际验证

- 首轮新增测试＋旧模板文件共21项：4通过、17实际失败；实现后21项全部通过。补全跨±180度、缩小后模板可放入搜索图、非有限响应/低纹理、两目标跨姿态去重及预算拒绝；最终定向49逻辑通过。
- 定向WPF/流程56通过：原属性直写、旋转/Scale真实执行、原节点ID和参数/模板随.flow重开、表格及四角拾取、非单位Scale下游拒绝和原模板/定位回归。
- 完整逻辑首轮701/702：新增公开只读派生字段不符合参数DTO元数据守卫；将仅算法使用的UsesPoseSearch改internal，最终 **702通过/0失败/0跳过**；补充候选预算和重叠边界后最终 **706通过/0失败/0跳过**。
- 完整WPF首轮376通过后90秒无进展，Blame终止宿主：ImageFilePathEditorTests.LateNativeSelection_CannotWriteNewNode_WhileOwnedEditorStillTargetsItsOriginalNode。dump中的UI线程处于WPF TimeManager.Tick/Dispatcher，测试线程等待Dispatcher；仅凭快照不能认定根因。该类单独复跑3通过，同一完整命令及同90秒门禁复跑 **571通过/0失败/0跳过，10.0168分钟正常退出；补充候选预算后再次完整复跑571通过、7.0954分钟正常退出**；未调整测试或超时，也不能声称偶发挂起已根治。
- Vision宿主独立产物目录增量 **0警告/0错误**；默认目录 **4条未改RoiEditorView CS8632警告/0错误**。未运行共享FlowEditor/FlowNodes独立套件，不将前次419等计作本轮证据。实图精度/节拍、人工鼠标/跨屏及Native HALCON未验。

### 命令与日志

根目录串行，以下均追加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/template-pose-diagnostics"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore
```

%TEMP%/opencv-template-pose-*保存red、green、logic-focused、wpf-focused、logic（失败）、logic-final、wpf（超时）、filepicker-recheck、hang-stacks、wpf-final、host及default-host日志。以下为历史轮次，不能以历史数量替代本次结果。

## 统一工具注册与基础节点组合

### 修改与边界

- 复用FlowNodeCatalog的反射目录；FlowToolDiscoveryService合并模块提供的ToolMetadata（ToolId＋延迟Factory），身份冲突在发布前拒绝，同一描述引用重复提供幂等，不把同CLR契约的多个视觉工具误合并。
- ToolboxService同步/异步共用描述投影与分组搜索；VisionFlowViewModel通过UseToolboxAsync与原容器共用服务，去掉手写Switch、单独分类/搜索及覆盖默认集合的调用。节点实际创建/命名/图像继承与原会话观察仍归原VM；原Factory/Mapper恢复文件，原算法与执行器不改。
- 当前工具箱35项：28视觉＋7原控制节点（开始、结束、延时、日志、条件分发、循环、分支汇聚）。旧Condition隐藏；容器/并行暂不开放新建，文档解析目录仍保留全部已有类型。新Switch仍要求显式输入，旧文件常量语义不变。
- VisionFlowRegistration.CreateToolRegistrations是可选描述入口，其他宿主可筛选或不加入；真正使用需另组合原节点类型目录与VisionNodeFactory，不把描述入口说成完整接入。FlowNodes.Workbench源码未改、未默认加入Vision；MachineApp未改/构建/验证。
- 不新增项目、包、工作流框架、平行DTO或文件引擎，不实现动态来源刷新/插件热加载或新的撤销机制。现有八个具体视觉节点与其余20个通用入口保持不变。

### 实际验证

- 基线25通过；新增首轮9项中1通过/8实际失败（缺基础工具项），实施后9项全绿；随后定向64通过。补充其他宿主可选两项、循环三次原会话与延时停止取消，最终VisionNodeRegistrationTests共13通过。
- 共享FlowEditor首次误用Vision产物目录，419中63项因测试从输出路径向上定位FlowEditor.sln失败。改用FlowEditor模块下的隔离产物目录，不改测试寻根逻辑或断言；随后发现3项实际回归：新增投影方法缺文档、DryIoc自动选择扩展构造器并错误解析集合/委托。补方法说明并在原FlowEditorModule显式选择目录构造入口，最终 **419通过/0失败/0跳过**。
- Vision逻辑首次678通过/1失败：精确依赖白名单尚未包含负责统一注册的Services/Composition/VisionFlowRegistration.cs。仅新增这一明确装配边界，保留FlowNode不得依赖Controls/Workbench的断言，最终 **679通过/0失败/0跳过**。
- Vision WPF最终 **568通过/0失败/0跳过**（555＋13），**9.6721分钟正常退出**，包含混合开始→延时→真实灰度统计→结束的.flow重开执行、原端口/节点ID、会话隔离、循环与取消。RequiresRuntime仍排除，不代表Native或硬件通过。
- Vision隔离目录显式增量构建0警告0错误；默认目录重编5警告0错误（未修改ConfigManagerView.cs的CS8603及RoiEditorView的4条CS8632）。FlowNodes.Workbench隔离目录消费者构建9警告0错误，均位于本轮未修改的Device/ScriptEditor/FlowNodes文件；未运行其独立测试或接硬件。
- 未人工验鼠标/OLE/设计器/主题DPI；未安装Runtime、解除Skip、加载授权模型。保留进入本轮前的暂存区，不新增暂存或提交。

### 命令与日志

根目录串行，以下均追加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/registry-diagnostics"
dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/registry-verification" --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/FlowEditor/Tests/obj/registry-diagnostics"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore
dotnet build "Software Frame/FlowNodes/FlowNodes.Workbench/FlowNodes.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
```

FlowEditor隔离产物初次由同命令省略--no-restore完成还原。%TEMP%/vision-tool-registry-*保存baseline、red、green、focused、lifecycle、floweditor、floweditor-module、floweditor-final、logic、logic-final、wpf、host、default-host与flownodes-host日志。以下均为历史轮次，不以旧数量替代本轮结果。

## 2026-10-26具体节点第三批：阈值、统计、几何、公差

### 修改与保持

- 新增Nodes/OpenCv/ThresholdNode、GrayStatisticsNode及Nodes/Common/GeometryNode、ToleranceNode；分别声明原常用DTO字段和数值输入、统计输出、几何/参考测量输入及结果、公差测量输入和布尔结果。字段名、默认值、单位及选择模式均不改变，不创建参数副本或新执行器。
- Definitions索引八份具体节点声明，其余20工具仍用原通用节点及宿主白名单。VisionNodeFactory路由新实例，并在创建会话前拒绝具体类型/工具配置不匹配；原通用CLR/vision.tool.instance、新旧flow字段与端口ID保留。未迁移白名单回归从本批已迁移的Threshold改为仍未迁移的Morphology，不删除其覆盖。
- 原契约用例扩展到八类，直接比较同IN/OUT状态下的原端口名称/属性/顺序，参数编辑前清IsModified以验证真实回写及置脏；Geometry.Operation按原枚举类型设置。新旧文件用例纳入八个具体节点+一个通用节点，保留此前模板独立像素、圆字段、节点/端口/连接身份与原参数断言。
- 原算法、Core契约、共享绑定、后台快照/运行锁/结果失效及关闭释放未修改；Common/Geometry只是原数学工具的流程适配，不宣称新HALCON或Native能力。MachineApp、FlowNodes和端口折叠不在本轮；没有新增项目或依赖。

### 红绿与最终结果

- 扩展的ConcreteVisionNodeTests先17项中8通过/9实际失败，原因是新增四种工具仍返回通用类型/尚未注册具体文档身份；实施后同17项通过。新增4个参数/类型/端口案例和4个错类型拒绝案例，文件用例扩展但数量不变。
- 定向 **64通过/0失败/0跳过**：ConcreteVisionNodeTests、VisionNumericBindingTests、VisionOtsuTests、VisionToleranceTests、VisionDispatchTests、VisionLinePairTests、VisionPointPairTests、VisionBasicToolsE1Tests。包含真实数值→阈值、Otsu拒绝不适用绑定、点/线几何、公差→Switch及缺失/旧结果/单位/异来源拒绝链，未放宽旧断言。
- 最终完整 **679逻辑/555WPF，0失败/0跳过**，WPF **6.9187分钟正常退出**。555=前轮547+本批8个理论案例；RequiresRuntime仍排除，不能把Skipped0当Native通过。
- 宿主独立产物目录显式增量构建 **0警告0错误**，默认目录显式生成 **4条未改RoiEditorView nullable警告/0错误**，不冒称重编警告已消除。最终命令退出码0，非零预期测试数/正常退出/Unhandled exception、Test host process crashed、No test matches复核通过；diff-check=0，暂存为空，未提交。
- MachineApp未修改/构建/验证；未安装Runtime、接硬件、加载授权模型或解除Skip。Native、物理UI/设计器、实图精度节拍仍未验；共享FlowEditor、PropertyGrid、FlowNodes独立测试套件本轮未运行，不借历史数作当前证据。

### 命令与日志

仓库根串行执行，以下命令均追加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/measurement-nodes-diagnostics"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore
```

红例filter为FullyQualifiedName~ConcreteVisionNodeTests；定向filter为 `Category!=RequiresRuntime&(FullyQualifiedName~ConcreteVisionNodeTests|FullyQualifiedName~VisionNumericBindingTests|FullyQualifiedName~VisionOtsuTests|FullyQualifiedName~VisionToleranceTests|FullyQualifiedName~VisionDispatchTests|FullyQualifiedName~VisionLinePairTests|FullyQualifiedName~VisionPointPairTests|FullyQualifiedName~VisionBasicToolsE1Tests)`，结果目录Tests/obj/measurement-nodes-focused。%TEMP%日志共用vision-measurement-nodes-前缀：red.log、focused.log、logic.log、wpf.log、host.log、default-host.log、diff-check.log。下方四个具体类/其余24工具等属于上一阶段记录，当前以八个具体类/其余20工具为准。


## 2026-10-26具体节点第二批：找圆与模板匹配

### 修改和边界

- 新增Vision.FlowNode/Nodes/OpenCv/FindCircleNode.cs、TemplateMatchNode.cs，均直接继承VisionToolNodeBase。找圆声明RadiusMin/RadiusMax、Circle/Center及原圆/点端口；模板声明MinScore/MaxMatches及定位候选输出端口。字段定义仍只描述原DTO/结果，不增加参数、执行器或模型副本。
- 定位候选输出从公共基类移到TemplateMatchNode与旧VisionToolNode构造中；原端口名称/绑定属性/顺序保持，通用HALCON ShapeMatch等仍按原会话能力建立端口，未运行Native正向。原共享绑定、窗口借用、快照、运行锁、失效和释放逻辑不变。
- 原Definitions索引四个具体类的同一份声明；VisionNodeFactory路由新类型并在分配会话前校验类型与ToolIdentity。稳定标识新增vision.opencv.find-circle、vision.opencv.template-match；旧vision.tool.instance继续按原类型恢复，未改flow版本/字段，不冒称全历史文件均兼容。
- 当前四个具体工具，其余24工具保持通用接入和原宿主参数白名单；不修改算法、模板/模型加载策略、图像或几何单位/来源规则，不增加项目或Controls/Tools反向依赖。FlowEditor输入/输出折叠仍未实施，MachineApp不在范围。

### 实跑证据

- 扩展原ConcreteVisionNodeTests，9项先有4通过/5失败：新增找圆、模板类型未接入导致实际类型/文件/错类型拒绝预期失败。实施后同9项通过；新增两个参数/端口/DTO案例和两个错类型案例，文件用例扩展到四个具体节点+一个通用节点，不删除旧断言。
- 文件往返验证圆/点输出字段、模板4×4独立像素、新旧节点类型/ID/端口ID/连接与原参数；模板取样恢复不自动训练或加载模型。端口在相同IN/OUT状态下与原通用实例逐一比较。
- 定向 **44通过/0失败/0跳过**，包含ConcreteVisionNodeTests、VisionFindCircleFollowTests、VisionFlowP3Tests、VisionFlowP5Tests、TemplateEditorWorkflowTests；实际模板匹配→圆心域/期望点跟随、断线/失效/异Crop拒绝和旧入口行为保留。
- 最终完整 **679逻辑/547WPF，0失败/0跳过**，WPF **8.6832分钟正常退出**。547=前轮543+2个具体节点参数案例+2个错类型案例；RequiresRuntime仍排除，Skipped0不代表Native通过。
- 默认目录宿主显式构建 **4条未改RoiEditorView nullable警告/0错误**；独立产物目录显式增量构建 **0警告0错误**，不代表原警告已消除。最终退出码0，非零预期数量、正常退出及Unhandled exception/Test host process crashed/No test matches均复核。diff-check=0，暂存为空，未提交。
- MachineApp未修改/构建/验证。未接硬件、未安装Runtime或加载授权模型；Native、物理UI/设计器、实图精度节拍仍未验；共享FlowEditor、PropertyGrid、FlowNodes独立套件本轮未运行，不借历史数字作当前证据。

### 实际命令与日志

仓库根串行执行，以下命令均追加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/circle-template-nodes-diagnostics"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore
```

红例filter为FullyQualifiedName~ConcreteVisionNodeTests；定向filter为 `Category!=RequiresRuntime&(FullyQualifiedName~ConcreteVisionNodeTests|FullyQualifiedName~VisionFindCircleFollowTests|FullyQualifiedName~VisionFlowP3Tests|FullyQualifiedName~VisionFlowP5Tests|FullyQualifiedName~TemplateEditorWorkflowTests)`，结果目录为Tests/obj/circle-template-nodes-focused。%TEMP%日志共用vision-circle-template-nodes-前缀：red.log、focused.log、logic.log、wpf.log、host.log、default-host.log、diff-check.log。下方首批的仅Blob/找线、其余26工具等属于阶段记录，由本批四个具体节点/其余24工具替代。


## 2026-10-26视觉节点基类与具体节点首批

### 实现范围

- 原公共实现归Nodes/VisionToolNodeBase及其partial，保留原Core会话、输入准备、结果归属、连接失效和异步归还；新建OpenCV Blob/FindLine直接派生具体类，分别位于Vision.FlowNode/Nodes/OpenCv/BlobNode.cs、FindLineNode.cs。具体类声明自己的关键参数、结果字段、数据端口和工具身份约束，不复制算法或DTO。
- Definitions/VisionNodeDefinitions仅索引这两类的同一份ToolDefinition；属性网格继续直接读写Session.Parameters。通用VisionToolNode保留其余26工具及旧.flow的接入和端口构造，仍使用宿主原参数白名单；不为本轮新增Tools/Controls引用或平行能力/执行框架。
- Controls、绑定helper及通用测试消费VisionToolNodeBase；原来检查唯一通用实现的IsType断言改为公共基类IsAssignableFrom，算法/参数/连接/生命周期断言不变，新增用例严格检查两个具体运行时类型和直接基类。
- 新实例经VisionNodeFactory.CreateToolNode路由。旧CLR身份Vision.FlowNode.VisionToolNode及稳定标识vision.tool.instance仍按原类型物化，不偷偷替换；新节点分别使用vision.opencv.blob、vision.opencv.find-line。原flow字段/状态/端口ID和mapper/store保持；类型与配置工具不匹配时在创建会话前拒绝并保留原画布。此为源码API结构调整，不宣称旧二进制消费者兼容。
- 共享数值/线/圆/点/布尔准备逻辑仍沿原partial/helper，不声称所有能力均已从基类组合拆出；本轮不扩大到其他工具，不实现FlowEditor输入/输出折叠，不改MachineApp或FlowNodes代码，不新建项目/执行器。

### 实际红绿和最终结果

- 改动前基线35通过：VisionFlowP2Tests、VisionFindLineFollowTests、VisionBlobCountTests、VisionFlowR0Tests，3.3965分钟正常退出。
- 新ConcreteVisionNodeTests初始4项全红，症状为Blob/找线仍返回通用类型。实现后首次39项中36通过：原35全部通过，新增3项失败是测试将独立节点默认IN状态与首节点入口状态直接比较，以及忽略测试Dialogs.ShowError抛错；对齐原机制后新增4项通过，未改生产控制流或错误传播。
- 最终审查补未迁移工具白名单用例取得1红，修复通用节点不应强制采用全局定义的回归，仍保留宿主原投影；新增5项及原R0共13通过。新用例验证具体类/定义身份、原DTO回写、与旧节点同IN/OUT状态下的端口契约、新旧文档类型/节点及端口ID/连接/参数恢复、错类型不替换原画布、未迁移宿主白名单保持。
- 完整首轮679逻辑/542WPF通过；最后修复之后重新完整运行，最终 **679逻辑/543WPF，0失败/0跳过**，WPF **10.6759分钟正常退出**。543=前轮538（包含固定按钮新增样式用例）+本批5项，不删除测试以维持数字。
- 独立产物路径和默认输出目录宿主均显式构建成功。首次产物重编4条RoiEditorView nullable警告，默认目录首次6条未改文件警告；最终两种目录增量构建均0警告0错误，不代表原警告已消除。最终生产代码全量验证后未再修改。
- 最终命令退出码0，预期非零测试数、正常退出及Unhandled exception/Test host process crashed/No test matches均复核；diff-check=0，未暂存/提交。MachineApp不修改/构建/验证。RequiresRuntime仍排除，HALCON/授权模型/真实设备/物理UI/实图节拍未验；FlowEditor、PropertyGrid和FlowNodes独立测试套件本轮未运行，不引用历史数作为本轮证据。

### 命令与日志

仓库根，以下串行命令均追加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/concrete-nodes-diagnostics"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore
```

基线沿WPF命令，filter为 `Category!=RequiresRuntime&(FullyQualifiedName~VisionFlowP2Tests|FullyQualifiedName~VisionFindLineFollowTests|FullyQualifiedName~VisionBlobCountTests|FullyQualifiedName~VisionFlowR0Tests)`，诊断目录concrete-nodes-baseline；首轮定向加ConcreteVisionNodeTests，诊断目录concrete-nodes-focused；最终13项filter为 `Category!=RequiresRuntime&(FullyQualifiedName~ConcreteVisionNodeTests|FullyQualifiedName~VisionFlowR0Tests)`。初始红filter为FullyQualifiedName~ConcreteVisionNodeTests，补充红为FullyQualifiedName~UnmigratedTool_KeepsOriginalHostParameterProjection。

%TEMP%日志共用vision-concrete-nodes-前缀：baseline.log、red.log、focused-first.log、new-focused.log、fallback-red.log、final-focused.log、logic-final.log、wpf-final.log、host-final.log、default-host-final.log、diff-check.log。wpf-first.log为最后白名单修复前的542通过，不当作最终543证据。文件/职责说明见[Vision.FlowNode README](../Vision.FlowNode/README.md)。


## 最新：操作按钮固定显示（2026-10-26）

按用户确认，删除VisionToolEditorView.ShowProjectToolbar、ShowExecutionButton及其DependencyProperty、文件按钮显隐触发器、执行按钮Visibility绑定和不再使用的BooleanVisibility转换器。打开、保存、另存为、导图、执行五项固定显示，不再依赖Root绑定使按钮出现。HasCurrentTool/HasProjectCommands及原CanExecute继续只负责禁用，不改变原会话、文件、运行锁或窗口所有权。旧隐藏测试改验900/1300宽度下五项始终显示及原命令/会话身份；节点窗口改验实际按钮可见，不以已删除开关值代替界面断言。

原新增样式用例先复现Root未解析时Collapsed；本轮退役开关检查再取得1项实际红，随后定向 **24通过、0失败/0跳过**（VisionToolEditorCompositionTests、VisionFlowR0Tests、NodeToolConfigurationTests），包含真实文件操作、空会话禁用、换节点及后台运行锁。Vision宿主显式增量构建 **0警告0错误**。未手动打开Visual Studio设计器，不将STA样式/布局验证称为设计器人工验收；本轮未重跑完整逻辑/WPF或Native测试，不沿用前轮全量数量作为本轮证据。MachineApp未改、未构建；未暂存/提交。

%TEMP%日志：vision-designer-toolbar-red.log（原Collapsed复现）、vision-fixed-toolbar-red.log（开关退役红）、vision-fixed-toolbar-focused.log（最终24通过）、vision-fixed-toolbar-host.log、vision-fixed-toolbar-diff-check.log。下方隐藏开关约定属于历史，由本节替代。

实际定向命令（仓库根，xUnit v2/VSTest，沿原产物路径）：

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime&(FullyQualifiedName~VisionToolEditorCompositionTests|FullyQualifiedName~VisionFlowR0Tests|FullyQualifiedName~NodeToolConfigurationTests)' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/fixed-toolbar-diagnostics" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
```


## 2026-10-26Compatibility目录移除

### 删除与保留边界

- 原Compatibility共17文件：删除14个废弃文件（两个控件别名、空端口面板XAML/代码、十个旧URI转发）；另删Themes/Generic.xaml，其内容只服务旧别名的空模板。目录及空子目录实际删除，csproj不留Page/Resource/LogicalName转发声明。
- 两个类型不是空壳：FlowNodesEditorAdapter/AINode实际使用ToolEditorView，AINode实际拥有DetectionToolSession。其3个源码分别归位Views/Editor/ToolEditorView.xaml(.cs)、ViewModels/Execution/DetectionToolSession.cs；原CLR身份、DI注册、Tool属性、参数/结果/模型准备以及借用/卸载边界保留。未修改FlowNodes代码，不把这些功能误删或换成空转发。
- 完整编辑器、线ROI、嵌套Class/List模板及相关测试直接使用正式VisionPropertyPanel；VisionMultiSelectComboBox实际实现不变。旧Ctr_PropertyPanel、Ctr_MultiSelectComboBox、Ctr_ToolPortBindingPanal与View/*、Ctr_Base/*、Ctr_Halcon/*资源URI明确退役；旧URI正向检查迁到正式Views/*或Themes/*，正式主题内容与自身合并层级保持。不承诺MachineApp或其他外部消费者兼容。

### 当前实跑证据

- 新CompatibilityLayer_HasNoExportedAliasesOrPackResources先取得**1项实际断言红**（旧Ctr_PropertyPanel仍导出）；删除后断言同时检查旧CLR/资源不存在、必要类型仍导出。原目录守卫同步要求Compatibility目录不存在、有效实现存在于正式目录。
- 定向 **78/78通过**，包括命名/资源、正式属性面板与嵌套行、线ROI、旧节点入口、换参/重绑/卸载及真实薄宿主装配；没有删除或放宽原算法、编辑、文件和生命周期断言。
- 完整 **679逻辑/537WPF，均0失败/0跳过**，WPF **5.3330分钟正常退出**；537=前轮536+1退役负向守卫。RequiresRuntime仍排除，不能据此声称Native通过。
- Vision独立宿主显式增量构建 **0警告0错误**，不代表历史重编nullable警告消除；FlowNodes实际消费者显式构建 **0错误、3条既有nullable警告**（ClassTest.ID/Name、VisionToolNodeBase.Image）。消费者源码无差异。
- MachineApp未修改/构建/验证；未接硬件、未安装Runtime、未加载授权模型或解除Skip。Native/物理UI/实图精度节拍仍未验；FlowNodes、共享FlowEditor/PropertyGrid独立测试套件未运行。未暂存/提交。

仓库根串行执行，以下命令均追加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/compatibility-retirement-diagnostics"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
dotnet build "Software Frame/FlowNodes/FlowNodes/FlowNodes.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
```

红例沿WPF入口及同一产物路径，filter为 `FullyQualifiedName~CompatibilityLayer_HasNoExportedAliasesOrPackResources`，不启用挂起收集；定向filter为 `Category!=RequiresRuntime&(FullyQualifiedName~ControlsNamingTests|FullyQualifiedName~NestedPropertyPanelTests|FullyQualifiedName~LegacyEntryConvergenceTests|FullyQualifiedName~LineRoiUnificationTests|FullyQualifiedName~VisionPropertyPanelTests|FullyQualifiedName~EditorRefinementTests|FullyQualifiedName~WorkbenchCompositionTests)`，诊断目录为 `Tests/obj/compatibility-retirement-focused`。

%TEMP%日志共用 `vision-compatibility-retirement-` 前缀：`red.log`、`focused.log`、`logic.log`、`wpf.log`、`host.log`、`flownodes-build.log`、`diff-check.log`。最终命令退出码0，预期非零测试数量、正常退出和未处理异常/宿主崩溃/零匹配已复核。下方Compatibility及旧URI“保留”均为历史范围，由本节替代。

## 2026-10-26退役API的测试同步（用户确认后执行）

- 续接时核实唯一编辑器和节点文件操作已落盘；后续删除 ImageViews、演示图/对比/基准API及目录 Compare 回调后，测试仍引用旧接口，定向命令在**编译阶段失败，未执行测试**。未把前轮536通过当作当前通过。
- 经用户选择“同步测试并验证”，仅更新原测试工程：目录构造调用删除废弃实参，模板/工程选路与图像加载回调保留；原演示像素改由测试夹具复用 SyntheticImages.WhiteRectangle(64,64,10,10,30,30)，通过现存 LoadImage 加载，不打开桌面选图对话框，不恢复生产API或增加兼容层。
- 图像选项断言直接核查实际 ListBoxItem 的四个标题和 ImageViewKind Tag；保留图层切换不裁剪/不改鼠标模式的断言。旧对比/基准入口测试改验原卡片真实异步执行、无输入明确失败、失败不重复执行及已退役公开属性不存在；共享会话/关闭/文件IO/DTO身份/连接与全部算法断言保持。
- 定向首轮53中52通过、1失败：新增用例误认为无输入时卡片命令禁用。检查原CanExecute和算法入口后，改为等待真实ExecuteAsync并断言Blob失败结果及image缺失诊断，**未改变生产命令或时序**。随后53/53通过；完整536数量不变，0失败/0跳过，7.0782分钟正常退出。
- 本轮实跑 **679逻辑/536WPF，0失败/0跳过**；独立Vision宿主显式增量构建 **0警告0错误**，不代表历史重编警告已消除。所有最终命令退出码0，日志无Unhandled exception、Test host process crashed或No test matches；diff-check=0，暂存为空，未提交。
- MachineApp不修改/构建/验证；没有生产代码修改，没有依赖/Runtime安装、解除Skip或加载模型。RequiresRuntime仍排除；Native、真实设备、物理UI、实图精度/节拍及共享FlowEditor/PropertyGrid/FlowNodes独立套件本轮未验。

实际命令从仓库根串行执行，均追加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/retired-api-diagnostics"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
```

定向沿上述WPF命令，filter为 `Category!=RequiresRuntime&(FullyQualifiedName~EditorRefinementTests|FullyQualifiedName~NodeToolConfigurationTests|FullyQualifiedName~BrowserWorkspaceTests|FullyQualifiedName~WorkbenchSmokeTests|FullyQualifiedName~VisionDebugWorkflowTests|FullyQualifiedName~BrowserInteractionTests)`，诊断目录改为 `Tests/obj/retired-api-focused-diagnostics`。

%TEMP%日志：`vision-editor-api-residue-before.log`（初始编译阻塞）、`vision-retired-api-focused-first.log`（新增断言判断错误）、`vision-retired-api-focused.log`（最终53通过）、`vision-retired-api-logic.log`、`vision-retired-api-wpf.log`、`vision-retired-api-host.log`、`vision-retired-api-diff-check.log`。下方“不修改/运行测试”或仍保留公开ImageViews的记录均为前阶段范围，不代表本轮。

## 2026-10-26配置映射、订阅与重复状态精简

共用原Codec.CreateProject清单映射，不统一不同导入语义；节点事件和会话事件分组，保留删除时退节点事件→关窗→退会话事件边界。删除两处输入更新后的HasImage重复赋值，关闭通知顺序保留。未删公开ImageViews API，未改测试/断言、文件格式、算法或MachineApp。定向55通过；完整 **679逻辑/536WPF，0失败/0跳过**。WPF首轮外层600秒中断，不计通过；相关进程退出后以1200秒外层预算重跑，11.5164分钟正常结束，单用例90秒挂起检测未变。宿主显式构建增量0警告0错误，不代表重编警告消除；diff-check=0，未暂存/提交。完整命令沿原入口，日志vision-simplification-*在%TEMP%，当前以wpf-retry.log作为全WPF通过证据；Native/物理未验。详见[结构清单](../Vision.Controls/docs/CODE_ORGANIZATION.md)。

## 2026-10-26唯一编辑器及节点真实配置文件操作

删除VisionBrowserView三份源码/资源、注册及空目录，独立消费者直接使用VisionToolEditorView。节点原会话复用Codec/FileStore实现真实打开/保存/另存工具配置、导图和执行；整个.flow仍由流程工具栏保存。节点/Session/卡片/原DTO/连接保持，导入清结果并撤销旧定位基准，动态模式不关闭；运行/只读、迟到对话框、取消/失败保护保留，模型引用不自动加载。节点文件7项、定向81通过；完整首轮旧Browser目录/注册断言已更新为明确退役及规范编辑器保留检查。最终 **679逻辑/536WPF，0失败**，WPF6.5892分钟正常退出，宿主 **4条原nullable警告/0错误**。日志vision-single-editor-logic-final.log、wpf-final.log、host-final.log均在%TEMP%（后三者共用vision-single-editor-前缀）；执行仍沿原产物路径/单进程参数。当前不涉及MachineApp，Native/物理未验；以下Browser薄容器/节点隐藏文件栏是历史，已由本节替代。详见[当前代码清单](../Vision.Controls/docs/CODE_ORGANIZATION.md)。

## 2026-10-26诊断退役、去重与浏览工具栏修复

再删ToolDiagnosticsView、ToolDebugView/Session/Model桥及其资源共7文件，撤导航/诊断Tab/插槽与DebugView访问器；Browser直接借规范编辑器，不复制XAML。先复现空会话时文件按钮被全部折叠的真实红，改默认五项可见、无命令禁用，绑定后沿原工程命令；Browser实际导入图像/保存/另存/重开验证。定向37、完整**679逻辑/531WPF**通过，独立宿主增量0警告0错误（不等于重编零警告），宿主退出/异常/非零数量已复核，暂存空、未提交。MachineApp不涉及。节点窗口显式隐藏单工具工程栏的原设置未改变，不混淆该入口与Browser空会话问题；Native/物理未验。详见[当前代码清单](../Vision.Controls/docs/CODE_ORGANIZATION.md)。

## 2026-10-26继续清理Views（不涉及MachineApp）

已实际删除ToolCatalogView、ProjectToolbarView、ToolCardView及VisionTaskView四类视图/代码/旧资源共11文件，撤旧注册及目录列/分隔条。保留工具工厂、节点定义、ToolCardViewModel/任务会话、完整参数/ROI/结果编辑器。任务页测试改为直接借用现用编辑器，不转移旧列表布局。先取得目录删除新断言红；定向暴露按钮异步等待问题，改await原ExecutionTask，不改生产时序/断言；42项编辑组件、31项任务生命周期/退役检查通过。最终**679逻辑/529WPF**正常退出，宿主**4条既有nullable警告/0错误**，未暂存/提交。本轮不修改/构建/验证MachineApp；旧视图CLR/URI确实退役，不宣称全仓兼容。详细删除位置、保留依据、命令及日志见[当前代码清单](../Vision.Controls/docs/CODE_ORGANIZATION.md)。

## 2026-10-26最终确认：保留独立Vision薄宿主

用户明确不合并FlowNodes.Workbench；已恢复Vision.Workbench工程、两解决方案登记和真实启动测试，壳仅含App/MainWindow/导航VM及模块装配。Controls流程页/编辑器/目录、FlowNode节点字段和Tools算法归位不回退；旧浏览/列表调试导航不恢复。FlowNodes相关合并改动撤回，已跟踪文件无差异，专属混合工具箱/额外通用节点代码清除。真实独立启动断言28算法/29工具箱、仅VisionFlowView导航，**679视觉逻辑/525视觉WPF/原28 FlowNodes通过**，Vision增量0警告0错误，宿主退出与异常/非零数量复核正常，暂存空、未提交。完整命令与位置见[当前代码清单](../Vision.Controls/docs/CODE_ORGANIZATION.md)。下方“统一FlowNodes”的阶段记录为已撤回历史，不是当前启动方式。

## 2026-10-26宿主退役：统一FlowNodes流程调试

独立Vision.Workbench项目、壳/旧ToolBrowser页面及上批兼容转发已删除，根/Vision解决方案除名，测试引用迁FlowNodes.Workbench。Controls流程View/VM正式归自身命名空间；唯一流程页合并原44项与28视觉工具，原脚本编辑器/节点目录不被覆盖。混合脚本→真实OpenCV阈值→完整窗口→原.flow保存重开实际已验；先取得首节点未设入口/脚本结果null的红，再修入口标记。**679逻辑/525视觉WPF/29 FlowNodes**全通过，统一宿主增量0警告0错误，退出/非零数量/异常复核、暂存空、未提交。旧宿主API/URI按用户授权退役；Controls公共浏览/列表组件仍有消费者，不等于全部旧组件清零。Native/物理未验。完整路径、命令、失败证据及生命周期边界见[代码归位清单](../Vision.Controls/docs/CODE_ORGANIZATION.md)。以下工具验收记录为既有历史，不代表重新扩充算法。

## V1.43实际（2026-10-25）：R4-A2 Hough圆心候选选择域与期望点刚体跟随

### 首期范围/契约

- 原FindCircle增加ISearchRegionTool：**原图全图Hough投票完成后，只筛候选圆心是否在域内**，再按本次ExpectedCenter就近选择。不是裁剪、域外填黑、完整圆周/边缘约束、环形卡尺或缩小投票计算量；域外像素仍可能影响Hough候选，不承诺ROI隔绝外部干扰。
- 无域保原全局就近；有域但无候选明确失败、不回退域外圆。原高斯/Hough梯度法、半径/Canny/累加器参数不改，仍Mono8；真实半径19.6等保原结果，不四舍五入成名义20伪精度。
- 只移动ROI会遗漏排序参考点。新增限定ISearchRegionFixtureTool扩展原面积能力，接收已准备不可变域和原位姿值对，在私有参数/像素快照中同步ExpectedCenter=current∘reference⁻¹(point)。原Card内部原位姿值对由线命名扩为搜索准备复用，不改公开IVisionToolSession或建调度器/参数框架/项目。
- 新AppliedExpectedCenter仅为运行追溯，原表追加“执行期望X/Y”，原摘要注明圆心选择域；原ExpectedCenter/ROI/半径不写回。独立准备沿原IBackgroundImageTool/后台/revision/关闭等待；原同步Run仍同步。
- 原UseCircleFixtureBinding默认false，选择原定位来源启用；模式只保State，来源只保Connection。动态缺基准/缺域、原画布断线/删源保true拒绝，false但仍有连接也拒绝；显式关闭且断开才无跟随。原有限/Scale=1/同帧检查保留，找圆也要求同直接来源锚点，异Crop即使像素/hash相同拒绝。
- 新找圆配置不保存运行Fixture/派生期望点，保基准和原ROI；受控旧无模式/定位端口/无ROI.flow仍全图就近。**不是全历史兼容证明**：旧已保存ROI现在具有候选圆心域语义，原会话最多一个选定ROI；不能笼统说旧ROI仍忽略或所有历史文件必可执行。
- 原程序化VisionToolFlowNode若遇新能力，也同步准备期望点再执行并恢复借用SearchRegion，避免只动域、不动参考点。仍原同步节点/取消和所有权机制；找线该旧入口的拒绝边界不改。仍28算法/29工具箱，没有HALCON SDK正向、包/Runtime或模型更改。

### S/C/U/L证据

| 门禁 | 实际证据 |
|---|---|
| S | FindCircleFixtureTests 9：无域全图就近、域内选择另一个圆/空域失败且不裁改像素；两个候选中移动期望点的平移和90度旋转、非有限/溢出/缺域拒绝；原ROI变更/工具及像素改写/Dispose后独立闭包仍选原域目标。原圆心/半径有限守卫及半径实际值测试保留 |
| C | 真实TemplateMatch基准中心(83.5,18.5)，换图分别平移(60,40)/(-50,40)，原定位→Hough圆心域/期望点同步→选择(160,140)/(50,140)附近目标，而不是图中仍存在的(100,100)干扰圆→原Circle连接/Geometry精确2R。独立像素预期中心±2、半径[18,22]/直径[36,44]，不是实图计量精度；保存关闭旧会话、新会话重开再执行 |
| U | 原定位来源ComboBox、原设基准/原运行按钮、原ROI列表/完整结果表/紫色只读派生域。原期望点(100,100)、ROI中心(100,100)、Geometry配方半径99保持；保存不含AppliedExpectedCenter/运行Fixture。无新属性控件，原图/域/测量同图坐标 |
| 旋转/程序化 | 公开编程姿态90度会话及原VisionToolFlowNode入口实际Hough均选新(100,140)，不误选(140,100)干扰圆，原期望点不变、借用域恢复；**不冒称OpenCV TemplateMatch已估旋转**，没有HALCON ShapeMatch调用 |
| 失败/坐标 | 缺基准域、域内无候选、旧FrameId/实际无匹配、异Crop相同像素拒绝，同一Crop才能设基准；运行中改模式拒绝。回到有基准圆的图片再断线/删源，模式保持true、不能用旧圆成功；显式关闭后才无跟随。缺失/失败无Circle/旧直径，非原点/默认圆 |
| L | 不可变SearchRegion内部复制原ROI值，参数JSON/图像私有快照；不借原结果列表/SDK指针。Mat/blurred沿原using释放、结果归原Card；窗口借Session，关窗不Dispose，关闭旧工作区Model.IsClosed、新会话无运行结果/Fixture/派生域 |

### 红绿与最终门禁

- `vision-circle-follow-red.log`：先有接口/结果追溯契约，工具未接时9项实际全红；实现后 `vision-circle-follow-unit.log`9通过，均真实OpenCV Hough或其明确拒绝，不以公共Pose数学抵扣算法。
- `vision-circle-follow-ui-first.log`：新找圆6＋原找线6共**12通过**，覆盖共享Card位姿对变更不回归原线跟随。随后在原编程90度用例补VisionToolFlowNode旧入口实际调用/借用域恢复，`vision-circle-follow-ui-final.log`找圆6通过。
- 最终完整 **678逻辑/521WPF/416流程/66属性**，0失败/过滤后Skipped0；宿主增量0警告0错误。非零预期数量/退出码0、未处理异常/崩溃/零匹配已复核，最终全量之后无生产修改。未放宽旧几何/计时断言、未安装运行库/解除Skip或接设备。
- HALCON格式10/域6及其他RequiresRuntime继续未执行，Skipped0不能当这些Native通过。R4-A2是Hough候选域/跟随，不是整个R4、HALCON双后端测量、环形卡尺精度或原十项终验完成。

### 所有权/运行隔离

| 对象 | 拥有/借用 | 停止/清理 |
|---|---|---|
| 基准期望点/ROI/定位基准 | 原工具参数、原ROI编辑器/节点 | 每次重新推导，原参数/ROI不变；动态模式保原State |
| Prepared域/候选参数/图像 | 原Capture/准备闭包 | 域不可变，参数/像素复制；ClearResult/revision隔离迟到，不保运行位姿到文件 |
| Hough输入/blurred Mat | 单次执行 | 原using在成功/异常归还，输出只暴露Core值 |
| 圆结果/图像与下游CircleValue | 原Card拥有，下游借只读值 | 上游失效/重跑/关闭撤旧值，缺失无默认圆 |
| 完整窗口/程序化借用域 | 原Owned入口借Session；原程序化节点暂借SearchRegion | 关窗不Dispose会话，旧节点finally恢复原域，原所有者等实际工作后关闭 |

### 实际命令

仓库根，SDK10.0.401/.NET9/C#13、xUnit v2/VSTest；以下串行并追加
`-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

| 命令 | 摘要 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **678通过/0失败/0跳过** | vision-circle-follow-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/circle-follow-diagnostics"` | **521通过/0失败/0跳过**，6.1866分钟、正常退出 | vision-circle-follow-wpf-final.log |
| `dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'` | **416通过/0失败/0跳过** | vision-circle-follow-floweditor-final.log |
| `dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid\|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'`（表内竖线转义，实际无反斜线） | **66通过/0失败/0跳过** | vision-circle-follow-propertygrid-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **增量0警告0错误** | vision-circle-follow-host-final.log |

旧依赖nullable/SourceLink/xUnit等警告保留，增量0不是全仓干净，测试总耗时不是工业节拍。最后 `vision-circle-follow-diff-check.log`，暂存区空、未提交。

### 产物/官方对照/仍待

原产物根 `Software Frame/Vision/Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/circle-follow-acceptance/`：
`circle-follow-60.flow`、`circle-follow--50.flow`、`flow-60.png`/`flow--50.png`、
`selected-circle-60.png`/`selected-circle--50.png`、`legacy-global-circle.flow`。
实际查看：模板→找圆→几何原三节点/动态模式；完整结果中原位干扰圆无绿标注，目标约(160.5,140.5)/半径19.6在紫色派生域中，表显示本次期望160/140。不是理想圆半径20精度证明，也不是物理鼠标/OLE/焦点、暗色/DPI/跨屏验收。

1. [OpenCV4.11.0 imgproc.hpp HoughCircles](https://raw.githubusercontent.com/opencv/opencv/4.11.0/modules/imgproc/include/opencv2/imgproc.hpp)，HTTP200，SHA256 `9020D31168E318655B72DCE3A171057FD8E5A05568C25ADFE3B38C6DA49158C1`：minDist为候选圆心间距，param1是梯度法Canny高阈值、param2为圆心累加器阈值；两者不是环形卡尺分数。本批只加后筛圆心域和同步排序期望点，未改变全图投票，不承诺ROI外像素无影响或完整圆周有效。包仍OpenCvSharp4.11.0.20250507。
2. [微软JsonIgnore/属性序列化](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/ignore-properties)，HTTP200，SHA256 `E5C6FC9099C7B52B2226239C10FBF611E3381727DE19D6DA2F6AD7A7F32CD36C`：沿现有简单DTO值复制/JsonIgnore排除运行状态，不把SDK或可变结果集合交给闭包；图像仍独立Copy。通用官方页面内容可能更新，本次快照哈希不同于V1.42，未因此升级本仓.NET9/SDK或改JSON版本。

原文留 `%TEMP%/vision-circle-follow-reference/0.txt`、`1.txt`。HALCON测量/旋转匹配、完整圆周/环形卡尺、一般坐标/mm/非单位尺度镜像、稳定项/多规则、模型资产及物理/实图精度节拍仍待。

---

## V1.42实际（2026-10-25）：R4-A1 OpenCV找线刚体跟随

### 范围/必要契约/兼容边界

- 既有ISearchRegionTool只承载面积SearchRegion，卡尺阵列/线搜索带不能把编辑包围框当算法域。新增**限定**Core ILineSearchFixtureTool：借用原ILineSearchRegion几何＋独立准备方法；IExecutableTool可选成员默认null、OpenCV适配器透传。没有新项目/工具/执行器/绑定表/文件引擎，也没有改IVisionToolSession签名或原公开类型/目录键。
- 只有OpenCV FindLine声明此能力；HALCON仍不因参数实现ILineSearchRegion而自动启用。原兼容VisionToolFlowNode按旧面积能力拒绝定位，未谎称所有旧入口已支持线跟随。原面积工具与P3模式不改，28算法/29工具箱不变。
- 原Pose2D的current∘reference⁻¹变换两个候选端点，单位尺度保留法向/切向宽度与数量、极性方向跟随路径一起转动。新LineSearchGeometry.FollowFixture只返回值，不写原DTO；FindLine准备阶段JSON复制原简单参数及独立像素，再使用原Calipers或EdgeBand算法。运行成功AppliedSearchStart/End追溯本次路径，测得线仍当前图像Pixel坐标，不变回基准图。
- 原Card新增一个本次位姿值对，准备/后台/revision/关闭等待与结果所有权继续原链。原ExecutionSearchRoi只绘制紫色只读**派生编辑外框**，不是面积算法ROI，也不声称逐把运行卡尺已另行渲染；基准黄/青线几何仍原参数编辑，派生框不进RoiEditor.Rois。
- **动态断线必须停止**：新增原NodeProperty UseLineFixtureBinding（默认false），原定位来源选择非空时启用，选择“不使用定位来源”是显式退出。模式只存原State，来源只在Connection；画布断线/删源保true、下一次拒绝执行，不用旧Fixture或退回基准。false但仍有定位线也拒绝，显式关闭且无连接才无跟随。不是任意多槽。
- 原定位同帧/有限/Scale=1守卫保留；找线额外验证同保守直接来源锚点，异Crop即使像素/身份相同也不能设基准。新找线导出只含原参数/ReferenceFixture/模式与连接，运行Fixture=null，派生路径/帧不保存。旧缺模式/定位端口、缺SearchMode文件默认无跟随EdgeBand，不自动绑定。

### S/C/U/L实际证据

| 门禁 | 已执行范围 |
|---|---|
| S | FindLineFixtureTests 9：Calipers/EdgeBand分别真实平移与90度旋转找线，未移动路径在同一目标图确实失败；重复准备不累积，基准非零90度先逆变换；NaN/Infinity/端点不可表示拒绝。像素/原模式/参数改写及工具Dispose后准备闭包仍可实际找线；null当前位姿仍原基准路径 |
| C | 真实OpenCV TemplateMatch基准中心(18.5,18.5)，换图实际中心平移(30,40)，原定位Connection→找线路径(70,90)至(170,90)→原Line连接→Geometry测长约100（独立99–101像素预期）。两模式均原Next执行，保存关闭旧会话、新会话重开再次运行；单独重跑找线撤旧测量，再执行测量，不允许保旧结果求绿 |
| U | 原定位来源ComboBox/SelectedItem、原“设为定位基准”按钮、原流程按钮/匹配索引/原完整窗口，紫色派生框与实际路径相符。基准路径(40,50)至(140,50)不改，Geometry原配方距离5保持。运行锁拒绝来源修改；旧无新State/定位端口/SearchMode文件仍原EdgeBand路径 |
| 旋转边界 | 两模式单工具实际90度找线＋一个公开编程姿态90度会话/派生框实例，均真实OpenCV算法；**不是OpenCV TemplateMatch估旋转**，该matcher仍固定方向/单位尺度。没有调用HALCON ShapeMatch或解除授权门禁 |
| 失败/坐标 | 未设基准、真实定位无匹配/来源失效、旧FrameId、运行中改来源、同像素不同Crop拒绝；改同一Crop可设基准。原画布移除定位线/删源后模式保true并拒绝，即使图像回到可成功的基准边也不能用旧位姿/原路径成功；显式关闭后才无跟随 |
| L | 原Card/Workspace独占工具与结果，准备闭包仅借创建时复制的参数/图像值；原工具Dispose不影响已准备操作。单次Mat及临时拟合对象沿原using归还；窗口借原Session，关窗不Dispose，旧工作区关闭后Model.IsClosed，新会话无结果/Fixture/派生框。原P3迟到/revision生命周期回归保留，不用其fake数量抵扣新增Native精度 |

### 红绿/排障与最终门禁

- `vision-line-follow-red.log`：仅先有Core能力契约时，9项实际全红（工具尚未声明能力）；实现后 `vision-line-follow-unit.log` **9通过**，包含实际图像找线而非仅变换数学。
- 初稿WPF误用不存在的Pose2D.Position，CS1061构建失败、未执行测试；修测试用Pose2D值比较，日志 `vision-line-follow-test-compile-failure.log`保留。随后6项中4通过2失败：测试单独重跑上游找线后仍断言旧测量应存在，违反原失效策略；改为先断言撤下，再实际重跑测量。生产未保旧结果，`vision-line-follow-ui-fixed.log`6通过。
- 首次669/515/416/66全过后做边界复核：删节点仍有无效连接时已正确拒绝，但**原画布完整断开连接会无异常返回成功**。增强同一现有用例为“回基准图可找到线→移除定位Connection→再次执行必须拒绝”，`vision-line-follow-source-loss-red.log`取得实际“No exception was thrown”。不是拿失败图片制造表面安全。
- 原因：连接存在性不能同时代表用户选择的动态模式。补原State/元数据UseLineFixtureBinding，断线/删源保模式而停止、关闭且断开才清当前位姿恢复基准。增强模式重开/旧缺模式文件断言，`vision-line-follow-mode-fixed.log`6再过。探针 `vision-line-follow-source-removal-probe.log`是有无效边时的通过记录，不误记成断线红。
- 模式守卫后完整重跑 **669逻辑/515WPF/416流程/66属性**，0失败、过滤后Skipped0；宿主增量0警告0错误。非零预期数量/退出码0、未处理异常/宿主崩溃/零匹配均复核。未放宽几何/计时断言、未装Runtime/改包/解除Skip/接硬件，最终全量后生产未再改。

### 所有权/文件

| 对象 | 创建/拥有 | 借用/失效/归还 |
|---|---|---|
| 基准线参数/基准位姿 | 原工具/节点State | 原元数据和线编辑器借用；运行从基准重算，不积累平移/旋转 |
| 候选参数/输入快照 | 原准备方法 | JSON只复制原DTO可序列化字段，像素独立Copy；不捕获原工具、原生指针或结果列表 |
| 执行Fixture值对 | 原Card本次准备 | ClearResult清空，原Session执行前重新解析；动态模式保State不保运行Pose |
| 源/拟合Mat与线结果 | 单次执行/原Card | 原using释放原生临时对象，结果由原Card在失效/关闭归还；下游只取不可变LineValue |
| 派生编辑框 | 原会话Model.ExecutionSearchRoi | 只读显示，不放入ROI列表/配方/文件；上游失效、失败、断线或关闭撤下 |
| 完整窗口 | 原Owned入口 | 借同一Session/DTO；关窗不Dispose，节点所有者等真实调用后异步关闭 |

### 实际命令

仓库根，SDK10.0.401/.NET9/C#13、xUnit v2/VSTest。以下串行并追加
`-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

| 命令 | 摘要 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **669通过/0失败/0跳过** | vision-line-follow-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/line-follow-diagnostics"` | **515通过/0失败/0跳过**，最终3.9859分钟、正常退出 | vision-line-follow-wpf-final.log |
| `dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'` | **416通过/0失败/0跳过** | vision-line-follow-floweditor-final.log |
| `dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid\|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'`（表内竖线转义，实际无反斜线） | **66通过/0失败/0跳过** | vision-line-follow-propertygrid-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **增量0警告0错误** | vision-line-follow-host-final.log |

原依赖nullable/SourceLink/xUnit等警告未清理，增量0不是全仓零警告；执行时间不是工业节拍。HALCON格式10/域6及其他RequiresRuntime仍未执行，Skipped0不能抵扣这些Native。最后 `vision-line-follow-diff-check.log`，暂存区未动、未提交。

### 产物/官方对照/剩余边界

原产物根 `Software Frame/Vision/Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/line-follow-acceptance/`：
`line-follow-Calipers.flow`、`line-follow-EdgeBand.flow`、`flow-Calipers.png`/`flow-EdgeBand.png`、
`derived-line-Calipers.png`/`derived-line-EdgeBand.png`、`legacy-unbound-line.flow`。
实际查看：原模板→找线→几何三节点、原定位选择/基准及动态模式；完整工具图像里基准黄/青搜索几何与紫色当前编辑框分开，实际线结果在新边处。截图/STA不是物理鼠标/OLE/系统焦点/主题DPI/跨屏或实图精度证明。

1. [微软System.Text.Json忽略属性](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/ignore-properties)，HTTP200，SHA256 `D950ABA277D3808823448BCD7BE40A425DEFDCDF9C8FC387BEA343D025299295`：JsonIgnore排除可见性派生字段；本批沿本仓.NET9已有JsonIgnore/JSON契约复制简单找线DTO，不借可变原对象。通用文档可能随版本更新，不称.NET10升级或任意图像/SDK对象可这样复制；图像仍单独公共Copy。
2. [OpenCV4.11.0 imgproc.hpp](https://raw.githubusercontent.com/opencv/opencv/4.11.0/modules/imgproc/include/opencv2/imgproc.hpp)，HTTP200，SHA256 `9020D31168E318655B72DCE3A171057FD8E5A05568C25ADFE3B38C6DA49158C1`：fitLine方向/线上点与模板匹配固定方法语义保持；既有Welsch搜索带不是卡尺，不将两模式阈值、采样/精度当等价。本批只变换搜索路径，未重写拟合或宣称新旋转匹配。包仍OpenCvSharp4.11.0.20250507。

原文 `%TEMP%/vision-line-follow-reference/0.txt`、`1.txt`。HALCON找线/ShapeMatch正向、FindCircle搜索域/跟随、有向角/mm/缩放/镜像/一般坐标链、稳定项/多规则、模型资产去重及物理/实图节拍仍待；R4-A1不是整个R4/A4或原十项终验完成。

---

## V1.41实际（2026-10-25）：E1/T04a原Threshold兼容Otsu/反向

### 范围/旧契约

- 落实扩充计划中既有“原Threshold兼容扩展Otsu”，不是A3授权SDK完成。ThresholdMode显式Range=0/Otsu=1/OtsuInverse=2，旧缺Mode参数及.flow仍含端点Range；原ToolIdentity/类型/MinGray/MaxGray/数值端口/文件机制保持，不新增算法条目或控件框架。
- **仅全图Mono8**；不引入ROI、16位自动阈值/彩色隐式转换或HALCON阈值。正常像素src>t为255、否则0，反向src≤t为255。两级0/80、10/200样本返回0/10；常量0/7/255样本自动阈值0有效，不能把0视为失败。未承诺实图分割稳定性或消除光照影响。
- 自动模式不消费原MinGray/MaxGray数值输入，若仍配置连接则沿原准备链拒绝，不悄悄忽略来源；切回Range仍能消费本次Gray8快照且不改配方。自动模式保留未参与运算的原区间配方，即使其界限对Range无效也不参与自动计算；不会自动重置配置。
- 新运行追溯AppliedMode/可空AppliedOtsuThreshold；合法0有值，失败/未执行null。原AppliedMinGray/MaxGray仅在Range表示执行区间，自动模式表/摘要只显示本次自动阈值/Gray8/模式/前景数，不能把保存的99/100当算法阈值。
- 新Mode沿原ToolProperty/白名单与INotifyPropertyChanged驱动Min/Max显隐；UsesRange只读/JsonIgnore/不展示，不是新参数。原IBackgroundImageTool捕获模式/参数/像素私有快照，包括正stride填充行打包；原同步Run接口不变，卡片IAsyncRelayCommand须等待完成。没有新调度器、绑定表或会话接口，原revision/关闭等待继续负责迟到隔离。

### S/C/U/L证据

| 门禁 | 已执行范围 |
|---|---|
| S | OtsuThresholdTests 17：两级图正常/反向及等阈值边界、常量0/7/255合法零阈值、缺图/BGR/Mono16拒绝、未知模式失败、自动模式拒绝两个范围绑定、未使用区间不参与计算、填充图/模式快照与Dispose隔离、旧JSON默认Range含端点/枚举值/不存可见性条件 |
| C | 实际任务图20背景/180矩形，Otsu自动阈值20、正常前景12/反向2548；原图没有255，Blob仅找255仍Count=1证明实际消费掩膜，Count→同掩膜公差→原Switch正常OK/反向有效NG。原Next与两图像来源/数值/布尔连接，保存关闭旧会话、新会话重开再执行。全黑正常0像素/0目标，反向2560像素/1目标，阈值均为合法0 |
| U | 原Mode ComboBox实际选择/SelectedItem、Min/Max原全部行的IsVisible及可见集合，原流程按钮/完整结果/掩膜；原MinGray99/MaxGray100保持，不存AppliedOtsuThreshold/UsesRange。旧无Mode文件仍80..80输出2像素，不自动选Otsu |
| L | 准备闭包仅持私有VisionImage/候选参数，原像素清空/模式和配方修改/工具Dispose后仍返回原反向掩膜。源/输出Mat沿原using释放，结果由原Card拥有；完整窗口借Session关闭不Dispose，关闭旧工作区后Model.IsClosed，新会话无运行掩膜/布尔 |
| 失效 | 错模式/彩色输入清掩膜、Count、旧公差；自动模式显式范围连接拒绝，切回Range后实际统计最小值20覆盖本次执行、配方99保持。公差图像改回原任务灰度图，按原同帧守卫拒绝，不以放宽图像身份求绿 |

### 首次失败与处理

- `vision-otsu-red.log`：先加模式/追溯契约、算法未接时17项实际执行，**13失败/4通过**；实现后 `vision-otsu-unit.log` **17通过**。
- `vision-otsu-ui-first.log`：首轮5项 **2通过/3失败**。两项测试误在仅可见Items查已隐藏行，按现有AllItems读取并继续断言隐藏；另项组合中公差错误使用原任务灰度图，真实错误为“数值来源不是同一帧输入”。`vision-otsu-frame-evidence.log`单项复现已确认阈值/Blob成功而公差拒绝。改测试显式连接同一掩膜后5项通过，并保留切回原任务图必须失败的回归；**没有修改生产同帧守卫**，临时DEBUG诊断已移除。
- 首轮全量逻辑659/660：P4元数据守卫发现新UsesRange缺完整DisplayName及显式隐藏条件登记；补“区间模式”并精确登记，只读/JsonIgnore/IsDisplay=false断言保留，不泛化豁免任意属性。日志 `vision-otsu-logic-guard-failure.log`。
- 首轮WPF508/509：旧卡片测试对IAsyncRelayCommand使用Execute后立即读“成功”，新增后台能力后实际尚“执行中”。将该测试改await原ExecuteAsync，成功/600像素断言原样保留；不把生产命令改同步、加Delay或放宽断言。日志 `vision-otsu-wpf-await-failure.log`。
- 最终完整 **660逻辑/509WPF/416流程/66属性**通过，宿主增量0警告0错误，非零预期数量/退出码0及未处理异常/宿主崩溃/零匹配已复核。最终全量后无生产代码改变；未换包、装Runtime、解除Skip或接设备。

### 所有权

| 对象 | 创建/拥有者 | 借用/停止/释放 |
|---|---|---|
| 原Threshold工具/配方 | 原工厂与Card/Workspace | 窗口/属性行借原DTO；原工作区等待实际执行后关闭 |
| 执行参数/输入快照 | 原PrepareRun闭包 | 复制模式/界限与像素，不借原工具；原Task完成后可回收 |
| 源/输出Mat | 单次执行 | 原using在成功或异常后释放；不把原生指针传到ViewModel |
| 结果/输出VisionImage | 原Card | 下游与窗口借用；失效/重跑/关闭沿原清理，不二次Dispose来源对象 |
| 完整窗口 | 原Owned启动入口 | 借Session；关窗只撤视图，不关闭节点会话 |

### 实际命令

仓库根，SDK10.0.401/.NET9/C#13、xUnit v2/VSTest，以下串行并追加
`-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

| 命令 | 摘要 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **Passed 660/Failed 0/Skipped 0** | vision-otsu-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/otsu-diagnostics"` | **Passed 509/Failed 0/Skipped 0**，最终3.7305分钟、正常退出 | vision-otsu-wpf-final.log |
| `dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'` | **Passed 416/Failed 0/Skipped 0** | vision-otsu-floweditor-final.log |
| `dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid\|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'`（表内竖线转义，实际无反斜线） | **Passed 66/Failed 0/Skipped 0** | vision-otsu-propertygrid-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **增量0警告0错误** | vision-otsu-host-final.log |

旧依赖nullable/SourceLink/xUnit警告仍在，增量0不是全仓零警告；执行时间不是工业节拍。最后diff-check日志 `vision-otsu-diff-check.log`，暂存区空、未提交。660/509不包含被RequiresRuntime过滤的HALCON/AI正向，Skipped0不等于这些Native通过。

### 产物/版本对照与差距

原产物根 `Software Frame/Vision/Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/otsu-acceptance/`：
`otsu-Otsu.flow`、`otsu-OtsuInverse.flow`、`flow-Otsu.png`/`flow-OtsuInverse.png`、`result-Otsu.png`/`result-OtsuInverse.png`、`legacy-range.flow`。实际查看正常掩膜/自动阈值20 Gray8结果表与反向NG完整流程；截图/STA调用不抵扣物理鼠标/OLE/焦点、暗色/DPI/跨屏验收。

1. [OpenCV4.11.0 thresh.cpp官方实现](https://raw.githubusercontent.com/opencv/opencv/4.11.0/modules/imgproc/src/thresh.cpp)，HTTP200，SHA256 `334504BAF244C78BF707C2384FFA8406EDB65FF321955A08B473BB021B6455C1`：最大类间方差与严格大于更新阈值；本次两级/常量图有独立期望，源码有8/16位Otsu分派，但**本工具仍只Mono8**，不因底层分派支持就开放16位。
2. [OpenCV4.11.0 imgproc.hpp官方接口说明](https://raw.githubusercontent.com/opencv/opencv/4.11.0/modules/imgproc/include/opencv2/imgproc.hpp)，HTTP200，SHA256 `9020D31168E318655B72DCE3A171057FD8E5A05568C25ADFE3B38C6DA49158C1`：Otsu与Binary/BinaryInv组合、返回计算阈值及正常/反向边界。头文件仍有“only8-bit”注释，而源码允许16位，明确记录文档差异，不宣称两者完全一致或扩本工具格式。包固定OpenCvSharp4.11.0.20250507，未改IPP/线程等优化设置。
3. [微软WPF属性更改通知](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/how-to-implement-property-change-notification)，HTTP200，SHA256 `8ED8B937C54ECB505D2E7DB168D0AC9F8D249739DA2AD17D01432078737F08B1`：源属性及派生条件必须通知，沿原PropertyItemViewModel/PropertyGroupViewModel显隐，不构造另一套属性控件。

快照在 `%TEMP%/vision-otsu-reference/0.txt`、`1.txt`、`2.txt`。本批不抵扣HALCON阈值/资源、A3域/格式正向或R4测量刚体跟随；模型/资产去重、稳定项/多规则、一般坐标/mm与实图精度/节拍仍待。

---

## V1.40实际（2026-10-25）：A2/R1e2较小夹角→Degree公差→原Switch

### 范围/契约/单位

- 原GeometryOperation末尾追加LineAngle=3，旧Distance/LineIntersection/CircleDiameter=0/1/2保留；NumericUnit末尾追加Degree=5，旧Unknown/Gray8/Gray16/Pixel/Count=0/1/2/3/4保留。当前28算法/29工具箱不变，不加工程、工具、执行器、通用槽/新会话接口或文件引擎。
- 沿V1.39的FirstLine＋ReferenceLine、默认false的原动态参考线模式/原两条Connection/PortValueTransfer；绑定LineAngle必须本次两线有效，仍逐一Pixel/有限非退化/同帧/同目标直接锚点。缺失、失败、断线不使用配方第二线或旧运行值；原单线距离/双线求交和旧文件测试继续通过。
- **无方向支撑线较小夹角0–90度**：反转任一端点方向不改变口径，平行/重合是合法0，与“求交无唯一交点”不同。方向各除最大绝对分量后计算atan2(abs叉积, abs点积)×180/π，避免平方/乘积溢出下溢；零方向、端点差不可表示或非有限失败。原绑定LineValue长度守卫不放宽；手动1e±200只是公共数学尺度测试，不承诺找线可产生这些输入。
- AngleDegrees仅在成功LineAngle及两条实际绑定Pixel/同身份线时沿原INumericResult输出Degree；不输出Distance/Diameter或Intersection。手动夹角的角度维度明确，可显示Degree，但没有帧/来源时不输出可绑定标量；不是默认0。
- 原NumericOutput对Pixel/Degree保留当前直接来源锚点；原公差允许Degree，闭区间/有限值/同帧/同锚点不变，不能把Pixel/Count/Gray当Degree、不隐式转换弧度。Degree本身不在通用数值契约限制0–90，当前范围由LineAngle定义，非整数度数可消费。
- 原Operation/输出字段/公差单位下拉、原数值连接/布尔端口、原Switch规则/分支与.flow全部复用。原表/摘要显示Degree及“较小/无方向”口径；原画布只画两条实际线，不伪交点或角度弧。原四端点配方与运行值隔离，不保存AngleDegrees/AppliedReferenceLine进参数。

### S/C/U/L实际证据

| 门禁 | 已执行范围 |
|---|---|
| S | LineAngleTests 18：0/45/90及非整数26.56505117707799度、反向/钝角折回、手动1e200/1e-200归一化、退化/NaN/差值溢出失败；第二线单位/帧/锚点/缺失/退化拒绝；Degree公差含端点0/45、非整数通过和90有效NG，错Pixel/异锚点拒绝；枚举旧值、快照/释放保留 |
| C | 真实Calipers和旧EdgeBand各水平/竖直FindLine→LineAngle约90度（独立预期[89.9,90]，只是合成图边界）→Degree公差→原Switch true/false规则，两模式各OK及有效NG，共4实例；保存、关闭旧会话、新会话重开再次执行。再将第二来源改同一线得0且[0,0]为OK，只跑匹配分支 |
| U | 原Operation下拉从LineIntersection改为LineAngle、原字段选AngleDegrees、公差单位从Pixel改为Degree、原数值来源/运行按钮；完整结果表/两实际线叠加/单位摘要。FirstStart/End仍(1000,1000)/(1005,1005)，SecondStart/End仍(2000,2000)/(2005,2005)。原ZoomToFit生成完整7节点截图，不改生产布局/渲染 |
| L | 准备闭包仅持不可变LineValue及候选参数，原工具/图像修改和Dispose后仍45度；原完整窗口借原Session，关窗不Dispose，关闭旧工作区Model.IsClosed，新会话无运行线/角度/公差。公差仍复制界限/不可变NumericValue，不持原生对象 |
| 失败/坐标 | 新第5个WPF实例：错Pixel单位无公差，移除第二线不伪0，真实EdgeBand边缘数量不足失败撤角度/旧布尔；全幅Crop与原图像身份完全相同，公差目标改不同直接来源仍明确拒绝。既有求交5实例继续覆盖参考线异帧/同像素异Crop/断线/删源/运行锁/旧手动文件 |

### 红绿/最终门禁

- `vision-line-angle-red.log`：先追加枚举/结果字段，尚无算法和Degree消费时18项实际运行，**10失败/8通过**；实现后 `vision-line-angle-unit.log` **18通过**。本批无新增Native/授权调用，不把已有守卫通过称全红。
- 原VisionLinePairTests文件中新增5个WPF实例，原求交5项不删除；首轮定向5通过。随后使用原ZoomToFit并加强四端点/配置不含运行值断言，完整504通过。最终仅补非整数角度与公差测试数据，完整643再通过；生产代码未在最终全量之后变化。
- 最终 **643逻辑/504WPF/416流程/66属性**，0失败、过滤后Skipped0；宿主增量0警告0错误。四份日志非零数量/退出码0、未处理异常/崩溃/零匹配已复核；Skipped0不是HALCON Native通过。未放宽旧计时/几何断言，未装Runtime/换包/解除Skip/接硬件。
- HALCON格式10/域6及其他RequiresRuntime仍未执行；公共几何位于HALCON命名空间不等于HALCON原生夹角或测量正向。A3 Native/资源、实图精度节拍、物理鼠标/OLE/主题DPI/跨屏、一般多槽/多规则/稳定项、有向角/mm/一般坐标链仍未验收。

### 实际命令

仓库根，SDK10.0.401/.NET9/C#13、xUnit v2/VSTest；以下均串行并追加
`-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

| 命令 | 摘要 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **643通过/0失败**（加强非整数后重跑） | vision-line-angle-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/line-angle-diagnostics"` | **504通过/0失败**，7.4650分钟，宿主正常退出 | vision-line-angle-wpf-final.log |
| `dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'` | **416通过/0失败** | vision-line-angle-floweditor-final.log |
| `dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid\|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'`（表内竖线转义，实际无反斜线） | **66通过/0失败** | vision-line-angle-propertygrid-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **增量0警告0错误** | vision-line-angle-host-final.log |

旧依赖nullable/SourceLink/xUnit警告未清理，增量0不代表全仓干净。最后diff-check日志 `vision-line-angle-diff-check.log`；累计改动/未跟踪文件保留、未暂存/提交。

### 产物/官方对照

原产物根 `Software Frame/Vision/Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/line-pair-acceptance/`：
`angle-Calipers-True/False.flow`、`angle-EdgeBand-True/False.flow`（分别四文件）、
`angle-flow-{Calipers|EdgeBand}-{True|False}.png`、`angle-result-{Calipers|EdgeBand}-{True|False}.png`。
实际查看完整7节点/公差Degree与OK支路、完整夹角结果表/两线叠加/90 Degree摘要；STA调用/截图不是物理操作或实图精度证据。

1. [.NET9 Math.Atan2(Double,Double)](https://learn.microsoft.com/en-us/dotnet/api/system.math.atan2?view=net-9.0)，HTTP200，SHA256 `EB0F60971484138416FCC02DB6BE92097F81E87419507367A28B86FC496FA9F2`：参数顺序y,x，返回弧度。本批以abs叉积为y、abs点积为x，两者非负得到[0,π/2]并显式转度，不混用其他平台同名函数参数顺序或宣称有向角。
2. [OpenCV4.11.0 imgproc.hpp](https://raw.githubusercontent.com/opencv/opencv/4.11.0/modules/imgproc/include/opencv2/imgproc.hpp)，HTTP200，SHA256 `9020D31168E318655B72DCE3A171057FD8E5A05568C25ADFE3B38C6DA49158C1`：fitLine提供归一化方向及线上点，不直接给本项目角度/公差/身份协议；方向反转不应改变无方向夹角。保留当前FindLine卡尺/旧Welsch搜索带模式及参数差异，包仍OpenCvSharp4.11.0.20250507，不承诺不同后端/版本方向和亚像素精度相同。

原文 `%TEMP%/vision-line-angle-reference/0.txt`、`1.txt`；官方资料不抵扣HALCON2311正向/资源、非单位尺度或一般坐标验证。

---

## V1.39实际（2026-10-25）：A2/R1e1显式双支撑线求交→交点继续消费

### 范围/旧契约

- 沿原LineParameterValue附加非位置init ReferenceLine、LineParameterDefinition附加默认false SupportsReferenceLine；保旧三参构造/解构、PrepareLineRun、Session/Adapter接口，不新增工具/项目/执行器/通用参数槽。
- 原Geometry.Distance仍单线长度，不能忽略传入的第二线；绑定LineIntersection要求第二条本次有效线，缺失不使用配方SecondStart/End。未绑定的旧手动LineIntersection保留；旧无模式/新端口受控文件仍能求得(5,5)，不自动绑定、不伪Pixel。不保证旧Reader可执行新语义。
- 原NodeProperty“动态参考线”默认false、参考线来源/原BoundReferenceLine端口，模式只保State、来源只保Connection；ConnectionService循环检查/原PortValueTransfer与Card执行快照不变。true断线/删源仍true并撤运行值，false仍接参考线明确拒绝；不新增第二张NodeId表。
- 两线各自Pixel/有限/非退化/同ImageIdentity/同目标直接来源锚点。**按无限支撑直线求交，不是仅线段范围相交**；合法交点可在端点外，原点0也有效。保原绝对叉积1e-12的平行拒绝边界；不是近似平行病态精度、角度、线段相交分类或mm验收。
- 旧手动极大有限端点可使叉积/t/交点非有限仍Success，本批先取得真实失败，再加中间量和最终交点有限守卫。平行/重合/非有限失败、Intersection=null，不发布假原点。
- GeometryResult追加IPointResult显式Intersection；只在成功双线实际绑定/Pixel与身份明确时输出点，手动结果保持未知单位。原字段下拉/PointOutput端口继续消费，不加输出框架。原表/摘要显示Pixel或未声明，原叠加绘制两实际线段及交点；原配方四端点不写回。

### S/C/U/L证据

| 门禁 | 已执行范围 |
|---|---|
| S | 新LinePairTests 12：两支撑线原点/(10,20)/(100,-30)，含端点外交点；平行/重合无点，参考线单位/帧/锚点/NaN/退化拒绝，缺第二线/错误操作/不声明能力拒绝，旧手动(5,5)、有限计算溢出失败；原FindLine单工具真实卡尺/旧搜索带回归保留 |
| C | 两个真实OpenCV FindLine，以Calipers及旧EdgeBand分别提取矩形水平/竖直边，经原Next A→B→求交→距离及两线/点数据连接，约(99.5,59.5)/(99,59)继续测距约50；独立边界预期x∈[98,101]/y∈[58,61]，距离∈[48,52]，不强迫Canny整数边和卡尺亚像素一致。保存、关闭旧会话、重新打开再执行 |
| U | 原模式行/参考来源ComboBox SelectedValue/SelectedItem及可见“竖直支撑线”、原Intersection字段下拉、原流程按钮/完整结果表与叠加。原FirstStart(1000,1000)/SecondStart(2000,2000)保持；运行中改模式/来源拒绝。新模式短标签“动态参考线”避免与旧参考点模式窄列同名截断 |
| L | 已准备闭包仅持LineValue/ReferenceLine及候选参数，工具Dispose/原参数和图像修改后仍得(10,20)，不借图像/结果集合/SDK对象。完整窗口借原Session，关窗不Dispose；关闭原会话Model.IsClosed，新会话无运行输入/点/结果；沿原连接/上游失效清理 |
| 失败/坐标 | 同线作两来源导致平行/重合失败、下游无结果；原画布移除参考连接/删源清值；模式与接线不一致拒绝；真实EdgeBand边缘数量不足失败不残留点；参考线单独换FrameId拒绝。两真实Crop分别裁相同像素块，公共身份相同仍因直接来源不同拒绝，改接同一Crop后才成功 |

### 红绿/最终门禁

- **vision-line-pair-red.log**：附加值契约已加、尚未消费第二线时12项实际执行，7失败5通过；未错误声称12项全红。实现验证/快照/输出及有限守卫后 **vision-line-pair-unit.log**12通过。
- 初稿WPF因HALCON/OpenCV同名FindLineParameters产生CS0104构建失败，未运行测试；增加明确OpenCV别名后5项通过。失败日志 **vision-line-pair-test-compile-failure.log**保留，不当作行为红绿。原线/双点定向12通过。
- 新VisionLinePairTests **5实例**（两找线模式各一正向、失效链、异帧/异Crop、旧手动文件）；不是HALCON与OpenCV双后端，**两模式均真实OpenCV，几何为无SDK公共数学**。
- 首次完整625/499/416/66均过；随后仅缩短新属性标签并加强第二线异Crop断言，定向5再过，完整499重跑再过，最终宿主增量0警告0错误。原计时/几何断言未放宽，未改包/SDK/Runtime/Skip/授权或接硬件。
- 过滤后的Skipped0不代表Native通过。HALCON正向格式10/域6及其他RequiresRuntime仍未执行；28算法/29工具箱不变，角度/通用多来源/多槽/稳定ID/mm、A3 Native、物理UI与实图精度节拍不销项。

### 实际命令/日志

SDK10.0.401/.NET9/C#13、xUnit v2/VSTest。仓库根串行，以下每条追加
`-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

| 命令 | 摘要 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **625通过/0失败** | vision-line-pair-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/line-pair-diagnostics"` | **499通过/0失败**，最终6.6048分钟、宿主正常退出 | vision-line-pair-wpf-final.log |
| `dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'` | **416通过/0失败** | vision-line-pair-floweditor-final.log |
| `dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid\|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'`（表内竖线转义，实际无反斜线） | **66通过/0失败** | vision-line-pair-propertygrid-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **增量0警告0错误** | vision-line-pair-host-final.log |

均检查退出码0、非零预期数量及未处理异常/宿主崩溃/零匹配；最终复核625/499/416/66。旧依赖nullable/SourceLink/xUnit等警告未清理，增量0不是全仓零警告。最终diff-check日志 **vision-line-pair-diff-check.log**，暂存区不动、未提交。

### 产物与两官方对照

原产物根 `Software Frame/Vision/Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/line-pair-acceptance/`：
`line-pair-Calipers.flow`、`line-pair-EdgeBand.flow`及`pair-Calipers.png`/`pair-EdgeBand.png`、`intersection-Calipers.png`/`intersection-EdgeBand.png`、`legacy-manual-intersection.flow`。实际查看完整结果截图：两线段/端点外交叉标记、Pixel表和摘要明确；STA截图/原按钮与下拉调用不是物理鼠标/OLE/焦点、暗色/DPI/跨屏验收。

1. [OpenCV 4.11.0官方imgproc.hpp](https://raw.githubusercontent.com/opencv/opencv/4.11.0/modules/imgproc/include/opencv2/imgproc.hpp)，HTTP200，SHA256 `9020D31168E318655B72DCE3A171057FD8E5A05568C25ADFE3B38C6DA49158C1`：fitLine输出归一化方向(vx,vy)和线上一点(x0,y0)，不是线段相交判定器；现有Welsch拟合/有限采样跨度保留，本批在原Geometry明确无限支撑线求交。Calipers为原采样模式，不把Welsch文档当其精度证明。固定4.11.0对应本仓OpenCvSharp4.11.0.20250507，不承诺跨版本端点相同。
2. [.NET9 Double.IsFinite](https://learn.microsoft.com/en-us/dotnet/api/system.double.isfinite?view=net-9.0)，HTTP200，SHA256 `96FF3E5BEE250D5E485CE47234219A894BD2A0142DE403BF0FAABC391EC8517C`：排除NaN/无穷；有限输入不保证减法/叉积/除法/交点有限，本批在原求交路径检查中间量及最终点，不以改大容差或假原点代替失败。

原文留 `%TEMP%/vision-line-pair-reference/0.txt`、`1.txt`；另核对微软record附加init/with样例，未改位置构造/解构。上述对照不等于HALCON2311找线/一般单位坐标/实图节拍验收。

---

## V1.38实际（2026-10-25）：A2/R1d2显式第二动态点

### 范围/旧契约

- 仅扩展原Geometry.Distance的点快照：PointParameterValue增加非位置`ReferencePoint`，null保原配方参考点；PointParameterDefinition增加默认false的`SupportsReferencePoint`，当前只有Geometry明确启用。原三参构造/解构与PreparePointRun签名、原Session/Adapter接口保留，不新增执行器/工具/项目/通用槽或绑定表。
- 原节点元数据增加默认false的UseReferencePointBinding及参考点来源，支持者构造原自定义输入端口BoundReferencePoint；运行值JsonIgnore、不进配置。模式保原State，来源只保原Connection，沿ConnectionService循环检查/规划、PortValueTransfer与原Card准备一个点参数包。
- **启用必须有本次第二点**；参考点0/2候选、失败/断线/删源无值，不能退原点、上轮参考或配方。断线不自动改true模式；关闭模式但仍有参考线也拒绝，必须显式断开才回到配方。主点仍需显式选择FirstStart，不允许只有第二点。
- 两点分别验证Pixel、有限、同ImageIdentity及同目标直接来源锚点；不同Crop即使公共图像身份/hash/像素一致也拒绝，不能推断跨Crop坐标等价。原点/同点/0距离合法，两个有限端点算距溢出仍失败；原数值/线/圆/点混合覆盖拒绝保持，**不是一般多槽/双线/求交/mm/稳定ID**。
- Geometry原参数FirstStart/FirstEnd不写回，执行结果AppliedPoint/AppliedReferencePoint和实际AppliedStart/End可追溯；原数值输出Distance仍有Pixel/帧/坐标，不另造输出合约。新模式需认识新State/端口的Reader，不承诺旧软件执行双点；受控旧缺模式/端口单点文件仍用配方。

### S/C/U/L实际证据

| 门禁 | 已执行范围 |
|---|---|
| S | 公共数学两点(0,0)→(0,0)/(3,4)/(30,40)分别0/5/50；参考点单位/异帧/异锚点/缺身份/NaN/Infinity拒绝；不声明能力的消费者拒绝附加参考点；有限极大端点距离溢出不发布Infinity |
| C | 两个真实OpenCV Blob以现有面积筛选得到唯一中心(12.5,22.5)/(42.5,62.5)，原Next A→B→Geometry＋两个点数据连接，得50；同一源作两端得0。0/2参考候选、失败、断线、删源撤输入/结果而不回退；原.flow保存关旧会话重开保模式/连接并重新得50/0 |
| U | 原属性模式行写true、原参考来源ComboBox的SelectedValue/SelectedItem及可见TextBlock、原运行按钮/完整结果表/叠加；原配方(1000,1000)/(-1000,-1000)均保持。运行中改模式/来源拒绝；受控旧无模式/新端口单点图默认false并得5，不自动连第二点 |
| L | 两PointValue与候选Geometry参数为独立准备快照，原工具Dispose及图像修改后仍得5；null参考仍保旧配方6/8→10。原完整工具窗口借Session，关窗不Dispose；关旧会话Session关闭，新会话无BoundPoint/BoundReferencePoint/Result。连接变化/上游失效沿原退订与清结果机制 |
| 坐标 | 实际参考源单独换FrameId后拒绝；两个真实Crop输出像素/身份完全相同但来源锚点不同仍拒绝，把参考源改接同一Crop后才得50 |

### 红绿/数量与边界

- **vision-point-pair-red.log**：先加兼容的附加值契约、尚未消费/验证第二点时，11项实际全红（沿用配方而不是第二点/非法参考点未拒绝）。实现后**vision-point-pair-unit.log**11通过；随后增补有限端点距离溢出实例，最终新增12逻辑。
- **VisionPointPairTests 5实例**：两个实际50/0链与重开、缺失/歧义/参数失败/断线/删源/锁/模式与接线不一致、参考点异帧/同像素异Crop、受控旧单点结构兼容。首轮5项通过；完整494后仅改测试节点两排摆放、增强删源断言，最后定向5再通过。没有生产代码在全量后变化。
- 原VisionBlobPointTests 5项在新端口/默认false实施后定向通过，随后完整494保留原单点/Count/圆/线/布尔链门禁。未放宽既有计时/几何断言、未新增Runtime依赖/解除Skip/装模型/接设备。
- 本批是**真实OpenCV点＋无SDK公共几何**，不把HALCON命名空间里的Geometry公共数学称HALCON原生正向。A3域6例/格式10例及其他RequiresRuntime继续未执行，Native/物理/实图节拍不销项。

### 所有权

| 对象 | 拥有/借用 | 失效/关闭 |
|---|---|---|
| 两生产节点工具/Blob结果/列表 | 原各自Card/Workspace拥有，消费者只取不可变点 | 原运行等待/清结果/Dispose，不借列表或图像给测量闭包 |
| PointParameterValue.ReferencePoint | 本次准备闭包，非位置init值 | null仅代表原API配方模式；图模式true必须先解析到非空值，断线不构建假输入 |
| Geometry参数/结果 | 原配方归原工具，闭包复制候选；结果归原Card | 只覆盖本次FirstStart/FirstEnd，旧配方/单位不写回；AppliedReferencePoint只为运行追溯 |
| 模式/来源 | 原State bool＋原Connection | 不保存第二份NodeId/绑定表；断线/删源保模式true且立即清BoundReferencePoint/结果 |
| 完整窗口 | 借原节点Session/DTO | 关闭不Dispose；移除流程等实际调用停止后原Workspace归还 |

### 实际命令

仓库根，SDK10.0.401/.NET9/C#13、xUnit v2/VSTest；每条追加
`-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`，串行，原artifacts/no-restore路径不变。

| 实际命令 | 摘要 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **Passed: 613, Failed: 0, Skipped: 0** | vision-point-pair-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/point-pair-diagnostics"` | **Passed: 494, Failed: 0, Skipped: 0** | vision-point-pair-wpf-final.log |
| `dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'` | **Passed: 416, Failed: 0, Skipped: 0** | vision-point-pair-floweditor-final.log |
| `dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid\|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'`（表内竖线转义，实际无反斜线） | **Passed: 66, Failed: 0, Skipped: 0** | vision-point-pair-propertygrid-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **显式增量0警告0错误** | vision-point-pair-host-final.log |

最终定向同WPF入口改`--filter FullyQualifiedName~VisionPointPairTests`（无blame参数）：**Passed: 5, Failed: 0, Skipped: 0**，vision-point-pair-ui-final.log。退出码/非零预期数量与Unhandled exception/Test host process crashed/宿主崩溃/No test matches核查；Skipped 0因过滤Native，不表示其通过。旧依赖nullable/xUnit警告不以增量0掩盖；最终diff --check，暂存区不动，累计修改保留/未提交。

### 产物与未验

`Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/point-pair-acceptance/`：point-pair-True/False.flow、pair-True/False.png、pair-result-True/False.png、legacy-single-point.flow。实看原三节点/四条线、绑定动态参考点勾选/参考点B可见文字、完整结果50 Pixel及实际两点/连接线；同点0保持有效。最后测试两排布局避免A→Geometry长线穿过B的端口造成视觉误认，不改生产连线渲染。

合成图解析距离不是实物精度/节拍证明；原属性/程序化接线/STA不抵扣物理鼠标/OLE、暗色/DPI/跨屏。不是通用多槽、双线求交/角度、稳定目标身份、多规则/mm/一般坐标或A3 Native完成。

### 两项官方对照（2026-10-25）

Microsoft Learn先搜索/官方代码样例/全文读取，HTTP200快照`%TEMP%/vision-point-pair-reference/`。C#只用现有C#13支持的record/init/非位置属性，不采用文档里的新版本语法。

| 来源 | 对照与差距 | SHA256 |
|---|---|---|
| [C# record](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record) | record仅浅不可变；附加非位置ReferencePoint保原构造/解构，引用本身为不可变PointValue/Point2D，不能借可变列表/图像。准备后原工具/图像修改与Dispose实际仍得到原5，原参数不写回 | FB228676A61B94446AEE6B077C6845052E94C869D881B07B3AFEBE35D4506ACF |
| [.NET9 Double.IsFinite](https://learn.microsoft.com/en-us/dotnet/api/system.double.isfinite?view=net-9.0) | 两端点分别有限性/身份/坐标校验；有限输入也可能算距溢出，因此沿原结果有限守卫，极大双点不发布Infinity。未声称任意极值稳健算法/世界单位或Native测量精度 | 96FF3E5BEE250D5E485CE47234219A894BD2A0142DE403BF0FAABC391EC8517C |

最新V1.38：28算法/29工具箱、613/494/416/66；以下V1.37及更早保持历史。

## V1.37实际（2026-10-25）：A2/R1d1唯一Blob中心→原点测量

### 选择范围与不可替代边界

A3域/格式Native仍受授权限制，未转成“完成”。本批推进计划允许并行的A2结果消费：两后端既有BlobResult沿IPointResult增加显式`SingleCenter`，不增加工具、工程、参数框架、运行/文件引擎或选择器。

- 只有本次Success/RoiImage有效、最终Items恰好1个、Image坐标系且中心有限才输出Pixel点；搜索域、MinArea和用户特征筛选先于唯一性判断。
- **不是跨帧稳定目标ID**，也不是选中行/Index=1、第一项或最大面积。唯一候选的Index改成99仍有效；零候选或多个候选无点，不能冒充原点。原点作为实际一个候选的中心则合法。
- 两BlobResult增加CoordinateSpace，默认Image，ToWorld/ToMachine明确标Calibration/Machine；不改原几何映射数学。重新附加RoiImage也不能让已映射结果输出Pixel。HALCON原中心3位舍入保持，不以公共消费承诺后端精度等价。
- 两原目录只补PointResultFields，原节点字段/参数/来源下拉、原Connection/PortValueTransfer/Card、原Geometry.Distance执行与.flow复用；输出字段仍默认空，不自动选点。28算法/29工具箱不变，旧参数、目录键、URI/导航/公开方法不改。
- 原点契约继续同输入身份/保守直接来源锚点；仅本次FirstStart快照覆盖，FirstEnd固定配方参考点，不随定位自动变换。不是第二动态点源、跨Crop/mm/一般坐标链或混合多槽开放。

### S/C/U/L与实际红绿

| 范围 | 本批实际证据 |
|---|---|
| S | 真实OpenCV 6×6矩形，像素中心范围x=10..15/y=20..25，质心严格(12.5,22.5)；加25像素第二目标后按MinArea30保留唯一36像素目标；零/两个目标不择优；实际单像素原点、失败/未执行/释放/非有限中心拒绝 |
| C | 原Blob→Geometry点连接/Next控制，唯一中心到同点为0，到(15.5,26.5)为5；0或2候选虽Blob执行有效，测量无本次点而失败，不能沿用旧中心或配方起点；真实128..100反序阈值失败、原画布断线撤输入/结果 |
| U | 原PointOutputField/PointInputParameter/PointSourceNodeId下拉、原运行按钮/完整结果窗口；选中Item和来源实际TextBlock包含“唯一目标中心”已断言，不只看ID；配方起点(1000,1000)不改；原.flow保存、关旧会话、新会话重开运行；无点State/端口受控旧Blob恢复空字段不自动绑定 |
| L | 数值点与执行参数独立快照，源Items清空、原输入像素修改、原Blob/结果与Geometry释放后已准备闭包仍得5；原完整窗口借原Session，关窗不Dispose，关旧流程Session关闭，新会话无旧PointOutput/BoundPoint/Result |
| Native | HALCON仅托管结果唯一性与world/machine标签合约（2实例）；既有RequiresRuntime补唯一中心(6.5,6.5)与过滤后无点断言，仅编译未执行；不抵扣HALCON算法/资源S/C/U/L |

- **vision-single-center-red.log**：10逻辑实例先10/10实际失败（两BlobResult没有IPointResult）；补显式字段/有限性/唯一性/坐标标签后**vision-single-center-unit.log**十项通过。
- **vision-single-center-catalog-red.log**：原目录尚未声明SingleCenter时1项实际失败；沿原注册补白名单后通过。
- **BlobPointTests 10实例**含真实像素/筛选、缺失/歧义、原点/快照/释放、失败/非有限与两后端坐标映射合约。**VisionBlobPointTests 5实例**含2个实际0/5链与重开、目录/默认字段、失效/锁、旧文件。
- 完整601/489通过后仅加强选中Item/可见来源文字断言及投放属性滚动，定向5再过；生产代码未在全量后变化。没有去掉Skip、放宽断言或安装SDK/模型/设备。

### 所有权

| 对象 | 所有者/借用 | 本批结束方式 |
|---|---|---|
| Blob工具/结果/Items/RoiImage | 原Card/Workspace，原显示借用 | 仍原Run/清结果/Dispose，不新增长期资源；RoiImage=null或失败后不输出点 |
| PointValue与Geometry本次参数 | 准备闭包 | 只复制中心值，既不借用Item/List也不写原配方；原对象释放后已准备执行有效 |
| 原完整工具窗口 | 借原节点Session | 关窗不释放；原流程/Workspace拥有关闭与等待责任 |
| 点来源与控制连接 | 原Chart/Connection/执行器 | 缺失/断线/来源失败撤值，不回退原点/默认起点；原运行锁保留 |

### 完整门禁与命令

仓库根，SDK10.0.401/.NET9/C#13、xUnit v2/VSTest。每条均追加
`-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`，串行，原artifacts/no-restore不变。

| 实际命令 | 摘要 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **Passed: 601, Failed: 0, Skipped: 0** | vision-single-center-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/single-center-diagnostics"` | **Passed: 489, Failed: 0, Skipped: 0** | vision-single-center-wpf-final.log |
| `dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'` | **Passed: 416, Failed: 0, Skipped: 0** | vision-single-center-floweditor-final.log |
| `dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid\|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'`（表内竖线转义，实际无反斜线） | **Passed: 66, Failed: 0, Skipped: 0** | vision-single-center-propertygrid-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **显式增量0警告0错误** | vision-single-center-host-final.log |

最后定向：同WPF测试命令改`--filter FullyQualifiedName~VisionBlobPointTests`（不加blame参数），**Passed: 5, Failed: 0, Skipped: 0**，vision-single-center-ui-final.log。退出码、非零预期数量与Unhandled exception/Test host process crashed/宿主崩溃/No test matches均检查；RequiresRuntime过滤排除，Skipped 0不是Native已过。旧依赖nullable/xUnit警告不以增量0掩盖，未改Skip/包/原生授权；最终diff --check、暂存区空，累计修改保留/未提交。

### 产物与未验

`Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/single-center-acceptance/`：single-blob-True/False.flow、对应原页图、single-distance-True/False.png、legacy-blob-without-single-center.flow。

实看原2节点/2条线（Next与点数据分开）、原来源行与完整结果0/Pixel/实际起点(12.5,22.5)及中心十字。位图放大显示与合成图解析预期不代表实物定位精度、实图节拍或物理鼠标/OLE/主题/DPI/跨屏验收。HALCON仅托管契约，A3正向域/格式/资源仍待；真正稳定项身份、多来源/多规则/mm/一般坐标链仍待。

### 三项官方对照（2026-10-25）

HTTP200全文`%TEMP%/vision-single-center-reference/`；Microsoft Learn先搜索，再按.NET9全文读取及官方示例对照。

| 来源 | 对照与差距 | SHA256 |
|---|---|---|
| [OpenCV4.11 imgproc.hpp](https://raw.githubusercontent.com/opencv/opencv/4.11.0/modules/imgproc/include/opencv2/imgproc.hpp) | 连通域centroids按列X/行Y；原工具已跳过背景并完成筛选。本次直接复用最终Item.Center，不从框中心/显示行索引重新推算，不改变算法/连通/孔洞口径。真实矩形与单像素原点已执行 | 9020D31168E318655B72DCE3A171057FD8E5A05568C25ADFE3B38C6DA49158C1 |
| [HALCON2311 area_center](https://www.mvtec.com/doc/halcon/2311/en/area_center.html) | Row/Column语义由原工具转换为X=Column/Y=Row；复用最终中心并保原3位舍入。空候选不因缺省中心为0而输出原点；Native唯一点/资源仍未执行，不以托管合约等价OpenCV | 493F2E6CF1DC524DEE491B5B05721D26E51AEC488F5706F40B86EFBBF9D4EBB1 |
| [.NET9 Enumerable.Single](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.single?view=net-9.0) | 恰好一个不同于First；生产采用显式Count==1并在0/多项时返回缺失，不用异常驱动界面或自动择优。这是唯一候选策略，不是跟踪/稳定ID协议 | C86A0564B929463B15DF1CA486BC3C7378FDE647F738A88ABEFBCDBEE872E39E |

最新V1.37：28算法/29工具箱、601/489/416/66；以下V1.36及更早保持历史。

## V1.36实际（2026-10-25）：A3/R2b域安全代码与显示侧拒绝诊断

### 范围与兼容

- 当前ToVisionImage灰度/RGB路径在Marshal.Copy之前统一GetDomain/GenRectangle1/TestEqualRegion，精确比较完整矩形域；不是只比面积，代码拒绝受限/空/带孔或等面积但偏移图外的域。**Native尚未执行**，这是边界代码接通，不宣称域行为已实测修复。
- ImageFile移除原仅按面积的重复检查，委托同一出口；原NotSupportedException回退不变：原生读取Success/尺寸/LoadedImage保留，公共OutputImage=null/诊断，消费者原链拒绝空输出。无隐式FullDomain、填黑、裁剪或浮点降位深，也未把domain塞进公共像素格式。
- 仓内当前文本检索的生产消费者为Blob二值快照、ImageFile及三个显示页；Blob原RegionToBin完整图路径不改。外部调用方不穷尽保证；公共函数签名保留，但非完整域由原先可能复制矩形存储改成明确拒绝，是安全行为改变，不称纯重构或完全无行为变化。
- 三个原显示页只捕获预期NotSupportedException，沿既有VisionImageViewport空图错误文字绘制；其余异常仍由调用方处理。新增一个必要UI入口ReportImageError（没有新DP/错误框架/专属界面）：清旧Image/Result、保原绑定、不释放借用对象；重载不丢诊断，null清错误，合法新图恢复。DP.Clear仍不破坏原DisplayImage绑定。
- 原URI/CLR类型/目录/参数/State/.flow不变，28算法/29工具箱；算法紧凑Mono8限制、V1.35 BGRA/stride口径和旧单值/数量/分支链不改。

### 所有权与停止语义

| 对象 | 拥有/借用 | 本批行为 |
|---|---|---|
| 输入HObject | 原调用方拥有，转换同步借用 | 不Dispose/改domain；拒绝前不Marshal.Copy像素；调用方保持其同步有效性 |
| domain/full/equal | 转换器本次创建 | 嵌套using释放区域/比较HTuple；原生分配失败与释放实际验证仍待授权 |
| ImageFile.LoadedImage | 原工具拥有、旧API借用 | 仍有效到下次运行/Dispose；不支持公共输出仍保原生Success/尺寸/句柄 |
| 原显示快照/结果 | 原拥有者拥有，画布借用 | Report清引用不Dispose，SetCurrentValue保Image/Result绑定，不反写绑定源 |
| 原画布错误文字 | 画布本次诊断 | 无新会话/计时器；重载保留，清屏/新图撤销；实际依赖UI线程VerifyAccess |

### 红绿与未执行部分

- **vision-domain-ui-red.log**：原画布没有可用的错误报告入口，新增公开能力断言实际先红；接通后清输入/结果、借用对象、状态、重载、新图恢复通过。
- **vision-domain-binding-red.log**：加入原Image/Result绑定精确断言，直接设置本地null会破坏绑定，实际红；改SetCurrentValue后绿，再重跑完整484与共享显示1项。没有去掉绑定或放宽断言求绿。
- ImageConversionFailureTests为**1新增WPF实例**，还比较实际渲染错误与默认占位的像素不同，避免只有Status正确但画面仍是默认提示。捕获绘制必须UpdateLayout/ApplicationIdle；强断言初次未等待失效重绘时失败，保留断言补正确渲染时序，不用固定延时/改生产逻辑求绿。
- 原DisplayImageMigrationTests的1项（三显示页）增补Report错误→DP.Clear清诊断与保原DisplayImage绑定，离线STA通过；**未触发真实HALCON受限域输入**。
- **HalconDomainSnapshotRuntimeTests新增6例**：Mono8/Mono16/RGB受限域、孔洞、空域、等面积偏移域，拒绝后借用原图仍可读；全部RequiresRuntime/Skip，只有编译，**尚无执行红绿**。现存10个格式往返及文件域/浮点原生例也未执行，不以WPF错误报告测试抵扣Native域/资源S/C/U/L。
- 新Native测试初稿误用HObject.GetDomain（无该成员）造成编译失败，见vision-domain-native-test-authoring-failure.log；删除不需要的调用后当前逻辑全量编译/运行通过。不是SDK行为缺陷，更没有解除Skip验证Native。

### 实际门禁

仓库根，SDK10.0.401/.NET9/C#13、xUnit v2/VSTest。以下每条均追加
`-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`，串行；沿原artifacts/no-restore，不重新安装Runtime/包或接硬件。

| 实际命令 | 摘要 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **Passed: 591, Failed: 0, Skipped: 0** | vision-domain-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/domain-diagnostics"` | **Passed: 484, Failed: 0, Skipped: 0** | vision-domain-wpf-final.log |
| `dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'` | **Passed: 416, Failed: 0, Skipped: 0** | vision-domain-floweditor-final.log |
| `dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid\|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'`（表内竖线转义，实际无反斜线） | **Passed: 66, Failed: 0, Skipped: 0** | vision-domain-propertygrid-final.log |
| `dotnet test "Software Frame/WPFControls/Tests/WPFControls.Tests/WPFControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFControls/Tests/obj/display-retirement" --filter FullyQualifiedName~DisplayImageMigrationTests --logger 'console;verbosity=normal'` | **Passed: 1, Failed: 0, Skipped: 0** | vision-domain-display-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **显式增量0警告0错误** | vision-domain-host-final.log |

最终预期非零测试数量/退出码/Unhandled exception/Test host process crashed/宿主崩溃/No test matches均核查；RequiresRuntime过滤排除，Skipped 0不是Native通过。旧nullable/SourceLink/xUnit等警告不被增量0掩盖；累计修改保留，未暂存/提交，不改Skip/包版本/授权/设备，diff --check通过。

### 留图与界限

`Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/domain-acceptance/conversion-error.png`：实际查看原画布诊断文字；由离线直接调用错误入口生成，**不是一次真实HALCON拒绝的截图**。没有旧白图/旧结果残影，原图像缓冲与原结果对象仍可由拥有者使用。渲染比较保证不是仅后台状态正确。

该图/STA测试不抵扣实际Native回调/三页面受限域输入、物理鼠标/OLE、主题/DPI/跨屏、实图/节拍。A3仍待Native域/资源正向验证；A2稳定项/多来源/多规则及后续阶段仍未完成。

### 官方对照（2026-10-25）

HTTP200全文快照`%TEMP%/vision-domain-reference/`；HALCON固定2311，WPF按.NET9。WPF先Learn搜索后全文读取与官方代码样例对照。

| 来源 | 对照与差距 | SHA256 |
|---|---|---|
| [HALCON2311 get_image_pointer1](https://www.mvtec.com/doc/halcon/2311/en/get_image_pointer1.html) | reduced与原图可以共享同一矩阵/指针，单纯Marshal.Copy不携带domain。因此统一精确完整域保护，不用矩形存储冒充已清零掩膜；Native拒绝/回读/资源未执行 | 8CC277F3E71ED733828B931F25709C47AF270E0CF7D7B70157504D802ECF25E9 |
| [HALCON2311 test_equal_region](https://www.mvtec.com/doc/halcon/2311/en/test_equal_region.html) | 比较实际区域相等，覆盖仅面积检查不能排除的等面积偏移域；临时区域/HTuple归转换器。仅代码接通/编译，六个Native例仍待授权 | AC6F6547E462067B589CFE03BBE3B56BD50EFB88AA07B2EEC4D5E4FCD4BB0D45 |
| [.NET9 SetCurrentValue](https://learn.microsoft.com/en-us/dotnet/api/system.windows.dependencyobject.setcurrentvalue?view=windowsdesktop-9.0) | 改当前值但保值来源/绑定；实际先红后绿验证Image/Result绑定与借用对象不受破坏 | 1396B0AFDD0A4A5ADBE8510898A94F091883A15C123A2D582246081980F63FFE |
| [.NET9 InvalidateVisual](https://learn.microsoft.com/en-us/dotnet/api/system.windows.uielement.invalidatevisual?view=windowsdesktop-9.0) | 失效会安排布局/重绘，不是立即同步像素更新；测试等待布局/Idle后比较错误/默认提示，未降低断言或用截图冒充Native | 5937A18CDE935E4C52587A740E774EA3A2BDD635CA3D0EB43D12B9F9164809FD |

最新V1.36：28算法/29工具箱，591/484/416/66＋1共享显示；以下V1.35及更早保持历史。

## V1.35实际（2026-10-25）：A3/R2a显示格式桥代码接通，Native未验

### 本批边界

- **不是A3完成**：只推进既有HalconImageConverter.FromDisplaySnapshot的显示输入桥：Mono8/Mono16/RGB/BGR/BGRA、正stride填充行。继续原公共VisionImageSnapshot.Copy做尺寸/stride/缓冲/checked校验和逐行紧凑复制，不新增公开API、类型、工程、包、格式框架、UI或运行配置。
- BGRA传原HALCON2311 `bgrx`，**丢弃Alpha**，不做背景合成/预乘还原；原生结果为三通道，公共回读RGB不含Alpha。Mono16仍uint2，Float32/未知格式与负stride明确拒绝，不自动降位深/归一化/翻转。
- **算法契约保留**：FromVisionImage仍仅紧凑Mono8，把旧stride拒绝显式保留在算法入口；原HalconInternalTests.ImageConverter_RejectsPaddedRowsBeforeNativeCall仍通过。显示扩展不能抵扣算法Mono16/彩色或Native正向能力。
- 所有支持格式先复制到独立紧凑缓冲，再pin该快照；不直接pin调用方可变像素。沿原GenImage1/GenImageInterleaved和try/finally解pin，返回HObject归调用者释放。紧凑输入也增加一次托管复制，成本尚未实测；没有改全局显示/算法调度或取消语义。
- **未扩出口/domain**：ToVisionImage、ImageFile旧原生读图/受限公共输出、domain/浮点边界不改。准备快照继承FrameId/Timestamp，但原生图像本身不保存这些公共元数据，不承诺原生往返自动恢复帧身份。

### 资源所有权

| 对象 | 创建/拥有与借用 | 释放/验证边界 |
|---|---|---|
| 调用方VisionImage | 调用方拥有，入口只读借用 | 本批不改写/释放；公共Copy独立缓冲已验，不承诺并发写入时原子取帧 |
| 紧凑VisionImage快照 | 原公共Copy创建，仅本次转换拥有 | 校验失败在pin/SDK前退出；成功后由托管GC回收，无新增缓存/会话 |
| GCHandle/HTuple指针 | 转换器本次拥有 | 原try/finally Free与using Dispose；正常/异常代码路径成对，但SDK失败分配/资源实测仍待授权 |
| 返回HObject | HALCON创建，原调用方拥有 | 官方说明独立分配，原调用方Dispose；**原生像素独立性/Alpha与释放正向未执行** |
| ToVisionImage输出 | 原调用方拥有独立托管缓冲 | 原出口逻辑不改，本批不能据离线Copy称Native回读已验 |

### 红绿与测试证据

- **vision-format-red.log**：13离线实例首轮Passed 10/Failed 3。失败是BGRA尚被格式拒绝，未按支持格式执行畸形缓冲/短stride/溢出预检；没有通过调用SDK或检测机器恰有License来凑绿。
- **vision-format-focused.log**：接入原Copy与bgrx代码、保留算法stride守卫后，13新契约＋12原HalconInternalTests共25通过。五格式奇数宽/填充/独立缓冲只证明原公共打包，不证明HALCON生成图像；原拒绝测试继续绿。
- **HalconDisplayFormatTests 13项**：五种公共格式打包5例；BGRA非法缓冲/短stride/宽度溢出在Native前拒绝3例；旧算法Mono16/RGB/BGR/BGRA拒绝4例；Float32/负stride仍拒绝1例。不是13个Native算法实例。
- 原**HalconImageSnapshotRuntimeTests**从4例扩为5格式×紧凑/填充10例，沿原RequiresRuntime/Skip未解除：独立预期像素、RGB/BGR换序、BGRA Alpha=0/128/255仍只丢第4通道、生成原生图后改原缓冲再回读、释放原生图后公共结果仍可读。全部只编译，**Native S/C/U/L未执行**。
- **591逻辑/483WPF/416流程/66属性定向**全部0失败0跳过，宿主增量0警告0错误；共享DisplayImageMigrationTests额外1项通过，仅原三显示页、无原生窗口的视口捕获/清理，不调用已布局GetDisplayImage正向HALCON，也不证明bgrx色彩。

### 共享显示门禁的还原图故障

首次在原display-retirement产物树执行显示测试，构建阶段失败，不能算测试失败/通过：Vision.Controls临时WPF项目CS0234/CS0246缺FlowEditor.Core/Runtime等引用。排序假设：旧还原图过期、目标框架解析差异、真实引用回归。

对比确认旧产物树`obj/Vision.Controls/project.assets.json`的projectReferences无FlowEditor，而当前Vision.Controls.csproj及已通过完整483的editor-verification还原图均包含FlowEditor.Controls。故先刷新**该旧产物树的生成依赖图**；只用本机NuGet缓存作源，无工程引用/包版本/源配置文件修改，无新Runtime安装或源码绕过。

实际：
`dotnet restore "Software Frame/WPFControls/Tests/WPFControls.Tests/WPFControls.Tests.csproj" --artifacts-path "Software Frame/WPFControls/Tests/obj/display-retirement" --source "$env:USERPROFILE/.nuget/packages" --ignore-failed-sources -p:BuildInParallel=false -m:1 -nr:false`

还原成功后同一显示命令通过1项。保留`vision-format-display-stale-assets-failure.log`与`vision-format-display-restore.log`，最终成功日志`vision-format-display-final.log`；未改生产引用/测试或降低断言，未捕获吞异常/强停进程。旧依赖SourceLink/nullable/VSTHRD等警告仍存在，不将Vision宿主增量0警告称全仓干净。

### 实际命令与摘要

仓库根，SDK10.0.401/.NET9/C#13、xUnit v2/VSTest。以下每条均追加
`-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`，串行。

| 实际命令 | 摘要 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **Passed: 591, Failed: 0, Skipped: 0** | vision-format-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/format-diagnostics"` | **Passed: 483, Failed: 0, Skipped: 0** | vision-format-wpf-final.log |
| `dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'` | **Passed: 416, Failed: 0, Skipped: 0** | vision-format-floweditor-final.log |
| `dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid\|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'`（表内竖线转义，实际无反斜线） | **Passed: 66, Failed: 0, Skipped: 0** | vision-format-propertygrid-final.log |
| `dotnet test "Software Frame/WPFControls/Tests/WPFControls.Tests/WPFControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFControls/Tests/obj/display-retirement" --filter FullyQualifiedName~DisplayImageMigrationTests --logger 'console;verbosity=normal'` | **Passed: 1, Failed: 0, Skipped: 0**（刷新旧还原图之后） | vision-format-display-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **显式增量0警告/0错误** | vision-format-host-final.log |

检查退出码、预期非零测试数、Unhandled exception/Test host process crashed/宿主崩溃/No test matches；原生用例过滤排除，Skipped 0不等于HALCON通过。最终diff --check与暂存区复核；累计修改保留、未暂存提交。不下载模型、不接相机/设备/生产、不安装授权Runtime或解除Skip。

### 两项官方对照与差距

2026-10-25，全文快照`%TEMP%/vision-format-reference/`；Microsoft Learn先搜索后按.NET9全文读取并核对官方样例。

| 来源 | 对照与未验边界 | SHA256 |
|---|---|---|
| [HALCON2311 gen_image_interleaved](https://www.mvtec.com/doc/halcon/2311/en/gen_image_interleaved.html) | bgrx四通道第4通道直接丢弃，新图由HALCON分配独立存储，不能把预乘BGRA自动当直通颜色还原。沿原0 Alignment使用公共紧凑行；原指针保持IntPtr/HTuple无32位截断。只是代码对照与离线契约，10个Native往返/释放例待授权 | AB026167F567D60F28283B62B078906B49837EAD0961982D9C93A402947F8115 |
| [.NET9 GCHandle](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.gchandle?view=net-9.0) | Pinned避免GC移动；必须显式Free，否则泄漏。只pin本次独立快照、沿原finally解pin，不新增长寿命pin或unsafe。原生异常分配/释放与调用性能未实测，不据代码审查销项 | C36CAA4B815CAFC46EAF736DAA474BE6BE99E804D5B516EC82E7E46ACADB88D7 |

本批无新增UI/原生截图，未将此前Count/OpenCV截图冒充BGRA HALCON转换。A3其余格式/资源、Native S/C/U/L、物理UI/主题DPI、实图精度/节拍，A2稳定项/多来源/多规则等继续待办。最新V1.35：28算法/29工具箱、591/483/416/66＋1共享显示；以下为历史。

## V1.34实际（2026-10-25）：A2/R1c3 Blob目标数量→公差→原Switch

### 行为与复用

- 既有OpenCV/HALCON BlobResult追加INumericResult/只读Count，字段只允许`Count`，数值来自**最终Items.Count**：搜索域、MinArea、用户特征筛选之后；不含背景，不直接使用连通域返回标签数，也不是当前选中行数/目标ID。两后端原Connection/FillUp差异保留；Count不能证明跨帧稳定项身份。
- NumericUnit只末尾追加Count=4，原0/1/2/3不重排；Count独立于Gray8/Gray16/Pixel，输入必须有限/非负/整数，即使消费定义未额外要求整数，也不能把小数Count截断使用。公差显式Count单位，有限小数界限仍合法（[2.5,3.5]接受整数3）；仍校验同ImageIdentity。数量不是空间坐标，不应用Pixel直接来源坐标锚点，也不宣称跨Crop映射能力。
- 原ToolRegistration/目录只增加Count白名单，HALCON内部注册方法增加可选数值字段参数；原公开标识/工厂/导航/默认Blob配置/旧目录键保留。**28算法＋1原Switch不变**；无新工程/工具/依赖/绑定表、公式、属性、执行或文件引擎。
- 沿原节点字段、参数/来源/单位下拉、原Connection/PortValueTransfer/Card、独立准备闭包、布尔端口与原Switch。原公差默认Pixel、原Blob默认未选字段不变；不自动改规则/绑定。成功空域/无前景可输出0；默认未执行/缺图/参数失败/释放后的结果不输出数量。当前Blob通过既有RoiImage已形成本次输出/Dispose置空来区分未执行，不改原Success默认契约、不加运行标记或缓存表。
- 实际0目标在[0,0]为OK，3目标在[0,3]为OK，3目标在[0,2]为成功执行/NG；原运行按钮只走对应OK/NG支路。原节点单位下拉从Pixel改Count、字段/来源/布尔下拉真实回写原DTO，原完整结果窗口借用同一Session；关闭窗口不释放，保存关旧会话重开再执行。
- 缺失字段、断线、旧帧、删源和真实无效阈值都不能回退0/上轮布尔，原运行锁保留。配方只存上下限/单位，不写Measurement/AppliedMeasurement或运行值；旧Blob剥去数值State/端口后恢复MinArea=10且不自动选Count，有新增空输出能力而非自动绑定。
- 窄结果表原有横向滚动会让最后单位列不在初始视野，未重做布局；公差摘要补同一实际单位（例如`NG · 3 [0,2] Count`），Count与Pixel/Gray域不会只凭数字猜测。

### 生命周期

| 对象 | 所有者/借用 | 停止与释放 |
|---|---|---|
| 原Blob工具/原生中间对象 | 原Card/Workspace及原Run | 不新增跨Run资源；仍原等待实际执行后Dispose |
| BlobResult/列表/RoiImage | 原执行会话；Count只读列表长度 | 原清结果/Dispose撤RoiImage，数值接口不再输出；不改变原Items可变公开契约 |
| NumericValue Count快照 | 本次准备闭包 | 不借用Items/图像，原列表清空/原结果工具释放/输入像素改写后，已准备公差仍得到原3及界限 |
| 原完整工具窗口 | 原节点Session的借用者 | 关窗不DisposeSession，关旧流程原Workspace关闭；新会话不带旧Result/BoundNumeric |
| 数值与分支连接 | 原Chart/Connection/执行器 | 缺失/断线/删源/来源失败撤输入与布尔，不回退配方或旧值 |

### 红绿与新增用例

- **vision-count-red.log**：16项首轮真实执行，Passed 2/Failed 14；Blob未实现显式数值接口、公差拒绝Count、负数/小数Count未拒绝的实际失败。先有独立4/9/16像素面积与数量期望，再接实现；NaN/Infinity已有守卫的2项先绿并保留。
- **vision-count-unit.log**：实施后16项全绿；随后补两个真实搜索域实例（域内1/域外0），最终完整逻辑**578=旧560+18**。包括失败/缺图/未执行/释放无值、白名单、面积/特征筛选后计数、0/有效NG、小数界限、输入数量不允许小数/负数、单位/旧帧拒绝、不可变快照与枚举旧值。
- **vision-count-catalog-red.log**：两目录尚无Count白名单时实际失败；接入原注册后通过。
- **vision-count-ui-first.log**：5过1失败；失败是测试试图经原编辑器输入256而被已有灰度0..255守卫拒绝，**不是生产回归，也没有放宽输入守卫**。改成可输入范围内反序阈值128..100，经真实Validate失败验证撤链；随后6项全绿。
- **vision-count-unit-display-red.log**：实际摘要`公差：OK · 0 [0,0]`无单位，新增断言先红；摘要追加实际单位后完整483通过。
- **VisionBlobCountTests最终7实例**：原字段/单位/来源/布尔下拉与原运行按钮三种数量链、原完整结果及窗口所有权、关旧会话重开、旧State/端口兼容、断线/旧帧/真实阈值失败/删源/运行锁、两后端目录白名单与单位摘要。
- HALCON的1项托管结果合约**不使用SDK**，仅验证最终列表Count/单位/失败无值；原BlobSearchRegionRuntimeTests增补非空2、空域0、过滤后0断言，仍RequiresRuntime/Skip，**未执行Native S/C**。不把OpenCV或托管列表抵扣HALCON。

### 实际命令与门禁

仓库根，SDK10.0.401/.NET9/C#13、xUnit v2/VSTest。以下每条均追加
`-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`，串行，原artifacts/no-restore路径不变。

| 实际命令 | 摘要 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **Passed: 578, Failed: 0, Skipped: 0** | vision-count-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/count-diagnostics"` | **Passed: 483, Failed: 0, Skipped: 0** | vision-count-wpf-final.log |
| `dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'` | **Passed: 416, Failed: 0, Skipped: 0** | vision-count-floweditor-final.log |
| `dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid\|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'`（表内竖线转义，实际无反斜线） | **Passed: 66, Failed: 0, Skipped: 0** | vision-count-propertygrid-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **显式增量0警告0错误** | vision-count-host-final.log |

核对退出码0、非零预期数量及Unhandled exception/Test host process crashed/宿主崩溃/No test matches；RequiresRuntime仍过滤排除，Skipped 0不表示Native通过。旧nullable/xUnit依赖警告不被宿主增量0掩盖，未改Skip/包/Runtime/阈值断言，未接设备/模型/生产。最终diff --check与暂存区核查见vision-count-diff-check.log；累计工作区修改保留、未暂存/提交。

### 产物/人工边界

`Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/count-acceptance/`：
- blob-count-0-0.flow、blob-count-3-3.flow、blob-count-3-2.flow；对应count-branch/count-verdict截图；legacy-blob-without-count.flow。
- 实际查看3目标在[0,2]的原5节点/6条线、仅NG绿边；原完整结果3/0/2/NG与底部Count单位、零目标0/0/0/OK及Count单位。显示图放大/插值不等于修改原像素或实图计量证据。
- 真实OpenCV与原控件自动化不抵扣物理鼠标/OLE、焦点/暗色/DPI/跨屏、实图精度/节拍或SDK授权。仅单最终总数，不是稳定项/跟踪、面积标量、文本/列表/多规则聚合、多来源/mm/通用坐标链。

### 三项官方对照（2026-10-25）

HTTP200全文快照`%TEMP%/vision-count-reference/`，枚举文档先Learn搜索再全文读取；保留固定OpenCV4.11与HALCON2311版本。

| 来源 | 实现对照与差距 | SHA256 |
|---|---|---|
| [OpenCV4.11 imgproc.hpp](https://raw.githubusercontent.com/opencv/opencv/4.11.0/modules/imgproc/include/opencv2/imgproc.hpp) | ConnectedComponents返回N包含背景标签0；原算法跳过0再面积/特征筛选，所以Count取最终Items.Count，不能直接用N或N-1。真实0/3/筛选1/域1或0已验；不改变连通规则 | 9020D31168E318655B72DCE3A171057FD8E5A05568C25ADFE3B38C6DA49158C1 |
| [HALCON2311 count_obj](https://www.mvtec.com/doc/halcon/2311/en/count_obj.html) | 对象数不等于连通分量，一个含三不连通部分的region仍为1。继续原Connection/FillUp/SelectShape后的最终Items，不重写原生算法；Native断言已补但未执行，不能承诺与OpenCV同一物理数量口径 | 9CFFA4D48FFD454FBCA13BF314EC04540E9E45A1030A8A1747F558672F0C3B38 |
| [.NET Enum Design](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/enum) | 可追加值但旧调用方未必处理；只追加Count=4，原构造/解构与枚举旧值不动，旧Blob未选字段兼容，新Count文件需认识新单位的Reader | 1BF0CCA813F460ACCC935C869A8A9D73B6B3D7C8E6A077E0AC19DF6E20D9CAEF |

最新V1.34：28算法/29工具箱、578/483/416/66；以下V1.33及更早为历史。

## V1.33实际（2026-10-25）：A2/R1c2原条件分发入口

### 实施范围

- 将既有SwitchNode通过原FlowToolAttribute元数据投影为原NodePaletteItem，追加原工具箱“流程控制/条件分发”。**28个算法条目不变，工具箱29项=28算法+1原控制节点**；没有Switch视觉会话、第二规则框架、属性面板、执行器或文件格式，隐藏ConditionNode未重新上架。
- 同一原FlowChartContainer工具箱/搜索/添加命令；新AddNodePaletteAt只交原NodeModel工厂/Chart.LoadNodeModel，原投放事件共用它。旧AddPaletteAt仍仅视觉且返回VisionToolNode，不改签名或偷偷添加控制节点。新Switch独立实例、选中原NodeModel、原属性、坐标和画布扩展已测；运行/关闭拒绝添加。
- SwitchNode新增**通用可选RequireInputConnection**：默认false，旧工程常量规则不变；视觉新建设true，断线即使InputValue残留上轮/常量也报错。默认仍原1/2→OUT1/OUT2，不自动替换；布尔需用原规则窗口显式配置true→OK/false→NG。匹配/端口同步/剪枝继续原Switch逻辑。
- EditDispatchRules→原WpfNodeEditorLauncher→原DispatchRuleEditorWindow.ShowDialog。取消/重复规则不写回、保存原端口同步、A窗口选B仍只写A；不复制规则表。新连接约束/原ApplyRuleConfiguration及窗口打开/保存有运行锁，UI启动器对运行中Switch提示返回，不让拒绝异常抛入Dispatcher。
- 原窗口基类会在构造时注册Application.Windows；直接构造被运行锁拒绝时必须先Close撤注册。初版未清导致两断言全过却STA退出超时，已修并加集合数量精确断言；未改Fixture吞异常/超时或强停进程。
- 真实Hough→直径→公差→工具箱新Switch→OK/NG标记，原下拉/原运行按钮只走匹配支路；原模态窗口真实保存/取消/校验，保存关旧会话重开保规则/端口/严格标志。断线/未命中/未选布尔字段均不执行下游。受控旧无新标志.flow恢复常量YES，当前新建仍严格。
- 仅当前原命令/程序化投放与模态控件路径验收；**不是物理鼠标/OLE/焦点、暗色/DPI/跨屏或全部控制节点验收**。多规则公差、稳定项/多来源、多槽/mm/一般坐标链与真实SDK/设备/节拍不销项。新严格标志需认识它的新Reader，不保证旧软件能执行新语义。

### 生命周期

| 对象 | 拥有/借用 | 结束规则 |
|---|---|---|
| SwitchNode | 原Chart/文档，原属性与窗口借用 | 无Vision Workspace/SDK，沿原节点/流程退出 |
| 原规则窗口 | 原WpfNodeEditorLauncher，固定借用构造节点 | 原ShowDialog/Owner/取消/保存；运行拒绝直接构造先Close再抛，UI入口非抛错返回 |
| 规则行临时集合 | 原DispatchRuleEditorWindow | 仅有效保存交ApplyRuleConfiguration；取消/重复/运行锁不写节点 |
| 工具箱项 | 原NodePaletteItem/容器，元数据不创建算法 | 原关闭/运行CanExecute，旧视觉接口保留 |
| 输入/控制连接 | 原Connection/PortValueTransfer/执行器 | 严格模式必须真实InputValue连接，旧值/常量不能补缺失 |

### 红绿/失败证据

- **vision-dispatch-red.log**：推荐分发未在视觉工具箱，原目录用例实际先红；接入后通过。
- **vision-dispatch-editor-lock-red.log**：原窗口运行中构造未拒绝，Assert.Throws实际失败；加入运行锁后该断言通过。
- **vision-dispatch-window-cleanup-failure.log**：初版在Window已注册后直接抛构造异常，2断言通过但Fixture报“工作台STA线程未在关闭后退出”；**不计成功**。改为Close后拒绝，并断言Application.Windows数量不增；-first.log两项干净退出，后完整476/定向6通过。没有改Fixture/吞异常或强停用户进程。
- 复查发现仅构造抛错会从原UI启动器冒到Dispatcher，针对实际EditDispatchRules入口去掉本批新UI保护取得**vision-dispatch-ui-lock-red.log**，真实失败；恢复启动器提示返回后完整476再跑通过。直接API构造/规则事务的异常守卫仍保留。
- **SwitchRequiredInputTests 2项**：原默认false无连接常量仍走OK；严格true无Service/无输入线不能使用true常量。**VisionDispatchTests 6实例**：原工具箱命令/选中/投放位置/画布扩展/旧API、两条实际公差分支与原模态保存/重开，断线旧值/未命中/布尔缺失停止，旧标志缺失兼容，取消/重复规则/A-B/运行锁及窗口注册清理。
- 末次仅将测试旧图同步已完成调用改await去除新增测试阻塞警告；随后UI入口保护最终完整476、416重跑。未删Skip、放宽长度/计时断言或添加临时生产日志；无新依赖/Runtime/模型/硬件/授权动作，暂存/提交未动。

### 实际门禁与命令

仓库根，SDK10.0.401/.NET9/C#13、xUnit v2/VSTest。各命令均追加
`-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`，串行执行。

| 实际命令 | 摘要 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **Passed: 560, Failed: 0, Skipped: 0** | vision-dispatch-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/dispatch-diagnostics"` | **Passed: 476, Failed: 0, Skipped: 0** | vision-dispatch-wpf-final.log |
| `dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'` | **Passed: 416, Failed: 0, Skipped: 0** | vision-dispatch-floweditor-final.log |
| `dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid\|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'`（表内竖线转义，实际无反斜线） | **Passed: 66, Failed: 0, Skipped: 0** | vision-dispatch-propertygrid-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **显式增量0警告/0错误** | vision-dispatch-host-final.log |

退出码/非零测试数、Unhandled exception/Test host process crashed/宿主崩溃/No test matches均核查；不能只看断言成功。RequiresRuntime仍过滤排除，Skipped 0不表示HALCON/AI通过。旧依赖nullable/xUnit警告仍存在，不将增量0警告称全仓干净；最终diff --check通过。

### 产物与边界

- `Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/dispatch-acceptance/`：dispatch-True/False.flow、dispatch-True/False.png、original-rule-editor.png、legacy-switch-constant.flow。实际查看原规则表true→OK/false→NG、原布尔数据线/分发支路、要求输入连接勾选、LastMatchedBranch=NG和仅NG分支绿边；原28算法＋1流程控制元数据可搜索/添加，非新主布局。
- 规则窗口为原模态对话框；自动化通过其真实按钮/原入口完成，不以替代假对话框抵扣。工具箱测试使用原AddNodeCommand，投放使用同一路由的程序化坐标验证，物理双击/OLE、焦点/跨屏/暗色/DPI矩阵仍未执行。
- 旧常量兼容样本为从当前保存剥去新选项的受控旧结构，不穷尽仓外旧文档；原ConditionNode历史入口保留但继续隐藏。本轮不是多规则公差/工业联锁或SDK总验收。

### 两项官方来源（2026-10-25）

搜索、全文读取与HTTP200快照`%TEMP%/vision-dispatch-reference/`，采用.NET9 WPF，不把.NET10/11摘要套入本次。

| 来源 | 对照与差距 | SHA256 |
|---|---|---|
| [.NET9 Application.Windows](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.windows?view=windowsdesktop-9.0) | UI线程实例化窗口即加入集合，Closing处理后/Closed前移除。因此Window构造拒绝也需Close；实际孤儿窗口/Fixture退出失败先红，修后集合数量和完整宿主退出通过。不通过修改ShutdownMode或清理用户窗口求绿 | BE243CE833DA9AF831487FC230EDFFB68950AD1B07435ACE911EF70841CBB9BC |
| [.NET9 Window.ShowDialog](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.showdialog?view=windowsdesktop-9.0) | 模态返回须窗口关闭，DialogResult区分提交/取消，Owner由原启动器设置；测试通过原模态消息循环操作真实窗口并清理待调度回调，不另造规则窗口或把模态说成非模态。物理焦点/系统窗口行为仍待 | D704A5BC22AA14EF8792E997249771B9D65E735CE9A8A7E9727110FE76A4247D |

最新V1.33：28算法/29工具箱、560/476/416/66；以下V1.32及更早保持历史，不以旧414/470覆盖本轮。

## V1.32实际（2026-10-25）：A2/R1c1测量→闭区间公差→布尔分支

### 交付与约束

- 未找到现成公差工具，新增**Vision.Core/Tools/ToleranceTool.cs**纯BCL数学及Controls薄适配，不挂厂商名、不建Common.Tools/新基类/执行器。Common/Tolerance唯一新条目；当前28（OpenCV14/HALCON8/AI4/Common2），24传统/公共合约，旧27项键/类型/默认Blob/模板回调保留。
- 单数值**闭区间Minimum≤Measurement≤Maximum**，两端/0合法；默认Pixel，也可显式Gray8/Gray16同域。没有可保存Measurement配方/默认0，未绑定Run明确失败。真实NG是Success=true且IsWithinTolerance=false；失败/未执行IsWithinTolerance=null、无布尔输出，不伪造NG或OK。非有限/乱序界限、NaN/Infinity、单位/帧不符直接拒绝。
- GeometryResult只明确提供Distance/Diameter：须对应运算、实际绑定线/点/圆且Pixel/帧/坐标明确，手动未知单位、操作不符/缺失不输出默认0。NumericUnit追加Pixel而旧序号不改；NumericValue/ParameterValue增加非位置坐标属性，保留原构造/解构。Pixel输入另核对直接图像来源锚点，Gray8/16不混量纲；不放开mm/坐标推断。
- 本次数值和上下限复制入原Card准备闭包，不改原DTO，不借用图像像素/SDK；原工具释放后已准备闭包仍可用。原NodeProperty数值来源/消费参数与新增布尔字段、原ConnectionService/PortValueTransfer/Card/Session/mapper/store/.flow仍唯一链；bool?缺失与false分开，字段写State、来源写原Connection，不造布尔绑定总线。
- 视觉生产节点已接布尔线但未选择字段，或执行后无本次布尔时，明确失败，不让原条件节点拾取上轮Condition或未绑定常量；原ConditionNode合法无绑定常量契约未改。已选非空bool?按原端口装箱为bool，False真实可传。
- 真实组合Hough→圆直径→公差→**原ConditionNode**→两个公共数学分支，OK/NG均只执行选中分支；原按钮/原属性/完整结果表/关旧会话重开验证。ConditionNode是隐藏兼容节点，本批按原模型加入/原线保存恢复，不恢复其工具箱、不宣称默认条件分发节点或物理鼠标/OLE添加已验。
- 范围仅单规则/单布尔；多规则AND/OR、计数/文本/列表聚合、稳定项/多来源、多覆盖槽/mm/一般坐标链、SDK/实图精度/现场节拍均待。合成半径30实际估计29.7→直径59.4，公差比较用59.4，不伪造名义60；不把Hough当环形卡尺。

### 所有权与失效

| 对象 | 拥有/借用 | 结束规则 |
|---|---|---|
| ToleranceParameters | 原工具/Workspace，原属性/完整页借用 | 仅Minimum/Maximum/Unit保存，运行不回写 |
| NumericValue及上下限快照 | 原Card闭包，纯托管double/enum/string | 不引用原工具或像素；工作实际结束/原revision退迟到 |
| ToleranceResult/表 | 原Card拥有，视图借用 | 原清结果/表/关闭链负责撤销和释放；无SDK资源 |
| BooleanOutput | 原节点当前结果投影 | 未选/失败/无结果为null，有效NG才false；原线/State持久化 |
| ConditionNode | 原通用图/执行器 | 原True/False激活规则；视觉输入失败不执行它或任何下游标记 |

### 红绿与UI复查

- **vision-tolerance-red.log**首个目录用例实际红（Common/Tolerance不存在）；-first.log接入后通过。真实数学18项和真实组合6项随后执行，不用注册/Validate代替S+C。
- **ToleranceTests 18实例**：闭区间上下限/内部/上下越界、合法0与NG成功false；无输入/未执行无布尔；独立快照和原工具释放；NaN/Infinity/单位/帧/坐标/参数名/乱序界限、Gray8/Gray16显式同域、真实已绑定几何Pixel字段及手动未知单位拒绝。
- **VisionToleranceTests 6实例**：原控件/按钮Hough→直径→公差→ConditionNode，两分支分别真实执行（标记5/13），原.flow关旧会话重开；清布尔选择后两分支不再执行，测量0在[0,0]为OK、断线不复用旧值；旧帧/Gray送Pixel拒绝/运行锁；不同裁剪同像素/hash/帧但坐标锚点不符拒绝，改同一输出可执行。
- 新条目加入原EveryCatalogTool面板和24合约，原目录/节点/工具箱精确数量从27到28，未放宽参数、像素或生命周期断言。旧标量/线/圆/点完整回归保留。
- 截图初版闭区间OK列在最右不可见，已在**新工具原结果表**将OK列前置，不改共享表框架。测试默认节点坐标相互遮挡，改为本测试显式两排布局；打开完整窗口后容器按原活动策略暂停连线视觉，留图改为开窗前+ApplicationIdle。未改生产布局/活动策略，也不将无连线截图当连接验收。更新后实际查看6节点/8条原控制和数据线、NG仅对应分支绿边、可见OK复选列与NG摘要。
- 完整470两轮通过（最终表列/测试布局后重跑），最后仅测试留图时序改动后6定向再过（vision-tolerance-ui-final.log）；不是修改算法求绿。保留-wpf-before-table-layout.log，无临时生产探针/删Skip/改阈值；未装Runtime/模型、接硬件或改授权，暂存/提交未动。

### 实际门禁与命令

仓库根，SDK10.0.401/.NET9/C#13、xUnit v2/VSTest。每条均追加
`-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`，串行执行。

| 实际命令 | 摘要 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **Passed: 560, Failed: 0, Skipped: 0** | vision-tolerance-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/tolerance-diagnostics"` | **Passed: 470, Failed: 0, Skipped: 0** | vision-tolerance-wpf-final.log |
| `dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'` | **Passed: 414, Failed: 0, Skipped: 0** | vision-tolerance-floweditor-final.log |
| `dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid\|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'`（表内竖线转义，实际无反斜线） | **Passed: 66, Failed: 0, Skipped: 0** | vision-tolerance-propertygrid-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **显式增量0警告/0错误** | vision-tolerance-host-final.log |

核查退出码、非零测试数和Unhandled exception/Test host process crashed/宿主崩溃/No test matches；RequiresRuntime仍过滤排除，Skipped 0不表示HALCON/AI Native通过。依赖重编译旧nullable/xUnit警告保留，不将增量0警告冒称全仓干净；最终diff --check通过。

### 产物与未验

- `Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/tolerance-acceptance/`：tolerance-True/False.flow、branch-True/False.png、verdict-True/False.png。实际查看：59.4在[100,200]为NG、OK未勾选；原条件False支路执行13，True标记未执行。参考上下限/单位和选择保存，重开无运行布尔，执行后重算，不保存运行判定。
- 像素框架无单位的旧手动Geometry不被变成Pixel；当前灰度直接公差仅同域契约/纯数学证据，未据此宣称全部Gray→条件UI链。默认条件分发UI/物理拖入、多个布尔逻辑/工业联锁、暗色/DPI/跨屏、实图公差准确度/重复性/节拍、真实SDK/设备均未验收。

### 两项官方来源（2026-10-25）

检索、全文读取与HTTP200快照`%TEMP%/vision-tolerance-reference/`；只采用当前C#13已存在的比较/装箱规则，不套用预览特性。

| 来源 | 对照与差距 | SHA256 |
|---|---|---|
| [Nullable value types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/nullable-value-types) | bool?有true/false/null，非空装箱为底层bool，空装箱null。本仓保留NG false、缺失null；视觉生产者对连线缺失主动失败，不依赖通用Condition常量/默认false，原两分支真实验证 | C843666C7B74D5B9F2C9C06B281782E7DF0DA95381BF19211F90A20E523DBE14 |
| [Comparison operators](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/comparison-operators) | <=包含端点，NaN参与关系比较均false，不能把比较false直接当有效NG。先验有限值/界限/单位/帧/坐标，再闭区间判定；0与两端值有真实单工具验证 | 99276484864EB0D7DB101DFAAF0745B65745719F92DE4DCC696509345AC94952 |

下一步多规则/稳定项/多来源、原默认分发入口或A3/R2边界按计划继续；本轮不宣称全R1/十项终验完成。以下V1.31及更早保留历史计数。

## V1.31实际（2026-10-25）：A2/R1b3单点→配方参考点

### 交付与限制

- OpenCV/FindCircle显式Center，OpenCV/FindLine显式Start/End，新增IPointResult/PointValue；仅当前已选结果字段，不反射任意子属性或用排序行号当对象ID。PointValue是值类型Point2D与Pixel/输入/坐标字符串快照，原点合法、缺失用null。非像素、失败/无有效边或圆、未知字段不输出点；ToWorld/ToMachine后的线点同样不能冒称Pixel。
- 原Common/Geometry.Distance新增可选IPointParameterTool.FirstStart。本次仅覆盖执行起点，配方FirstEnd为固定参考点；原FirstStart/FirstEnd均不回写。参考点在原完整Geometry参数“第一组终点”编辑，新增说明，不扩张共享编辑器或造参考点控件。参考点属于当前输入的固定像素坐标，不是第二动态点来源，也不随工件定位/世界坐标自动变换。
- 原NodeProperty点字段/点测量输入/点来源、ConnectionService、PortValueTransfer、Card、Workspace、mapper/store/.flow仍唯一链；接口默认空能力，旧实现未被强制新增成员。仍27目录/23传统公共合约，不新增工具、枚举、工程或第二执行/属性/文件框架。当前一次仅一种覆盖，数值/线段/圆/点混合明确拒绝。
- 同ImageIdentity及TaskInput或直接NodeId+OutputPortId锚点；不同裁剪即使同像素/hash/帧/尺寸仍拒绝，同一显式裁剪可执行。不推断滤波/转换等坐标等价，不开放mm/缩放/镜像/一般坐标链。缺失/失败/断线/删源/旧帧/非有限点/未知单位不能当原点或回退配方；断线立即撤BoundPoint/结果。
- 原结果表显示实际点、配方参考点、距离与Pixel；原叠加画连线/两端十字。三个真实字段组合独立3:4:5布局预期约50；原配方起点(1000,1000)不变，关旧会话/新会话重开配置与结果可复现。真实检测点设为参考点时距离0成功；随后断线必须失败，不因0被当缺失，也不拿缺失当0。
- 有限极大坐标的旧Point2D距离可能经差/平方溢出得Infinity仍Success，已先红后绿加入Distance结果有限性校验。不重写Point2D/引入Hypot，不宣称任意极大数值稳健性；对旧算法不可表示的计算明确失败，普通有效数学与旧字段/enum保持。
- 下一步仍需布尔/公差消费者、稳定项身份、双点/双线等多来源/多槽与更多消费者；R4定位跟随/实图精度、SDK/物理UI不销项。此批不是全R1或计量终验。

### 所有权

| 对象 | 拥有/借用 | 释放/失效 |
|---|---|---|
| PointValue与执行参数 | 原Card准备/闭包，纯托管值 | 无可变点集、像素或厂商句柄借用；ClearResult/连接变更撤临时输入 |
| 配方起点/参考点 | 原工具/Workspace，原面板借用 | FirstEnd按值复制到执行参数，运行不写回；关窗不释放会话 |
| 独立GeometryTool | 原Card调度闭包创建并释放 | 原工具释放后已准备闭包仍执行；沿原revision/实际调用等待链 |
| 字段/来源 | 原State/Connection | 保存引用与配置，不保存运行PointValue；原订阅退出链未另造 |

### 红绿证据

- **vision-point-red.log**：原PointOutputField属性用例实际先红，接通后-first.log通过，不以工厂/Validate当真实组合。
- **vision-point-math-red.log**：两个有限极大端点，旧Geometry返回Infinity却Success，Assert.False实际失败。新增DISTANCE_OVERFLOW校验后该用例与完整537通过；未改数学函数、普通像素长度预期或断言阈值。
- **vision-point-unit.log 16/16**：真实Hough/卡尺选字段、独立快照与原工具释放、合法原点/相同非零点距离0、非法单位/帧/锚点/坐标/参数名、坏参考点与溢出。**vision-point-focused.log 10/10**：原控制/原命令真实三条链、0与缺失/断线/失败、旧帧/删源/运行锁/同像素异裁剪、原Card同步/异步混合拒绝、旧结构配置；最终完整537/463再过。
- 受控旧结构.flow移除新增Point State/端口，关旧会话后加载原Distance=5，增加能力端口但不自动启用原点绑定。仅证明该旧结构；新点连接需要新Reader，不保证旧软件能理解新语义或所有仓外历史文件。
- 未删除失败测试、增Skip、改阈值/放宽断言；无新包/Runtime/模型、生产硬件或授权变更。未添加临时生产探针，暂存和提交未动。

### 实际门禁与命令

仓库根，SDK10.0.401/.NET9/C#13、xUnit v2/VSTest。每条均追加
`-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`，串行执行。

| 实际命令 | 摘要 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **Passed: 537, Failed: 0, Skipped: 0** | vision-point-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/point-diagnostics"` | **Passed: 463, Failed: 0, Skipped: 0** | vision-point-wpf-final.log |
| `dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'` | **Passed: 414, Failed: 0, Skipped: 0** | vision-point-floweditor-final.log |
| `dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid\|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'`（表内竖线转义，实际无反斜线） | **Passed: 66, Failed: 0, Skipped: 0** | vision-point-propertygrid-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **显式增量0警告/0错误** | vision-point-host-final.log |

已核对退出码、非零测试数和Unhandled exception/Test host process crashed/宿主崩溃/No test matches。RequiresRuntime被原过滤排除，Skipped 0不抵扣HALCON/AI Native；重编译依赖旧nullable/xUnit警告保留，不将增量0警告冒称全仓无警告。最终diff --check通过。

### S/C/U/L与产物

- **PointParameterTests 16实例**：3条真实算法取字段（Center/Start/End），有限极大距离溢出、独立快照/参考点冻结/原工具释放、2种合法零距离、7种坏输入、坏配方参考点、无结果/失败/非像素与合法原点。原WorldCoordinateTests补映射结果不能输出像素点断言。
- **VisionPointBindingTests 10实例**：三个实际字段经原下拉/按钮→固定参考点距离并关旧会话重开；配方与实际点/参考点/单位表，当前检测点等于参考点得到0；失效/原画布断线/删除源/全黑失败、旧帧/坐标锚点、运行锁、原Card两执行方式混合拒绝、旧配置不自动绑定。
- `Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/point-acceptance/`：point-distance-Center/Start/End.flow、point-binding-Center/Start/End.png、measured-point-Center/Start/End.png、legacy-without-point-binding.flow。已实际查看End结果距离50、起点(180,79.5)到参考点(150,119.5)的绿色连线/十字及Pixel单位；Center原流程来源非空，原整页操作栏/工具箱/属性/输出仍在。
- 合成布局/STA/截图不等于物理连线、鼠标/OLE、暗色/DPI/跨屏、实图精度/重复性/节拍或真实SDK/设备。两动态点来源、mm与定位跟随不由此证明。

### 两项官方来源（2026-10-25）

检索/全文读取与HTTP200快照`%TEMP%/vision-point-reference/`，按OpenCV4.11与.NET9核对。

| 来源 | 对照与差距 | SHA256 |
|---|---|---|
| [OpenCV4.11 imgproc.hpp](https://raw.githubusercontent.com/opencv/opencv/4.11.0/modules/imgproc/include/opencv2/imgproc.hpp) fitLine/HoughCircles | Hough返回(x,y,radius)，fitLine返回方向/线上点而非线段端点；Start/End沿原实际检测投影跨度生成，Center沿原实际已选圆。只暴露已存在字段，不拿名义搜索线/期望圆心冒充检测点，也不宣称物体端部/亚像素计量精度 | 9020D31168E318655B72DCE3A171057FD8E5A05568C25ADFE3B38C6DA49158C1 |
| [.NET9 Math.Sqrt](https://learn.microsoft.com/en-us/dotnet/api/system.math.sqrt?view=net-9.0) | 正无穷输入仍返回正无穷；差/平方溢出不会自动变成业务失败。本仓显式验证旧DistanceTo结果有限，原点/同点0保留；不把有限性检查冒称大数稳健算法升级 | 00C165C420CAE176A290146B94463981BE594E4B1DEC1D837086B13B862CC433 |

本轮依据当前源码/真实执行核查，不用陈旧图索引缺失推断无消费者。最新V1.31/537/463在此，以下V1.30及更早记录保留历史。

## V1.30实际（2026-10-25）：A2/R1b2圆→公共直径

### 交付范围与限制

- OpenCV/FindCircle新增显式ICircleResult.Circle；它是按原ExpectedCenter就近选择的当前Hough圆，不是全候选列表/排序索引/跟踪ID。CircleValue只含值类型圆心/半径、Pixel与输入/坐标身份，不借用原生对象或可变集合。共用原GeometryUnit和R1b1捕获的坐标锚点，不新增坐标引擎。
- 同一Common/Geometry追加CircleDiameter运算：原Distance=0、LineIntersection=1保持，CircleDiameter=2。GeometryParameters增加CircleCenter/Radius，原手动距离/求交及HALCON命名空间/类型不删；直径仅2R公共数学，不要求HALCON图像/License，不新增Common.Tools项目。目录仍27项、23传统/公共合约，旧键和默认值不变。
- 原NodeProperty圆字段/圆测量输入/圆来源下拉，经原ConnectionService/PortValueTransfer/Card/Workspace/mapper/store/.flow执行。可选ICircleParameterTool及旧Session/Adapter默认空能力；未改旧方法签名、不替换原DTO，仓内兼容门禁不代表所有仓外接口实现/旧版本Reader可执行新CircleDiameter配置。
- 本次只允许一种参数覆盖：数值/线段/圆同时启用由原Card明确拒绝，同步/异步都测，不自动取消旧绑定或择优回退。圆绑定仅CircleDiameter，不混线段距离/求交。未绑定手动直径半径必须正、有限且2R可表示；原点圆心合法，默认0半径不当有效圆。
- 快照只复制执行参数/圆值；原配方半径0/99分别在真实半径26/38链路执行、保存、关旧会话/重开后仍0/99，不写运行结果。原表显示实际圆心/半径/直径/Pixel，结果图画实际圆与直径；手动无单位不伪标Pixel。38像素样本Hough实际半径37.5→直径75，不用配置38×2伪造76。
- 输入必须同ImageIdentity及TaskInput或直接NodeId+OutputPortId锚点；不同裁剪产生相同像素/hash/帧/尺寸仍拒绝，同一显式裁剪可执行。不推断中间处理坐标等价，不开放mm/一般仿射/缩放/镜像。缺失/失败/未选字段/断线/删源不当原点或配方直径198，连接/上游失效立即撤BoundCircle和结果。
- Hough仍原Gaussian+HOUGH_GRADIENT，8位单通道、原半径floor/ceil、Canny/累加器及就近选择语义不变；不是环形亚像素卡尺/圆点集拟合，不承诺搜索ROI/定位跟随、实图计量精度或现场节拍。点/布尔公差/多槽/稳定项身份与通用坐标链仍待，不销整个R1/R4。

### 生命周期

| 对象 | 拥有/借用 | 结束规则 |
|---|---|---|
| CircleValue/本次参数 | 原准备/Card，纯托管值 | 值类型点/double、不可变字符串；ClearResult/连接变更撤临时输入 |
| GeometryParameters | 原节点Workspace/工具，原属性与完整编辑器借用 | 原配方不被运行快照写回；关窗不释放会话 |
| 准备后的GeometryTool | 原Card调用闭包创建并释放 | 不引用原工具/图像像素；原工具释放后已准备闭包仍可执行 |
| 结果/叠加/表 | 原Card拥有，原结果视图借用 | 原revision丢弃迟到结果、旧表/结果按原链释放 |
| 配置与端口 | 原节点State/Connection/mapper | 只保存字段/槽/来源，不保存CircleValue或SDK对象；原订阅退役链不变 |

### 红绿证据与兼容

- **vision-circle-red.log**：首个原节点属性用例实际先红，CircleOutputField尚不存在；-first.log接通后通过，不把编译/工厂创建当真实组合。
- **vision-circle-hough-validation-red.log**：原FindCircle正负比较未拒NaN/Infinity/超int范围。仅对本批校验改动临时还原原条件，运行6个Validate断言全红（Expected False/Actual True），断言即停，没有让坏参数进入SDK。恢复新有限/范围守卫后27个圆逻辑/完整521通过。未改高斯/Hough算子、阈值或几何精度断言。
- Hough现在校验有限的ExpectedCenter、RadiusMin/Max、CannyHigh、AccumThreshold及半径int上限，诊断明确指出有限/范围，不把无穷错误说成小于最小半径。
- **vision-circle-focused.log 8/8**包含两条真实链及原Card同步/异步混合槽拒绝；其后补旧结构兼容用例与配方0场景，最终完整453通过。没有失败被删/加Skip或降阈值求绿。
- 新增受控旧结构.flow：从原保存文件剥去CircleOutputField/CircleInputParameter、CircleCenter/Radius及圆端口；关旧会话，原加载器恢复Distance=5、增加能力端口但不自动绑定/输出圆。它证明这一旧结构路径，不是所有仓外历史工程兼容证明。v1/v2原格式/标识不变，新运算仍要求新Reader。

### 实际门禁与命令

仓库根，SDK10.0.401/.NET9/C#13、xUnit v2/VSTest。各命令均追加
`-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`，串行执行，无新依赖/Runtime/硬件启动。

| 实际命令 | 摘要 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **Passed: 521, Failed: 0, Skipped: 0** | vision-circle-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/circle-diagnostics"` | **Passed: 453, Failed: 0, Skipped: 0** | vision-circle-wpf-final.log |
| `dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'` | **Passed: 414, Failed: 0, Skipped: 0** | vision-circle-floweditor-final.log |
| `dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid\|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'`（表内竖线转义，实际无反斜线） | **Passed: 66, Failed: 0, Skipped: 0** | vision-circle-propertygrid-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **显式增量0警告/0错误** | vision-circle-host-final.log |

已检查退出码、非零测试数和Unhandled exception/Test host process crashed/宿主崩溃/No test matches；RequiresRuntime由原过滤排除，Skipped 0不表示真实HALCON/AI通过。重编译依赖旧nullable/xUnit警告保留，不将增量0警告称全仓干净；最终diff --check通过。未改生产设备、授权或模型配置，暂存/提交未动。

### S/C/U/L及截图

- **CircleParameterTests 27实例**：真实Hough圆/全黑，显式Pixel圆与错误字段/非像素/默认/失败拒绝；2R实际数学/无图/BGR/旧enum值、原点圆心合法、5种坏手动半径、独立参数快照与原工具释放、12种单位/帧/坐标/圆心/半径/溢出/字段名错误、6种Hough参数守卫。
- **VisionCircleBindingTests 9实例**：原属性/下拉/运行按钮，两种合成半径与配方0/99分离、实际结果表/圆+直径叠加，保存关旧Session/新会话重开；缺失/全黑失败/原画布断线/源失效/删源、运行锁、同像素异帧及周期圆图两裁剪同像素不同坐标锚点；原Card同步/异步混合拒绝，旧结构恢复原距离且不自动绑定。
- 产物`Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/circle-acceptance/`：circle-diameter-26/38.flow、circle-binding-26/38.png、measured-diameter-26/38.png、legacy-without-circle-binding.flow。已实际查看38结果：半径37.5、直径75、圆心(80.5,70.5)、Pixel以及绿色圆/直径；26原流程来源不空白、原操作栏/工具箱/属性/输出未另造。
- 未验环形卡尺、搜索域/刚体跟随、实图重复精度/节拍、系统物理连线/OLE、暗色/DPI/跨屏全矩阵、HALCON/AI Runtime及真实设备。CPU数学/合成图/STA不抵扣这些验收。

### 两项官方来源对照（2026-10-25）

检索、全文/对应版本原文与HTTP200快照`%TEMP%/vision-circle-reference/`；不以新版摘要冒充OpenCV4.11或.NET9。

| 来源 | 对照与差距 | SHA256 |
|---|---|---|
| [OpenCV4.11 imgproc.hpp](https://raw.githubusercontent.com/opencv/opencv/4.11.0/modules/imgproc/include/opencv2/imgproc.hpp) L2234–2280 HoughCircles | 输入8-bit单通道，输出(x,y,radius)，param1为Canny高阈值，GRADIENT的param2为圆心累加器阈值。官方明确中心可能较好但半径可能不准，需半径范围/额外过程；本次不将Hough半径或三位小数说成亚像素计量。保留原floor/ceil整数范围语义 | 9020D31168E318655B72DCE3A171057FD8E5A05568C25ADFE3B38C6DA49158C1 |
| [.NET9 Double.IsFinite](https://learn.microsoft.com/en-us/dotnet/api/system.double.isfinite?view=net-9.0) | 排除NaN/Infinity，不等于半径正或2R可表示；本仓另验正半径/直径溢出、单位/帧/坐标与int范围，六个原比较漏洞有实际红绿。Circle快照另沿既有record值拷贝模式，不借用可变像素/集合 | 96FF3E5BEE250D5E485CE47234219A894BD2A0142DE403BF0FAABC391EC8517C |

下一步R1b3点/结果项身份和布尔公差等明确消费者；一般几何坐标/世界单位与多覆盖槽不因当前单圆2R通过而放开。历史V1.29以下计数保留，不覆盖本轮521/453证据。

## V1.29实际（2026-10-25）：A2/R1b1线段端点距离

### 范围与语义

- 原OpenCV/FindLine新增ILineResult稳定字段Line；单条拟合结果无需排序行号/索引。Core新增LineValue（两个Point2D值类型、Pixel单位、ImageIdentity、CoordinateIdentity）/LineParameterValue及可选ILineParameterTool。既有GeometryTool距离/求交仍沿Point2D数学，UsesInputImage=false避免无关图像转换；保留旧HALCON命名空间/类型/方法和手动参数入口，新增Common/Geometry条目，不造Common.Tools工程、不调用HALCON算子挂名。当前27项（OpenCV14/HALCON8/AI4/Common1），旧26项键/默认Blob/模板回调保留。
- 原NodeProperty上三个下拉：输出线段字段、测量输入、线段来源；按原KeepOptionSelection显示稳定NodeId。字段/槽写原State、来源仅原Connection，仍ConnectionService/PortValueTransfer/Card/Workspace/mapper/store/.flow。不是新执行、属性、文件或绑定框架。
- FirstLine消费只支持距离分支：本次复制参数及检测端点，不改原DTO。配方(1000,1000)→(1003,1004)对应5，实际卡尺160、旧搜索带约161；保存关闭旧会话重开仍是原配方，运行值不持久化。原结果表显示实际端点/距离/Pixel，结果图绘实际线段；手动距离单位未声明则明确未声明，不伪标像素。
- **长度口径**：FindLine端点是实际选中检测点的拟合投影最小/最大位置，当前测其端点距离，不是无限直线长度，也不是已验收的物体总长/厚度。卡尺采样范围、缺边与忽略点会影响投影跨度；合成图160不是现场计量精度/节拍承诺。
- 同帧复用InputImageIdentity，另以TaskInput或**直接图像来源NodeId+OutputPortId**捕获保守坐标锚点；目标显式图像来源必须与找线节点一致。两个裁剪X=0/5产生相同像素/hash/帧/尺寸仍拒绝，改为同一裁剪输出可执行；不同滤波/转换等坐标等价亦不推断。它不是通用坐标链、相机硬件身份或跨Crop映射；mm、缩放/镜像/非线性不开放。ToWorld/ToMachine标记CoordinateSpace后，不再作为像素Line输出。
- 缺失/失败/断线/删源/未选字段不当原点/0/配方；有限坐标/非退化/Pixel/同帧/同锚点检查。只有显式取消测量输入才恢复原配方。绑定求交两来源尚未开放，单槽求交明确拒绝；旧手动距离0及有效求交保留，未知操作/非有限点现显式校验失败。
- 数学无SDK不等于HALCON找线Native通过，AI/HALCON RequiresRuntime边界及全部物理/实图验收保留；本期仅R1b1，点/圆/布尔、公差、多槽与结果项身份仍待。

### 所有权与生命周期

| 对象 | 拥有/借用 | 结束/失效 |
|---|---|---|
| LineValue与本次参数值 | 原Card准备的纯托管快照 | 点为值类型、字符串不可变；不借用厂商对象/结果集合/图像像素 |
| 原GeometryParameters | 原Workspace/工具，原属性及完整编辑器借用 | 运行覆盖不修改它；关窗不Dispose会话 |
| 准备后的执行工具 | 原Card调度闭包创建并释放 | 不保留原工具或帧；原revision撤迟到、原会话等待实际工作 |
| 配置/连接 | 原节点State/原Connection | 只保存字段名/参数名/来源，不保存运行LineValue/结果 |
| 运行输入 | 原Node/原Card | 原画布连接变更和上游失效即清BoundNumeric/BoundLine/结果；准备再次解析来源 |

### 红绿与失败记录（不计入通过）

- vision-r1b-red.log实际执行首个目录/原属性回归先红：Common/Geometry尚不存在，后接通。
- 初次单元文件缺Xunit using、FindLineResult及卡尺枚举重名导致编译失败（vision-r1b-unit.log、-wpf-compile.log），只算编译失败，不算算法红证据；已显式using/限定类型。统一契约加Common后首轮494/493/1，旧精确计数22未同步；仅更新为23传统/公共条目，没有放宽有效性/结果断言，记录vision-r1b-logic-count-red.log。
- 卡尺组合首轮7/6/1（vision-r1b-calipers.log）：测试误把水平向右路径朝上法向的白→黑上沿设置Positive，真实0边缘失败；核实原LineSearchGeometry.Normal与既有极性测试后改用Negative。未改变算法、梯度阈值、端点/长度断言；Calipers与EdgeBand都完成真实组合/重开。
- 复核原画布直接断线：只Session.ClearResult会留Node.BoundLine旧值；增强原端口删除用例取得**vision-r1b-disconnect-red.log**（Assert.Null失败）。在原RefreshUpstream/UpstreamChanged同步撤临时数值/线段输入，不造失效总线；**-disconnect-green.log 16/16**（7线段+9标量），最终完整444再次通过。原已通过完整日志另保留-wpf-before-disconnect-fix.log。
- 未使用fake替代找线或数学，没有删Skip、放宽阈值、启动生产设备或安装Runtime。源码检查保留原数学与原图像处理边界，临时探针未加入生产。

### 实际命令与门禁

仓库根，SDK10.0.401/.NET9/C#13、xUnit v2/VSTest。各命令均追加
`-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`，串行执行，未升级依赖。

| 实际命令 | 字面摘要 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **Passed: 494, Failed: 0, Skipped: 0** | vision-r1b-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/r1b-diagnostics"` | **Passed: 444, Failed: 0, Skipped: 0** | vision-r1b-wpf-final.log |
| `dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'` | **Passed: 414, Failed: 0, Skipped: 0**，无退出未处理异常 | vision-r1b-floweditor-final.log |
| `dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid\|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'`（表中竖线转义，实际无反斜线） | **Passed: 66, Failed: 0, Skipped: 0** | vision-r1b-propertygrid-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **显式增量0警告/0错误** | vision-r1b-host-final.log |

检查退出码、非零测试数与日志Unhandled exception/Test host process crashed/宿主崩溃/No test matches；RequiresRuntime由原过滤排除，Skipped 0不等于SDK通过。依赖重编译旧nullable/xUnit警告保留，不将增量0警告冒称全仓干净。最后仅补求交绑定拒绝断言后逻辑494再跑；断线修复后完整444再跑。

### S/C/U/L与产物

- LineParameterTests **11实例**：实际无图/BGR的3-4-5/合法0/求交(5,5)/平行失败，独立运行快照及DTO/原工具释放、求交单槽拒绝，8种非法单位/帧/坐标/坐标缺失/NaN/Infinity/退化/参数名；显式字段和非像素/无边/失败结果拒绝。WorldCoordinateTests增加转换后不能冒称Pixel线段；Common.Geometry加入原统一23项合约（不是另起测试工程）。
- VisionLineBindingTests **7实例**：两种真实找线模式→距离、原下拉/原流程按钮、完整结果表及叠加、实际配方不变、原.flow关旧Session/新会话重开、旧帧/同像素异裁剪/缺失/失败/断线/删源、运行锁。新增Common同样跑原EveryCatalogTool完整参数面板，当前总444。
- `Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/r1b-acceptance/`：line-distance-band/calipers.flow、line-binding-band/calipers.png、measured-distance-band/calipers.png。实际查看卡尺端点距离160、Y=79.5、Pixel单位和绿色真实线段；原整页流程与6.几何连接来源显示正常。2×2/合成条带和STA截图不是物理鼠标、真实精度或现场节拍。
- 未验系统连线/OLE、暗色/DPI/跨屏全矩阵、实图重复精度/节拍、HALCON找线/AI Runtime/设备；R4定位跟随及其余R1不销项。

### 两项官方来源对照（2026-10-25）

直接HTTP200并读取对应原文，快照`%TEMP%/vision-r1b-reference/`；初次PowerShell非交互解析请求失败，改UseBasicParsing后两项成功，无绕过授权或密钥。

| 来源 | 对照与差距 | SHA256 |
|---|---|---|
| [OpenCV4.11 imgproc.hpp](https://raw.githubusercontent.com/opencv/opencv/4.11.0/modules/imgproc/include/opencv2/imgproc.hpp) L4387–4422 fitLine | 返回(vx,vy,x0,y0)是方向和线上点，不是线段两端。原工具已按真实检测点投影范围生成端点，本期消费这些端点；Welsch拟合、卡尺范围或丢点不等于工件端部定位/物理长度精度 | 9020D31168E318655B72DCE3A171057FD8E5A05568C25ADFE3B38C6DA49158C1 |
| [C# record参考](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record) | positional record class为init属性，with是浅拷贝；LineValue只有值类型点/enum与不可变字符串，不引用可变点集/SDK/图像，参数DTO另显式复制。原工具释放后真实快照仍执行；不能泛称任意record自动深拷贝 | BBB7DE9B990A82884D2A903BECC0E62EC99D2F52BB78866B7F0A529298A7C244 |

图SoftwareFramework索引仍2026-10-22；GeometryTool/MeasureTools/ResultCoordinates freshness missing，已读当前源码，不凭图缺失宣称无消费者。后续优先R1b2其余明确几何/结果项/单位，再布尔公差，继续小批真实S+C。


## V1.28实际（2026-10-25）：A2/R1a最小标量显式消费

### 已实施与边界

- GrayStatistics以INumericResult白名单提供Minimum/Maximum/Mean/Median；NumericValue区分Gray8/Gray16并携带本次输入身份。Threshold通过可选INumericParameterTool消费MinGray/MaxGray；未新增目录工具，仍26项，普通未绑定阈值主路径不变。
- 字段/参数写原节点State，来源只写原Connection；原NodeProperty/ConnectionService/PortValueTransfer/Card/mapper/store仍唯一执行/编辑/文件链。没有泛反射所有Result或新绑定/文件/属性引擎。
- 本次局部帧/参数快照覆盖目标，不写保存DTO。原配方99，实际下限0/80；结果AppliedMinGray/AppliedMaxGray显示在原结果表；保存、关旧Session、重开配方仍99。候选/缺失/断线/删源不能回退配方，只有显式取消消费参数才恢复原模式。
- 同输入用既有ImageIdentity（尺寸/stride/格式/FrameId/Timestamp/hash）核验，填充输入在压紧前核验身份；它不是新增几何坐标链。Gray16不能当Gray8，NaN/Infinity/缺失/旧帧拒绝；先验整数性，再checked转换，70.25不能截断为70。原Threshold.Validate仍校验上下限/区间。
- 每节点仅一输出字段/一消费参数槽；布尔/点线圆/结果项身份、世界单位/跨Crop几何、更多消费者/多数值槽仍待R1b。HALCON/AI/物理UI不销项。旧Session/Adapter默认空能力，null输入走原Prepare/Run；仓内兼容通过不等于仓外/ref struct或旧二进制理解新语义。

| 对象 | 拥有/借用 | 结束规则 |
|---|---|---|
| NumericValue/本次数值参数 | 原准备/Card，纯托管值 | 每次解析原连接；ClearResult/失效撤销，不保存运行值 |
| 原参数DTO | 原工具/Workspace，原属性行借用 | 配方唯一对象，不被绑定写入；不触发配方修改 |
| 执行帧/参数快照 | 原Card调度的独立闭包/工具 | 原revision丢弃迟到结果，实际工作结束后释放，不假取消 |
| 字段/目标/来源 | 原节点State/原Connection | 沿原.flow/.flows持久化；未识别新字段的实现严格拒绝 |

### 真实红绿与显示问题

- vision-a2-red.log首个原属性行回归先红（缺NumericOutputField），后接通。
- vision-a2-unit.log：旧普通Threshold不能直接处理填充输入，新测试按原契约显式压紧，绑定准备则验证原stride身份后复制；未放宽旧格式。
- vision-a2-focused.log：初版测试使用小写minGray而原Codec是MinGray，以及设置IsRunning触发原整图清结果后“旧帧”用例实际成为缺失；按实际契约修测试字段/顺序，保留精确原配方/旧帧拒绝断言，不改文件或清理语义。
- 截图复核发现来源可能SelectedValue是正确ID但SelectedItem/可见文本空白，单看执行成功会漏检。新增真实ComboBox SelectedItem与TextBlock文字断言，-selector-minimal/-probe/-visible-label.log实际失败。只改初始化顺序或单独SelectedItem不能完全解决；全局Text补偿会按重名猜错来源，-display-names.log实际捕获。
- 最终只在新数值来源行启用默认false的KeepOptionSelection：原行增加只读SelectedOption（由Value和当前候选计算，不保存第二选择）；原模板保留Value双向绑定/SelectedValuePath/资源样式，对启用行绑定选中项/显示文本并关闭按文本猜选。可通过列表/箭头选择，不能按重复显示名自动换ID；未启用旧行的交互及通知不变。最终断言同时检查重名中第二个ID、连接、SelectedValue、SelectedItem和实际渲染TextBlock，临时调试信息已移除。
- 并行门禁一轮1000节点编译耗时106ms>原100ms，413/414失败记录在vision-a2-floweditor-parallel-timing.log；不删测试/去Skip/放宽断言。串行无代码变化完整414连过两轮（-final/-repeat.log），不声称计时无波动。

### 实际门禁

仓库根，原.NET9/C#13/xUnit v2/VSTest；所有命令附加
`-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`。

| 实际命令 | 结果 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **Passed: 478, Failed: 0, Skipped: 0** | vision-a2-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/a2-diagnostics"` | **Passed: 436, Failed: 0, Skipped: 0** | vision-a2-wpf-final.log |
| `dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --no-build --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'`，两轮 | **Passed: 414, Failed: 0, Skipped: 0**，无退出异常 | vision-a2-floweditor-final.log、-repeat.log |
| `dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid\|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'`（表中竖线转义，实际无反斜线） | **Passed: 66, Failed: 0, Skipped: 0** | vision-a2-propertygrid-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **显式增量0警告/0错误** | vision-a2-host-final.log |

末次仅收窄未启用行额外通知后9个数值WPF/66属性再过；补MaxGray正向断言后478逻辑再过。RequiresRuntime排除，不以Skipped 0抵扣Native；旧nullable/xUnit重编译警告仍存在，不把增量0警告称全仓干净。

### 单工具、组合与产物

- NumericParameterTests 10实例：真实统计四字段/Gray8/16/0与缺失；阈值下限0、上限80快照、非法区间、不改DTO、帧与原工具释放后独立；NaN/Infinity/单位/帧/整数检查。
- VisionNumericBindingTests 9实例：原下拉/原容器运行按钮统计→阈值→Blob（0/80，4像素区域），配方99与结果上下限审计，关旧会话/重开，失效撤全链、缺失/断线/删源、相同像素不同FrameId拒绝、分数均值与Gray16单位拒绝、运行锁、同名/改名稳定ID及实际显示文字。
- PropertyGridMetadataTests新增1项：SelectedOption为稳定值投影，候选改名/缺失不改原值。普通旧行不启用保留选项元数据。
- 产物`Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/a2-acceptance/`内statistics-threshold-0/80.flow、parameter-binding-0/80.png、applied-threshold-0/80.png。已实际查看来源正确显示、配方仍99、真实下限80/上限255/4像素结果；2×2合成图放大不当实图精度证据。
- 未操作系统物理连线/鼠标/OLE、跨屏/DPI/暗色全矩阵、HALCON/AI Runtime/模型或真实设备，未改生产启动；R1b几何/布尔等不销项。

### 官方来源对照（2026-10-25）

搜索/全文读取/直接HTTP200，快照`%TEMP%/vision-a2-reference/`。不将WinUI/.NET11预览/C#15新行为套用当前环境。

| 来源 | 对照与差距 | SHA256 |
|---|---|---|
| [默认接口成员版本演进](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/interface-implementation/default-interface-methods-versions) | 合理默认能力让当前既有实现无需新增方法；本仓默认空能力/null走旧执行。仅仓内证明，不穷尽仓外特殊实现 | 70C86C2247A3ADE8392F0243D6C2001E7B64E45A95FA6DC6EB98C14010417769 |
| [C#数值转换](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions) | double→int向零取整，checked不替代整数性；本仓先有限/单位/帧/整数验证，再checked，70.25不变70 | 278375A370C9943F4C38FCB548D364F47D2926CECCD128722A2C04D2DF83F156 |
| [.NET9 WPF IsTextSearchEnabled](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.itemscontrol.istextsearchenabled?view=windowsdesktop-9.0) | 仅新来源行关闭按显示文本猜选，避免重名错源；文档不是空白根因证明，症状由真实控件/渲染断言复现并验证修复 | AB7F2E140036EF75F3A84DC3B32D2618F25D1204C56F3F0D50CCEDEBDB9F70CF |

图SoftwareFramework仍2026-10-22；相关/新文件freshness missing已按当前源码核查，不用索引缺失证明无消费者。下一步R1b先真实结果项/几何/单位坐标与消费者，不泛反射大量端口。


## V1.27实际（2026-10-24—25）：A1文件取图公共输出闭环

### 实施、兼容及所有权

- 新增Tools/OpenCv/Acquisition/ImageFileTool.cs，按单一ToolRegistration追加OpenCV/ImageFile；当前26项（OpenCV14/HALCON8/AI4），22个传统工具统一契约。未增加项目/包/执行器/文件引擎/属性框架，旧目录键、默认Blob、模板回调保持。
- 原OpenCvToolBase增加仅文件源覆盖的RunWithoutImage；原图像处理工具null输入仍失败。文件工具以BCL读取编码字节（中文路径），OpenCV4.11 ImDecode(Unchanged)解码单文件首帧，明确Mono8/Mono16/8位BGR/BGRA、紧凑行拷贝；不转灰度、不归一化、不按EXIF旋转。浮点和16位彩色明确失败。每次重新读文件，没有文件监视器或隐式缓存。
- PrepareRun沿原后台契约冻结路径参数，闭包独立拥有执行工具，不再访问原工具/DTO/无关输入。原源实例释放后已准备操作仍可完成；处置/迟到隔离由既有Workspace/Card revision与关闭等待负责，不作假取消。
- HALCON ImageFileResult新增IOutputImageResult，原ReadImage/GetImageSize/LoadedImage保留；原ToVisionImage复制Mono8/Mono16/字节RGB，不转调OpenCV。不先将无关任务彩色/Float32送旧Mono8算法桥，其他HALCON处理工具不扩大格式能力。
- 保留旧原生消费者行为：公共快照格式/图像元组不支持时，原生读取成功、尺寸与借用LoadedImage仍保留，Message明确公共输出不可用且OutputImage为空；原图像端口准备拒绝该空输出，不能当0、全黑或回退任务图。HALCON非完整矩形域同样不给公共快照，避免丢弃有效域/伪造域外已清零。其他SDK错误仍真实失败并清理候选。
- 原路径＋选文件按钮、原节点属性/Owned完整编辑器仍直接编辑原DTO，无视觉专属操作栏或新布局。自动化使用选文件委托，不自动操作系统文件对话框本体。
- 外部路径存原参数，绝对路径正常追溯；相对路径仍按进程工作目录解析，不自动打包/迁移模型或图像。大文件有编码缓冲＋解码Mat＋托管像素成本，未声称节拍或内存优化。

| 对象 | 创建/拥有者 | 借用者 | 释放/有效期 |
|---|---|---|---|
| 编码字节/解码Mat | 单次OpenCV执行 | 行拷贝边界 | 文件读取结束即无文件句柄；Mat在Run结束释放 |
| 公共VisionImage像素 | ImageFileResult/原Card | 下游节点、显示页 | 独立托管缓冲；结果Dispose撤引用，既有借用图像本身不被清零；下游按原失效链撤旧输入 |
| 原HALCON LoadedImage | 原ImageFileTool | 旧原生消费者，仅借用 | 下次实际RunCore或工具Dispose释放；公共输出不借其指针 |
| HALCON domain/HTuple/候选图像 | 本次原生读取/快照边界 | 无跨回调借用 | using/finally；失败不遗留候选 |

### 红绿与实际测试

- vision-a1-red.log：原目录找不到OpenCV/ImageFile，新端口源回归实际先红1项；接入后通过。
- vision-a1-halcon-input-red.log是**编译失败**（新增接口前对sealed旧结果做as转换），不能记成执行红证据；修后契约真实运行通过。没有用该编译失败冒称原生SDK运行过。
- ImageFileToolTests九实例真实OpenCV：PNG四格式/奇数宽度、Mono16 TIFF、中文空格路径、原生Mat/文件/工具结束后像素仍有效、输出不共享缓冲、冻结路径与原工具释放、文件重读/合法全黑、缺失/损坏/空路径、浮点/16位彩色拒绝、旧处理工具仍拒绝缺失输入。
- ImageFilePathContractTests新增一项：无关任务BGR不先转换，缺失路径应在工具自身文件检查处失败；仅错误路径，不触发HALCON Runtime。旧通知/JSON测试保留。
- VisionFileAcquisitionTests四实例：两后端目录/公共输出端口与空任务输入；两条**原容器当前流程执行按钮**驱动ImageFile→ImageConvert→Threshold→Blob（BGR及Mono16），9×9文件原像素、16像素Blob与Bounds(2,3,4,4)独立预期，显式借用相邻输出、原图不改；保存、释放旧Session、重开后无关Float32任务图不影响文件源，文件改全黑合法成功，改坏路径清整链/拒绝回退/停止下游。第四项模拟旧HALCON .flow没有公共输出端口，验证保留NodeId/FilePath并补当前能力端口，不改原mapper/格式。
- ImageFileOutputRuntimeTests新增五实例（Mono8/Mono16/RGB真HALCON单工具与跨后端算法组合、浮点旧契约、缩减域保护）。**仅编译，均RequiresRuntime/Skip保留，未执行。**不使用fake替代，不以OpenCV组合抵扣HALCON。
- 首张完整编辑器截图碰到原MaterialDesign展开动画中间帧，不能说按钮缺失或据此验收；留图前在原UI Dispatcher等待动画完成，重新生成并实际查看。此等待仅用于截图，不是算法性能断言，未改生产动画/样式。

### 最新实际门禁

仓库根执行，沿原.NET9/xUnit v2/VSTest配置，所有命令附加
`-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`，保留原恢复产物，不安装新依赖。

| 实际命令 | 实际结果 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **Passed: 468, Failed: 0, Skipped: 0** | vision-a1-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/a1-diagnostics"` | **Passed: 427, Failed: 0, Skipped: 0** | vision-a1-wpf-final.log |
| `dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'` | **Passed: 414, Failed: 0, Skipped: 0**，日志无未处理退出异常 | vision-a1-floweditor-final.log |
| `dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid\|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'`（竖线在表中转义，实际无反斜线） | **Passed: 65, Failed: 0, Skipped: 0** | vision-a1-propertygrid-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **显式增量0警告/0错误** | vision-a1-host-final.log |

RequiresRuntime被原过滤排除，不属于已执行集合的Skipped 0。历史nullable/xUnit依赖重编译警告仍存在，不把增量0警告说成全仓干净。没有修改生产启动/设备连接或运行MachineApp。

### 官方两来源对照与差距

直接读取HTTP200，快照`%TEMP%/vision-a1-reference/`；公开搜索Exa本轮429，未修改密钥/配置绕过，直接核查已知版本官方原文。

| 官方来源 | 依据/对照/未抵扣项 | SHA256 |
|---|---|---|
| [OpenCV4.11 imgcodecs.hpp](https://raw.githubusercontent.com/opencv/opencv/4.11.0/modules/imgcodecs/include/opencv2/imgcodecs.hpp) | Unchanged保留Alpha并忽略EXIF旋转；imdecode坏数据为空Mat；彩色BGR顺序。当前显式格式过滤/复制，不使用新版本摘要冒充4.11。PNG/TIFF已测，不代表所有编码器/多页/真实样本均验 | 8F9060934FD48D4608F184DDEECA44464E14936908C27DD3625D50683027B6E0 |
| [HALCON2311 read_image](https://www.mvtec.com/doc/halcon/2311/en/read_image.html) | 原生文件可含多图像元组/额外通道/Alpha及域；不能无条件当紧凑RGB。完整域检查和ToVisionImage只接明示支持类型；原生读取成功与公共快照可用分离以保旧消费者。真实ReadImage/域/文件/释放待授权，不靠文档抵扣 | 967B95CBA5508F794FBB03DC22AC1C92B7291B05CB5F5B9480231B61341511C0 |

另已查询Microsoft Marshal.Copy官方样例核对非托管→byte[]拷贝，未更换依赖/架构。

### 产物与A2起点

- `Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/a1-acceptance/`内Bgr24/Mono16 .flow、真实中文PNG、source/editor截图及legacy-halcon-file.flow；已实际查看Bgr24-editor.png与Mono16-source.png。全黑分支会重写测试输入文件，最终PNG不再是最初前景样本；截图/文件/像素断言的阶段不混淆。
- A1离线范围完成；HALCON正向与物理UI不销项。当前26项不表示全工具真实SDK验收。既有图像源端口暂保留通用兼容布局，文件算法忽略无关任务图；不增加视觉专属节点壳。
- 已进入A2/R1结构核查：原NodeModel.PortValues仅GetProperty查节点直接属性；原PortValueTransfer只做端口兼容和值传递，不能自然解析Result.Minimum或保证单位/同帧/缺失≠0。灰度统计Minimum/Maximum为int，Mean/Median为double；不能默认截断写入Threshold的int参数，也不能将绑定运行值直接污染保存DTO。
- 图索引SoftwareFramework为2026-10-22快照；coverage对上述三个现文件返回freshness missing，已按当前源文件逐一核对，不用图入度0/缺失证明没有消费者。A2数值/点线圆代码尚未实施，下一步先最小标量字段/合法消费者/配置与执行值隔离，再扩几何，不造平行绑定或文件引擎。


## V1.26实际：ImageFile同行选文件、逐工具S+C硬门禁与本轮组合补漏

### 修改与计划

- 根据最新四项反馈，将IMPLEMENTATION_PLAN更新为V1.26、TOOL_EXPANSION_PLAN更新为V2.1，新增TOOL_TEST_MATRIX逐工具台账。A0本轮反馈→A1文件取图公共输出（原R2子项前置）→A2/R1结果消费→A3/R2其余格式→R3–R7；原P0–P6/E1证据、十项终验/测量跟随/资产/模型路径等未完项继续保留。无需逐批确认，但不擅自安装Runtime、加载未授权模型、接硬件或破坏兼容。
- ImageFile.FilePath保留原名称/默认值/序列化形状/ToolIdentity；原ToolProperty增加显式FileDialogFilter，适配原NodeProperty/PropertyItem/Selector。原节点与完整元数据面板复用同一个小型FilePathEditor：路径旁FolderOpenOutline按钮，CompactTextBox/CompactFlatButton及主题动态资源，无新主操作栏/属性框架/文件引擎。普通字符串仍使用原模板。
- 默认Microsoft.Win32.OpenFileDialog，Owner来自当前窗口，检查文件存在、单选；确认才以SetCurrentValue回写原绑定，取消/异常/只读/运行/卸载/重绑不写路径。注入PickFile只替换交互，不替换文件解码或算法。错误在原行显示，不清旧路径。
- ImageFileParameters增加精确PropertyChanged通知，使节点和借用完整页双向刷新，重复赋同路径不通知；JSON仍只有FilePath。回归发现TabItem禁用不保证所呈现参数内容禁用，已将IsEditingEnabled同时绑定原ParametersPanel，只锁参数修改、不禁用图像浏览或另造执行命令。
- 新增ImageFilePathEditorTests三项，驱动实际按钮；覆盖双入口/同DTO、取消不脏、成功标脏、A/B、运行锁、异常可恢复、迟到不跨目标、.flow恢复以及截图。选择真实中文空格PNG路径后借用**原WorkbenchComposition的LoadImageByPath**，真实像素断言后走原任务输入→OpenCV Threshold→Blob，检查像素数20、Bounds(7,6,5,4)、原图未改。**此链不包含HALCON ImageFile正向执行，不伪造其输出。**
- 新增ImageFilePathContractTests两项：原DTO通知/JSON/过滤器及真实工具缺失文件失败路径。新增共享属性元数据一项：显式启用路径编辑，不改变普通字符串，仍写原值。
- VisionToolCombinationTests新增两项真实Threshold→EdgeBand找线、Threshold→Hough找圆；独立合成几何预期，显式上游掩膜、原图不改、保存、释放旧会话、重开重跑、源参数失效清下游及失败停止。**不抵扣卡尺主链跟随、跨裁剪坐标或实图精度。**

### 先红、真实失败与收尾

- vision-filepicker-red.log：新按钮回归先红（未找到FilePathEditor）；补接入后vision-filepicker-sync-red.log捕获完整页仍显示旧空路径；增加原DTO精确通知后覆盖该症状。
- 第一轮运行锁断言发现完整页参数仍可点，保留“运行中不调用picker/路径不变”的断言；在原ParametersPanel锁修改后通过。没有降低断言或去Skip。
- FlowEditor前两轮414项断言虽全过且CLI退出0，但日志存在Unhandled exception（WeakEventTable退出时跨STA移除Loaded订阅），不能算干净通过。过滤去PNG有一次未复现，但仅15项容器UI仍复现，排除“仅PNG线程”假设。尝试ProcessExit收尾过晚，引发已取消调度Task；该临时代码已移除。
- 根因范围落在无生产主窗口的共享测试STA退场：WpfHostCollection将所有五个WpfTestHost消费者归同一xUnit集合，在Fixture.Dispose内正常BeginInvokeShutdown并Join，异常/超时直接失败，不吞异常、不停止用户进程、不代替生产清理。修后完整414连跑三轮，日志均无Unhandled exception；此问题未宣称由新增文件模板引起，也未改生产FlowEditor生命周期。
- 门禁需检查日志异常，而不只退出码/总数；复跑使用日志Unhandled exception检查，命中则命令失败。完整错误记录：vision-filepicker-floweditor-recheck.log、vision-filepicker-floweditor-ui-only.log；失败尝试记录vision-filepicker-sta-green.log保留，不计为通过。

### 最新实际门禁

工作目录为仓库根；均使用既有恢复产物、原.NET9/xUnit v2/VSTest模式。所有命令附加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`。

| 范围/实际命令 | 结果 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **Passed: 453, Failed: 0, Skipped: 0** | vision-filepicker-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/filepicker-diagnostics"` | **Passed: 422, Failed: 0, Skipped: 0** | vision-filepicker-wpf-final.log |
| `dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'`；随后同命令加`--no-build`两轮 | **Passed: 414, Failed: 0, Skipped: 0**，三轮均无退出异常 | vision-filepicker-floweditor-final.log、-repeat1.log、-repeat2.log |
| `dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid\|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'`（表中\|为Markdown转义，实际过滤符号为竖线） | **Passed: 65, Failed: 0, Skipped: 0** | vision-filepicker-propertygrid-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **0警告/0错误，显式增量** | vision-filepicker-host-final.log |

注意：依赖重编译中原nullable/xUnit警告仍存在；不能用宿主增量0警告说全仓干净。被RequiresRuntime过滤掉的测试不属于上表“Skipped 0”所指已执行集合，不代表授权测试通过。

### 产物与未验收

- 已实际读取查看 `Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/image-file-acceptance/image-file-node.png` 与 `image-file-editor.png`，按钮与输入同行，保持原主题/紧凑风格；同目录有image-file-path.flow、实际 文件.png。测量组合文档在combination-acceptance/threshold-line.flow、threshold-circle.flow。
- 本轮没有打开/自动操作系统文件对话框本体（自动测试注入选文件结果）；没有物理鼠标/OLE、暗色/DPI/跨屏验收。默认系统对话框代码完成，不将STA按钮路由测试冒充人工系统对话框验收。
- 未执行HALCON ReadImage正向、三家AI真实模型、真实训练/推理/设备；未安装依赖/Runtime、去Skip或启动生产宿主。ImageFileResult尚无IOutputImageResult，正向S+C不销项；下一批A1负责公共输出，而不是按本轮PNG宿主测试标其完成。
- 当前仍25项；本轮没有注册新算法。453/422是最新，V1.25的451/417保留为历史。原十项终验整体仍未全部完成。

### 执行后两来源官方对照（2026-10-24 22:57 +08:00）

均已官方搜索/全文读取/直接HTTP200；快照 `%TEMP%/vision-filepicker-reference/`。

| 来源 | 对照与差距 | SHA256 |
|---|---|---|
| [.NET9 CommonDialog.ShowDialog](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.commondialog.showdialog?view=windowsdesktop-9.0) | 显式Owner及确认返回true；当前控件使用所属窗口，取消不回写。系统对话框物理交互仍待验 | 2FC874009524C7DD0668B44246BDDC71A584C3EC86671BCB3F6C208832843D9F |
| [WPF依赖属性优先级/SetCurrentValue](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/properties/dependency-property-value-precedence) | SetCurrentValue不替换既有Binding；连续双入口回写与重开有测试。额外目标/运行守卫属于本仓生命周期规则，不从文档推导Native SDK已可用 | 0C2CD9188E80B5A754693925D820843F625C2D6D859A9FF87EF1CA6385B59D47 |


## V1.25实际：复用FlowNodes同款原生FlowEditor页面，不自建操作栏

### 已实施

- 对照FlowNodes.Workbench/App.xaml.cs、ViewModels/MainWindowViewModel.cs及FlowEditor.Workbench/App.xaml.cs，确认复用点是Controls的FlowChartContainerView。VisionFlowView.xaml现只内嵌该原容器，完整复用FlowCommandBarView/工具箱/标签页/属性/输出。无复制命令栏、无新Vision.FlowEditor/执行/文件框架或Workbench.exe引用；兼容旧VisionFlowView路由和程序化命令，但主页面只用原生操作栏。
- 原容器通过SetToolboxItemsAsync接收原25项视觉目录投影；不为不同后端另造添加容器。新建页仍由原IFlowTabFactory提供，VisionFlowVM只协调各页VisionToolNode所有权、输入/参数失效、原Workspace等待释放和编辑窗口。原生按钮与原容器/原Chart命令同对象，有STA断言；多页像素输入与结果独立，当前页切换正确。
- 来源/定位/索引/基准从视觉顶部栏迁入NodeProperty元数据；图像加载为节点属性扩展动作，委托到该节点所属任务页（空页先加节点；原零节点输入程序化保存能力保留）。原NodeModel仍为PropertyGrid目标，原DTO/连接/FixtureIndex/ReferenceFixture为唯一配置。下拉值是NodeId而非显示名；非法/缺失来源保旧、运行锁拒绝修改，非ROI定位能力不显示定位字段。
- 动态显隐复用原行/分组/面板VM；ToolProperty.IsVisibleProperty适配原NodeProperty机制，找线模式切换只改原DTO。旧string候选保留，新增EnumOption候选的值/显示名分离。搜索隐藏行仍刷新，精确通知不增加无关属性的UI派发。
- 完整工具窗口仍为原VisionToolEditorView，借用原Session/Card/DTO/ROI/结果；A/B/任务页切换不误绑，重复打开激活、卸载不释放工具。换DataContext关闭旧借用窗口但不Dispose旧任务；同任务重载复用原容器，AutoWire多余空容器释放路由。主壳保留主题入口，窄屏/分隔条按原容器，不再维护视觉ToolboxPanel专属布局。
- 接通原.flows后，回归先捕获“后续页损坏导致旧页已被移除”（预期2页实际1页）。修复原FlowChartContainer文档链：所有候选成功后提交，失败异步清理候选；恢复DocumentProperties，原流程组保存/打开不绕过编辑/运行锁，关闭等待组命令。仍原格式/mapper/store，不把通用层写成视觉专用执行器。旧同步CreateTabFromFlow入口保留，原生组打开用具备候选清理的异步入口。

### 红绿、门禁与产物

- 新来源/模式测试先红2项（vision-r0b-red.log）；早期显示候选使用C#单元素只读集合时，WPF未应用SelectedValuePath期间以string查询IList.IndexOf，触发InvalidCastException/XamlParseException，中止运行（vision-r0b-focused.log），不计为通过。共享选项适配转成List后恢复，并追加无类型IndexOf回归。
- 共享属性门禁最初61/62：新显隐逻辑把同对象所有属性通知都派发，破坏原“只派发相关字段”守卫。按值字段/候选字段/可见性谓词精确筛选后保留原Assert.Single，最终64/64；未改断言求绿。
- 新原生容器多页/命令身份/独立输入/原生全部运行/坏组保旧/有效组重开有真实OpenCV回归。vision-native-group-red.log实际因2页变1页失败，修复后vision-native-group-green.log通过。P2/P3仍驱动原属性机制生成的真实ComboBox/原动作Button，不仅检查VM；原像素、定位、模型、保存关闭等断言保留。
- 最新完整门禁：
  - **Passed: 451, Failed: 0, Skipped: 0**，vision-native-logic-final.log。
  - **Passed: 417, Failed: 0, Skipped: 0**，vision-native-wpf-final.log。
  - **Passed: 414, Failed: 0, Skipped: 0**，vision-native-floweditor-final.log。
  - **Passed: 64, Failed: 0, Skipped: 0**，vision-native-propertygrid-final.log（PropertyGrid与Layering定向，不声称共享库全量）。
  - Vision.Workbench显式增量构建0警告/0错误，vision-native-host-final.log；不代表全仓干净重编译无警告。
  - 额外仅编译FlowNodes.Workbench，0错误、139警告，vision-native-flownodes-build.log；未启动该宿主、未连接设备，不将构建算作其SDK验收。
- 产物仍Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/r0-acceptance/：task-properties.png已查看，显示原生操作栏/标签页/左工具箱/右原属性/输出区；原blob-editor.png保留。invalid.flows测试路径先写坏组验证失败，再覆盖有效组验证重开；不把最终该文件称作保留的坏样本。
- 未新增包/项目或运行库，未暂存、未清理累计用户修改。图像/定位/模式R0代码项已落地；主题DPI、物理鼠标/OLE/跨屏和授权SDK/实图节拍仍未验收。R1数值几何消费与HALCON扩充未开始。

本轮命令沿用上轮完整命令，仅日志改vision-native-*；WPF诊断目录为Tests/obj/native-diagnostics，仍Category!=RequiresRuntime。共享属性的原PropertyGrid|Layering过滤不变，现在含新增2项；FlowNodes额外命令：

```powershell
dotnet build "Software Frame/FlowNodes/FlowNodes.Workbench/FlowNodes.Workbench.csproj" --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
```

### 对照核查与取舍（2026-10-24 21:50 +08:00）

主要门禁后再次读取两页Microsoft官方文档，搜索/完整读取及直接HTTP均成功，快照%TEMP%/vision-native-reference/：
- [Selector.SelectedValuePath（.NET9）](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.primitives.selector.selectedvaluepath?view=windowsdesktop-9.0)，HTTP200，SHA256 `396FDE6060B4E0A226B68104A476488864743DF6FBEDFB20DBC81FE1BEC477AE`：显示项与所选值不同，本轮使用NodeId绑定；改名只刷新显示，不能改连接身份。
- [WPF Property Change Notification](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/how-to-implement-property-change-notification)，HTTP200，SHA256 `1F252EA148CF02EA7118A93D7C2DBBFD642A3EB08C6189991EE68BE2E35B2F07`：动态候选/显隐/依赖字段需对应通知；本轮不以全量重建控件代替原行刷新，精确派发守卫保留。
- 仓内FlowNodes.Workbench与FlowEditor.Workbench的真实装配作为直接复用依据；其后额外编译FlowNodes工作台通过，不以外部示例重造视觉操作栏。文档/API验证与STA截图不代表竞品实机或现场验收。


## V1.24实际执行：R0首批界面分层（整阶段未销项）

### 已交付／保持不变

- 主任务右侧直接复用原PropertyGridHost/PropertyGridHostViewModel，TargetObject为SelectedNode.Model；未创建VisionNodePropertyView或第二套属性控件。VisionNodePropertyGridBuilder委托原生成器，仅追加ToolEntry.NodeParameterNames显式白名单中的原DTO属性行；ToolIdentity与本次状态只读。大多数2–4项，原本只有一个标量的工具保留一项，NPointsCalibration表格仍在完整编辑器，不凑数。
- 原INodeEditorLauncher/Handler贯通属性按钮、节点动作/右键与双击，节点请求由所属宿主处理，不按全局选中节点开窗。Workbench拥有每节点一个Owned非模态窗口，复用原VisionToolEditorView并隐藏单工具文件操作；重复打开激活、A/B不重绑、关窗不Dispose会话、删节点/任务关闭先收窗再走原等待释放链。窗口关闭错误进入原可观察聚合清理错误，不阻断其余节点释放。
- 参数直接写原DTO、清旧结果并标任务修改；完整页反向刷新关键行，包括搜索隐藏再显示后的编辑。Blob专用适配刷新不重建工具/Card/DTO。ROI变化比较真实几何，单纯切换ROI/节点或运行不误标脏。关键字段/ROI保存原.flow→关闭旧会话→新会话重开，结果不恢复。旧.flow/.visionproj、像素/模型/生命周期业务断言保留，只迁移旧内嵌布局的访问位置。
- Shared PropertyGridHost增加默认true的IsEditingEnabled：只禁属性/端口编辑，保留搜索、滚动和扩展动作；无其他宿主迁移要求。主任务保留原主题插槽。未改算法执行或引入项目、包、文件/执行框架，目录仍25。

### 实际门禁与红绿

- SDK10.0.401，xUnit2/VSTest。当前根/上级AGENTS.md和run-tests覆盖文件均不存在；WPFCustomControls/AGENTS.md已读。无Runtime安装、未加载授权模型/接硬件。工作区累计修改与未跟踪文件保留，未暂存。
- 先将原工具箱布局测试改为要求现成PropertyGridHost：旧实现实际失败1项（%TEMP%/vision-r0-layout-red.log，值为null）；改生产布局后工具箱2项通过。
- 新VisionFlowR0Tests共4项：25目录显式属性白名单且未运行SDK、路由双击同启动器、同会话/0额外工厂/窗口开关/运行只锁修改/删节点、原DTO双向刷新/标脏/ROI/保存重开。与原P1/P3/P6定向共35项通过（vision-r0-focused.log）。
- 首轮全量WPF 412通过/1失败：新增删除测试实际仍选A，却要求删除B后面板为空；明确先选B再删除，保留“已删目标必须清空”断言，不放宽为任意状态。随后全量 **Passed: 413, Failed: 0, Skipped: 0**（vision-r0-wpf-final.log）。之后仅加强上述原测试的保存/关旧会话/新会话重开断言，4项再次通过（vision-r0-roundtrip-final.log），未再改生产代码。
- 逻辑 **Passed: 451, Failed: 0, Skipped: 0**（vision-r0-logic-full.log）；通用FlowEditor **Passed: 414, Failed: 0, Skipped: 0**（vision-r0-floweditor-full.log）；共享PropertyGrid/Layering定向 **Passed: 62, Failed: 0, Skipped: 0**（vision-r0-propertygrid-verified.log）。第一次为共享控件指定新artifacts目录并--no-restore时退出0但没有发现/执行测试，不计证据；改用已恢复的menu-grid-verification目录后实际执行62项。
- 显式宿主增量构建0警告/0错误（vision-r0-host-final.log）。此前依赖/测试重编译仍输出既有nullable/xUnit警告，不把增量结果冒充全仓无警告。
- 已查看STA生成的task-properties.png与blob-editor.png，确认左工具箱/中画布/右现成属性和独立原编辑器。产物：Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/r0-acceptance/，含quick-parameters.flow。空图窗口截图不冒充有实图执行效果。

实际命令（仓库根；各命令末尾统一附加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`）：

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/r0-diagnostics"
dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal'
dotnet test "Software Frame/WPFCustomControls/Tests/WPFCustomControls.Tests/WPFCustomControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFCustomControls/Tests/obj/menu-grid-verification" --filter 'FullyQualifiedName~PropertyGrid|FullyQualifiedName~LayeringTests' --logger 'console;verbosity=normal'
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"
```

### 差距／后续仍为R0

- 来源/定位/索引/基准仍在明确标注的顶部“选中节点配置”，没有纳入原节点属性元数据；未复制到完整窗口，也不让A窗调用全局SelectedNode命令。后续只允许沿原属性机制适配，不能新造节点概要界面。
- 模式相关关键字段显隐、更多DataContext重载/主题/窄屏/DPI、物理鼠标/OLE/跨屏仍需收尾；本轮路由鼠标事件与STA截图不等于物理鼠标验收。R0未整阶段完成，R1–R7未开始；HALCON格式/公共输出、测量跟随、数值几何消费及原十项残项不销项。
- 没有授权SDK、模型/实图和现场节拍证据；假Provider/文档/合成测试不能替代。

### 执行后官方核查（2026-10-24 19:58 +08:00）

两页Microsoft官方文档已搜索并完整读取，随后直接HTTP读取均200；快照%TEMP%/vision-r0-reference/：
1. [WPF Window.Owner（.NET9）](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.owner?view=windowsdesktop-9.0)，SHA256 `17A424E02CE22461C385BA95CD42A43A82B7121A310B3F1D4EBA9BF58EA7DA8D`。Show不是模态；需显式Owner，Owner关闭会关闭Owned窗，并可能不触发子窗Closing。本轮卸载清理由Closed/Unloaded及任务显式收窗负责，不在子窗Closing释放借用会话。
2. [WPF Specify Binding Source](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/how-to-specify-the-binding-source)，SHA256 `6A2F27B088593A14BF4E822843DFCD5D7F8E224C56F60625A3CA214B7EE3E75E`。默认DataContext继承会在上下文更换时重算；本轮窗口/编辑Session绑定打开时节点的原对象，标题显式Source=node，不从当前选中节点重新取会话。

取舍：先用原Window/属性控件和显式借用对象，不引入Docking/第二套属性或执行框架；文档证明API语义，不代表竞品实机或授权SDK验收。


## 最新规划说明：现成节点属性＋独立完整编辑器＋双后端（V1.23，未实施）

用户确认节点属性直接使用FlowEditor.Controls现成界面，不新增Vision节点概要控件。本轮仅核查现有属性选择/启动器、HALCON格式桥与ImageFile结果，并制定IMPLEMENTATION_PLAN的R0–R7、TOOL_EXPANSION_PLAN V2.0双后端矩阵及完整编辑器所有权/关闭规则；下一步先R0界面分层，再结果消费/格式桥和双后端工具。

本次无运行代码/工具注册变更，未运行测试或构建、未执行SDK/硬件。451/409和0警告/0错误仍是下方V1.22的实际历史门禁，不是新窗口/节点属性或新HALCON工具验收。19:05 +08:00已直接读取HALCON2311 gray_histo/dyn_threshold/measure_pairs与VisionPro10 2D清单，均HTTP200；快照、差异与SHA256见新计划。SDK授权、物理鼠标/跨屏、原十项残项继续保留。


## 最新：E1第二批灰度变换/自适应阈值/颜色分割（计划V1.22）

### 实际范围

- 新增IntensityTransform、AdaptiveThreshold、ColorSegment；原22条目不变，现25。复用原ToolRegistration、后台快照、Card/参数/结果、VisionToolNode及通用.flow。无项目/包新增，无通用FlowEditor生产改动。
- 灰度变换仅Mono8：线性含负增益，Gamma指数定义为255×(in/255)^Gamma（>1变暗），显式按本帧min/max归一化；常量图保持原值，半值向上/饱和，不是显示亮度设置。图像转换的固定16位映射契约不变。
- 自适应阈值仅Mono8：Mean/Gaussian，3~255奇数邻域，整数C，正/反向二值；边界复制、正常严格大于/反向小于等于，输出0/255与像素数/占比，无Otsu。
- 颜色分割支持Rgb24/Bgr24/Bgra32，显式RGB/HSV范围（R/H、G/S、B/V），忽略Alpha、不合成背景；HSV为OpenCV8位H=0~179，仅H允许跨179→0环绕，RGB禁止反向区间。默认S/V下限排除白/灰与过暗红；不把内部HSV缓冲作为公共BGR图输出。
- 三工具均为全图处理，保持尺寸、像素坐标、帧号/时刻；不声明ISearchRegionTool，画布上的ROI标注不意味着算法限域。后台准备复制原帧和全部参数；转换/阈值使用原适配，ColorSegment的RGB/HSV Mat仅在单次调用using内拥有/释放。未引入新取消线程或SDK跨调用句柄。

### 独立像素与组合验证

- SegmentationToolsTests新增13项：线性舍入/饱和/负增益、Gamma2与0.5、显式归一化/常量图、两种局部阈值/反向/小图边界/整数C、HSV色相两端/RGB顺序/低饱和与暗色/Alpha/stride、范围校验、损坏/不支持输入、原工具释放后后台快照仍有效。原统一契约增加3工具×5项，完整元数据/参数JSON契约覆盖同步，原工具断言保留。
- 复用VisionBasicToolsE1Tests夹具、保存及截图方法，只新增两条组合用例：Gamma2→Gaussian局部阈值→Blob/统计为1像素，颜色分割10像素→Morphology.Open→Blob/统计为9像素；统计分别显式读取二值/形态学输出，后者120像素、均值19.125。前景数/面积/均值来自独立输入构造，不从当前结果反算。
- 两条均保存→关闭原会话→新会话加载→重跑；Gamma/颜色空间/环绕范围/局部模式/邻域/C恢复，打开不恢复运行结果。颜色分割改成无命中的合法范围，所有下游继续成功且统计均值0；再设无效RGB反向范围，或将局部邻域改偶数，旧掩膜/下游输入/结果表撤下，流程失败后不会复用旧结果。
- 已查看本轮e1-acceptance/e1b-color.png；同目录有e1b-adaptive.png及两份.flow，根为Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug。窗口为真实离线STA合成场景，不是物理鼠标/OLE、系统对话框或实际跨屏DPI验收。

### 实际门禁与红绿证据

| 范围 | Passed | Failed | Skipped | 日志（%TEMP%） |
|---|---:|---:|---:|---|
| 逻辑定向：新工具/统一契约/元数据 | 123 | 0 | 0 | vision-e1b-logic-green.log |
| WPF定向：E1/目录/工具箱/所有工具参数面板 | 35 | 0 | 0 | vision-e1b-workbench-focused.log |
| 非Runtime逻辑全量 | 451 | 0 | 0 | vision-e1b-logic-final.log |
| 非Runtime WPF全量 | 409 | 0 | 0 | vision-e1b-wpf-final.log |

- 最终显式dotnet build Vision.Workbench.csproj退出0、增量构建0警告/0错误，vision-e1b-host-final.log；重编译中既有nullable/xUnit警告不由此抵扣，不称全仓重编译零警告。
- 命令与前轮保持现有VSTest/xUnit v2两个工程入口、--no-restore、--artifacts-path Software Frame/Vision/Tests/obj/editor-verification、--filter 'Category!=RequiresRuntime'、--logger 'console;verbosity=normal'、-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false。WPF另加--blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory Software Frame/Vision/Tests/obj/e1b-diagnostics。无通用FlowEditor生产改动，其全量本轮未重跑。
- 目录先更新精确键/25项预期，vision-e1b-catalog-before.log的2项因工具尚不存在而失败。新逻辑首轮119/123，4失败揭示HSV三通道误用GetArray<byte>导致8像素只按单通道元素数分配/读取；改为逐行复制完整H/S/V字节，保留原色相/通道/快照断言，最终123通过。失败日志vision-e1b-logic-focused.log保留，没有删Skip或放宽像素预期。

### 执行后官方对照

2026-10-24 18:30–18:32 +08:00在全量/构建后重新读取以下来源，快照%TEMP%/vision-e1b-reference/：

| 来源 | 核实事实与本轮取舍 |
|---|---|
| [Aurora Image Processing](https://docs.adaptive-vision.com/current/studio/machine_vision_guide/ImageProcessing.html)，HTTP200 | 支持HSV分离色度/亮度、预处理与显示分离，并警告二值化后不适合保留亚像素测边信息。本轮处理输出与原像素分离，颜色掩膜仅供分割/Blob；不把二值化用于计量精度承诺，也不声称预处理搜索ROI已实现。 |
| [MERLIC 26.10 Transform Color Space](https://www.mvtec.com/doc/merlic/26.10/manual/en-us/Content/Tool_reference/Preprocessing/Color_transformation/transform_color_space.html)，HTTP200 | 提供HSI/HSV等及Hue Shift，说明红色色域在色相范围两端。本轮采用环绕区间而非移动色相刻度，明确本后端H=0~179；MERLIC Hue图0~255及独立通道图不能直接照搬或称数值相同。未实现其多种颜色空间/通道预览。 |
| [OpenCV 4.11 thresh.cpp](https://github.com/opencv/opencv/blob/4.11.0/modules/imgproc/src/thresh.cpp)、[color_hsv.simd.hpp](https://github.com/opencv/opencv/blob/4.11.0/modules/imgproc/src/color_hsv.simd.hpp)，原始源码HTTP200 | 对照当前后端版本的复制边界、均值/高斯、阈值比较及180档H路径；本轮调用原API而非复制实现。4.11及4.x对应文档页面均HTTP403，明确未读取；搜索给出的4.13/5.0摘要不作已读取当前版本依据。 |

对应0.html、detail-0.txt、detail-1.txt、detail-2.txt的SHA256依次：ACAB092DE67244650F216035B5B419EF90DF1B370BEF79640AD73D8B5DF0598E、B0B51C1D7BFA407D6506BC1615A94994F88726A520C4252AD88E1AFEEC432C5C、334504BAF244C78BF707C2384FFA8406EDB65FF321955A08B473BB021B6455C1、BF819900EA530F6D04CDFF0147EB3184EDAA0C63BABFBCD50B36D8319D7567F7。额外VisionPro 2D清单重核HTTP200只作目录参照，不用它证明本轮颜色/阈值数值等价。

E1六个首期工具已交付不等于原十项或整个E0–E4完成；下一步先补最小数值/几何消费与FindLine/FindCircle搜索域，再推进E2测量/公差。Otsu、预处理搜索ROI、图形直方图、实图精度/节拍与授权SDK/硬件、物理交互仍待验收。


## 最新：E1首批转换/滤波/灰度统计（计划V1.21）

### 范围与结果

- 新增3个真正可执行工具：ImageConvert（固定范围16→8、RGB/BGR/BGRA亮度/通道）、ImageFilter（Mono8 Gaussian/Median/Mean及显式边界）、GrayStatistics（Mono8/16原ROI统计与分档直方图），原19项全部保留，现22项。Mono8/Bgr24等常量/脉冲、步长/尾部字节、Alpha忽略、原始灰度/总体标准差与中位数有独立预期；不暗转Float32、不改输入。
- 仍由原节点拥有Workspace/Card及结果，编辑器/节点借用呈现；3工具PrepareRun在UI准备时复制帧/参数，统计域借用已不可变的SearchRegion，原工具释放后快照可独立执行。滤波Mat仅在单次执行using内拥有/释放，没有跨调用SDK句柄或新取消线程。
- 新BasicImageToolsTests共18项；沿用统一契约增加3工具×5项（15项），元数据守卫同步包含新DTO。新VisionBasicToolsE1Tests共4项：Bgr24/Mono16两条真实处理链及保存→关闭原会话→重开、失效停止；数值表/空域失效；实际模板匹配→统计搜索ROI平移，原ROI不改。目录/工具箱/默认Blob守卫按22及确切新增键更新，未删除旧工具覆盖。
- 处理链中10个亮像素经Median去噪/修角后5个，Threshold前景5、Blob面积5；统计明确读取滤波图，81像素、均值640÷81、0档76像素/128档5像素。档宽64在.flow重开仍为64，但原始均值/中位数不变。
- 统计结果页为6行只读数值表和最多995个非零直方图档文本；完整分档计数在本次结果Histogram中。未新增图形直方图、数值消费者端口、公差工具或测量跟随，不能将其宣传成完成全部扩充计划。

### 实际门禁与失败证据

| 范围 | Passed | Failed | Skipped | 日志（%TEMP%） |
|---|---:|---:|---:|---|
| 非Runtime逻辑全量 | 423 | 0 | 0 | vision-e1-logic-final.log |
| 非Runtime WPF全量 | 404 | 0 | 0 | vision-e1-wpf-final.log |
| 属性面板+E1定向（全量前） | 26 | 0 | 0 | vision-e1-editor-green.log |

- VSTest/xUnit v2，现有Vision.Tests.csproj和Vision.Workbench.Tests.csproj；--no-restore、--artifacts-path Software Frame/Vision/Tests/obj/editor-verification、--filter 'Category!=RequiresRuntime'、--logger 'console;verbosity=normal'、-m:1、-p:BuildInParallel=false、-p:UseSharedCompilation=false、-nr:false。WPF另用--blame-hang-timeout 90s --blame-hang-dump-type mini，结果目录Tests/obj/e1-diagnostics。
- 显式dotnet build Vision.Workbench.csproj，沿用上述输出/串行参数，退出0、0警告/0错误，vision-e1-host-final.log。为增量构建，首次重编译仍出现既有nullable/xUnit警告，不宣称全仓干净重编译无警告。未改通用FlowEditor，本轮未重跑其全量。
- vision-e1-catalog-before.log：先新增精确目录预期，2项因3工具不存在失败；实施后旧19/15数量守卫按新增事实调整，保留确切目录键与类型校验。逻辑首轮421/422仅剩契约表数量仍15，改成实际18后全量通过。
- WPF首轮403/404发现统计工具参数面板为空，不通过放宽NotEmpty求绿；增加有实际用途的直方图档宽参数，Mono16可合理分档，且新增回归证明分档不量化均值/中位数/标准差，后台快照与.flow恢复不借用新参数。属性面板定向26及最终全量404通过，原守卫不减。
- 已查看e1-acceptance/Mono16-statistics.png（同目录还有Bgr24-statistics.png、两份.flow），根在Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug。真实离线WPF/算法图，不是竞品、物理鼠标、系统文件对话框或跨屏DPI验收。会话关闭测试为DisposeAsync；原P6真实宿主Closing回归在全量中继续保留，不把本例的局部截图窗口关闭冒充主宿主验收。

### 执行后官方对照

2026-10-24 17:53 +08:00在最终测试/构建后重新访问两来源，均HTTP200，快照%TEMP%/vision-e1-reference/：

| 来源 | 核实与本实现取舍 |
|---|---|
| [Aurora Image Processing](https://docs.adaptive-vision.com/current/studio/machine_vision_guide/ImageProcessing.html) | 包含Gaussian/Median/Mean、ROI/边界及避免在亚像素测边前二值化的说明。采纳预处理与显示分离/明确边界；本轮OpenCV采用显式Replicate/Reflect101/补零，不声称与Aurora裁短邻域边界逐像素相同。ROI限域仅统计，本轮滤波/转换不假装应用搜索ROI。 |
| [VisionPro 10 Histogram Tool](https://docs.cognex.com/vpromx_1000/web/EN/visionpro/Content/Topics/users-guide/control-reference/histogram/control-reference-histogram.htm) | 提供均值/中位数/标准差等与图形直方图。本轮实现原像素统计、数值表/完整计数，明确总体标准差口径；未实现其图形编辑器/图形直方图，不声称评分或API等价。 |

SHA256依次ACAB092DE67244650F216035B5B419EF90DF1B370BEF79640AD73D8B5DF0598E、71A759B26EC9431B5945CF8234B711DBF2D80549E7457D16FB5D0CBD40A69CCE。下一步T03/T04/T06；E0数值/几何消费、FindLine/FindCircle跟随、其余工具、实图/性能目标与授权/物理验收继续待办，不自动销项。


## 最新：纠正为左侧工具箱（计划V1.19）

- 按用户纠正，仅调整VisionFlowView.xaml列顺序为左侧工具箱→流程画布→图像/参数编辑器，展开方向改Right；复用控件、搜索/添加/拖放、窄窗口收起及生命周期不变。没有增加测试数量，只将既有布局断言改为验证左侧及三栏顺序。
- 红：%TEMP%/vision-toolbox-left-before.log，Passed 1, Failed 1, Skipped 0，原右侧布局不满足新断言。绿：vision-toolbox-left-verified.log，工具箱+P6+组合定向Passed 24, Failed 0, Skipped 0。命令与V1.18定向一致，过滤FullyQualifiedName~VisionFlowToolboxTests|FullyQualifiedName~VisionFlowP6Tests|FullyQualifiedName~VisionToolCombinationTests，保留--no-restore、Tests/obj/editor-verification和串行构建参数。
- 显式宿主增量构建0警告/0错误，vision-toolbox-left-build.log。已查看本轮重生成processing-branches.png，左侧工具箱可见，中央流程与右侧图像/参数并列。未重跑全量，390/397/414仍是V1.18历史结果。
- 2026-10-24 16:55 +08:00执行后重核下方V1.18两个官方来源，均HTTP200；快照%TEMP%/vision-toolbox-left-reference/，SHA256分别AB56A00F82A37FE378A855571AF155DAD12B615161F9E347651285D916664934、18FB94F04AA89238B4398F91FFF1A860F260F0BB30791D986BCA6F8EBCDB6462。仍只采纳工具选择/连接/配置与阶段结果原则；左右布局依据用户要求及本仓FlowEditor，不声称官方资料要求左侧。物理鼠标/OLE、跨屏和真实SDK未验收。


## 最新：多工具组合修复与右侧工具箱（计划V1.18）

### 实际变更与边界

- V1.17新增VisionToolCombinationTests的14项：真实OpenCV Crop→Threshold→Morphology→Blob重复/重开，六节点双支路、全黑中间图、四种形态学独立面积预期、参数失败停止/独立支路、同裁剪图模板→定位→Blob。首轮失败后修复：图像上游失效撤销本次借用输入但保留ConfiguredImage；设基准与执行共同PrepareImageInput；通用连线改变清旧预览并报告来源/目标；Blob原DTO恢复后重建借用编辑适配，面板不再显示默认10冒充已恢复1。
- V1.17红绿日志：vision-combination-before/input-green、fixture-before/fixture-green、wiring-before/focused、editor-confirmed/editor-green，均在%TEMP%。该轮最终逻辑390/WPF395，宿主0警告/0错误；通用414在该轮较早执行，不冒充其后又跑。
- V1.18直接复用FlowEditor.Controls.Views.FlowToolboxView。新增可选AddNodeCommand依赖属性，未设置保留通用容器原双击/拖放行为；视觉目录只投影原19项ToolEntry，按原Backend分组。搜索/浏览不创建工具。视觉宿主接收标准NodePaletteItem拖放载荷，换算画布位置，复用原Add，阻止通用Drop再次创建；不新增工具目录、执行器或文件格式。
- 顶部重复的工具下拉/添加按钮移除。右侧可收起工具箱，小于1200 DIP自动收起以保留图像区域。只锁添加，运行时仍可搜索。主导航收起独立单工具调试，旧路由/API/.visionproj与节点内执行保留；没有删除自动化单工具测试。
- 新增VisionFlowToolboxTests两项覆盖实际右侧共享控件/顶部入口撤下/900 DIP收起、19项搜索/独立实例/坐标与画布尺寸/选中编辑器/运行关闭守卫；P6改从实际工具箱绑定命令添加，后续像素/模型/定位/.flow/宿主关闭重开断言不减。

### 实际执行

| 范围 | Passed | Failed | Skipped | 日志（%TEMP%） |
|---|---:|---:|---:|---|
| 工具箱+P6+组合定向 | 24 | 0 | 0 | vision-toolbox-final-focused.log |
| Vision非Runtime逻辑 | 390 | 0 | 0 | vision-toolbox-logic-full.log |
| Vision非Runtime WPF | 397 | 0 | 0 | vision-toolbox-wpf-full.log |
| 通用FlowEditor全量 | 414 | 0 | 0 | vision-toolbox-floweditor-verified.log |

- 命令沿用现有dotnet test工程入口、--no-restore、-m:1、-p:BuildInParallel=false、-p:UseSharedCompilation=false、-nr:false。Vision输出隔离Tests/obj/editor-verification，通用输出Tests/obj/p1-verification。定向过滤FullyQualifiedName~VisionFlowToolboxTests|FullyQualifiedName~VisionFlowP6Tests|FullyQualifiedName~VisionToolCombinationTests；Vision全量过滤Category!=RequiresRuntime。WPF额外使用--blame-hang-timeout 90s、--blame-hang-dump-type mini、结果Tests/obj/toolbox-diagnostics。
- 最终显式dotnet build Vision.Workbench.csproj使用相同Vision输出目录/串行参数，退出0、0警告/0错误，vision-toolbox-host-verified.log。该次为增量构建；先前重编译出现既有nullable/xUnit警告，不声称全仓无警告。
- 初始工具箱回归因共享控件不存在而失败（vision-toolbox-before.log）；首次P6旧按钮查找失败，按新需求改为真实工具箱绑定命令，未削弱后续业务断言。通用首轮413通过/1失败是新私有方法缺说明（vision-toolbox-floweditor-full.log），补注释后全量414；无Skip/断言放宽。
- 已查看本轮重生成的processing-branches.png、crop-match-blob.png，目录Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/combination-acceptance。工具箱与图像/参数并列；裁剪局部坐标与紫色派生域仍保持。命令调用与创建位置验证不是物理鼠标/OLE拖放验收；主题/物理DPI跨屏及授权SDK仍待现场验收。

### 执行后公开对照与下一步

- 2026-10-24 16:40 +08:00重新核实Aurora FirstProgram和OpenPnP CvPipelineEditor，均HTTP200；%TEMP%/vision-toolbox-reference/0.txt、1.txt，SHA256分别AB56A00F82A37FE378A855571AF155DAD12B615161F9E347651285D916664934、18FB94F04AA89238B4398F91FFF1A860F260F0BB30791D986BCA6F8EBCDB6462。采纳任务内选工具/连接/配置及阶段结果预览，不照搬完整Docking或另建注册框架；不声称竞品实机操作。具体链接/事实/差距见实施计划V1.18对照表。
- 组合轮执行后12:15已核实RunningAndAnalysingPrograms和CvPipeline：%TEMP%/vision-combination-reference/final/，SHA256分别326AEE2CE89809835ED06F2857858D5E909AD053513EDCEB8E068A0181AABB20、E0398B56036DC80394427A8B643580C584ACDFA1E1B421A53A596C5A125C147B。
- 十项终验仍未全部达标；P7测量跟随未实施，同输入刚体边界不变；缩放/镜像/世界单位/跨裁剪坐标链、资产去重/模型打包迁移、授权SDK/实图与物理交互不自动销项。


## 最新：P6复核、P7范围待确认（计划V1.16）

- 本轮抽查默认任务导航、文件Unchanged解码/格式拒绝及P6回归；定向 **Passed 8, Failed 0, Skipped 0**，退出0，日志%TEMP%/vision-p6-review.log。
- 实际命令：与下方P6记录相同的dotnet test入口，--filter 'FullyQualifiedName~VisionFlowP6Tests'，--no-restore，--artifacts-path保持Tests/obj/editor-verification，-m:1、BuildInParallel=false、UseSharedCompilation=false、-nr:false。
- 本轮没有生产代码改动、没有另跑全量/显式宿主构建；390/381/414和构建0警告/0错误仍是V1.15历史门禁，不冒充此次重跑。
- 原计划仅P0–P6，没有已定义P7。本轮在计划中加入“OpenCV测量工具刚体定位跟随”建议，待用户确认；未实施P7，未声明新的联网对照或P7测试通过。
- 第8项测量/任意帧局部编辑/跨图坐标链、授权SDK与物理UI仍未销项；V1.15十项终验“尚未全部达标”结论不变。


## 最新：P5复核/P6入口与最终矩阵审计（计划V1.15；整体未全部完成）

### 本轮实际范围

- P5定向 **Passed 15, Failed 0, Skipped 0**（vision-p5-review.log），设备契约 **Passed 4, Failed 0, Skipped 0**（vision-p5-device-review.log）。
- 默认真实启动进入VisionFlowView，仍直接组合通用FlowChartView与原视觉编辑器；单工具/旧任务路由保留。夹具在ApplicationIdle、其它测试导航前记录实际初始活动页，避免事后导航假装验证默认入口。3项旧单工具宿主测试改为先显式导航，再保留原页面/工作区/外观断言，不依赖全局默认页或测试顺序。
- 任务页“保存任务”绑定既有Chart.SaveCommand，首次选路径、后续原路径保存；另存和打开保持原命令。没有新增任务文件/执行器或参数副本。
- P6端到端使用实际页面按钮的绑定命令、宿主原文件解码器和实际OpenCV：导入文件→矩形ROI取样→保存/清除/加载.vtemplate→定位基准→加载平移图→运行→保存.flow→真实Window.Closing/WorkbenchLifetime/CloseRegionAsync→新工作区打开→再执行。旧会话Closed、旧结果清空、新会话非原对象；两次Blob面积9、框(34,18,3,3)，基准ROI不改，本次域(33,17,5,5)。文件选择是可控回调，不冒充物理鼠标或系统对话框点击。
- 真实文件加载器原来强制Grayscale，16位和彩色会静默转Mono8；改为Unchanged及按行拷贝，保留Mono8/Mono16/Bgr24/Bgra32，PNG导入→原像素导出→再导入逐字节一致。浮点TIFF明确拒绝，不暗中降位。原OpenCV算法格式限制不变，不声称导入任意图即可运行算法；无隐式灰度转换节点。
- 兼容审计只作保留结论：codebase-memory索引2026-10-22、34991节点/170589边，新兼容路径均缺记录；没有据0匹配断言无消费者。源码App注册、WorkbenchComposition旧任务创建、MainWindow插槽与兼容测试仍有消费者，三个公开类型及旧URI/路由保留；Controls仍有历史流程依赖。没有破坏性删除或批量清理累计未提交工作。
- 本轮触及的MainWindowViewModel清理模板化注释，夹具修正旧StartupUri/单工具默认说明；关键默认导航、借用/关闭和导入失败语义已注明，不宣称全仓注释治理完成。

### 新增测试、失败与修复

- VisionFlowP6Tests共8项：真实初始页1、原保存命令1、4种格式原像素文件往返、浮点拒绝1、实际宿主保存关闭重开1。
- vision-p6-before.log为新增测试访问internal组合根导致的编译错误，不算产品红测；vision-p6-red.log还包含测试反射查找标志错误/未加载视觉树，不拿这两项当缺陷证据。
- 修正测试访问/加载方式后，vision-p6-verified-red.log为6项中1通过/5失败：Mono8本就通过；Mono16/Bgr24/Bgra32实得Mono8，默认页仍Browser，保存任务按钮不存在。修复后vision-p6-first-green.log 6/6通过。新增两个端到端/浮点场景后vision-p6-focused-first.log 8/8通过。
- 最终 **Passed 390, Failed 0, Skipped 0**（逻辑，vision-p6-logic-full.log）；**Passed 381, Failed 0, Skipped 0**（Vision WPF，vision-p6-wpf-full.log）；**Passed 414, Failed 0, Skipped 0**（通用FlowEditor，vision-p6-floweditor-full.log）；Workbench显式增量构建 **0警告/0错误**（vision-p6-host-final.log）。
- 全量后仅调整新增测试元组的初始可空标注，消除自身静态分析警告；生产代码未变，最终P6 8/8复核见vision-p6-focused-final.log。旧测试nullable警告没有顺带全仓修理，不把测试工程构建说成0警告。
- 本轮各门禁均正常结束，未强停进程、去Skip或改阈值/放宽像素断言。未运行MachineApp/生产脚本、SDK授权模型或硬件，也未做全仓构建。

### 产物、资料与终验结论

Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/p6-acceptance/包含host-roundtrip.flow、host-template.vtemplate、两帧PNG、before-close.png/after-reopen.png及各格式导入导出样本。已读取after-reopen.png，显示真实Next/定位线、基准、派生域及重开后结果；最终文件SHA256为5B1FAB6022A96FB261DDDE49520CD2817EDD979648E10277F526FA81289F0A93。1500×850窗口、96-DPI合成输入，不是物理连线/跨屏或实图精度验收。

最终门禁和P6复核后11:33 +08:00重新获取Aurora First Program和OpenPnP CvPipeline，均HTTP200；快照%TEMP%/vision-p6-reference/final/，11:31初次读取保留。采纳文件/实例/配置/数据/结果闭环及配置/运行资源分离；任意端口预览/Docking不扩入本期，真实SDK与物理交互不抵扣。完整URL、SHA256与差距见V1.15计划。

十项逐项结果、证据与下一步已写入计划第4节：**整体未全部达标**。功能阻断为P3测量跟随/任意帧局部编辑与跨图坐标链；授权验收为HALCON形状训练/文件/匹配及三家AI推理、预处理/资源释放；人工待验为鼠标/系统对话框/跨屏。兼容保留和资产去重/模型打包/相对路径等明确后续，不自动销项。P6离线入口/闭环执行完成不等于P6整体终验或整个计划完成。

### 实际命令

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~VisionFlowP5Tests' --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~P5DeviceSelectionTests' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~VisionFlowP6Tests' --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/p6-diagnostics" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
```


## 最新：P4复核与P5模型/匹配/AI离线闭环（计划V1.13）

### 复核及改动

- P4定向 **Passed 12, Failed 0, Skipped 0**（vision-p4-review.log）：四视图9项及Blob缓存/手势3项；完整呈现边界还由最终逻辑门禁覆盖。P3测量/任意帧局部编辑与授权边界不销项。
- P5原TemplateEditor/ITemplateTool/TemplateModelFile及原.flow路径继续复用。成功取样/训练/标脏/清除撤销旧文件引用，保存/加载更新并通知；失败/取消保留原模型引用。模板页显示可用/需训练状态与文件路径，OpenCV任务内嵌取样不再被误报为必须显式加载。
- 工作区提供模板命令只读/忙态谓词，原三参构造保留。直接Capture也守卫；模态文件选择返回后再校验，期间任务进入只读则不改模型、不清当前结果；借用会话仍不拥有/释放模型。
- OpenCV真实模型文件→双匹配（稍低分第二候选）→明确索引1/基准→任务保存→移开模型引用仍由内嵌取样重开→换图平移→Blob面积9、框(52,30,3,3)、基准ROI不变。坏文件加载拒绝并撤销来源/下游旧结果，旧取样和路径仍保留。不是旋转/缩放灰度算法，score不与HALCON跨后端比较。
- 四类AI×路径/设备两种身份变化，用原目录/工具/任务工厂与fake Provider验证：Run不隐式加载，变更后旧会话不可用，显式加载/重开/释放计数正确；每例3次Open对应3次Close。没有加载真实推理模型，不把Load叫Train。
- 实际三家Provider新增不支持设备值的显式拒绝；HALCON当前仅默认/cpu，Aiditron/Invi仅默认。Invi拒绝先于native manager创建。默认参数和旧参数名/格式不变，之前被忽略的非默认值现在报错。详细模型/Provider能力与所有权见MODEL_CAPABILITIES.md。

### 红绿与新增场景

- vision-p5-before.log：4项失败（两项旧ModelReferencePath残留、只读时命令仍可执行、模态选择返回后仍换模型）；vision-p5-first-green.log四项通过。没有放宽模型/结果对象身份断言，也不删除旧模型文件“求一致”。
- vision-p5-focused-first.log：**15/15通过**，包含上述4项、2项受控形状训练引用契约、8项四AI身份/重开、1项真实OpenCV端到端。形状fake不执行SDK，不宣称真实训练/文件验收。
- vision-p5-device-before.log：仅Invi注入的fake factory被错误调用，预期NotSupportedException、实际manager创建错误；未触发真实native。修复后三家共4项契约通过（vision-p5-device-green.log）。其他两家没有未经修复就尝试native来制造红测。
- 最终 **Passed 390, Failed 0, Skipped 0**（逻辑，vision-p5-logic-full.log）；**Passed 373, Failed 0, Skipped 0**（WPF，vision-p5-wpf-full.log，含P5 15项）；宿主独立显式增量构建 **0警告/0错误**（vision-p5-host-verified.log）。
- 复合门禁命令超过240秒工具时限：WPF已成功（3.1804分钟）、逻辑已成功，但首次构建未取得结果；vision-p5-host-final.log为空，不作为成功证据。未发现仍运行的dotnet进程，随后只独立重跑build并通过，没有强停用户进程或隐去超时。未做全仓构建；通用FlowEditor未改未重跑，414是P2历史复核数据。

### 产物与执行后对照

Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/p5-acceptance/保存实际.vtemplate/.flow及model-fixture-blob.png。已读取该1500×850窗口的96-DPI截图，显示真实Next/定位数据线、索引1、基准与派生搜索域；不是物理拖线/跨屏或实图精度验收。截图SHA256为4F15E3C58C6A9B6FCB5E7368AAE24C952D8FE72F4BE4741FA3684D5588648B25。

最终构建后11:08 +08:00重新获取Aurora模型编辑和HALCON 23.11匹配算子文档，均HTTP200，快照%TEMP%/vision-p5-reference/；核实取样/模型/匹配参数分离、角度/尺度/参考点语义。采纳状态/原始返回值与明确能力；灰度旋转/尺度算法暂缓，授权SDK保持待验。URL、SHA256、达到/差距/取舍见计划V1.13，不把文档当SDK验收。

### 实际命令

以下各条输出重定向到上述TEMP日志；复合运行超时后build独立重跑。

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~VisionFlowP4Tests|FullyQualifiedName~BlobViewportRegressionTests' --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~VisionFlowP5Tests' --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~P5DeviceSelectionTests' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/p5-diagnostics" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
```

### 未完成

HALCON真实训练/保存/重开/匹配及两后端实图精度、三家AI模型/预处理/实际推理与资源释放仍待授权验收，RequiresRuntime/Skip未更改。P3残项、外部模型打包/相对路径迁移、P2图像去重及P6终验仍未交付；没有进入P6或声称十项全部完成。


## 最新：P3复核及P4独立显示/呈现（计划V1.11）

- P3本轮定向13/13通过，日志%TEMP%/vision-p3-review.log。主链来源/基准/同输入本次性及失效隔离仍有效；测量工具跟随、任意帧局部编辑、缩放/镜像和授权SDK边界不销项。
- P4复用原模板工具的真实取样，统一Input/ROI/Result/Training画布。模板能力显式控制训练页可见性，无样本禁用、清除显示空态、切无能力工具回输入；模板/参数/结果仍由原Workspace/Card拥有，编辑会话与画布借用。训练图不混用输入ROI/结果坐标（同尺寸也禁止）。
- 原像素PNG/TIFF与视口截图语义分离；本轮截图只由测试独立生成，无新增截图产品命令。16位stride合同用fake取样检查显示/保存，不执行HALCON训练。真实OpenCV模板仅支持Mono8，取样图不叫已完成AI训练。
- VisionDisplay.OutputImage使用原IOutputImageResult，第三方输出无需改画布分支；具体后端绘制迁到Presentation/Rendering/VisionResultDrawing，图像画布无Tools/AI结果分支，保留原结果选择、像素坐标、画笔层次与缓存。原元数据参数/三基础页/异构结果表不改为巨大DTO。
- 最终 **Passed 386, Failed 0, Skipped 0**（逻辑，vision-p4-release-logic.log）；**Passed 358, Failed 0, Skipped 0**（WPF，vision-p4-release-wpf.log，含9项新增P4）；宿主显式增量构建 **0警告/0错误**（vision-p4-release-host.log）。通用FlowEditor生产代码本轮未改、未重跑；414项是P2复核证据，不能冒充本轮结果。没有全仓构建结论。
- 验收产物：Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/p4-acceptance/。4组900/1300宽度、深浅主题、100/125/150/200%模拟DPI，每组四视图并卸载重载，输出各视口截图与像素图；不是16组全笛卡尔组合，也不是物理跨屏。实际取样PNG及Mono16 PNG/TIFF有逐像素校验。已读900宽、深色200%训练页截图核查布局。
- 10:44 +08:00最终门禁后重新取得Aurora Preview/Data Presenting与OpenPnP CvPipelineEditor，均HTTP200；快照%TEMP%/vision-p4-reference/release/。采纳显示不改算法数据、图像/视口保存区分及原实例/呈现分离；Docking、任意端口预览、显示变换暂缓。URL、SHA256及差距见V1.11计划。

### 本轮失败与修复（保留日志，不放宽断言）

1. vision-p4-before.log：2项失败，缺训练视图、第三方IOutputImageResult未显示；原功能缺口，不是编译失败。vision-p4-second.log两项转绿。过程中XAML属性元素位置错误为编译错误，不冒充产品红测。
2. vision-p4-switch-red.log：期望Input、实际Training；切工具能力消失未退出旧视图。Model在能力撤销时复位，vision-p4-focused.log 7项通过（尚未加后两项Mono16用例）。
3. vision-p4-wpf-first.log：351通过/5失败。3项原测试仍对不可见训练Tab检查非零高度，改为明确检查能力显隐，并保留全部可见Tab的高度/边界断言；2项真实UI回归为新增第二信息行导致绘制时画布高度变化15.24。改回固定单行分列与绘制动作同位替换，原高度/像素断言不改。vision-p4-ui-verified.log定向21通过。
4. vision-p4-logic-final.log：384通过/1失败，原架构守卫只允许三Tab；更新为四Tab且新增HasTrainingImage及SupportsTemplateInput绑定断言，没有删除固定XAML/非生成集合约束。vision-p4-logic-verified.log 385通过（最终新呈现守卫前）。
5. vision-p4-presentation-before.log：新增画布后端分支守卫失败，完成局部绘制适配提取。vision-p4-presentation-wpf-final.log 357通过/1失败：旧缓存回归反射访问已迁出私有字段；测试改为访问呈现适配里的同一缓存，保留同一底图/缩放/选择重用、结果替换换缓存、Frozen全部断言。最终release三日志全绿。此结构改动不宣称是算法缺陷修复。
6. 未改旧算法阈值、跳过标记、分类或Runtime安装；未强停用户进程，暂存区未操作。

### 最终实际命令

各命令输出以Out-File -Encoding utf8写入上述日志；退出码分别核对，失败时未继续构建冒充成功。

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~VisionFlowP3Tests' --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/p4-diagnostics" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
```

后续：保留P3测量/局部编辑差距及真实SDK、物理交互待验；P5未执行，不能把四类图像显示当成全部十项需求完成。


## 最新：复核P2通过，P3刚体定位主闭环（计划V1.9；P3未全部收口）

### P2复核

- 旧Software-Framework.sln引用更新为当前真实Software Frame.sln；缺失中心历史报告不伪造，新增源码可追溯的FlowEditor/ARCHITECTURE.md作为当前维护入口。测试仍逐一验证全部项目/文档链接存在，无Skip或断言删除。通用全量414/414通过（%TEMP%/vision-p2-review-floweditor.log）。
- 空任务图像状态先红后绿（vision-p2-empty-before.log，NotNull失败），用原文档DocumentProperties保留宿主版本化输入，VisionTaskDocumentStore仅包装原存储作校验，不自写文件引擎。增加2 MiB输入、两个节点的往返回归；文档8,394,955字节，去重未实现且不当作压缩包。
- P2复核逻辑381、WPF336通过（vision-p2-review-logic.log、vision-p2-review-wpf.log），均0失败/0跳过。08:55 +08:00执行后再次联网核实MERLIC Tools/OpenPnP CvPipeline并更新V1.8后进入P3。

### P3实际改动

- IFixtureResult/FixtureObservation由原OpenCV/HALCON匹配结果提供候选，保持位置/角度/分数/真实Scale；OpenCV固定方向匹配明确是零角度、单位尺度，不假装提供旋转测量。注册元数据来自结果能力，不猜Name或Backend。
- 主任务页提供定位来源下拉、从0起匹配索引、“设为定位基准”与基准X/Y/角度。匹配索引变化需重新设基准；配置随原.flow重开。仍使用原Node/端口/PortValueTransfer/Card与准备回调，没有第二份参数或调度器。
- 来源结果携带执行准备时的输入身份（尺寸/格式/stride/frame/timestamp/像素hash），不同图像/旧帧/原地像素改写、无匹配、失败、索引越界、非有限值、非单位尺度均拒绝，不回退旧位姿。目标只接受显式搜索ROI能力，测量DTO不被假装成搜索域能力。
- 基准ROI不改写，每次派生本次搜索几何；紫色只读虚线单独绘制实际搜索域，不可被手势编辑。来源失效/连线变化撤下下游结果，后台目标失效依原revision释放迟到结果，并立即撤掉派生叠加。
- 真实离线OpenCV模板匹配→Blob：实际组合控件选择来源、绑定命令设基准、换位置、当前工具按钮与任务运行、保存重开均通过，结果面积9、位置(34,18)、搜索域(33,17,5,5)，原ROI保持(23.5,12.5)。截图在Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/p3-acceptance/template-blob-follow.png（96-DPI合成输入）。
- 原HALCON托管结果的+90/-90/180°消费、第二匹配候选选择、各种失效及迟到场景使用受控源+实际OpenCV目标验证，不调用HALCON Runtime，不替代真实训练/匹配验收。
- 能力/单位/未实现项见FIXTURE_CAPABILITIES.md：当前只有Blob声明搜索ROI跟随；FindLine/FindCircle等主链跟随、任意帧局部编辑、跨预处理/裁剪图坐标链仍未完成。缩放、镜像和世界单位换算明确不支持，不宣称整个P3或第8项交付。

### 红绿证据与问题修复

- 90°搜索ROI首个端到端回归发现4×2矩形应8像素却9像素（vision-p3-focused-first.log）。独立Core Contains计数排除包围框/重复变换后±90°先失败（vision-p3-quarterturn-before.log，4项中2失败）。以精确正交角三角值统一Pose2D/搜索域，非正交角不近似；±90/180°和Core回归转绿，没有改为期望9或放宽像素断言。
- 后续±90/180°坐标断言暴露同类Pose2D变换浮点残差，使用相同RotationMath源消除正交残差，不以ToString近似值相同代替坐标正确。
- 上游在目标后台调用中失效，旧派生ROI仍留在画布：vision-p3-late-before.log中期望null、实际Rectangle。会话ClearResult明确清叠加，不依赖LastResult从非空变空的通知；回归证明撤下叠加、结果不回填、迟到结果恰好释放一次。
- 编译阶段遗漏using及codemode语法错误不算产品红绿证据；未修改算法阈值、放宽断言、去掉RequiresRuntime/Skip或强停进程。

### 最终验证

- Vision逻辑全量：**Passed 385, Failed 0, Skipped 0**（vision-p3-logic-full.log）。
- Vision WPF全量：**Passed 349, Failed 0, Skipped 0**（vision-p3-wpf-full.log）。
- P3场景：13项，已包含在349中；专门定向12项通过的vision-p3-focused-verified.log为加入timestamp用例前记录，不能把12冒充最终13项定向重跑。
- Workbench显式增量构建：**0警告、0错误**（vision-p3-workbench-final.log）。通用FlowEditor414项是进入P3前P2复核结果；P3未再修改其生产代码，不冒充此次重跑。
- 所有日志在本机TEMP；产物在Tests/obj隔离目录。没有MachineApp全仓构建或真实SDK/鼠标/跨屏验收结论。

### 执行后对照

10:02 +08:00在最终Vision门禁及构建后重新读取Aurora 5.5.6 Local Coordinate Systems与OpenPnP develop AffineWarp源码，均HTTP200，快照%TEMP%/vision-p3-reference/。Aurora强调alignment输入、局部编辑及绝对坐标输出；本实现采纳显式来源/派生几何/同坐标叠加，但局部编辑仍有差距。OpenPnP明确scale/shear/mirror及实际单位、相机中心/Y向上约定，本实现只做图像像素刚体并明确拒绝其它能力，不照搬单位/符号或实现。MERLIC ROI尝试404，记待证，不作为核实来源。URL、SHA256、采纳/暂缓及下阶段边界见计划V1.9。

### 实际命令

```powershell
# P2完整复核
dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
# Core正交像素红测/绿测
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~QuarterTurnRectangle' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
# P3定向（各次过滤范围与日志分别记录）
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~VisionFlowP3Tests' --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/p3-diagnostics" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
```


## 最新：复核P1、执行P2核心文档/数据闭环（计划V1.7；完整模块门禁仍有3项失败）

### P1先收口再进入P2

- 复核并新增“移除空闲节点立即归还”2项回归，最初均失败（DisposeCount期望1/实际0，%TEMP%/vision-p1-retire-before.log），修复后通过；补运行中移除，确认不提前释放、不回填迟到结果、关闭等待实际调用。
- VisionFlowViewModel通过既有Chart.Nodes集合变化采用/退役节点，退役错误实时可见且关闭聚合保留，不新增调度器。P1收口轮逻辑381/WPF322通过（vision-p1-close-logic.log、vision-p1-close-wpf.log）。
- 三个旧公开类型VisionTaskViewModel/VisionTaskToolNode/VisionToolFlowNode按已核实仓内消费者保留，ControlsDependencyTests精确锁定历史FlowEditor使用范围且禁止Controls→Vision.FlowNode；不得据此声称依赖已清零。兼容债务明示交P6，无破坏性移动或兼容工程新增。
- 本地验证后07:51 +08:00联网读取MERLIC Tools/OpenPnP CvPipeline，均HTTP200，更新V1.6并进入用户授权的P2；不是跳过原P1资源阻断。

### P2实际实现

- 原通用.flow/mapper/store/INodeFactory/执行器均复用。INodeDocumentState支持严格配置捕获/候选恢复；VisionNodeFactory只物化目录已知视觉实例。通用文档准备完所有候选及连接后才提交，准备失败清理候选并保旧；存储先序列化再同目录临时写入/替换，不截断旧文件。
- VisionToolConfiguration v1保存工具身份、原参数JSON、Provider、ROI/选择、基准/静态位姿、原始输入快照/stride/格式/source/frame/timestamp/hash、取样/训练参数及模型引用。恢复不加载模型、不训练、不执行或恢复旧结果；缺失模型引用明确提示。原.visionproj v1/v2读取路径保留。
- 新IOutputImageResult及条目能力明确声明可链式输出图，不猜工具名称/Backend。当前Crop/Threshold/Morphology声明输出；下拉来源修改原通用数据连接。Node准备复用PortValueTransfer和SyncInputValues，手动按钮/流程共用原Card回调；不新增并行绑定或运行结果拥有者。
- 真实离线OpenCV Crop→Blob：未绑定面积100，选Crop输出后12；另存/重开后及当前工具按钮均12。清除上游结果，下游清旧结果并报告无本次输出，不回退原图；切回任务输入恢复100。配置原图保持10×10，本次执行为4×3，实际状态显示同步更新。
- 图像原像素、Mono16及stride=16/宽3的填充缓冲、source/frame/timestamp均往返保留；多个同类节点ID、原参数独立和ROI/选择保持。真实组合控件加载不会把已有数据来源重置。截图为Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/p2-acceptance/crop-blob-bound.png，96-DPI合成输入，不是物理鼠标/跨屏或竞品实机验收。
- P2使实际宿主文档能力可用，原P1导航测试相应从“命令禁用”改为“命令可用”；这是授权新增行为，非削弱原生命周期断言。原P0/兼容/算法回归保留。
- 保存当前按节点保存输入快照，尚未提供去重资产包、大图体积及零节点任务独立输入状态保证。定位来源选择UI/匹配姿态消费仍P3；已保存基准位姿不等于完成该项。

### 失败反馈与修复证据

- 复制/粘贴ROI共享：vision-p2-copy-before.log中Assert.NotSame失败；改为配置值快照及独立ROI后通过，原DTO仍是每实例自己的同一份参数对象。
- 旧单工具JSON被当空流程：vision-p2-legacy-before.log中期望拒绝、实际接受；加载器增加流程根结构检查后保留旧任务并明确提示走原单工具入口。
- 执行输入状态残留：vision-p2-input-status-before.log中期望4×3、实际10×10；瞬时执行输入更新图像状态后通过，不覆盖配置原图。
- 首轮通用全量10项失败（vision-p2-floweditor-full.log）：2项来自本次方法落在主partial/方法摘要遗漏，已按原架构守卫修复，不放宽断言；5项插件契约失败来自测试将隔离产物项目名误作Configuration，按实际已生成独立DLL路径支持标准/隔离两种布局，保留ReferenceOutputAssembly=false和Assembly.LoadFrom。3项目录/资料治理仍失败，见下。
- 配置冻结改动曾使JSON大小写与测试损坏样本路径不一致，产生测试NullReferenceException；已统一视觉配置camelCase并复测，未作为产品红绿证据。

### 最终执行结果

| 范围 | Passed | Failed | Skipped | 日志（本机TEMP） |
|---|---:|---:|---:|---|
| 定向：P2/P1/P0/原程序化节点 | 39 | 0 | 0 | vision-p2-focused-verified.log |
| Vision逻辑全量非Runtime | 381 | 0 | 0 | vision-p2-logic-final.log |
| Vision WPF全量非Runtime | 334 | 0 | 0 | vision-p2-wpf-verified.log |
| 通用FlowEditor Workbench全量 | 411 | 3 | 0 | vision-p2-floweditor-gate.log |
| Workbench显式增量构建 | 0警告/0错误 | — | — | vision-p2-workbench-verified.log |

通用全量的三个当前阻塞，不得略过后声称全绿：

1. Solutions_ReferenceExistingProjects_AfterRename：旧名称Software-Framework.sln不存在；实际根文件是Software Frame.sln。
2. NoInfrastructureCatchAll_InSolutions：同样找不到旧名称根解决方案。
3. ArchitectureDocuments_ReferenceCentralDocumentation：Documentation/Refactoring/Software-Architecture-Analysis-and-Refactoring-Plan-2026-10-03.md缺失。

没有伪造文档/解决方案、删除测试、添加Skip、安装Runtime或强停用户进程。不能仅凭文件当前缺失就宣称此前某一全量基线也失败；本轮已实际观测的完整门禁仍是411/414。P2核心闭环通过但完整阶段未收口，**未进入P3**。

### 执行后联网对照

在最终验证后2026-10-24 08:32 +08:00重新访问MERLIC 5.6.2 Tools和OpenPnP develop CvPipeline，均HTTP200，快照在%TEMP%/vision-p2-comparison/。核实MERLIC区分连接/状态与MVApp参数结果；OpenPnP提供toXmlString/fromXmlString配置交换、按阶段取得结果及明确缺失结果错误。采纳配置/运行结果分离、显式数据来源和缺失结果失败；暂缓额外任务接口与资产优化，不照搬XML/Java实现，也不拿文档证明竞品功能对等。URL、SHA256与逐项取舍见计划V1.7。

### 实际命令

```powershell
# P1退役红测；修复后完整P1回归另记上述日志
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~RemovedIdleNode_RetiresImmediately' --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
# P2最终定向
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~VisionFlowP2Tests|FullyQualifiedName~VisionFlowP1Tests|FullyQualifiedName~VisionTaskP0Tests|FullyQualifiedName~VisionToolFlowNodeTests' --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-build --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/p2-diagnostics"
dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
```

P2三个独立红测沿用上述WPF测试命令，过滤分别为GenericCopyPaste_CreatesIndependentRoiAndParameterInstances、InvalidCandidate_KeepsOldInstancesAndParameters、ExplicitCropOutputBinding；日志均保留。


## 最新：P0复核 + P1主路径实施（计划V1.5，兼容收口未完成）

### 改动与范围

- 实施前P0的8项回归再次通过（%TEMP%/vision-p1-p0-recheck.log）。全部旧P0用例保留，本轮定向门禁仍包含它们；不重置用户工作区或删除旧公开类型。
- 新增Vision.FlowNode.VisionToolNode，接管Core.IVisionToolSession，不引用Vision.Controls/厂商工具程序集，不复制算法；现有NodeModel来自FlowEditor.Controls，明确仍依赖WPF。
- 新轻量VisionToolInstanceWorkspace复用PreparedProjectSelection、原ToolCardViewModel/Workspace、原DTO、ROI、TemplateEditor和Provider准备/释放，不逐节点创建完整BrowserWorkspace、目录切换或单工具工程协调。
- Workbench“任务编排”现直接使用通用FlowChartView/FlowChartViewModel，右侧借用原VisionToolEditorView；INodeEditorHandler只选中既有实例。首节点为入口，其余节点由通用Next连线调度。切换实例不重建工具/参数，流程与手动按钮使用同一Card和基准ROI刚体准备。
- 原HALCON/OpenCV/AI工具、三家Provider、旧单工具工程/导航均保留；未加载模型、连接设备或运行授权SDK。Core.SearchRegionPreparation被新主路径和原程序化节点复用，仅平移/旋转，不宣称缩放/镜像或主任务定位来源绑定已完成。
- 通用FlowEditor的SupportsDocumentEditing默认true；当前视觉宿主false，禁用保存/打开、复制粘贴恢复、撤销重做，快捷键遵循CanExecute。没有新增视觉编辑器项目、调度器或任务文件格式。
- 新增项目同步登记根解决方案、Vision.sln和原VisionLayout真源。布局守卫初次失败提醒遗漏登记后修正，未删除守卫或降低精确集合断言。

### 测试反馈与修复

- 新增7项P1回归：实际导航/控件/缓存复用，原实例及双入口定位准备，双入口真实Closing+失败非活动缓存页+Provider归还（2项），程序集边界，失败阻止下游及默认通用文档命令保留，前置取消清旧结果/端口。
- 前置取消回归确实先失败：Expected null / Actual Result(ok, 0ms)，见vision-p1-cancel-before.log；修复会话在检查前置取消前撤下旧结果，验证原工具不再运行、旧结果恰好释放一次、输出清空。不是改变断言求绿。
- 早期编译中测试API拼写/类型错误不算运行失败证据。整流程关闭早期超时来自测试漏连第二个节点，被原执行器合法性检查拒绝；补合法Next拓扑及失败后的正常窗口清理，没有改SDK取消策略或放宽超时。早期失败/清理超时见vision-p1-focused-run2.log，不计为通过。
- **最终定向24通过，逻辑380通过，WPF全量319通过，通用FlowEditor相关24通过，均0失败/0跳过**。Workbench显式增量构建0警告/0错误；不是MachineApp或全仓构建。
- 日志依次为vision-p1-focused-gate-final.log、vision-p1-logic-final.log、vision-p1-wpf-final.log、vision-p1-floweditor-regression.log、vision-p1-workbench-final.log，均在本机TEMP。
- 完整WPF后仅增强截图的Loaded队列排空及实际连线层非空断言，关闭集成2项再通过（vision-p1-visual-close.log）；生产代码未再修改。两张96-DPI合成图位于 `Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/p1-acceptance/busy-flow-{True,False}.png`。截图展示实际通用Next线、独立节点和运行中切到只读实例；不是物理拖线、完整主题/DPI布局验收或原生SDK验收。

### 执行后联网对照与门禁

- 最终离线门禁后2026-10-24 07:36 +08:00重新读取MERLIC 5.6.2 Tools文档与OpenPnP develop CvPipelineEditor源码，均HTTP200；本机快照在%TEMP%/vision-p1-comparison/，URL/SHA256及逐项取舍见计划V1.5。
- MERLIC：核实工具流展示实例、连接、状态与另行定义的MVApp参数/结果。采纳实例/状态/Next可见，不把控制连接冒充数据绑定；数据/定位来源仍P2/P3。
- OpenPnP：核实编辑器接收既有pipeline并组合PipelinePanel/ResultsPanel。采纳借用原实例，不照搬Swing/Java注册或线程策略；本实现移出BrowserWorkspace的主路径已验，旧公开类型仍有兼容残留。
- **P1不能标记全部完成**：Controls仍保留旧公开任务/节点及FlowEditor引用，需继续核对消费者与兼容收口策略；画布移除的实例目前仍归流程所有、最迟关闭时归还，频繁增删的即时退役另需验证。P2尚未开始。
- 真实SDK/长调用、关闭超时后的人工选择、物理鼠标拖拽、跨屏和全主题布局未验收。无安装Runtime、强停进程、自动模型加载、断言豁免或厂商算法升级。

### 实际命令

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-build --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~VisionTaskP0Tests' --logger 'console;verbosity=normal'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~VisionFlowP1Tests|FullyQualifiedName~VisionTaskP0Tests|FullyQualifiedName~VisionToolFlowNodeTests' --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-build --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/p1-diagnostics"
dotnet test "Software Frame/FlowEditor/Tests/FlowEditor.Workbench.Tests/FlowEditor.Workbench.Tests.csproj" --artifacts-path "Software Frame/FlowEditor/Tests/obj/p1-verification" --filter 'FullyQualifiedName~FlowCommandBarViewTests|FullyQualifiedName~FlowChartContainerAcceptanceTests|FullyQualifiedName~OptionalNodeAssemblyTests' --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
```

初次接入新项目的构建及测试曾省略--no-restore，以恢复必要项目资产；没有修改包版本或安装厂商Runtime。前置取消红测使用同一测试命令过滤VisionFlowP1Tests.CanceledBeforeNodeExecution；截图补验过滤VisionFlowP1Tests.RealClosing。


## 最新：P0收尾——真实宿主Closing与缓存页清理集成

- 用户要求继续P0，本轮只补既有VisionTaskP0Tests的2项集成用例，不修改生产代码，不进入P1。分别从整任务和当前工具命令发起受控后台fake，通过测试窗口真实Closing事件→WorkbenchLifetime→CloseRegionAsync验证关闭等待，而不是只调用任务VM.DisposeAsync。
- Region中先放置一个释放失败的非活动缓存页，再放活动执行页；确认前者被尝试清理且错误可观察，后者未实际结束时窗口仍保留；释放阻塞后窗口才关闭，结果/工具恰好清理一次。仅创建并关闭本地测试窗口，不关闭共享测试主窗、用户Workbench/VS进程，不运行相机或SDK。
- 定向 **Passed: 2, Failed: 0, Skipped: 0**（%TEMP%/vision-p0-host-close-first.log）；完整非Runtime WPF **Passed: 312, Failed: 0, Skipped: 0**（vision-p0-host-wpf-full.log）。这2项首次运行即通过，是宿主集成补验，不冒充红绿修复。P0合计6项有红绿证据的回归+2项集成用例。
- 本轮仅测试/文档变更，没有重新运行逻辑线或显式宿主构建；上一修复轮逻辑380和宿主0错误仍是各自记录，不能说成此次重新执行。
- 完整WPF回归后，于2026-10-24 05:02 +08:00再次联网读取Aurora Preview and Data Presenting和OpenPnP CvPipeline develop源码，均HTTP200，快照与上轮SHA256相同，保存在本机 `%TEMP%/vision-p0-host-comparison/`。继续采纳预览关闭与运行资源拥有者分离；不由Java close或预览文档推断WPF关闭正确，WPF依据仍是本次实际Closing测试。
- 计划更新 **V1.3**：P0离线收尾完成，P1迁移必须保留宿主Closing/缓存页清理回归。真实SDK长调用、关闭超时后的人工决定、物理鼠标拖拽及跨屏仍未验收，不启动硬件补验，也不增加本阶段无关功能。

实际命令（仓库根目录，沿用现有xUnit v2/VSTest和隔离产物）：

```powershell
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~WindowClose_DrainsActiveTaskAndCachedPage_ThroughRealHostLifetime' --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-build --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/p0-host-diagnostics"
```


## 最新：P0生命周期修复、只读画布浏览及执行后联网对照

- 按实施计划P0限定范围执行，没有开始Vision.FlowNode拆分，不新增Vision.FlowEditor，不修改HALCON/OpenCV/AI算法、参数JSON、任务/单工具格式或SDK加载策略。
- 先新增VisionTaskP0Tests并执行：原5项全部失败，分别观测其余实例DisposeCount为0、单工具执行尚未完成关闭却已返回、运行中画布被禁用（%TEMP%/vision-p0-before.log）。修复后5项转绿；复核同一关闭链再补“工具释放异常不能跳过Provider”用例，先失败（期望1/实际0，vision-p0-provider-before.log），再修复。共6项P0回归，不以编译失败或放宽断言充当红绿证据。
- ToolCardViewModel记录命令和直接API发起的实际RunAsync任务；BeginClose先禁止新调用并通过原Workspace revision撤下/丢弃迟到结果，不提前释放正在使用的工具。VisionTaskToolNode提供异步关闭，等待卡片调用及节点收尾后再清理编辑会话和工作区；同步Dispose只观察已完成的清理，不在UI阻塞SDK。
- VisionTaskViewModel先发起各节点清理，并等待整任务；一个释放失败不跳过其它实例或等待，全部处理后以AggregateException保留错误。VisionBrowserWorkspace在工具释放失败后仍尝试其它拥有资源和Provider清理，状态收尾不被第一个异常截断。失败资源仅能声明已尝试并上报，不能冒充真实SDK已成功回收。
- TaskView不再禁用整个编辑器。ToolCardModel.IsReadOnly是任务级用户编辑锁的单一状态，参数/ROI/模型准备和用户执行命令遵循锁；内部任务执行仍沿用原调用策略，画布与只读预览保持可用。没有参数DTO副本、通用调度器或新增依赖。

所有权与关闭入口：

| 对象 | 创建/拥有者 | 借用者 | 关闭/释放 |
|---|---|---|---|
| 原工具、每次结果及结果表 | 原ToolCardWorkspace | 图像/参数/结果视图、任务节点 | 关闭先失效结果，等待实际卡片执行后由原工作区释放 |
| Provider | 原VisionBrowserWorkspace | AI工具会话 | 工具之后仍尝试清理，错误上报，不因工具异常跳过 |
| 每个节点的Workspace/Editor | VisionTaskToolNode | 当前任务编辑视图 | DisposeAsync等待卡片和节点收尾，视图卸载不释放 |
| 节点集合及整任务运行 | VisionTaskViewModel | Workbench页面 | 取消后续调度，等待各节点与整任务；聚合错误 |

- 定向测试 **Passed: 38, Failed: 0, Skipped: 0**（vision-p0-focused.log）。完整非Runtime逻辑 **Passed: 380, Failed: 0, Skipped: 0**（vision-p0-logic-full.log）；WPF **Passed: 310, Failed: 0, Skipped: 0**（vision-p0-wpf-full.log）。全量使用定向编译后的隔离产物，未把旧测试数量相加。Workbench显式增量构建0警告/0错误（vision-p0-workbench-build.log），不表示全仓历史警告已解决。
- STA回归通过真实任务页切换到尚未执行的实例，检查参数、ROI、重复执行及换图命令禁用，而画布启用；点击实际放大菜单后比较真实渲染白色图像宽度增加，点击平移菜单确认进入平移模式。停止/关闭通过受控后台fake证明等待实际完成、结果清理一次和资源拥有者释放一次。不把命令点击/模式验证冒充物理鼠标拖拽验收。
- 全量验证后于 **2026-10-24 04:51 +08:00** 重新联网读取Aurora 5.5.6 Preview and Data Presenting和OpenPnP develop CvPipeline源码，两项HTTP200，保存本机 `%TEMP%/vision-p0-comparison/`。Aurora明确Move/Zoom只是预览导航、不修改程序图像，采纳为只读浏览约束；OpenPnP明确pipeline管理所保留阶段图像及release/close，采纳明确拥有者，不照搬Java finalize/克隆/线程实现。它们不构成本项目异步SDK关闭的实机证明。具体URL、快照SHA256、差距与取舍已写入IMPLEMENTATION_PLAN.md的P0对照表。
- 计划升级 **V1.2**：P0通过离线自动化门禁；P1迁移必须给出所有权表并保留双入口关闭/清理异常/只读浏览回归；P4继续分离显示变换与算法输入。Docking/任意多窗口预览暂缓，未因对标扩大范围。P1仍未执行。

实际命令（仓库根目录，SDK10.0.401，现有xUnit v2/VSTest）：

```powershell
# 初始红测；随后补充Provider红测时使用对应方法名过滤，均有独立日志
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~VisionTaskP0Tests' --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
# 修复后定向编译与回归
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~VisionTaskP0Tests|FullyQualifiedName~VisionTaskTests|FullyQualifiedName~BrowserWorkspaceTests|FullyQualifiedName~BlobExecutionSessionTests|FullyQualifiedName~EditorFeedbackTests|FullyQualifiedName~WorkbenchCompositionTests' --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
# 完整离线回归
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-build --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-build --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/p0-diagnostics"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
```

本轮未重新构建MachineApp，未连接设备、未运行授权HALCON/AI SDK。真实鼠标拖拽、真实跨屏/显示器、实际SDK长调用及释放错误仍需授权环境验收；P0自动化通过不等于十项需求或完整视觉平台已完成。


## 最新：公共执行契约与 Workbench 顺序任务编排

- 用户确认仿射/定位跟随方向并升级多工具任务编排。本批是功能增量，不把新增任务执行行为称为纯结构重构；保留旧单工具默认导航、类型/参数身份、JSON/工程格式和各SDK执行边界。根AGENTS.md在继续时已不存在，未擅自重建；复核当前技能与实际源码后继续。
- Core新增IImageTool（Name/Validate/Run/LastElapsedMs/Dispose），因为旧ITool是Run()与结果集合语义、Controls执行面又不适合作为无界面契约。三个后端基类显式桥接原泛型结果，HALCON继续RunImage转换/释放，AI不隐式加载模型。原IExecutableTool继承公共执行面，保留原成员身份和默认转发，未删除原UI能力入口。
- VisionToolFlowNode复用NodeModel和原FlowChartExecutionEngine，独占原始工具/结果，借用输入/基准ROI；只对声明ISearchRegionTool的工具应用基准ROI平移/旋转，每次从基准计算、恢复工具原搜索域，不修改输入/ROI。未知能力明确拒绝；不把此项宣称为所有工具已支持完整仿射、缩放或镜像。
- VisionTaskViewModel/VisionTaskView提供实际顺序任务：同类工具可添加多个独立实例，切换保留原参数/ROI/结果；上移/下移决定真实引擎依赖顺序，移除仅释放所选实例，共同输入由原工作区处理。VisionTaskToolNode复用每个实例原Workspace/卡片，不创建第二份DTO或结果；不同于原始工具节点，它只借用卡片拥有的结果，UI准备/发布经既有IUiDispatcher，后台能力仍由原工具声明。
- Workbench左上角导航可进入任务页；原单工具调试仍默认、旧路由仍有效。外观控件通过原插槽在两页间转移，不复制主题控件。任务页隐藏单工具工程操作，避免把保存单工具误认为保存整任务；没有新增占位保存/连线按钮。尚未实现任务文件保存、自由连线、数据绑定和任务页定位来源绑定；工具输出不自动替换后续输入。
- 整任务开始清除所有旧结果；失败阻止后续工具，停止只取消后续调度并等当前调用实际结束，取消/关闭后清理迟到结果。任务VM实现IAsyncDisposable，宿主关闭时沿用WorkbenchLifetime异步清理，让Dispatcher保持到当前执行归还资源；视图导航/卸载不释放任务。
- 新增程序化节点9项定向测试已通过（vision-flow-node-first.log）。原始工具15项公共契约测试以及四类AI fake全流程经Core接口执行，现有目录测试检查原始/卡片19项注册均实现公共执行面。VisionTaskTests新增6项（含停止/关闭两种参数）覆盖真实编辑器实例切换、重排实际执行顺序、所有权、失败清旧、停止/异步关闭和Prism导航重用；相关定向共31项通过（vision-task-ui-focused.log）。首轮任务测试因WPF/领域ValidationResult同名编译失败，已限定测试返回类型，未修改断言或生产结果求绿。
- 最终非Runtime逻辑全量 **380通过/0失败/0跳过**（%TEMP%/vision-task-logic-full.log）；WPF全量 **304通过/0失败**（vision-task-wpf-full.log）。全量用已完成定向编译的Tests/obj隔离产物，--no-build --no-restore；不是把历史结果相加。原基础验证首次逻辑调用超时、WPF调用中断无摘要均不计通过；逻辑重跑380和WPF基础重跑298完整结束后才继续任务页实现。
- 显式构建Vision.Workbench **0错误/0警告（此次增量构建）**（vision-task-workbench-build.log），不表示解决全仓历史警告；本批未重新构建MachineApp或运行真实SDK/设备。目标tracked diff --check通过；原有累计改动未清理、未提交。

实际命令（仓库根目录，SDK 10.0.401，现有xUnit v2/VSTest，无MTP模式配置）：

```powershell
# 定向会编译当前代码（日志 vision-task-ui-focused.log）
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'FullyQualifiedName~VisionTaskTests|FullyQualifiedName~VisionToolFlowNodeTests|FullyQualifiedName~ToolCatalogTests|FullyQualifiedName~WorkbenchCompositionTests|FullyQualifiedName~AppearanceEntryTests|FullyQualifiedName~BrowserInteractionTests' --logger 'console;verbosity=normal' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
# 两条完整测试线使用已编译产物
dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-build --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime'
dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-build --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/task-ui-diagnostics"
dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
```

仍需人工验证新任务页的窄窗口/主题/实际鼠标体验；Runtime/License、真实图像定位精度、缩放镜像、完整任务持久化及自由连线不在本批通过结论中。


## 最新：OpenCV真正卡尺测量与同一编辑几何

- 用户明确要求继续解决两套“卡尺”实际不一致。此次升级OpenCV FindLine，不再只修拟合/显示：新工具默认Calipers，一维投影测量后再拟合。HALCON算法、类型身份、参数和SDK执行边界不改、不调用HALCON Runtime。
- 范围/文件：Tools/OpenCv/Measurement/MeasureTools.cs保留旧Canny及DTO字段，新增SearchMode及卡尺参数；FindLineTool.Calipers.cs承载专用算法partial，EdgeTransition.cs/CaliperEdgeSelection.cs是OpenCV独立参数枚举，不反向引用Tools.Halcon、不新建工程/依赖/通用采样框架。派生可见性bool标记JsonIgnore/不可编辑/不展示。
- 实际测量：按与公共卡尺矩形一致的中心/切向/法向采样，双线性插值及沿切向均值形成一维投影，高斯平滑和灰度梯度后做峰值抛物线插值；按法向负半长到正半长应用All/Positive/Negative及每把卡尺All/First/Last，随后每端剪点及Welsch拟合。至少两把不同且有效的卡尺；不把同一把卡尺的多条边当成一条沿线目标。图外采样跳过整把，不补黑制造边缘。沿用紧凑Mono8输入；每次投影/平滑工作量上限2000万，有限值/枚举/数量/尺寸/Sigma在分配前验证。
- 兼容：FindLineParameters默认SearchMode=EdgeBand，旧Parameters JSON对象缺少新字段时仍为原Canny方式，旧阈值/半宽/最少点数语义不变；FindLineTool构造显式选择新Calipers默认。新旧模式均写入SearchMode后可往返，模式条件不入JSON，不新增工程版本。参数页显式切模式不会运行算法，清旧结果并更新可见参数和ROI说明；旧工程若想显示/运行卡尺须主动改为Calipers。此兼容不承诺新版卡尺工程交给旧程序后仍能正确执行。
- Controls只补模式条件显示/说明刷新：复用原LineRoiEditorView、公共几何和原DTO，不加新ROI/VM；采样框数量来自真实CaliperCount，四角、旋转和参数分区沿用原编辑链。旧“模型Items里应无卡尺字段”测试改为检查当前可见元数据行，保留字段/原对象/JSON/取消和原算法阈值断言；没有靠删断言隐藏新模式。
- 最先新增“新OpenCV工具应与HALCON默认卡尺几何相同”用例，修改前 **0通过/1失败**（%TEMP%/opencv-caliper-geometry-before.log，实际EdgeBand）。新增真实算子合成测试覆盖水平/竖直/反向及正负倾斜的解析平滑边界、极性不符失败、逐卡尺首末边选择、All点数、间隙不测量、剪点、图外边界、至少两把有效卡尺、输入不变、无效配置/工作量及JSON兼容。合成轴向定位≤0.2像素、倾斜≤0.25像素为该组样例独立解析容差，不宣称任意实图测量精度。
- 新增STA用例验证真实采样阵列与数量字段直接写回同一DTO、模式切换可见性/说明且不执行；新增工程导出/导入用例验证新版卡尺字段/模式恢复，旧无模式JSON导入仍为Canny且再次保存/重开不丢失。原Canny专项用例显式选EdgeBand，原带宽/端点外筛点/反光回归继续执行，没有用新卡尺替代旧口径测试。
- 定向首轮逻辑56通过/1失败为元数据测试将所有JsonIgnore当成新只读条件，已将例外精确限定为OpenCV两个派生模式条件，其他DTO规则不变；之后逻辑全量 **365通过**（opencv-real-caliper-logic-full.log）。WPF定向 **32通过**（opencv-real-caliper-ui-first.log）；最终WPF全量 **289通过/0失败**（opencv-real-caliper-wpf-final.log）。合计654个非Runtime用例，不包含额外重复累加。
- 首次并行全量/整机验证超时，opencv-real-caliper-wpf-full.log及opencv-real-caliper-machineapp.log没有最终摘要，不计通过。逻辑已完整结束；利用已编译隔离产物以--no-build --no-restore单独重跑WPF全量，再独立增量构建MachineApp，最终 **0错误/535警告**（opencv-real-caliper-machineapp-final.log）。没有关闭VS/Workbench进程、强制Shutdown、改SDK/断言/基线求绿。
- 实际入口：dotnet test两个现有Vision测试工程，--artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime'；WPF最终另加--no-build --no-restore --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/real-caliper-diagnostics"。整机沿用MachineApp/MachineApp/MachineApp.csproj及Vision/Tests/obj/machine-architecture-serial-verification隔离产物、串行/no-shared-compilation参数。
- 未取得用户脱敏原图和找线参数，实图精度、标定、真实鼠标/显示器及授权HALCON对照仍未验收。两后端阈值/平滑/插值/拟合不是同一实现，不保证逐像素等价。RequiresRuntime/Skip保留；未连接设备或执行生产。


## 最新：OpenCV FindLine局部反光拉偏修复

- 用户报告OpenCV找线不准确。代码确认当前实现是Canny → 搜索带过滤 → FitLine，SearchKind仍为EdgeBand，不是真正一维卡尺。未取得用户脱敏原图/当前参数，不能将合成图结果宣称为原图验收，也没有给搜索带画假卡尺阵列。
- 先在FindLineSearchBandTests构造解析边界（灰度0→128）和旁侧35像素长的局部反光块（255），不依赖生产图像。原L2拟合的3个水平/倾斜/反向用例全部失败，已观测端点法向误差约8.485/8.933像素（%TEMP%/opencv-findline-reflection-before.log）。这是单一长目标边缘旁的短干扰，不是两条同等候选边的选择策略。
- 最小生产改动仅在Tools/OpenCv/Measurement/MeasureTools.cs：FitLine的L2改为Welsch并更新注释，降低远离主边界的干扰点权重；未更改Canny、搜索带筛点、阈值/几何参数、DTO/JSON、端点顺序、结果字段或输入所有权。不新增配置、依赖、通用拟合框架。拟合方法变化是本次精度修复的一部分，旧工程仍可读，但重新执行不保证与旧L2逐像素相同。
- 首轮找线定向10项通过（opencv-findline-reflection-after.log）。随后扩展为6个水平/竖直/倾斜/反向反光回归用例，对起终点分别按解析边界计算法向误差并保留≤1.5像素阈值；同时断言端点顺序、边缘点计数下限及原像素不变。没有按新实现反算期望或放宽旧测试。Canny整数定位误差仍在，不宣称亚像素测量精度。
- 最终非Runtime逻辑全量 **350通过**（opencv-findline-accuracy-logic-full.log）、WPF全量 **286通过**（opencv-findline-accuracy-wpf-full.log）。沿用dotnet test两个现有Vision测试工程，--artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime'；WPF另加90s blame诊断到Tests/obj/findline-accuracy-diagnostics。
- MachineApp沿用隔离产物串行构建 **0错误/701警告**（opencv-findline-accuracy-machineapp.log）；未整改无关警告。HALCON算法/SDK未执行或修改，RequiresRuntime/Skip不变。公开OpenCV文档检索受搜索服务限流，结论以真实算子回归及代码证据为准，不声称已检索成功。
- 仍需用户脱敏原图和LineStart/LineEnd、BandHalfWidth、CannyLow/CannyHigh参数，检查参考方向、是否包含多条长边以及目标极性/选边要求。Welsch不能代替明确选边、亚像素定位或真正卡尺采样；真实卡尺实现及兼容策略需另行确认，未在本轮暗中改变算法产品形态。


## 最新：离线主题/DPI/右键与保存验收

- 本轮仅在BrowserVisualStyleTests补充2个宽度参数化STA用例，不修改生产代码、不新增测试工程或宿主。复用既有WorkbenchAppFixture及只用于布局的PreviewTool；其执行/加载方法会抛出异常，测试不执行算法、模型或SDK，不启动MachineApp或连接设备。
- 每个宽度900/1300覆盖浅色/深色和100%/125%/150%/200% DPI，共16组组合。用VisualTreeHelper.SetRootDpi更新测试Window及菜单Popup的根视觉，读取GetDpi确认传播；这是WPF布局/渲染模拟，不改Windows显示设置，不冒充真实显示器跨屏WM_DPICHANGED验收。
- 使用宿主ThemeService.Initialize切换并在finally恢复原主题/颜色和窗口DPI，不持久化主题配置。检查实际背景随深浅变化、前景明暗方向、预设项高度、菜单图标边界与图像栏不覆盖像素。
- 打开真实ContextMenu Popup，检查7项可见/启用且位于菜单边界内；通过原路由点击1:1验证同一视口。此方式不代替物理鼠标触发ContextMenuOpening的验证。既有VisionImagePanelTests继续验证右键命令、PNG/TIFF的Mono16原始尺寸/像素往返及无图拒绝保存。
- 每组保存页面截图、菜单截图和独立底图导出，合计48个本地合成图像产物：%TEMP%/vision-offline-ui-acceptance/。底图按640×480 Mono8逐字节核对，确认主题、DPI、视口1:1操作不改变原始像素或输入对象。读取复核900宽200%深浅页面和深色菜单截图，未见文字/图标裁切；截图为合成棋盘，不含生产图像，不上传外部。
- 首轮新增用例 **2通过**（%TEMP%/vision-offline-theme-dpi-first.log）；最终非Runtime WPF全量 **286通过/0失败**（vision-offline-ui-wpf-full.log）。实际命令：dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/offline-ui-diagnostics"。目标diff --check通过。
- 本轮只改测试和此记录，未重复执行逻辑全量或MachineApp构建；之前344逻辑通过及整机构建结果为各自历史证据，不相加为本轮数量。RequiresRuntime/Skip保持，授权SDK验收未执行。

### 仍需人工确认（未标记通过）

在已确认不会连接硬件的离线Vision工作台中，使用非生产图像：

1. 物理右击打开菜单，核对无图时禁用、加载/清空后状态更新，绘制时右键仍用于确认而不弹菜单。
2. 打开系统“另存为”对话框，分别选择PNG/TIFF，检查取消不改输入/结果，覆盖确认和不可写路径错误提示可见；不要选生产文件做覆盖测试。
3. 将窗口在真实100%/125%/150%/200%不同缩放显示器间移动，确认窗口尺寸、菜单弹出位置、图标与文字清晰度及绘制前后画布稳定。模拟DPI通过不替代此项。
4. 实际切换深浅主题及主辅色，检查右键、ROI句柄/结果标注对比度和保存后重开图像。当前合成截图只能证明已检查组合，不能泛化为全部机型/显示器验收。


## 最新：删除DisplaySingleView无效Handle占位

- 检索Software Frame及MachineApp的C#/XAML/配置/脚本：DisplaySingleView.Handle/HandleProperty仅有声明/访问器/注册，没有仓库调用、绑定或反射消费者。旧CLR属性为HObject，DP注册类型却为HWindow；新显示器不创建原生窗口。删除这3处声明/实现，不新增返回null的假兼容层或无效句柄。
- 明确公开API退役：仓库外插件如引用Handle需迁移到ImageDisplay.ImageViewport；不能保证未提供的外部二进制插件兼容。DisplayImage、Title、托管显示入口、截图和清屏API及图像所有权保持不变。修改前单文件备份位于WPFControls/Tests/obj/display-handle-backup/20261022-201604。
- 复用现有串行STA显示迁移用例，对3页增加CLR属性、公开静态字段及DependencyPropertyDescriptor均不存在Handle的断言；保留原加载/卸载、原生窗口缺席、右键、截图物理尺寸/像素和清屏绑定检查。修改生产代码前测试 **0通过/1失败**，实际旧属性为HalconDotNet.HObject Handle（%TEMP%/display-handle-retirement-before.log）；删除后迁移/架构/公共装配/导航定向 **26通过**（display-handle-retirement-after.log）。
- 实际测试入口：dotnet test "Software Frame/WPFControls/Tests/WPFControls.Tests/WPFControls.Tests.csproj" --no-restore --artifacts-path "Software Frame/WPFControls/Tests/obj/display-retirement" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false --filter 'FullyQualifiedName~DisplayImageMigrationTests|FullyQualifiedName~ArchitectureDependencyTests|FullyQualifiedName~PublicControlsCompositionTests|FullyQualifiedName~NavigationCompatibilityTests'。
- MachineApp按既有Vision/Tests/obj/machine-architecture-serial-verification隔离产物串行构建，**0错误/693警告**（display-handle-retirement-machineapp.log）；目标diff --check无空白错误。没有顺手整理其他属性、命名或编译警告。
- 本轮未修改Vision算法/控件源码，未重复跑Vision逻辑/WPF全量；上轮344/284全部通过为上一轮证据。SDK、设备及物理鼠标/主题/DPI验收未执行，RequiresRuntime/Skip不变。显示迁移的已知代码收尾项已处理，不继续将“继续”扩展为未授权的SDK/硬件操作。


## 最新：收敛5项WPF布局/旧入口测试失败

- 先用既有最小用例复现 **0通过/5失败**（%TEMP%/vision-five-regressions-before.log）：三个预设项实际25.24而不是28–36，900/1300宽度下绘制使画布变化6DIP，两个Blob用例仍寻找已删除的结果页范围折叠区。没有先降低断言或删除失败用例。
- 图像预设：VisionImageToolbarView三个ListBoxItem本地MinHeight=10覆盖了VisionSelectionTab的MinHeight=32；仅移除3个本地覆盖，继续用同一共享样式。没有改字号/字重、固定外框裁切或放宽原28–36断言。
- 底栏：VisionImagePanel状态区原最小28、绘制区最小34，Auto行按显隐变化导致6DIP跳动。统一由父Grid预留34DIP，移除绘制区重复的最小高度；状态文字仍单行省略和完整Tooltip，独立无会话显示仍整行Collapsed。保留原画布前后变化≤1DIP断言及900/1300两种宽度。
- Blob：当前参数页共享元数据面板已经提供Ranges表，结果页不应再有第二份范围编辑UI。将原测试入口迁至ParametersPanel的真实DataGrid单元格，新增原Editor/Ranges/行对象同一性、可见性、执行前结果列和无重复结果页编辑器检查。保留原DTO下限回写、真实OpenCV宽10/下限12筛空与下限8筛回、结果互选/逐斑点测量断言。
- BlobFeatureFilterView仍是保留的独立兼容控件，因此原本次最小/最大、筛空无观测值和实测单元格文本10检查迁到测试内独立窗口，继续借用同一Editor和实际Results.Items；不把旧结果页折叠区恢复到产品。删除仅服务旧折叠动画的WaitForExpandedContent计时等待，不加Sleep、全局FindName兜底或新生产视图。
- 原5项定向 **5通过/0失败**（vision-five-regressions-after.log）。最终逻辑全量 **344通过**（vision-five-regressions-logic-full.log），Vision WPF全量 **284通过/0失败**（vision-five-regressions-wpf-full.log），合计628个非Runtime用例通过，不包含上轮WPFControls26项的重复累加。此前5项失败现已收敛，不再作为当前遗留失败。
- 实际命令（仓库根执行）：dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime'；WPF入口为"Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj"，使用同一产物路径/过滤，并加--blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/five-regressions-diagnostics"。
- MachineApp隔离构建 **0错误/701警告**（vision-five-regressions-machineapp.log），沿用--no-restore --artifacts-path Software Frame/Vision/Tests/obj/machine-architecture-serial-verification -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false。警告计数按本轮实际输出报告，不等同于警告整改。
- 仅改两个图像区XAML及Blob回归测试；未改算法、参数默认值/持久化/所有权，也未删除Handle或执行SDK/真实设备验收。RequiresRuntime/Skip保留。物理鼠标、主题/DPI等仍需人工验收；离线全绿不替代这些验收。


## 最新：旧HALCON显示实现退役清理

- 三个WPFControls显示页迁移后，再次检索仓库C#/XAML/项目/配置/脚本，HSmartWindowView、HSmartWindowHelper、Ctr_ROI只剩定义互引和项目资源声明，未发现业务消费者。删除Compatibility/Halcon下5个实现文件及Compatibility/Resources/Halcon/View下2个控件转发资源，同时移除csproj对应2个Resource声明。没有保留空控件壳、假原生句柄或全局资源兜底。共7个文件；删除前备份在Tests/obj/retired-halcon-ui-backup/20261022-194438。
- 这是明确的退役边界：上述3个旧公开类型及Ctr_Halcon/View/{Ctr_ROI,HSmartWindowView}.xaml不再提供。仓库外插件若引用它们须迁移到新控件，不声称保留这些源码/资源API兼容。其他ToolDebug兼容类型、工程格式、持久化身份和算法不变。
- 保留宿主仍使用的Ctr_Halcon/View/ResourceDic .xaml及Compatibility/Resources/Halcon/LegacyHalconResources.xaml → Themes/LegacyHalconResources.xaml转发层级；没有直接改宿主URI、合并层级或主题样式。Tools/Halcon和HObject转换/截图API仍保留，未删除SDK依赖或伪造能力。
- Controls架构扫描移除Compatibility/Halcon豁免，扩展为全部C#/XAML并剥离XML注释；保持目录存在性、禁止项和防空扫描下限，增加HWindow/HSmartWindow禁止项。新增STA用例验证旧类型不导出、旧控件资源不打包，旧主题通过实际Application.LoadComponent仍加载并保留FocusVisual和ScrollViewer样式；没有只检查文件路径存在。
- 逻辑全量 **344通过**（%TEMP%/vision-native-ui-retirement-logic-final.log）；WPF命名/主题兼容/装配/图像面板/视口定向 **28通过**（vision-native-ui-retirement-wpf.log）。
- WPFControls迁移/公共装配/导航/源码架构 **26通过**（wpfcontrols-native-ui-retirement-architecture.log）。将该次隔离产物放在WPFControls/Tests/obj/display-retirement，旧定位规则即可找到源码；上轮10项定位阻塞已消除，没有修改定位器/生产逻辑/基线/断言。
- MachineApp最终隔离构建 **0错误/704警告**（vision-native-ui-retirement-machineapp.log）；警告计数与构建涉及的项目/增量范围有关，不把上轮377误报为本轮结果。目标diff --check无空白错误。
- 实际入口：dotnet test Vision.Tests（Category!=RequiresRuntime）、dotnet test Vision.Workbench.Tests（ControlsNamingTests/WorkbenchCompositionTests/VisionImagePanelTests/VisionViewportTests）、dotnet test WPFControls.Tests（DisplayImageMigrationTests/ArchitectureDependencyTests/PublicControlsCompositionTests/NavigationCompatibilityTests）；均使用上述obj隔离产物。MachineApp沿用--no-restore --artifacts-path Vision/Tests/obj/machine-architecture-serial-verification -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false。
- 本轮没有重跑Vision WPF全量；上一轮278通过/5失败为历史结果，那5项尺寸/范围区约定未修改，不能据本轮定向通过宣称全绿。新增RequiresRuntime用例继续Skip；未启动生产、连接设备或做SDK/人工主题/DPI验收。


## 最新：WPFControls三个显示页面替换

- DisplaySingleView、DisplaySingleView_DP、DisplaySingleGroupView的XAML改为直接组合VisionImagePanel，不嵌入编辑器或操作栏，不创建Session。保留Title和HObject DisplayImage入口；公开ImageDisplay以便直接设置/绑定托管VisionImage。清除这三页的HSmart/HalconWindow调用，原生图像不由页面Dispose。旧Compatibility/Halcon类型/pack URI仍保留，不删除算法目录或修改宿主主题入口。
- 复用HalconImageConverter：ToVisionImage复制Mono8、Mono16或平面RGB转交错RGB；拒绝图像元组、其他通道/类型，不悄悄截断或降位深。转换失败先清旧显示再传播异常。新增FromDisplaySnapshot仅供显示截图转换，FromVisionImage算法入口保留Mono8限制，避免把显示能力冒充算法能力。
- DP页Clear清视口但不破坏DisplayImage绑定或释放源。新增CaptureViewport返回冻结、按DPI物理尺寸的视口截图，不触发SDK；旧GetDisplayImage经显示快照转换返回独立HObject，仍含视口变换，调用方Dispose。没有把原图冒充视口截图。无会话的图像面板隐藏空状态栏，去掉画布180最低行高，适配多图小单元格；编辑会话的状态/绘制布局保持。
- 在现有WPFControls.Tests增加一个串行STA集成用例，使用实际宿主资源但不启动Prism/生产：依次验证三页无原生窗口/编辑器/操作栏、上下文隔离、共享输入、右键平移、160高布局与卸载重载；同时验证DP截图尺寸/内容（2×2白图保持1:1而非拉伸）、清屏不改借用像素及源绑定。Application仅为加载真实资源创建一次；不新增生产测试宿主或依赖。
- WPFControls迁移/公共装配/导航定向 **16通过**（%TEMP%/wpfcontrols-vision-display-focused.log）。另一次扩展架构测试结果16通过/10失败：10项统一由旧FindProjectDirectory仅搜索祖先/WPFControls/WPFControls.csproj导致，当前Vision/Tests/obj隔离输出无法定位；未改定位器、基线或断言，本轮架构源码扫描记为阻塞（wpfcontrols-vision-display-migration-final.log）。
- 首轮逻辑343通过/1失败，现有HALCON契约测试发现显示格式扩展意外扩大算法输入并使该离线用例进入原生路径；未涉及设备。已分离显示转换入口，恢复Mono8约束，未弱化测试；最终逻辑 **344通过**（vision-display-migration-logic-final.log）。
- 最终Vision WPF全量 **278通过/5失败/283**（vision-display-migration-wpf-final.log），仍为此前图像选项25.24高度、底栏6DIP变化和已移除Blob范围区的旧测试，未新增失败。MachineApp最终构建 **0错误/377警告**（vision-display-migration-machineapp-final.log）。
- 新增4种奇数宽快照往返/通道顺序/位深/借用所有权/源释放后像素稳定的RequiresRuntime用例，保持Skip，未做授权SDK验收。HObject输入桥、原生截图返回、特殊HALCON类型及主题/DPI/物理鼠标仍需人工验收；不把托管测试描述为真实SDK或设备验收。


## 最新：图像显示/操作拆分与右键功能

- 本轮按最新需求先拆分图像区；WPFControls三个旧HALCON显示页的迁移暂停，未修改它们、原生图像入口或SDK生命周期。
- VisionImagePanel移出顶部操作栏，仅保留现有VisionImageViewport、状态/绘制提示和右键菜单；新增VisionImageToolbarView直接声明原固定控件，通过ImagePanel属性借用显示区。VisionToolEditorView负责上下组合，Browser/Editor公开ImageToolbar转发。没有新增ViewModel、接口、图像副本或控件工厂。
- 右键提供放大/缩小、1:1、适应图像、平移、选择/编辑、另存为；放大/缩小和滚轮共用原ZoomAt算法与0.01–100范围。无图像时禁用，绘制时不打开菜单，保留右键确认。PNG/TIFF保存当前底图原始像素大小，不含叠加/缩放；无图像不创建文件，异常可见，取消不改输入/结果。
- 新增4个STA用例：独立显示不含操作栏、右键各操作调用同一视口且不改输入/ROI、重载借用状态、PNG/TIFF输出Mono16像素逐字节往返（3×2输出而非20×20输入）、无图像拒绝写文件。现有测试按ImageToolbar的明确Namescope定位顶部控件，保留实际参数/ROI/图层/执行/所有权断言，不加FindName全局兜底。源码固定选项检查移到新操作栏文件。
- 定向19项通过（%TEMP%/vision-image-split-context-save.log）；逻辑全量344通过（vision-image-split-logic.log）。最终WPF全量 **278通过/5失败/总283**（vision-image-split-wpf-final.log），新增功能和Scope迁移无剩余失败；5项仍为当前图像选项高度25.24与旧28–36约定、状态栏28与绘制栏34的6DIP变化、当前页面已移除Blob范围区的旧测试。未更改这些布局或降低断言以求绿。
- MachineApp/共享控件独立套件本轮未重跑；未启动生产、安装Runtime或执行真实SDK，RequiresRuntime/Skip保留。物理右键、文件对话框、主题/DPI仍须人工验收。


## 最新：Controls 代码删除、XAML收敛及职责梳理

- 范围只限Vision.Controls与对应测试/说明。仓库源码、XAML、配置及注册检索确认Ctr_PropertyClassList（含PropMeta/GroupedProps）和PropertiesConverter无消费者，删除两份源文件，约570行；保留仍有注册/旧资源URI/调用的类型。原文件备份仅在Tests/obj/controls-cleanup-backup隔离目录，不改变Git暂存区或无关改动。
- ToolDetailsView.xaml从578行收敛至458行：结果表共同配置、关联选择/事件和结果文本各用一份页内样式；列仍直接声明，合并冗长列属性排版；移除结果页的无作用Grid/空行与重复TabItem属性。保留真实异构结果模板、原列/测量语义、Blob Card及用户设置的80行高，没有新增控件/接口/项目。
- 删除已经不存在的退役文件Compile/Page排除项。VisionImagePanel直接引用既有ItemContainerStyle，删除空派生样式与未使用xmlns。操作栏中的GridSplitter本来位于WrapPanel且不能调整列宽，改为静态分隔线，真正的列宽分隔条保留。
- 排查发现ShowProjectToolbar仍是公开属性，但当前XAML已删除ProjectActions容器导致开关失效。用一份ProjectFileButton样式控制四个工程按钮的可见/可用状态；测试保留按钮显隐、命令身份、旧节点隔离与同一实例断言，不再硬依赖内部WrapPanel。工程按钮间距改为0,0,4,0，保留原4DIP按钮间隔并修复首按钮向右偏2px；按钮尺寸不变。
- README由多轮互相冲突的“当前/最新”记录改为当前架构图、职责、所有权、XAML维护规则和接入链接。历史留在本验证记录及已有计划；FILE_LAYOUT中两份删除源文件明确标注。
- 关键结构维持：Views→ViewModels/Session→Workspace；文件协调与工程事务仍属Controls；Presentation负责Tools/Core结果转换；Core不引用WPF/Controls；旧HALCON显示仅兼容区。可替换工具/文件委托/借用会话的测试缝保留。未在本轮重造架构或强制去掉必要事务/回滚。
- 已执行：逻辑全量 **344通过**（%TEMP%/vision-controls-cleanup-logic.log）；重点STA **42通过**（vision-controls-cleanup-focused.log）与最终编辑器/结果/布局 **30通过**（vision-controls-cleanup-composition-final.log），两组有重叠不累计。未新增测试项目或框架。
- 最终WPF全量完整结束 **274通过/5失败/总279**（%TEMP%/vision-controls-cleanup-wpf-final.log）：1项图像选项实际25.24而旧断言28–36；2项状态栏当前MinHeight28、DrawingActions MinHeight34导致画布变化6DIP；2项测试仍查找BlobRangesExpander/BlobFeatureFilter，但本轮修改前备份即已无这两个控件。保留当前用户布局与原断言，没有恢复已移除UI或放宽断言以求绿。首轮全量尝试曾420秒超时，最终有界诊断运行已完整结束，不能把旧成功计数当本轮全绿。
- MachineApp首次隔离串行构建420秒超时，已编到WPFControls；随后同一隔离输出--no-restore增量构建 **0错误/377警告**（%TEMP%/vision-controls-cleanup-machineapp-incremental.log），证明仓库消费者可编译，不等于清除警告。未启动生产、强停VS/Workbench、连接设备或执行真实SDK。RequiresRuntime/Skip保持，人工主题/DPI/鼠标及授权SDK另行验收。


## 最新：菜单裁切与共享DataGridView复用

- VisionImagePanel图像栏改为Auto高度；Menu移除固定外框，顶层MenuItem显式Height=Auto并保留少量Padding。主题默认固定高度不再压缩图标，图标从20调整为24也按内容测量；不更换菜单模板或新增弹出逻辑。原ROI/结果/中间图勾选绑定保持。
- Blob结果页直接声明WPFCustomControls.Controls.DataGridView；BlobResultsGrid改为所选特征列的附加属性助手，仅按每个表格保存自己的动态列和集合订阅。Loaded连接、Unloaded退订，保留原数据、固定测量列、缺失值、排序/行画布互选、冻结ID、只读单选及Recycling，不引入DataGridService或复制结果集合。
- 直接跨程序集继承带XAML的DataGridView已通过失败用例证实不可用，最终直接复用控件本体。共享控件原SelectionMode=Extended的本地值会压过模板调用方的Single；仅将多选及Standard虚拟化默认值移至自身样式，默认仍为Extended/Standard，调用方Single/Recycling可正常生效。
- 最终相关STA **9通过**：900/1300宽度验证图标完全落在菜单内、菜单完全落在图像栏内、五个展开项实际可见及原勾选；真实OpenCV Blob测量/动态列/互选/卸载停止观察及重载恢复、共享默认值与调用方覆盖。日志`%TEMP%/vision-menu-shared-grid-final-related.log`。
- 共享WPFCustomControls全量 **120通过**，日志`%TEMP%/vision-menu-shared-grid-library.log`。较宽的Vision定向曾20通过，之后当前编辑器工具栏边距同步修改，扩展回归为19通过/1对齐失败（图像左侧203、首按钮205）；未覆盖这些无关布局或修改对齐断言，日志`%TEMP%/vision-menu-shared-grid-final.log`。本轮未宣称Vision全量通过，未跑MachineApp或真实SDK/人工主题DPI。临时DEBUG-grid诊断已删除。


## 最新：Vision 简洁性检查与局部等价简化

- 检查范围：AI、Tools、Vision.Core、Vision.Controls、Vision.Workbench及现有测试；排除bin/obj/artifacts/.vs，静态筛查11个csproj和360个C#/XAML/项目文件，重点阅读界面/注册/执行/存储与较大旧实现。不是逐行安全审计，也不以旧知识图谱的零引用判断死代码；索引未覆盖新目录，结论以磁盘源码为准。
- 仅简化5个生产文件的6处：RoiEditorView.xaml直接声明五个固定字符串选项，保持SelectedItem和原字符串绑定；AIToolBase.UnloadModel与HalconAIProvider.Release移除仅重抛的catch；ToolBus.TypeCompat.IsCompatible直接返回现有可赋值判断；VisionPropertyPanel直接调用已有编辑权限帮助方法并去掉空对象初始化器。未新增类型、接口、项目、配置开关或调度机制，未改算法/JSON/公开API。
- 两处关闭仍在成功后才清状态，失败自然上抛，不改变原生资源所有权：

| 对象 | 拥有者/关闭入口 | 本轮保持 |
| --- | --- | --- |
| AI模型会话 | AIToolBase，经原_sessionOwner.CloseSession关闭 | 失败保留_session与归属，成功再清字段 |
| HALCON模型 | HalconAIModelSession.Release，经ClearDlModel关闭 | 失败保留_model和加载态，成功再清字段 |

- 保留必要机制：ToolDetailsView结果模板对应不同结果类型；属性反射对应运行时DTO；工具/Provider目录来自宿主；工程准备/提交及执行revision保证失败恢复、迟到结果和所有权隔离，不以“少代码”为由删掉。
- 旧ToolBus/ToolMge端口、PairedCollections双向订阅和旧SDK兼容界面涉及历史行为/生命周期，仅记录后续审查点，不在本轮合并框架、删除旧API或重写同步逻辑。
- 复用现有RoiInteractionTests补充固定选项、双向选择、改类型不添加ROI的行为检查；原显式添加/角度测试保留。中间曾以ComboBoxItem替代字符串，完整测试暴露类型契约不一致，最终改为直接sys:String以保持原SelectedItem类型，没有修改原测试期望。
- 已执行：AI定向25通过；最终ROI/原显式添加/工程操作定向12通过；全量非Runtime逻辑 **344通过**（vision-simplicity-all-logic.log）。全量WPF **277通过/1失败**（vision-simplicity-all-wpf-bounded.log）。
- 唯一WPF失败为BrowserVisualStyleTests.RegionHeadersAndSelectionTabs_AreCompact_AndRenderAnOfflinePreview：当前工作区VisionImagePanel头行Height=30、条目MinHeight=10，条目实际25而原断言要求28–36；这些布局值不属于本轮修改，保留当前布局与原断言，待确认尺寸约定。没有宣称全绿。完整脚本因此未通过；另一次不带有界挂起诊断的WPF全量命令曾420秒超时，最终带90秒单测挂起收集器完整结束，无挂起dump。
- 日志均在%TEMP%。Workbench构建0错/8警告；MachineApp/共享控件独立套件本轮未跑；RequiresRuntime/Skip保留，未执行真实ClearDlModel、连接硬件或安装Runtime。SDK、人工鼠标/主题/DPI与生产未验收。


## 最新：直接XAML按钮与固定图像选项

- VisionToolEditorView.xaml直接声明五个有名字的Button：OpenProjectButton/SaveProjectButton/SaveProjectAsButton/LoadImageButton/RunButton。移除ContentControl和DataTemplate切换；文件组与执行按钮用Visibility控制。浏览页不再额外创建工程工具栏，只组合工具箱与同一个编辑器，仍严格断言整个页面只有一个执行按钮。
- 原ProjectToolbarView保留旧资源兼容入口，其执行按钮也改成直接声明。VisionToolEditorView的已有d:设计尺寸、命名空间和用户调整的边距保留。
- VisionImagePanel.xaml直接声明三个ListBoxItem，Content为输入图像/ROI图像/结果图像，Tag为原ImageViewKind枚举，SelectedValuePath=Tag双向绑定SelectedImageView。不再用ItemsSource/DisplayMemberPath生成固定选项，无新转换器；原ImageViews/ImageViewOption公开API保留兼容。
- 布局/装配测试按新的编辑器Namescope定位TopMenuBar，保留按钮顺序、命令身份、唯一执行、窄窗/拖动后图像对齐、外观插槽、工程保存/重开及所有权的严格断言。隐藏/恢复改验证同一组按钮实例的可见性与旧命令解绑，不要求Visibility折叠的控件从视觉树销毁。
- 新增2项源码架构断言：按钮直接位于面板且不含ContentControl/DataTemplate、浏览页不建第二套工具栏；图像选项为三个固定XAML条目且没有ItemsSource。STA验证直接选中ROI条目写回VM，VM切结果预设回选对应条目，原图层/画布行为保持。
- 最终完整脚本退出0：定向逻辑/WPF35/198，全量非Runtime **344/278，共622通过**；Workbench构建0错/8警告。日志`%TEMP%/vision-editor-direct-xaml-full.log`。MachineApp/共享控件独立套件本轮未重跑；未连接设备或安装Runtime，保留RequiresRuntime/Skip。真实SDK、物理鼠标、主题/DPI及生产仍未验收。


## 最新：单工具编辑入口包含可选工程工具栏

- VisionToolEditorView在会话提供IVisionProjectCommands时显示打开、保存、另存为、导入图像与执行，直接复用ProjectToolbarView与原Controls Workspace命令/工程包事务。ShowProjectToolbar默认true；无工程能力的节点借用会话不创建工程按钮，也不构造目录/第二个工具。
- 完整浏览页显式关闭内层ShowProjectToolbar和ShowExecutionButton，外层工程按钮对齐方式不变；独立入口可提供ToolbarAccessory。工程工具栏内执行按钮按需实例化，关闭时不留下隐藏按钮。工程文件格式、算法/参数、输入和工具释放规则未改。
- 新增独立入口真实保存/原路径重存/另存为/重开回归，确认没有ToolCatalogView、不隐式执行；取消保持路径，输入/参数还原。900/1300宽度验证对齐、按钮可见、隐藏/恢复与外观插槽、唯一执行、切到借用节点后旧工程消息隔离、重载不创建工具及Dispose只退订。
- 红验证日志`%TEMP%/vision-editor-project-toolbar-red.log`：新增独立入口用例因缺少ProjectToolbarView而失败。中间切换测试的状态初值预期曾不符合既有卡片状态显示规则，修正为切换后状态快照并保留旧通知隔离的严格断言，未改生产状态行为。
- 最终完整脚本退出0：定向逻辑/WPF35/198，全量非Runtime **342/278，共620通过**；Workbench增量构建0错/0警告。日志`%TEMP%/vision-editor-project-toolbar-full.log`。MachineApp/共享控件独立套件本轮未重跑；真实SDK、物理鼠标、主题/DPI和生产未验收。保留RequiresRuntime/Skip，不安装或连接硬件。


## 最新：Controls 规范命名与职责目录

- 79个原文件按职责迁移到Views、ViewModels、Models、Services、Presentation、Contracts、Compatibility等子目录，转换器统一到Converters。主类型改为VisionToolEditorView/Session/Model，多选编辑器为VisionMultiSelectComboBox；拆分契约/三套适配器、描述/模型、参数行/枚举、工程存储接口等混合文件，不改算法/DTO格式。
- 保留公开CLR命名空间，旧ToolDebug*与Ctr_*类型、构造器及完整编辑器旧访问器留兼容桥；新旧导航名都可用。旧View/Ctr_Halcon等资源URI通过轻量XAML资源转发，主视图在新目录编译为BAML。旧浏览页实际LoadComponent测试通过。
- 资源整理曾暴露隐式样式作用域问题；通过受限测试宿主dump定位为资源查找，保留宿主原主题URI及合并层级后恢复。没有强停用户Workbench/VS；诊断项目、dump与迁移清单仅在Tests/obj隔离目录。并未通过关闭SDK守卫、忽略启动异常或强制Shutdown掩盖问题。
- 架构守卫迁移到Views/Contracts及Compatibility/Halcon新边界，保留原SDK禁止项与扫描下限；新增目录/无空格文件名/契约独立断言。新WPF用例覆盖4个规范类型、旧别名状态复用、6个新旧URI和旧浏览页加载；原参数/工程/ROI/结果/生命周期回归保留。
- 最终完整脚本退出0：定向逻辑/WPF35/195，全量非Runtime **342/275，共617通过**。增量Workbench0错/0警告不等于清理全部既有警告；日志 `%TEMP%/vision-controls-naming-full.log`。
- 最后仅整理空行、源文件说明和文档后，新旧名称/URI及编辑器组合16项再次通过，日志 `%TEMP%/vision-controls-naming-final-focused.log`。
- MachineApp隔离串行构建0错/**706警告**，未整改或屏蔽警告；日志 `%TEMP%/vision-controls-naming-machineapp.log`。未启动生产、连接设备或安装Runtime；RequiresRuntime/Skip保留。真实SDK、人工主题/DPI/鼠标及IDE热重载未验收。
- 目录及迁移表：[FILE_LAYOUT.md](../Vision.Controls/docs/FILE_LAYOUT.md)；接入说明：[TOOL_EDITOR_INTEGRATION.md](../Vision.Controls/docs/TOOL_EDITOR_INTEGRATION.md)。本轮不新增工程或流程调度器。


## 最新：完整编辑器与无目录单工具调试入口拆分

- `VisionBrowserView` 在Controls中保留工程工具栏和工具箱，内部组合 `ToolDebugView`。打开/保存/另存为、工程编解码及存储不移至Workbench；Workbench仍只提供工具注册、资源和对话框/图像服务。ImagePanel/DetailsPanel旧访问入口转发至组合子视图。
- 提取 `ToolDebugModel` / `ToolDebugSession`：没有ToolEntry、目录、工程路径或文件存储要求；`VisionBrowserPageModel` / `VisionBrowserViewModel`继承公共调试能力后仅增加目录/工程操作。图像与详情视图改为识别通用调试会话，工具身份显式提供，不依赖SelectedEntry。
- 单工具界面复用现有VisionImagePanel、ToolDetailsView、属性编辑器、ROI及逐斑点结果表。宿主提供原卡片/DTO/图像/ROI/结果，会话不调用工具工厂、不在打开时重新设置输入或执行；卸载不释放会话/工具。会话Dispose只清自己的订阅和属性适配，宿主已提供的适配也不覆盖或释放。
- 比较/Benchmark提为可选DiagnosticsContent插槽，独立调试不需要这些命令。调试执行按钮按模板按需实例化；完整编辑器关闭该入口后不留隐藏重复按钮，仍只有工程工具栏中的一个执行入口。
- 新增4项ToolDebugCompositionTests：无目录实例化及原对象复用、重载/释放边界/参数写回、换节点退订、完整编辑器组合与工程命令、借用已有属性适配。原34项定向先通过；扩大回归发现3处重复执行按钮定位失败，改为不创建隐藏按钮后原交互断言通过，未降低断言。
- 最终完整隔离脚本退出0：定向逻辑/WPF35/183，全量非Runtime **340/263，共603通过**；增量Workbench构建0错/0警告不代表清理全部既有警告。日志 `%TEMP%/vision-debug-split-full.log`。
- MachineApp隔离串行构建0错/**721警告**，未整改或隐藏警告，日志 `%TEMP%/vision-debug-split-machineapp.log`；没有启动生产宿主、连接硬件或安装Runtime。RequiresRuntime/Skip保留，人工主题/DPI及真实SDK待验收。
- 接入与所有权说明：[docs/TOOL_EDITOR_INTEGRATION.md](../Vision.Controls/docs/TOOL_EDITOR_INTEGRATION.md)。旧ToolEditorView/IEditableTool入口保留兼容，不冒险转换旧HALCON会话。本轮没有新增工程、流程调度器或完成旧流程节点启动器迁移；后续流程适配应直接使用独立调试入口。


## 最新：状态栏只显示当前操作

- `VisionBrowserViewModel.CurrentOperationText` 替代多段历史信息拼接：加载后显示加载反馈；一旦开始执行即显示执行中，完成后显示本次状态、结果和耗时。执行异常同样替换加载提示，不混入旧路径、旧耗时或Viewport的“加载图像开始”/鼠标状态。
- 加载失败、保存等新的工作区消息仍作为当前操作显示；再次加载会切回加载消息。同一路径重载即使ImageInfoText文案相同也会刷新操作选择，取消加载不主动覆盖当前反馈。原ImageInfoText等数据仍保留，不清除诊断或输入元数据。
- 新成功/异常两项WPF用例先红后绿，覆盖执行中、完成、鼠标移动不覆盖、重新加载及相同文案重载。断言在执行调用外验证，避免被工具异常捕获掩盖。
- 本轮非Runtime WPF全量 **259项通过**，日志 `%TEMP%/vision-current-status-wpf-full.log`。只修改Controls状态投影、XAML绑定及回归；未重跑逻辑全量、MachineApp、共享控件独立测试或真实SDK。物理鼠标/主题/DPI仍待验收。


## 最新：画布下方单行状态

- `VisionImagePanel.xaml` 将工具状态、结果摘要、耗时、缩放/坐标、图像及中间图信息合并为单行；不换行，长内容省略，悬浮提示显示完整文本。Run绑定显式OneWay，避免只读Status被默认双向绑定。
- 状态区由72压缩为34DIP预留（正文22DIP）；绘制时同一区域显示单行提示与完成/取消，不挤动画布。新900/1300宽度用例先红后绿，验证长路径、完整Tooltip绑定、绘制切换前后视口高度不变。
- 定向26项通过；全量非Runtime WPF **257项通过**，日志 `%TEMP%/vision-footer-wpf-full.log`。本轮仅XAML布局与WPF测试，没有重跑逻辑全量、独立共享控件或MachineApp；不把上一轮340逻辑计数冒充本轮执行。合成图离线预览已查看，实际鼠标/主题/DPI仍需人工验收。


## 最新：Blob 改为逐斑点尺寸明细，而非以汇总为主

- 主结果表现在一行一个斑点，最前列固定为“ID / 面积(px²) / 外接宽(px) / 外接高(px)”。无需先勾选width/height也能看到两项外接尺寸；其他所选特征列紧随尺寸，中心坐标靠后。宽高从真实区域有效Bounds读取，不把矩形面积当区域面积；缺失几何为“—”，没有标定不输出mm。
- 冻结ID列、压缩本表局部内边距，避免尺寸藏在横向滚动末尾。选择行与画布保持同一结果ID，底部只展示选中斑点的面积、外接尺寸及其特征读数；不再用全体斑点最小/最大值代替逐斑点测量。
- 上轮范围对照移至结果页底部“筛选范围对照（可选）”，默认折叠；展开仍绑定原范围并保留写回/精度/生命周期约定。与新主表语义同步迁移旧断言，不删除动态特征值、范围回写或选择联动的验证。
- 新增真实OpenCV两斑点用例先红（原先前四列是ID/面积/中心X/中心Y），改后逐行验证面积100、24及尺寸10×10、6×4，点击行/画布验证身份和底部详情联动。无效几何不补零也有断言。筛选范围展开测试使用有界Dispatcher就绪等待，保留实际加载/布局/编辑断言，不以固定Sleep规避折叠动画时序。
- 完整隔离脚本最终退出0：定向逻辑/WPF35/175；全量非Runtime **340/255，共595通过**。增量Workbench0错/0警告不代表清理所有既有警告。日志 `%TEMP%/vision-blob-per-item-full.log`。
- 已查看更新后的 `%TEMP%/vision-blob-feature-results.png`：真实OpenCV处理两个合成区域，第二个高亮且详情为24px²/6×4px；不是用户真实图片/物理鼠标验收。算法、参数JSON、搜索域及后端填孔语义未变；真实HALCON、主题/DPI、MachineApp和性能本轮未验收。


## 最新：Blob 结果页常驻所选特征与范围

- 纠正前述判断：真实OpenCV目录路径的动态结果列测试可以通过，尚未证明用户“未显示”的全部原因；不能仅凭假工具或推测断言已复现。此次把已勾选类型提升为结果页独立固定区，不再只依靠横向滚动后的列头。
- `BlobFeatureFilterView` 显示“特征 / 下限 / 上限 / 本次最小 / 本次最大”；勾选后、执行前和筛空时仍可看到特征及限定范围。实测极值仅来自当前通过筛选的结果，不代表全部候选；无读数为“—”，不补造数值。
- 下限/上限通过 `BlobFeatureFilterRow` 代理回写同一个Blob编辑会话及原DTO，修改后清旧结果并要求重执行。上下限编辑不使用三位格式化，避免未编辑的高精度数值因进入/离开单元格而量化；只读实测值按三位显示。摘要表使用局部紧凑单元格与比例列宽，在340宽度可同时看到五列。
- 仅Loaded期间观察范围和结果集合；切页/卸载时取消未提交单元格编辑、退订并释放展示行，重载重新绑定原编辑会话；迟到展示行写入被拒绝，不释放原DTO或工具。结果页编辑继续受IsEditingEnabled忙时守卫约束。本轮没有更改算法或增加HALCON异常吞噬兜底。
- 新增4项WPF回归：真实OpenCV执行及结果值、执行前类型可见/单元格编辑写回/筛空与放宽重执行、两个后端参数会话的卸载重载及高精度保留。宽10的区域在下限12时筛空，放宽到8后实测最小/最大为10，不能把筛空期望误写成仍有读数。旧结果表测试按明确的结果表标识定位，不把新增范围表误当结果明细表。
- 完整隔离脚本退出0：非Runtime逻辑/WPF **340/254，共594通过**；定向35/174为子集。增量Workbench构建0错/0警告，不代表既有警告已清理。日志 `%TEMP%/vision-blob-range-full.log`。随后紧凑样式调整并再次定向4项通过，日志 `%TEMP%/vision-blob-range-final-focused.log`。
- 已生成并查看 `%TEMP%/vision-blob-feature-results.png`，是真实OpenCV对合成方块的离线渲染，不是用户真实图片/物理鼠标验收。HALCON仅参数会话与编译验证，授权SDK执行、主题/DPI及MachineApp本轮未验收；RequiresRuntime/Skip保留。


## 最新：截图反馈五项修复及交互补验

1. 执行状态/结果摘要/输入图像信息移入 `VisionImagePanel` 的画布下方，与缩放坐标/绘制提示共用有界底部区域，不再跨全窗；右侧扩展为工具交互区。
2. `ToolDetailsView.ToolIdentity` 常驻显示目录的完整工具标识（含后端），不随参数/ROI/结果/诊断页切换而消失。参考 [MERLIC 官方 Tool Workspace](https://www.mvtec.com/doc/merlic/5.5/manual/en-us/Content/Creator/User_interface_creator/tool_workspace.html) 的“当前所选工具对应工作区”原则，并非声称复制其视觉布局。Exa请求失败后通过官方网页读取了正文。
3. `BlobResultsGrid` 按勾选特征提供测量列；面积/中心定位列保留，不重复添加area/row/column。特征变更清空旧结果时仍显示空表列头，重新执行后更新数值；只显示通过当前筛选的区域，不是未筛选候选预览。OpenCV导出已有筛选计算的原值，HALCON读取最终区域的region_features；不以框/轮廓近似未提供的测量值，缺失显示“—”。结果行深复制特征字典，防跨执行漂移。phi仍为原始弧度口径。
4. ROI图像优先选择/编辑模式，右侧停留参数或结果页不再阻止ROI操作；切回结果图像且右侧为结果页时恢复结果拾取。观察显示状态在加载时订阅，卸载/更换VM时退订；忙时编辑守卫保留。
5. 上轮确实只统一三只找线手柄。本轮两后端增加四角缩放及橙色旋转柄，复用普通旋转ROI的手势规则；保留端点、法向半宽和整体移动。手势派生外框不加入持久化ROI列表，映射回原始端点/半宽，取消恢复；HALCON端点采样的沿线半宽计入外框但不改变采样参数，OpenCV不伪造卡尺。
- 初始新WPF用例7项中5红2绿，OpenCV测量发布用例先红；最终新增9项WPF（含两个后端的不可变快照）和1项OpenCV测量回归通过。旧布局测试改为新ROI预设优先语义，并保留结果图像恢复拾取断言；未弱化算法、坐标回写或生命周期断言。另新增HALCON测量Runtime用例，保留Skip/RequiresRuntime，未执行。
- 完整隔离脚本退出0：Workbench **0错/8个既有nullable警告**；定向逻辑/WPF35/170；全量非Runtime **340/250，共590通过**，定向不重复计数。日志 `%TEMP%/vision-feedback-full.log`。补充结果单元格真实绑定断言后定向9项再次通过，日志 `%TEMP%/vision-feedback-final-focused.log`。
- 已查看更新后的棋盘/fake工具离线预览，不代表用户真实图像或物理鼠标验收。没有升级FindLine算法、修改历史参数/默认值或搜索域语义；Blob仅补测量值发布，保持现有阈值、填孔及筛选口径。本轮未重跑MachineApp/共享控件独立测试；真实HALCON读数、鼠标/主题/DPI、设备及性能仍未验收。

## 最新：FindLine 统一 ROI 展示/编辑（不升级算法）

- 两套 FindLine 的端点和法向半宽移入 ROI 页，共用 `LineRoiEditorView` / 原 DTO 的属性绑定；HALCON 的沿线采样半宽与卡尺数量也在此页，阈值、极性、平滑、拟合仍在参数页。通过 `ToolPropertyAttribute.Area` 显式分区，不按名称/Backend猜测；未设置分区筛选的旧属性面板仍显示全部字段。
- 进入找线 ROI 页或修改几何时恢复原图 ROI 预设；共用黄色编辑外框、路径、端点/宽度手柄及原图坐标。HALCON 额外保留真实青色卡尺阵列，黄色外框只用于编辑，不替代实际采样域；OpenCV 保留连续 Canny 搜索带，不画虚假卡尺。
- 拖动过程中通过 UpdateTarget 刷新现有几何编辑器，不重建输入控件、不反向回写或取消拖动；结束/取消后保持数值与原 DTO 一致。旧目标回调不清新工具结果，卸载/重载不释放借用会话。
- 新增8项WPF回归和2项派生几何回归；初始4项WPF全红，实施后通过。迁移原 PointEditor 回归到 ROI 页，保留分量回写、其他坐标不覆盖和清结果的断言。两工具源文件本轮仅修改展示元数据，算法体、参数字段/默认值和 JSON 不变。
- 完整命令 `Verify-EditorRefinement.ps1 -Full -ArtifactsPath 'Software Frame/Vision/Tests/obj/editor-verification'` 退出0：Workbench 0错/10个既有nullable警告；定向逻辑/WPF34/161；全量非Runtime **339/241，共580通过**（定向不重复计数）。日志：`%TEMP%/vision-line-roi-full.log`。
- 本轮未重跑 MachineApp 或共享控件独立测试，未运行真实HALCON/AI SDK或生产宿主；RequiresRuntime/Skip保留。自动化验证不等于物理鼠标、实际图片精度、深浅主题/DPI、授权SDK或性能验收。

## 最新收敛：保存/另存为、输入快照工程包与共享外观入口

- 操作栏：打开工程、保存、另存为、导入图像、执行；移除工程/图像/调试菜单、演示图和顶部Benchmark。执行从参数区移至全局操作栏，Benchmark移诊断。适应/1:1/标注仍在图像区，不造停止/采集等能力。
- 保存复用成功打开/保存的路径，另存为始终选择新路径；取消/失败不改路径，弹出路径对话框期间切工具不向新工具提交。旧JSON按旧格式读、相对图像路径按工程目录解析，保存升级要求另存，不静默覆盖旧JSON。
- 新v2 ZIP工程包携带当前内存输入快照，不靠原路径：固定project.json与images/input.bin，保留全部受支持像素格式/Stride/FrameId/Timestamp。Source仅追溯，不读取原文件；Mono16/Float32保持原始字节。现有原始JSON导入/导出接口仍支持v1配置。
- 包读取拒绝重复/缺失条目、未知版本/格式、错误尺寸/长度与像素哈希；清单8MiB、像素512MiB边界防无界读取，不是性能指标。没有解压任意路径。完整生成临时包后替换目标；不宣称断电耐久性。当前只含单工具参数/ROI/视图/输入，不打包模板、SDK模型或运行结果。
- WPFCustomControls新增AppearancePickerView，复用ThemePickerView/ThemeService，不替换宿主DataContext或释放服务。首次打开才创建内部选择器，收起/卸载退订，重开回灌；替换主题服务时不会残留订阅。Vision宿主通过ToolbarAccessory插槽放到最右侧，取消旧独占主题行；内容观察在卸载时退订。
- PortableProjectTests首次7项全红（保存重复问路径/不能恢复无源图像），实现后10项通过，另3项AppearanceEntryTests覆盖控件/宿主/900宽度。更新旧布局测试为新位置与命令，保留参数、ROI、结果、资源所有权等断言。离线预览已查看，不是物理鼠标验收。
- 完整命令 `Verify-EditorRefinement.ps1 -Full -ArtifactsPath 'Software Frame/Vision/Tests/obj/editor-verification'` 退出0：Workbench0错/10个既有nullable警告，定向逻辑/WPF32/153，全量非Runtime337/233，共 **570通过**。共享 `WPFCustomControls.Tests` 在Tests/obj/appearance-verification隔离输出另120项通过，不与Vision计数混淆。
- MachineApp隔离串行构建（原-m:1/BuildInParallel=false/UseSharedCompilation=false/nr:false参数）0错/716个依赖警告；未清理或屏蔽这些警告，未运行生产宿主。RequiresRuntime/Skip保留，不连接设备/安装SDK/强停VS或Workbench。实际鼠标、整机UI、主题视觉、高DPI、授权SDK、IDE热重载仍待分层验收。

## 最新视觉轮：紧凑桌面菜单与统一分区样式

- ProjectToolbarView 新增工程/图像/调试菜单：文件及调试项绑定原命令，适应窗口/1:1通过区域事件连接画布，标注显隐双向绑定。原五个快捷按钮继续展开，不增加新建、撤销或帮助等空菜单。
- Themes/VisionBrowserStyles.xaml 提供30高扁平按钮、32高图像/详情页签与目录标题。图像区继续与快捷操作左对齐、不覆盖图像；目录长名称省略并保留完整提示，ROI列表中文显示，执行/完成保留主要操作强调。选择色/半粗只作用于详情页签标题，不向参数内容继承。
- 新增 BrowserVisualStyleTests 两项，先取得菜单缺失和旧尺寸失败，再验证真实命令、缩放菜单、五个快捷入口、页签/按钮尺寸及正文不继承选中标题粗体。生成棋盘输入与fake工具渲染离线PNG到 `%TEMP%/vision-editor-desktop-preview.png`，已查看渲染结果；不是物理鼠标、授权SDK或真实图片验收。
- 完整 `Verify-EditorRefinement.ps1 -Full -ArtifactsPath 'Software Frame/Vision/Tests/obj/editor-verification'` 退出0：Workbench0错误/10个既有nullable警告；定向逻辑/WPF32/140，全量非Runtime337/220，共 **557通过**。定向为子集，不重复计数。RequiresRuntime/Skip未改变。
- 本轮仅Controls展示与跨区显示事件、测试、文档，不改工程格式/算法/所有权，不新建工程。MachineApp构建沿用下方前轮证据，本轮未重跑；实际深浅主题、高DPI、键盘/鼠标、IDE热重载、设备/SDK仍待分层验收。是桌面视觉工作台首版，不宣称像素级复刻VisionPro或美观已获用户验收。

## 最新架构优化：分区视图与工程准备/提交

实施清单及生命周期表见 [ARCHITECTURE_REFINEMENT.md](../ARCHITECTURE_REFINEMENT.md)，A–E已完成。

- 父视图由约802行收敛为约60行，四个 UserControl：ProjectToolbarView / ToolCatalogView / VisionImagePanel / ToolDetailsView。按钮样式共用 VisionBrowserStyles.xaml，结果模板留在详情区；原属性面板/ROI编辑器继续复用。区域只借用同一VM，不新增工程/Prism模块/Workspace实例。
- 项目JSON与文件存储已提取；保留单参数Workspace构造器并添加显式文件存储注入入口。导入先构造和验证候选参数/工具/Provider/编辑适配，成功发布后释放旧会话；失败清理候选，原状态不替换。候选清理异常保留原准备异常；旧资源释放失败继续回收其它资源并给出诊断。
- 工程v1字段不变，不打包模型或结果；无效ROI选择索引、超过当前工具搜索域容量的ROI明确拒绝，不悄悄丢失。图像缺失仍非致命：保留当前输入并提示。工程保存先完整写同目录临时文件再替换，不宣称断电耐久性或跨文件事务。
- 初始5项隔离测试4红1绿；完成后新增共12项覆盖失败保留状态、候选释放、Provider释放顺序、忙时拒绝、文件读失败、Unicode/编码失败文件保护、四区共用VM、重载及替换VM退订。迁移旧测试到子区域名字作用域，未删除算法/绑定/绘制/结果/所有权断言。
- 最终完整命令 `Verify-EditorRefinement.ps1 -Full -ArtifactsPath 'Software Frame/Vision/Tests/obj/editor-verification'` 退出0：Workbench构建0错误/10个既有nullable上下文警告；定向逻辑/WPF32/138通过；全量非Runtime逻辑337、WPF218，共 **555通过**，定向为子集。
- 整机 `dotnet build MachineApp/MachineApp/MachineApp.csproj --artifacts-path 'Software Frame/Vision/Tests/obj/machine-architecture-serial-verification' -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false` 通过。首次并行隔离输出遇到CS2012占用，未停用户进程；串行完整构建0错/1018警告，补保留原构造器后的最终增量0错/708警告，不等于警告整改完成。仅构建，未启动MachineApp或执行整机/真实设备测试。
- 保留RequiresRuntime/Skip。实际鼠标/主题/SDK/IDE Hot Reload仍未验收；新增C#类型、XAML名字作用域与事件分区需重新构建并重启调试查看。当前只整理架构，不把它称为VisionPro视觉改版完成。下方为历史轮次。

## 最新小步：顶部按钮对齐并展开更多

- `VisionBrowserView.xaml` 顶部采用标题列 + 操作区布局：操作区起点与中间图像区左边界对齐；宽度绑定目录面板的 ActualWidth，拖动目录分隔条后继续同步。不能绑定 ColumnDefinition.ActualWidth 来期待后续更新，其并无同等的尺寸变更绑定通知，回归已发现并修正这一差异。
- 新 VisionProjectButton 样式统一 34 高度、内边距和间距；打开工程/保存工程/导入图像/演示图/Benchmark 直接展开，删除“更多”菜单。全部仍使用原命令，Benchmark 是显式五次执行，不自动运行。
- 更新回归先取得缺少展开按钮的失败，修复后进一步捕获目录宽度从190改为220时顶部未跟随的失败；最终相关10项通过。再执行最新编译产物的非Runtime WPF全量：206通过、0失败（`dotnet test ... --artifacts-path ... --no-build --no-restore --filter "Category!=RequiresRuntime"`）。本轮仅布局，未重跑逻辑全量，不把下方337/206的历史完整脚本结果冒充本轮重新执行。实际主题/鼠标/DPI仍需人工确认。

## 最新修复：图像区布局对齐与单次绘制确认

- 针对截图，图像视图栏不再跨工具目录/参数区，而与显示工具一起收进中间图像区上沿；工具条不再覆盖图像像素。中间区独立采用上沿/画布/提示区三行，窗口较窄时只在本区换行。侧栏宽度调整为工具 190（最小160）、参数 340（最小300），保留分隔条。
- ROI 类型/绘制/添加/删除/清空集中到 ROI 列表上方；RoiEditorView 增加可选 AdditionalActions 插槽，默认调用方无附加操作时不占空间，不重复维护两套类型下拉。列表按内容布局，不再用星号行拉出空白；坐标/尺寸最多显示三位小数，焦点切换不量化原 DTO 值（已有回归）。
- 原缺陷并非矩形未保存，而是鼠标松开后仍处于连续绘制模式：新 RoiDrawingCompletionTests 首次可运行的 8 项中 7 失败，确认模式未退出、右键无提交及 Esc 不退出。统一 CompleteDrawing / CancelDrawing 入口后，矩形/旋转矩形/圆/椭圆松开即确认，自动回到选择模式并显示编辑手柄；多边形支持完成、Enter、双击或右键确认，少点/共线不提交。Esc/取消清除草稿但不替换已有搜索 ROI；隐藏/不可编辑等状态禁止提交。
- Mode 改为控件依赖属性，并公开只读 IsDrawing / DrawingHint 供 XAML 显示明确反馈；这些是显示控件状态，不写入工具参数 JSON。“完成/取消”和提示在图像区底部，与状态区共用最小高度，避免工具覆盖图像及显示提示时挤动绘制区域。
- 布局回归在修复前另取得 3 失败/1 通过；修复后覆盖图像栏与画布左右边界一致、900 宽度下不覆盖画布、绘制按钮与类型集中、页面实际完成/取消按钮、全精度坐标保留。几何输入在部分测试中注入私有草稿，再调用真实鼠标释放/键盘/双击路径或页面按钮；不是物理鼠标验收。
- 完整命令仍为 `Verify-EditorRefinement.ps1 -Full -ArtifactsPath 'Software Frame/Vision/Tests/obj/editor-verification'`，退出 0：Workbench 构建 0 错误/10 个既有 nullable 上下文警告；定向逻辑/WPF 32/126；全量非 Runtime **337/206，共543项通过**。未运行授权 Runtime、生产设备、MachineApp 或实际鼠标/主题/IDE Hot Reload 验收。含 C# 控件状态变化，实际调试建议重建并重新启动，不仅依赖 XAML 热重载；未强停任何运行中的用户进程。

## 前轮布局：常用入口展开，取消独立操作栏

- 顶部直接展示打开工程、保存工程、导入图像；更多下拉只保留已有的演示图和 Benchmark，不新增文件/编辑/视图/工具四套菜单。工程按钮复用现有 ImportProjectCommand / SaveProjectCommand 及工程 JSON，不新增持久化格式，不声称已实现新建、另存为、最近工程、图像导出或模型打包；保存仍由现有路径选择入口决定位置。
- 输入图像 / ROI 图像 / 结果图像继续在顶部下方独占一行。移除画布上方独立 ToolActions 行；执行与按能力出现的加载模型放在参数区。画布内小工具条提供选择、适应窗口、1:1 和标注设置；ROI/结果显隐不改变算法搜索域，中间 ROI 图仅显式打开，不冒充 ROI 原图预设。
- ROI 类型、添加/删除继续在 ROI 页，新增同页绘制入口；没有输入或执行中禁用绘制。点击绘制恢复原图和 ROI 标注并使用所选类型，不预创建几何；画布选择按钮可回到编辑模式，结果页保持直接拾取上下文。模板训练/保存/加载仍在模板页。
- 四项 BrowserActionLayoutTests 先红，覆盖 900 宽度下直接入口可见、图像预设独立一行、画布工具不占布局行、标注绑定、显式 ROI 绘制、原 DTO/ROI 的真实工程文件保存及打开、借用 VM 关闭后的禁用与 Workspace 不释放。第一次修复后其中一项测试错误地从 TabItem 视觉树找内容；改为从它的真实 Content 检查归属，四项通过。既有裁剪测试按新布局先返回参数页再点击执行，保留输出/输入独立性等全部断言。
- 实际执行完整 `Verify-EditorRefinement.ps1 -Full -ArtifactsPath 'Software Frame/Vision/Tests/obj/editor-verification'`，最终退出 0：Workbench 增量构建 0 错误/0 警告，定向逻辑/WPF 32/115 通过，全量非 Runtime 逻辑/WPF **337/195 通过，共 532 项**。定向是子集，不重复累计；本次统计包括工作区原有新增用例，不把全部增长归为本轮新增。最终增量无构建警告不代表清理了既有 nullable 或测试分析器警告。
- 保留 RequiresRuntime/Skip，未启动生产设备、安装 Runtime 或验收 HALCON/AI。自动化布局/绑定/工程往返不等于实际主题观感、鼠标操作或 IDE Hot Reload 验收。下方为前轮历史结果。

## 补充修复：属性分组卡片圆角

- `View/VisionPropertyPanel.xaml` 的分组容器由 Border 换为 MaterialDesign Card；`Themes/VisionPropertyPanelStyles.xaml` 中保留原有 12 圆角、间距/内边距和动态主题背景，改用 UniformCornerRadius，并显式启用 ClipContent。Border 的圆角不自动裁剪子内容，不能只靠 CornerRadius 防止 Expander 背景覆盖圆角。
- 新圆角回归先红：真实布局下 Expander 的内容宿主 Clip 为 null；修改后四项 VisionPropertyPanelTests 通过，覆盖实际圆角裁剪几何、动态半径变化、窗口缩窄及不重建编辑器/不改 DTO/不误报参数变化。
- 本轮命令：`dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --no-build --no-restore --filter "Category!=RequiresRuntime"`，181 通过、0 失败；其前定向命令已构建最新代码。未改 Core/Tools，未重跑逻辑全量或完整脚本，不沿用下方历史总数作为本次验证。实际主题观感、IDE Hot Reload 和鼠标交互仍待人工确认。

## 最近一次完整脚本结果：共用 PropertyGrid.Core、Vision 独立 XAML 属性视图

已执行 `Verify-EditorRefinement.ps1 -Full -ArtifactsPath 'Software Frame/Vision/Tests/obj/editor-verification'`，退出码 0：

| 范围 | 结果 |
|---|---|
| Vision.Workbench 构建 | 通过，0 错误；该次 10 个 nullable 上下文警告，未屏蔽 |
| 定向逻辑 / WPF 回归 | 32 / 104 通过 |
| Vision.Tests 非 Runtime 全量 | 325 通过，0 失败 |
| Vision.Workbench.Tests 非 Runtime 全量 | 180 通过，0 失败 |

全量合计 505 项；定向回归是其中子集，不重复累计。两条全量命令过滤 `Category!=RequiresRuntime`，因此结果中的“跳过 0”不表示真实 SDK 用例已执行。保留全部 RequiresRuntime / Skip，未安装厂商 Runtime、未连接设备。真实鼠标/主题/大图性能及授权 HALCON 行为仍须人工验收。

本轮属性面板迁移：
- Controls 显式引用现有 WPFCustomControls.PropertyGrid.Core；VisionPropertyGridBuilder 实现共用构建接口，把 ToolPropertyAttribute 映射为 Core 属性行，不要求工具 DTO 改成 NodePropertyAttribute。共用 PropertyGridHostViewModel、PropertyGroupViewModel、PropertyItemViewModel 的分组、搜索和订阅释放，不修改通用 Core 或原 PropertyGridHost 视图。
- 新 View/VisionPropertyPanel.xaml 负责分组/属性行布局，Themes/VisionPropertyPanelStyles.xaml 负责间距、列宽、控件样式、集合表和嵌套明细模板。动态资源可在不重建 DTO/编辑器的情况下替换。运行时 PropertyInfo 绑定、内容编辑权限、点/多选/数值集合、集合通知仍由 Vision 的专用 C# 编辑器装配，保持原 DTO 校验/回写；不是把任意复杂属性改成文本。
- Ctr_PropertyPanel 只保留继承新视图的兼容名称，旧宿主与嵌套面板实际使用同一 XAML 实现，没有维护两套面板。通用 PropertyGridHost 的外观与其他调用方不变；Core/Tools/JSON/宿主装配没有改变。
- 新 VisionPropertyPanelTests 三项：实际共用 Core 行与元数据/范围映射、原 DTO 回写与搜索、动态样式和列宽替换且不重建/不写值、卸载/换对象后旧行不再收到 DTO 通知及重载恢复。组合定向曾 34 通过/1 失败：搜索初始化绑定误发参数变更；加入未 Loaded 编辑器初始化过滤后相关 13 项通过，最终完整脚本通过。
- 自动化验证的是运行时资源替换与 WPF 布局/绑定，不是 Visual Studio 调试器的 Hot Reload 人工验收。修改 XAML 时仍需支持 XAML Hot Reload 的调试会话；发布程序不会自动监听源码文件。主题、实际鼠标、MachineApp 与授权 SDK 仍待分层验收。

前轮四项交互调整（501 项通过的记录）：
- 进入结果页自动启用结果拾取，选中当前结果表行也恢复此上下文；不再要求点击“选择结果”。离开结果页恢复普通选择模式，显式平移仍可使用，列表/表格冒泡事件不会误当切页。拾取继续遵守结果可见性、输入坐标和执行身份校验，不把结果选择变成 ROI 编辑。
- Workspace 不再创建种子 ROI。加载/执行不自动补 ROI；新浏览器的 Blob 空列表自然使用全输入搜索域。模板必须由用户明确添加矩形范围后才能提交。
- 顶部五个形状按钮收敛为“ROI 类型下拉 + 绘制ROI”；ROI 页同样使用类型下拉 + 添加，二者共享选中类型。仅选择类型不创建 ROI、不改变当前鼠标模式；需显式绘制或添加。原增删/各类型命令 API 保留。
- 普通矩形没有旋转柄，角度字段禁用，数值入口不会将它转为旋转矩形；移动/缩放仍可用。旋转矩形即使角度为 0 也保留类型和旋转能力；旋转取消恢复原位姿，椭圆仍可旋转。
- 新 EditorInteractionSimplificationTests 六项 + BlobBrowserSearchRegionTests 一项真实 OpenCV 全图回归，共新增七项。最初五项已执行失败（拾取/默认列表/矩形旋转/零度改类/无类型下拉），实现后通过；另验证共享类型、显式添加/绘制与角度字段启禁。
- 更新旧模板/导航测试为显式选择类型并添加范围，保留训练/释放/绑定等原断言，追加无 ROI 时模板按钮禁用检查；旋转事务测试改为真正的旋转矩形，包括零度与取消，不恢复普通矩形可旋转的旧行为。
- 本次只改 Controls、测试及文档；不改算法、历史参数 JSON、Core ROI/Fixture 或 Workbench 装配。实际物理鼠标/主题/窄窗口仍需人工验收，不以图像坐标拾取 API 和 WPF 自动化替代真实鼠标验证。

前轮 AI 检测实现与证据（494 项通过的记录）：
- Core 公共 AIDetectionResult 接入原有结果会话，不依赖工具名称、Provider 名称或 SDK 类型。行快照保存真实框、类别、类别编号和分数；框中心由框计算，不充当分割质心，不生成物体面积、姿态或轮廓。
- 新 DetectionResultsTemplate 仍位于原浏览页，复用同一选择、键盘和滚动事件；类别/分数排序不改变图形 ID。画布与表格双向选择、高亮；重叠时按最后绘制项优先，隐藏图层不接受拾取。
- 空/负尺寸、NaN、无穷大和边界溢出只保留标量行，标记“无有效位置”，既不绘制也不拾取。画布使用托管快照，不再每帧读取检测 DTO 的可变 Boxes，防止结果被更改后表画位置不一致。
- 新 DetectionResultSelectionTests 共 10 项：快照/类别隔离、重叠拾取、跨执行旧行拒绝、5 类无效定位、空/失败结果清理、实际表格排序/卸载重载/输入变化、像素高亮与隐藏图层。先执行最初 7 项，因没有检测行/无法拾取而失败，再实现并扩充到 10 项全部通过；包含模板与原结果表的定向组合 25 项通过。
- 完整脚本通过，仍使用仓库内 Tests/obj 隔离输出。该次增量构建的 10 个警告不等于已清理前轮全依赖构建的 154 个警告。没有修改参数 JSON、Provider/模型执行、Core/Tools 或 Workbench 装配；没有测量或宣称性能提升。
- AI 分割、多边形/掩膜、异常定位和测量结果尚未接入共享几何选择；真实 AI SDK 推理与人工交互仍未验收。本次测试只构造托管结果，不加载模型或连接设备。

前轮模板匹配实现与证据（484 项通过的记录）：
- 两套模板匹配接入同一 ResultSelectionViewModel；OpenCV 用真实匹配矩形拾取，HALCON 仅用结果中心点，不臆造模型轮廓。行与画布按执行内 ID 互选、高亮，排序不改变 ID；旧行、重跑、参数/输入变化继续隔离。
- 匹配行显示分数、中心、实际提供的角度/缩放及定位语义。不为模板结果填造 Blob 面积；OpenCV 未提供角度/缩放显示空值。读码无真实位置时仍不接入几何选择。
- 新 TemplateResultSelectionTests 共 8 项，覆盖匹配矩形/中心拾取、快照及分数/姿态隔离、跨执行旧行拒绝、排序/表画联动、卸载重载、隐藏结果与实际高亮像素。已执行完整回归；本轮不宣称取得逐项回退的红绿证据。
- 默认输出曾出现 XAML 生成成员缺失，显式 Rebuild 成功；随后默认构建明确被运行中的 Vision.Workbench.exe / devenv.exe 锁定，未强停进程。新增可选 ArtifactsPath，隔离到测试 obj 下后完整脚本通过。
- 最初将逻辑测试输出放在仓库外 Temp 时，19 个架构用例因无法从输出向上定位源码根而失败；未删除/过滤/降低这些断言，改为仓库内 Tests/obj 的隔离路径，325 项全部通过。若自选隔离目录，应位于源码框架根之下且使用 obj/bin，避免源码扫描包含生成物。

前轮执行发现并修复（476 项通过的记录）：
- Ctr_PropertyPanel 的 DataGridTextColumn 与 MaterialDesign 同名冲突；若干新测试的 ValidationResult 冲突及 ToolEntry 缺少引用。
- 测试工程链接的 `Build/TestSupport/WorkbenchTestLifetime.cs` 原本缺失，已重建；沿用主窗 Close 并有界等待 STA 退出，不强制 Shutdown，不吞关闭异常。当前只验证 Vision 测试线，其他引用该文件的模块未验证。
- nullable 坐标清空未回写 null：定向测试曾 16 通过 / 1 失败，加入显式 TargetNullValue 映射后 17 全通过；未降低断言。
- 兼容裁剪 API 的状态提示补充“局部坐标”：全量 WPF 曾 150 通过 / 1 失败，修补后 151 全通过。
- Windows PowerShell 下清理脚本的 Include 未有效过滤扩展名，误把文档及排除项当引用；改为明确检查 .cs/.xaml。先 WhatIf，确认后已删除三个退役文件。两个验收脚本增加窄范围 gitignore 例外，避免克隆后缺失。

以下保留历史范围说明；此前“未执行/缺少 shell/物理文件尚在”的描述只代表当时状态，以本节为准。

## 本地执行（Windows PowerShell，仓库根）

```powershell
& '.\Software Frame\Vision\Tests\Verify-EditorRefinement.ps1' -Full
# 正在运行工作台或 VS 锁住默认输出时，使用独立输出；不终止用户进程：
& '.\Software Frame\Vision\Tests\Verify-EditorRefinement.ps1' -Full -ArtifactsPath '.\Software Frame\Vision\Tests\obj\editor-verification'
& '.\Software Frame\Vision\Tests\Remove-RetiredEditors.ps1' -WhatIf
# 确认候选仅为三个已退出编译的旧文件后：
& '.\Software Frame\Vision\Tests\Remove-RetiredEditors.ps1'
```

清理仅针对 BlobParametersView 的 XAML/代码隐藏及 PropertyGridThemeAssist；不删除仍被 MachineApp/流程节点使用的 ToolEditorView，不移除 Xceed 数值控件依赖。已执行预演及物理清理，仅移除这三个文件。

## 真实中间 ROI 图（逻辑/WPF 回归已执行；HALCON Runtime 未验收）

- `BlobRoiImageTests`：逐像素检查阈值/搜索域二值图，即使特征筛选无结果仍有真实分割中间图；零阈值、全图/空域、原尺寸坐标、帧元数据、独立缓冲、释放后已借用快照、JSON 排除与独立后台快照。
- `RoiImageViewTests`：ROI 预设保持原图底图并显示 ROI 层；任意工具通过结果接口发布的中间图只经叠加设置显式选为底图，无名称/后端判断；不替换输入、输入位图复用；旧中间图失效不能经 Fit 回滚正在拖动的 ROI；未提供或失败时有明确提示。
- `BlobRoiImageRuntimeTests`：HALCON 中间图在 FillUp 前保留孔洞，最终结果保留 FillUp；域外清零、零阈值、空域、帧信息和 SDK 释放后的托管快照。仍保留 Skip/RequiresRuntime，仅授权离线环境验收。
- 继续静态收敛底图切换：同一输入坐标系内的底图开关（包括绑定依次到达、开关未实际换图）保留手势与缩放/平移；新输入即使同尺寸也必须取消，局部裁剪输出必须回滚。新增适应窗口/1:1、自动 ROI 预设绑定链、隐藏 ROI 回滚、新输入与局部输出负向回归。上述回归已随非 Runtime 全量通过；未回退补丁验证它们在旧实现上的失败，不声称逐项获得红/绿证据。
- 每次执行额外持有一幅全尺寸 Mono8 快照；HALCON 另有 RegionToBin 临时图。应记录峰值内存、执行/显示耗时及回切输入时的拖动稳定性，不添加未经测量的性能结论。其他工具尚未推广中间图。

## 前轮继续修补（仍未构建/执行）

- 旧 ToolEditorView / ToolCardView 的独立校验按钮移除；旧宿主执行前仍验证，无效参数不调用 Run。
- 旧宿主按 Core 的 IModelPreparationTool 区分加载与训练，ShapeMatchSession / DetectionToolSession 显式声明；不再按具体 Session 类型判断，也不把普通 Config 当模型准备。绑定不隐式调用。
- 面积 ROI 新增橙色旋转圆柄、多边形黄色顶点柄，保持原 DTO；非法零面积顶点位置不提交。Esc/失捕获/切换选中项回滚几何、类型和外接框。
- HALCON 找线新增 Sigma、每把卡尺 All/First/Last、拟合每端忽略点数；分别传到 MeasurePos / FitLineContourXld，不是仅修改 UI。旧 JSON 缺少新字段时保留 1 / All / 0 的原行为；写入为新增字段，不改原字段名。
- 卡尺组 Lines 列表新增 DataClassList 元数据，行明细继续复用 Ctr_PropertyPanel，允许添加/删除 DTO 行；集合观察改为 Loaded/Unloaded 管理的表格视图通知，不再直接永久订阅外部集合。CannyLine / Geometry / TransformPoint 也补中文元数据。旧宿主嵌套参数变化会提示重新执行，旧目标事件不污染新工具。AutoBarcode 补齐码制、double 缩放列表与 nullable 矩形坐标；新增同面板 DoubleList 编辑器（逗号分隔整组回写 List<double>），非法/非有限输入不写入 DTO，空坐标仍为 null，原 JSON 结构不变。增加真实 UI 回写/错误/清空/JSON 回归，拒绝 null 缩放列表和非有限区域。
- 新增 NestedPropertyPanelTests（真实嵌套编辑、增删、卸载/重载、换目标隔离）、LegacyEntryConvergenceTests（含回调重入切工具）、RoiGeometryCompletionTests、FindLineCaliperContractTests；FindLineCaliperRuntimeTests 保留 Skip/RequiresRuntime。脚本已加入定向回归。

## 新回归覆盖

- `LineSearchGeometryTests`：卡尺测量轴、数量/分布、Canny 单一搜索带、无效几何、旧 JSON 不增加派生接口字段。
- `FindLineSearchBandTests`：路径端点外的实际 Canny 边缘不参与带内找线，移动搜索带后可找到同一目标；真实拟合偏移不能退回期望路径，正反路径保持端点顺序，非有限阈值在调用 OpenCV 前拒绝。
- `LineSearchEditorTests`：端点/测量半长写回原 DTO、整体移动、取消回滚、防止退化路径、五类真实面积 ROI、拒绝零面积多边形。
- `EditorRefinementTests`：19 个注册工具都产生非空 Ctr_PropertyPanel、无旧 PropertyGrid、无隐式 Provider/模型加载；三个视图不裁剪输入、不改变鼠标模式；旧宿主 Blob 适配、参数对象替换、卸载/重载和借用所有权。
- 扩大参数元数据契约，调整旧按钮、结果选择、图层与拖动测试，保留原行为断言；执行仍自动验证。

## 必须人工验收

1. HALCON 找线卡尺阵列：拖两端、主体、黄色测量轴手柄；半宽/数量数值修改；Esc、失捕获、切图/切工具/关闭回滚。授权离线 SDK 验证测量方向、采样点、资源释放。
2. OpenCV 找线明确为 Canny 搜索带；带的端点/宽度与参与检测的点一致，不显示假卡尺。增加超出端点的干扰边缘。
3. 图像视图为输入 / ROI / 结果预设，另有独立平移、搜索域编辑、结果选择。ROI 预设始终为原图加 ROI 标注；Blob 的算法中间图（阈值/搜索域二值图）需在叠加设置中显式选为底图，未提供或失效时明确提示并回退输入。隐藏叠加不关闭搜索域。拖动导致中间图失效不能回滚当前手势；输出尺寸改变时不在错误坐标系编辑原图几何。
4. 矩形、旋转矩形、圆、椭圆、多边形绘制/选择/移动/缩放；角度字段与橙色旋转圆柄均可编辑；多边形顶点可逐点拖动。多边形单击加点、双击或 Enter 完成、Esc 取消。不声称已实现自由区域/XLD/圆环或全部 HALCON 绘图类型。
5. 深浅主题、窄窗口换行、键盘与图标提示、参数重绑、MachineApp/流程节点旧宿主入口；实际大图与复杂轮廓性能仍需测量。

这些变更不等于全 P4 或全部原版功能完成；Sigma/first-last/忽略首尾点已写入但未验收；任意坏点剔除、更多 HALCON ROI 类型、模板匹配之外的非 Blob 图表联动及全工具后台执行仍未完成。
