// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：WorkbenchXamlSmokeTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Workbench、DataManager.Core、DataManager.Workbench.Infrastructure、DataManager.Controls.Views、DataManager.Workbench.ViewModels、DataManager.Workbench.Views。导入不代表所有类型都会在运行时加载。
using DataManager.Workbench;
using DataManager.Core;
using DataManager.Workbench.Infrastructure;
using DataManager.Controls.Views;
using DataManager.Workbench.ViewModels;
using DataManager.Workbench.Views;
using Prism.Ioc;
using Prism.Regions;
using System.Windows;

namespace DataManager.Workbench.Tests
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

        /// <summary>
        /// 测试相关入口 AppBoot_NavigateAllViews_NoErrors；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Invoke、Resolve、RequestNavigate、nameof；具体条件和先后次序见方法体。
        /// </summary>
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
                var probeWindow = new MainWindow();
                try
                {
                    _ = new SqlView();
                    _ = new LogView();
                    _ = new ExcelView();
                }
                finally
                {
                    try { (probeWindow.DataContext as IDisposable)?.Dispose(); }
                    finally { probeWindow.Close(); }
                }
                Assert.DoesNotContain(probeWindow, Application.Current.Windows.Cast<Window>());
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
