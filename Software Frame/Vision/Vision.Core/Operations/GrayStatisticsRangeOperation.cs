#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>明确选择一次灰度统计的指标并按原位深灰度单位作闭区间判定；不借用图像或SDK。</summary>
public sealed class GrayStatisticsRangeOperation : VisionOperation<GrayStatisticsRangeOutput>
{
    public const string Id = "vision.evaluation.gray-statistics-range";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
    public GrayStatisticsOutput Source { get; }
    public GrayStatisticsMetric Metric { get; }
    public double Minimum { get; }
    public double Maximum { get; }

    public GrayStatisticsRangeOperation(GrayStatisticsOutput source, GrayStatisticsMetric metric, double minimum, double maximum)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!Enum.IsDefined(metric))
        {
            throw new ArgumentOutOfRangeException(nameof(metric));
        }
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum));
        }
        Source = source;
        Metric = metric;
        Minimum = minimum;
        Maximum = maximum;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}

/// <summary>明确指标，不通过任意字段名或动态绑定查找。</summary>
public enum GrayStatisticsMetric
{
    Minimum,
    Maximum,
    Mean,
    PopulationStandardDeviation
}
