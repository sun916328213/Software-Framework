using System.Windows.Media;
using System.Windows.Media.Imaging;
using Device.Core.Cameras;

namespace Device.Controls.Services;

/// <summary>
/// 把与 SDK 无关的 <see cref="ImageFrame"/> 渲染成 WPF 可显示的位图。
/// 仅按「像素格式名 + 数据长度」判定，不依赖任何厂商转换库。
/// </summary>
public static class ImageFrameRenderer
{
    /// <summary>渲染一帧图像；无法识别的数据抛业务异常。</summary>
    public static BitmapSource Render(ImageFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var pixelCount = frame.Width * frame.Height;
        if (pixelCount <= 0)
            throw new NotSupportedException("图像尺寸无效。");

        var data = frame.Data;
        var format = (frame.PixelFormat ?? string.Empty).Trim().ToLowerInvariant();

        byte[] buffer;
        PixelFormat target;

        if (data.Length == pixelCount)
        {
            // Mono8 与 Bayer 原始 8 位数据按灰度直接呈现（帧说明里会显示真实格式名）。
            buffer = data.ToArray();
            target = PixelFormats.Gray8;
        }
        else if (data.Length == pixelCount * 3)
        {
            buffer = format.Contains("rgb") && !format.Contains("bgr")
                ? SwapRedBlue(data.Span)
                : data.ToArray();
            target = PixelFormats.Bgr24;
        }
        else if (data.Length == pixelCount * 2)
        {
            // 16 位单通道（Mono10/12/16 等小端数据）取高字节降位显示。
            buffer = Downsample16To8(data.Span);
            target = PixelFormats.Gray8;
        }
        else
        {
            throw new NotSupportedException(
                $"暂不支持像素格式 '{frame.PixelFormat}'（{frame.Width}×{frame.Height}，{data.Length} 字节）。");
        }

        var stride = frame.Width * target.BitsPerPixel / 8;

        var bitmap = BitmapSource.Create(frame.Width, frame.Height, 96, 96, target, null, buffer, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] SwapRedBlue(ReadOnlySpan<byte> source)
    {
        var result = new byte[source.Length];
        for (var i = 0; i + 2 < source.Length; i += 3)
        {
            result[i] = source[i + 2];
            result[i + 1] = source[i + 1];
            result[i + 2] = source[i];
        }

        return result;
    }

    private static byte[] Downsample16To8(ReadOnlySpan<byte> source)
    {
        var result = new byte[source.Length / 2];
        for (var i = 0; i < result.Length; i++)
            result[i] = source[i * 2 + 1];

        return result;
    }
}
