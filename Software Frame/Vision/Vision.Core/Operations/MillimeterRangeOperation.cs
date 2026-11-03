#nullable enable
using Vision.Core.Calibration;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>只能对带标定依据的毫米长度做有限闭区间公差判定；不接受裸像素数改标签。</summary>
public sealed class MillimeterRangeOperation : VisionOperation<MillimeterRangeOutput>
{
    public const string Id = "vision.evaluation.millimeter-length-range";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
    public IMillimeterLengthMeasurement Source
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

    public MillimeterRangeOperation(IMillimeterLengthMeasurement source, double minimumMillimeters, double maximumMillimeters)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source is not (MillimeterLengthOutput or AffineMillimeterDistanceOutput or AffineMillimeterPointLineDistanceOutput))
        {
            throw new ArgumentException("毫米公差只接引擎明确支持的带标定长度快照。", nameof(source));
        }
        if (!double.IsFinite(minimumMillimeters) || !double.IsFinite(maximumMillimeters) || maximumMillimeters < minimumMillimeters)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumMillimeters));
        }

        Source = source;
        MinimumMillimeters = minimumMillimeters;
        MaximumMillimeters = maximumMillimeters;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
