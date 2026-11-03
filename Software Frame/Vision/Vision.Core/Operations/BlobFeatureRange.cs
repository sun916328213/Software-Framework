#nullable enable

namespace Vision.Core.Operations;

/// <summary>明确特征的包含上下限范围，不使用任意字符串属性名或SDK参数字典。</summary>
public sealed class BlobFeatureRange
{
    public BlobFeature Feature
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

    public BlobFeatureRange(BlobFeature feature, double minimum, double maximum)
    {
        if (!Enum.IsDefined(feature))
        {
            throw new ArgumentOutOfRangeException(nameof(feature));
        }

        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || minimum < 0 || maximum < minimum
            || feature is (BlobFeature.Extent or BlobFeature.ContourCircularity) && maximum > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum));
        }

        Feature = feature;
        Minimum = minimum;
        Maximum = maximum;
    }
}
