#nullable enable
using Vision.Core.Calibration;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>明确毫米世界空间及目标帧的逆映射，不自动猜帧或裁切视野。</summary>
public sealed class MillimeterPointToPixelOperation : VisionOperation<ReprojectedPixelPointOutput>
{
    public const string Id = "vision.calibration.world-to-pixel-point";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
    public MillimeterPoint Source
    {
        get;
    }

    public AffinePixelCalibration Calibration
    {
        get;
    }

    public Guid TargetFrameId
    {
        get;
    }

    public MillimeterPointToPixelOperation(MillimeterPoint source, AffinePixelCalibration calibration, Guid targetFrameId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(calibration);
        if (source.WorldSpaceId != calibration.WorldSpaceId || targetFrameId == Guid.Empty)
        {
            throw new ArgumentException("毫米世界空间或目标帧与逆标定不符。");
        }

        Source = source;
        Calibration = calibration;
        TargetFrameId = targetFrameId;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
