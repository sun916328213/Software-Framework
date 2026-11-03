# Controls当前目录与界面分工（2026-10-26）

## 当前：统一工具注册与基础流程组合

- 基础节点继续由FlowNodeCatalog反射发现；FlowToolDiscoveryService把反射元数据与模块显式工具描述合并，按ToolId校验冲突，不按显示名或CLR类型合并不同配置工具。描述相同引用重复提供是幂等的，冲突身份在发布前拒绝。
- Vision的28个工具由原ToolEntry目录投影为延迟Factory，浏览、搜索不创建会话。原ToolboxService统一分类/搜索；VisionFlowViewModel与原编辑容器通过UseToolboxAsync共用同一个服务，不再手写Switch工具项或覆盖基础工具集合。
- Vision当前35项：28视觉工具＋开始、结束、延时、日志输出、条件分发、循环、分支汇聚七项原节点。沿用FlowTool的名称/图标/排序，旧Condition仍隐藏。ToolBlock、循环容器、不执行容器和并行分叉暂不开放新建，不从文档类型目录删除。
- CreateToolRegistrations返回可选视觉工具描述；其他宿主可全部、部分或不选用。该入口不注册Vision页面；真正接入还需要宿主显式组合Vision.FlowNode程序集及原VisionNodeFactory。FlowNodes.Workbench本轮未加入Vision工具，源码不修改。
- 不改原DTO/算法/端口/文件身份、会话所有权或执行器；本轮不增加动态插件加载、运行时来源刷新、文档版本迁移或撤销机制。


## 一份编辑器实现

- `Views/Editor/VisionToolEditorView.xaml(.cs)`：唯一完整工具编辑布局，包含五个操作按钮、图像操作栏、显示和详情区。
- `VisionBrowserView`及Browser视图目录、旧资源URI和注册已删除；独立使用直接绑定VisionToolEditorView.Session。原Browser VM/Workspace的文件和程序化能力保留。
- `Views/Flow/VisionFlowView.xaml(.cs)`：复用原FlowChartContainerView；双击节点借用原编辑会话，窗口不拥有工具。
- `ViewModels/Flow`：流程展示交互；统一消费VisionToolNodeBase，新建Blob/FindLine/FindCircle/TemplateMatch/Threshold/GrayStatistics/Geometry/Tolerance经VisionNodeFactory路由具体节点，算法属于Tools。
- `Vision.FlowNode/Nodes`：公共基类和原共享绑定partial；Nodes/OpenCv与Nodes/Common中的八个具体节点声明常用参数、结果字段和端口。Definitions索引这八份声明及其余20工具原定义，不重复保存参数；旧VisionToolNode/vision.tool.instance仍读原.flow。
- `Services/Catalog`：工具工厂/注册，`Services/Composition`：默认目录及模块装配；宿主不声明工具字段。
- `Views/Editor/ToolEditorView.xaml(.cs)`及`ViewModels/Execution/DetectionToolSession.cs`：FlowNodes现用IEditableTool编辑/检测实现，保持原CLR身份；不是Vision.Workbench的完整视觉编辑布局。

## 操作栏

默认五项始终显示：打开工程、保存工程、另存为、导入图像、执行。
文件按钮由Session.ProjectCommands提供原命令；未绑定或不具备工程能力时禁用，不再因为HasProjectCommands=false而消失。
五个按钮固定显示，无会话时执行也禁用；已删除ShowProjectToolbar、ShowExecutionButton及其显隐绑定，不保留隐藏开关。
节点Owned窗口同样显示并接通五项操作。VisionToolInstanceWorkspace.Files复用原VisionProjectFileStore/Codec保存当前工具.visionproj；不保存节点身份、连接、运行结果或派生定位。流程工具栏仍保存整个.flow。
打开配置要求相同工具及Provider，直接更新原DTO/ROI/取样和模型引用，不创建第二个工具，不自动加载模型。成功导入清结果、撤销旧定位基准并标记任务修改；连接保留，动态模式不关闭，定位跟随需重新显式设置基准。文件对话框返回后复查关闭/忙态/只读及配方revision。

配置清单的共同字段仅在Services/Projects/VisionProjectCodec.CreateProject映射；两个Workspace仍各自捕获其拥有的原配置并保留不同导入语义。流程节点/会话事件分别由VisionFlowViewModel的ObserveNodeEvents/ObserveSessionEvents统一管理，删除节点时的关窗边界不变。

## 仍使用的部件

| 位置 | 职责与实际调用 |
|---|---|
| Views/Editor/ToolDetailsView | 原完整编辑器的参数、ROI、结果、模型/模板页 |
| Views/PropertyGrid | 直接使用VisionPropertyPanel及其Class/List表格编辑器，无Ctr_PropertyPanel别名 |
| Views/Roi | ToolDetailsView直接使用两种ROI编辑器 |
| Views/Results | Blob特征筛选及实际结果交互 |
| Views/Imaging | 共用图像面板、工具栏及viewport手势 |
| ViewModels/Editor | 单工具会话，借原DTO/卡片/输入/结果 |
| ViewModels/Browser | 原工程/选择与程序化命令，直接继承规范单工具会话 |
| Models/Browser | 原工程状态，直接继承VisionToolEditorModel |
| Views/Editor/ToolEditorView | FlowNodes原节点编辑器的实际入口，借用原IEditableTool |
| ViewModels/Execution/DetectionToolSession | FlowNodes.AINode仍使用的检测适配 |
| Themes/LegacyHalconResources.xaml | 正式主题内容保留，旧Ctr_Halcon URI转发退役 |

## 已退役，不要再引用

ToolCatalogView、ProjectToolbarView、ToolCardView、VisionTaskView、
ToolDiagnosticsView、ToolDebugView、ToolDebugSession、ToolDebugModel、VisionBrowserView及DebugView访问器。
相关旧资源、导航注册和DiagnosticsContent/DiagnosticsTab插槽已清理；不承诺这些旧类型或URI继续存在。
Compatibility目录已彻底删除：Ctr_PropertyPanel、Ctr_MultiSelectComboBox、Ctr_ToolPortBindingPanal及View/*、Ctr_Base/*、Ctr_Halcon/*旧URI转发退役；Themes/Generic.xaml仅有旧别名空模板，亦删除。正式属性面板和嵌套表格直接使用VisionPropertyPanel，选项使用VisionMultiSelectComboBox；不新增别名或转发层。

## 保持的约定

XAML与代码同名同目录；SDK/原生句柄仍留Tools，Controls只消费Core图像/结果。
原项目/配置存储、参数编辑器、执行会话和文件机制复用，不为目录整齐建立新工程或平行框架。
当前独立启动仍是Vision.Workbench，薄壳只导航Controls视觉流程页，不并入FlowNodes.Workbench。
范围、删除记录与门禁见[CODE_ORGANIZATION](CODE_ORGANIZATION.md)。
