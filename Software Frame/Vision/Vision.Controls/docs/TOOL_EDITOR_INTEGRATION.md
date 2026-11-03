# 完整编辑器与单工具编辑入口

## 当前：唯一编辑器与节点工具配置文件（2026-10-26）

VisionBrowserView、原XAML/代码、旧pack URI及注册已删除，不留容器或资源壳。独立和节点窗口都直接使用VisionToolEditorView；Browser VM/Workspace仍保留实际工程能力。下方V1.25/V1.24/V1.23的隐藏文件栏约定是历史，已由本节替代。

- 节点窗口借用原workspace.Editor，ProjectCommands借用同一workspace。打开/保存/另存为作用于当前工具.visionproj，导入图像作用于该工具配方输入，执行仍走原Card；整个.flow仅由流程工具栏保存。
- 配置包含原参数、基准ROI、当前配方输入快照、可选托管取样/训练参数及模型引用；不包含节点/连接、运行结果、定位派生值或SDK模型。沿原v1/v2清单和图像包增加可选模板字段；清单仍限8MiB，大取样超过上限会明确失败，不截断旧文件。
- 导入要求工具身份和Provider与当前实例相同；Provider不同时先显式选择对应Provider。不替换原Session、Card、DTO或节点，不自动训练/加载引用模型。成功导入撤销旧结果及流程定位基准；保留连接和动态模式，必须重新显式捕获基准，不回退旧位姿。
- 运行/流程只读/关闭时禁用文件命令；对话框返回后复查状态和配方revision。取消/非法文件不修改配方，失败另存不接管路径。导入配方图像不改图像接线，接线运行仍以显式来源为准。
- 关闭工具窗口不释放节点会话。自定义未提供ProjectCommands的展示会话仍可显示禁用文件按钮；这不等于节点窗口缺少文件能力。


## V1.25阶段记录：与FlowNodes.Workbench相同的原生流程页面

VisionFlowView只做薄适配，内嵌原FlowChartContainerView；FlowCommandBarView、左工具箱、标签页、属性和输出区原样复用。不再创建视觉操作栏/日志区/三栏布局，不引用Workbench.exe，也无Vision.FlowEditor新项目。VisionFlowView旧路由保留。

输入/定位来源、匹配索引与基准已进入同一原PropertyGridHost及元数据生成；TargetObject仍是具体NodeModel。下拉使用EnumOption.Value=NodeId，显示名不参与保存。定位能力与模式控制显隐，搜索过滤不丢原行/参数。图像加载放在节点属性扩展动作，只修改所属任务页；单工具Controls原工程入口仍保留。原容器的新建/运行/保存/打开/导出命令不复制，原流程组恢复的候选失败保旧和资源释放已补齐。

多页视觉会话按原容器页归属，A窗口保持原Session/DTO/Card/ROI/结果，不随选页或选B重绑；关窗口不释放任务，删节点/移除页/宿主关闭才按原实际调用等待链清理。DataContext切换与视图重放置有STA回归，不等于物理鼠标/跨屏验收。

门禁451逻辑/417WPF/414通用流程/64属性定向通过，Vision宿主增量0警告0错误；额外FlowNodes宿主编译0错误、139警告。真实SDK、实图节拍与物理UI待验，详见EDITOR_REFINEMENT。以下V1.24/V1.23段落为历史，不作为当前顶部操作栏或来源配置位置说明。


## V1.24实际：现成节点属性＋按需完整窗口（R0首批）

主任务已直接使用FlowChartContainerView相同的PropertyGridHost/PropertyGridHostViewModel，TargetObject是所选NodeModel。Vision.FlowNode中的属性Builder仅复用原生成器并把显式白名单字段直接绑定到原DTO；不新增节点概要UI，也不复制配置。完整VisionToolEditorView已移到Workbench创建的Owned非模态窗口；原启动器路由到节点所属宿主，不再Resolve全局VM并Select节点。

- 每节点一个窗口，重复打开激活；借用原Workspace.Editor/Card/DTO/ROI/结果。选择B不重绑A；关窗只卸载借用视图，删节点和任务关闭先收窗，再沿原释放链等待实际调用。
- PropertyGridHost默认true的IsEditingEnabled仅锁编辑内容，搜索/滚动/扩展动作保留。Blob关键字段原DTO与专用适配双向刷新，真实修改标任务脏；选择与运行不误标脏。
- 窗口隐藏单工具文件操作，主任务仍保存原.flow；独立Controls/旧.visionproj不删。主任务主题入口留在主工具栏，不把同一个UIElement挂到多个窗口。
- R0未整阶段完成：来源/定位/基准仍保留在顶部“选中节点配置”，未纳入原属性元数据；没有把全局SelectedNode命令复制进A窗口。模式显隐与更多物理UI/跨屏收尾待做。
- 451逻辑/413WPF/414通用流程及62属性定向通过，宿主增量0警告/0错误。截图在Tests/obj/editor-verification/bin/Vision.Workbench.Tests/debug/r0-acceptance/；执行证据见../../Tests/EDITOR_REFINEMENT.md，不代表授权SDK或物理鼠标验收。

