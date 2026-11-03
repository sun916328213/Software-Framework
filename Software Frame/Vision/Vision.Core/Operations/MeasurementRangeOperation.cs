#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>
/// 完成几何标量的包含上下限公差判定；单位必须与来源一致，NG仍为Success。
/// 有符号角区间按数轴比较，不跨±180度自动包绕；当前不支持毫米换标签。
/// </summary>
public sealed class MeasurementRangeOperation : VisionOperation<MeasurementRangeOutput>
{
    public const string Id = "vision.evaluation.measurement-range";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;

    public ScalarMeasurementOutput Source
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

    public GeometryMeasurementUnit Unit
    {
        get;
    }

    public MeasurementRangeOperation(ScalarMeasurementOutput source, double minimum, double maximum, GeometryMeasurementUnit unit)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!Enum.IsDefined(unit) || unit != source.Unit)
        {
            throw new ArgumentException("公差单位必须与来源测量一致，不猜单位或换算。", nameof(unit));
        }

        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum));
        }

        Source = source;
        Minimum = minimum;
        Maximum = maximum;
        Unit = unit;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
