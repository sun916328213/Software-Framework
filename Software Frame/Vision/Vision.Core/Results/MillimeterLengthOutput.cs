#nullable enable
using Vision.Core.Calibration;

namespace Vision.Core.Results;

/// <summary>带原像素测量和明确均匀比例依据的毫米长度，不修改原标量单位，不暴露原生模型。</summary>
public sealed class MillimeterLengthOutput : IMillimeterLengthMeasurement
{
    public ScalarMeasurementOutput SourcePixels
    {
        get;
    }

    public UniformPixelScaleCalibration Calibration
    {
        get;
    }

    public double ValueMillimeters
    {
        get;
    }

    public Guid CalibrationId => Calibration.CalibrationId;
    public Guid FrameId => SourcePixels.FrameId;
    public Guid CoordinateSpaceId => SourcePixels.CoordinateSpaceId;

    public MillimeterLengthOutput(ScalarMeasurementOutput sourcePixels, UniformPixelScaleCalibration calibration, double valueMillimeters)
    {
        ArgumentNullException.ThrowIfNull(sourcePixels);
        ArgumentNullException.ThrowIfNull(calibration);
        if (sourcePixels.Unit != GeometryMeasurementUnit.Pixels || sourcePixels.CoordinateSpaceId != calibration.CoordinateSpaceId
            || !double.IsFinite(valueMillimeters) || valueMillimeters < 0 || sourcePixels.Value > 0 && valueMillimeters == 0
            || valueMillimeters != sourcePixels.Value * calibration.MillimetersPerPixel)
        {
            throw new ArgumentException("毫米长度必须有同空间像素来源和有效标定，不发布非有限值或正长度下溢为零。");
        }

        SourcePixels = sourcePixels;
        Calibration = calibration;
        ValueMillimeters = valueMillimeters;
    }
}
