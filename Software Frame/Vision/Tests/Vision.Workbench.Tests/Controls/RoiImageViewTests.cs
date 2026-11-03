using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Vision.Controls.Models;
using Vision.Controls.Presentation;
using Vision.Controls.Services;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Xunit;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class RoiImageViewTests(WorkbenchAppFixture host)
{
    [Fact]
    public void RoiPreset_KeepsOriginalBase_AndIntermediateImageStaysExplicit()
    {
        host.Invoke(() =>
        {
            using var workspace = Workspace(true); using var browser = new VisionBrowserViewModel(workspace);
            browser.LoadImage(); var input = browser.InputImage;
            var page = new VisionToolEditorView { Session = browser }; var window = Show(page);
            try
            {
                var viewport = (VisionImageViewport)page.ImagePanel.FindName("Viewport");
                browser.SelectedImageView = ImageViewKind.Input; Drain(); var inputBitmap = Bitmap(viewport);
                browser.CurrentCard!.Run(); var result = Assert.IsType<StageResult>(browser.CurrentCard.LastResult);
                browser.SelectedImageView = ImageViewKind.Roi; Drain();
                Assert.False(viewport.ShowRoiImage); Assert.True(browser.ShowRois);
                Assert.Same(input, DisplayImage(viewport)); Assert.Same(inputBitmap, Bitmap(viewport));
                Assert.Same(input, browser.InputImage); Assert.Same(input, viewport.Image);
                Assert.Equal(string.Empty, browser.RoiImageInfo);
                browser.ShowRois = false; Drain(); Assert.Same(input, DisplayImage(viewport));
                Assert.Same(result, browser.CurrentCard.LastResult);
                browser.SelectRoiImageCommand.Execute(null); Drain();
                Assert.True(viewport.ShowRoiImage); Assert.Same(result.RoiImage, DisplayImage(viewport));
                Assert.Contains("测试分割阶段", browser.RoiImageInfo); Assert.False(browser.ShowOutputImage);
                browser.SelectedImageView = ImageViewKind.Input; Drain();
                Assert.False(viewport.ShowRoiImage); Assert.Same(input, DisplayImage(viewport));
                browser.SelectRoiImageCommand.Execute(null); Drain();
                browser.SelectOriginalImageCommand.Execute(null); Drain();
                Assert.False(viewport.ShowRoiImage); Assert.True(browser.ShowOriginalImage);
                Assert.Same(input, DisplayImage(viewport)); Assert.Same(inputBitmap, Bitmap(viewport));
            }
            finally { window.Close(); Drain(); }
        });
    }
    [Fact]
    public void ClearingIntermediateImageDuringRoiDrag_DoesNotRefitOrRollBackTheGesture()
    {
        host.Invoke(() =>
        {
            using var workspace = Workspace(true); using var browser = new VisionBrowserViewModel(workspace);
            browser.LoadImage(); browser.RoiEditor.Clear(); var roi = browser.RoiEditor.AddRectangle(4, 4, 4, 4);
            var page = new VisionToolEditorView { Session = browser }; var window = Show(page);
            try
            {
                var viewport = (VisionImageViewport)page.ImagePanel.FindName("Viewport");
                browser.CurrentCard!.Run(); browser.SelectedImageView = ImageViewKind.Roi;
                browser.SelectRoiImageCommand.Execute(null); Drain(); viewport.Fit();
                Assert.True(browser.RoiEditor.TryBeginManipulation(new(4, 4), 0.1));
                typeof(VisionImageViewport).GetField("_gestureEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(viewport, browser.RoiEditor);
                Assert.True(browser.RoiEditor.UpdateManipulation(new(5, 4)));
                browser.CurrentCard.ClearResult(); Drain();
                Assert.True(browser.RoiEditor.IsManipulating); Assert.Equal(5, roi.Pose.X);
                Assert.Same(browser.InputImage, DisplayImage(viewport));
                Assert.Contains("非处理结果", browser.RoiImageInfo);
                browser.RoiEditor.EndManipulation();
            }
            finally { window.Close(); Drain(); }
        });
    }
    [Fact]
    public void MissingOrFailedIntermediateImage_IsExplicitlyReported_NotInvented()
    {
        host.Invoke(() =>
        {
            using var workspace = Workspace(false); using var browser = new VisionBrowserViewModel(workspace);
            browser.LoadImage(); browser.CurrentCard!.Run(); browser.SelectedImageView = ImageViewKind.Roi;
            browser.SelectRoiImageCommand.Execute(null);
            Assert.Contains("没有有效", browser.RoiImageInfo);
            using var failed = new StageResult { Success = false, RoiImage = VisionImage.FromBuffer(8, 8, PixelFormat.Mono8, new byte[64]) };
            Assert.Null(VisionDisplay.RoiImage(failed));
            Assert.Contains("非处理结果", VisionDisplay.RoiImageInfo(failed));
            Assert.Null(VisionDisplay.RoiImage(null));
        });
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameCoordinateBaseImageSwitch_PreservesDragAndItsCancellation(bool fit)
    {
        host.Invoke(() =>
        {
            var input = VisionImage.FromBuffer(8, 8, PixelFormat.Mono8, new byte[64]);
            using var result = new StageResult { RoiImage = VisionImage.FromBuffer(8, 8, PixelFormat.Mono8, new byte[64]) };
            var editor = new RoiEditorViewModel(); var roi = editor.AddRectangle(4, 4, 4, 4);
            var viewport = new VisionImageViewport { Image = input, Result = result, Editor = editor, ShowOutput = false };
            var window = Show(viewport);
            try
            {
                if (fit) viewport.Fit(); else viewport.ActualPixels();
                Assert.True(editor.TryBeginManipulation(new(4, 4), 0.1));
                typeof(VisionImageViewport).GetField("_gestureEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(viewport, editor);
                Assert.True(editor.UpdateManipulation(new(5, 4)));
                var scale = Field(viewport, "_scale"); var offset = Field(viewport, "_offset");
                // 两个绑定先后到达；第二个开关实际上不改变当前底图。
                viewport.ShowRoiImage = true; viewport.ShowOutput = true; Drain();
                Assert.True(editor.IsManipulating); Assert.Equal(5, roi.Pose.X);
                Assert.Same(result.RoiImage, DisplayImage(viewport));
                Assert.Equal(scale, Field(viewport, "_scale")); Assert.Equal(offset, Field(viewport, "_offset"));
                viewport.ShowRoiImage = false; Drain();
                Assert.True(editor.IsManipulating); Assert.Same(input, DisplayImage(viewport));
                viewport.ShowRois = false;
                Assert.False(editor.IsManipulating); Assert.Equal(4, roi.Pose.X);
            }
            finally { window.Close(); Drain(); }
        });
    }
    [Fact]
    public void ReplacingInputDuringDrag_StillRollsBack_EvenWithIdenticalDimensions()
    {
        host.Invoke(() =>
        {
            var editor = new RoiEditorViewModel(); var roi = editor.AddRectangle(4, 4, 4, 4);
            var viewport = new VisionImageViewport
            { Image = VisionImage.FromBuffer(8, 8, PixelFormat.Mono8, new byte[64]), Editor = editor, ShowOutput = false };
            var window = Show(viewport);
            try
            {
                Assert.True(editor.TryBeginManipulation(new(4, 4), 0.1));
                typeof(VisionImageViewport).GetField("_gestureEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(viewport, editor);
                Assert.True(editor.UpdateManipulation(new(5, 4)));
                viewport.Image = VisionImage.FromBuffer(8, 8, PixelFormat.Mono8, new byte[64]); Drain();
                Assert.False(editor.IsManipulating); Assert.Equal(4, roi.Pose.X);
            }
            finally { window.Close(); Drain(); }
        });
    }
    [Fact]
    public void RoiNotificationAutoSelectsRoiPreset_WithoutRollingBackTheActiveDrag()
    {
        host.Invoke(() =>
        {
            using var workspace = Workspace(true); using var browser = new VisionBrowserViewModel(workspace);
            browser.LoadImage(); browser.RoiEditor.Clear(); var roi = browser.RoiEditor.AddRectangle(4, 4, 4, 4);
            var page = new VisionToolEditorView { Session = browser }; var window = Show(page);
            try
            {
                var viewport = (VisionImageViewport)page.ImagePanel.FindName("Viewport");
                browser.CurrentCard!.Run(); browser.ShowRois = true; Drain();
                Assert.Equal(ImageViewKind.Result, browser.SelectedImageView);
                Assert.True(browser.RoiEditor.TryBeginManipulation(new(4, 4), 0.1));
                typeof(VisionImageViewport).GetField("_gestureEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(viewport, browser.RoiEditor);
                Assert.True(browser.RoiEditor.UpdateManipulation(new(5, 4))); Drain();
                Assert.Equal(ImageViewKind.Roi, browser.SelectedImageView);
                Assert.False(viewport.ShowRoiImage); Assert.True(browser.ShowRois);
                Assert.True(browser.RoiEditor.IsManipulating);
                Assert.Equal(5, roi.Pose.X);
                Assert.Same(browser.InputImage, DisplayImage(viewport));
                browser.RoiEditor.EndManipulation(); Assert.Equal(5, roi.Pose.X);
            }
            finally { window.Close(); Drain(); }
        });
    }
    [Fact]
    public void SwitchingToLocalCropOutput_RollsBackOriginalCoordinateDrag()
    {
        host.Invoke(() =>
        {
            var editor = new RoiEditorViewModel(); var roi = editor.AddRectangle(4, 4, 4, 4);
            using var result = new CropResult
            {
                OutputImage = VisionImage.FromBuffer(4, 4, PixelFormat.Mono8, new byte[16]),
                InputOffset = new(2, 2), SourceBounds = new(2, 2, 4, 4)
            };
            var viewport = new VisionImageViewport
            { Image = VisionImage.FromBuffer(8, 8, PixelFormat.Mono8, new byte[64]), Result = result, Editor = editor, ShowOutput = false };
            var window = Show(viewport);
            try
            {
                Assert.True(editor.TryBeginManipulation(new(4, 4), 0.1));
                typeof(VisionImageViewport).GetField("_gestureEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(viewport, editor);
                Assert.True(editor.UpdateManipulation(new(5, 4)));
                viewport.ShowOutput = true; Drain();
                Assert.False(editor.IsManipulating); Assert.Equal(4, roi.Pose.X);
                Assert.False(editor.UpdateManipulation(new(6, 4))); Assert.Same(result.OutputImage, DisplayImage(viewport));
            }
            finally { window.Close(); Drain(); }
        });
    }
    private static object? Field(VisionImageViewport view, string name) => typeof(VisionImageViewport).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view);
    private static VisionBrowserWorkspace Workspace(bool stage) => new(new(
        [new("Arbitrary", "Other", () => (new StageTool(stage), new object(), null))], [],
        _ => throw new InvalidOperationException(), () => VisionImage.FromBuffer(8, 8, PixelFormat.Mono8, Enumerable.Repeat((byte)10, 64).ToArray())));
    private sealed class StageTool(bool publish) : IExecutableTool
    {
        public string Name => "Arbitrary";
        public long LastElapsedMs => 0;
        public ValidationResult Validate() => new();
        public ToolResult Run(VisionImage? image) => publish ? new StageResult
        { RoiImage = VisionImage.FromBuffer(image!.Width, image.Height, PixelFormat.Mono8, Enumerable.Repeat((byte)200, image.Width * image.Height).ToArray()) } : new ToolResult();
        public void EnsureLoaded() { }
        public void Dispose() { }
    }
    private sealed class StageResult : ToolResult, IRoiImageResult
    {
        public VisionImage? RoiImage { get; set; }
        public string RoiImageDescription => "测试分割阶段";
        public override void Dispose() { RoiImage = null; base.Dispose(); }
    }
    private static object? DisplayImage(VisionImageViewport view) => typeof(VisionImageViewport).GetProperty("DisplayImage", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view);
    private static object? Bitmap(VisionImageViewport view) => typeof(VisionImageViewport).GetField("_bitmap", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view);
    private static Window Show(object content)
    {
        var window = new Window { Content = content, Width = 1300, Height = 850, Left = -10000, Top = -10000, ShowInTaskbar = false };
        window.Show(); Drain(); return window;
    }
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
}
