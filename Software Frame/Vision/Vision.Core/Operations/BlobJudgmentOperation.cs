#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>
/// 无图的区域数量/指定区域面积包含上下限判定。NG是本次有效业务结果，不是SDK执行失败。
/// 面积判定必须显式指定一基结果序号，不默认取第一个区域。
/// </summary>
public sealed class BlobJudgmentOperation : VisionOperation<BlobJudgmentOutput>
{
    public const string Id = "vision.evaluation.blob-range";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;

    public BlobOutput Source
    {
        get;
    }
    public BlobJudgmentMetric Metric
    {
        get;
    }
    public int? ItemIndex
    {
        get;
    }
    public double Minimum
    {
        get;
    }
    public double Maximum
    {
        get;
    }

    private BlobJudgmentOperation(BlobOutput source, BlobJudgmentMetric metric, int? index, double minimum, double maximum)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || minimum < 0 || maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum));
        }

        if (metric == BlobJudgmentMetric.Area && (index is not { } selected || selected < 1 || selected > source.Items.Count))
        {
            throw new ArgumentOutOfRangeException(nameof(index), "面积判定必须选择本次有效区域的一基序号。");
        }

        Source = source;
        Metric = metric;
        ItemIndex = index;
        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>数量仍为非负整数，闭区间边界可为有限小数，不对边界取整。</summary>
    public static BlobJudgmentOperation ForCount(BlobOutput source, double minimum, double maximum)
    {
        return new BlobJudgmentOperation(source, BlobJudgmentMetric.Count, null, minimum, maximum);
    }

    /// <summary>存在性复用完整数量判定，不把空结果当作执行异常。</summary>
    public static BlobJudgmentOperation ForPresence(BlobOutput source, bool expectedPresent = true)
    {
        return ForCount(source, expectedPresent ? 1 : 0, expectedPresent ? int.MaxValue : 0);
    }

    public static BlobJudgmentOperation ForArea(BlobOutput source, int itemIndex, double minimum, double maximum)
    {
        return new BlobJudgmentOperation(source, BlobJudgmentMetric.Area, itemIndex, minimum, maximum);
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
