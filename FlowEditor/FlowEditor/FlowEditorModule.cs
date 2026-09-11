using Extension.Core.ReflectionEx;
using FlowEditor.Core;
using FlowEditor.Interfaces;
using FlowEditor.Nodes;
using FlowEditor.Presentation.Factories;
using FlowEditor.Presentation.NodeEditing;
using FlowEditor.Presentation.Notifications;
using FlowEditor.Presentation.Threading;
using FlowEditor.Services;
using FlowEditor.Services.Storage;
using FlowEditor.ViewModels;
using Prism.Ioc;
using Prism.Modularity;
using System.Reflection;
using System.Windows;namespace FlowEditor
{
    /// <summary>
    /// FlowEditor 的 Prism 模块类。
    /// 负责注册流程图编辑器相关的视图、服务和工具类型。
    /// 所有跨方案复用的流程图功能均通过此模块加载。
    /// </summary>
    public class FlowEditorModule : IModule
    {
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
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // ── 服务层（IoC 管理生命周期） ──
            // 工具箱节点发现服务：单例，全局共享
            containerRegistry.RegisterSingleton<IFlowToolDiscoveryService, FlowToolDiscoveryService>();
            // 对话框服务：单例，封装消息框与文件对话框
            containerRegistry.RegisterSingleton<IDialogService, DialogService>();
            // 导出服务：单例，封装 PNG/SVG 导出
            containerRegistry.RegisterSingleton<IExportService, ExportService>();
            // 节点专用编辑器和信息提示 adapter：单例，集中隔离 WPF 窗口选择
            containerRegistry.RegisterSingleton<INodeEditorLauncher, WpfNodeEditorLauncher>();
            containerRegistry.RegisterSingleton<INodeNotificationSink, DialogNodeNotificationSink>();
            // UI 调度 adapter：应用服务无需直接依赖 WPF Application/Dispatcher
            containerRegistry.RegisterSingleton<IUiDispatcher, WpfUiDispatcher>();
            // JSON 文档存储：统一 .flow 与 .flows 文件读写
            containerRegistry.RegisterSingleton<IFlowDocumentStore, JsonFlowDocumentStore>();
            // 标签页 factory：把 Prism Resolve 限制在表现层 adapter
            containerRegistry.RegisterSingleton<IFlowTabFactory, PrismFlowTabFactory>();
            // 文档 mapper：无状态，统一单流程、多流程、快照与节点复制的 DTO 转换
            containerRegistry.RegisterSingleton<INodeFactory, PrismNodeFactory>();
            containerRegistry.RegisterSingleton<FlowDocumentMapper>();
            // 执行引擎：单例（无状态纯算法）
            containerRegistry.RegisterSingleton<FlowChartExecutionEngine>();
            // 流程图服务：瞬态，每个标签页独立实例
            containerRegistry.Register<IFlowChartService, FlowChartService>();
            // 工具箱服务：瞬态，每个容器 ViewModel 独立实例
            containerRegistry.Register<ToolboxService>();
            // 多流程容器文档服务：瞬态
            containerRegistry.Register<FlowChartContainerDocumentService>();
            // 连接领域模块：无状态，统一端口校验、建线与自动绑定规则
            containerRegistry.RegisterSingleton<ConnectionService>();

            // 注册流程图服务和 ViewModel（瞬态注册，每个标签页独立实例）
            containerRegistry.Register<FlowChartViewModel>();
            containerRegistry.Register<FlowChartContainerViewModel>();

            // 注册所有节点类型（通过反射扫描）
            RegisterNodeTypes(containerRegistry);

            // 注册 FlowEditor 的所有视图（按命名空间扫描，单例 + (object, 导航名) 单例）
            containerRegistry.RegisterNavigationViewsFromNamespace(Assembly.GetExecutingAssembly(), "FlowEditor.Views");
        }

        /// <summary>
        /// 通过反射扫描所有 NodeModel 子类并注册到容器中
        /// </summary>
        private void RegisterNodeTypes(IContainerRegistry containerRegistry)
        {
            // 内建基础节点（FlowControl/Common 等，随本程序集）+ 扩展工具节点（FlowNodes 程序集）
            var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };

            var flowNodesAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "FlowNodes")
                ?? TryLoadFlowNodesAssembly();

            if (flowNodesAssembly != null)
                assemblies.Add(flowNodesAssembly);
            else
                System.Diagnostics.Debug.WriteLine("[FlowEditorModule] FlowNodes assembly not found, only built-in nodes registered");

            foreach (var assembly in assemblies)
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!type.IsAbstract && type.IsSubclassOf(typeof(NodeModel)))
                        {
                            var attr = type.GetCustomAttribute<FlowToolAttribute>();
                            if (attr != null)
                            {
                                containerRegistry.Register(type);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        /// <summary>尝试显式加载 FlowNodes 程序集（尚未被加载时）。</summary>
        private static Assembly? TryLoadFlowNodesAssembly()
        {
            try { return Assembly.Load("FlowNodes"); }
            catch { return null; }
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
                var flowChartResources = new ResourceDictionary();
                flowChartResources.Source = new Uri(
                    "pack://application:,,,/FlowEditor;component/Resources/Resources.xaml",
                    UriKind.Absolute);
                app.Resources.MergedDictionaries.Add(flowChartResources);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"合并 FlowEditor 资源字典时出错: {ex.Message}");
            }
        }
    }
}
