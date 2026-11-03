using Vision.Controls.ViewModels;
using Vision.Core.Geometry;
using Vision.Core.Roi;
using Xunit;

namespace Vision.Workbench.Tests.Controls;

public sealed class RoiGeometryCompletionTests
{
    [Fact]
    public void RotationHandle_RotatesAboutCenter_AndCancelRestoresKindAndGeometry()
    {
        var editor = new RoiEditorViewModel();
        var roi = editor.AddRotatedRectangle(100, 100, 40, 20, 0);
        Assert.Equal(new Point2D(100, 70), editor.RotationHandle);
        Assert.True(editor.TryBeginManipulation(editor.RotationHandle!.Value, 0.5));
        Assert.True(editor.UpdateManipulation(new(130, 100)));
        Assert.Equal(90, roi.Pose.AngleDeg, 8);
        Assert.Equal(RoiKind.RotatedRectangle, roi.Kind);
        Assert.Equal(new Size2D(40, 20), roi.Size);
        Assert.Equal(100, roi.Pose.X); Assert.Equal(100, roi.Pose.Y);
        editor.EndManipulation(cancel: true);
        Assert.Equal(RoiKind.RotatedRectangle, roi.Kind); Assert.Equal(new Pose2D(100, 100, 0), roi.Pose);
        Assert.True(editor.TryBeginManipulation(editor.RotationHandle!.Value, 0.5));
        Assert.True(editor.UpdateManipulation(new(130, 100)));
        editor.EndManipulation();
        Assert.Equal(90, roi.Pose.AngleDeg, 8); Assert.Same(roi, editor.Selected);
        Assert.False(editor.IsManipulating);
    }
    [Fact]
    public void PolygonVertexDrag_UpdatesBounds_RejectsDegenerateShape_AndCanCancel()
    {
        var editor = new RoiEditorViewModel();
        Point2D[] original = [new(0, 0), new(40, 0), new(0, 40)];
        var polygon = editor.AddPolygon(original);
        Assert.Equal(original, editor.VertexHandles);
        Assert.True(editor.TryBeginManipulation(new(0, 0), 0.5));
        Assert.False(editor.UpdateManipulation(new(20, 20))); // 三点共线。
        Assert.Equal(original, polygon.OutlinePoints);
        Assert.True(editor.UpdateManipulation(new(-10, -20)));
        Assert.Equal(new Rect2D(-10, -20, 50, 60), polygon.Bounds);
        Assert.Equal(original[1], polygon.OutlinePoints[1]); Assert.Equal(original[2], polygon.OutlinePoints[2]);
        editor.EndManipulation(cancel: true);
        Assert.Equal(original, polygon.OutlinePoints); Assert.Equal(new Rect2D(0, 0, 40, 40), polygon.Bounds);
        Assert.True(editor.TryBeginManipulation(new(40, 0), 0.5));
        editor.UpdateManipulation(new(50, 0)); editor.EndManipulation();
        Assert.Equal(new Point2D(50, 0), polygon.OutlinePoints[1]);
        Assert.Equal(new Rect2D(0, 0, 50, 40), polygon.Bounds);
    }
    [Fact]
    public void SelectionChange_RollsBackTheOriginalPolygon_NotTheNewSelection()
    {
        var editor = new RoiEditorViewModel();
        var other = editor.AddCircle(100, 100, 10);
        var polygon = editor.AddPolygon([new(0, 0), new(40, 0), new(0, 40)]);
        Assert.True(editor.TryBeginManipulation(new(0, 0), 0.5));
        editor.UpdateManipulation(new(-10, -20));
        editor.Selected = other;
        Assert.Equal(new Point2D(0, 0), polygon.OutlinePoints[0]);
        Assert.Equal(new Rect2D(0, 0, 40, 40), polygon.Bounds);
        Assert.Same(other, editor.Selected); Assert.Equal(new Pose2D(100, 100, 0), other.Pose);
        Assert.False(editor.IsManipulating);
    }
}
