using System.Security.Cryptography;
using OpenCvSharp;
using Vision.Core.Imaging;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.OpenCv.Imaging;

namespace Vision.Tools.OpenCv.Engine;

internal sealed partial class OpenCvBackend
{
    // 编码文件读取预算，不是解码器原生内存分配的硬隔离上限。
    private const int MaximumEncodedImageBytes = 64 * 1024 * 1024;

    private static ImageFileOutput ReadFile(ImageFileOperation operation, CancellationToken cancellationToken)
    {
        using var file = new FileStream(operation.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length <= 0 || file.Length > MaximumEncodedImageBytes)
        {
            throw new InvalidDataException("图像编码文件为空或超过64MiB读取预算。");
        }

        // 只解码此次实际读取的字节，指纹与送入SDK的内容来自同一快照。
        var bytes = new byte[(int)file.Length];
        file.ReadExactly(bytes);
        cancellationToken.ThrowIfCancellationRequested();
        using var decoded = Cv2.ImDecode(bytes, ImreadModes.Unchanged);
        if (decoded.Empty())
        {
            throw new InvalidDataException("文件没有可解码的主图像。");
        }

        var format = OpenCvFrameAdapter.DecodedFormat(decoded);
        cancellationToken.ThrowIfCancellationRequested();
        var image = OpenCvFrameAdapter.Snapshot(decoded, format, Guid.NewGuid());
        return new ImageFileOutput(image, operation.FilePath, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private static bool SupportsConversion(PixelFormat input, PixelFormat output)
    {
        return input == PixelFormat.Mono8 && output == PixelFormat.Mono16
            || ColourConversion(input, output) != null;
    }

    private static ColorConversionCodes? ColourConversion(PixelFormat input, PixelFormat output) => (input, output) switch
    {
        (PixelFormat.Bgr24, PixelFormat.Mono8) => ColorConversionCodes.BGR2GRAY,
        (PixelFormat.Rgb24, PixelFormat.Mono8) => ColorConversionCodes.RGB2GRAY,
        (PixelFormat.Bgra32, PixelFormat.Mono8) => ColorConversionCodes.BGRA2GRAY,
        (PixelFormat.Bgr24, PixelFormat.Rgb24) => ColorConversionCodes.BGR2RGB,
        (PixelFormat.Rgb24, PixelFormat.Bgr24) => ColorConversionCodes.RGB2BGR,
        (PixelFormat.Bgra32, PixelFormat.Bgr24) => ColorConversionCodes.BGRA2BGR,
        (PixelFormat.Bgra32, PixelFormat.Rgb24) => ColorConversionCodes.BGRA2RGB,
        (PixelFormat.Bgr24, PixelFormat.Bgra32) => ColorConversionCodes.BGR2BGRA,
        (PixelFormat.Rgb24, PixelFormat.Bgra32) => ColorConversionCodes.RGB2BGRA,
        (PixelFormat.Mono8, PixelFormat.Bgr24 or PixelFormat.Rgb24) => ColorConversionCodes.GRAY2BGR,
        (PixelFormat.Mono8, PixelFormat.Bgra32) => ColorConversionCodes.GRAY2BGRA,
        _ => null
    };

    /// <summary>按语义RGB选择存储下标，只复制指定分量，不计算亮度或与alpha合成。</summary>
    private static ImageTransformOutput ExtractColorChannel(ColorChannelExtractOperation operation)
    {
        var frame = operation.Image;
        var channel = operation.Channel switch
        {
            RgbChannel.Red => frame.Format == PixelFormat.Rgb24 ? 0 : 2,
            RgbChannel.Green => 1,
            RgbChannel.Blue => frame.Format == PixelFormat.Rgb24 ? 2 : 0,
            _ => throw new NotSupportedException("未知RGB通道。")
        };
        using var source = OpenCvFrameAdapter.Copy(frame);
        using var extracted = new Mat();
        Cv2.ExtractChannel(source, extracted, channel);
        return new ImageTransformOutput(frame.FrameId,
            OpenCvFrameAdapter.Snapshot(extracted, PixelFormat.Mono8, frame.CoordinateSpaceId));
    }

    private static ImageTransformOutput ConvertImage(ImageConvertOperation operation)
    {
        var frame = operation.Image;
        using var source = OpenCvFrameAdapter.Copy(frame);
        using var converted = new Mat();
        if (frame.Format == PixelFormat.Mono8 && operation.OutputFormat == PixelFormat.Mono16)
        {
            // 仅扩大整数存储，0..255保持原值，不自动乘257拉伸。
            source.ConvertTo(converted, MatType.CV_16UC1);
        }
        else
        {
            var code = ColourConversion(frame.Format, operation.OutputFormat)
                ?? throw new NotSupportedException("未支持的颜色转换组合。");
            Cv2.CvtColor(source, converted, code);
        }

        // 颜色转灰使用OpenCV的BT.601系数；BGRA丢弃alpha而非与背景合成。
        return new ImageTransformOutput(frame.FrameId,
            OpenCvFrameAdapter.Snapshot(converted, operation.OutputFormat, frame.CoordinateSpaceId));
    }

    private static ImageTransformOutput LinearGray(GrayLinearOperation operation)
    {
        var frame = operation.Image;
        using var source = OpenCvFrameAdapter.CopyGray(frame);
        using var scaled = new Mat();
        // 先以double执行，再显式限幅；避免巨大有限值在整数转换时溢出后错误变成0。
        source.ConvertTo(scaled, MatType.CV_64FC1, operation.Gain, operation.Offset);
        Cv2.Max(scaled, 0, scaled);
        Cv2.Min(scaled, operation.OutputFormat == PixelFormat.Mono8 ? byte.MaxValue : ushort.MaxValue, scaled);
        using var converted = new Mat();
        scaled.ConvertTo(converted, operation.OutputFormat == PixelFormat.Mono8 ? MatType.CV_8UC1 : MatType.CV_16UC1);
        return new ImageTransformOutput(frame.FrameId,
            OpenCvFrameAdapter.Snapshot(converted, operation.OutputFormat, frame.CoordinateSpaceId));
    }
}
