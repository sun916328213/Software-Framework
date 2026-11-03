using System.Windows;
using System.Windows.Media;
using Vision.Core.Geometry;

namespace Vision.Controls.View;

/// <summary>模型局部轮廓的独立预览，不作为搜索图坐标或检测结果。</summary>
public sealed class TemplateContourPreview : FrameworkElement
{
    public static readonly DependencyProperty ContoursProperty = DependencyProperty.Register(nameof(Contours),
        typeof(IReadOnlyList<RegionContour>), typeof(TemplateContourPreview),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<RegionContour> Contours
    {
        get => (IReadOnlyList<RegionContour>)GetValue(ContoursProperty);
        set => SetValue(ContoursProperty, value);
    }
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(27, 30, 35)), null, new Rect(RenderSize));
        var points = Contours?.SelectMany(c => c.Points).ToArray();
        if (points == null || points.Length == 0 || ActualWidth <= 16 || ActualHeight <= 16) return;
        var left = points.Min(p => p.X); var top = points.Min(p => p.Y);
        var width = points.Max(p => p.X) - left;
        var height = points.Max(p => p.Y) - top;
        var scale = Math.Min((ActualWidth - 16) / Math.Max(1, width), (ActualHeight - 16) / Math.Max(1, height));
        Point Screen(Point2D p) => new((p.X - left) * scale + (ActualWidth - width * scale) / 2,
            (p.Y - top) * scale + (ActualHeight - height * scale) / 2);
        var pen = new Pen(Brushes.LimeGreen, 1.5);
        foreach (var contour in Contours)
        {
            if (contour.Points.Count == 0) continue;
            if (contour.Points.Count == 1) { dc.DrawEllipse(pen.Brush, null, Screen(contour.Points[0]), 2, 2); continue; }
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(Screen(contour.Points[0]), false, contour.IsClosed);
                context.PolyLineTo(contour.Points.Skip(1).Select(Screen).ToArray(), true, false);
            }
            dc.DrawGeometry(null, pen, geometry);
        }
    }
}
