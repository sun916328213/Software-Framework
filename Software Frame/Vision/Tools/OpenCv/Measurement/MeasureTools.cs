// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：FindLineParameters、FindLineResult、FindLineTool、FindCircleParameters、FindCircleResult、FindCircleTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：OpenCvSharp、Vision.Core.Geometry、Vision.Core.Imaging、Vision.Core.Results、Vision.Tools.OpenCv.Internal。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P4 —— FindLineTool / FindCircleTool（Measurement）。
// 对标 HALCON 侧 FindLineTool：参数语义对齐（期望几何 + 阈值 + 数量下限），
// Result 形状对齐（起终点 / 中心+半径 + 边缘点数），按业务容差对比，不逐像素一致。
//
// 实现：Canny → 感兴趣带过滤 → FitLine（直线）；GaussianBlur → HoughCircles（圆）。
// ─────────────────────────────────────────────────────────────────────────────

using OpenCvSharp;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Internal;

namespace Vision.Tools.OpenCv.Measurement;

/// <summary>找线参数（语义与 HALCON 侧 FindLineParameters 对齐）。</summary>
public sealed class FindLineParameters
{
    /// <summary>期望线段起点（X=列，Y=行）。</summary>
    public Point2D LineStart { get; set; } = new(100, 100);

    /// <summary>期望线段终点。</summary>
    public Point2D LineEnd { get; set; } = new(200, 100);

    /// <summary>搜索带半宽（像素，>0；只取距期望线段此距离内的边缘点）。</summary>
    public double BandHalfWidth { get; set; } = 10;

    /// <summary>Canny 低阈值（>0）。</summary>
    public double CannyLow { get; set; } = 50;

    /// <summary>Canny 高阈值（>CannyLow）。</summary>
    public double CannyHigh { get; set; } = 150;

    /// <summary>最少边缘点数（≥2）。</summary>
    public int MinEdgePoints { get; set; } = 10;
}

/// <summary>找线结果（与 HALCON 侧同形）。</summary>
public sealed class FindLineResult : ToolResult
{
    /// <summary>拟合直线起点（裁到边缘点投影区间）。</summary>
    public Point2D Start { get; set; }

    /// <summary>拟合直线终点。</summary>
    public Point2D End { get; set; }

    /// <summary>参与拟合的边缘点数。</summary>
    public int EdgePointCount { get; set; }
}

/// <summary>
/// 找线工具（Measurement）：Canny 边缘 + 搜索带过滤 + 最小二乘拟合。
/// </summary>
public sealed class FindLineTool : OpenCvToolBase<FindLineParameters, FindLineResult>
{
    /// <inheritdoc/>
    public override string Name => "FindLine";

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ValidateCore。
    /// 正文中的关键协作调用包括 AddError、nameof；具体条件和先后次序见方法体。
    /// </remarks>
    protected override ValidationResult ValidateCore(FindLineParameters parameters)
    {
        var result = new ValidationResult();
        if (parameters.LineStart == parameters.LineEnd)
            result.AddError("LINE_DEGENERATE", "期望线段起点与终点重合。", nameof(FindLineParameters.LineStart));
        if (parameters.BandHalfWidth <= 0)
            result.AddError("BAND_WIDTH", $"搜索带半宽 {parameters.BandHalfWidth} 必须为正。", nameof(FindLineParameters.BandHalfWidth));
        if (parameters.CannyLow <= 0)
            result.AddError("CANNY_LOW", $"Canny 低阈值 {parameters.CannyLow} 必须为正。", nameof(FindLineParameters.CannyLow));
        if (parameters.CannyHigh <= parameters.CannyLow)
            result.AddError("CANNY_HIGH", $"Canny 高阈值 {parameters.CannyHigh} 必须大于低阈值 {parameters.CannyLow}。", nameof(FindLineParameters.CannyHigh));
        if (parameters.MinEdgePoints < 2)
            result.AddError("EDGE_POINTS", $"最少边缘点数 {parameters.MinEdgePoints} 至少为 2。", nameof(FindLineParameters.MinEdgePoints));
        return result;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 运行当前处理流程；对应入口为 RunCore。
    /// 正文中的关键协作调用包括 FromVisionImage、Canny、FindNonZero、GetArray、Sqrt；具体条件和先后次序见方法体。
    /// </remarks>
    protected override FindLineResult RunCore(VisionImage image, FindLineParameters parameters)
    {
        using var src = MatConverter.FromVisionImage(image);
        using var edges = new Mat();
        Cv2.Canny(src, edges, parameters.CannyLow, parameters.CannyHigh);
        using var nonzero = new Mat();
        Cv2.FindNonZero(edges, nonzero);
        nonzero.GetArray(out Point[] edgePoints);
        {
            var dx = parameters.LineEnd.X - parameters.LineStart.X;
            var dy = parameters.LineEnd.Y - parameters.LineStart.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            var dirX = dx / length;
            var dirY = dy / length;

            var inBand = new List<Point2f>();
            foreach (var point in edgePoints)
            {
                var relX = point.X - parameters.LineStart.X;
                var relY = point.Y - parameters.LineStart.Y;
                var dist = Math.Abs(relX * dirY - relY * dirX);
                if (dist <= parameters.BandHalfWidth)
                    inBand.Add(new Point2f(point.X, point.Y));
            }

            if (inBand.Count < parameters.MinEdgePoints)
                throw new InvalidOperationException($"带内仅 {inBand.Count} 个边缘点，少于下限 {parameters.MinEdgePoints}。");

            // 直线拟合（点数已保底；拟合方向仅用于存在性确认，裁剪沿期望方向投影）。
            using var pointArray = InputArray.Create(inBand);
            using var fittedLine = new Mat();
            Cv2.FitLine(pointArray, fittedLine, DistanceTypes.L2, 0, 0.01, 0.01);

            var projections = inBand.Select(p =>
                (p.X - parameters.LineStart.X) * dirX + (p.Y - parameters.LineStart.Y) * dirY).ToArray();
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
                EdgePointCount = inBand.Count,
            };
        }
    }
}

