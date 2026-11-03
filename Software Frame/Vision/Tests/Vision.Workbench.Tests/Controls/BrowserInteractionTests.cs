// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：BrowserInteractionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Prism.Ioc、Prism.Regions、System.Windows、System.Windows.Automation.Peers、System.Windows.Automation.Provider、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using Prism.Ioc;
using Prism.Regions;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Vision.Controls.View;

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
    public async Task StandaloneControlsComponent_BindsOriginalSession_RealButtonsRunToolAndAddRoi()
    {
        TestBrowserViewModel browser = null!;
        VisionToolEditorView page = null!;
        Window window = null!;
        Task execution = Task.CompletedTask;
        var count = 0;
        try
        {
            host.Invoke(() =>
            {
                browser = new TestBrowserViewModel();
                page = new VisionToolEditorView { DataContext = browser };
                window = new Window { Content = page, Width = 1300, Height = 800, Left = -10000, ShowInTaskbar = false };
                window.Show(); window.UpdateLayout();
                page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Same(browser, page.DataContext);
                Assert.Empty(Descendants(page).OfType<VisionToolEditorView>());
                Assert.Same(browser, page.Session);
                Assert.DoesNotContain(Descendants(page).OfType<ListBox>(), list => ReferenceEquals(list.ItemsSource, browser.ToolEntries));
                browser.SelectedEntry = browser.ToolEntries.Single(entry => entry.Name == "OpenCV/Threshold");
                page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                count = browser.RoiEditor.Count;
                browser.LoadImage();
                Invoke(page, "执行");
                execution = browser.CurrentCard!.RunCommand.ExecutionTask!;
                Assert.NotNull(execution);
            });
            await execution; // 真正等待原异步命令完成；DataBind/Idle不能替代后台执行完成。
            host.Invoke(() =>
            {
                Assert.Equal("执行成功", browser.CurrentCard!.StatusText);
                var viewport = Assert.Single(Descendants(page).OfType<VisionImageViewport>());
                Assert.Same(browser.InputImage, viewport.Image);
                Assert.Same(browser.CurrentCard.LastResult, viewport.Result);
                Assert.NotNull(Vision.Controls.Presentation.VisionDisplay.OutputImage(viewport.Result));
                Assert.Contains("synthetic-rect", browser.Model.ImageInfoText);
                var tabs = Descendants(page).OfType<TabControl>().Single(t => t.Items.OfType<TabItem>().Any(i => Equals(i.Header, "ROI")));
                tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(i => Equals(i.Header, "ROI"));
                page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var roiView = Assert.Single(Descendants(page).OfType<RoiEditorView>());
                var roiType = Assert.Single(Descendants(roiView).OfType<ComboBox>());
                roiType.SelectedItem = "圆";
                page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Invoke(page, "添加ROI"); Assert.Equal(count + 1, browser.RoiEditor.Count);
                Invoke(page, "删除"); Assert.Equal(count, browser.RoiEditor.Count);
            });
        }
        finally { host.Invoke(() => { window?.Close(); browser?.Dispose(); }); }
    }

    [Fact]
    public void ControlsPage_BorrowsDataContext_WhileExplicitBrowserOverridesDefaultBinding()
    {
        host.Invoke(() =>
        {
            using var firstWorkspace = new Vision.Controls.Services.VisionBrowserWorkspace(new([], [],
                _ => throw new InvalidOperationException(), TestBrowserViewModel.CreateInput));
            using var secondWorkspace = new Vision.Controls.Services.VisionBrowserWorkspace(new([], [],
                _ => throw new InvalidOperationException(), TestBrowserViewModel.CreateInput));
            using var first = new Vision.Controls.ViewModels.VisionBrowserViewModel(firstWorkspace);
            using var second = new Vision.Controls.ViewModels.VisionBrowserViewModel(secondWorkspace);
            var page = new VisionToolEditorView { DataContext = first };
            page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Same(first, page.Session);
            page.DataContext = second;
            page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Same(second, page.Session);
            page.Session = first;
            Assert.Same(second, page.DataContext);
            page.DataContext = first;
            page.DataContext = second;
            page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Same(first, page.Session);
            Assert.False(firstWorkspace.Model.IsClosed);
            Assert.False(secondWorkspace.Model.IsClosed);
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
                _ => throw new InvalidOperationException(), TestBrowserViewModel.CreateInput));
            using var vm = new Vision.Controls.ViewModels.VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = vm };
            page.Measure(new Size(1400, 800));
            page.Arrange(new Rect(0, 0, 1400, 800));
            page.UpdateLayout();
            Button ButtonFor(string caption) => Descendants(page).OfType<Button>().Single(b => AutomationProperties.GetName(b) == caption);
            Assert.Null(page.FindName("CompareBlobMenu"));
            Assert.DoesNotContain(Descendants(page).OfType<Button>(), b => AutomationProperties.GetName(b) is "校验" or "合成图");
            Assert.Null(vm.CurrentCard);
            Assert.False(ButtonFor("执行").IsEnabled);
            vm.LoadImage();
            Assert.NotNull(vm.InputImage);
            Assert.False(ButtonFor("执行").IsEnabled); // 无工具的会话不能因导图而启用执行。
            Assert.True(ButtonFor("加载图像").IsEnabled);
            vm.Dispose();
            page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.False(ButtonFor("加载图像").IsEnabled);
            Assert.False(vm.LoadImageCommand.CanExecute(null));
        });
    }

    /// <summary>
    /// 测试相关入口 Invoke；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Single、OfType、Descendants、Equals、NotNull；具体条件和先后次序见方法体。
    /// </summary>
    private static void Invoke(FrameworkElement root, string caption)
    {
        var button = Descendants(root).OfType<Button>().Single(b => Equals(b.Content, caption) || AutomationProperties.GetName(b) == caption);
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
