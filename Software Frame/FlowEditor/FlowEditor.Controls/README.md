# FlowEditor.Controls

流程编辑器的可复用 WPF 页面，使用 CommunityToolkit.Mvvm。宿主通过 Prism 导航到 `FlowChartContainerView`，并负责工具发现、对话框适配及 ProcessName 日志路由配置。

- Models 保存流程页面及多流程文档数据。
- Services 协调标签页创建、文档读写、工具箱、连接和导出；Core/Runtime 保持既有领域与执行边界。
- ViewModels 管理活动标签、节点选择和命令状态，服务通过构造器注入。
- Views 组合标题、命令栏、工具箱、画布、属性面板及输出。需要视觉元素的 PNG 导出由页面定位活动画布，再交给导出服务。

页面和工作台使用动态 Material Design 资源，浅色表面为纯白；深浅模式及主辅色变化无需重新创建页面。工具栏和输出筛选支持换行。切换活动标签时，属性面板立即同步所选节点或清空。

工作台只承担应用资源、模块加载、居中窗口及 Prism Region 导航。通用标题属于 Controls 页面，宿主不重复渲染。

验证记录：仓库 `Build/Baselines/Flow-Controls-UI-Validation.json`。验收测试包含真实按钮调用、标签切换、窄宽度输出工具栏以及保存/打开、PNG 导出路径。

容器执行命令沿用子流程的 CanExecute 条件：空流程禁用运行，空闲状态禁用停止，后台标签可运行时允许全部运行。循环等待间隔禁止单次启动。标签集合增删、替换及 Reset 统一协调选择/命令订阅，移除后不再接收旧标签事件；画布上下文清空时丢弃排队中的连线重绘。


画布在 Unloaded 时解除模型、节点和连线视觉订阅，清空视觉缓存；Loaded 时恢复当前 DataContext。卸载期间修改上下文不会订阅隐藏页面，重新显示后按最新流程重建。页面卸载不改变流程会话的运行状态。

