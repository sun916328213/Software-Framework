// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。 本文件属于组合注册入口，具体依赖可以在这里组装；注册页面不等于选择导航或启动设备。
// 本文件定义：WPFControlsModule。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.Controls.Diagnostics、Device.Controls、WPFControls.Integration.Alarms、Extension.Integration.Prism、DataManager.Core.Log、DataManager.Runtime.Log。导入不代表所有类型都会在运行时加载。
using WPFControls.Controls.Diagnostics;
using Device.Controls;
using WPFControls.Integration.Alarms;
using Extension.Integration.Prism;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.DataManagerLogging;
using Prism.Ioc;
using Prism.Modularity;
using System.Reflection;
using System.Windows;
using WPFControls.ProgramControl.Services;
using WPFControls.ProgramControl.Views;


namespace WPFControls
{
    /// <summary>
    /// WPFControl的Prism模块类
    /// 负责注册本模块的视图和服务
    /// </summary>
    [Prism.Modularity.ModuleDependency(nameof(DeviceControlsModule))]
    public class WPFControlsModule : IModule 
    {
        /// <summary>
        /// 模块初始化完成后调用
        /// </summary>
        /// <param name="containerProvider">容器提供者</param>
        public void OnInitialized(IContainerProvider containerProvider)
        {
            MergeResourceDictionaries();

        }

        /// <summary>
        /// 合并WPFControl的资源字典到应用程序资源中
        /// </summary>
        private void MergeResourceDictionaries()
        {
            try
            {
                // 获取当前应用程序
                var app = global::System.Windows.Application.Current;
                if (app == null) return;

                // 创建资源字典（含BundledTheme，须在启动时加载，避免首次打开登录窗口时生成主题耗时）


                MergeResource(app, "pack://application:,,,/WPFControls;component/Shared/Resources/Resources.xaml");
                MergeResource(app, "pack://application:,,,/WPFControls;component/Resources/NavigationStyle.xaml");

                // 说明：DataManager.Controls 三个测试页（SqlView / LogView / ExcelView）的样式
                // **不在这里合并**。那批键归 DataManager.Controls 自己所有，由各页面在自己的
                // UserControl.Resources 里合并 Themes\PageStyles.xaml。
                // 宿主替页面合并样式是错的：样式跟着页面走，页面换宿主（Workbench / Packing Machine）
                // 都不需要宿主配合；宿主也不该反向依赖页面的资源键。
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"合并资源字典时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 MergeResource。
        /// 正文中的关键协作调用包括 Any、Add；具体条件和先后次序见方法体。
        /// </summary>
        private static void MergeResource(System.Windows.Application app, string source)
        {
            var uri = new Uri(source);
            if (!app.Resources.MergedDictionaries.Any(dictionary => dictionary.Source == uri))
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = uri });
        }

        /// <summary>
        /// 注册模块的类型
        /// </summary>
        /// <param name="containerRegistry">容器注册器</param>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            ArgumentNullException.ThrowIfNull(containerRegistry);
            containerRegistry.RegisterNavigationViewsFromNamespace(typeof(WPFControlsModule).Assembly, "WPFControls.Controls.Views");
            WPFControlsComposition.RegisterServices(containerRegistry);
            WPFControls.Composition.FeatureViewModelComposition.Register();
            if (!containerRegistry.IsRegistered<FlowEditor.Controls.Interfaces.INodeEditorHandler>())
                containerRegistry.RegisterSingleton<FlowEditor.Controls.Interfaces.INodeEditorHandler,
                    FlowNodes.Presentation.NodeEditing.FlowNodesEditorAdapter>();
            if (!containerRegistry.IsRegistered<TryCatchHelper>())
                containerRegistry.RegisterSingleton<TryCatchHelper>(provider => new TryCatchHelper(error =>
                    provider.Resolve<LogInfoService>().AddRunInfo(error.ToString(), DataManager.Core.Log.Definition.ELogLevel.SysError)));

            if (!containerRegistry.IsRegistered<PrintService>()) containerRegistry.RegisterSingleton<PrintService>();
            if (!containerRegistry.IsRegistered(typeof(object), nameof(PrintView)))
                containerRegistry.RegisterForNavigation<PrintView, WPFControls.ProgramControl.ViewModels.Printer.PrintViewModel>();

            WPFControls.Integration.Queries.QueryComposition.Register(containerRegistry);

