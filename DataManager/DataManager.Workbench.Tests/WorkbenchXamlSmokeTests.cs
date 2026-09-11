using DataManager.Workbench.Infrastructure;
using DataManager.Workbench.Views;
using Prism.Ioc;
using Prism.Regions;

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
    }
}
