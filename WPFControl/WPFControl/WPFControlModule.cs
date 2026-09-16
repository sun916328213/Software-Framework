using Extension.Core.Reflection;
using DataManager.Log;
using FlowEditor.Core;
using FlowEditor.DataManagerLogging;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Prism.Ioc;
using Prism.Modularity;
using SkiaSharp;
using System.Globalization;
using System.Reflection;
using System.Windows;
using WPFControl.Helper; // 添加Helper命名空间引用
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
            if (Extension.Core.SystemEx.LangSwitchEx.Language == Extension.Core.SystemEx.ELang.ZH)
            {
                LiveCharts.Configure(config => config.HasGlobalSKTypeface(SKFontManager.Default.MatchCharacter('汉')));
            }

            // 合并WPFControl的资源字典到应用程序资源中
            MergeResourceDictionaries();
 

            //// 初始化TryCatchHelper
            var tryCatchHelper = new TryCatchHelper(containerProvider);

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
            containerRegistry.RegisterServicesFromNamespace(
                Assembly.GetExecutingAssembly(),
                "WPFControl.ProgramControl.Services");
            containerRegistry.RegisterSingleton<IFlowLogConfiguration>(provider =>
                provider.Resolve<LogConfigService>());
            containerRegistry.RegisterSingleton<IFlowLogSink>(provider =>
                new DataManagerFlowLogSink(
                    provider.Resolve<LogManager>(),
                    provider.Resolve<IFlowLogConfiguration>()));

            // 批量注册 ProgramControl.Views 命名空间下的导航视图（排除 UserInfoView，用作对话框）
            containerRegistry.RegisterNavigationViewsFromNamespace(
                Assembly.GetExecutingAssembly(),
                "WPFControl.ProgramControl.Views",
                t => t != typeof(UserInfoView));

            // ---- DataManager.Controls 的三个配置页：SqlView / LogView / ExcelView ----
            // 不在这里注册，只由本模块让它们随 WPFControl 一起被引用（见 csproj 的 ProjectReference）。
            //
            // 原因：SqlViewModel<T> / ExcelViewModel<T> 是泛型，类型实参是「宿主自己的行实体」
            // （Controls 里只约束 ISqlEntity / IExcelTestEntity）。而 WPFControl 是**被宿主引用的库**，
            // 模块里看不到宿主的实体类型 —— 反向引用会循环。Prism 的命名约定也推不出类型实参。
            // ⇒ 由宿主在自己的 App.RegisterTypes 里成对注册（见 Packing Machine\App.xaml.cs），
            //   与 DataManager.Workbench 的写法一致。
            //
            // 同理，页面样式也不在这里合并：那批键归 DataManager.Controls 自己所有，由各页面在
            // 自己的 UserControl.Resources 里合并 Themes\PageStyles.xaml。

            // 注册对话框视图
            containerRegistry.RegisterSingleton<UserInfoView>();
            containerRegistry.RegisterDialog<UserInfoView>();
        }
    }
  
}
