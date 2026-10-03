# ScriptEditor.Controls

本模块已按 Model、Service、ViewModel 分层，继续使用 CommunityToolkit 的 ObservableObject、RelayCommand 和 AsyncRelayCommand。Core 保留脚本数据与契约，Runtime 拥有 Roslyn 引擎及逐脚本会话；Controls 通过注入服务绑定编辑器，不解析 Core 服务的全局容器。

`ScriptManagerService` 持有任务模型和脚本列表；`ScriptManagerViewModel` 转发操作并维护命令可用性。空任务禁用批量执行和循环；首末脚本禁用对应方向的移动命令。集合/选中项变化时立即刷新按钮状态。

`ScriptManagerView` 使用共享 PageHeaderView，页面自行合并 `Themes/PageStyles.xaml`，工具栏和编辑工具栏共用图标、尺寸及留白。工具栏允许换行，脚本标签置于顶部，空任务显示操作提示。页面、输出区和工作台背景使用动态 Card.Background，浅色为纯白，深色跟随 ThemeService。

管理页面 Loaded 时附加当前 VM 通知；DataContext 更换时先退订旧 VM；Unloaded 时立即退订。再次 Loaded 会补发状态。退订页面不会停止或释放运行中的脚本会话。

工作台维持 Prism 模块初始化后导航到 ScriptManagerView 的方式。编辑器服务通过宿主附加属性提供，脚本引用、ID、断点和任务工程的持久化边界保持现有 Core/Runtime 设计。

验证包含真实添加/复制/移动按钮的可用性、实际加载页面的 DataContext 更换与卸载退订、Roslyn 编辑器深浅主题切换后文档不变及纯白浅色表面；运行时、引用和持久化回归使用 ScriptEditor.Tests。
