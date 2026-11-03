#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>点到无限直线的无符号正交距离，不是线段端点距离或测宽。</summary>
public sealed class PointLineDistanceOperation : VisionOperation<ScalarMeasurementOutput>
{
    public const string Id = "vision.geometry.point-line-distance";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;

    public PixelPoint Point
    {
        get;
    }

    public PixelLine Line
    {
        get;
    }


    public PointLineDistanceOperation(PixelPoint point, PixelLine line)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(line);
        PixelGeometryValidation.RequireSameSource(point.FrameId, point.CoordinateSpaceId, line.FrameId, line.CoordinateSpaceId);

        Point = point;
        Line = line;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
