# MotionControl.Controls

完整运动页面和复用轴控件，采用与 Device.Controls 相同的状态、交互和工作空间职责划分。

| 层 | 类型 | 职责 |
|---|---|---|
| Models | MotionPageModel、AxisStateModel | 可观察的页面配置、选择及轴状态；不调用驱动 |
| Services | MotionWorkspace | 连接、断开和页面自有卡生命周期，持有 MotionPageModel |
| Services | AxisWorkspace | 持有共享 AxisStateModel，执行轴操作及读取，管理刷新订阅 |
| ViewModels | MotionViewModel、AxisViewModel | CommunityToolkit.Mvvm 命令、输入校验和状态转发 |
| Views | MotionView、四个轴控件 | XAML 展示、依赖属性适配、Loaded/Unloaded 订阅及鼠标捕获行为 |

业务宿主注册 `MotionControlsModule` 后，可导航到 `MotionView`。宿主需要提供 Material Design 基础资源；页面自行合并 `Themes/PageStyles.xaml`，主题刷子使用动态资源。

页面复用 Device.Controls 同款 WPFCustomControls.PageHeaderView。MotionCard 样式由本模块维护，四个轴控件自行合并样式字典；统一 16px 内边距、12px 圆角与主题边框。工作台和页面使用 MaterialDesign.Brush.Card.Background 作为主表面，浅色显示纯白，深色及主辅色切换仍由主题服务控制。

模块默认注册单例 MotionWorkspace，同一模块工作空间的完整页面共享页面状态。四个独立控件仍为瞬态，保留 MotionCard、AxisIndex、RefreshIntervalMs 和原导航名称，不改写宿主 DataContext。AxisViewModel 原有属性、方法和构造函数保留为兼容转发；新 XAML 直接绑定 Model。

同一个 IMotionCard 对象的同一个轴共享 AxisWorkspace 和 AxisStateModel；不同卡对象隔离。卡缓存使用 ConditionalWeakTable。已加载的状态/总览控件订阅刷新，同轴多个订阅合并为一个计时器，使用当前订阅的最短间隔；卸载、切卡及切轴释放旧订阅，最后一个订阅释放后停止计时器。

MotionWorkspace 接受注入的 MotionManager，只通过 Create/Remove 管理自己创建的卡，不调用 Config 或 DisconnectAll，不释放借用的管理器。关闭页面不会释放模块持有的 Workspace；宿主退出或对应工作空间的所有者负责 Dispose。独立测试可创建 MotionWorkspace 并显式释放；工作台页面始终借用 DI 工作空间。

点动绑定原轴直到停止；松开鼠标、丢失捕获、卸载或切换卡/轴时均停止原轴。服务先检查连接与轴范围，再调用驱动；读取失败清空失效状态并保留诊断文字。

未连接或轴索引不可用时，Toolkit 命令通过 CanExecute 禁用运动操作；点动按钮和轴选择也同步可用状态。未连接时不显示虚构轴列表，位置显示“—”。多轴总览保留 ListView/GridView 展示轴号、位置、运行状态与操作反馈，空集合显示连接提示；反馈列按可用宽度伸展，窄窗口可横向滚动。

MotionControl.Workbench 只保留主题、窗口和壳导航 ViewModel，完整页面布局与页面 ViewModel 均由 Controls 提供。

验证入口：`Tests/MotionControl.Workbench.Tests` 覆盖实际按钮、共享状态、切轴输入保留、刷新订阅清理、注入替换、宿主卡隔离和点动停止。`Build/Test-WorkbenchVisual.ps1 -Names MotionControl -SwitchThemes` 检查实际工作台的主题切换及布局。

Motion 工作台沿用 Device 的壳导航方式：MainWindowViewModel 管理 NavItems、SelectedNav 和主题，模块加载后 SelectFirst 触发导航，侧栏选择同样走 RegionManager。主页直接使用 Controls.MotionView 和 MotionViewModel，不再存在 MotionWorkbenchView 或独立工作台页面 VM。App 退出时解除页面 VM 订阅并释放模块工作空间。
