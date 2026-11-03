# Vision代码归位与宿主边界

## 当前：统一工具注册与基础流程组合

- 基础节点继续由FlowNodeCatalog反射发现；FlowToolDiscoveryService把反射元数据与模块显式工具描述合并，按ToolId校验冲突，不按显示名或CLR类型合并不同配置工具。描述相同引用重复提供是幂等的，冲突身份在发布前拒绝。
- Vision的28个工具由原ToolEntry目录投影为延迟Factory，浏览、搜索不创建会话。原ToolboxService统一分类/搜索；VisionFlowViewModel与原编辑容器通过UseToolboxAsync共用同一个服务，不再手写Switch工具项或覆盖基础工具集合。
- Vision当前35项：28视觉工具＋开始、结束、延时、日志输出、条件分发、循环、分支汇聚七项原节点。沿用FlowTool的名称/图标/排序，旧Condition仍隐藏。ToolBlock、循环容器、不执行容器和并行分叉暂不开放新建，不从文档类型目录删除。
- CreateToolRegistrations返回可选视觉工具描述；其他宿主可全部、部分或不选用。该入口不注册Vision页面；真正接入还需要宿主显式组合Vision.FlowNode程序集及原VisionNodeFactory。FlowNodes.Workbench本轮未加入Vision工具，源码不修改。
- 不改原DTO/算法/端口/文件身份、会话所有权或执行器；本轮不增加动态插件加载、运行时来源刷新、文档版本迁移或撤销机制。


## 最新：阈值/统计/几何/公差第三批（2026-10-26）

新增OpenCv.ThresholdNode、GrayStatisticsNode和Common.GeometryNode、ToleranceNode，分别声明原阈值参数及数值输入、灰度统计输出、几何输入/参考输入与结果、公差测量输入及布尔结果。Definitions只引用节点本身的声明；原DTO、单位/同帧/直接来源校验、缺失拒绝及原卡片执行不变，不给Geometry新增Native能力。当前八个具体类、其余20工具/旧vision.tool.instance保持通用入口，未迁移白名单测试改用仍未迁移的Morphology。

扩展原契约17项先8通过/9失败，实施后定向64及完整 **679逻辑/555WPF，0失败/0跳过**，WPF6.9187分钟正常退出；新旧文件用例包含全部八个具体类型和一个通用节点。默认宿主构建4条未改RoiEditorView nullable警告/0错误，独立产物目录增量0警告0错误。MachineApp未动，未暂存/提交，不做端口折叠或Native/物理验收；命令与日志见[验证记录](../../Tests/EDITOR_REFINEMENT.md)。

## 最新：找圆/模板具体节点第二批（2026-10-26）

新增Nodes/OpenCv/FindCircleNode和TemplateMatchNode，分别声明原半径参数和Circle/Center端口、原MinScore/MaxMatches和定位候选端口；不复制DTO、像素、模型或算法。定位候选输出从公共基类归给具体模板节点/旧通用节点，原端口名称、属性和顺序保持。Definitions引用两份节点声明；新建和文档类型/工具身份校验均接原VisionNodeFactory。当前四个具体类型、其余24工具和旧vision.tool.instance仍走通用入口，不改变原.flow版本/字段。

扩展原契约用例先9中5红，主要为新类型尚未接入；实施后定向44通过，完整 **679逻辑/547WPF，0失败/0跳过**，WPF8.6832分钟正常退出。默认宿主构建4条原RoiEditorView nullable警告/0错误；独立产物目录增量0警告0错误。旧/新文件对比包含圆字段选择与模板像素独立快照，不自动训练/加载模型；原实际模板→找圆跟随/失效/异来源拒绝链通过。MachineApp未动，未提交，Native/物理未验；详见[验证记录](../../Tests/EDITOR_REFINEMENT.md)。

## 最新：具体视觉节点首批（2026-10-26）

抽出Nodes/VisionToolNodeBase和原共享绑定partial；新建OpenCV Blob/找线分别为OpenCv.BlobNode、OpenCv.FindLineNode，直接继承公共基类，各自声明常用参数/结果字段和数据端口，不复制DTO、算法或执行器。Definitions索引两类的同一份声明；属性行仍直接绑定Session.Parameters。Controls与绑定helper消费基类；其余26工具和旧文件保留VisionToolNode，旧CLR/vision.tool.instance不改，未迁移工具仍走原宿主参数白名单。新类型与配置工具身份不符在建会话前拒绝。