/// <summary>找圆参数。</summary>
public sealed class FindCircleParameters
{
    /// <summary>期望圆心（X=列，Y=行；用于多候选时就近选择）。</summary>
    public Point2D ExpectedCenter { get; set; } = new(100, 100);

    /// <summary>最小半径（>0）。</summary>
    public double RadiusMin { get; set; } = 10;

    /// <summary>最大半径（≥最小半径）。</summary>
    public double RadiusMax { get; set; } = 100;

    /// <summary>Canny 高阈值（>0，传给 Hough 梯度法）。</summary>
    public double CannyHigh { get; set; } = 100;

    /// <summary>累加器阈值（>0；越小检出越多）。</summary>
    public double AccumThreshold { get; set; } = 20;
}

/// <summary>找圆结果。</summary>
public sealed class FindCircleResult : ToolResult
{
    /// <summary>圆心（X=列，Y=行）。</summary>
    public Point2D Center { get; set; }

    /// <summary>半径（像素）。</summary>
    public double Radius { get; set; }
}

/// <summary>
/// 找圆工具（Measurement）：高斯平滑 + Hough 梯度法。
/// </summary>
public sealed class FindCircleTool : OpenCvToolBase<FindCircleParameters, FindCircleResult>
{
    /// <inheritdoc/>
    public override string Name => "FindCircle";

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 ValidateCore。
    /// 正文中的关键协作调用包括 AddError、nameof；具体条件和先后次序见方法体。
    /// </remarks>
    protected override ValidationResult ValidateCore(FindCircleParameters parameters)
    {
        var result = new ValidationResult();
        if (parameters.RadiusMin <= 0)
            result.AddError("RADIUS_MIN", $"最小半径 {parameters.RadiusMin} 必须为正。", nameof(FindCircleParameters.RadiusMin));
        if (parameters.RadiusMax < parameters.RadiusMin)
            result.AddError("RADIUS_RANGE", $"最大半径 {parameters.RadiusMax} 小于最小半径 {parameters.RadiusMin}。", nameof(FindCircleParameters.RadiusMax));
        if (parameters.CannyHigh <= 0)
            result.AddError("CANNY_HIGH", $"Canny 高阈值 {parameters.CannyHigh} 必须为正。", nameof(FindCircleParameters.CannyHigh));
        if (parameters.AccumThreshold <= 0)
            result.AddError("ACCUM_THRESHOLD", $"累加器阈值 {parameters.AccumThreshold} 必须为正。", nameof(FindCircleParameters.AccumThreshold));
        return result;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 运行当前处理流程；对应入口为 RunCore。
    /// 正文中的关键协作调用包括 FromVisionImage、GaussianBlur、HoughCircles、Max、Floor；具体条件和先后次序见方法体。
    /// </remarks>
    protected override FindCircleResult RunCore(VisionImage image, FindCircleParameters parameters)
    {
        using var src = MatConverter.FromVisionImage(image);
        using var blurred = new Mat();
        Cv2.GaussianBlur(src, blurred, new Size(9, 9), 2);

        var circles = Cv2.HoughCircles(blurred, HoughModes.Gradient, 1,
            Math.Max(parameters.RadiusMin, 10),
            parameters.CannyHigh, parameters.AccumThreshold,
            (int)Math.Floor(parameters.RadiusMin), (int)Math.Ceiling(parameters.RadiusMax));
        if (circles.Length == 0)
            throw new InvalidOperationException("未检出任何圆（可调小累加器阈值或检查半径范围）。");

        var best = circles
            .OrderBy(c => Math.Sqrt(
                (c.Center.X - parameters.ExpectedCenter.X) * (c.Center.X - parameters.ExpectedCenter.X) +
                (c.Center.Y - parameters.ExpectedCenter.Y) * (c.Center.Y - parameters.ExpectedCenter.Y)))
            .First();
        return new FindCircleResult
        {
            Center = new Point2D(Math.Round(best.Center.X, 3), Math.Round(best.Center.Y, 3)),
            Radius = Math.Round(best.Radius, 3),
        };
    }
}
