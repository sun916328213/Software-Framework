#nullable enable

namespace Vision.Core.Imaging;

/// <summary>
/// 无SDK的二值参与域：0忽略，1/255参与；归一为0/255。不是加权掩膜。
/// </summary>
public sealed class VisionMask
{
    private readonly byte[] _pixels;
    /// <summary>
    /// 图像宽度，单位为像素。
    /// </summary>
    public int Width
    {
        get;
    }

    /// <summary>
    /// 图像高度，单位为像素。
    /// </summary>
    public int Height
    {
        get;
    }

    /// <summary>
    /// 只读像素缓冲，不暴露可写数组或原生句柄。
    /// </summary>
    public ReadOnlySpan<byte> Pixels => _pixels;

    /// <summary>
    /// 创建二值参与域，校验尺寸并将有效像素统一为二百五十五。
    /// </summary>
    public VisionMask(
        int width,
        int height,
        ReadOnlySpan<byte> pixels)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (pixels.Length != checked(width * height))
        {
            throw new ArgumentException("掩膜尺寸与像素数不符。", nameof(pixels));
        }

        _pixels = new byte[pixels.Length];
        var hasValidPixel = false;
        for (var i = 0; i < pixels.Length; i++)
        {
            if (pixels[i] is not (0 or 1 or 255))
            {
                throw new ArgumentException("只接受二值掩膜，不解释未知权重。", nameof(pixels));
            }

            if (pixels[i] == 0)
            {
                continue;
            }

            _pixels[i] = 255;
            hasValidPixel = true;
        }
        if (!hasValidPixel)
        {
            throw new ArgumentException("掩膜不能全部无效。", nameof(pixels));
        }

        Width = width;
        Height = height;
    }

    /// <summary>
    /// 参与域必须与其所在图像或模板逐像素对齐。
    /// </summary>
    public void EnsureSize(int width, int height)
    {
        if (Width != width || Height != height)
        {
            throw new ArgumentException("掩膜必须与所属图像同尺寸。");
        }
    }
}
