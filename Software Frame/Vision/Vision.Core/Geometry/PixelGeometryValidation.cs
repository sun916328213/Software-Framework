#nullable enable

namespace Vision.Core.Geometry;

internal static class PixelGeometryValidation
{
    internal static void RequireSameSource(Guid firstFrame, Guid firstSpace, Guid secondFrame, Guid secondSpace)
    {
        if (firstFrame != secondFrame || firstSpace != secondSpace)
        {
            throw new ArgumentException("几何运算必须来自同一帧及像素空间；缺少明确映射时不能跨分支或裁剪运算。");
        }
    }
}
