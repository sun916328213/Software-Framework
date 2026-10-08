// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ImageFrameRendererTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Workbench、System.Windows.Media、Device.Controls.Services、Device.Core.Cameras、Xunit。导入不代表所有类型都会在运行时加载。
using Device.Workbench;
using System.Windows.Media;
using Device.Controls.Services;
using Device.Core.Cameras;
using Xunit;

namespace Device.Workbench.Tests;

/// <summary>
/// ImageFrameRendererTests 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class ImageFrameRendererTests
{
    /// <summary>
    /// 测试相关入口 Render_Mono8_ProducesGray8Bitmap；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Render、Equal、True；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Render_Bgr24_KeepsChannelOrderAndUsesBgr24；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Render、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Render_Rgb24_SwapsChannelsForBgr24Target；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Render、CopyPixels、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Render_Mono16_DownsamplesHighBytesToGray8；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Render、CopyPixels、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Render_ZeroSize_Throws；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Render；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Render_ZeroSize_Throws()
    {
        var frame = new ImageFrame(0, 0, "Mono8", ReadOnlyMemory<byte>.Empty);

        Assert.Throws<NotSupportedException>(() => ImageFrameRenderer.Render(frame));
    }

    /// <summary>
    /// 测试相关入口 Render_UnsupportedDataLength_ThrowsWithFormatName；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Render、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Render_UnsupportedDataLength_ThrowsWithFormatName()
    {
        var frame = new ImageFrame(2, 2, "YUV422", new byte[7]);

        var ex = Assert.Throws<NotSupportedException>(() => ImageFrameRenderer.Render(frame));

        Assert.Contains("YUV422", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 Render_NullFrame_Throws；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Render；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Render_NullFrame_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ImageFrameRenderer.Render(null!));
    }
}
