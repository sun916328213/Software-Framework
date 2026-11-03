using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Extension.UI.Theme;
using MaterialDesignThemes.Wpf;
using Prism.Ioc;
using Prism.Regions;
using Vision.Controls.View;
using Vision.Controls.ViewModels;

namespace Vision.Workbench.Tests;

/// <summary>在独立Vision薄宿主中验证Controls流程导航与资源；不合并FlowNodes宿主。</summary>
[Collection("WorkbenchAppBoot")]
public sealed class WorkbenchCompositionTests(WorkbenchAppFixture host)
{
    [Fact]
    public void AppBoot_ShowsIndependentVisionMainWindow_WithoutStartingHardware()
    {
        host.Invoke(() =>
        {
            Assert.IsType<MainWindow>(Application.Current.MainWindow);
            Assert.True(System.IO.File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, VisionEngineConfiguration.FileName)));
            Assert.True(((IContainerRegistry)ContainerLocator.Container).IsRegistered<Vision.Core.IVisionEngine>());
            // 这里只检查注册，不解析新引擎，实际启动/关闭不会因此初始化SDK。
        });
    }

    [Fact]
    public void FlowPageAndViewModel_AreOwnedByControls_WithoutRetiredHostReferences()
    {
        host.Invoke(() =>
        {
            var controls = typeof(VisionToolEditorView).Assembly;
            Assert.Same(controls, typeof(VisionFlowView).Assembly);
            Assert.Same(controls, typeof(VisionFlowViewModel).Assembly);
            Assert.DoesNotContain(controls.GetReferencedAssemblies(), a => a.Name == "Vision.Workbench");
            Assert.Equal("Vision.Workbench", typeof(App).Assembly.GetName().Name);
            Assert.DoesNotContain(typeof(App).Assembly.GetReferencedAssemblies(), a => a.Name == "FlowNodes.Workbench");
            Assert.Null(controls.GetType("Vision.Workbench.Views.VisionFlowView"));
            Assert.IsType<VisionFlowView>(Application.LoadComponent(
                new Uri("/Vision.Controls;component/Views/Flow/VisionFlowView.xaml", UriKind.Relative)));
        });
    }

    [Fact]
    public void Navigation_DirectlyHostsFlowPage_AndReusesPageAndSession()
    {
        host.Invoke(() =>
        {
            var regions = ContainerLocator.Container.Resolve<IRegionManager>();
            var region = regions.Regions["VisionRegion"];
            regions.RequestNavigate("VisionRegion", nameof(VisionFlowView));
            var page = Assert.IsType<VisionFlowView>(Assert.Single(region.ActiveViews));
            var flow = Assert.IsType<VisionFlowViewModel>(page.DataContext);
            page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var model = flow.Chart.Model;
            region.Deactivate(page);
            Assert.Empty(region.ActiveViews);
            NavigationResult? result = null;
            regions.RequestNavigate("VisionRegion", nameof(VisionFlowView), value => result = value);
            page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.NotNull(result); Assert.Null(result.Error); Assert.True(result.Result);
            Assert.Same(page, Assert.Single(region.ActiveViews));
            Assert.Same(flow, page.DataContext); Assert.Same(model, flow.Chart.Model);
            Assert.Same(flow.Editor, Assert.IsAssignableFrom<Control>(page.FindName("FlowHost")).DataContext);
            Assert.True(flow.IsEditingEnabled);
        });
    }

    [Fact]
    public void ThemeService_UsesHostThemeBeforeNestedControlsTheme()
    {
        host.Invoke(() =>
        {
            var theme = Assert.Single(Application.Current.Resources.MergedDictionaries.OfType<BundledTheme>());
            Assert.Same(theme, ThemeService.FindBundledTheme());
        });
    }

    [Fact]
    public void AppBoot_MergesHostAndControlsResources()
    {
        host.Invoke(() => Assert.True(Application.Current.Resources.MergedDictionaries.Count >= 3));
    }

    [Theory]
    [InlineData("pack://application:,,,/Vision.Workbench;component/MainWindow.xaml")]
    [InlineData("pack://application:,,,/Vision.Workbench;component/App.xaml")]
    [InlineData("pack://application:,,,/Vision.Controls;component/Views/Flow/VisionFlowView.xaml")]
    [InlineData("pack://application:,,,/Vision.Controls;component/Views/Editor/VisionToolEditorView.xaml")]
    [InlineData("pack://application:,,,/Vision.Controls;component/Themes/LegacyHalconResources.xaml")]
    public void PackUri_ResolvesResources_FromForeignEntryAssembly(string uri)
        => host.Invoke(() => Assert.NotNull(Application.GetResourceStream(new Uri(uri, UriKind.Absolute))));
}
