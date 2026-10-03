// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：FrameGrabberProfile、FrameGrabberPageModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、System.Windows.Media、CommunityToolkit.Mvvm.ComponentModel、Device.Core.Devices、Device.Core.FrameGrabbers。导入不代表所有类型都会在运行时加载。
using Device.Controls;
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ToOptions。
    /// </summary>
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
    /// <summary>
    /// 初始化 FrameGrabberPageModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
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
