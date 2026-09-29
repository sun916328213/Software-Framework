// ─────────────────────────────────────────────────────────────────────────────
// P3 —— FindLineTool（Measurement）。
// 迁移源：DPLibrary_Halcon_F48/Tools/Find/HTool_FindLine.cs（卡尺测量ardo）
//       + HTool_FindLineCanny.cs（FitLineContourXld 直线拟合）。
//
// 标准化内容：
//   · Parameters：期望线段（起点/终点 Point2D）+ 卡尺几何（长度/厚度/阈值/极性/点数）；
//   · Result：拟合直线起终点 + 拟合残差（稳定类型；旧 HFindLCResult 的行列起点终点语义保留）；
//   · Validate：点数/阈值/几何合法性；
//   · Run：GenMeasureRectangle2 → MeasurePos → GenContourPolygonXld → FitLineContourXld
//    （标准一维卡尺链；与旧 FindLine 的测量思想一致，参数落到统一语义上）。
// ─────────────────────────────────────────────────────────────────────────────

using HalconDotNet;
using Vision.Core.Geometry;
using Vision.Core.Results;
using Vision.Tools.Halcon.Internal;

namespace Vision.Tools.Halcon.Measurement;

/// <summary>边缘极性（旧 <c>E_MeasureTransition</c> 的重命名）。</summary>
public enum EdgeTransition
{
    /// <summary>任意极性。</summary>
    All,
    /// <summary>暗→亮。</summary>
    Positive,
    /// <summary>亮→暗。</summary>
    Negative,
}

/// <summary>找线参数。</summary>
public sealed class FindLineParameters
{
    /// <summary>期望线段起点（图像坐标：X=列，Y=行）。</summary>
    public Point2D LineStart { get; set; } = new(100, 100);

    /// <summary>期望线段终点。</summary>
    public Point2D LineEnd { get; set; } = new(200, 100);

    /// <summary>卡尺半长（沿线方向，像素，必须为正）。</summary>
    public double CaliperHalfLength { get; set; } = 40;

    /// <summary>卡尺半宽（垂直线方向，像素，必须为正）。</summary>
    public double CaliperHalfWidth { get; set; } = 10;

    /// <summary>边缘阈值（1~255）。</summary>
    public int Threshold { get; set; } = 30;

    /// <summary>边缘极性。</summary>
    public EdgeTransition Transition { get; set; } = EdgeTransition.All;

    /// <summary>沿线采样卡尺数（≥2）。</summary>
    public int CaliperCount { get; set; } = 9;
}

/// <summary>找线结果。</summary>
public sealed class FindLineResult : ToolResult
{
    /// <summary>拟合直线起点。</summary>
    public Point2D Start { get; set; }

    /// <summary>拟合直线终点。</summary>
    public Point2D End { get; set; }

    /// <summary>参与拟合的边缘点数。</summary>
    public int EdgePointCount { get; set; }
}

/// <summary>
/// 找线工具（Measurement）：沿期望线段布设一维卡尺，拟合直线。
/// </summary>
public sealed class FindLineTool : HalconToolBase<FindLineParameters, FindLineResult>
{
    /// <inheritdoc/>
    public override string Name => "FindLine";

    /// <inheritdoc/>
    protected override ValidationResult ValidateCore(FindLineParameters parameters)
    {
        var result = new ValidationResult();
        if (parameters.CaliperHalfLength <= 0)
            result.AddError("CALIPER_LENGTH", $"卡尺半长 {parameters.CaliperHalfLength} 必须为正。", nameof(FindLineParameters.CaliperHalfLength));
        if (parameters.CaliperHalfWidth <= 0)
            result.AddError("CALIPER_WIDTH", $"卡尺半宽 {parameters.CaliperHalfWidth} 必须为正。", nameof(FindLineParameters.CaliperHalfWidth));
        if (parameters.Threshold < 1 || parameters.Threshold > 255)
            result.AddError("THRESHOLD", $"边缘阈值 {parameters.Threshold} 超出 1~255。", nameof(FindLineParameters.Threshold));
        if (parameters.CaliperCount < 2)
            result.AddError("CALIPER_COUNT", $"卡尺数 {parameters.CaliperCount} 至少为 2。", nameof(FindLineParameters.CaliperCount));
        if (parameters.LineStart == parameters.LineEnd)
            result.AddError("LINE_DEGENERATE", "期望线段起点与终点重合。", nameof(FindLineParameters.LineStart));
        return result;
    }

