#nullable enable
using Vision.Core.Geometry;

namespace Vision.Core.Calibration;

/// <summary>
/// 单个已知长度建立的同一像素空间均匀比例标定值。只表达mm/px，不提供原点、仿射/透视/畸变校正。
/// 两点精确约束不等于独立精度验收，因此不虚构零残差/置信度；物理平面和光学条件适用性由调用方确认。
/// </summary>
public sealed class UniformPixelScaleCalibration
{
    public Guid CalibrationId
    {
        get;
    }

    public PixelPoint First
    {
        get;
    }

    public PixelPoint Second
    {
        get;
    }

    public double ReferencePixelLength
    {
        get;
    }

    public double KnownLengthMillimeters
    {
        get;
    }

    public double MillimetersPerPixel
    {
        get;
    }

    public Guid CoordinateSpaceId => First.CoordinateSpaceId;

    public UniformPixelScaleCalibration(Guid calibrationId, PixelPoint first, PixelPoint second,
        double referencePixelLength, double knownLengthMillimeters, double millimetersPerPixel)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        PixelGeometryValidation.RequireSameSource(first.FrameId, first.CoordinateSpaceId, second.FrameId, second.CoordinateSpaceId);
        var actualPixels = double.Hypot(second.Position.X - first.Position.X, second.Position.Y - first.Position.Y);
        if (calibrationId == Guid.Empty || !double.IsFinite(referencePixelLength) || referencePixelLength < 1
            || actualPixels != referencePixelLength
            || !double.IsFinite(knownLengthMillimeters) || knownLengthMillimeters <= 0
            || !double.IsFinite(millimetersPerPixel) || millimetersPerPixel <= 0
            || knownLengthMillimeters / referencePixelLength != millimetersPerPixel)
        {
            throw new ArgumentException("比例标定必须有有效身份、至少1像素的参考长度及可表示的正mm/px比例。");
        }

        CalibrationId = calibrationId;
        First = first;
        Second = second;
        ReferencePixelLength = referencePixelLength;
        KnownLengthMillimeters = knownLengthMillimeters;
        MillimetersPerPixel = millimetersPerPixel;
    }
}
