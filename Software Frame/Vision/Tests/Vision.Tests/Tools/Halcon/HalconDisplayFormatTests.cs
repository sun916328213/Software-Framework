using Vision.Core.Imaging;
using Vision.Tools.Halcon.Internal;
using Xunit;

namespace Vision.Tests.Tools.Halcon;

/// <summary>只验证共享紧凑快照和Native调用前拒绝；正向HALCON图像生成仍在RequiresRuntime。</summary>
[Trait("Category", "Contract")]
public sealed class HalconDisplayFormatTests
{
    [Theory]
    [InlineData(PixelFormat.Mono8)]
    [InlineData(PixelFormat.Mono16)]
    [InlineData(PixelFormat.Rgb24)]
    [InlineData(PixelFormat.Bgr24)]
    [InlineData(PixelFormat.Bgra32)]
    public void SharedSnapshot_CompactsOddWidthRows_PreservesRawChannelsAndOwnsPixels(PixelFormat format)
    {
        var bpp = format is PixelFormat.Mono8 ? 1 : format is PixelFormat.Mono16 ? 2 : format is PixelFormat.Bgra32 ? 4 : 3;
        var row = 3 * bpp; var stride = row + 3;
        var pixels = Enumerable.Repeat((byte)222, stride * 2).ToArray();
        var expected = Enumerable.Range(0, row * 2).Select(i => (byte)(i * 7)).ToArray();
        Array.Copy(expected, 0, pixels, 0, row); Array.Copy(expected, row, pixels, stride, row);
        var input = VisionImage.FromBuffer(3, 2, format, pixels, stride, "display-input"); input.FrameId = 51;
        var snapshot = VisionImageSnapshot.Copy(input);
        Assert.Equal(expected, snapshot.Pixels); Assert.Equal(format, snapshot.Format); Assert.Equal(0, snapshot.Stride);
        Assert.Equal(51, snapshot.FrameId); Assert.Equal(input.Timestamp, snapshot.Timestamp); Assert.Equal(input.Source, snapshot.Source);
        Array.Clear(pixels); Assert.Equal(expected, snapshot.Pixels);
        // 不调用HALCON正向入口：该断言仅证明公共打包，不能抵扣bgrx或SDK拷贝语义。
    }

    [Theory]
    [InlineData("buffer")]
    [InlineData("stride")]
    [InlineData("overflow")]
    public void BgraMalformedInput_IsValidatedAsSupportedFormat_BeforeAnyNativeCall(string invalid)
    {
        var image = new VisionImage { Width = 3, Height = 2, Format = PixelFormat.Bgra32, Pixels = new byte[32], Stride = 16 };
        if (invalid == "buffer") image.Pixels = [1];
        if (invalid == "stride") image.Stride = 11;
        if (invalid == "overflow") image.Width = int.MaxValue;
        if (invalid == "buffer") Assert.Throws<ArgumentException>(() => HalconImageConverter.FromDisplaySnapshot(image));
        if (invalid == "stride") Assert.Throws<ArgumentOutOfRangeException>(() => HalconImageConverter.FromDisplaySnapshot(image));
        if (invalid == "overflow") Assert.Throws<OverflowException>(() => HalconImageConverter.FromDisplaySnapshot(image));
    }

    [Theory]
    [InlineData(PixelFormat.Mono16)]
    [InlineData(PixelFormat.Rgb24)]
    [InlineData(PixelFormat.Bgr24)]
    [InlineData(PixelFormat.Bgra32)]
    public void DisplayFormatSupport_DoesNotRelaxLegacyAlgorithmMono8Contract(PixelFormat format)
    {
        var image = VisionImage.FromBuffer(1, 1, format, new byte[4]);
        Assert.Throws<NotSupportedException>(() => HalconImageConverter.FromVisionImage(image));
    }

    [Fact]
    public void UnsupportedFloatAndNegativeStride_RemainRejectedBeforeNativeCall()
    {
        var image = VisionImage.FromBuffer(1, 1, PixelFormat.Float32, new byte[4]);
        Assert.Throws<NotSupportedException>(() => HalconImageConverter.FromDisplaySnapshot(image));
        image.Format = PixelFormat.Mono8; image.Stride = -1;
        Assert.Throws<NotSupportedException>(() => HalconImageConverter.FromDisplaySnapshot(image));
        Assert.Throws<NotSupportedException>(() => HalconImageConverter.FromVisionImage(image));
    }
}