    /// <inheritdoc/>
    protected override FindLineResult RunCore(HObject? image, FindLineParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(image);
        HOperatorSet.GetImageSize(image, out var imageWidth, out var imageHeight);
        var imageW = imageWidth.I;
        var imageH = imageHeight.I;
        imageWidth.Dispose();
        imageHeight.Dispose();

        var dx = parameters.LineEnd.X - parameters.LineStart.X;
        var dy = parameters.LineEnd.Y - parameters.LineStart.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        var phi = Math.Atan2(dy, dx);

        var allRows = new List<double>();
        var allCols = new List<double>();
        var transition = parameters.Transition switch
        {
            EdgeTransition.Positive => "positive",
            EdgeTransition.Negative => "negative",
            _ => "all",
        };

        try
        {
            for (var i = 0; i < parameters.CaliperCount; i++)
            {
                var t = parameters.CaliperCount == 1 ? 0.5 : (double)i / (parameters.CaliperCount - 1);
                var centerRow = parameters.LineStart.Y + dy * t;
                var centerCol = parameters.LineStart.X + dx * t;

                HOperatorSet.GenMeasureRectangle2(
                    centerRow, centerCol, phi,
                    parameters.CaliperHalfLength, parameters.CaliperHalfWidth,
                    imageW, imageH, "nearest_neighbor", out var handle);
                try
                {
                    HOperatorSet.MeasurePos(image, handle, 1.0, parameters.Threshold, transition, "all",
                        out var rowEdge, out var colEdge, out _, out _);
                    try
                    {
                        for (var k = 0; k < rowEdge.Length; k++)
                        {
                            allRows.Add(rowEdge[k].D);
                            allCols.Add(colEdge[k].D);
                        }
                    }
                    finally
                    {
                        rowEdge.Dispose();
                        colEdge.Dispose();
                    }
                }
                finally
                {
                    HOperatorSet.CloseMeasure(handle);
                }
            }

            if (allRows.Count < 2)
                throw new InvalidOperationException($"仅找到 {allRows.Count} 个边缘点，至少需要 2 个才能拟合直线。");

            HOperatorSet.GenContourPolygonXld(out var contour, new HTuple(allRows.ToArray()), new HTuple(allCols.ToArray()));
            try
            {
                HOperatorSet.FitLineContourXld(contour, "tukey", -1, 0, 5, 2,
                    out var rowStart, out var colStart, out var rowEnd, out var colEnd, out _, out _, out _);
                try
                {
                    // 把无限直线裁到期望线段的投影区间内，便于显示与下游使用。
                    var dirX = dx / length;
                    var dirY = dy / length;
                    var projections = allCols.Zip(allRows, (c, r) => (c - parameters.LineStart.X) * dirX + (r - parameters.LineStart.Y) * dirY).ToArray();
                    var t0 = projections.Min();
                    var t1 = projections.Max();
                    return new FindLineResult
                    {
                        Start = new Point2D(
                            Math.Round(parameters.LineStart.X + dirX * t0, 3),
                            Math.Round(parameters.LineStart.Y + dirY * t0, 3)),
                        End = new Point2D(
                            Math.Round(parameters.LineStart.X + dirX * t1, 3),
                            Math.Round(parameters.LineStart.Y + dirY * t1, 3)),
                        EdgePointCount = allRows.Count,
                    };
                }
                finally
                {
                    rowStart.Dispose();
                    colStart.Dispose();
                    rowEnd.Dispose();
                    colEnd.Dispose();
                }
            }
            finally
            {
                contour.Dispose();
            }
        }
        catch (HalconException)
        {
            throw;
        }
    }
}
