using Extension.Integration.Prism;
using DataManager.Log;
using FlowEditor.Core;
using FlowEditor.DataManagerLogging;
using Device.Controls.Services;
using Device.Controls.ViewModels;
using Device.Controls.Views;
using Device.Core.Cameras;
using Device.Core.LabelPrinting;
using Device.Core.Lights;
using Device.Core.Rfid;
using Device.Core.Scanners;
using Device.Runtime.Cameras;
using Device.Runtime.LabelPrinting;
using Device.Runtime.Lights;
using Device.Runtime.Rfid;
using Device.Runtime.Scanners;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Prism.Ioc;
using Prism.Modularity;
using SkiaSharp;
using System.Globalization;
using System.Reflection;
using System.Windows;
using WPFControl.Helper; // 添加Helper命名空间引用
using WPFControl.Integration.Device;
using WPFControl.ProgramControl.Services;
using WPFControl.ProgramControl.Views;


namespace WPFControl
{
    /// <summary>
    /// WPFControl的Prism模块类
    /// 负责注册本模块的视图和服务
    /// </summary>
    public class WPFControlModule : IModule 
    {
        /// <summary>
        /// 模块初始化完成后调用
        /// </summary>
        /// <param name="containerProvider">容器提供者</param>
        public void OnInitialized(IContainerProvider containerProvider)
        {
            // 模块初始化完成后的逻辑
            // 可以在这里执行一些需要容器完全配置后才能执行的操作

            // 初始化应用程序语言设置
            InitializeLanguage();

            // 配置LiveCharts中文字体
            if (Extension.UI.Controls.LangSwitchEx.Language == Extension.UI.Controls.ELang.ZH)
            {
                LiveCharts.Configure(config => config.HasGlobalSKTypeface(SKFontManager.Default.MatchCharacter('汉')));
            }

            // 合并WPFControl的资源字典到应用程序资源中
            MergeResourceDictionaries();

            // 2026-09-24（P8-3）：旧 Device 配置一次性迁移（幂等、非破坏）。
            // 必须早于任何新设备页读 Config/*.json —— 页面在导航时才创建，这里足够早。
            TryImportLegacyDeviceConfig();

            //// 初始化TryCatchHelper
            var tryCatchHelper = new TryCatchHelper(containerProvider);

        }

        /// <summary>
        /// 执行旧 Device 配置迁移（P8-3），并把结果写到调试输出。
        /// 迁移本身幂等且非破坏（新文件已存在即跳过、旧文件永不改动），
        /// 因此任何异常都不应阻断模块初始化 —— 只记日志。
        /// </summary>
        private static void TryImportLegacyDeviceConfig()
        {
            try
            {
                var result = LegacyDeviceConfigImporter.Import();

                foreach (var file in result.Imported)
                    System.Diagnostics.Debug.WriteLine($"旧设备配置已迁移：Config/{file}");
                foreach (var file in result.Skipped)
                    System.Diagnostics.Debug.WriteLine($"旧设备配置跳过（新文件已存在）：Config/{file}");
                foreach (var warning in result.Warnings)
                    System.Diagnostics.Debug.WriteLine($"旧设备配置迁移告警：{warning}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"旧设备配置迁移失败（已忽略，不影响启动）：{ex.Message}");
            }
        }

        /// <summary>
        /// 合并WPFControl的资源字典到应用程序资源中
        /// </summary>
        private void MergeResourceDictionaries()
        {
            try
            {
                // 获取当前应用程序
                var app = Application.Current;
                if (app == null) return;

                // 创建资源字典（含BundledTheme，须在启动时加载，避免首次打开登录窗口时生成主题耗时）
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/WPFControl;component/Resources/Resources.xaml")
                });

                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/WPFControl;component/Resources/NavigationStyle.xaml")
                });

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
        /// 初始化应用程序语言设置
        /// </summary>
        private void InitializeLanguage()
        {
            try
            {
                // 设置默认语言为系统语言
                var culture = CultureInfo.CurrentUICulture;
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
  
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化语言设置时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 注册模块的类型
        /// </summary>
        /// <param name="containerRegistry">容器注册器</param>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {  
            // 批量注册 CustomControl.Views 命名空间下的导航视图
            containerRegistry.RegisterNavigationViewsFromNamespace(Assembly.GetExecutingAssembly(), "WPFControl.CustomControl.Views");

            // 批量注册 ProgramControl.Services 命名空间下的服务
            containerRegistry.RegisterServicesFromNamespace(Assembly.GetExecutingAssembly(), "WPFControl.ProgramControl.Services");
        
            containerRegistry.RegisterSingleton<IFlowLogSink>(provider =>new DataManagerFlowLogSink(provider.Resolve<LogManager>()));

            // 批量注册 ProgramControl.Views 命名空间下的导航视图（排除 UserInfoView，用作对话框）
            containerRegistry.RegisterNavigationViewsFromNamespace(Assembly.GetExecutingAssembly(), "WPFControl.ProgramControl.Views",t => t != typeof(UserInfoView));
 
            // 注册对话框视图
            containerRegistry.RegisterSingleton<UserInfoView>();
            containerRegistry.RegisterDialog<UserInfoView>();

            // 2026-09-24（P8-1）：新 Device 栈的组合根。WPFControl 是宿主侧 Prism 模块，
            // 天然承担组合职责 —— 工厂在这里注册，而不是把 Prism 依赖塞进 Device.Runtime。
            // 只注册旧 WPFControl 有对应入口的 5 类设备（相机 / 光源 / RFID / 扫码 / 标签打印）；
            // FrameGrabber 与原始网络 Printer 在旧 WPFControl 没有入口，注册进来只会扩大 P8 范围。
            // 工厂构造只保存延迟创建委托，此处不会连接或枚举任何真实硬件。
            containerRegistry.RegisterInstance<ICameraFactory>(new CameraFactory());
            containerRegistry.RegisterInstance<ILightControllerFactory>(new LightControllerFactory());
            containerRegistry.RegisterInstance<IRfidDeviceFactory>(new RfidDeviceFactory());
            containerRegistry.RegisterInstance<IScannerFactory>(new ScannerFactory());
            containerRegistry.RegisterInstance<ILabelPrintEngineFactory>(new LabelPrintEngineFactory());

            // Workspace 单例：档案集合、连接状态与实时循环必须跨导航存活，不能每次进页面重建。
            containerRegistry.RegisterSingleton<CameraWorkspace>();
            containerRegistry.RegisterSingleton<LightWorkspace>();
            containerRegistry.RegisterSingleton<RfidWorkspace>();
            containerRegistry.RegisterSingleton<ScannerWorkspace>();
            containerRegistry.RegisterSingleton<LabelPrintWorkspace>();

            // Device.Controls 的页面不在本程序集内，上面的按命名空间批量注册扫不到它们，
            // 故显式注册为 (object, 导航名) 单例 —— 与其它导航视图同一约定，
            // 宿主的 PrewarmNavigationViews 才有意义（瞬时注册会让预热实例被丢弃）。
            containerRegistry.RegisterNavigationView<CameraView, CameraViewModel>();
            containerRegistry.RegisterNavigationView<LightView, LightViewModel>();
            containerRegistry.RegisterNavigationView<RfidView, RfidViewModel>();
            containerRegistry.RegisterNavigationView<ScannerView, ScannerViewModel>();
            containerRegistry.RegisterNavigationView<LabelPrintView, LabelPrintViewModel>();
        }
    }
  
}
