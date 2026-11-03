#nullable enable
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>同帧同空间且同一标定的两个已映射毫米点求距，不自动混合标定修订。</summary>
public sealed class CalibratedPointDistanceOperation : VisionOperation<AffineMillimeterDistanceOutput>
{
    public const string Id = "vision.geometry.calibrated-point-distance";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
    public CalibratedMillimeterPointOutput First
    {
        get;
    }

    public CalibratedMillimeterPointOutput Second
    {
        get;
    }

    public CalibratedPointDistanceOperation(CalibratedMillimeterPointOutput first, CalibratedMillimeterPointOutput second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        PixelGeometryValidation.RequireSameSource(first.Source.FrameId, first.Source.CoordinateSpaceId,
            second.Source.FrameId, second.Source.CoordinateSpaceId);
        if (!ReferenceEquals(first.Calibration, second.Calibration))
        {
            throw new ArgumentException("毫米点距必须显式使用同一个标定快照，不能混合不同标定修订。");
        }
        First = first;
        Second = second;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
