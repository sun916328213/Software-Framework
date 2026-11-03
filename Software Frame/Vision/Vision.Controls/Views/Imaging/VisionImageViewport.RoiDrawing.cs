using System.Windows;
using System.Windows.Media;

#nullable enable
namespace Vision.Controls.View;

public sealed partial class VisionImageViewport
{
    private readonly List<Point> _polygonPoints = [];
    private Point _polygonCursor;
    private void AddPolygonPoint(Point screen, bool complete)
    {
        if (!ShowRois || !CanEditImage || !IsRoiEditingEnabled || LineSearch != null) return;
        var pixel = Pixel(screen);
        if (Image == null || pixel.X < 0 || pixel.Y < 0 || pixel.X >= Image.Width || pixel.Y >= Image.Height) return;
        Focus(); _polygonCursor = pixel;
        if (_polygonPoints.Count == 0 || (_polygonPoints[^1] - pixel).Length > 0.5) _polygonPoints.Add(pixel);
        if (complete) CompleteDrawing();
        InvalidateVisual();
    }
    /// <summary>确认当前草稿；鼠标、快捷键和页面按钮使用同一入口。</summary>
    public bool CompleteDrawing()
    {
        if (!IsDrawing) return false;
        if (Editor == null || !ShowRois || !CanEditImage || !IsRoiEditingEnabled || LineSearch != null)
        {
            CancelDrawing();
            return false;
        }
        var kind = Mode;
        if (kind == "多边形")
        {
            if (_polygonPoints.Count < 3) { SetValue(StatusPropertyKey, "多边形至少需要三个顶点。"); return false; }
            try { Editor.AddPolygon(_polygonPoints.Select(p => new Core.Geometry.Point2D(p.X, p.Y)).ToArray()); }
            catch (ArgumentException error) { SetValue(StatusPropertyKey, error.Message); return false; }
        }
        else
        {
            if (_draft is not { Width: >= 1, Height: >= 1 } rect)
            {
                SetValue(StatusPropertyKey, "请先在图像上拖拽绘制；松开鼠标即确认。");
                return false;
            }
            var x = rect.X + rect.Width / 2;
            var y = rect.Y + rect.Height / 2;
            switch (kind)
            {
                case "矩形": Editor.AddRectangle(x, y, rect.Width, rect.Height); break;
                case "旋转矩形": Editor.AddRotatedRectangle(x, y, rect.Width, rect.Height, 0); break;
                case "圆": Editor.AddCircle(x, y, Math.Min(rect.Width, rect.Height)); break;
                case "椭圆": Editor.AddEllipse(x, y, rect.Width, rect.Height); break;
            }
        }
        Mode = "选择"; // 结束单次绘制，显示编辑手柄，不再连续替换刚确认的 ROI。
        SetValue(StatusPropertyKey, $"已确认{kind} ROI；可拖动主体或手柄调整。");
        return true;
    }

    public void CancelDrawing()
    {
        if (IsDrawing)
        {
            Mode = "选择";
            SetValue(StatusPropertyKey, "已取消绘制，保留原有 ROI。");
        }
        else CancelGesture();
    }
    private void DrawPolygonDraft(DrawingContext drawing)
    {
        if (_polygonPoints.Count == 0) return;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(_polygonPoints[0], false, false);
            foreach (var point in _polygonPoints.Skip(1)) context.LineTo(point, true, false);
            context.LineTo(_polygonCursor, true, false);
        }
        drawing.DrawGeometry(null, new Pen(Brushes.Yellow, 1.5 / _scale), geometry);
        foreach (var point in _polygonPoints) drawing.DrawEllipse(Brushes.Yellow, null, point, 3 / _scale, 3 / _scale);
    }
}
