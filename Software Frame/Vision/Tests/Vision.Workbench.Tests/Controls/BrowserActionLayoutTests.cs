using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class BrowserActionLayoutTests(WorkbenchAppFixture host)
{
    [Fact]
    public void CommonActions_AreExpanded_AndImagePresetsHaveTheirOwnRow_AtCompactWidth()
    {
        host.Invoke(() =>
        {
            using var workspace = Workspace();
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                var top = Assert.IsAssignableFrom<Panel>(page.FindName("TopMenuBar"));
                var buttons = Descendants(top).OfType<Button>().ToArray();
                Assert.Equal(new[] { "打开工程", "保存工程", "另存为", "加载图像", "执行" }, buttons.Select(AutomationProperties.GetName));
                Assert.Same(browser.ImportProjectCommand, buttons[0].Command);
                Assert.Same(browser.SaveProjectCommand, buttons[1].Command);
                Assert.Same(browser.SaveProjectAsCommand, buttons[2].Command);
                Assert.All(buttons, button => Assert.False(string.IsNullOrWhiteSpace(button.ToolTip?.ToString())));
                Assert.Null(page.FindName("FileMenu"));
                Assert.Null(page.FindName("MoreActionsMenu"));
                Assert.Same(browser.LoadImageCommand, buttons[3].Command);
                Assert.Same(browser.CurrentCard!.RunCommand, buttons[4].Command);
                var views = Assert.IsAssignableFrom<FrameworkElement>(page.ImageToolbar.FindName("ImageViewsBar"));
                Assert.Equal(0, Grid.GetRow(top)); Assert.Equal(0, Grid.GetRow(views));
                var viewport = Assert.IsType<VisionImageViewport>(page.ImagePanel.FindName("Viewport"));
                Assert.Same(page.ImagePanel, page.ImageToolbar.ImagePanel);
                Assert.Empty(Descendants(page.ImagePanel).OfType<VisionImageToolbarView>());
                Assert.True(views.TranslatePoint(new Point(), page).Y >= top.TranslatePoint(new Point(0, top.ActualHeight), page).Y);
                window.Width = 900; Drain();
                var headerBounds = views.TransformToAncestor(page).TransformBounds(new Rect(views.RenderSize));
                var imageBounds = viewport.TransformToAncestor(page).TransformBounds(new Rect(viewport.RenderSize));
                Assert.Equal(imageBounds.Left, headerBounds.Left, 2); Assert.Equal(imageBounds.Right, headerBounds.Right, 2);
                Assert.True(headerBounds.Bottom <= imageBounds.Top + 1);
                Assert.Equal(imageBounds.Left, buttons[0].TranslatePoint(new Point(), page).X, 2);
                Assert.All(buttons, button => Assert.Equal(buttons[0].ActualHeight, button.ActualHeight));
                Assert.Null(page.FindName("ToolColumn"));
                Assert.Null(page.FindName("ToolDirectoryPanel"));
                Assert.Equal(0, page.TranslatePoint(new Point(), page).X, 2);
                Assert.Equal(page.ActualWidth, page.ActualWidth, 2);
                window.Width = 1300; Drain();
                Assert.Equal(viewport.TranslatePoint(new Point(), page).X, buttons[0].TranslatePoint(new Point(), page).X, 2);
                Assert.All(buttons, button =>
                {
                    Assert.True(button.IsVisible);
                    var bounds = button.TransformToAncestor(page).TransformBounds(new Rect(button.RenderSize));
                    Assert.InRange(bounds.Left, 0, page.ActualWidth);
                    Assert.True(bounds.Right <= page.ActualWidth + 1);
                });
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Theory]
    [InlineData(900)]
    [InlineData(1300)]
    public void CanvasTools_StayInTheImageHeader_WithoutCoveringPixels_WhileExecutionStaysInTheTopToolbar(int width)
    {
        host.Invoke(() =>
        {
            using var workspace = Workspace();
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                var viewport = Assert.IsType<VisionImageViewport>(page.ImagePanel.FindName("Viewport"));
                var tools = Assert.IsAssignableFrom<FrameworkElement>(page.ImageToolbar.FindName("CanvasTools"));
                var header = Assert.IsAssignableFrom<FrameworkElement>(page.ImageToolbar.FindName("ImageViewsBar"));
                Assert.Contains(tools, Descendants(header));
                var overlayMenu = Assert.Single(Descendants(tools).OfType<Menu>());
                var overlayHeader = Assert.IsType<MenuItem>(Assert.Single(overlayMenu.Items.Cast<object>()));
                window.Width = width; Drain();
                var run = Assert.IsType<Button>(page.FindName("RunButton"));
                var operationBottom = run.TranslatePoint(new Point(0, run.ActualHeight), page).Y;
                var controlTop = header.TranslatePoint(new Point(), page).Y;
                Assert.InRange(controlTop - operationBottom, 0, 2);
                overlayMenu.ApplyTemplate(); overlayHeader.ApplyTemplate(); page.UpdateLayout(); Drain();
                var icon = Assert.IsType<MaterialDesignThemes.Wpf.PackIcon>(overlayHeader.Header);
                Assert.True(overlayMenu.ActualHeight >= icon.ActualHeight + overlayHeader.Padding.Top + overlayHeader.Padding.Bottom);
                var iconBounds = icon.TransformToAncestor(overlayMenu).TransformBounds(new Rect(icon.RenderSize));
                Assert.True(new Rect(overlayMenu.RenderSize).Contains(iconBounds), $"菜单图标被裁切：{iconBounds} / {overlayMenu.RenderSize}");
                var menuBounds = overlayMenu.TransformToAncestor(header).TransformBounds(new Rect(overlayMenu.RenderSize));
                Assert.True(new Rect(header.RenderSize).Contains(menuBounds), $"菜单入口超出图像栏：{menuBounds} / {header.RenderSize}");
                overlayHeader.IsSubmenuOpen = true; Drain();
                try
                {
                    var options = overlayHeader.Items.OfType<MenuItem>().ToArray();
                    Assert.Equal(5, options.Length);
                    Assert.All(options, option => { Assert.True(option.IsVisible); Assert.True(option.ActualWidth > 0); Assert.True(option.ActualHeight > 0); });
                }
                finally { overlayHeader.IsSubmenuOpen = false; Drain(); }
                Assert.True(tools.TranslatePoint(new Point(0, tools.ActualHeight), page).Y <= viewport.TranslatePoint(new Point(), page).Y + 1);
                var actions = Assert.IsType<WrapPanel>(page.DetailsPanel.FindName("ToolActions"));
                var parameters = Assert.IsType<TabItem>(page.DetailsPanel.FindName("ParametersTab"));
                Assert.Contains(actions, Descendants(Assert.IsType<DockPanel>(parameters.Content)));
                var actionNames = Descendants(actions).OfType<Button>().Select(AutomationProperties.GetName).ToArray();
                Assert.DoesNotContain("执行", actionNames); Assert.Contains("加载模型", actionNames);
                var top = Assert.IsAssignableFrom<Panel>(page.FindName("TopMenuBar"));
                Assert.Contains(Descendants(top).OfType<Button>(), button => AutomationProperties.GetName(button) == "执行");
                Assert.DoesNotContain("加载图像", actionNames); Assert.DoesNotContain("适应窗口", actionNames);
                browser.LoadImage(); Drain();
                var selector = Assert.IsType<ListBox>(page.ImageToolbar.FindName("ImageViewSelector"));
                selector.SelectedValue = ImageViewKind.Roi; Drain();
                var roiToggle = Assert.IsType<MenuItem>(page.ImageToolbar.FindName("RoiOverlayMenu"));
                Assert.True(roiToggle.IsChecked);
                roiToggle.SetCurrentValue(MenuItem.IsCheckedProperty, false); Drain();
                Assert.False(browser.ShowRois); Assert.False(viewport.ShowRois);
                Assert.Empty(browser.RoiEditor.Rois);
                var resultToggle = Assert.IsType<MenuItem>(page.ImageToolbar.FindName("ResultOverlayMenu"));
                resultToggle.SetCurrentValue(MenuItem.IsCheckedProperty, true); Drain();
                Assert.True(browser.ShowResults); Assert.True(viewport.ShowResults);
                var intermediate = Assert.IsType<MenuItem>(page.ImageToolbar.FindName("IntermediateImageMenu"));
                Assert.False(intermediate.IsChecked);
                intermediate.SetCurrentValue(MenuItem.IsCheckedProperty, true); Drain();
                Assert.True(viewport.ShowRoiImage);
                selector.SelectedValue = ImageViewKind.Result; Drain();
                Assert.False(viewport.ShowRoiImage); Assert.True(viewport.ShowOutput);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void RoiDrawing_StartsOnlyFromTheRoiTab_AndDoesNotCreateASeedRoi()
    {
        host.Invoke(() =>
        {
            using var workspace = Workspace();
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                ((TabItem)page.DetailsPanel.FindName("RoiTab")).IsSelected = true; Drain();
                var draw = Assert.Single(Descendants(page).OfType<Button>(), button => AutomationProperties.GetName(button) == "绘制ROI");
                Assert.False(draw.IsEnabled);
                var roiView = Assert.Single(Descendants(page).OfType<RoiEditorView>());
                Assert.Contains(draw, Descendants(roiView));
                browser.LoadImage(); Drain(); Assert.True(draw.IsEnabled);
                browser.RoiEditor.SelectedRoiType = "椭圆";
                Assert.Empty(browser.RoiEditor.Rois);
                Invoke(draw);
                var viewport = Assert.IsType<VisionImageViewport>(page.ImagePanel.FindName("Viewport"));
                Assert.Equal("椭圆", viewport.Mode); Assert.Empty(browser.RoiEditor.Rois);
                Assert.True(viewport.ShowRois); Assert.False(viewport.ShowOutput); Assert.False(viewport.ShowRoiImage);
                var select = Assert.IsType<Button>(page.ImageToolbar.FindName("SelectCanvasButton"));
                Invoke(select); Assert.Equal("选择", viewport.Mode);
                ((TabItem)page.DetailsPanel.FindName("ResultsTab")).IsSelected = true; Drain();
                Invoke(select); Assert.Equal("选择", viewport.Mode); // ROI 预设的编辑不受右侧结果页限制。
                browser.SelectedImageView = ImageViewKind.Result; Drain();
                Invoke(select); Assert.Equal("结果选择", viewport.Mode);
                browser.Dispose(); Drain();
                Assert.False(draw.IsEnabled);
                Assert.False(workspace.Model.IsClosed);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void ExpandedProjectButtons_UseRealRoundTrip_AndAreDisabledAfterBorrowerCloses()
    {
        host.Invoke(() =>
        {
            var path = Path.Combine(Path.GetTempPath(), $"vision-actions-{Guid.NewGuid():N}.vproject");
            var picks = new List<bool>(); var loads = 0;
            var catalog = new VisionBrowserCatalog(
                [new("Regions", "Test", () => (new BrowserLayoutTestsFakeTool(), new Parameters(), null))], [],
                _ => throw new InvalidOperationException(),
                () => { loads++; return Image(); },
                PickProjectPath: save => { picks.Add(save); return path; });
            using var workspace = new VisionBrowserWorkspace(catalog);
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                var top = Assert.IsAssignableFrom<Panel>(page.FindName("TopMenuBar"));
                Button Button(string name) => Descendants(top).OfType<Button>().Single(b => AutomationProperties.GetName(b) == name);
                Invoke(Button("加载图像")); Assert.Equal(1, loads);
                Assert.IsType<Parameters>(browser.CurrentParameters).Threshold = 37;
                browser.RoiEditor.AddRectangle(20, 20, 10, 12);
                Invoke(Button("保存工程")); Assert.True(File.Exists(path)); Assert.Equal(new[] { true }, picks);
                Assert.IsType<Parameters>(browser.CurrentParameters).Threshold = 99;
                browser.RoiEditor.Clear();
                Invoke(Button("打开工程")); Assert.Equal(new[] { true, false }, picks);
                Assert.Equal(37, Assert.IsType<Parameters>(browser.CurrentParameters).Threshold);
                Assert.Single(browser.RoiEditor.Rois); Assert.Null(browser.CurrentCard!.LastResult);
                browser.Dispose(); Drain();
                Assert.All(Descendants(top).OfType<Button>(), button => Assert.False(button.IsEnabled));
                Assert.False(workspace.Model.IsClosed);
            }
            finally { window.Close(); Drain(); File.Delete(path); }
        });
    }

    [Fact]
    public void DrawingButtons_ConfirmOrCancelPolygon_AndKeepFullPrecisionInTheDto()
    {
        host.Invoke(() =>
        {
            using var workspace = Workspace();
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                browser.LoadImage();
                ((TabItem)page.DetailsPanel.FindName("RoiTab")).IsSelected = true; Drain();
                var roiView = Assert.Single(Descendants(page).OfType<RoiEditorView>());
                var draw = Assert.Single(Descendants(roiView).OfType<Button>(), b => AutomationProperties.GetName(b) == "绘制ROI");
                var viewport = Assert.IsType<VisionImageViewport>(page.ImagePanel.FindName("Viewport"));
                var drawingActions = Assert.IsType<DockPanel>(page.ImagePanel.FindName("DrawingActions"));
                Assert.False(drawingActions.IsVisible);
                browser.RoiEditor.SelectedRoiType = "多边形"; Invoke(draw);
                Assert.True(drawingActions.IsVisible);
                var points = (List<Point>)typeof(VisionImageViewport).GetField("_polygonPoints",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(viewport)!;
                points.AddRange([new(10, 10), new(40, 10), new(10, 40)]);
                Invoke(Assert.IsType<Button>(page.ImagePanel.FindName("CompleteDrawingButton")));
                Assert.Single(browser.RoiEditor.Rois); Assert.Equal("选择", viewport.Mode);
                Assert.False(drawingActions.IsVisible);
                var original = browser.RoiEditor.Selected;
                Invoke(draw); points.AddRange([new(15, 15), new(45, 15), new(15, 45)]);
                Invoke(Assert.Single(Descendants(drawingActions).OfType<Button>(), b => Equals(b.Content, "取消")));
                Assert.Same(original, Assert.Single(browser.RoiEditor.Rois)); Assert.Empty(points);
                var roi = browser.RoiEditor.AddRectangle(22.123456789, 30, 10, 10); Drain();
                var x = Assert.IsType<TextBox>(roiView.FindName("RoiX"));
                Assert.Equal("22.123", x.Text);
                x.Focus(); Drain(); draw.Focus(); Drain();
                Assert.Equal(22.123456789, roi.Pose.X, 10);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Theory]
    [InlineData(900)]
    [InlineData(1300)]
    public void ImageFooter_IsOneEllipsizedLine_WithFullTooltip_AndDrawingDoesNotResizeCanvas(int width)
    {
        host.Invoke(() =>
        {
            using var workspace = Workspace();
            using var browser = new VisionBrowserViewModel(workspace);
            browser.LoadImage();
            browser.ImageInfoText = "图像100×100 Mono8 " + new string('x', 300);
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page); window.Width = width; Drain();
            try
            {
                var status = Assert.IsType<TextBlock>(page.ImagePanel.FindName("ExecutionStatus"));
                Assert.Equal(TextWrapping.NoWrap, status.TextWrapping);
                Assert.Equal(TextTrimming.CharacterEllipsis, status.TextTrimming);
                Assert.InRange(status.ActualHeight, 1, 24);
                Assert.Contains(browser.ImageInfoText, status.Text);
                var tooltip = Assert.IsType<ToolTip>(status.ToolTip);
                tooltip.PlacementTarget = status;
                try
                {
                    tooltip.IsOpen = true; Drain();
                    Assert.Equal(status.Text, Assert.IsType<TextBlock>(tooltip.Content).Text);
                }
                finally { tooltip.IsOpen = false; }
                var viewport = page.ImagePanel.ImageViewport;
                var height = viewport.ActualHeight;
                ((TabItem)page.DetailsPanel.FindName("RoiTab")).IsSelected = true; Drain();
                browser.RoiEditor.SelectedRoiType = "多边形";
                var draw = Assert.Single(Descendants(page.DetailsPanel).OfType<Button>(), b => AutomationProperties.GetName(b) == "绘制ROI");
                Invoke(draw);
                Assert.False(status.IsVisible);
                Assert.InRange(Math.Abs(viewport.ActualHeight - height), 0, 1);
                var actions = Assert.IsType<DockPanel>(page.ImagePanel.FindName("DrawingActions"));
                Assert.True(actions.IsVisible);
                Invoke(Assert.Single(Descendants(actions).OfType<Button>(), b => Equals(b.Content, "取消")));
                Assert.True(status.IsVisible);
                Assert.InRange(Math.Abs(viewport.ActualHeight - height), 0, 1);
            }
            finally { window.Close(); Drain(); }
        });
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Footer_ShowsOnlyCurrentOperation_AndExecutionReplacesLoadAndMouseMessages(bool fail)
    {
        host.Invoke(() =>
        {
            var tool = new FooterStatusTool(fail);
            var imageNumber = 0;
            using var workspace = new VisionBrowserWorkspace(new([new("Footer", "Test", () => (tool, new Parameters(), null))], [],
                _ => throw new InvalidOperationException(), () => VisionImage.FromBuffer(100, 100, Vision.Core.Imaging.PixelFormat.Mono8, new byte[10000], source: $"input{Math.Min(++imageNumber, 2)}.png")));
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                browser.LoadImage(); Drain();
                var status = Assert.IsType<TextBlock>(page.ImagePanel.FindName("ExecutionStatus"));
                Assert.Contains("input1.png", status.Text);
                string? duringRun = null;
                tool.DuringRun = () => { Drain(); duringRun = status.Text; };
                browser.CurrentCard!.Run(); Drain();
                Assert.NotNull(duringRun); Assert.Contains("执行中", duringRun);
                Assert.DoesNotContain("已加载", duringRun); Assert.DoesNotContain("input1.png", duringRun);
                Assert.Contains(fail ? "执行异常" : "执行成功", status.Text);
                Assert.DoesNotContain("已加载", status.Text); Assert.DoesNotContain("input1.png", status.Text);
                Assert.DoesNotContain("加载图像开始", status.Text);
                var execution = status.Text;
                page.ImagePanel.ImageViewport.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = System.Windows.Input.Mouse.MouseMoveEvent });
                Drain(); Assert.Equal(execution, status.Text);
                browser.LoadImage(); Drain();
                Assert.Contains("input2.png", status.Text);
                Assert.DoesNotContain("执行成功", status.Text); Assert.DoesNotContain("执行异常", status.Text);
                browser.CurrentCard.Run(); Drain();
                browser.LoadImage(); Drain(); // 同一路径/相同加载文案，也应替换刚才的执行消息。
                Assert.Contains("input2.png", status.Text);
                Assert.DoesNotContain("执行成功", status.Text); Assert.DoesNotContain("执行异常", status.Text);
            }
            finally { window.Close(); Drain(); }
        });
    }
    private sealed class FooterStatusTool(bool fail) : IExecutableTool
    {
        public Action? DuringRun { get; set; }
        public string Name => "FooterProbe";
        public long LastElapsedMs => 7;
        public Vision.Core.Results.ValidationResult Validate() => new();
        public Vision.Core.Results.ToolResult Run(VisionImage? image)
        {
            DuringRun?.Invoke();
            if (fail) throw new InvalidOperationException("模拟执行失败");
            return new() { Success = true, ElapsedMs = 7 };
        }
        public void EnsureLoaded() { }
        public void Dispose() { }
    }
    private sealed class Parameters { public int Threshold { get; set; } }
    private sealed class BrowserLayoutTestsFakeTool : IExecutableTool
    {
        public string Name => "Regions";
        public long LastElapsedMs => 0;
        public Vision.Core.Results.ValidationResult Validate() => new();
        public Vision.Core.Results.ToolResult Run(VisionImage? image) => new();
        public void EnsureLoaded() { }
        public void Dispose() { }
    }
    private static VisionImage Image() => VisionImage.FromBuffer(100, 100, Vision.Core.Imaging.PixelFormat.Mono8, new byte[10000]);
    private static VisionBrowserWorkspace Workspace() => new(new(
        [new("Regions", "Test", () => (new BrowserLayoutTestsFakeTool(), new Parameters(), null))], [],
        _ => throw new InvalidOperationException(), Image));
    private static Window Show(FrameworkElement content)
    {
        var window = new Window { Content = content, Width = 1300, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
        window.Show(); Drain(); return window;
    }
    private static void Invoke(Button button)
    {
        Assert.True(button.IsEnabled);
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)!).Invoke(); Drain();
    }
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
