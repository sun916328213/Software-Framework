#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>两条无限直线的交点；近似平行阈值为单位方向叉积的绝对值，不是像素长度。</summary>
public sealed class LineIntersectionOperation : VisionOperation<LineIntersectionOutput>
{
    public const string Id = "vision.geometry.line-intersection";
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

    public double MinimumSinAngle
    {
        get;
    }

    public LineIntersectionOperation(PixelLine first, PixelLine second, double minimumSinAngle = 1e-6)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        PixelGeometryValidation.RequireSameSource(first.FrameId, first.CoordinateSpaceId, second.FrameId, second.CoordinateSpaceId);
        if (!double.IsFinite(minimumSinAngle) || minimumSinAngle < 1e-12 || minimumSinAngle > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumSinAngle));
        }

        First = first;
        Second = second;
        MinimumSinAngle = minimumSinAngle;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
