#nullable enable

namespace Vision.Core.Results;

/// <summary>毫米公差OK/NG及完整测量/标定依据快照，NG不是SDK执行失败。</summary>
public sealed class MillimeterRangeOutput
{
    public IMillimeterLengthMeasurement Measurement
    {
        get;
    }

    public double MinimumMillimeters
    {
        get;
    }

    public double MaximumMillimeters
    {
        get;
    }

    public bool Passed
    {
        get;
    }

    public MillimeterRangeOutput(IMillimeterLengthMeasurement measurement, double minimumMillimeters, double maximumMillimeters, bool passed)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        if (measurement is not (MillimeterLengthOutput or AffineMillimeterDistanceOutput or AffineMillimeterPointLineDistanceOutput))
        {
            throw new ArgumentException("毫米判定必须保留引擎支持的带标定测量。", nameof(measurement));
        }
        if (!double.IsFinite(minimumMillimeters) || !double.IsFinite(maximumMillimeters) || maximumMillimeters < minimumMillimeters)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumMillimeters));
        }

        if (passed != (measurement.ValueMillimeters >= minimumMillimeters && measurement.ValueMillimeters <= maximumMillimeters))
        {
            throw new ArgumentException("毫米公差判定必须符合本次测量与闭区间。", nameof(passed));
        }
        Measurement = measurement;
        MinimumMillimeters = minimumMillimeters;
        MaximumMillimeters = maximumMillimeters;
        Passed = passed;
    }
}
