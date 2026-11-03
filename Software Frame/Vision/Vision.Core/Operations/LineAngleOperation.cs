#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>同帧同空间两条无限直线的明确方向角度，单位度。</summary>
public sealed class LineAngleOperation : VisionOperation<ScalarMeasurementOutput>
{
    public const string Id = "vision.geometry.line-angle";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;

    public PixelLine First
    {
        get;
    }

    public PixelLine Second
    {
        get;
    }

    public LineAngleMode Mode
    {
        get;
    }

    public LineAngleOperation(PixelLine first, PixelLine second, LineAngleMode mode)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        PixelGeometryValidation.RequireSameSource(first.FrameId, first.CoordinateSpaceId, second.FrameId, second.CoordinateSpaceId);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        First = first;
        Second = second;
        Mode = mode;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
