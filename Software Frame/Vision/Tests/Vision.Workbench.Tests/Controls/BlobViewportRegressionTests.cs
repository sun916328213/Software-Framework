using System.Collections.Specialized;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class BlobViewportRegressionTests(WorkbenchAppFixture host)
{
    [Fact]
    public void FrozenOutlineAndBitmap_AreReusedAcrossZoomFitSelectionAndLayerChanges()
    {
        host.Invoke(() =>
        {
            using var card = new ToolCardViewModel(new ResultSelectionTests.ResultTool(ResultSelectionTests.CreateResult));
            card.Run();
            var viewport = new VisionImageViewport
            {
                Width = 100, Height = 100, ShowOutput = false, Result = card.LastResult, Results = card.Results,
                Image = VisionImage.FromBuffer(100, 100, Vision.Core.Imaging.PixelFormat.Mono8, new byte[10000])
            };
            viewport.Measure(new Size(100, 100)); viewport.Arrange(new Rect(0, 0, 100, 100)); viewport.Fit();
            var cache = Outline(viewport);
            var bitmap = Field("_bitmap").GetValue(viewport);
            var geometry = Assert.IsType<StreamGeometry>(cache.GetType().GetProperty("Geometry")!.GetValue(cache));
            Assert.True(geometry.IsFrozen);
            for (var i = 0; i < 5; i++)
            {
                viewport.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, 120) { RoutedEvent = Mouse.MouseWheelEvent });
                viewport.ActualPixels(); viewport.Fit();
                card.Results.SelectId(i % 2 == 0 ? 9 : 4);
                viewport.ShowResults = i % 2 == 0;
                new RenderTargetBitmap(100, 100, 96, 96, PixelFormats.Pbgra32).Render(viewport);
                Assert.Same(cache, Outline(viewport));
                Assert.Same(bitmap, Field("_bitmap").GetValue(viewport));
            }
            viewport.Result = ResultSelectionTests.CreateResult();
            Assert.NotSame(cache, Outline(viewport));
            Assert.Same(bitmap, Field("_bitmap").GetValue(viewport)); // 同一底图不重复复制像素。
        });
    }
    [Fact]
    public void InvalidatingOldBlobResult_DuringRoiDrag_DoesNotCancelOrRollBackTheGesture()
    {
        host.Invoke(() =>
        {
            using var workspace = new VisionBrowserWorkspace(new(
                [new("Scoped", "Unknown", () => (new ScopedResultTool(), new object(), null))], [],
                _ => throw new InvalidOperationException(), TestBrowserViewModel.CreateInput));
            using var browser = new VisionBrowserViewModel(workspace);
            browser.LoadImage(); browser.RoiEditor.Clear();
            var roi = browser.RoiEditor.AddRectangle(20, 20, 10, 10);
            var page = new VisionToolEditorView { Session = browser };
            var window = new Window { Content = page, Width = 1250, Height = 800, Left = -10000, Top = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                browser.CurrentCard!.Run();
                Assert.NotNull(browser.CurrentCard.LastResult);
                var viewport = Assert.IsType<VisionImageViewport>(page.ImagePanel.FindName("Viewport"));
                browser.SelectedImageView = Vision.Controls.Models.ImageViewKind.Roi;
                viewport.Fit();
                Assert.True(browser.RoiEditor.TryBeginManipulation(new(20, 20), 0.1));
                // 使用真实手势持有者，覆盖 Result DP 失效通知与手势回滚之间的回归。
                Field("_gestureEditor").SetValue(viewport, browser.RoiEditor);
                Assert.True(browser.RoiEditor.UpdateManipulation(new(23, 24)));
                Assert.Null(browser.CurrentCard.LastResult);
                Assert.True(browser.RoiEditor.IsManipulating);
                Assert.Equal(23, roi.Pose.X); Assert.Equal(24, roi.Pose.Y);
                browser.RoiEditor.EndManipulation(cancel: true);
                Assert.Equal(20, roi.Pose.X); Assert.Equal(20, roi.Pose.Y);
            }
            finally { window.Close(); }
        });
    }
    [Fact]
    public void ManyBlobRows_ArePublishedAsBatchReset_NotOneNotificationPerTarget()
    {
        var result = new Vision.Tools.OpenCv.Inspection.BlobResult
        {
            Items = Enumerable.Range(1, 10000).Select(i => new Vision.Tools.OpenCv.Inspection.BlobItem
                { Index = i, Area = 1, Center = new(i, 1), Bounds = new(i, 1, 1, 1) }).ToList()
        };
        using var card = new ToolCardViewModel(new ResultSelectionTests.ResultTool(() => result));
        var actions = new List<NotifyCollectionChangedAction>();
        ((INotifyCollectionChanged)card.Results.Items).CollectionChanged += (_, e) => actions.Add(e.Action);
        card.Run();
        Assert.Equal(10000, card.Results.Items.Count);
        Assert.InRange(actions.Count, 1, 2); // 开始清空 + 单次发布；禁止 10000 次 Add 通知。
        Assert.All(actions, action => Assert.Equal(NotifyCollectionChangedAction.Reset, action));
    }
    private static object Outline(VisionImageViewport viewport)
    {
        var presentation = Field("_resultDrawing").GetValue(viewport)!;
        return presentation.GetType().GetField("_outline", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(presentation)!;
    }
    private static FieldInfo Field(string name) => typeof(VisionImageViewport).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private sealed class ScopedResultTool : IExecutableTool, ISearchRegionTool
    {
        public string Name => "Scoped result";
        public long LastElapsedMs => 0;
        public ISearchRegionTool SearchRegionTool => this;
        public SearchRegion? SearchRegion { get; set; }
        public ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) => ResultSelectionTests.CreateResult();
        public void EnsureLoaded() { }
        public void Dispose() { }
    }
}
