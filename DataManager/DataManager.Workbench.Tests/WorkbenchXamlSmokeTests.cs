using DataManager.Workbench.Infrastructure;
using DataManager.Controls.Views;
using DataManager.Workbench.ViewModels;
using DataManager.Workbench.Views;
using Prism.Ioc;
using Prism.Regions;
using System.Windows;

namespace DataManager.Tests.Workbench
{
    /// <summary>
    /// 工作台启动冒烟测试：三页逐一导航创建，主窗口直接构造。
    /// 任一 XAML（StaticResource / PackIcon.Kind）或 Prism 接线（区域/导航注册/模块）有问题都会在此失败。
    /// （纯构建只能验 XAML 语法，资源键与导航必须运行时验。）
    /// App 由集合 Fixture 统一启动（单例），本测只做断言封送。
    /// </summary>
    [Collection("WorkbenchAppBoot")]
    public class WorkbenchXamlSmokeTests(WorkbenchAppFixture host)
    {
        private readonly WorkbenchAppFixture _host = host;

        [Fact]
        public void AppBoot_NavigateAllViews_NoErrors()
        {
            _host.Invoke(() =>
            {
                var regionManager = ContainerLocator.Container.Resolve<IRegionManager>();
                regionManager.RequestNavigate(RegionNames.MainRegion, nameof(SqlView));
                regionManager.RequestNavigate(RegionNames.MainRegion, nameof(LogView));
                regionManager.RequestNavigate(RegionNames.MainRegion, nameof(ExcelView));

                // 直接构造再验一遍解析（含需区域管理器的 MainWindow）
                _ = new MainWindow();
                _ = new SqlView();
                _ = new LogView();
                _ = new ExcelView();
            });
        }

        /// <summary>
        /// 壳加载后（MainWindow.Loaded → SelectFirst）既要导航到默认页，**也要把导航项选上**。
        /// 曾经只导航不设 SelectedNav，于是「内容区显示着 SQL 页、侧栏里却没有任何一项高亮」，
        /// 看着像什么都没选中。这条同时守住侧栏选中态（Primary 底 + Primary.Foreground 前景）能被走到。
        /// </summary>
        [Fact]
        public void ShellStartup_SelectsTheDefaultNavItem()
        {
            _host.Invoke(() =>
            {
                var window = Application.Current.MainWindow;
                Assert.NotNull(window);

                var vm = window!.DataContext as MainWindowViewModel;
                Assert.NotNull(vm);
                Assert.NotNull(vm!.SelectedNav);
                Assert.Same(vm.NavItems[0], vm.SelectedNav);
            });
        }
    }
}
