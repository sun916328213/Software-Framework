using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class BrowserCompositionTests(WorkbenchAppFixture host)
{
    [Fact]
    public void ToolbarImageAndDetails_BorrowOneViewModel_WithoutCatalogOrToolOwnership()
    {
        host.Invoke(() =>
        {
            var tool = new CountingTool(); var creates = 0;
            using var workspace = new VisionBrowserWorkspace(new(
                [new("Tool", "Test", () => { creates++; return (tool, new object(), null); })], [],
                _ => throw new InvalidOperationException(), () => null));
            using var browser = new VisionBrowserViewModel(workspace);
            var page = new VisionToolEditorView { Session = browser };
            var window = Show(page);
            try
            {
                Assert.Same(browser, Assert.IsType<System.Windows.Controls.Grid>(page.FindName("TopMenuBar")).DataContext);
                Assert.Null(page.FindName("ToolDirectoryPanel"));
                Assert.Null(page.FindName("ToolColumn"));
                Assert.DoesNotContain(Descendants(page).OfType<System.Windows.Controls.ListBox>(), list => ReferenceEquals(list.ItemsSource, browser.ToolEntries));
                Assert.Same(browser, page.ImagePanel.DataContext);
                Assert.Same(browser, page.DetailsPanel.DataContext);
                Assert.Same(workspace.RoiEditor, page.ImagePanel.ImageViewport.Editor);
                for (var i = 0; i < 2; i++)
                {
                    window.Content = null; Drain();
                    Assert.Equal(0, tool.DisposeCount); Assert.False(workspace.Model.IsClosed);
                    window.Content = page; Drain();
                }
                Assert.Equal(1, creates); Assert.Equal(0, tool.DisposeCount);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void ReplacingBorrowedBrowser_DetachesTheOldModel_WithoutOwningEitherWorkspace()
    {
        host.Invoke(() =>
        {
            var firstTool = new CountingTool(); var secondTool = new CountingTool();
            using var first = Workspace(firstTool); using var second = Workspace(secondTool);
            using var a = new VisionBrowserViewModel(first); using var b = new VisionBrowserViewModel(second);
            var page = new VisionToolEditorView { Session = a }; var window = Show(page);
            try
            {
                page.Session = b; Drain();
                Assert.Same(b, page.DetailsPanel.DataContext); Assert.Same(b, page.ImagePanel.DataContext);
                page.ImagePanel.ImageViewport.Mode = "平移";
                first.Model.CurrentParameters = new object(); Drain();
                Assert.Equal("平移", page.ImagePanel.ImageViewport.Mode);
                second.Model.CurrentParameters = new object(); Drain();
                Assert.Equal("选择", page.ImagePanel.ImageViewport.Mode);
                Assert.Equal(0, firstTool.DisposeCount); Assert.Equal(0, secondTool.DisposeCount);
                Assert.False(first.Model.IsClosed); Assert.False(second.Model.IsClosed);
            }
            finally { window.Close(); Drain(); }
        });
    }

    private static VisionBrowserWorkspace Workspace(CountingTool tool) => new(new(
        [new("Tool", "Test", () => (tool, new object(), null))], [],
        _ => throw new InvalidOperationException(), () => null));
    private sealed class CountingTool : IExecutableTool
    {
        public int DisposeCount { get; private set; }
        public string Name => "Tool";
        public long LastElapsedMs => 0;
        public ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) => new() { Success = true };
        public void EnsureLoaded() { }
        public void Dispose() => DisposeCount++;
    }
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