基线35通过，新类型用例4项实际红后转绿；审查补未迁移白名单回归，取得1红后修复。最终新增5项与原R0共13通过，完整 **679逻辑/543WPF，0失败/0跳过**，WPF10.6759分钟正常退出。默认和独立产物目录的宿主最终增量构建均0警告0错误；首次重编有RoiEditorView等未改文件警告，不代表警告消除。MachineApp未动，不做折叠、Native或物理验收，未暂存/提交。结构见[节点README](../../Vision.FlowNode/README.md)，命令和日志见[验证记录](../../Tests/EDITOR_REFINEMENT.md)。

## 最新：操作按钮固定显示（2026-10-26）

按用户确认，删除VisionToolEditorView.ShowProjectToolbar、ShowExecutionButton及其DependencyProperty、文件按钮显隐触发器、执行按钮Visibility绑定和不再使用的BooleanVisibility转换器。打开、保存、另存为、导图、执行五项固定显示，不再依赖Root绑定使按钮出现。HasCurrentTool/HasProjectCommands及原CanExecute继续只负责禁用，不改变原会话、文件、运行锁或窗口所有权。旧隐藏测试改验900/1300宽度下五项始终显示及原命令/会话身份；节点窗口改验实际按钮可见，不以已删除开关值代替界面断言。

原新增样式用例先复现Root未解析时Collapsed；本轮退役开关检查再取得1项实际红，随后定向 **24通过、0失败/0跳过**（VisionToolEditorCompositionTests、VisionFlowR0Tests、NodeToolConfigurationTests），包含真实文件操作、空会话禁用、换节点及后台运行锁。Vision宿主显式增量构建 **0警告0错误**。未手动打开Visual Studio设计器，不将STA样式/布局验证称为设计器人工验收；本轮未重跑完整逻辑/WPF或Native测试，不沿用前轮全量数量作为本轮证据。MachineApp未改、未构建；未暂存/提交。

%TEMP%日志：vision-designer-toolbar-red.log（原Collapsed复现）、vision-fixed-toolbar-red.log（开关退役红）、vision-fixed-toolbar-focused.log（最终24通过）、vision-fixed-toolbar-host.log、vision-fixed-toolbar-diff-check.log。下方隐藏开关约定属于历史，由本节替代。


## 最新：Compatibility目录删除（2026-10-26）

