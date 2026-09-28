using DataManager;
using DataManager.Controls.Services;
using DataManager.Controls.ViewModels;
using DataManager.Controls.Views;
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
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
           
            containerRegistry.RegisterSingleton<ThemeService>();
            containerRegistry.RegisterSingleton<SqlWorkspace<TestEntity>>();
            containerRegistry.RegisterSingleton<LogWorkspace>();
            containerRegistry.RegisterSingleton<ExcelWorkspace<ExcelPerson>>();
            containerRegistry.RegisterForNavigation<SqlView, SqlViewModel<TestEntity>>();
            containerRegistry.RegisterForNavigation<LogView, LogViewModel>();
            containerRegistry.RegisterForNavigation<ExcelView, ExcelViewModel<ExcelPerson>>();
        }

        protected override void OnInitialized()
        {
            // 官方做法：启动时把持久化的主题应用到 App.xaml 里的 BundledTheme 实例
            Container.Resolve<ThemeService>().Initialize();
            base.OnInitialized();
        }

        /// <summary>
        /// P9-5：退出前先取消在途数据库操作，再释放 SQL 实例——
        /// 保证关闭工作台后不会再有后台任务继续跑并回写界面。
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            try { Container.Resolve<SqlWorkspace<TestEntity>>().Dispose(); } catch { }
            base.OnExit(e);
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            // 注册 DataManager 模块（内部注册 LogManager / SQLManager / ExcelManager 单例）
            moduleCatalog.AddModule<DataManagerModule>();
            moduleCatalog.AddModule<WPFCustomControlsModule>();
        }
    }
}
