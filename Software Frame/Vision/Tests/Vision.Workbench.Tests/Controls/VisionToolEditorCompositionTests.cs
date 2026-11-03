using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Vision.Controls;
using Vision.Controls.Models;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class VisionToolEditorCompositionTests(WorkbenchAppFixture host)
{
    [Fact]
    public void StandaloneEditor_NeedsNoCatalogOrProject_AndBorrowsTheOriginalInputParametersAndResult()
    {
        host.Invoke(() =>
        {
            var tool = new Probe(); var parameters = new Vision.Tools.OpenCv.Inspection.BlobParameters();
            using var card = new ToolCardViewModel(tool);
            var image = Image(); card.SetImage(image); card.Run(); var result = card.LastResult;
            var state = new VisionToolEditorModel { ToolIdentity = "节点A/Blob", CurrentCard = card, CurrentParameters = parameters, InputImage = image };
            using var session = new VisionToolEditorSession(state);
            var view = new VisionToolEditorView { Session = session };
            var window = Show(view);
            try
            {
                Assert.DoesNotContain(Descendants(view), element => element.GetType().Name is "ToolCatalogView" or "ProjectToolbarView");
                Assert.Null(typeof(VisionToolEditorModel).GetProperty("ToolEntries"));
                Assert.Null(typeof(VisionToolEditorSession).GetProperty("SaveProjectCommand"));
                Assert.Same(parameters, state.CurrentParameters); Assert.Same(image, view.ImagePanel.ImageViewport.Image);
                Assert.Same(result, card.LastResult); Assert.Equal(1, tool.RunCount);
                Assert.Equal("节点A/Blob", ((TextBlock)view.DetailsPanel.FindName("ToolIdentity")).Text);
                Assert.Same(session, view.ImagePanel.DataContext); Assert.Same(session, view.DetailsPanel.DataContext);
                Assert.Same(session.ParameterEditor, ((VisionPropertyPanel)view.DetailsPanel.FindName("ParametersPanel")).TargetObject);
                Assert.Single(Descendants(view).OfType<Button>(), button => AutomationProperties.GetName(button) == "执行");
                Assert.Null(view.DetailsPanel.FindName("DiagnosticsTab"));
                for (var i = 0; i < 2; i++)
                {
                    window.Content = null; Drain(); Assert.Equal(0, tool.DisposeCount);
                    Assert.Same(result, card.LastResult);
                    window.Content = view; Drain(); Assert.Same(result, card.LastResult);
                }
                var adapter = Assert.IsType<BlobParametersEditorViewModel>(session.ParameterEditor);
                adapter.MinGray = 123;
                Assert.Equal(123, parameters.MinGray); Assert.Null(card.LastResult);
                Assert.Equal(0, tool.DisposeCount);
                session.Dispose();
                Assert.False(session.IsSessionActive); Assert.False(state.IsClosed);
                Assert.Same(parameters, state.CurrentParameters); Assert.Same(card, state.CurrentCard);
                Assert.Equal(0, tool.DisposeCount);
                card.Run(); Assert.True(card.LastResult!.Success); // 宿主仍可运行自己的卡片。
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void SwitchingDebugSessions_DetachesOldModel_AndDoesNotReleaseEitherNodesTool()
    {
        host.Invoke(() =>
        {
            var firstTool = new Probe(); var secondTool = new Probe();
            using var firstCard = new ToolCardViewModel(firstTool); using var secondCard = new ToolCardViewModel(secondTool);
            using var a = new VisionToolEditorSession(new() { CurrentCard = firstCard, CurrentParameters = new Vision.Tools.OpenCv.Measurement.FindLineParameters(), InputImage = Image() });
            using var b = new VisionToolEditorSession(new() { CurrentCard = secondCard, CurrentParameters = new Vision.Tools.Halcon.Measurement.FindLineParameters(), InputImage = Image() });
            var view = new VisionToolEditorView { Session = a }; var window = Show(view);
            try
            {
                view.Session = b; Drain();
                Assert.Same(b.LineSearch, view.ImagePanel.ImageViewport.LineSearch);
                view.ImagePanel.ImageViewport.Mode = "平移";
                a.Model.CurrentParameters = new object(); Drain();
                Assert.Equal("平移", view.ImagePanel.ImageViewport.Mode);
                b.Model.CurrentParameters = new object(); Drain();
                Assert.Equal("选择", view.ImagePanel.ImageViewport.Mode);
                Assert.Same(b, view.DetailsPanel.DataContext);
                Assert.Equal(0, firstTool.DisposeCount); Assert.Equal(0, secondTool.DisposeCount);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void BrowserContainer_ComposesTheSameEditor_WithoutCatalogOrDuplicateToolbar()
    {
        host.Invoke(() =>
        {
            var tool = new Probe(); var creates = 0;
            using var workspace = new VisionBrowserWorkspace(new([new("Test/Blob", "Other", () => { creates++; return (tool, new Vision.Tools.OpenCv.Inspection.BlobParameters(), null); })], [],
                _ => throw new InvalidOperationException(), () => Image()));
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser }; var window = Show(page);
            try
            {
                Assert.Empty(Descendants(page).OfType<VisionToolEditorView>());
                Assert.Same(browser, page.Session);
                Assert.Same(browser, page.ImagePanel.DataContext); Assert.Same(browser, page.DetailsPanel.DataContext);
                Assert.DoesNotContain(Descendants(page), element => element.GetType().Name is "ToolCatalogView" or "ProjectToolbarView");
                Assert.Same(browser, Assert.IsType<Grid>(page.FindName("TopMenuBar")).DataContext);
                var buttons = Descendants(page).OfType<Button>().ToArray();
                Assert.Single(buttons, button => AutomationProperties.GetName(button) == "执行");
                Assert.Contains(buttons, button => ReferenceEquals(button.Command, browser.SaveProjectCommand));
                Assert.Equal(1, creates); Assert.Equal(0, tool.DisposeCount);
                Assert.Null(page.DetailsPanel.FindName("DiagnosticsTab"));
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void HostProvidedParameterEditor_IsBorrowedAndNotDisposedByTheDebugSession()
    {
        host.Invoke(() =>
        {
            var parameters = new Vision.Tools.OpenCv.Inspection.BlobParameters();
            using var editor = BlobParametersEditorViewModel.TryCreate(parameters, () => { })!;
            var state = new VisionToolEditorModel { CurrentParameters = parameters, BlobEditor = editor };
            var session = new VisionToolEditorSession(state);
            Assert.Same(editor, session.ParameterEditor);
            session.Dispose();
            Assert.Same(editor, state.BlobEditor);
            editor.MinGray = 42;
            Assert.Equal(42, parameters.MinGray);
        });
    }
    [Fact]
    public void BrowserEditor_OpensSavesSavesAsAndImportsImage_UsingOriginalProjectCommands()
    {
        host.Invoke(() =>
        {
            var first = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"vision-editor-{Guid.NewGuid():N}.visionproj");
            var second = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"vision-editor-copy-{Guid.NewGuid():N}.visionproj");
            var paths = new Queue<string>([first, second, first]); var picks = new List<bool>(); var creates = 0;
            using var workspace = new VisionBrowserWorkspace(new(
                [new("Test/Blob", "Test", () => { creates++; return (new Probe(), new Vision.Tools.OpenCv.Inspection.BlobParameters(), null); })], [],
                _ => throw new InvalidOperationException(), Image,
                PickProjectPath: save => { picks.Add(save); return paths.Dequeue(); }));
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var view = page; var window = Show(page);
            try
            {
                Assert.DoesNotContain(Descendants(view).OfType<ListBox>(), list => AutomationProperties.GetName(list) == "视觉工具目录");
                var toolbar = Assert.IsType<Grid>(view.FindName("TopMenuBar"));
                var buttons = Descendants(toolbar).OfType<Button>().ToArray();
                Assert.Equal(new[] { "打开工程", "保存工程", "另存为", "加载图像", "执行" }, buttons.Select(AutomationProperties.GetName));
                Assert.Same(browser.ImportProjectCommand, buttons[0].Command);
                Assert.Same(browser.SaveProjectCommand, buttons[1].Command);
                Assert.Same(browser.SaveProjectAsCommand, buttons[2].Command);
                Assert.Single(Descendants(view).OfType<Button>(), button => AutomationProperties.GetName(button) == "执行");
                Assert.All(buttons, button => Assert.True(button.IsVisible));
                Click(buttons[3]); Assert.NotNull(browser.InputImage);
                Assert.Equal(1, creates); Assert.Empty(picks); Assert.Null(browser.CurrentCard!.LastResult);
                var parameters = Assert.IsType<Vision.Tools.OpenCv.Inspection.BlobParameters>(browser.CurrentParameters);
                parameters.MinGray = 74;
                Click(buttons[1]); Assert.True(System.IO.File.Exists(first)); Assert.Equal(first, browser.CurrentProjectPath);
                parameters.MinGray = 80; Click(buttons[1]); Assert.Equal(new[] { true }, picks);
                parameters.MinGray = 99; Click(buttons[2]); Assert.True(System.IO.File.Exists(second)); Assert.Equal(second, browser.CurrentProjectPath);
                parameters.MinGray = 21; Click(buttons[0]);
                Assert.Equal(new[] { true, true, false }, picks); Assert.Equal(first, browser.CurrentProjectPath);
                Assert.Equal(80, Assert.IsType<Vision.Tools.OpenCv.Inspection.BlobParameters>(browser.CurrentParameters).MinGray);
                Assert.NotNull(browser.InputImage); Assert.Null(browser.CurrentCard!.LastResult); Assert.Equal(2, creates);
                window.Content = null; Drain(); window.Content = page; Drain();
                Assert.Same(toolbar, view.FindName("TopMenuBar")); Assert.Equal(2, creates);
                browser.Dispose(); Drain(); Assert.All(buttons, button => Assert.False(button.IsEnabled));
                Assert.False(workspace.Model.IsClosed);
            }
            finally { window.Close(); Drain(); System.IO.File.Delete(first); System.IO.File.Delete(second); }
        });
    }
    [Theory]
    [InlineData(900)]
    [InlineData(1300)]
    public void ProjectToolbar_StaysVisible_AndSwitchingToABorrowedNodeDoesNotKeepOldCommands(int width)
    {
        host.Invoke(() =>
        {
            var tool = new Probe(); var creates = 0; var picks = 0;
            using var workspace = new VisionBrowserWorkspace(new(
                [new("Project", "Test", () => { creates++; return (tool, new object(), null); })], [],
                _ => throw new InvalidOperationException(), Image,
                PickProjectPath: _ => { picks++; return null; }));
            using var browser = new VisionBrowserViewModel(workspace);
            using var nodeCard = new ToolCardViewModel(new Probe());
            using var node = new VisionToolEditorSession(new() { CurrentCard = nodeCard, InputImage = Image(), ImageInfoText = "node status" });
            var accessory = new TextBlock { Text = "外观插槽" };
            var view = new VisionToolEditorView { Session = browser, ToolbarAccessory = accessory };
            var window = Show(view); window.Width = width; Drain();
            try
            {
                Assert.True(browser.HasProjectCommands); Assert.False(node.HasProjectCommands);
                var toolbar = Assert.IsType<Grid>(view.FindName("TopMenuBar"));
                var run = Assert.IsType<Button>(view.FindName("RunButton"));
                Assert.Same(run, Assert.Single(Descendants(toolbar).OfType<Button>(), b => AutomationProperties.GetName(b) == "执行"));
                Assert.Contains(accessory, Descendants(toolbar));
                var buttons = Descendants(toolbar).OfType<Button>().ToArray();
                Assert.Equal(view.ImagePanel.ImageViewport.TranslatePoint(new Point(), view).X, buttons[0].TranslatePoint(new Point(), view).X, 2);
                Assert.All(buttons, button =>
                {
                    var bounds = button.TransformToAncestor(view).TransformBounds(new Rect(button.RenderSize));
                    Assert.InRange(bounds.Left, 0, view.ActualWidth); Assert.True(bounds.Right <= view.ActualWidth + 1);
                });
                Click(buttons[1]); Assert.Equal(1, picks); Assert.Null(browser.CurrentProjectPath); // 取消不修改会话。
                Assert.Equal(5, buttons.Length);
                Assert.All(buttons, button => Assert.True(button.IsVisible));
                Assert.Same(toolbar, view.FindName("TopMenuBar"));
                Assert.Contains(accessory, Descendants(toolbar));
                Assert.Equal(new[] { "打开工程", "保存工程", "另存为", "加载图像", "执行" }, buttons.Select(AutomationProperties.GetName));
                Assert.Same(run, Assert.Single(Descendants(view).OfType<Button>(), button => AutomationProperties.GetName(button) == "执行"));
                view.Session = node; Drain();
                Assert.All(buttons, button => Assert.True(button.IsVisible));
                Assert.All(buttons.Take(4), button => { Assert.Null(button.Command); Assert.False(button.IsEnabled); });
                var status = (TextBlock)view.ImagePanel.FindName("ExecutionStatus");
                var nodeStatus = node.CurrentOperationText;
                Assert.Equal(nodeStatus, status.Text);
                browser.ImageInfoText = "old project status"; Drain();
                Assert.Equal(nodeStatus, status.Text); Assert.DoesNotContain("old project status", status.Text);
                Assert.Same(node, view.DetailsPanel.DataContext); Assert.Equal(0, tool.DisposeCount);
                view.Session = browser; Drain();
                Assert.All(buttons.Take(4), button => Assert.True(button.IsVisible)); Assert.Same(toolbar, view.FindName("TopMenuBar"));
                Assert.Same(browser.ImportProjectCommand, buttons[0].Command);
                Assert.Same(browser.SaveProjectCommand, buttons[1].Command);
                Assert.Same(run, view.FindName("RunButton"));
                Assert.Equal(1, creates); Assert.Equal(1, picks); Assert.Equal(0, tool.RunCount);
                Assert.Equal(0, tool.DisposeCount); Assert.False(workspace.Model.IsClosed);
            }
            finally { window.Close(); Drain(); }
        });
    }
    [Fact]
    public void BrowserToolbar_ShowsAllOperationsBeforeBinding_ThenUsesOriginalProjectCommands()
    {
        host.Invoke(() =>
        {
            var page = new VisionToolEditorView();
            var window = Show(page);
            try
            {
                var toolbar = Assert.IsType<Grid>(page.FindName("TopMenuBar"));
                var buttons = Descendants(toolbar).OfType<Button>().ToArray();
                Assert.Equal(new[] { "打开工程", "保存工程", "另存为", "加载图像", "执行" }, buttons.Select(AutomationProperties.GetName));
                Assert.All(buttons, button => Assert.True(button.IsVisible));
                Assert.All(buttons, button => Assert.False(button.IsEnabled));
                using var workspace = new VisionBrowserWorkspace(new(
                    [new("Test/Tool", "Test", () => (new Probe(), new object(), null))], [],
                    _ => throw new InvalidOperationException(), Image));
                using var browser = new VisionBrowserViewModel(workspace);
                page.Session = browser; Drain();
                Assert.All(buttons, button => Assert.True(button.IsVisible));
                Assert.Same(browser.ImportProjectCommand, buttons[0].Command);
                Assert.Same(browser.SaveProjectCommand, buttons[1].Command);
                Assert.Same(browser.SaveProjectAsCommand, buttons[2].Command);
                Assert.Same(browser.LoadImageCommand, buttons[3].Command);
                Assert.Same(browser.CurrentCard!.RunCommand, buttons[4].Command);
                Assert.True(buttons[0].IsEnabled); Assert.True(buttons[3].IsEnabled);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void ProjectFileButton_StaysVisible_WithoutToolbarVisibilitySwitches()
    {
        host.Invoke(() =>
        {
            var editor = new VisionToolEditorView();
            Assert.Null(typeof(VisionToolEditorView).GetProperty("ShowProjectToolbar"));
            Assert.Null(typeof(VisionToolEditorView).GetField("ShowProjectToolbarProperty"));
            Assert.Null(typeof(VisionToolEditorView).GetProperty("ShowExecutionButton"));
            Assert.Null(typeof(VisionToolEditorView).GetField("ShowExecutionButtonProperty"));
            // 使用实际按钮样式，确认不依赖Root名称作用域或会话来显示。
            var button = new Button { Style = (Style)editor.Resources["ProjectFileButton"] };
            var window = Show(button);
            try
            {
                Assert.Equal(Visibility.Visible, button.Visibility);
                Assert.True(button.IsVisible);
                Assert.False(button.IsEnabled); // 没有文件命令不能误启用。
            }
            finally { window.Close(); Drain(); }
        });
    }

    private static void Click(Button button)
    {
        Assert.True(button.IsEnabled);
        ((System.Windows.Automation.Provider.IInvokeProvider)new System.Windows.Automation.Peers.ButtonAutomationPeer(button)
            .GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)!).Invoke();
        Drain();
    }
    private sealed class Probe : IExecutableTool
    {
        public int RunCount { get; private set; }
        public int DisposeCount { get; private set; }
        public string Name => "Probe";
        public long LastElapsedMs => 1;
        public Vision.Core.Results.ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image)
        {
            ObjectDisposedException.ThrowIf(DisposeCount != 0, this);
            RunCount++;
            return new Vision.Tools.OpenCv.Inspection.BlobResult { Items = [new() { Index = 1, Area = 20, Bounds = new(10, 10, 4, 5), Center = new(12, 12) }] };
        }
        public void EnsureLoaded() { }
        public void Dispose() => DisposeCount++;
    }
    private static VisionImage Image() => VisionImage.FromBuffer(100, 100, Vision.Core.Imaging.PixelFormat.Mono8, new byte[10000]);
    private static Window Show(FrameworkElement content)
    {
        var window = new Window { Content = content, Width = 1300, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
        window.Show(); Drain(); return window;
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
