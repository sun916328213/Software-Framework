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
