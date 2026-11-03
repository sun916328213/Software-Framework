#nullable enable
using Vision.Core.Operations;

namespace Vision.Core.Results;

/// <summary>统计快照的指定指标及闭区间判定；格式和帧来源保留在Source中，NG不是执行错误。</summary>
public sealed class GrayStatisticsRangeOutput
{
    public GrayStatisticsOutput Source { get; }
    public GrayStatisticsMetric Metric { get; }
    public double Value { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public bool Passed { get; }

    public GrayStatisticsRangeOutput(GrayStatisticsOutput source, GrayStatisticsMetric metric, double minimum, double maximum)
    {
        ArgumentNullException.ThrowIfNull(source);
        Value = metric switch
        {
            GrayStatisticsMetric.Minimum => source.Minimum,
            GrayStatisticsMetric.Maximum => source.Maximum,
            GrayStatisticsMetric.Mean => source.Mean,
            GrayStatisticsMetric.PopulationStandardDeviation => source.PopulationStandardDeviation,
            _ => throw new ArgumentOutOfRangeException(nameof(metric))
        };
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum));
        }
        Source = source;
        Metric = metric;
        Minimum = minimum;
        Maximum = maximum;
        Passed = Value >= minimum && Value <= maximum;
    }
}
