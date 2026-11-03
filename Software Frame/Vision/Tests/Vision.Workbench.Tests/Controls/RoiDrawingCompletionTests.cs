using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Imaging;
using Vision.Core.Roi;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class RoiDrawingCompletionTests(WorkbenchAppFixture host)
{
    [Theory]
    [InlineData("矩形", RoiKind.Rectangle)]
    [InlineData("旋转矩形", RoiKind.RotatedRectangle)]
    [InlineData("圆", RoiKind.Circle)]
    [InlineData("椭圆", RoiKind.Ellipse)]
    public void DragRelease_ConfirmsExactlyOneRoi_AndReturnsToSelection(string mode, RoiKind kind)
    {
        host.Invoke(() => WithViewport(view =>
        {
            view.Mode = mode;
            Set(view, "_start", new Point(10, 10));
            Set(view, "_draft", new Rect(10, 10, 30, 20));
            Set(view, "_gestureButton", MouseButton.Left);
            view.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent });
            Assert.Equal(kind, Assert.Single(view.Editor!.Rois).Kind);
            Assert.Equal("选择", view.Mode);
            var roi = view.Editor.Selected;
            view.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent });
            Assert.Same(roi, Assert.Single(view.Editor.Rois));
            Assert.False(view.Editor.IsManipulating);
        }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Polygon_EnterOrRightClickConfirms_AndReturnsToSelection(bool rightClick)
    {
        host.Invoke(() => WithViewport(view =>
        {
            view.Mode = "多边形";
            Points(view).AddRange([new(10, 10), new(40, 10), new(10, 40)]);
            if (rightClick)
                view.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = Mouse.MouseDownEvent });
            else PressKey(view, Key.Enter);
            var roi = Assert.Single(view.Editor!.Rois);
            Assert.Equal(RoiKind.Polygon, roi.Kind); Assert.Equal(3, roi.OutlinePoints.Count);
            Assert.Equal("选择", view.Mode); Assert.Empty(Points(view));
        }));
    }

    [Fact]
    public void Escape_CancelsOnlyTheDraft_AndKeepsThePreviouslyConfirmedSearchRoi()
    {
        host.Invoke(() => WithViewport(view =>
        {
            view.Editor!.MaxRois = 1;
            var original = view.Editor.AddRectangle(50, 50, 20, 10);
            view.Mode = "多边形";
            Points(view).AddRange([new(10, 10), new(40, 10), new(10, 40)]);
            PressKey(view, Key.Escape);
            Assert.Equal("选择", view.Mode); Assert.Empty(Points(view));
            Assert.Same(original, Assert.Single(view.Editor.Rois));
            Assert.Same(original, view.Editor.Selected);
        }));
    }

    [Fact]
    public void InvalidOrHiddenPolygon_CannotBeConfirmed_AndDoesNotReplaceExistingRoi()
    {
        host.Invoke(() => WithViewport(view =>
        {
            view.Editor!.MaxRois = 1;
            var original = view.Editor.AddRectangle(50, 50, 20, 10);
            view.Mode = "多边形"; Points(view).AddRange([new(10, 10), new(40, 10)]);
            PressKey(view, Key.Enter);
            Assert.Equal("多边形", view.Mode); Assert.Equal(2, Points(view).Count);
            Assert.Same(original, Assert.Single(view.Editor.Rois));
            Points(view).Add(new(10, 40));
            view.ShowRois = false;
            PressKey(view, Key.Enter);
            Assert.Same(original, Assert.Single(view.Editor.Rois));
            Assert.Empty(Points(view));
        }));
    }

    [Fact]
    public void PolygonDoubleClickPath_ConfirmsTheLastVertex_AndFinishesDrawing()
    {
        host.Invoke(() => WithViewport(view =>
        {
            view.Mode = "多边形";
            Points(view).AddRange([new(10, 10), new(40, 10)]);
            var scale = (double)Field("_scale").GetValue(view)!;
            var offset = (Vector)Field("_offset").GetValue(view)!;
            var screen = new Point(10 * scale + offset.X, 40 * scale + offset.Y);
            typeof(VisionImageViewport).GetMethod("AddPolygonPoint", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(view, [screen, true]);
            Assert.Equal(3, Assert.Single(view.Editor!.Rois).OutlinePoints.Count);
            Assert.Equal("选择", view.Mode); Assert.Empty(Points(view));
        }));
    }

    [Fact]
    public void CollinearPolygon_IsRejectedWithoutReplacingThePreviousRoi()
    {
        host.Invoke(() => WithViewport(view =>
        {
            view.Editor!.MaxRois = 1;
            var original = view.Editor.AddRectangle(50, 50, 20, 10);
            view.Mode = "多边形";
            Points(view).AddRange([new(10, 10), new(20, 20), new(30, 30)]);
            Assert.False(view.CompleteDrawing());
            Assert.Same(original, Assert.Single(view.Editor.Rois)); Assert.Equal("多边形", view.Mode);
            view.CancelDrawing(); Assert.Empty(Points(view)); Assert.Equal("选择", view.Mode);
        }));
    }

    private static List<Point> Points(VisionImageViewport view) => (List<Point>)Field("_polygonPoints").GetValue(view)!;
    private static FieldInfo Field(string name) => typeof(VisionImageViewport).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static void Set(VisionImageViewport view, string name, object value) => Field(name).SetValue(view, value);
    private static void PressKey(VisionImageViewport view, Key key) => view.RaiseEvent(
        new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(view)!, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
    private static void WithViewport(Action<VisionImageViewport> action)
    {
        var view = new VisionImageViewport
        {
            Editor = new RoiEditorViewModel(), ShowRois = true, ShowOutput = false,
            Image = VisionImage.FromBuffer(100, 100, Vision.Core.Imaging.PixelFormat.Mono8, new byte[10000])
        };
        var window = new Window { Content = view, Width = 500, Height = 450, Left = -10000, Top = -10000, ShowInTaskbar = false };
        window.Show(); Drain();
        try { action(view); }
        finally { window.Close(); Drain(); }
    }
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
}
