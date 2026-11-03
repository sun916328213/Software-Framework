#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>先完整映射点和无限直线，再在毫米欧氏平面求垂距；同帧同空间同一标定，不做有限线段夹取。</summary>
public sealed class CalibratedPointLineDistanceOperation : VisionOperation<AffineMillimeterPointLineDistanceOutput>
{
    public const string Id = "vision.geometry.calibrated-point-line-distance";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
    public CalibratedMillimeterPointOutput Point
    {
        get;
    }

    public CalibratedMillimeterLineOutput Line
    {
        get;
    }

    public CalibratedPointLineDistanceOperation(CalibratedMillimeterPointOutput point, CalibratedMillimeterLineOutput line)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(line);
        PixelGeometryValidation.RequireSameSource(point.Source.FrameId, point.Source.CoordinateSpaceId,
            line.Source.FrameId, line.Source.CoordinateSpaceId);
        if (!ReferenceEquals(point.Calibration, line.Calibration))
        {
            throw new ArgumentException("毫米点线垂距不能混合不同标定快照或修订。");
        }
        Point = point;
        Line = line;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
