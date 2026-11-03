#nullable enable
using Vision.Core.Calibration;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>3..4096个同帧同空间点对拟合像素→毫米二维仿射；残差限和条件数限必须明确。</summary>
public sealed class AffineCalibrationOperation : VisionOperation<AffineCalibrationOutput>
{
    public const string Id = "vision.calibration.affine-points";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
    public IReadOnlyList<CalibrationPointPair> Points
    {
        get;
    }

    public double MaximumResidualMillimeters
    {
        get;
    }

    public double MaximumConditionNumber
    {
        get;
    }

    public AffineCalibrationOperation(IEnumerable<CalibrationPointPair> points, double maximumResidualMillimeters, double maximumConditionNumber = 1e6)
    {
        ArgumentNullException.ThrowIfNull(points);
        var snapshot = points.Take(4097).ToArray();
        if (snapshot.Length < 3 || snapshot.Length > 4096 || snapshot.Any(pair => pair == null))
        {
            throw new ArgumentException("仿射标定需要3..4096个非空点对。", nameof(points));
        }
        var first = snapshot[0];
        if (snapshot.Any(pair => pair.Pixel.FrameId != first.Pixel.FrameId
            || pair.Pixel.CoordinateSpaceId != first.Pixel.CoordinateSpaceId || pair.World.WorldSpaceId != first.World.WorldSpaceId))
        {
            throw new ArgumentException("参考点必须属于同一参考帧、像素空间与毫米世界空间。", nameof(points));
        }
        if (!double.IsFinite(maximumResidualMillimeters) || maximumResidualMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumResidualMillimeters));
        }
        if (!double.IsFinite(maximumConditionNumber) || maximumConditionNumber < 1 || maximumConditionNumber > 1e12)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumConditionNumber));
        }
        Points = Array.AsReadOnly(snapshot);
        MaximumResidualMillimeters = maximumResidualMillimeters;
        MaximumConditionNumber = maximumConditionNumber;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
