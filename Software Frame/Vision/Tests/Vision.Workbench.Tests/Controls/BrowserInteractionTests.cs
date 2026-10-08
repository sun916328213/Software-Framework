// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：BrowserInteractionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Prism.Ioc、Prism.Regions、System.Windows、System.Windows.Automation.Peers、System.Windows.Automation.Provider、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using Prism.Ioc;
using Prism.Regions;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Vision.Controls.View;
using Vision.Workbench;

namespace Vision.Workbench.Tests.Controls;

/// <summary>
/// BrowserInteractionTests 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class BrowserInteractionTests(WorkbenchAppFixture host)
{
    /// <summary>
    /// 测试相关入口 Navigation_BindsControlsPage_RealButtonsRunToolAndAddRoi；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、IsType、Single、Resolve、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Navigation_BindsControlsPage_RealButtonsRunToolAndAddRoi()
    {
        host.Invoke(() =>
        {
            var page = Assert.IsType<ToolBrowserView>(Assert.Single(ContainerLocator.Container.Resolve<IRegionManager>().Regions["VisionRegion"].ActiveViews));
            var browser = Assert.IsType<ToolBrowserViewModel>(page.Browser);
            Assert.Same(browser, page.DataContext);
            var controlsPage = Assert.Single(Descendants(page).OfType<VisionBrowserView>());
            Assert.Same(browser, controlsPage.Browser);
            var original = browser.SelectedEntry;
            var count = browser.RoiEditor.Count;
            try
            {
                var catalog = Descendants(page).OfType<ListBox>().Single(list => ReferenceEquals(list.ItemsSource, browser.ToolEntries));
                catalog.SelectedItem = browser.ToolEntries.Single(entry => entry.Name == "OpenCV/Threshold");
                page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Invoke(page, "合成图");
                Invoke(page, "执行");
                Assert.Equal("执行成功", browser.CurrentCard!.StatusText);
                Assert.Contains("合成图", browser.Model.ImageInfoText);
                Invoke(page, "添加圆");
                Assert.Equal(count + 1, browser.RoiEditor.Count);
                Invoke(page, "删除");
                Assert.Equal(count, browser.RoiEditor.Count);
            }
            finally { browser.SelectedEntry = original; }
        });
    }

    /// <summary>
    /// 测试相关入口 BrowserButtons_ReflectMissingInputAndClosedViewModel；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Measure、Arrange、UpdateLayout、Single；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void BrowserButtons_ReflectMissingInputAndClosedViewModel()
    {
        host.Invoke(() =>
        {
            using var workspace = new Vision.Controls.Services.VisionBrowserWorkspace(new([], [],
                _ => throw new InvalidOperationException(), () => null, _ => "Compared"));
            using var vm = new Vision.Controls.ViewModels.VisionBrowserViewModel(workspace);
            var page = new VisionBrowserView { Browser = vm };
            page.Measure(new Size(1400, 800));
            page.Arrange(new Rect(0, 0, 1400, 800));
            page.UpdateLayout();
            Button ButtonFor(string caption) => Descendants(page).OfType<Button>().Single(b => Equals(b.Content, caption));
            Assert.False(ButtonFor("后端对比").IsEnabled);
            Assert.False(ButtonFor("Benchmark").IsEnabled);
            Invoke(page, "合成图");
            Assert.True(ButtonFor("后端对比").IsEnabled);
            Invoke(page, "后端对比");
            Assert.Equal("Compared", vm.CompareText);
            vm.Dispose();
            page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.False(ButtonFor("合成图").IsEnabled);
            Assert.False(ButtonFor("加载图像").IsEnabled);
            Assert.False(ButtonFor("后端对比").IsEnabled);
        });
    }

    /// <summary>
    /// 测试相关入口 Invoke；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Single、OfType、Descendants、Equals、NotNull；具体条件和先后次序见方法体。
    /// </summary>
    private static void Invoke(FrameworkElement root, string caption)
    {
        var button = Descendants(root).OfType<Button>().Single(b => Equals(b.Content, caption));
        Assert.NotNull(button.Command);
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)!).Invoke();
        root.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    /// <summary>
    /// 测试相关入口 Descendants；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetChildrenCount、GetChild、Descendants；具体条件和先后次序见方法体。
    /// </summary>
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var item in Descendants(child)) yield return item;
        }
    }
}
