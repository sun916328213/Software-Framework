using System.Runtime.InteropServices;
using OpenCvSharp;
using Vision.Core.Imaging;
using Vision.Core.Roi;

namespace Vision.Tools.OpenCv.Imaging;

/// <summary>
/// Core快照与Mat的复制边界。不改变格式解释，不读取行尾padding，也不借用托管指针。
/// RGB与BGR的存储类型相同，但通道含义由明确的转换操作处理。
/// </summary>
internal static class OpenCvFrameAdapter
{
    internal static Mat RegionMask(VisionFrame frame, SearchRegion region)
    {
        var pixels = new byte[checked(frame.Width * frame.Height)];
        foreach (var run in region.Runs(frame.Width, frame.Height))
        {
            pixels.AsSpan(run.Row * frame.Width + run.Start, run.End - run.Start + 1).Fill(255);
        }

        var result = new Mat(frame.Height, frame.Width, MatType.CV_8UC1);
        try
        {
            Marshal.Copy(pixels, 0, result.Data, pixels.Length);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>灰度算法专用守卫，不能因通用输入边界支持颜色而隐式转灰。</summary>
    internal static Mat CopyGray(VisionFrame frame)
    {
        if (frame.Format is not (PixelFormat.Mono8 or PixelFormat.Mono16))
        {
            throw new NotSupportedException("灰度输入只支持Mono8/Mono16，不隐式转换格式。");
        }

        return Copy(frame);
    }

    internal static Mat Copy(VisionFrame frame)
    {
        var rowBytes = checked(frame.Width * BytesPerPixel(frame.Format));
        var bytes = new byte[checked(rowBytes * frame.Height)];
        for (var row = 0; row < frame.Height; row++)
        {
            frame.Pixels.Slice(row * frame.Stride, rowBytes).CopyTo(bytes.AsSpan(row * rowBytes));
        }

        var result = new Mat(frame.Height, frame.Width, NativeType(frame.Format));
        try
        {
            Marshal.Copy(bytes, 0, result.Data, bytes.Length);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>逐行复制输出，允许输入为非连续Mat视图，结果不依赖原生对象存活。</summary>
    internal static VisionFrame Snapshot(Mat image, PixelFormat format, Guid space)
    {
        if (image.Empty() || image.Type() != NativeType(format))
        {
            throw new InvalidDataException("OpenCV输出存储类型与声明的像素格式不符。");
        }

        var rowBytes = checked(image.Width * BytesPerPixel(format));
        var pixels = new byte[checked(rowBytes * image.Height)];
        for (var row = 0; row < image.Height; row++)
        {
            Marshal.Copy(image.Ptr(row), pixels, row * rowBytes, rowBytes);
        }

        return new VisionFrame(Guid.NewGuid(), space, image.Width, image.Height, format, pixels);
    }

    /// <summary>解码器颜色结果按BGR/BGRA解释，16位多通道等未定义格式明确拒绝。</summary>
    internal static PixelFormat DecodedFormat(Mat image)
    {
        return image.Type() switch
        {
            var type when type == MatType.CV_8UC1 => PixelFormat.Mono8,
            var type when type == MatType.CV_16UC1 => PixelFormat.Mono16,
            var type when type == MatType.CV_8UC3 => PixelFormat.Bgr24,
            var type when type == MatType.CV_8UC4 => PixelFormat.Bgra32,
            var type when type == MatType.CV_32FC1 => PixelFormat.Float32,
            _ => throw new NotSupportedException("文件解码像素格式尚未接通，不自动降位深或丢弃通道。")
        };
    }

    private static MatType NativeType(PixelFormat format) => format switch
    {
        PixelFormat.Mono8 => MatType.CV_8UC1,
        PixelFormat.Mono16 => MatType.CV_16UC1,
        PixelFormat.Rgb24 or PixelFormat.Bgr24 => MatType.CV_8UC3,
        PixelFormat.Bgra32 => MatType.CV_8UC4,
        PixelFormat.Float32 => MatType.CV_32FC1,
        _ => throw new NotSupportedException("未知像素格式。")
    };

    private static int BytesPerPixel(PixelFormat format) => format switch
    {
        PixelFormat.Mono8 => 1,
        PixelFormat.Mono16 => 2,
        PixelFormat.Rgb24 or PixelFormat.Bgr24 => 3,
        PixelFormat.Bgra32 or PixelFormat.Float32 => 4,
        _ => throw new NotSupportedException("未知像素格式。")
    };
}
