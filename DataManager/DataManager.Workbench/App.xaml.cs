using DataManager;
using DataManager.Workbench.Views;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using System.Windows;

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
            containerRegistry.RegisterForNavigation<SqlView, ViewModels.SqlViewModel>();
            containerRegistry.RegisterForNavigation<LogView, ViewModels.LogViewModel>();
            containerRegistry.RegisterForNavigation<ExcelView, ViewModels.ExcelViewModel>();
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            // 注册 DataManager 模块（内部注册 LogManager / SQLManager / ExcelManager 单例）
            moduleCatalog.AddModule<DataManagerModule>();
        }
    }
}
