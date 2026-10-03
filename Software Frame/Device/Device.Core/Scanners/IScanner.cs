// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：IScanner。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;

namespace Device.Core.Scanners;

/// <summary>
/// 被动上报型扫码器的公共接口，不暴露串口或厂商通信类型。
/// </summary>
public interface IScanner : IDevice
{
    /// <summary>创建扫码器时使用的配置快照。</summary>
    ScannerOptions Options { get; }

    /// <summary>收到一条完整扫码数据时触发。</summary>
    event EventHandler<ScanReceivedEventArgs>? ScanReceived;
}