## V1.23已确认的设计约束（历史规划，实施进度以上文为准）

以IMPLEMENTATION_PLAN V1.23的R0为准：**不新建VisionNodePropertyView/节点概要控件/另一套PropertyGrid**。直接使用FlowEditor.Controls现成节点属性界面、绑定和元数据生成机制，TargetObject保持所选NodeModel，而非VisionToolEditorSession。

- 主界面：左侧现有工具箱、中间现有流程画布、右侧现成节点属性。将目前常驻的完整VisionToolEditorView从该属性区移出。当前代码尚未执行这个变更。
- 节点属性展示流程级名称、端口/输入、定位来源、关键状态；需要少量工具关键参数时仅在视觉节点侧适配原属性机制，getter/setter直达原DTO，沿用参数变化通知/结果失效及原面板刷新，不复制参数，不把Session的全部属性展开成“节点配置”。
- 增加“打开完整工具编辑器”入口；与节点双击/右键使用同一个INodeEditorLauncher/INodeEditorHandler。控件按钮只绑定命令，Workbench负责实际窗口/Owner/单实例激活，不让Controls引用FlowEditor或在ViewModel偷偷new工具。
- 完整窗口内仍是原VisionToolEditorView，借用打开时节点的原Workspace.Editor/Session、参数、Card、图像、ROI、模型和结果。打开不调用目录工厂、不重置图像、不训练/加载模型、不执行工具。
- 首期每节点一个非模态Owned窗口；重复打开激活旧窗，A/B选择互不误写，不能跨任务重开复用已关闭Session。窗口动作绑定明确节点，不能通过全局SelectedNode把A窗口命令作用到B。单工具执行和流程执行仍走原锁/准备/失效机制，不复制调度器。
- 关编辑窗口仅卸载视图订阅/结束手势，不Dispose节点/会话或停止流程；删节点先关对应窗口，任务关闭先禁止新开窗口、收窗口，再走原停止/等待实际调用/释放链。等待不持有UI/SDK互斥锁；SDK无即时取消能力时不伪装已取消。
- 所有真实参数/ROI/模型修改都写原对象、标记任务修改并清下游；仅运行状态/选择变化不误标配方已改。任务绑定窗口隐藏单工具工程文件操作，.flow由原任务保存；独立Controls/旧.visionproj入口仍保留。
- 门禁包括：现成节点属性身份、完整编辑器同对象/0次工厂新增、关键字段双向刷新、窗口重复打开/A-B切换/运行锁/删节点/宿主关闭、原文件/像素/ROI/模型/结果回归。不是把旧“内嵌ToolEditor”布局断言删掉就算完成。

以下章节描述Controls能力与独立/内嵌接入方式；主任务已采用上方V1.24窗口方式，R0剩余项继续保留。


## 职责

| 类型 | 职责 |
| --- | --- |
| `VisionToolInstanceWorkspace` | 节点原工具/卡片/DTO所有权及单工具配置文件命令，不保存流程图 |
| `VisionBrowserViewModel` / `VisionBrowserWorkspace` | 目录选择、工程文件、宿主委托、工具/Provider所有权 |
| `VisionToolEditorView` | 单工具图像 + 参数/ROI/结果；没有工具箱，可选工程工具栏 |
| `VisionToolEditorSession` / `VisionToolEditorModel` | 单工具展示状态、命令及订阅；可选借用工程命令，借用宿主卡片、DTO、图像、ROI及结果 |
| `IVisionProjectCommands` | Controls工程操作能力：打开、保存、另存为、导入图像及工程路径/标题；不要求界面具有工具箱 |
| Workbench | 独立薄宿主，注册模块并导航Controls流程页；工具目录/文件回调的可复用实现归Controls/Tools |

工程打开、保存、另存为、编解码及文件存储继续在 Controls，不搬回 Workbench。完整入口直接复用同一个 `VisionToolEditorView`，不是复制一套图像/属性界面。图像和详情通过唯一编辑器的ImagePanel / DetailsPanel访问，不保留Browser转发入口。

## 独立使用：编辑器自身包含工程操作

