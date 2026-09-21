using System.Windows.Media;
using System.Windows.Media.Imaging;
using Device.Core.FrameGrabbers;

namespace Device.Controls.Services;

/// <summary>
/// 把带行填充（Stride）的采集卡帧渲染成 WPF 位图。
/// 与 <see cref="ImageFrameRenderer"/>（相机帧）同思路，但必须按 Stride 逐行去填充。
/// </summary>
public static class FrameGrabberFrameRenderer
{
    /// <summary>渲染一帧；无法识别的 尺寸/Stride/长度 组合抛业务异常。</summary>
    public static BitmapSource Render(FrameGrabberFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var (width, height) = (frame.Width, frame.Height);
        var pixelCount = width * height;
        if (pixelCount <= 0)
            throw new NotSupportedException("采集卡帧尺寸无效。");

        var data = frame.Data.Span;
        var format = (frame.PixelFormat ?? string.Empty).Trim().ToLowerInvariant();

        var bytesPerPixel = GuessBytesPerPixel(data.Length, width, height, frame.Stride);
        var rowBytes = width * bytesPerPixel;
        var stride = frame.Stride >= rowBytes ? frame.Stride : rowBytes;
        if (data.Length < stride * (height - 1) + rowBytes)
            throw new NotSupportedException(
                $"帧数据长度不足（{data.Length} 字节，{width}×{height}，Stride {frame.Stride}，格式 {frame.PixelFormat}）。");

        PixelFormat target = bytesPerPixel switch
        {
            3 => PixelFormats.Bgr24,
            4 => PixelFormats.Bgra32,
            _ => PixelFormats.Gray8,
        };
        var dstStride = (width * (target.BitsPerPixel / 8) + 3) & ~3;
        var buffer = new byte[dstStride * height];

        for (var y = 0; y < height; y++)
        {
            var srcRow = data.Slice(y * stride, rowBytes);
            var dstRow = buffer.AsSpan(y * dstStride, width * (target.BitsPerPixel / 8));
            switch (bytesPerPixel)
            {
                case 1:
                    srcRow.CopyTo(dstRow);
                    break;
                case 2:
                    // 16 位单通道取高字节降位显示（与相机渲染一致）。
                    for (var x = 0; x < width; x++)
                        dstRow[x] = srcRow[x * 2 + 1];
                    break;
                case 3:
                    CopyRowRgb(srcRow, dstRow, swapRedBlue:
                        format.Contains("rgb") && !format.Contains("bgr"));
                    break;
                default:
                    // 32 位按 BGRA/BGRA 混合处理：含 "rgb" 且不含 "bgr" 时换 R/B。
                    CopyRowRgba(srcRow, dstRow, swapRedBlue:
                        format.Contains("rgb") && !format.Contains("bgr"));
                    break;
            }
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, target, null, buffer, dstStride);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>依据总长度/宽高/Stride 推断每像素字节数；无法解释时抛异常。</summary>
    private static int GuessBytesPerPixel(int length, int width, int height, int declaredStride)
    {
        foreach (var bpp in new[] { 1, 2, 3, 4 })
        {
            var rowBytes = width * bpp;
            var alignedRow = (rowBytes + 3) & ~3;
            var stride = declaredStride >= rowBytes ? declaredStride : rowBytes;

            if (length == stride * height && (stride == rowBytes || stride == alignedRow))
                return bpp;

            // 无填充的紧凑数据也接受。
            if (length == rowBytes * height && declaredStride < rowBytes)
                return bpp;
        }

        throw new NotSupportedException(
            $"无法按 宽高({width}×{height})/Stride({declaredStride}) 解释帧长度 {length} 字节。");
    }

    private static void CopyRowRgb(ReadOnlySpan<byte> src, Span<byte> dst, bool swapRedBlue)
    {
        for (var x = 0; x < dst.Length / 3; x++)
        {
            if (swapRedBlue)
            {
                dst[x * 3] = src[x * 3 + 2];
                dst[x * 3 + 1] = src[x * 3 + 1];
                dst[x * 3 + 2] = src[x * 3];
            }
            else
            {
                src.Slice(x * 3, 3).CopyTo(dst.Slice(x * 3, 3));
            }
        }
    }

    private static void CopyRowRgba(ReadOnlySpan<byte> src, Span<byte> dst, bool swapRedBlue)
    {
        for (var x = 0; x < dst.Length / 4; x++)
        {
            if (swapRedBlue)
            {
                dst[x * 4] = src[x * 4 + 2];
                dst[x * 4 + 1] = src[x * 4 + 1];
                dst[x * 4 + 2] = src[x * 4];
                dst[x * 4 + 3] = src[x * 4 + 3];
            }
            else
            {
                src.Slice(x * 4, 4).CopyTo(dst.Slice(x * 4, 4));
            }
        }
    }
}
