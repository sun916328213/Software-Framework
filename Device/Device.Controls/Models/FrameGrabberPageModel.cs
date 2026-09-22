using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Device.Core.Devices;
using Device.Core.FrameGrabbers;

namespace Device.Controls.Models;

/// <summary>采集卡档案（对应 <see cref="FrameGrabberOptions"/>）。</summary>
public partial class FrameGrabberProfile : ObservableObject, IDeviceProfile
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string driverId = string.Empty;

    /// <summary>厂商相机配置文件路径（Matrox DCF / Euresys CAM）。</summary>
    [ObservableProperty]
    private string configurationFilePath = string.Empty;

    [ObservableProperty]
    private int boardIndex;

    [ObservableProperty]
    private string connector = "A";

    [ObservableProperty]
    private string pixelFormat = "Mono8";

    [ObservableProperty]
    private int bufferCount = 2;

    public FrameGrabberOptions ToOptions() => new()
    {
        Name = Name,
        DriverId = new DeviceDriverId(DriverId),
        ConfigurationFilePath = ConfigurationFilePath,
        BoardIndex = BoardIndex,
        Connector = Connector,
        PixelFormat = PixelFormat,
        BufferCount = BufferCount,
    };
}

/// <summary>采集卡页状态：连续采集帧计数与最新帧预览。</summary>
public partial class FrameGrabberPageModel : DevicePageModelBase<FrameGrabberProfile>
{
    public FrameGrabberPageModel()
        : base("FrameGrabbers.json")
    {
    }

    /// <summary>本次连接已收到的帧数。</summary>
    [ObservableProperty]
    private long frameCount;

    /// <summary>最近一帧的渲染结果。</summary>
    [ObservableProperty]
    private ImageSource? preview;

    /// <summary>最近一帧的规格说明。</summary>
    [ObservableProperty]
    private string frameInfo = "尚无图像";

    /// <summary>收到新帧时更新预览与计数（UI 线程调用）。</summary>
    public void ShowFrame(ImageSource image, int width, int height, string pixelFormat, long sequence)
    {
        Preview = image;
        FrameCount++;
        FrameInfo = $"{width}×{height} {pixelFormat} · 帧号 {sequence} · 共 {FrameCount} 帧";
    }
}