打开工程、保存、另存为、导入图像和执行固定显示，都是直接写在Views/Editor/VisionToolEditorView.xaml中的Button，不经ContentControl/DataTemplate生成。ShowProjectToolbar、ShowExecutionButton及对应DependencyProperty/显隐绑定已经删除，设计器无需解析显隐绑定即可显示全部按钮。Session提供ProjectCommands时绑定原文件命令，未绑定/没有工程能力时禁用而非折叠，空会话执行也禁用；运行中由原CanExecute和会话状态禁用，不改变文件与执行机制。

```csharp
// 复用Controls中原有的工程准备/提交、包编解码和保存逻辑。
// catalog是已注册工具的工厂/解码/选路径委托，不是工具箱UI。
using var workspace = new VisionBrowserWorkspace(catalog);
using var session = new VisionBrowserViewModel(workspace);
var editor = new VisionToolEditorView
{
    Session = session
};
// 展示editor；不必再套VisionBrowserView，也不会生成ToolCatalogView。
// 在窗口/页面真正结束使用时再Dispose session和workspace，不在Unloaded时释放。
```

VisionBrowserViewModel实现IVisionProjectCommands并通过ProjectCommands返回自身，因而新入口复用同一组命令，不复制工程读写逻辑。自定义Controls编辑会话也可覆写ProjectCommands提供相同能力，但命令必须操作该会话正在展示的同一工作区，不能把另一节点的工程命令接到当前编辑区。

Browser视图容器及旧ToolCatalogView、ProjectToolbarView、ToolDebugView桥均已删除。工程按钮与图像区在编辑器内部对齐；ToolbarAccessory直接设置在编辑器上。按钮只声明一份，换会话不隐藏、重建或复制按钮，独立和完整入口均只有一个执行按钮。

Views/Imaging/VisionImagePanel.xaml直接声明三个ListBoxItem（输入图像、ROI图像、结果图像），Tag使用ImageViewKind枚举，SelectedValuePath=Tag继续双向绑定SelectedImageView；不再绑定ImageViews集合生成固定选项。不增加枚举转换器。ImageViews属性及ImageViewOption类型已按用户授权直接删除，不保留兼容壳；现有XAML选项及SelectedImageView绑定不变。

## 不构造目录的单工具编辑

```csharp
// card、parameters、input、rois 均由宿主/节点持有。
// card 已经连接了该节点的输入及实际执行所用的ROI来源。
var state = new VisionToolEditorModel
{
    ToolIdentity = "检测节点 / Blob",
    CurrentCard = card,
    CurrentParameters = parameters,
    InputImage = input,
    SelectedImageView = ImageViewKind.Result
};
var session = new VisionToolEditorSession(state, rois,
    parametersChanged: () =>
    {
        card.ClearResult();
        card.RefreshValidation();
        // 在这里通知节点配方已修改（按宿主实际API实现）。
    });
var editor = new VisionToolEditorView { Session = session }; // 没有工程命令时文件按钮可见但禁用。
```

不需要 `ToolEntry`、`VisionBrowserCatalog`、`VisionBrowserWorkspace`、工程路径或文件存储。会话不调用工具工厂，不在打开界面时执行工具或重新设置输入，不清除节点已有结果。

宿主需要传递**同一份**运行参数与ROI编辑状态，不要为界面另建一套运行工具或参数副本。图像输入、搜索域捕获以及运行时实例由宿主先连接好；仅给展示模型赋值不能替代运行时输入/端口接线。输入变化后，宿主同步自己的卡片输入和展示模型。

## 所有权与关闭

- `VisionToolEditorView.Unloaded` 只退订视图事件和回滚未提交的绘制手势，不释放 `Session` 或工具。
- 宿主负责在不再需要调试会话时调用 `session.Dispose()`。它释放自己创建的属性适配器并退订，不释放 `card`、工具、输入、ROI或执行结果，不把宿主模型标为关闭。
- 宿主若已提供 `VisionToolEditorModel.BlobEditor`，会话只借用，不覆盖或释放。也可显式使用 `manageParameterEditor: false`，由宿主维护属性适配。
- 卡片/工具仍由原宿主负责最终释放；关闭调试窗口不等于删除节点或停止流程。
- 绑定期间换节点可替换 `editor.Session`；旧模型与旧卡片的迟到变化不能重置新节点的交互上下文。

## OpenCV找线：卡尺与旧搜索带

新建OpenCV FindLine工具默认使用Calipers（卡尺测量）。ROI页的法向半宽、沿线采样半宽、卡尺数量和画布上的青色框对应实际采样窗口；参数页提供平滑Sigma、投影梯度阈值、极性、每把卡尺All/First/Last选择及每端忽略点数。与HALCON共用ILineSearchRegion/LineSearchGeometry/LineRoiEditorView，不另建ROI或参数副本。

