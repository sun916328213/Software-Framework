using DataManager.Workbench;
using DataManager.Workbench.Infrastructure;
using DataManager.Workbench.Views;
using Prism.Ioc;
using Prism.Regions;
using System.Windows.Threading;

namespace DataManager.Tests.Workbench
{
    /// <summary>
    /// 工作台启动冒烟测试：STA 线程无头完整启动 Prism App（资源字典 + 模块 + 主窗口），
    /// 再把三页逐一导航创建，最后关停。任一 XAML（StaticResource / PackIcon.Kind）或
    /// Prism 接线（区域/导航注册/模块）有问题都会在此失败。
    /// （纯构建只能验 XAML 语法，资源键与导航必须运行时验。）
    /// </summary>
    public class WorkbenchXamlSmokeTests
    {
        [Fact]
        public void AppBoot_NavigateAllViews_NoErrors()
        {
            Exception? error = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var app = new App();
                    app.InitializeComponent(); // 等价于生产入口：先载 App.xaml 资源再 Run
                    app.Startup += (_, _) =>
                    {
                        app.Dispatcher.BeginInvoke(new Action(() =>
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

                            app.Dispatcher.BeginInvoke(new Action(() => app.Shutdown()));
                        }), DispatcherPriority.ApplicationIdle);
                    };
                    app.Run();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(90)))
                throw new TimeoutException("工作台启动超时");
            Assert.Null(error);
        }
    }
}
