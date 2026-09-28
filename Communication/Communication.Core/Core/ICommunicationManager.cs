namespace CommunicationNew.Core;

/// <summary>
/// P11 Core seam：设备管理器的最小契约。
/// 实现位于 Runtime（<c>CommunicationManager</c>）；Controls/Workbench 只依赖此接口，保持 Core-only。
/// </summary>
public interface ICommunicationManager : IDisposable, IAsyncDisposable
{
    /// <summary>所有已注册的设备。</summary>
    IReadOnlyCollection<ICommunicationDevice> Devices { get; }

    /// <summary>创建并注册一个通信设备。</summary>
    ICommunicationDevice CreateDevice(CommunicationParam param);

    /// <summary>注册已有设备（所有权转移给管理器）。</summary>
    void RegisterDevice(ICommunicationDevice device);

    /// <summary>按 ID 获取设备。</summary>
    ICommunicationDevice? GetDevice(string deviceId);

    /// <summary>按名称获取设备。</summary>
    ICommunicationDevice? GetDeviceByName(string name);

    /// <summary>按协议类型获取设备列表。</summary>
    List<ICommunicationDevice> GetDevicesByType(ProtocolType type);

    /// <summary>移除并释放设备。</summary>
    Task<bool> RemoveDeviceAsync(string deviceId);

    /// <summary>连接所有设备。</summary>
    Task ConnectAllAsync(CancellationToken ct = default);

    /// <summary>断开所有设备。</summary>
    Task DisconnectAllAsync(CancellationToken ct = default);
}
