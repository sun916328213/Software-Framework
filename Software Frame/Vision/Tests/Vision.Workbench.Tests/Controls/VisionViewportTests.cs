using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vision.Controls.Presentation;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Tools.OpenCv.Processing;
using ImageFormat = Vision.Core.Imaging.PixelFormat;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
public sealed class VisionViewportTests(WorkbenchAppFixture host)
{
    [Fact]
    public void BitmapConversion_RespectsStrideAndFreezesDisplaySnapshot()
    {
        host.Invoke(() =>
        {
            var image = VisionImage.FromBuffer(2, 2, ImageFormat.Mono8, [1, 2, 99, 3, 4, 99], stride: 3);
            var bitmap = VisionDisplay.ToBitmap(image);
            Assert.True(bitmap.IsFrozen);
            var pixels = new byte[4];
            bitmap.CopyPixels(pixels, 2, 0);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, pixels);
        });
    }

    [Fact]
    public void Viewport_RendersProcessedImage_OriginalToggle_AndRejectsFailedOutput()
    {
        host.Invoke(() =>
        {
            var image = VisionImage.FromBuffer(2, 2, ImageFormat.Mono8, [0, 0, 0, 0]);
            var output = VisionImage.FromBuffer(2, 2, ImageFormat.Mono8, [255, 255, 255, 255]);
            var viewport = new VisionImageViewport { Image = image };
            viewport.Measure(new Size(100, 100));
            viewport.Arrange(new Rect(0, 0, 100, 100));
            viewport.UpdateLayout();
            viewport.Fit();
            Assert.Equal(0, CenterBlue(viewport));
            viewport.Result = new ThresholdResult { OutputImage = output };
            Assert.Equal(255, CenterBlue(viewport));
            viewport.ShowResults = false;
            Assert.Equal(255, CenterBlue(viewport)); // 隐藏结果几何不隐藏处理输出底图。
            viewport.ShowOutput = false;
            Assert.Equal(0, CenterBlue(viewport));
            viewport.ShowOutput = true;
            viewport.Result = new ThresholdResult { Success = false, OutputImage = output };
            Assert.Equal(0, CenterBlue(viewport));
            viewport.ActualPixels();
            viewport.Fit();
        });
    }

    [Fact]
    public void RoiAndResultLayers_RenderIndependently_OnOriginalImage_WithoutChangingSelection()
    {
        host.Invoke(() =>
        {
            var editor = new RoiEditorViewModel();
            var roi = editor.AddCircle(70, 70, 20);
            var result = new Vision.Tools.OpenCv.Inspection.BlobResult
            {
                Items = [new() { Index = 1, Area = 400, Center = new Point2D(20, 20), Bounds = new Rect2D(10, 10, 20, 20) }]
            };
            var viewport = new VisionImageViewport
            {
                Image = VisionImage.FromBuffer(100, 100, ImageFormat.Mono8, new byte[10000]),
                Result = result, Editor = editor, ShowOutput = false, ShowResultCenters = true
            };
            viewport.Measure(new Size(100, 100));
            viewport.Arrange(new Rect(0, 0, 100, 100));
            viewport.UpdateLayout();
            viewport.ActualPixels();
            Assert.True(PixelAt(viewport, 20, 20).G > 100);
            Assert.Equal(Colors.Yellow, PixelAt(viewport, 80, 70));
            viewport.ShowResults = false;
            Assert.Equal(Colors.Black, PixelAt(viewport, 20, 20));
            Assert.Equal(Colors.Yellow, PixelAt(viewport, 80, 70));
            viewport.ShowRois = false;
            Assert.Equal(Colors.Black, PixelAt(viewport, 80, 70));
            viewport.ShowResults = true;
            Assert.True(PixelAt(viewport, 20, 20).G > 100);
            Assert.Equal(Colors.Black, PixelAt(viewport, 80, 70));
            Assert.Same(roi, editor.Selected);
            Assert.Single(editor.Rois);
            Assert.Same(result, viewport.Result);
        });
    }

    private static byte CenterBlue(FrameworkElement view) => PixelAt(view, 50, 50).B;
    private static Color PixelAt(FrameworkElement view, int x, int y)
    {
        view.UpdateLayout();
        var bitmap = new RenderTargetBitmap(100, 100, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }
}
