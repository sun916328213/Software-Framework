#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>同帧同空间两点的欧氏距离，单位像素；不携带假图。</summary>
public sealed class PointDistanceOperation : VisionOperation<ScalarMeasurementOutput>
{
    public const string Id = "vision.geometry.point-distance";
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


    public PointDistanceOperation(PixelPoint first, PixelPoint second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        PixelGeometryValidation.RequireSameSource(first.FrameId, first.CoordinateSpaceId, second.FrameId, second.CoordinateSpaceId);

        First = first;
        Second = second;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
