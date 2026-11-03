#nullable enable
using Vision.Core.Calibration;
using Vision.Core.Capabilities;
using Vision.Core.Diagnostics;
using Vision.Core.Engine;
using Vision.Core.Geometry;
using Vision.Core.Results;

namespace Vision.Core.Operations;

/// <summary>
/// 只用同一像素空间的均匀比例将像素长度换算为毫米，不直接接受角度、面积或再次换算的毫米值。
/// 标定可用于同空间后续帧；CoordinateSpaceId相同不证明光学/物理平面仍适用，须由调用方确认。
/// </summary>
public sealed class LengthToMillimetersOperation : VisionOperation<MillimeterLengthOutput>
{
    public const string Id = "vision.calibration.length-to-millimeters";
    public override string OperationId => Id;
    public override VisionExecutionDomain Domain => VisionExecutionDomain.Vision;
    public override VisionOperationFeatures RequiredFeatures => VisionOperationFeatures.None;
    public ScalarMeasurementOutput Source
    {
        get;
    }

    public UniformPixelScaleCalibration Calibration
    {
        get;
    }

    public LengthToMillimetersOperation(ScalarMeasurementOutput source, UniformPixelScaleCalibration calibration)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(calibration);
        if (source.Unit != GeometryMeasurementUnit.Pixels || source.CoordinateSpaceId != calibration.CoordinateSpaceId)
        {
            throw new ArgumentException("只能换算适用像素空间中的像素长度，不猜跨裁剪/分支映射或角度单位。");
        }

        Source = source;
        Calibration = calibration;
    }

    protected override void ValidateParameters(List<VisionIssue> issues)
    {
    }
}
