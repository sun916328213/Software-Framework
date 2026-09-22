using System.Windows.Media;
using Device.Controls.Services;
using Device.Core.Cameras;
using Xunit;

namespace Device.Controls.Tests;

public sealed class ImageFrameRendererTests
{
    [Fact]
    public void Render_Mono8_ProducesGray8Bitmap()
    {
        var frame = new ImageFrame(2, 2, "Mono8", new byte[] { 0, 64, 128, 255 });

        var bitmap = ImageFrameRenderer.Render(frame);

        Assert.Equal(2, bitmap.PixelWidth);
        Assert.Equal(2, bitmap.PixelHeight);
        Assert.Equal(PixelFormats.Gray8, bitmap.Format);
        Assert.True(bitmap.IsFrozen);
    }

    [Fact]
    public void Render_Bgr24_KeepsChannelOrderAndUsesBgr24()
    {
        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        var frame = new ImageFrame(2, 2, "BGR8", data);

        var bitmap = ImageFrameRenderer.Render(frame);

        Assert.Equal(PixelFormats.Bgr24, bitmap.Format);
        Assert.Equal(2, bitmap.PixelWidth);
        Assert.Equal(2, bitmap.PixelHeight);
    }

    [Fact]
    public void Render_Rgb24_SwapsChannelsForBgr24Target()
    {
        // 纯红像素 (255,0,0) 按 RGB 存入；渲染到 Bgr24 前必须交换 R/B。
        var data = new byte[] { 255, 0, 0 };
        var frame = new ImageFrame(1, 1, "RGB8", data);

        var bitmap = ImageFrameRenderer.Render(frame);
        var pixels = new byte[3];
        bitmap.CopyPixels(pixels, 3, 0);

        Assert.Equal(PixelFormats.Bgr24, bitmap.Format);
        Assert.Equal([0, 0, 255], pixels);
    }

    [Fact]
    public void Render_Mono16_DownsamplesHighBytesToGray8()
    {
        // 小端 16 位：低字节 0x11，高字节 0xAB → 显示取 0xAB。
        var frame = new ImageFrame(1, 1, "Mono16", new byte[] { 0x11, 0xAB });

        var bitmap = ImageFrameRenderer.Render(frame);
        var pixels = new byte[1];
        bitmap.CopyPixels(pixels, 1, 0);

        Assert.Equal(PixelFormats.Gray8, bitmap.Format);
        Assert.Equal(0xAB, pixels[0]);
    }

    [Fact]
    public void Render_ZeroSize_Throws()
    {
        var frame = new ImageFrame(0, 0, "Mono8", ReadOnlyMemory<byte>.Empty);

        Assert.Throws<NotSupportedException>(() => ImageFrameRenderer.Render(frame));
    }

    [Fact]
    public void Render_UnsupportedDataLength_ThrowsWithFormatName()
    {
        var frame = new ImageFrame(2, 2, "YUV422", new byte[7]);

        var ex = Assert.Throws<NotSupportedException>(() => ImageFrameRenderer.Render(frame));

        Assert.Contains("YUV422", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_NullFrame_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ImageFrameRenderer.Render(null!));
    }
}