旧工程Parameters对象缺少SearchMode时仍按EdgeBand执行，旧CannyLow/CannyHigh/BandHalfWidth/MinEdgePoints字段不变；需要卡尺时在参数页将“找线方式”显式切换为Calipers，再保存。切换更新当前可见参数、说明和采样框并清旧结果，不自动执行。直接new FindLineParameters仍表示兼容旧参数；FindLineTool构造才显式选择新卡尺默认，JSON反序列化不会误继承工具默认。

OpenCV实际执行：双线性采样、沿切向均值投影、高斯平滑、法向梯度、峰值抛物线亚像素定位、极性/顺序选边与Welsch拟合。卡尺越界时整把跳过而不以图外黑色补边，至少两把有效卡尺才能拟合；有限工作量/尺寸/Sigma校验在分配前执行。当前输入沿用Tools.OpenCv已有紧凑Mono8边界。

两后端同参数时编辑几何可一致，但采样、梯度阈值、滤波、拟合实现并不相同，不保证数值或结果逐像素等价。合成边缘上的亚像素定位测试通过不替代用户实图/设备验收。不要仅给旧EdgeBand画采样框伪装卡尺。

## 图像显示与操作栏

VisionToolEditorView分别组合VisionImageToolbarView和VisionImagePanel。前者负责图像预设、选择、适应/1:1及标注开关，后者负责画布、状态/绘制确认与右键菜单；两者共用原Session，不新建工具或复制输入。

```csharp
var display = new VisionImagePanel { DataContext = session };
var toolbar = new VisionImageToolbarView { DataContext = session, ImagePanel = display };
// 将toolbar和display放到宿主布局的上下两行；只显示图时仅放display。
// 不依赖编辑会话的单图显示也可以直接赋display.ImageViewport.Image。
```

右键提供放大/缩小、实际像素1:1、适应图像、平移、选择/编辑、另存为。ImagePanel.SaveImage(path)保存当前底图原始尺寸的PNG/TIFF，不含标注/缩放；文件选择仅点击右键保存时打开，不在构造/加载中触发。公开ImageViewport和参数刷新事件保持，新增ImageToolbar入口用于独立名字作用域，WPFControls三个旧显示页现已改为组合VisionImagePanel；HObject只在Tools.Halcon转换边界生成托管快照，已无仓库消费者的HSmartWindowView、HSmartWindowHelper、Ctr_ROI及两个控件URI已退役；宿主旧主题URI保留。外部插件若引用这些旧类型需迁移，不保留空壳或假原生句柄。

## 能力与操作

- Provider、模型/模板、裁剪能力由模型标志及宿主回调明确提供，不按工具名或Backend字符串猜测。
- `cropInput`、`setTemplate`、`setCropRegion` 是可选回调；未提供时相应命令不执行。训练/保存模型等继续借用宿主提供的 `TemplateEditorViewModel`。
- 编辑界面固定显示一个直接声明的单工具执行按钮；独立和完整入口都复用这一实例，不切换模板或额外创建按钮。
- 流程宿主的“运行到该节点”等调度语义沿原流程工具栏命令；编辑器五项操作仍面向当前工具，不冒充完整流程调度/配方。节点借用会话不会自动创建工程工作区。
- ToolDiagnosticsView、DiagnosticsContent插槽及诊断Tab已删除。比较/Benchmark程序化能力本批未扩充，不再提供旧诊断界面。

## 兼容与范围

旧ToolDebugView/Session/Model及DebugView访问器已经删除；新接入使用VisionToolEditorView/Session/Model，Browser会话直接绑定Session。Compatibility目录、Ctr_PropertyPanel/Ctr_MultiSelectComboBox别名和旧资源URI转发已全部退役，正式属性面板及嵌套模板直接使用VisionPropertyPanel；目录规则和路径对照见[FILE_LAYOUT.md](FILE_LAYOUT.md)。

已有 `ToolEditorView.Tool : IEditableTool` 仍被FlowNodesEditorAdapter/AINode调用，其实现归位`Views/Editor/ToolEditorView.xaml(.cs)`；AINode的DetectionToolSession归位`ViewModels/Execution`。原CLR身份、参数、执行/准备模型/保存及加载卸载的借用边界保留，不只是留下空转发，也不强行把旧HALCON会话转换成新的运行时类型。新入口复用 `VisionImagePanel`、`ToolDetailsView` 和共享属性编辑器，没有第三套参数控件。迁移旧节点启动器、统一新流程端口、流程持久化和调度属于后续流程集成任务，不在本轮冒充完成。

完整回归命令见 `../../Tests/EDITOR_REFINEMENT.md`。测试覆盖无目录入口、原对象复用、参数回写、重载、换节点、释放边界以及完整编辑器工程命令；真实SDK、流程生产执行和人工主题/DPI另行验收。
