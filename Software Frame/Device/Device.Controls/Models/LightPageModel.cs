// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：LightProfile、LightPageModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、CommunityToolkit.Mvvm.ComponentModel、Device.Core.Devices、Device.Core.Lights。导入不代表所有类型都会在运行时加载。
using Device.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Device.Core.Devices;
using Device.Core.Lights;

namespace Device.Controls.Models;

/// <summary>光源控制器档案（对应 <see cref="LightControllerOptions"/>）。</summary>
public partial class LightProfile : ObservableObject, IDeviceProfile
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string driverId = string.Empty;

    [ObservableProperty]
    private string portName = "COM1";

    [ObservableProperty]
    private int channelCount = 4;

    [ObservableProperty]
    private int maximumIntensity = 255;

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ToOptions。
    /// </summary>
    public LightControllerOptions ToOptions() => new()
    {
        Name = Name,
        DriverId = new DeviceDriverId(DriverId),
        PortName = PortName,
        ChannelCount = ChannelCount,
        MaximumIntensity = MaximumIntensity,
    };
}

/// <summary>光源页状态：通道号/亮度微调值。</summary>
public partial class LightPageModel : DevicePageModelBase<LightProfile>
{
    /// <summary>
    /// 初始化 LightPageModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public LightPageModel()
        : base("Lights.json")
    {
    }

    /// <summary>单通道设置的通道号（从 1 开始）。</summary>
    [ObservableProperty]
    private int channel = 1;

    /// <summary>单通道设置的亮度值。</summary>
    [ObservableProperty]
    private int intensity = 128;

    /// <summary>全通道统一亮度值。</summary>
    [ObservableProperty]
    private int allIntensity = 128;
}
