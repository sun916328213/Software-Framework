namespace Vision.Core.Geometry;

/// <summary>
/// 仅对恰为正交角的输入采用精确三角值，保持半开像素边界；一般角度不做近似。
/// </summary>
internal static class RotationMath
{
    internal static (double Cos, double Sin) SinCosDegrees(double degrees) => (degrees % 360) switch
    {
        0 => (1d, 0d),
        90 or -270 => (0d, 1d),
        180 or -180 => (-1d, 0d),
        270 or -90 => (0d, -1d),
        _ => (Math.Cos(degrees * Math.PI / 180), Math.Sin(degrees * Math.PI / 180))
    };
}
