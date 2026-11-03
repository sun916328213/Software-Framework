using Vision.Controls.ViewModels;
using Vision.Core.Geometry;
using Xunit;
using Cv = Vision.Tools.OpenCv.Measurement;

namespace Vision.Workbench.Tests.Controls;

public sealed class LineSearchEditorTests
{
    [Fact]
    public void EndpointDrag_WritesOriginalDto_AndCancelRestoresIt()
    {
        var parameters = new Cv.FindLineParameters { LineStart = new(0, 0), LineEnd = new(100, 0), BandHalfWidth = 10 };
        var editor = new LineSearchEditorSession(parameters);
        Assert.True(editor.Begin(new(0, 0), 2));
        Assert.True(editor.Update(new(10, 20)));
        Assert.Equal(new Point2D(10, 20), parameters.LineStart);
        Assert.False(editor.Update(parameters.LineEnd)); // 不允许退化成零长度。
        editor.End(cancel: true);
        Assert.Equal(new Point2D(0, 0), parameters.LineStart);
        Assert.Equal(new Point2D(100, 0), parameters.LineEnd);
        Assert.False(editor.IsActive);
    }
    [Fact]
    public void WidthHandle_UpdatesNativeHalfLength_AndBodyMovesBothEndpoints()
    {
        var parameters = new Vision.Tools.Halcon.Measurement.FindLineParameters
        { LineStart = new(0, 0), LineEnd = new(100, 0), CaliperHalfLength = 10, CaliperHalfWidth = 2, CaliperCount = 5 };
        var editor = new LineSearchEditorSession(parameters);
        Assert.Equal(new Point2D(50, -10), editor.WidthHandle);
        Assert.True(editor.Begin(editor.WidthHandle, 1));
        Assert.True(editor.Update(new(50, -25))); editor.End();
        Assert.Equal(25, parameters.CaliperHalfLength);
        Assert.Equal(2, parameters.CaliperHalfWidth); Assert.Equal(5, parameters.CaliperCount);
        Assert.True(editor.Begin(new(50, 0), 1));
        Assert.True(editor.Update(new(60, 20)));
        Assert.Equal(new Point2D(10, 20), parameters.LineStart);
        Assert.Equal(new Point2D(110, 20), parameters.LineEnd);
        editor.End(cancel: true);
        Assert.Equal(new Point2D(0, 0), parameters.LineStart); Assert.Equal(25, parameters.CaliperHalfLength);
    }
    [Fact]
    public void AllAreaRoiTypes_AreRealGeometry_NotRenamedRectangles()
    {
        var editor = new RoiEditorViewModel();
        editor.AddDefaultRectangle(); editor.AddDefaultRotatedRectangle(); editor.AddDefaultCircle(); editor.AddDefaultEllipse(); editor.AddDefaultPolygon();
        Assert.Equal(5, editor.Count);
        Assert.Equal(5, editor.Rois.Select(r => r.Kind).Distinct().Count());
        var polygon = editor.Selected!;
        Assert.Equal(3, polygon.OutlinePoints.Count);
        Assert.Throws<ArgumentException>(() => editor.AddPolygon([new(0, 0), new(1, 1), new(2, 2)]));
        Assert.Equal(5, editor.Count); // 失败不能留下半个区域或改变选择。
        Assert.Same(polygon, editor.Selected);
    }
}
