#nullable enable
using Vision.Core.Calibration;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>只将适用像素空间点经明确仿射映射为毫米世界坐标。</summary>
public sealed class PixelToMillimeterPointOperation : VisionOperation<CalibratedMillimeterPointOutput>
{
    public const string Id = "vision.calibration.pixel-to-world-point";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
    public PixelPoint Source
    {
        get;
    }

    public AffinePixelCalibration Calibration
    {
        get;
    }


    public PixelToMillimeterPointOperation(PixelPoint source, AffinePixelCalibration calibration)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(calibration);
        if (source.CoordinateSpaceId != calibration.PixelSpaceId)
        {
            throw new ArgumentException("像素空间与标定不符，不能猜裁剪或分支映射。");
        }

        Source = source;
        Calibration = calibration;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
