using DataManager;
using DataManager.Controls.Services;
using DataManager.Controls.ViewModels;
using DataManager.Controls.Views;
using DataManager.Workbench.Models;
using DataManager.Workbench.Views;
using Extension.Core.ThemeEx;
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

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            // 注册 DataManager 模块（内部注册 LogManager / SQLManager / ExcelManager 单例）
            moduleCatalog.AddModule<DataManagerModule>();
            moduleCatalog.AddModule<WPFCustomControlsModule>();
        }
    }
}
