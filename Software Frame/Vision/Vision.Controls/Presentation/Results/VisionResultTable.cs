using System.Data;
using System.Globalization;
using Vision.Core.AI;
using Vision.Core.Results;

#nullable enable
namespace Vision.Controls.Presentation;

/// <summary>非 Blob 结果的只读标量快照；不持有 SDK、掩膜像素或原始 DTO 引用，也不冒充画布选择。</summary>
public sealed class VisionResultTable : IDisposable
{
    private readonly DataTable _table;
    private bool _disposed;
    public DataView View { get; }
    private VisionResultTable(DataTable table)
    {
        _table = table;
        View = new DataView(table) { AllowEdit = false, AllowNew = false, AllowDelete = false };
    }
    private static VisionResultTable Build((string Name, Type Type)[] columns, IEnumerable<object[]> rows)
    {
        var table = new DataTable { Locale = CultureInfo.InvariantCulture };
        try
        {
            foreach (var column in columns) table.Columns.Add(column.Name, column.Type);
            foreach (var row in rows) table.Rows.Add(row);
            return new(table);
        }
        catch { table.Dispose(); throw; }
    }
    public static VisionResultTable? Create(ToolResult? result)
    {
        if (result?.Success != true) return null;
        return result switch
        {
            Vision.Core.Tools.ToleranceResult tolerance when tolerance.AppliedMeasurement != null && tolerance.IsWithinTolerance.HasValue => Build(
                [("OK", typeof(bool)), ("测量值", typeof(double)), ("下限（含）", typeof(double)), ("上限（含）", typeof(double)), ("单位", typeof(string))],
                [new object[] { tolerance.IsWithinTolerance.Value, tolerance.AppliedMeasurement.Value, tolerance.AppliedMinimum, tolerance.AppliedMaximum, tolerance.AppliedMeasurement.Unit.ToString() }]),
            Vision.Tools.Halcon.Measurement.GeometryResult geometry when geometry.Operation == Vision.Tools.Halcon.Measurement.GeometryOperation.LineAngle => Build(
                [("夹角（度）", typeof(double)), ("单位", typeof(string))], [new object[] { geometry.AngleDegrees, NumericUnit.Degree.ToString() }]),
            Vision.Tools.Halcon.Measurement.GeometryResult geometry when geometry.Operation == Vision.Tools.Halcon.Measurement.GeometryOperation.CircleDiameter => Build(
                [("直径", typeof(double)), ("半径", typeof(double)), ("单位", typeof(string)), ("圆心X", typeof(double)), ("圆心Y", typeof(double))],
                [new object[] { geometry.Diameter, geometry.AppliedRadius, geometry.AppliedCircle?.Unit.ToString() ?? "未声明", geometry.AppliedCenter.X, geometry.AppliedCenter.Y }]),
            Vision.Tools.Halcon.Measurement.GeometryResult geometry when geometry.Operation == Vision.Tools.Halcon.Measurement.GeometryOperation.Distance => Build(
                [("距离", typeof(double)), ("单位", typeof(string)), ("执行起点X", typeof(double)), ("执行起点Y", typeof(double)), ("执行终点X", typeof(double)), ("执行终点Y", typeof(double))],
                [new object[] { geometry.Distance, (geometry.AppliedLine?.Unit ?? geometry.AppliedPoint?.Unit)?.ToString() ?? "未声明",
                    geometry.AppliedStart.X, geometry.AppliedStart.Y, geometry.AppliedEnd.X, geometry.AppliedEnd.Y }]),
            Vision.Tools.Halcon.Measurement.GeometryResult { Intersection: { } point } geometry => Build(
                [("交点X", typeof(double)), ("交点Y", typeof(double)), ("单位", typeof(string))],
                [new object[] { point.X, point.Y, geometry.TryGetPointField("Intersection", out _) ? "Pixel" : "未声明" }]),
            Vision.Tools.OpenCv.Processing.ThresholdResult threshold when threshold.AppliedMode != Vision.Tools.OpenCv.Processing.ThresholdMode.Range => Build(
                [("阈值方式", typeof(string)), ("自动阈值", typeof(double)), ("单位", typeof(string)), ("前景像素数", typeof(int)), ("前景占比", typeof(double))],
                [new object[] { threshold.AppliedMode.ToString(), (object?)threshold.AppliedOtsuThreshold ?? DBNull.Value, "Gray8", threshold.WhitePixelCount, threshold.ForegroundRatio }]),
            Vision.Tools.OpenCv.Processing.ThresholdResult threshold => Build(
                [("执行灰度下限", typeof(int)), ("执行灰度上限", typeof(int)), ("前景像素数", typeof(int)), ("前景占比", typeof(double))],
                [new object[] { threshold.AppliedMinGray, threshold.AppliedMaxGray, threshold.WhitePixelCount, threshold.ForegroundRatio }]),
            Vision.Tools.OpenCv.Processing.AdaptiveThresholdResult adaptive => Build(
                [("前景像素数", typeof(int)), ("前景占比", typeof(double))], [new object[] { adaptive.WhitePixelCount, adaptive.ForegroundRatio }]),
            Vision.Tools.OpenCv.Processing.ColorSegmentResult color => Build(
                [("颜色空间", typeof(string)), ("前景像素数", typeof(int)), ("前景占比", typeof(double))],
                [new object[] { color.Space.ToString(), color.WhitePixelCount, color.ForegroundRatio }]),
            Vision.Tools.OpenCv.Inspection.GrayStatisticsResult statistics => Build(
                [("统计项", typeof(string)), ("数值", typeof(double))],
                [new object[] { "有效像素数", (double)statistics.PixelCount }, new object[] { "最小灰度", (double)statistics.Minimum },
                 new object[] { "最大灰度", (double)statistics.Maximum }, new object[] { "平均灰度", statistics.Mean },
                 new object[] { "中位数", statistics.Median }, new object[] { "总体标准差", statistics.StandardDeviation }]),
            Vision.Tools.Halcon.Identification.BarcodeResult codes => Codes(codes.Codes),
            Vision.Tools.Halcon.Identification.DataCodeResult codes => Codes(codes.Codes),
            Vision.Tools.Halcon.Measurement.FindLineResult line => Line(line.Start, line.End, line.EdgePointCount),
            Vision.Tools.OpenCv.Measurement.FindLineResult line => Line(line.Start, line.End, line.EdgePointCount),
            Vision.Tools.OpenCv.Measurement.FindCircleResult circle => Build(
                [("中心 X", typeof(double)), ("中心 Y", typeof(double)), ("半径（像素）", typeof(double)), ("执行期望X", typeof(double)), ("执行期望Y", typeof(double))],
                [new object[] { circle.Center.X, circle.Center.Y, circle.Radius, (object?)circle.AppliedExpectedCenter?.X ?? DBNull.Value, (object?)circle.AppliedExpectedCenter?.Y ?? DBNull.Value }]),
            Vision.Tools.Halcon.Calibration.NPointsCalibrationResult calibration => Build(
                [("X 当量", typeof(double)), ("Y 当量", typeof(double)), ("变换矩阵", typeof(string))],
                [new object[] { calibration.ScaleX, calibration.ScaleY, calibration.Matrix }]),
            AIClassificationResult classification => Build(
                [("类别 ID", typeof(int)), ("类别", typeof(string)), ("分数", typeof(double))],
                classification.Scores.Select(i => new object[] { i.ClassId, i.Label, i.Score })),
            AIDetectionResult detection => Build(
                [("ID", typeof(int)), ("类别 ID", typeof(int)), ("类别", typeof(string)), ("分数", typeof(double)),
                 ("左上 X", typeof(double)), ("左上 Y", typeof(double)), ("宽", typeof(double)), ("高", typeof(double))],
                detection.Boxes.Select((i, n) => new object[] { n + 1, i.ClassId, i.Label, i.Score, i.Box.X, i.Box.Y, i.Box.Width, i.Box.Height })),
            AISegmentationResult segmentation => Build(
                [("ID", typeof(int)), ("类别", typeof(string)), ("分数", typeof(double)), ("左上 X", typeof(double)),
                 ("左上 Y", typeof(double)), ("框宽", typeof(double)), ("框高", typeof(double)), ("轮廓点数", typeof(int)), ("提供掩膜", typeof(bool))],
                segmentation.Items.Select((i, n) => new object[] { n + 1, i.Label, i.Score, i.Bounds.X, i.Bounds.Y,
                    i.Bounds.Width, i.Bounds.Height, i.Polygon.Count, i.Mask != null })),
            AIAnomalyResult anomaly => Build(
                [("是否异常", typeof(bool)), ("原始异常分数", typeof(double)), ("定位信息", typeof(string))],
                [new object[] { anomaly.IsAnomaly, anomaly.Score, anomaly.AnomalyBounds.Width > 0 && anomaly.AnomalyBounds.Height > 0
                    ? anomaly.AnomalyBounds.ToString() : "未提供有效定位" }]),
            Vision.Tools.OpenCv.Matching.TemplateMatchResult match => Build(
                [("ID", typeof(int)), ("分数", typeof(double)), ("中心 X", typeof(double)), ("中心 Y", typeof(double)),
                 ("角度（度）", typeof(double)), ("缩放", typeof(double))],
                match.Matches.Select((i, n) => new object[] { n + 1, i.Score, i.Center.X, i.Center.Y, i.AngleDeg, i.Scale })),
            Vision.Tools.Halcon.Matching.ShapeMatchResult match => Build(
                [("ID", typeof(int)), ("分数", typeof(double)), ("中心 X", typeof(double)), ("中心 Y", typeof(double)),
                 ("角度（度）", typeof(double)), ("缩放", typeof(double))],
                match.Matches.Select((i, n) => new object[] { n + 1, i.Score, i.Center.X, i.Center.Y, i.AngleDeg, i.Scale })),
            _ => null
        };
    }
    private static VisionResultTable Codes(IEnumerable<Vision.Tools.Halcon.Identification.DecodedCode> codes) => Build(
        [("ID", typeof(int)), ("解码文本", typeof(string)), ("码制字段", typeof(string))],
        codes.Select((i, n) => new object[] { n + 1, i.Text, i.Symbology })); // 后端尚未提供真实中心，不显示默认 (0,0)。
    private static VisionResultTable Line(Vision.Core.Geometry.Point2D start, Vision.Core.Geometry.Point2D end, int count) => Build(
        [("起点 X", typeof(double)), ("起点 Y", typeof(double)), ("终点 X", typeof(double)), ("终点 Y", typeof(double)), ("边缘点数", typeof(int))],
        [new object[] { start.X, start.Y, end.X, end.Y, count }]);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { View.Dispose(); }
        finally { _table.Dispose(); }
    }
}
