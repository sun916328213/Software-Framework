#nullable enable
using Vision.Core.Calibration;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>把适用像素空间中的完整有向无限直线映射到毫米平面，不能只变换法向或乘平均比例。</summary>
public sealed class PixelLineToMillimetersOperation : VisionOperation<CalibratedMillimeterLineOutput>
{
    public const string Id = "vision.calibration.pixel-line-to-world";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
    public PixelLine Source
    {
        get;
    }

    public AffinePixelCalibration Calibration
    {
        get;
    }

    public PixelLineToMillimetersOperation(PixelLine source, AffinePixelCalibration calibration)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(calibration);
        if (source.CoordinateSpaceId != calibration.PixelSpaceId)
        {
            throw new ArgumentException("像素直线与仿射标定空间不符，不能猜裁剪或分支映射。");
        }
        Source = source;
        Calibration = calibration;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
