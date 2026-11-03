using Vision.Core.Imaging;
using Vision.Tools.Halcon.Internal;
using Xunit;

namespace Vision.Tests.Tools.Halcon;

[Trait("Category", "RequiresRuntime")]
public sealed class HalconImageSnapshotRuntimeTests
{
    [Theory(Skip = "需要 HALCON Runtime 和 License；仅在授权离线 SDK 环境手动执行。")]
    [InlineData(PixelFormat.Mono8, false)]
    [InlineData(PixelFormat.Mono8, true)]
    [InlineData(PixelFormat.Mono16, false)]
    [InlineData(PixelFormat.Mono16, true)]
    [InlineData(PixelFormat.Rgb24, false)]
    [InlineData(PixelFormat.Rgb24, true)]
    [InlineData(PixelFormat.Bgr24, false)]
    [InlineData(PixelFormat.Bgr24, true)]
    [InlineData(PixelFormat.Bgra32, false)]
    [InlineData(PixelFormat.Bgra32, true)]
    public void Snapshot_PreservesOddWidthPixelsAndChannels_AndOutlivesBorrowedImage(PixelFormat format, bool padded)
    {
        byte[] packed = format switch
        {
            PixelFormat.Mono8 => [0, 127, 255, 1, 128, 254],
            PixelFormat.Mono16 => [0, 0, 255, 255, 0, 128, 52, 18, 1, 0, 254, 255],
            // Alpha=0/128/255仍只丢第4字节，不合成背景、不还原预乘。
            PixelFormat.Bgra32 => [255, 0, 0, 0, 0, 255, 0, 128, 0, 0, 255, 255, 1, 2, 3, 17, 4, 5, 6, 200, 7, 8, 9, 0],
            _ => [255, 0, 0, 0, 255, 0, 0, 0, 255, 1, 2, 3, 4, 5, 6, 7, 8, 9]
        };
        var rowBytes = packed.Length / 2; var stride = padded ? rowBytes + 3 : rowBytes;
        var pixels = Enumerable.Repeat((byte)222, stride * 2).ToArray();
        Array.Copy(packed, 0, pixels, 0, rowBytes); Array.Copy(packed, rowBytes, pixels, stride, rowBytes);
        var input = VisionImage.FromBuffer(3, 2, format, pixels, stride);
        var expected = packed.ToArray();
        if (format == PixelFormat.Bgr24)
            for (var i = 0; i < expected.Length; i += 3) (expected[i], expected[i + 2]) = (expected[i + 2], expected[i]);
        if (format == PixelFormat.Bgra32)
        {
            expected = new byte[18];
            for (var i = 0; i < 6; i++)
            { expected[3 * i] = packed[4 * i + 2]; expected[3 * i + 1] = packed[4 * i + 1]; expected[3 * i + 2] = packed[4 * i]; }
        }
        VisionImage snapshot;
        using (var native = HalconImageConverter.FromDisplaySnapshot(input))
        {
            // 原缓冲在读取原生图像前改写，验证生成图像不借用调用方像素。
            Array.Clear(pixels);
            snapshot = HalconImageConverter.ToVisionImage(native, "display");
            Assert.Equal((3, 2), HalconImageConverter.GetSize(native));
        }
        Assert.Equal(3, snapshot.Width); Assert.Equal(2, snapshot.Height); Assert.Equal("display", snapshot.Source);
        Assert.Equal(format is PixelFormat.Bgr24 or PixelFormat.Bgra32 ? PixelFormat.Rgb24 : format, snapshot.Format);
        Assert.Equal(expected, snapshot.Pixels); // 原生释放后独立快照继续有效。
    }
}
