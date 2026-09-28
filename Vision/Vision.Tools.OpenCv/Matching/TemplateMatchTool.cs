// ─────────────────────────────────────────────────────────────────────────────
// P4 —— TemplateMatchTool（Matching）。
// 对标 HALCON 侧 ShapeMatchTool 的“模板→搜索”能力，但不要求 1:1（计划 §1）：
// OpenCV 用灰度归一化相关（CCoeffNormed / CCorrNormed / SqDiffNormed），
// 不做 HALCON 的形状金字塔。模板以 VisionImage 参数传入（Core 类型）。
//
// 多目标：迭代取最值 + 模板尺寸邻域抑制，最多 MaxMatches 个。
// 分数统一为“越大越好 0~1”（SqDiffNormed 取 1-value 折算，便于跨后端对比）。
// ─────────────────────────────────────────────────────────────────────────────

using OpenCvSharp;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Internal;

namespace Vision.Tools.OpenCv.Matching;

/// <summary>模板匹配方法。</summary>
public enum TemplateMatchMethod
{
    /// <summary>归一化平方差（越小越好，内部折算为 1-value）。</summary>
    SqDiffNormed,
    /// <summary>归一化相关系数（越大越好）。</summary>
    CCoeffNormed,
    /// <summary>归一化互相关（越大越好）。</summary>
    CCorrNormed,
}

/// <summary>模板匹配参数。</summary>
public sealed class TemplateMatchParameters
{
    /// <summary>模板图（Mono8，须小于等于搜索图）。</summary>
    public VisionImage? Template { get; set; }

    /// <summary>匹配方法。</summary>
    public TemplateMatchMethod Method { get; set; } = TemplateMatchMethod.CCoeffNormed;

    /// <summary>最小分数（0~1，越大越好口径）。</summary>
    public double MinScore { get; set; } = 0.8;

    /// <summary>最大匹配个数（≥1）。</summary>
    public int MaxMatches { get; set; } = 1;
}

/// <summary>单个匹配。</summary>
public sealed class TemplateMatchItem
{
    /// <summary>中心（X=列，Y=行）。</summary>
    public Point2D Center { get; set; }

    /// <summary>分数（0~1，越大越好）。</summary>
    public double Score { get; set; }

    /// <summary>外接矩形（左上角 + 模板宽高）。</summary>
    public Rect2D Bounds { get; set; }
}

/// <summary>模板匹配结果。</summary>
public sealed class TemplateMatchResult : ToolResult
{
    /// <summary>全部匹配（按分数降序）。</summary>
    public List<TemplateMatchItem> Matches { get; set; } = [];
}

/// <summary>
/// 模板匹配工具（Matching）：归一化灰度模板匹配。
/// </summary>
public sealed class TemplateMatchTool : OpenCvToolBase<TemplateMatchParameters, TemplateMatchResult>
{
    /// <inheritdoc/>
    public override string Name => "TemplateMatch";

    /// <inheritdoc/>
    protected override ValidationResult ValidateCore(TemplateMatchParameters parameters)
    {
        var result = new ValidationResult();
        if (parameters.Template == null || parameters.Template.IsEmpty)
            result.AddError("TEMPLATE_EMPTY", "模板为空，请先设置模板。", nameof(TemplateMatchParameters.Template));
        if (parameters.MinScore < 0 || parameters.MinScore > 1)
            result.AddError("MIN_SCORE", $"最小分数 {parameters.MinScore} 超出 0~1。", nameof(TemplateMatchParameters.MinScore));
        if (parameters.MaxMatches < 1)
            result.AddError("MAX_MATCHES", $"最大匹配个数 {parameters.MaxMatches} 必须 ≥1。", nameof(TemplateMatchParameters.MaxMatches));
        return result;
    }

    /// <inheritdoc/>
    protected override TemplateMatchResult RunCore(VisionImage image, TemplateMatchParameters parameters)
    {
        var template = parameters.Template!;
        if (template.Width > image.Width || template.Height > image.Height)
            throw new ArgumentException($"模板 {template.Width}×{template.Height} 大于搜索图 {image.Width}×{image.Height}。");

        var method = parameters.Method switch
        {
            TemplateMatchMethod.SqDiffNormed => TemplateMatchModes.SqDiffNormed,
            TemplateMatchMethod.CCorrNormed => TemplateMatchModes.CCorrNormed,
            _ => TemplateMatchModes.CCoeffNormed,
        };
        var lowerIsBetter = parameters.Method == TemplateMatchMethod.SqDiffNormed;

        using var src = MatConverter.FromVisionImage(image);
        using var tpl = MatConverter.FromVisionImage(template);
        using var response = new Mat();
        Cv2.MatchTemplate(src, tpl, response, method);

        var result = new TemplateMatchResult();
        using var suppressed = response.Clone();
        for (var n = 0; n < parameters.MaxMatches; n++)
        {
            Cv2.MinMaxLoc(suppressed, out var minVal, out var maxVal, out var minLoc, out var maxLoc);
            var raw = lowerIsBetter ? minVal : maxVal;
            var loc = lowerIsBetter ? minLoc : maxLoc;
            var score = lowerIsBetter ? 1 - raw : raw;
            if (score < parameters.MinScore)
                break;

            result.Matches.Add(new TemplateMatchItem
            {
                Center = new Point2D(loc.X + tpl.Width * 0.5, loc.Y + tpl.Height * 0.5),
                Score = Math.Round(score, 4),
                Bounds = new Rect2D(loc.X, loc.Y, tpl.Width, tpl.Height),
            });

            // 邻域抑制：已命中处填入最差值，避免重复检出同一目标。
            var x0 = Math.Max(0, loc.X - tpl.Width / 2);
            var y0 = Math.Max(0, loc.Y - tpl.Height / 2);
            var x1 = Math.Min(suppressed.Width, loc.X + tpl.Width / 2);
            var y1 = Math.Min(suppressed.Height, loc.Y + tpl.Height / 2);
            if (x1 > x0 && y1 > y0)
                suppressed[new Rect(x0, y0, x1 - x0, y1 - y0)].SetTo(lowerIsBetter ? double.MaxValue : double.MinValue);
            else
                break;
        }

        return result;
    }
}
