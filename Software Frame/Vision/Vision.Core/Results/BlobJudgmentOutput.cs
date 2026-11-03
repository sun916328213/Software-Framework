#nullable enable
using Vision.Core.Operations;

namespace Vision.Core.Results;

/// <summary>本次数量或像素面积的数值与OK/NG快照；不需要图像或原生模型存活。</summary>
public sealed class BlobJudgmentOutput
{
    public Guid FrameId
    {
        get;
    }
    public Guid CoordinateSpaceId
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
    public double ActualValue
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
    public bool Passed
    {
        get;
    }

    public BlobJudgmentOutput(Guid frameId, Guid coordinateSpaceId, BlobJudgmentMetric metric,
        int? itemIndex, double actualValue, double minimum, double maximum, bool passed)
    {
        if (frameId == Guid.Empty || coordinateSpaceId == Guid.Empty || !Enum.IsDefined(metric))
        {
            throw new ArgumentException("判定必须注明数据来源和有效指标。");
        }

        if (!double.IsFinite(actualValue) || !double.IsFinite(minimum) || !double.IsFinite(maximum)
            || actualValue < 0 || minimum < 0 || maximum < minimum
            || metric == BlobJudgmentMetric.Count && itemIndex != null
            || metric == BlobJudgmentMetric.Area && (itemIndex == null || itemIndex < 1))
        {
            throw new ArgumentException("判定数值、上下限或目标选择无效。");
        }

        FrameId = frameId;
        CoordinateSpaceId = coordinateSpaceId;
        Metric = metric;
        ItemIndex = itemIndex;
        ActualValue = actualValue;
        Minimum = minimum;
        Maximum = maximum;
        Passed = passed;
    }
}
