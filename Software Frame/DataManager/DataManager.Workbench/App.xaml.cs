// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于验证宿主，模块组合与人工验证流程应与生产机型业务区分。
// 本文件定义：App。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Workbench.Infrastructure、WPFCustomControls.Workbench、Prism.Regions、DataManager.Core、DataManager.Controls、DataManager.Controls.Services。导入不代表所有类型都会在运行时加载。
using DataManager.Workbench.Infrastructure;
using WPFCustomControls.Workbench;
using Prism.Regions;
using DataManager.Core;
using DataManager.Controls;
using DataManager.Controls.Services;
using DataManager.Workbench.Models;
using DataManager.Workbench.Views;
using Extension.UI.Theme;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using System.Windows;
using WPFCustomControls;

namespace DataManager.Workbench
{
    /// <summary>
    /// Prism 应用入口
    /// </summary>
    public partial class App : PrismApplication
    {
    private WorkbenchLifetime? _lifetime;

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnStartup。
        /// 正文中的关键协作调用包括 EnsureAliasBrushes、OnStartup；具体条件和先后次序见方法体。
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            ThemeService.EnsureAliasBrushes();
            base.OnStartup(e);
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateShell。
        /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
        /// </summary>
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        /// <summary>
        /// 注册类型、服务或入口映射；对应入口为 RegisterTypes。
        /// 正文中的关键协作调用包括 RegisterSingleton；具体条件和先后次序见方法体。
        /// </summary>
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
           
            containerRegistry.RegisterSingleton<ThemeService>();
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnInitialized。
        /// 正文中的关键协作调用包括 Initialize、Resolve、OnInitialized、CancelInFlight、CloseRegionAsync；具体条件和先后次序见方法体。
        /// </summary>
        protected override void OnInitialized()
        {
            // 官方做法：启动时把持久化的主题应用到 App.xaml 里的 BundledTheme 实例
            Container.Resolve<ThemeService>().Initialize();
            base.OnInitialized();
        _lifetime = new WorkbenchLifetime(MainWindow,
            () => { (MainWindow.DataContext as IDisposable)?.Dispose(); return ValueTask.CompletedTask; },
            () => { Container.Resolve<SqlWorkspace<TestEntity>>().CancelInFlight(); return ValueTask.CompletedTask; },
            () => WorkbenchLifetime.CloseRegionAsync(Container.Resolve<IRegionManager>(), RegionNames.MainRegion),
            () => { Container.Resolve<SqlWorkspace<TestEntity>>().Dispose(); return ValueTask.CompletedTask; },
            () => { Container.Resolve<LogWorkspace>().Dispose(); return ValueTask.CompletedTask; });
            (MainWindow?.DataContext as DataManager.Workbench.ViewModels.MainWindowViewModel)?.SelectFirst();
        }

        /// <summary>
        /// P9-5：退出前先取消在途数据库操作，再释放 SQL 实例——
        /// 保证关闭工作台后不会再有后台任务继续跑并回写界面。
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
    {
        _ = _lifetime?.BeginCleanup();
        base.OnExit(e);
    }

        /// <summary>
        /// 配置当前对象或组合入口；对应入口为 ConfigureModuleCatalog。
        /// 正文中的关键协作调用包括 AddModule；具体条件和先后次序见方法体。
        /// </summary>
        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            // 注册 DataManager 模块（内部注册 LogManager / SQLManager / ExcelManager 单例）
            moduleCatalog.AddModule<DataManagerModule<TestEntity, ExcelPerson>>();
            moduleCatalog.AddModule<WPFCustomControlsModule>();
        }
    }
}
