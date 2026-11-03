#nullable enable

namespace Vision.Core.Results;

/// <summary>独立标量及公差的只读OK/NG结果，实际判定由Tools公共算法执行。</summary>
public sealed class MeasurementRangeOutput
{
    public ScalarMeasurementOutput Measurement
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

    public MeasurementRangeOutput(ScalarMeasurementOutput measurement, double minimum, double maximum, bool passed)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum));
        }

        Measurement = measurement;
        Minimum = minimum;
        Maximum = maximum;
        Passed = passed;
    }
}
