#nullable enable
using Vision.Core.Calibration;
using Vision.Core.Geometry;

namespace Vision.Core.Results;

/// <summary>毫米世界点经显式逆标定映射到调用方指定目标帧，不猜当前图像或裁切视野。</summary>
public sealed class ReprojectedPixelPointOutput
{
    public MillimeterPoint Source
    {
        get;
    }

    public AffinePixelCalibration Calibration
    {
        get;
    }

    public PixelPoint Point
    {
        get;
    }

    public ReprojectedPixelPointOutput(MillimeterPoint source, AffinePixelCalibration calibration, PixelPoint point)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(calibration);
        ArgumentNullException.ThrowIfNull(point);
        if (source.WorldSpaceId != calibration.WorldSpaceId || point.CoordinateSpaceId != calibration.PixelSpaceId
            || calibration.MillimetersToPixel.Transform(source.PositionMillimeters) != point.Position)
        {
            throw new ArgumentException("逆映射必须使用明确世界空间与目标像素空间。");
        }
        Source = source;
        Calibration = calibration;
        Point = point;
    }
}
