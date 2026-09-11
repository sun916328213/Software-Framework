using DataManager;
using DataManager.Workbench.Views;
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
           
            containerRegistry.RegisterSingleton<Services.ThemeService>();
            containerRegistry.RegisterSingleton<Services.SqlWorkspace>();
            containerRegistry.RegisterSingleton<Services.LogWorkspace>();
            containerRegistry.RegisterSingleton<Services.ExcelWorkspace>();
            containerRegistry.RegisterForNavigation<SqlView, ViewModels.SqlViewModel>();
            containerRegistry.RegisterForNavigation<LogView, ViewModels.LogViewModel>();
            containerRegistry.RegisterForNavigation<ExcelView, ViewModels.ExcelViewModel>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
        }

        protected override void OnInitialized()
        {
            // 官方做法：启动时把持久化的主题应用到 App.xaml 里的 BundledTheme 实例
            Container.Resolve<Services.ThemeService>().Initialize();
            base.OnInitialized();
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            // 注册 DataManager 模块（内部注册 LogManager / SQLManager / ExcelManager 单例）
            moduleCatalog.AddModule<DataManagerModule>();
            moduleCatalog.AddModule<WPFCustomControlsModule>();
        }
    }
}
