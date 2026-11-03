#nullable enable

namespace Vision.Core.Results;

/// <summary>所有完整选中对的扫描轴宽度统计；标准差为总体标准差，不自动剔除宽度异常值。</summary>
public sealed class CaliperWidthStatistics
{
    public int PairCount
    {
        get;
    }

    public ScalarMeasurementOutput Mean
    {
        get;
    }

    public double MinimumWidthPixels
    {
        get;
    }

    public double MaximumWidthPixels
    {
        get;
    }

    public double PopulationStandardDeviationPixels
    {
        get;
    }

    public CaliperWidthStatistics(int pairCount, ScalarMeasurementOutput mean,
        double minimumWidthPixels, double maximumWidthPixels, double populationStandardDeviationPixels)
    {
        ArgumentNullException.ThrowIfNull(mean);
        if (pairCount < 1 || mean.Kind != GeometryMeasurementKind.CaliperPairMeanWidth || mean.Value <= 0
            || !double.IsFinite(minimumWidthPixels) || !double.IsFinite(maximumWidthPixels)
            || !double.IsFinite(populationStandardDeviationPixels)
            || minimumWidthPixels <= 0 || maximumWidthPixels < minimumWidthPixels
            || mean.Value < minimumWidthPixels || mean.Value > maximumWidthPixels || populationStandardDeviationPixels < 0)
        {
            throw new ArgumentException("边缘对宽度统计的单位、范围或数量无效。");
        }

        PairCount = pairCount;
        Mean = mean;
        MinimumWidthPixels = minimumWidthPixels;
        MaximumWidthPixels = maximumWidthPixels;
        PopulationStandardDeviationPixels = populationStandardDeviationPixels;
    }
}
