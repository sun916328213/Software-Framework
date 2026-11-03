#nullable enable
using Vision.Core.Geometry;

namespace Vision.Core.Calibration;

/// <summary>一个显式像素点与同一物理平面的已知毫米参考点，标定不负责识别物理标记。</summary>
public sealed class CalibrationPointPair
{
    public PixelPoint Pixel
    {
        get;
    }

    public MillimeterPoint World
    {
        get;
    }

    public CalibrationPointPair(PixelPoint pixel, MillimeterPoint world)
    {
        Pixel = pixel ?? throw new ArgumentNullException(nameof(pixel));
        World = world ?? throw new ArgumentNullException(nameof(world));
    }
}
