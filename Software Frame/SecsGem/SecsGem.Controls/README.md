# SecsGem.Controls

完整的 SECS/GEM 联调页由 Controls 提供，宿主通过 Prism 导航到 `SecsView`。页面自行合并 `Themes/PageStyles.xaml`，宿主提供 Material Design 主题资源。

- `Models/SecsPageModel`：CommunityToolkit ObservableProperty 页面输入、目录、选中项、运行状态和消息列表。
- `Services/SecsWorkspace`：共享页面模型，执行连接、配置、SML、变量、事件和报警流程；订阅运行时事件并将通知送回 UI Dispatcher。可见页面通过引用计数租约共享状态轮询。
- `ViewModels/SecsViewModel`：CommunityToolkit RelayCommand 转发流程并暴露 Model；保留旧属性绑定作为兼容入口。
- `Views/SecsView`：共享页头、白色浅色背景、主题画刷和布局。加载时取得轮询租约并监听消息滚动；卸载/释放时解除页面订阅。

`SecsWorkspace` 在模块内注册为单例，`SecsView` 为独立页面实例。释放 View/VM 不会释放共享 Workspace；释放 Workspace 只停止轮询并退订事件，不释放注入的 `SecsGemRuntime`。

工作台直接通过 Prism 导航到 `SecsView`，不再有 `SecsWorkbenchView` 包装层。App 负责启动模块、导航主页及退出释放；仅独立工作台显式启用示例 HCACK 远程命令处理器和 WPF 终端显示（在 `App.OnInitialized` 里设置，通用 Controls 页面不会替换宿主的远程命令处理器）。运行时所有者负责清除工作台处理器并释放运行时。壳层 `MainWindow` 放在 `SecsGem.Workbench.Views` 下，DataContext 交给 Prism `ViewModelLocator` 按 Views→ViewModels 约定注入。

浅色背景使用动态 `MaterialDesign.Brush.Card.Background`，与 Device.Controls 一致；页头使用共享 `PageHeaderView`。S10 终端文字使用动态 Foreground，不再使用阴影颜色。

验证：`Tests/SecsGem.Workbench.Tests` 包含真实按钮绑定、错误输入、共享模型、页面/Workspace 释放边界与实际终端窗口深浅主题检查；协议和分层回归位于 `Tests/SecsGem.Tests`。

命令根据 Model.IsConnected、连接参数、SML 和选中变量/事件/报警刷新可用状态。端口使用 1–65535，Device ID 使用 0–32767，与 Core 参数校验一致。变量和配置保留离线编辑；网络消息要求连接。释放 VM 禁用自身命令且拒绝新轮询租约，其他借用页面继续工作。Workspace 关闭后禁用页面命令、忽略后续操作和完成日志，不释放宿主持有的 Runtime。

