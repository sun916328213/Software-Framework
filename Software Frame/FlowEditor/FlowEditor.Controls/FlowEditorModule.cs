// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：FlowEditorModuleOptions、FlowEditorModule。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Controls.Discovery、FlowEditor.Runtime、FlowEditor.Controls.Interfaces、FlowEditor.Controls.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Controls.Discovery;
using FlowEditor.Runtime;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Presentation.Factories;
using FlowEditor.Controls.Presentation.NodeEditing;
using FlowEditor.Controls.Presentation.Notifications;
using FlowEditor.Controls.Presentation.Threading;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.Services.Storage;
using FlowEditor.Controls.ViewModels;
using Prism.Ioc;
using Prism.Modularity;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace FlowEditor.Controls
{
    /// <summary>Controls whether the navigated editor page survives navigation.</summary>
    /// <remarks>
    /// FlowEditorModuleOptions 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
    /// </remarks>
    public sealed class FlowEditorModuleOptions
    {
        /// <summary>Production hosts reuse the editor page; independent workspaces can opt out.</summary>
        public bool ReuseNavigationViews { get; init; } = true;
    }

    /// <summary>
    /// FlowEditor 的 Prism 模块类。
    /// 负责注册流程图编辑器相关的视图、服务和工具类型。
    /// 所有跨方案复用的流程图功能均通过此模块加载。
    /// </summary>
    public class FlowEditorModule : IModule
    {
        private readonly bool _reuseViews;

        /// <summary>Creates a module with the existing production navigation policy.</summary>
        /// <remarks>
        /// 初始化 FlowEditorModule 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </remarks>
        public FlowEditorModule() : this(null) { }

        /// <summary>Creates a module with the host's page lifetime policy.</summary>
        /// <param name="options">Optional navigation policy; the default preserves the production editor page.</param>
        /// <remarks>
        /// 初始化 FlowEditorModule 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </remarks>
        public FlowEditorModule(FlowEditorModuleOptions? options)
            => _reuseViews = options?.ReuseNavigationViews ?? true;

        /// <summary>
        /// 模块初始化完成后调用
        /// </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
            MergeResourceDictionaries();
        }

        /// <summary>
        /// 注册模块的类型
        /// </summary>

        /// <summary>Registers defaults without overwriting host replacements.</summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            ArgumentNullException.ThrowIfNull(containerRegistry);
            IFlowNodeCatalog catalog;
            if (!containerRegistry.IsRegistered<IFlowNodeCatalog>())
            {
                catalog = new FlowNodeCatalog();
                containerRegistry.RegisterInstance<IFlowNodeCatalog>(catalog);
            }
            else
            {
                catalog = (containerRegistry as IContainerProvider
                    ?? throw new InvalidOperationException("The composition registry must expose the host catalog."))
                    .Resolve<IFlowNodeCatalog>();
            }
            // ── 服务层（IoC 管理生命周期） ──
            // 工具箱节点发现服务：单例，全局共享
            if (!containerRegistry.IsRegistered<IFlowToolDiscoveryService>())
                containerRegistry.RegisterSingleton<IFlowToolDiscoveryService, FlowToolDiscoveryService>();
            // 对话框服务：单例，封装消息框与文件对话框
            if (!containerRegistry.IsRegistered<IDialogService>())
                containerRegistry.RegisterSingleton<IDialogService, DialogService>();
            // 导出服务：单例，封装 PNG/SVG 导出
            if (!containerRegistry.IsRegistered<IExportService>())
                containerRegistry.RegisterSingleton<IExportService, ExportService>();
            // 节点专用编辑器和信息提示 adapter：单例，集中隔离 WPF 窗口选择
            if (!containerRegistry.IsRegistered<INodeEditorLauncher>())
                containerRegistry.RegisterSingleton<INodeEditorLauncher>(provider =>
                    new WpfNodeEditorLauncher(provider, provider.Resolve<IEnumerable<INodeEditorHandler>>()));
            if (!containerRegistry.IsRegistered<INodeNotificationSink>())
                containerRegistry.RegisterSingleton<INodeNotificationSink, DialogNodeNotificationSink>();
            // UI 调度 adapter：应用服务无需直接依赖 WPF Application/Dispatcher
            if (!containerRegistry.IsRegistered<IUiDispatcher>())
                containerRegistry.RegisterSingleton<IUiDispatcher, WpfUiDispatcher>();
            // JSON 文档存储：统一 .flow 与 .flows 文件读写
            if (!containerRegistry.IsRegistered<IFlowDocumentStore>())
                containerRegistry.RegisterSingleton<IFlowDocumentStore, JsonFlowDocumentStore>();
            // 标签页 factory：把 Prism Resolve 限制在表现层 adapter
            if (!containerRegistry.IsRegistered<IFlowTabFactory>())
                containerRegistry.RegisterSingleton<IFlowTabFactory, PrismFlowTabFactory>();
            // 文档 mapper：无状态，统一单流程、多流程、快照与节点复制的 DTO 转换
            if (!containerRegistry.IsRegistered<INodeFactory>())
                containerRegistry.RegisterSingleton<INodeFactory, PrismNodeFactory>();
            if (!containerRegistry.IsRegistered<FlowDocumentMapper>())
                containerRegistry.RegisterSingleton<FlowDocumentMapper>();
            // 日志上下文可共享；执行引擎含单次运行序号状态，每个流程服务使用独立实例
            if (!containerRegistry.IsRegistered<IExecutionLogContext>())
                containerRegistry.RegisterSingleton<IExecutionLogContext, FlowLogExecutionContextAdapter>();
            if (!containerRegistry.IsRegistered<FlowChartExecutionEngine>())
                containerRegistry.Register<FlowChartExecutionEngine>();
            // 流程图服务：瞬态，每个标签页独立实例
            if (!containerRegistry.IsRegistered<FlowChartService>())
                containerRegistry.Register<FlowChartService>();
            // 工具箱服务：瞬态，每个容器 ViewModel 独立实例
            if (!containerRegistry.IsRegistered<ToolboxService>())
                containerRegistry.Register<ToolboxService>();
            // 多流程容器文档服务：瞬态
            if (!containerRegistry.IsRegistered<FlowChartContainerDocumentService>())
                containerRegistry.Register<FlowChartContainerDocumentService>();
            // 连接领域模块：无状态，统一端口校验、建线与自动绑定规则
            if (!containerRegistry.IsRegistered<ConnectionService>())
                containerRegistry.RegisterSingleton<ConnectionService>();

            // 注册流程图服务和 ViewModel（瞬态注册，每个标签页独立实例）
            if (!containerRegistry.IsRegistered<FlowChartViewModel>())
                containerRegistry.Register<FlowChartViewModel>();
            if (!containerRegistry.IsRegistered<FlowChartContainerViewModel>())
                containerRegistry.Register<FlowChartContainerViewModel>();

            // 注册所有节点类型（通过反射扫描）
            RegisterNodeTypes(containerRegistry, catalog);

            // Only the editor page can be reused; canvas and child controls carry placement-specific state.
            RegisterNavigationViews(containerRegistry, Assembly.GetExecutingAssembly(), "FlowEditor.Controls.Views");
        }

        /// <summary>
        /// 将统一目录中带工具元数据的节点注册到容器，隐藏工具仍可还原。
        /// </summary>
        /// <param name="containerRegistry">宿主组合注册表。</param>
        /// <param name="catalog">宿主统一节点目录。</param>
        private static void RegisterNodeTypes(IContainerRegistry containerRegistry, IFlowNodeCatalog catalog)
        {
            foreach (var node in catalog.Nodes.Where(node => node.DisplayName != null))
                if (!containerRegistry.IsRegistered(node.NodeType)) containerRegistry.Register(node.NodeType);
        }

        /// <summary>区分可选程序集缺失与插件加载失败，保留后者的原始异常。</summary>
        internal static Assembly? LoadOptionalFlowNodes(Func<Assembly> load)
            => FlowNodeCatalog.LoadOptionalFlowNodes(load);

        /// <summary>按宿主策略注册编辑器主页，子控件保持瞬态。</summary>
        private void RegisterNavigationViews(
            IContainerRegistry registry,
            Assembly assembly,
            string namespacePrefix)
        {
            foreach (var viewType in assembly.GetTypes().Where(type =>
                         type.Namespace?.StartsWith(namespacePrefix, StringComparison.Ordinal) == true &&
                         type.IsSubclassOf(typeof(UserControl)) &&
                         type.IsClass &&
                         !type.IsAbstract))
            {
                if (viewType == typeof(Views.FlowChartContainerView))
                    Prism.Mvvm.ViewModelLocationProvider.Register<Views.FlowChartContainerView, FlowChartContainerViewModel>();
                if (!registry.IsRegistered(typeof(object), viewType.Name))
                {
                    if (_reuseViews && viewType == typeof(Views.FlowChartContainerView))
                        registry.RegisterSingleton(typeof(object), viewType, viewType.Name);
                    else
                        registry.Register(typeof(object), viewType, viewType.Name);
                }
            }
        }

        /// <summary>
        /// 合并 FlowEditor 的资源字典到应用程序资源中
        /// </summary>
        private void MergeResourceDictionaries()
        {
            try
            {
                var app = Application.Current;
                if (app == null) return;

                // FlowEditor 本地资源（MergedDictionaries 中已包含 ThemeColors.xaml）
                var source = new Uri(
                    "pack://application:,,,/FlowEditor.Controls;component/Resources/Resources.xaml",
                    UriKind.Absolute);
                if (!app.Resources.MergedDictionaries.Any(dictionary => dictionary.Source == source))
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = source });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"合并 FlowEditor 资源字典时出错: {ex.Message}");
            }
        }
    }
}
