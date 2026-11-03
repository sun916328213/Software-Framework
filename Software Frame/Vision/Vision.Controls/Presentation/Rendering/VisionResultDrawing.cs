using System.Windows;
using System.Windows.Media;
using Vision.Controls.View;
using Vision.Controls.ViewModels;
using Vision.Core.Results;
using Core = Vision.Core;
using Tools = Vision.Tools;

#nullable enable
namespace Vision.Controls.Presentation;

/// <summary>原结果到只读绘制的呈现适配；只借用Card结果，保留选择身份与每次结果的轮廓缓存。</summary>
internal sealed class VisionResultDrawing
{
    private readonly ToolResult? _result;
    private readonly IReadOnlyList<VisionResultItem> _resultItems;
    private readonly ResultOutlineCache _outline;
    private ResultOutlineCache _selectedOutline = ResultOutlineCache.Empty;
    private VisionResultItem? _selectedOutlineItem;
    public VisionResultDrawing(ToolResult? result)
    {
        _result = result;
        _resultItems = VisionDisplay.ResultItems(result);
        _outline = new(_resultItems.SelectMany(item => item.Contours));
    }
    public void Draw(DrawingContext dc, Pen pen, double scale, ResultSelectionViewModel? selection, bool showBounds, bool showCenters)
    {
        void Box(Core.Geometry.Rect2D b, Pen? style = null) { if (!b.IsEmpty) dc.DrawRectangle(null, style ?? pen, new Rect(b.X, b.Y, b.Width, b.Height)); }
        void Cross(Core.Geometry.Point2D p, Pen? style = null) { var r = 5 / scale; dc.DrawLine(style ?? pen, new Point(p.X - r, p.Y), new Point(p.X + r, p.Y)); dc.DrawLine(style ?? pen, new Point(p.X, p.Y - r), new Point(p.X, p.Y + r)); }
        switch (_result)
        {
            case Core.AI.AIDetectionResult:
                var selectedDetection = ReferenceEquals(selection?.Result, _result) ? selection?.SelectedId : null;
                var detectionHighlight = new Pen(Brushes.OrangeRed, 2 / scale);
                foreach (var item in _resultItems)
                    if (item.HasValidBounds) Box(item.Bounds, selectedDetection == item.Id ? detectionHighlight : pen);
                break;
            case Core.AI.AISegmentationResult segmentation:
                foreach (var item in segmentation.Items)
                {
                    if (item.Polygon.Count < 3) { Box(item.Bounds); continue; }
                    var geometry = new StreamGeometry();
                    using (var context = geometry.Open())
                    {
                        context.BeginFigure(new Point(item.Polygon[0].X, item.Polygon[0].Y), false, true);
                        context.PolyLineTo(item.Polygon.Skip(1).Select(p => new Point(p.X, p.Y)).ToArray(), true, false);
                    }
                    dc.DrawGeometry(null, pen, geometry);
                }
                break;
            case Core.AI.AIAnomalyResult anomaly:
                Box(anomaly.AnomalyBounds); break;
            case Tools.OpenCv.Inspection.BlobResult:
            case Tools.Halcon.Inspection.BlobResult:
                _outline.Draw(dc, pen, scale);
                var selectedItem = ReferenceEquals(selection?.Result, _result) ? selection?.SelectedItem : null;
                if (!ReferenceEquals(_selectedOutlineItem, selectedItem))
                {
                    _selectedOutlineItem = selectedItem;
                    _selectedOutline = selectedItem == null ? ResultOutlineCache.Empty : new(selectedItem.Contours);
                }
                var highlight = new Pen(Brushes.OrangeRed, 2 / scale);
                _selectedOutline.Draw(dc, highlight, scale);
                if (!showBounds && !showCenters) break;
                foreach (var item in _resultItems)
                {
                    var outlinePen = selectedItem?.Id == item.Id ? highlight : pen;
                    if (showBounds && !item.Bounds.IsEmpty)
                        dc.DrawRectangle(null, outlinePen, new Rect(item.Bounds.X, item.Bounds.Y, item.Bounds.Width, item.Bounds.Height));
                    if (showCenters)
                    {
                        var p = item.Center;
                        var r = 5 / scale;
                        dc.DrawLine(outlinePen, new Point(p.X - r, p.Y), new Point(p.X + r, p.Y));
                        dc.DrawLine(outlinePen, new Point(p.X, p.Y - r), new Point(p.X, p.Y + r));
                    }
                }
                break;
            case Tools.OpenCv.Matching.TemplateMatchResult:
            case Tools.Halcon.Matching.ShapeMatchResult:
                var selectedMatch = ReferenceEquals(selection?.Result, _result) ? selection?.SelectedId : null;
                foreach (var item in _resultItems)
                {
                    var matchPen = selectedMatch == item.Id ? new Pen(Brushes.OrangeRed, 2 / scale) : pen;
                    if (item.GeometryKind == VisionResultGeometryKind.TemplateRectangle && item.HasValidBounds)
                    {
                        if (item.Contours.Count == 0) Box(item.Bounds, matchPen);
                        else foreach (var contour in item.Contours)
                            for (var i = 0; i < contour.Points.Count; i++)
                            {
                                var a = contour.Points[i]; var b = contour.Points[(i + 1) % contour.Points.Count];
                                dc.DrawLine(matchPen, new Point(a.X, a.Y), new Point(b.X, b.Y));
                            }
                    }
                    Cross(item.Center, matchPen); // HALCON 只有真实中心，不能由训练预览臆造搜索图轮廓。
                }
                break;
            case Tools.Halcon.Measurement.GeometryResult circleGeometry when circleGeometry.AppliedCircle is { Unit: Core.Results.GeometryUnit.Pixel } measuredCircle:
                dc.DrawEllipse(null, pen, new Point(measuredCircle.Center.X, measuredCircle.Center.Y), measuredCircle.Radius, measuredCircle.Radius);
                dc.DrawLine(pen, new Point(measuredCircle.Center.X - measuredCircle.Radius, measuredCircle.Center.Y),
                    new Point(measuredCircle.Center.X + measuredCircle.Radius, measuredCircle.Center.Y)); break;
            case Tools.Halcon.Measurement.GeometryResult pointGeometry when pointGeometry.AppliedPoint is { Unit: Core.Results.GeometryUnit.Pixel } measuredPoint:
                dc.DrawLine(pen, new Point(measuredPoint.Position.X, measuredPoint.Position.Y), new Point(pointGeometry.AppliedEnd.X, pointGeometry.AppliedEnd.Y));
                Cross(measuredPoint.Position); Cross(pointGeometry.AppliedEnd); break;
            case Tools.Halcon.Measurement.GeometryResult { Operation: Tools.Halcon.Measurement.GeometryOperation.LineAngle,
                AppliedLine: { Unit: Core.Results.GeometryUnit.Pixel } angleFirst, AppliedReferenceLine: { Unit: Core.Results.GeometryUnit.Pixel } angleSecond }:
                dc.DrawLine(pen, new Point(angleFirst.Start.X, angleFirst.Start.Y), new Point(angleFirst.End.X, angleFirst.End.Y));
                dc.DrawLine(pen, new Point(angleSecond.Start.X, angleSecond.Start.Y), new Point(angleSecond.End.X, angleSecond.End.Y)); break;
            case Tools.Halcon.Measurement.GeometryResult { Operation: Tools.Halcon.Measurement.GeometryOperation.LineIntersection, Intersection: { } crossing,
                AppliedLine: { Unit: Vision.Core.Results.GeometryUnit.Pixel } firstLine, AppliedReferenceLine: { Unit: Vision.Core.Results.GeometryUnit.Pixel } secondLine }:
                dc.DrawLine(pen, new Point(firstLine.Start.X, firstLine.Start.Y), new Point(firstLine.End.X, firstLine.End.Y));
                dc.DrawLine(pen, new Point(secondLine.Start.X, secondLine.Start.Y), new Point(secondLine.End.X, secondLine.End.Y));
                Cross(crossing); break;
            case Tools.Halcon.Measurement.GeometryResult geometry when geometry.AppliedLine is { Unit: Vision.Core.Results.GeometryUnit.Pixel } measured:
                dc.DrawLine(pen, new Point(measured.Start.X, measured.Start.Y), new Point(measured.End.X, measured.End.Y)); break;
            case Tools.OpenCv.Measurement.FindLineResult line:
                dc.DrawLine(pen, new Point(line.Start.X, line.Start.Y), new Point(line.End.X, line.End.Y)); break;
            case Tools.Halcon.Measurement.FindLineResult line:
                dc.DrawLine(pen, new Point(line.Start.X, line.Start.Y), new Point(line.End.X, line.End.Y)); break;
            case Tools.OpenCv.Measurement.FindCircleResult circle when circle.Radius > 0:
                dc.DrawEllipse(null, pen, new Point(circle.Center.X, circle.Center.Y), circle.Radius, circle.Radius); break;
        }
    }
}
