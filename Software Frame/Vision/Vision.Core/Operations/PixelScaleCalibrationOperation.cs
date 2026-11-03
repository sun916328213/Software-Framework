#nullable enable
using Vision.Core.Calibration;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>从同帧同空间两个明确参考点及已知毫米长度建立均匀比例，不需要假帧或SDK模型。</summary>
public sealed class PixelScaleCalibrationOperation : VisionOperation<UniformPixelScaleCalibration>
{
    public const string Id = "vision.calibration.uniform-pixel-scale";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
    public PixelPoint First
    {
        get;
    }

    public PixelPoint Second
    {
        get;
    }

    public double KnownLengthMillimeters
    {
        get;
    }

    public PixelScaleCalibrationOperation(PixelPoint first, PixelPoint second, double knownLengthMillimeters)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        PixelGeometryValidation.RequireSameSource(first.FrameId, first.CoordinateSpaceId, second.FrameId, second.CoordinateSpaceId);
        var span = double.Hypot(second.Position.X - first.Position.X, second.Position.Y - first.Position.Y);
        if (!double.IsFinite(span) || span < 1 || !double.IsFinite(knownLengthMillimeters) || knownLengthMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(knownLengthMillimeters), "参考像素长度须至少1，已知毫米长度须有限为正。");
        }

        First = first;
        Second = second;
        KnownLengthMillimeters = knownLengthMillimeters;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
