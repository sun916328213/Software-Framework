using Vision.Core.Geometry;
using Vision.Core.Imaging;

namespace Vision.Tests.Tools.Engine;

/// <summary>独立算术纹理和逆映射栅格，不调用被测匹配器或旧工具来计算期望图像。</summary>
internal static class EngineSyntheticFrames
{
    internal static VisionFrame Pattern(int width = 24, int height = 16)
    {
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[y * width + x] = (byte)(30 + (x * 31 + y * 17 + x * y * 7) % 190);
            }
        }

        return new(Guid.NewGuid(), Guid.NewGuid(), width, height, PixelFormat.Mono8, pixels);
    }

    internal static (VisionFrame Image, Point2D Center) Scene(VisionFrame template, double angle, double scale)
    {
        const int width = 160;
        const int height = 120;
        var radians = angle * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        if (Math.Abs(cos) < 1e-12)
        {
            cos = 0;
        }

        if (Math.Abs(sin) < 1e-12)
        {
            sin = 0;
        }

        var center = new Point2D(
            40 + Math.Ceiling(scale * (Math.Abs(cos) * template.Width + Math.Abs(sin) * template.Height)) / 2,
            30 + Math.Ceiling(scale * (Math.Abs(sin) * template.Width + Math.Abs(cos) * template.Height)) / 2);
        var pixels = Enumerable.Repeat((byte)47, width * height).ToArray();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var dx = x + 0.5 - center.X;
                var dy = y + 0.5 - center.Y;
                var u = (cos * dx + sin * dy) / scale + (template.Width - 1) / 2d;
                var v = (-sin * dx + cos * dy) / scale + (template.Height - 1) / 2d;
                if (u < 0 || v < 0 || u > template.Width - 1 || v > template.Height - 1)
                {
                    continue;
                }

                var left = (int)Math.Floor(u);
                var top = (int)Math.Floor(v);
                var right = Math.Min(left + 1, template.Width - 1);
                var bottom = Math.Min(top + 1, template.Height - 1);
                var a = u - left;
                var b = v - top;
                var value = (1 - a) * (1 - b) * template.Pixels[top * template.Stride + left]
                    + a * (1 - b) * template.Pixels[top * template.Stride + right]
                    + (1 - a) * b * template.Pixels[bottom * template.Stride + left]
                    + a * b * template.Pixels[bottom * template.Stride + right];
                pixels[y * width + x] = (byte)Math.Clamp(Math.Round(value), 0, 255);
            }
        }

        return (new VisionFrame(Guid.NewGuid(), Guid.NewGuid(), width, height, PixelFormat.Mono8, pixels), center);
    }
}