            // 固定服务显式注册，避免新类进入目录后被自动注册。
            if (!containerRegistry.IsRegistered<AlarmInfoManagerService>()) containerRegistry.RegisterSingleton<AlarmInfoManagerService>();
            if (!containerRegistry.IsRegistered<BulletinBoardService>()) containerRegistry.RegisterSingleton<BulletinBoardService>();
            if (!containerRegistry.IsRegistered<CapacityStatisticsService>())
                containerRegistry.RegisterSingleton<CapacityStatisticsService>(provider => new CapacityStatisticsService(
                    provider.Resolve<TryCatchHelper>(),
                    provider.Resolve<WPFControls.Queries.Data.ICapacityStatisticsQueryService<WPFControls.ProgramControl.Models.CapacityStatisticsRow>>(),
                    provider.Resolve<WPFControls.Queries.Data.ICapacityStatisticsWriteService<WPFControls.ProgramControl.Models.CapacityStatisticsRow>>()));
            if (!containerRegistry.IsRegistered(typeof(DataQueryService<,>))) containerRegistry.RegisterSingleton(typeof(DataQueryService<,>), typeof(DataQueryService<,>));
            if (!containerRegistry.IsRegistered<ImageQueryService>()) containerRegistry.RegisterSingleton<ImageQueryService>();
            if (!containerRegistry.IsRegistered<AutoFlowService>()) containerRegistry.RegisterSingleton<AutoFlowService>();
            if (!containerRegistry.IsRegistered<InitializeFlowService>()) containerRegistry.RegisterSingleton<InitializeFlowService>();
            if (!containerRegistry.IsRegistered<ShutdownFlowService>()) containerRegistry.RegisterSingleton<ShutdownFlowService>();
            if (!containerRegistry.IsRegistered<StartupFlowService>()) containerRegistry.RegisterSingleton<StartupFlowService>();
            if (!containerRegistry.IsRegistered<LogAlarmInfoService>()) containerRegistry.RegisterSingleton<LogAlarmInfoService>();
            if (!containerRegistry.IsRegistered<LogConfigService>()) containerRegistry.RegisterSingleton<LogConfigService>();
            if (!containerRegistry.IsRegistered<LogInfoService>()) containerRegistry.RegisterSingleton<LogInfoService>();
            if (!containerRegistry.IsRegistered<LogQueryService>()) containerRegistry.RegisterSingleton<LogQueryService>();
            if (!containerRegistry.IsRegistered<SEMILogQueryService>()) containerRegistry.RegisterSingleton<SEMILogQueryService>();
            if (!containerRegistry.IsRegistered<SEMILogService>()) containerRegistry.RegisterSingleton<SEMILogService>();
            if (!containerRegistry.IsRegistered<SEMIManagerService>()) containerRegistry.RegisterSingleton<SEMIManagerService>();
            if (!containerRegistry.IsRegistered<PrintManagerService>()) containerRegistry.RegisterSingleton<PrintManagerService>();
            if (!containerRegistry.IsRegistered<ControlService>()) containerRegistry.RegisterSingleton<ControlService>();
            if (!containerRegistry.IsRegistered<DisplayService>()) containerRegistry.RegisterSingleton<DisplayService>();
            if (!containerRegistry.IsRegistered<MenuService>()) containerRegistry.RegisterSingleton<MenuService>();
            if (!containerRegistry.IsRegistered<ResourceMonitorService>()) containerRegistry.RegisterSingleton<ResourceMonitorService>();
            if (!containerRegistry.IsRegistered<StatisticService>()) containerRegistry.RegisterSingleton<StatisticService>();
            if (!containerRegistry.IsRegistered<StatusBarService>()) containerRegistry.RegisterSingleton<StatusBarService>();
            if (!containerRegistry.IsRegistered<WindowMenuService>()) containerRegistry.RegisterSingleton<WindowMenuService>();
            if (!containerRegistry.IsRegistered<UserInfoService>()) containerRegistry.RegisterSingleton<UserInfoService>();
            if (!containerRegistry.IsRegistered<VisionInspectionItemService>()) containerRegistry.RegisterSingleton<VisionInspectionItemService>();
            AlarmHistoryComposition.Register(containerRegistry,
                provider => provider.Resolve<AlarmInfoManagerService>().FilePath);
        
            if (!containerRegistry.IsRegistered<IFlowLogSink>())
                containerRegistry.RegisterSingleton<IFlowLogSink>(provider =>new DataManagerFlowLogSink(provider.Resolve<ILogManager>()));

            // 批量注册 ProgramControl.Views 命名空间下的导航视图（排除 UserInfoView，用作对话框）
            containerRegistry.RegisterNavigationViewsFromNamespace(Assembly.GetExecutingAssembly(), "WPFControls.ProgramControl.Views",t => t != typeof(UserInfoView));
 
            // 注册对话框视图
            if (!containerRegistry.IsRegistered<UserInfoView>()) containerRegistry.RegisterSingleton<UserInfoView>();
            if (!containerRegistry.IsRegistered(typeof(object), nameof(UserInfoView)))
                containerRegistry.RegisterDialog<UserInfoView>();

            // Device.Controls is loaded by the host module catalog.

        }
    }
  
}
