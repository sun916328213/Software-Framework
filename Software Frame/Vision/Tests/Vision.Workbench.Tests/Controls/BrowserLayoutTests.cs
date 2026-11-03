using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using MaterialDesignThemes.Wpf;
using System.Windows.Media;
using System.Windows.Threading;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.Core.Results;
using ValidationResult = Vision.Core.Results.ValidationResult;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class BrowserLayoutTests(WorkbenchAppFixture host)
{
    [Fact]
    public void ToolbarAndTabs_FollowCapabilities_NotBackendNames_AndFallBackWhenToolChanges()
    {
        host.Invoke(() =>
        {
            // 名称故意与能力不一致：不能按 Backend == AI 或名称包含 Template 决定界面。
            ToolEntry[] entries =
            [
                new("Plain", "AI", () => (new FakeTool(), new object(), null)),
                new("ProviderTool", "Other", () => (new FakeTool(), new object(), _ => { }), SupportsModelLoading: true),
                new("ImageTemplate", "Other", () => (new FakeTool(), new object(), null), SetTemplate: (_, _) => { }),
                new("ModelOnly", "Other", () => (new FakeTool(), new object(), null), SupportsModelLoading: true),
                new("ProviderOnly", "Other", () => (new FakeTool(), new object(), _ => { }))
            ];
            using var workspace = Workspace(entries);
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                var top = Assert.IsAssignableFrom<Panel>(page.FindName("TopMenuBar"));
                var projectButtons = Descendants(top).OfType<Button>().ToArray();
                Assert.Contains(projectButtons, b => AutomationProperties.GetName(b) == "打开工程" && ReferenceEquals(b.Command, browser.ImportProjectCommand));
                Assert.Contains(projectButtons, b => AutomationProperties.GetName(b) == "保存工程" && ReferenceEquals(b.Command, browser.SaveProjectCommand));
                Assert.Contains(projectButtons, b => AutomationProperties.GetName(b) == "加载图像" && ReferenceEquals(b.Command, browser.LoadImageCommand));
                Assert.Null(page.FindName("MainMenu")); Assert.Null(page.FindName("FileMenu"));
                Assert.Null(page.FindName("MoreActionsMenu"));
                Assert.DoesNotContain(projectButtons, b => AutomationProperties.GetName(b) is "使用演示图" or "Benchmark");
                Assert.Contains(projectButtons, b => AutomationProperties.GetName(b) == "另存为" && ReferenceEquals(b.Command, browser.SaveProjectAsCommand));
                Assert.Contains(projectButtons, b => AutomationProperties.GetName(b) == "执行" && ReferenceEquals(b.Command, browser.CurrentCard!.RunCommand));
                var actions = Assert.IsType<WrapPanel>(page.DetailsPanel.FindName("ToolActions"));
                var buttons = Descendants(actions).OfType<Button>().ToArray();
                Assert.DoesNotContain(buttons, b => AutomationProperties.GetName(b) is "加载图像" or "适应窗口" or "1:1");
                Assert.DoesNotContain(buttons, b => AutomationProperties.GetName(b) is "校验" or "合成图");
                Assert.Null(page.FindName("CompareBlobMenu"));
                Assert.DoesNotContain(buttons, b => AutomationProperties.GetName(b) == "执行");
                Assert.All(buttons, b => { Assert.NotEmpty(Descendants(b).OfType<PackIcon>()); Assert.NotNull(b.ToolTip); Assert.NotEmpty(AutomationProperties.GetName(b)); });
                Assert.Null(page.FindName("RoiTypeSelector"));
                Assert.DoesNotContain(Descendants(page).OfType<Button>(), b =>
                    AutomationProperties.GetName(b) is "选择结果" or "平移图像" or "编辑搜索域");
                Assert.DoesNotContain(Descendants(page).OfType<MenuItem>(), item =>
                    Equals(item.Header, "叠加设置") || Equals(item.Header, "ROI 叠加层") || Equals(item.Header, "结果叠加层"));
                var load = buttons.Single(b => AutomationProperties.GetName(b) == "加载模型");
                Assert.Equal(Visibility.Collapsed, load.Visibility);
                Assert.DoesNotContain(Descendants(page), element => element.GetType().Name == "ToolCardView");
                Assert.DoesNotContain(Descendants(page).OfType<Button>(), b => Equals(b.Content, "裁剪为输入"));
                var tabs = Assert.IsType<TabControl>(page.DetailsPanel.FindName("DetailsTabs"));
                var results = Assert.IsType<TabItem>(page.DetailsPanel.FindName("ResultsTab"));
                Assert.Contains(results, tabs.Items.Cast<object>());
                Assert.Contains(tabs.Items.OfType<TabItem>(), t => Equals(t.Header, "参数"));
                Assert.Contains(tabs.Items.OfType<TabItem>(), t => Equals(t.Header, "ROI"));
                var model = Assert.IsType<TabItem>(page.DetailsPanel.FindName("ModelTab"));
                var template = Assert.IsType<TabItem>(page.DetailsPanel.FindName("TemplateTab"));
                Assert.Equal(Visibility.Collapsed, model.Visibility);
                Assert.Equal(Visibility.Collapsed, template.Visibility);

                browser.SelectedEntry = entries[1];
                Drain();
                Assert.True(browser.SupportsProvider);
                Assert.Equal(Visibility.Visible, model.Visibility);
                Assert.Equal(Visibility.Visible, load.Visibility);
                model.IsSelected = true;
                Drain();
                Assert.Single(Descendants(page).OfType<ComboBox>().Where(c => ReferenceEquals(c.ItemsSource, browser.ProviderEntries)));
                browser.SelectedEntry = entries[0];
                Drain();
                Assert.False(browser.SupportsModelPanel);
                Assert.Equal(Visibility.Collapsed, model.Visibility);
                Assert.Equal("参数", Assert.IsType<TabItem>(tabs.SelectedItem).Header);

                browser.SelectedEntry = entries[2];
                Drain();
                Assert.Equal(Visibility.Visible, template.Visibility);
                Assert.Equal(Visibility.Collapsed, model.Visibility);
                template.IsSelected = true;
                Drain();
                var setTemplate = Descendants(page).OfType<Button>().Single(b => Equals(b.Content, "选中矩形设为模板"));
                Assert.False(setTemplate.IsEnabled);
                browser.LoadImage();
                Drain();
                Assert.False(setTemplate.IsEnabled); // 无默认 ROI：仅有输入图不能提交模板。
                browser.RoiEditor.AddRectangle(32, 32, 100, 60);
                Drain();
                Assert.True(setTemplate.IsEnabled);
                browser.SelectedEntry = entries[0];
                Drain();
                Assert.Equal(Visibility.Collapsed, template.Visibility);
                Assert.Equal("参数", Assert.IsType<TabItem>(tabs.SelectedItem).Header);

                browser.SelectedEntry = entries[3];
                Drain();
                Assert.False(browser.SupportsProvider);
                Assert.True(browser.SupportsModelPanel);
                Assert.Equal(Visibility.Visible, load.Visibility);
                browser.SelectedEntry = entries[4];
                Drain();
                Assert.True(browser.SupportsProvider);
                Assert.False(browser.SupportsModelLoading);
                Assert.Equal(Visibility.Collapsed, load.Visibility);
                browser.Dispose();
                Drain();
                Assert.False(actions.IsEnabled);
                Assert.False(workspace.Model.IsClosed); // 页面不拥有会话。
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void ImageViewRow_IsBelowTheMenu_AndPresetsDriveLayers()
    {
        host.Invoke(() =>
        {
            using var workspace = Workspace([new("Plain", "Test", () => (new FakeTool(), new object(), null))]);
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                var menuBar = Assert.IsAssignableFrom<FrameworkElement>(page.FindName("TopMenuBar"));
                var viewsBar = Assert.IsAssignableFrom<FrameworkElement>(page.ImageToolbar.FindName("ImageViewsBar"));
                Assert.Equal(0, Grid.GetRow(menuBar));
                Assert.Equal(0, Grid.GetRow(viewsBar));
                Assert.Same(page.ImagePanel, page.ImageToolbar.ImagePanel);
                Assert.Empty(Descendants(page.ImagePanel).OfType<VisionImageToolbarView>());
                var selector = Assert.IsType<ListBox>(page.ImageToolbar.FindName("ImageViewSelector"));
                Assert.Null(selector.ItemsSource);
                var presets = selector.Items.Cast<ListBoxItem>().ToArray();
                Assert.Equal(new[] { "输入图像", "ROI 图像", "结果图像", "训练图像" }, presets.Select(item => item.Content));
                Assert.Equal(new[] { ImageViewKind.Input, ImageViewKind.Roi, ImageViewKind.Result, ImageViewKind.Training }, presets.Select(item => Assert.IsType<ImageViewKind>(item.Tag)));
                Assert.Equal(browser.SelectedImageView, Assert.IsType<ListBoxItem>(selector.SelectedItem).Tag);
                var tools = Assert.IsType<StackPanel>(page.ImageToolbar.FindName("CanvasTools"));
                page.UpdateLayout(); Drain();
                var selectorWidth = selector.ActualWidth;
                var toolsLeft = tools.TranslatePoint(new Point(), page).X;
                foreach (var preset in presets.Concat(presets))
                {
                    selector.SelectedItem = preset; page.UpdateLayout(); Drain();
                    Assert.Equal(selectorWidth, selector.ActualWidth, 2);
                    Assert.Equal(toolsLeft, tools.TranslatePoint(new Point(), page).X, 2);
                }
                var viewport = Assert.Single(Descendants(page).OfType<VisionImageViewport>());
                selector.SelectedItem = presets[1];
                Drain();
                Assert.Equal(ImageViewKind.Roi, browser.SelectedImageView);
                Assert.True(viewport.ShowRois);
                Assert.False(viewport.ShowResults);
                Assert.False(viewport.ShowOutput);
                browser.SelectedImageView = ImageViewKind.Result;
                Drain();
                Assert.Same(presets[2], selector.SelectedItem);
                Assert.False(viewport.ShowRois);
                Assert.True(viewport.ShowResults);
                Assert.True(viewport.ShowOutput);
                browser.SelectedImageView = ImageViewKind.Input;
                Drain();
                Assert.False(viewport.ShowRois);
                Assert.False(viewport.ShowResults);
                Assert.False(viewport.ShowOutput);
                Assert.Null(page.FindName("OriginalImageMenu"));
                using var other = new VisionBrowserViewModel(workspace);
                Assert.Equal(ImageViewKind.Input, other.SelectedImageView);
                Assert.False(other.ShowRois);
                Assert.False(other.ShowResults);
            }
            finally { window.Close(); Drain(); }
        });
    }

    private static VisionBrowserWorkspace Workspace(ToolEntry[] tools) => new(new(tools, [],
        _ => throw new InvalidOperationException("不应加载 Provider。"), TestBrowserViewModel.CreateInput));
    private static Window Show(object content)
    {
        var window = new Window { Content = content, Width = 1300, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
        window.Show();
        Drain();
        return window;
    }
    private sealed class FakeTool : IExecutableTool
    {
        public string Name => "Fake";
        public long LastElapsedMs => 0;
        public ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) => new();
        public void EnsureLoaded() { }
        public void Dispose() { }
    }
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
