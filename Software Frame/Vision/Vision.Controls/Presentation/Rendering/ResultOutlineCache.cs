using System.Windows;
using System.Windows.Media;
using Vision.Core.Geometry;

#nullable enable
namespace Vision.Controls.View;

/// <summary>每个结果快照只构建一次；重绘只改变画布变换和笔宽，不重建轮廓点数组。</summary>
internal sealed class ResultOutlineCache
{
    public static ResultOutlineCache Empty { get; } = new([]);
    public StreamGeometry Geometry { get; } = new();
    public IReadOnlyList<Point> SinglePoints { get; }
    private double _pointScale = double.NaN;
    private StreamGeometry? _pointGeometry;
    public ResultOutlineCache(IEnumerable<RegionContour> contours)
    {
        var singles = new List<Point>();
        using (var context = Geometry.Open())
            foreach (var contour in contours)
            {
                var points = contour.Points;
                if (points.Count == 0) continue;
                if (points.Count == 1) { singles.Add(new(points[0].X, points[0].Y)); continue; }
                context.BeginFigure(new(points[0].X, points[0].Y), false, contour.IsClosed);
                for (var i = 1; i < points.Count; i++) context.LineTo(new(points[i].X, points[i].Y), true, false);
            }
        Geometry.Freeze();
        SinglePoints = singles.AsReadOnly();
    }
    public void Draw(DrawingContext context, Pen pen, double scale)
    {
        context.DrawGeometry(null, pen, Geometry);
        if (SinglePoints.Count == 0) return;
        if (_pointScale != scale)
        {
            var dots = new StreamGeometry { FillRule = FillRule.Nonzero };
            var radius = 1 / scale;
            using (var drawing = dots.Open())
                foreach (var point in SinglePoints)
                {
                    drawing.BeginFigure(new(point.X + radius, point.Y), true, true);
                    drawing.ArcTo(new(point.X - radius, point.Y), new Size(radius, radius), 0, false, SweepDirection.Clockwise, false, false);
                    drawing.ArcTo(new(point.X + radius, point.Y), new Size(radius, radius), 0, false, SweepDirection.Clockwise, false, false);
                }
            dots.Freeze(); _pointGeometry = dots; _pointScale = scale;
        }
        context.DrawGeometry(pen.Brush, null, _pointGeometry);
    }
}
