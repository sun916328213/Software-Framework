# Vision.Controls

视觉工具浏览页面由 Controls 提供，具体工具目录和后端装配由宿主提供。

- `Models/VisionBrowserPageModel`：CommunityToolkit 页面状态，包括目录、选中工具、参数、当前卡片和执行反馈。
- `Services/VisionBrowserWorkspace`：选择工具、图像输入、合成图、Provider 选择、对比、Benchmark 和资源生命周期。`VisionBrowserCatalog` 注入工具工厂、Provider 工厂、图像加载和对比能力，Controls 流程不使用视觉 SDK 类型。
- `ViewModels/VisionBrowserViewModel`：Toolkit 命令和属性绑定；默认借用 Workspace，释放 VM 只解除自己的模型订阅。
- `Models/ToolCardModel`、`Services/ToolCardWorkspace`、`ViewModels/ToolCardViewModel`：卡片状态、标准工具执行及命令转发。卡片 Workspace 幂等释放其工具。
- `View/VisionBrowserView`：独立可绑定的完整页面，在模块中注册。宿主设置 Browser；页面自行合并样式并使用共享 PageHeaderView。

`Vision.Workbench.ToolBrowserViewModel` 保留现有入口，构造自身拥有的 Workspace，装配 18 项工具和三家 Provider。具体图像解码、后端比较和工具类型保留在 Workbench，避免将 SDK 代码搬进 Controls。原 ToolBrowserView 保留为薄包装，Prism 统一绑定主页 ViewModel，不再在 App 导航后手动设置 Browser。

页面使用动态 Card.Background，与 Device.Controls 的纯白浅色表面一致；深浅模式、主色和辅色由宿主 ThemeService 提供。工具卡片统一圆角、边框和留白，结果支持换行；ROI 添加使用 Toolkit 命令，列表显示类型并通过提示保留完整几何信息。

Workspace 负责先释放卡片/模型会话，再释放自己创建的 Provider。共享 Workspace 可供多个 VM 使用；借用者不会释放其工具。独立工作台 VM 显式拥有 Workspace，退出时由 App 释放。

验证包括现有工具执行回归、真实目录选择与按钮执行、ROI 增删、共享状态和幂等释放、实际工作台主题切换及紧凑窗口渲染。HALCON 与部分 AI Runtime 的执行仍依赖相应 SDK/许可证；可用性和校验结果继续在页面显示。

执行命令会刷新可用状态：没有图像时禁用后端对比，没有卡片时禁用 Benchmark。释放 ViewModel 后禁用自身命令；借用 Workspace 的其他页面继续使用共享状态。切换工具后旧卡片命令禁用且程序直接调用不会触碰已释放工具。直接调用存活浏览页的无图像对比仍保留提示文本。Workspace 释放后拒绝再次执行底层操作。

