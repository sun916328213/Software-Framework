#nullable enable

namespace Vision.Core.Imaging;

/// <summary>
/// 新执行入口的独立像素快照。X向右、Y向下；同一采集帧可有不同坐标空间（例如Crop）。
/// 不向外暴露可写数组或SDK对象；缓冲仅由该值对象持有，不需要原生释放。
/// </summary>
public sealed class VisionFrame
{
    private readonly byte[] _pixels;
    /// <summary>
    /// 调用方提供的输入帧身份，不是文件路径、采集计数或SDK句柄；派生身份由操作结果契约规定。
    /// </summary>
    public Guid FrameId
    {
        get;
    }

    /// <summary>
    /// 像素坐标空间身份；数值相同不授权推断跨裁剪/处理的来源等价。
    /// </summary>
    public Guid CoordinateSpaceId
    {
        get;
    }

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
    /// 每行占用的字节数，包含行尾填充。
    /// </summary>
    public int Stride
    {
        get;
    }

    /// <summary>
    /// 厂商中立的像素格式。
    /// </summary>
    public PixelFormat Format
    {
        get;
    }

    /// <summary>
    /// 只读像素缓冲，不暴露可写数组或原生句柄。
    /// </summary>
    public ReadOnlySpan<byte> Pixels => _pixels;

    /// <summary>
    /// 创建独立图像快照，校验尺寸、格式、步长并复制像素。
    /// </summary>
    public VisionFrame(
        Guid frameId,
        Guid coordinateSpaceId,
        int width,
        int height,
        PixelFormat format,
        ReadOnlySpan<byte> pixels,
        int stride = 0)
    {
        if (frameId == Guid.Empty || coordinateSpaceId == Guid.Empty)
        {
            throw new ArgumentException("必须显式提供帧和坐标空间身份。");
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        var bytesPerPixel = format switch
        {
            PixelFormat.Mono8 => 1,
            PixelFormat.Mono16 => 2,
            PixelFormat.Float32 => 4,
            PixelFormat.Rgb24 or PixelFormat.Bgr24 => 3,
            PixelFormat.Bgra32 => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        var minimumStride = checked(width * bytesPerPixel);
        if (stride < 0 || (stride > 0 && stride < minimumStride))
        {
            throw new ArgumentOutOfRangeException(nameof(stride));
        }

        var actualStride = stride == 0 ? minimumStride : stride;
        var length = checked(actualStride * height);
        if (pixels.Length < length)
        {
            throw new ArgumentException("像素缓冲不足。", nameof(pixels));
        }

        FrameId = frameId;
        CoordinateSpaceId = coordinateSpaceId;
        Width = width;
        Height = height;
        Format = format;
        Stride = actualStride;
        _pixels = pixels[..length].ToArray();
    }
}