- 用户确认不再保留兼容目录。删除其中14文件：两个旧控件空别名、空端口面板XAML/代码、十个旧URI转发；另删仅含旧别名空模板的Themes/Generic.xaml。csproj不再声明Compatibility资源或LogicalName。
- 实际跨模块引用已核查：FlowNodesEditorAdapter/AINode仍调用ToolEditorView，AINode仍拥有DetectionToolSession。因此仅这3个有功能的源码归位Views/Editor与ViewModels/Execution，CLR身份/参数/序列化/结果所有权不改；不删除有效功能，不新增转发、不改FlowNodes代码或合并宿主。
- 原完整编辑器、线ROI和嵌套Class/List模板直接使用VisionPropertyPanel；正式VisionMultiSelectComboBox保留。旧Ctr_PropertyPanel/Ctr_MultiSelectComboBox/Ctr_ToolPortBindingPanal和View/*、Ctr_Base/*、Ctr_Halcon/* pack URI明确退役，不承诺外部消费者兼容；正式Themes/LegacyHalconResources.xaml内容与自身合并层级保留。
- 新退役断言先有1项真实红；定向78通过。完整 **679逻辑/537WPF，0失败/0跳过**，WPF5.3330分钟正常退出；537=前轮536+1兼容层负向守卫。Vision宿主显式增量构建0警告0错误；FlowNodes消费者显式构建0错误、3条既有nullable警告，未改其源码。旧节点编辑器与嵌套属性面板生命周期断言保持，未降低算法或文件测试。
- MachineApp不修改/构建/验证；Native/物理仍未验；FlowNodes、FlowEditor、PropertyGrid独立测试套件本轮未运行。未暂存/提交，详见[本轮验证记录](../../Tests/EDITOR_REFINEMENT.md)。下方Compatibility/旧URI保留记录属于历史，已由本节替代。

## 最新：用户确认同步退役API测试（2026-10-26）

唯一VisionToolEditorView及节点原会话文件操作保持；本轮仅修复生产API退役后的测试编译残留，不恢复ImageViews、合成图/对比/基准接口或废弃目录回调。测试改由夹具提供原64×64像素并走LoadImage/原卡片ExecuteAsync，选项直接核对实际XAML标题/Tag；保留文件、连接、DTO、图层和生命周期断言。初始编译失败后定向53通过，完整 **679逻辑/536WPF，0失败/0跳过**，WPF7.0782分钟正常退出，Vision宿主显式增量构建0警告0错误。MachineApp未触碰，Native/物理未验，未暂存/提交。命令、失败诊断和%TEMP%日志见[本轮验证记录](../../Tests/EDITOR_REFINEMENT.md)。下节“不修改/运行测试”是删除API当时的阶段约束，测试同步经后续用户确认后执行。

## 最新：删除旧图像选项API（2026-10-26）

按用户明确授权直接删除VisionToolEditorSession.ImageViews及Models/Editor/ImageViewOption.cs，不保留兼容属性或类型。现有XAML固定选项、ImageViewKind和SelectedImageView绑定不变。本轮不修改/运行测试程序，不涉及MachineApp；生产验证仅构建Vision宿主。下方“公开选项API暂不退役”属于前一阶段记录，已由本节替代。

## 最新：配置映射与事件订阅去重（2026-10-26）

- 仅做保持行为的结构重构：原VisionProjectCodec增加CreateProject，共用工具身份、参数、ROI、显示偏好、取样/训练参数及模型引用的清单映射。Browser和节点仍各自按原所有者捕获配置；复用既有VisionToolConfiguration作为临时载体，不增加类型、工具实例或持久化状态。Browser的ImageSource与节点原空值分别保留，文件字段/版本及独立可换工具、节点原实例导入的不同语义不变。
- VisionFlowViewModel将五处重复接线收拢为ObserveNodeEvents/ObserveSessionEvents两组；删除时仍先退订节点编辑事件、关闭窗口、再退订会话事件，不改实际调用等待/工具释放顺序。新增失败、节点恢复、移除和宿主关闭复用相同事件清单。
- 删去Browser普通SetImage/工程恢复中的两处HasImage重复赋值，由原OnInputImageChanged维护。关闭时HasImage=false的通知时机不同，保留，不为少一行改变通知顺序。公开ImageViews/ImageViewOption本轮不退役，不合并两个Workspace，不动MachineApp。
- 定向55通过；完整 **679逻辑/536WPF，均0失败/0跳过**，测试数量不变，无测试断言修改。WPF首轮外层600秒预算中断，当时日志仍在通过用例，未得到完整汇总；相关进程已退出后，以外层1200秒重跑 **11.5164分钟正常退出**。单用例90秒挂起检测及原断言未放宽；未确定整体耗时增加原因，不宣称性能提升。
- 独立宿主显式构建 **增量0警告0错误**，不代表原重编nullable警告消除。diff-check=0，暂存空，未提交。Native/物理未验，共享FlowEditor/PropertyGrid/FlowNodes独立套件本轮未重跑。
- %TEMP%日志：vision-simplification-focused.log、vision-simplification-logic-final.log、vision-simplification-wpf-final.log（外层超时，非通过证据）、vision-simplification-wpf-retry.log（完整通过）、vision-simplification-host-final.log、vision-simplification-diff-check.log。使用前述相同项目/过滤/产物路径及单进程参数；WPF结果目录Tests/obj/simplification-diagnostics。


## 当前：Browser视图退役、节点原会话文件操作（2026-10-26）

- 删除Views/Browser/VisionBrowserView.xaml、代码及Compatibility旧资源，共3文件；撤注册及csproj资源声明，空Browser视图目录删除。唯一完整布局为VisionToolEditorView，独立消费者和测试直接绑定Session，不留Browser容器/CLR或pack URI转发。
- Browser VM/Workspace仍保留实际工程/目录/所有权能力，不因删除View而删文件机制。节点VisionToolInstanceWorkspace实现原IVisionProjectCommands，编辑会话仅借用，文件实现归其Files partial并复用原Codec/FileStore，不创建第二个工具。
- 节点窗口也显示并接通打开、保存、另存、导图、执行。工具配置.visionproj与整个.flow保存明确分离；工具配置包含原DTO、ROI、配方输入、可选托管取样/训练参数与模型引用，不持久化结果、节点/连接或派生定位，不自动加载/训练模型。
- 打开必须匹配当前ToolIdentity和Provider；Provider不同先显式选择匹配Provider。加载更新原DTO而不换实例/节点/会话；保留连接及动态模式，清旧结果并撤销旧定位基准，后续跟随须重新显式捕获。导入配方图像不修改接线，执行仍以显式来源为准。
- 运行/只读/关闭禁用文件命令；对话框返回复查状态与revision，取消/无效/错工具文件保留配方，失败另存不接管路径。模板像素在原工具校验，参数先反序列化再写原DTO；清单仍有8MiB上限，超限明确失败，原安全替换文件机制保留。
- 原Session/卡片/参数对象身份及窗口借用所有权不变；关闭编辑窗口不释放节点。新节点文件回归7项覆盖真实按钮和文件IO、保存/另存/重开、取消/非法/错工具、只读/后台运行锁、迟到对话框、模板及失败保存。
- 先取得缺少ProjectCommands的真实红；定向81及节点文件7项通过。完整首轮发现旧Browser目录/注册断言，改为明确退役与规范Editor注册的正反检查，不降低算法/数据/生命周期断言。
- 最终完整 **679逻辑/536WPF，0失败**，WPF6.5892分钟正常退出；宿主 **0错误、4条原RoiEditorView nullable警告**。536=前轮531−3旧URI正向案例＋1退役类型案例＋7节点文件案例。RequiresRuntime仍排除，不能据此宣称Native通过。
- 日志均在%TEMP%：vision-node-files-red.log、vision-node-files-focused.log、vision-single-editor-focused.log、vision-single-editor-logic-final.log、vision-single-editor-wpf-final.log、vision-single-editor-host-final.log。沿历史命令与同一editor-verification产物路径，WPF诊断目录Tests/obj/single-editor-diagnostics，串行构建参数不变。
- MachineApp不修改/构建/验证；共享FlowEditor/PropertyGrid及FlowNodes独立套件本轮未重跑，不引用历史数量作本轮证据。Native/授权模型/物理UI/实图精度节拍仍未验。下方隐藏节点文件栏、Browser薄容器等是此前阶段记录，已由本节替代。


## 当前决定（2026-10-26，按用户最终确认）

**保留独立Vision.Workbench，但它是薄宿主，只导航Controls中的视觉流程页。**
不由FlowNodes.Workbench接管视觉工作台，也不恢复流程图引入前的独立浏览/列表调试入口。
上一轮“删除Vision宿主并合并FlowNodes”的实现已撤回，其29项混合宿主测试不代表当前布局。

## 正确位置

| 职责 | 唯一实现位置 |
|---|---|
| 独立视觉启动、模块注册、主窗/主题与导航 | Vision.Workbench/App、MainWindow、ViewModels/MainWindowViewModel |
| 视觉流程页面、完整编辑窗口交互 | Vision.Controls/Views/Flow、ViewModels/Flow |
| 工具目录/工厂/Provider注册 | Vision.Controls/Services/Catalog |
| 默认目录与桌面文件回调、流程装配 | Vision.Controls/Services/Composition/VisionCatalogFactory、VisionFlowRegistration |
| 节点常用参数及结果字段声明 | Vision.FlowNode/Nodes/OpenCv与Nodes/Common声明八个具体工具；Definitions索引八份声明及其余20工具原定义 |
| 节点、显式绑定和执行入口 | Vision.FlowNode/Nodes/VisionToolNodeBase及共享partial/helper；具体节点声明专属端口，执行委托原Core会话；VisionToolNode保留未迁移/旧文件接入 |
| 完整工具参数、算法及原生图像解码 | Tools；原宿主解码实现归Tools/OpenCv/Acquisition/VisionImageFileLoader.cs |

- 工具属性是原DTO，节点常用参数直接代理同一DTO；下游连接消费本次结果，不保存第二份参数/绑定。
- Controls目录消费28项节点定义，不在Workbench重复声明字段；维持28算法＋1控制项，共29视觉工具箱项。
- Vision.Workbench只调用RegisterVisionFlow(VisionCatalogFactory.CreateCatalog)，不放算法、节点字段白名单、项目IO引擎或会话实现。
- Controls→FlowNode→Core/FlowEditor，禁止FlowNode→Controls、Controls→Workbench及两个Workbench互相引用。
- View/VM正式全名为Vision.Controls.View.VisionFlowView、Vision.Controls.ViewModels.VisionFlowViewModel；不再把UI实现放回宿主旧命名空间。

## 保留与删除

- 已恢复Vision.Workbench.csproj及根/Vision解决方案登记，测试再次引用并启动真实Vision.Workbench.App。
- Workbench中不恢复ToolBrowserView/VM、四类工具目录、ToolRegistration、WorkbenchComposition或旧目录/流程UI转发。这些旧独立入口不注册、不出现在导航中。
- 完整工具编辑器、参数/ROI/结果组件全部保留。双击节点借原Session，关闭编辑窗口不Dispose节点。
- VisionBrowserView已删除；VisionBrowserViewModel保留会话/程序化工程能力；四类废弃View已删除，VisionTaskViewModel、ToolCardViewModel等执行/会话类型保留。保留VM不等于恢复独立调试导航。
- FlowNodes原App、模块/节点目录、通用FlowChartContainerView导航、主题布局及原测试均恢复；本次合并引起的已跟踪文件差异为0。专属72项混合工具箱、额外通用节点入口/构造重载及接管注册已撤回，避免留无消费者框架代码。
- 旧Vision宿主App/MainWindow入口重新存在，但旧ToolBrowserView、目录API兼容转发及旧Vision.Workbench.Views.VisionFlowView资源/类型不恢复。NodeTypeId、ToolIdentity、目录键、原DTO/受控.flow状态不改。

## 运行与生命周期

独立Vision宿主只加载共享控件、Vision.Controls、FlowEditor模块，不加载FlowNodes设备/通信/脚本宿主模块。
注册不等于模型加载/训练或设备连接，Provider仍按原工厂/可用性探测机制提供。
原FlowChartContainerView的运行/停止/调试、保存/重开和日志链不变；原Card准备独立执行快照、发布及revision迟到隔离不变。
宿主关闭沿共享WorkbenchLifetime→CloseRegionAsync→Controls流程VM异步归还节点会话；窗口只借Session。

## 前一阶段：诊断删除、编辑器去重和操作栏修复（2026-10-26；历史）

- 再删除7文件：ToolDiagnosticsView.xaml/.xaml.cs/旧资源，ToolDebugView.cs/旧资源、ToolDebugSession.cs、ToolDebugModel.cs；相应导航注册、诊断Tab/DiagnosticsContent插槽及DebugView访问器一起清理。空Compatibility/Models目录删除，DetectionToolSession仍有使用而保留。
- 真正的布局只有VisionToolEditorView.xaml；VisionBrowserView保留薄会话容器，不复制UI。Browser VM/Model直接继承规范VisionToolEditorSession/VisionToolEditorModel。旧ToolDebug* CLR名称及URI明确退役，不宣称外部兼容。
- 已复现用户报告：没有绑定Browser/工程会话时，原HasProjectCommands触发器把文件按钮全部折叠，仅留下执行。先有实际红 `vision-editor-toolbar-red.log`，不是只读样式猜测。
- 修复：默认显示打开、保存、另存为、导入图像、执行五项；缺少工程命令时禁用，空会话的执行也禁用，用FallbackValue/TargetNullValue避免未绑定时误启用。ProjectCommands仍沿原会话，不自动创建工具、SDK或工程，不挂空命令假装可用。
- Browser真实按钮验证：先无会话检查五项可见/禁用，后绑定原会话检查命令身份；另一个实际Browser用例经原按钮导入图像、保存/另存/重开.visionproj、参数及输入恢复、取消/关闭锁检查，未以模拟界面代替文件IO。
- **范围明确**：节点Owned窗口在VisionFlowView中仍显式ShowProjectToolbar=false，其会话没有单工具工程命令。该窗口的“执行”不是浏览容器缺按钮的同一原因；本批不把单工具工程包保存偷换成整个.flow保存。需要改变此入口时须明确其文件作用对象。
- 定向37通过；完整**679逻辑/531WPF**，WPF5.6360分钟正常退出，独立宿主增量**0警告0错误**。原重编nullable等警告仍保留。测试数变化=新增空会话工具栏回归、增加两个退役View检查、移除已退役ToolDebug资源正向案例；不降低旧参数/工程文件断言。
- %TEMP%日志：vision-editor-consolidation-focused.log、logic-first.log、wpf-first.log、host-final.log（后三者同前缀vision-editor-consolidation-）。沿上一节相同逻辑/全WPF/宿主命令，WPF诊断目录为Tests/obj/editor-consolidation-diagnostics；仍追加原单进程参数。
- 已核查Controls生产C#/XAML没有ToolDebug*、ToolDiagnosticsView、DiagnosticsContent/Tab残留。MachineApp不修改、不构建、不验证；Native及物理UI边界保留。非零数量/退出码/异常与diff检查通过，暂存空、未提交。
- 官方依据：[WPF BindingBase.FallbackValue .NET9](https://learn.microsoft.com/en-us/dotnet/api/system.windows.data.bindingbase.fallbackvalue?view=windowsdesktop-9.0)、[WPF Data binding overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/data-binding-overview)。使用System.Windows.Data而非搜索命中的WinUI/Microsoft.UI.Xaml API，未升级SDK/包。

## 上一轮视图清理（2026-10-26；历史记录）

### 已删除：4类视图，11个文件

| 类型 | 已删除源码/资源 |
|---|---|
| ToolCatalogView | Views/Browser/ToolCatalogView.xaml、.xaml.cs；Compatibility/Resources/Views/ToolCatalogView.xaml |
| ProjectToolbarView | Views/Browser/ProjectToolbarView.xaml、.xaml.cs；Compatibility/Resources/Views/ProjectToolbarView.xaml |
| ToolCardView | Compatibility/Views/ToolCardView.xaml、.xaml.cs；Compatibility/Resources/Views/ToolCardView.xaml |
| VisionTaskView | Views/Execution/VisionTaskView.xaml、.xaml.cs；空Execution视图目录同时移除 |

- VisionBrowserView的目录列/190像素预留宽度、分隔条及列索引已移除，原完整编辑器直接占满容器。
- csproj删除前三类旧资源转发，VisionControlsModule撤销ToolCardView注册；不留空页面或新转发代替。四类CLR类型、导航注册及BAML/旧资源确实不再发布，四行实际程序集/资源枚举测试锁定。
- 工具注册工厂、28项节点字段定义、真实流程工具箱不删除。ToolCardViewModel、VisionTaskViewModel及原执行工作区仍有实际运行/程序化调用，不能和同名View混删。
- 旧TaskView的UI测试直接借用VisionToolEditorView及SelectedEditor绑定；不重建废弃列表/工具栏。实例A/B原参数/ROI/结果、运行中只读但可缩放平移、活动与缓存DataContext关闭等待/释放错误聚合断言保留。
- 仅有负向断言的旧ProjectToolbarView已确认无布局实例；现用工程按钮直接在VisionToolEditorView.xaml中，保持打开/保存/另存/执行入口及原命令。

### 保留：已核查仍实际使用

- Flow/VisionFlowView：独立薄宿主唯一视觉流程导航。
- Editor/VisionToolEditorView、ToolDetailsView：原节点完整编辑器及参数/结果页。
- PropertyGrid/VisionPropertyPanel、VisionPropertyTableEditor：Ctr_PropertyPanel实际基类及Class/List属性编辑器，不是无人使用的同名文件。
- Roi/LineRoiEditorView、RoiEditorView，Results/BlobFeatureFilterView，Imaging两显示/操作控件：原完整工具页直接使用。
- 当轮Browser/VisionBrowserView曾仍嵌入ToolDiagnosticsView；该诊断区及插槽现已在上文删除。Browser只借同一个规范编辑器，原工程/程序化会话API不因此删除。

### 红绿、验证与边界

- `vision-browser-catalog-removal-red.log`：新“无ToolDirectoryPanel/ToolColumn”断言在原UI真实失败，2项中1红。
- 删除后的定向首次41/42：旧按钮测试把Dispatcher Idle当成后台执行完成，读到“执行中…”；保留原成功/图像/ROI断言，改为等待原RunCommand.ExecutionTask后42/42绿，未改生产同步策略/加Delay。
- 任务生命周期/退役类型定向31通过；最终**679逻辑/529WPF**全通过、过滤后Skipped0、正常退出。WPF总数525→529仅新增四个退役类型/注册/资源断言，不是新增算法。宿主构建**0错误、4条原RoiEditorView nullable警告**；不报成零警告。
- 本轮不读取/修改/构建/验证MachineApp，不承诺其旧ToolCardView等外部契约继续存在。按用户明确清理要求退役上述公开视图和URI，不能笼统宣称全仓无破坏兼容。
- 原FlowNodes及共享FlowEditor/PropertyGrid生产代码未改；本轮未重跑其独立套件。Native/授权模型、物理输入/DPI跨屏/实图精度节拍仍未验，不解除Skip。
- 已复核非零预期数量、退出码0及未处理异常/宿主崩溃/零匹配；diff-check=0、暂存空、未提交。

命令仍追加下文同样的单进程构建参数：

- `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'`：679，%TEMP%/vision-unused-views-logic-final.log。
- `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/unused-view-diagnostics"`：529、6.5926分钟，vision-unused-views-wpf-first.log。
- `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"`：4警告/0错误，vision-unused-views-host-final.log。

官方依据：[AsyncRelayCommand/ExecutionTask](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/asyncrelaycommand)、
[WPF程序集资源URI](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/pack-uris-in-wpf)。
本仓固定Toolkit8.4.2/.NET9；搜索命中过旧Microsoft.Toolkit7.1文档不用于本次API判断。类型/资源退役和异步执行完成分别验证，不以编译或Dispatcher Idle代替。

## 宿主恢复阶段验证（历史；本轮结果见上文）

SDK10.0.401/.NET9/C#13、WPF、Prism8.1.97、Toolkit8.4.2及既有OpenCV包未升级。
全部仓库根串行，追加 `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false`：

| 命令 | 结果 | %TEMP%日志 |
|---|---|---|
| `dotnet test "Software Frame/Vision/Tests/Vision.Tests/Vision.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal'` | **679通过/0失败/0跳过** | vision-thin-host-logic-final.log |
| `dotnet test "Software Frame/Vision/Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification" --filter 'Category!=RequiresRuntime' --logger 'console;verbosity=normal' --blame-hang-timeout 90s --blame-hang-dump-type mini --results-directory "Software Frame/Vision/Tests/obj/thin-host-diagnostics"` | **525通过/0失败/0跳过**，6.0947分钟、正常退出 | vision-thin-host-wpf-final.log |
| `dotnet test "Software Frame/FlowNodes/Tests/FlowNodes.Workbench.Tests/FlowNodes.Workbench.Tests.csproj" --no-restore --logger 'console;verbosity=normal'` | **原28通过/0失败/0跳过** | vision-thin-host-flownodes-final.log |
| `dotnet build "Software Frame/Vision/Vision.Workbench/Vision.Workbench.csproj" --no-restore --artifacts-path "Software Frame/Vision/Tests/obj/editor-verification"` | **增量0警告0错误** | vision-thin-host-build-final.log |

额外独立宿主启动/导航/资源/P6定向19通过；断言真实入口程序集为Vision.Workbench、不引用FlowNodes.Workbench、页面/VM归Controls、只有视觉流程导航、28算法/29工具箱、无旧浏览/任务导航。
恢复引用后仅重新restore既定artifacts图（vision-thin-host-restore.log），未改包版本；SDK自动加入Vision.sln的共享依赖登记已移除，布局守卫679通过。
所有预期非零数量、退出码0及未处理异常/宿主崩溃/零匹配已复核。diff-check=0，暂存区空、未提交。
原重编nullable/SourceLink等警告仍在，增量0不是全仓零警告。
共享FlowEditor/PropertyGrid生产未改，独立416/66本批未重跑；HALCON Native、AI授权模型、物理鼠标/OLE、DPI/跨屏及实图精度/节拍仍未验，未安装Runtime或解除Skip。

## 官方依据

- [WPF pack URI](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/pack-uris-in-wpf)：主窗资源归宿主，流程及工具编辑器BAML归Controls，跨程序集资源实际加载验证。
- [XAML命名空间/程序集映射](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/xaml-namespaces-and-namespace-mapping-for-wpf-xaml)：x:Class、CLR全名与程序集一致，不以复制页面解决资源定位。
在线文档默认版本变化不代表升级本仓固定版本。
